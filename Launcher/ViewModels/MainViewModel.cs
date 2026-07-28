using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using Aetheria.Launcher.Models;
using Aetheria.Launcher.Services;
using Aetheria.Shared;
using Aetheria.Shared.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.Launcher.ViewModels;

/// <summary>
/// État et logique de l'écran unique du Launcher : formulaire de connexion/inscription, puis
/// lancement du Client (voir <c>Docs/GameDesign.md</c> — section Launcher). La sélection et la
/// création de personnage se font désormais EN JEU (voir Client/Program.cs — SceneMode.CharacterSelect
/// / CharacterCreate), pas ici : le Launcher se contente de fournir un jeton de session au Client.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private AccountApiClient _accountApi;

    [ObservableProperty]
    private string _title = $"{GameInfo.Name} Launcher — v{GameInfo.Version}";

    [ObservableProperty]
    private string _usernameOrEmail = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _sessionToken;

    [ObservableProperty]
    private bool _isServerOnline;

    [ObservableProperty]
    private string _serverStatusText = "Vérification du serveur...";

    /// <summary>Vrai si le serveur tourne une version différente de ce Launcher (voir GDD/demande utilisateur — afficher "Mettre à jour" à la place de "Jouer").</summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string? _serverVersion;

    /// <summary>Le gros bouton du bas est soit JOUER, soit METTRE À JOUR — jamais les deux (voir GDD).</summary>
    public bool ShowPlayButton => IsLoggedIn && !IsUpdateAvailable;
    public bool ShowUpdateButton => IsLoggedIn && IsUpdateAvailable;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private KeyboardLayoutPreference _keyboardLayoutPreference;

    /// <summary>Adresse du serveur (voir GDD/demande utilisateur — jeu installé ailleurs, serveur hébergé chez l'utilisateur). "localhost" par défaut.</summary>
    [ObservableProperty]
    private string _serverHost = "localhost";

    /// <summary>Tunnel ngrok éventuel pour l'API de compte (voir GDD/demande utilisateur — "utilise ngrok"), vide = pas de tunnel, on utilise ServerHost:7778 comme avant.</summary>
    [ObservableProperty]
    private string _accountApiBaseUrl = string.Empty;

    public IReadOnlyList<KeyboardLayoutPreference> AvailableKeyboardLayouts { get; } = Enum.GetValues<KeyboardLayoutPreference>();

    public string DetectedLayoutText => KeyboardLayoutResolver.IsAzertyDetected()
        ? "Détecté sur cette machine : AZERTY"
        : "Détecté sur cette machine : QWERTY";

    [ObservableProperty]
    private bool _isNewsDetailOpen;

    [ObservableProperty]
    private bool _isAllNewsOpen;

    [ObservableProperty]
    private NewsItem? _selectedNews;

    /// <summary>Les trois plus récentes, affichées directement dans le panneau (voir GDD — page "toutes les actualités").</summary>
    public ObservableCollection<NewsItem> RecentNews { get; } = [];

    public ObservableCollection<NewsItem> AllNews { get; } = [];

    public MainViewModel()
    {
        var settings = GameSettings.Load();
        _keyboardLayoutPreference = settings.KeyboardLayout;
        _serverHost = settings.ServerHost;
        _accountApiBaseUrl = settings.AccountApiBaseUrl ?? string.Empty;
        _accountApi = new AccountApiClient(settings.ResolveAccountApiBaseUrl(GameInfo.DefaultAccountApiPort));

        _ = CheckServerStatusAsync();
        LoadNews();
    }

    /// <summary>Contenu statique de démonstration (voir Docs/README.md) — pas encore de flux géré côté serveur.</summary>
    private void LoadNews()
    {
        var items = new List<NewsItem>
        {
            new()
            {
                Title = "Le monde d'Aetheria s'agrandit",
                ShortDescription = "Nouvelle carte, bâtiments visitables, PNJ et donjon de test à explorer dès aujourd'hui.",
                FullContent = "La carte du monde a été agrandie et redessinée en isométrique 2D. Les bâtiments " +
                    "(capitale, village, hôtel des ventes, forge, guilde) sont désormais visitables, des PNJ vous " +
                    "accueillent avec leurs propres dialogues, et un premier donjon de test est accessible via son " +
                    "portail animé aux abords de la carte.",
                PublishedAtUtc = new DateTime(2026, 7, 20, 10, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Choisissez votre premier compagnon",
                ShortDescription = "Une dizaine de créatures communes vous attendent pour débuter votre collection.",
                FullContent = "La création de personnage se fait désormais entièrement en jeu : personnalisez " +
                    "l'apparence de votre personnage, puis rencontrez un vieux gardien qui vous proposera de choisir " +
                    "votre premier compagnon parmi une dizaine de créatures communes, chacune avec son élément et " +
                    "son histoire. Ce choix est définitif, alors observez bien avant de valider !",
                PublishedAtUtc = new DateTime(2026, 7, 24, 14, 30, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Combat tactique et boutique en jeu",
                ShortDescription = "Affrontez des monstres sauvages sur une grille tactique et équipez-vous en boutique.",
                FullContent = "Le système de combat tactique est maintenant jouable : engagez un monstre sauvage " +
                    "depuis l'entrée d'un donjon, déplacez vos combattants sur une grille 7x7, attaquez, capturez ou " +
                    "passez votre tour. Une boutique accessible à tout moment (touche B en jeu) vous permet " +
                    "d'acheter potions, armes et armures de départ contre de l'or.",
                PublishedAtUtc = new DateTime(2026, 7, 28, 9, 15, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Guerres de royaumes",
                ShortDescription = "Rejoignez un royaume et participez aux batailles pour le contrôle des territoires.",
                FullContent = "Chaque semaine, les royaumes s'affrontent pour le contrôle de mines, villages, forts " +
                    "et donjons rares. Le royaume qui contrôle un territoire en tire des bonus passifs pour tous ses " +
                    "citoyens. Choisissez votre camp à la création de votre personnage et faites pencher la balance.",
                PublishedAtUtc = new DateTime(2026, 7, 15, 18, 0, 0, DateTimeKind.Utc),
            },
        };

        foreach (var item in items.OrderByDescending(i => i.PublishedAtUtc))
        {
            AllNews.Add(item);
        }

        foreach (var item in AllNews.Take(3))
        {
            RecentNews.Add(item);
        }
    }

    [RelayCommand]
    private void OpenNewsDetail(NewsItem? news)
    {
        if (news is null)
        {
            return;
        }

        SelectedNews = news;
        IsNewsDetailOpen = true;
    }

    [RelayCommand]
    private void CloseNewsDetail() => IsNewsDetailOpen = false;

    [RelayCommand]
    private void ToggleAllNews() => IsAllNewsOpen = !IsAllNewsOpen;

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    /// <summary>Persiste immédiatement — le fichier de préférences est partagé avec le Client (voir GDD).</summary>
    partial void OnKeyboardLayoutPreferenceChanged(KeyboardLayoutPreference value)
    {
        var settings = GameSettings.Load();
        settings.KeyboardLayout = value;
        settings.Save();
    }

    /// <summary>
    /// Persiste l'adresse du serveur et reconstruit le client HTTP en conséquence (voir GDD —
    /// jouer depuis un autre PC/réseau contre un serveur distant hébergé ailleurs). Sans ceci,
    /// changer l'adresse dans les Paramètres n'aurait aucun effet tant que le Launcher n'est pas
    /// relancé.
    /// </summary>
    partial void OnServerHostChanged(string value)
    {
        var settings = GameSettings.Load();
        settings.ServerHost = value;
        settings.Save();

        _accountApi.Dispose();
        _accountApi = new AccountApiClient(settings.ResolveAccountApiBaseUrl(GameInfo.DefaultAccountApiPort));
        _ = CheckServerStatusAsync();
    }

    /// <summary>
    /// Persiste le tunnel ngrok éventuel et reconstruit le client HTTP en conséquence (même
    /// logique que <see cref="OnServerHostChanged"/>) — voir GDD/demande utilisateur : "utilise
    /// ngrok" pour l'API de compte, ServerHost restant utilisé tel quel pour la connexion TCP de
    /// jeu (transmis au Client via --host, distinct de --apiUrl).
    /// </summary>
    partial void OnAccountApiBaseUrlChanged(string value)
    {
        var settings = GameSettings.Load();
        settings.AccountApiBaseUrl = value;
        settings.Save();

        _accountApi.Dispose();
        _accountApi = new AccountApiClient(settings.ResolveAccountApiBaseUrl(GameInfo.DefaultAccountApiPort));
        _ = CheckServerStatusAsync();
    }

    /// <summary>
    /// Ping léger de /api/health pour le petit indicateur "Serveur en ligne/hors ligne" façon
    /// launcher Ankama — pas de retry ni de polling périodique dans cette première version,
    /// juste un état constaté au lancement (voir Docs/README.md).
    /// </summary>
    private async Task CheckServerStatusAsync()
    {
        try
        {
            var settings = GameSettings.Load();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var response = await http.GetAsync($"{settings.ResolveAccountApiBaseUrl(GameInfo.DefaultAccountApiPort)}/api/health");
            IsServerOnline = response.IsSuccessStatusCode;
            ServerStatusText = IsServerOnline ? "Serveur en ligne" : "Serveur hors ligne";

            if (IsServerOnline)
            {
                // Voir GDD/demande utilisateur — bloquer "Jouer" (afficher "Mettre à jour" à la
                // place) si le serveur tourne une version différente de ce Launcher. Pas de vrai
                // mécanisme de téléchargement/mise à jour automatique pour cette version (voir
                // Docs/README.md) : juste la détection et le blocage, l'utilisateur doit
                // retélécharger le Launcher manuellement.
                var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
                ServerVersion = health?.Version;
                IsUpdateAvailable = ServerVersion is { Length: > 0 } && ServerVersion != GameInfo.Version;
            }
            else
            {
                IsUpdateAvailable = false;
            }
        }
        catch (HttpRequestException)
        {
            IsServerOnline = false;
            IsUpdateAvailable = false;
            ServerStatusText = "Serveur hors ligne";
        }
        catch (TaskCanceledException)
        {
            IsServerOnline = false;
            IsUpdateAvailable = false;
            ServerStatusText = "Serveur hors ligne";
        }
    }

    private static bool IsValidEmail(string email)
    {
        // MailAddress lève ArgumentException (pas FormatException) pour une chaîne vide, et
        // ArgumentNullException pour null — ni l'une ni l'autre n'était interceptée ici, ce qui
        // faisait planter le Launcher (exception non gérée) plutôt que d'afficher un message
        // d'erreur quand le champ email était laissé vide au clic sur "Créer un compte".
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var address = new System.Net.Mail.MailAddress(email);
            return address.Address == email && email.Contains('@');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        if (!IsValidEmail(Email))
        {
            StatusMessage = "L'email doit être au format exemple@domaine.com.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _accountApi.RegisterAsync(UsernameOrEmail, Email, Password);
            StatusMessage = result.IsSuccess
                ? "Compte créé. Vous pouvez maintenant vous connecter."
                : result.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Login()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _accountApi.LoginAsync(UsernameOrEmail, Password);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            SessionToken = result.Value!.SessionToken;
            IsLoggedIn = true;
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        IsLoggedIn = false;
        SessionToken = null;
        Password = string.Empty;
        StatusMessage = null;
    }

    private bool CanPlay() => SessionToken is not null && !IsUpdateAvailable;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        if (SessionToken is null)
        {
            return;
        }

        if (!ClientLauncher.TryLaunch(SessionToken, ServerHost, AccountApiBaseUrl, out var error))
        {
            StatusMessage = error;
        }
    }

    partial void OnSessionTokenChanged(string? value) => PlayCommand.NotifyCanExecuteChanged();

    /// <summary>Revérifie si le Launcher peut jouer dès que la disponibilité d'une mise à jour change (voir GDD — bloquer "Jouer" tant qu'une mise à jour est disponible).</summary>
    partial void OnIsUpdateAvailableChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowPlayButton));
        OnPropertyChanged(nameof(ShowUpdateButton));
    }

    partial void OnIsLoggedInChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPlayButton));
        OnPropertyChanged(nameof(ShowUpdateButton));
    }

    private sealed class HealthResponse
    {
        public string? Status { get; init; }
        public string? Version { get; init; }
    }
}
