using System.Collections.ObjectModel;
using System.Net.Http;
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
    private readonly AccountApiClient _accountApi = new();

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

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private KeyboardLayoutPreference _keyboardLayoutPreference;

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
        _ = CheckServerStatusAsync();
        _keyboardLayoutPreference = GameSettings.Load().KeyboardLayout;
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
    /// Ping léger de /api/health pour le petit indicateur "Serveur en ligne/hors ligne" façon
    /// launcher Ankama — pas de retry ni de polling périodique dans cette première version,
    /// juste un état constaté au lancement (voir Docs/README.md).
    /// </summary>
    private async Task CheckServerStatusAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var response = await http.GetAsync($"http://localhost:{GameInfo.DefaultAccountApiPort}/api/health");
            IsServerOnline = response.IsSuccessStatusCode;
            ServerStatusText = IsServerOnline ? "Serveur en ligne" : "Serveur hors ligne";
        }
        catch (HttpRequestException)
        {
            IsServerOnline = false;
            ServerStatusText = "Serveur hors ligne";
        }
        catch (TaskCanceledException)
        {
            IsServerOnline = false;
            ServerStatusText = "Serveur hors ligne";
        }
    }

    private static bool IsValidEmail(string email)
    {
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

    private bool CanPlay() => SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        if (SessionToken is null)
        {
            return;
        }

        if (!ClientLauncher.TryLaunch(SessionToken, out var error))
        {
            StatusMessage = error;
        }
    }

    partial void OnSessionTokenChanged(string? value) => PlayCommand.NotifyCanExecuteChanged();
}
