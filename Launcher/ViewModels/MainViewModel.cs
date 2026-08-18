using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Aetheria.Launcher.Models;
using Aetheria.Launcher.Services;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Admin;
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
    private AdminApiClient? _adminApi;

    /// <summary>Voir GDD/demande utilisateur — "traduit le launcher en anglais aussi" : raccourci pour les messages assignés en C# (StatusMessage, ServerStatusText, ...), qui ne passent pas par le markup extension loc:Tr (réservé au XAML statique).</summary>
    private static string Tr(string text) => Aetheria.Shared.Localization.Localization.Translate(text, GameSettings.Load().Language);

    [ObservableProperty]
    private string _title = $"{GameInfo.Name} Launcher — v{GameInfo.Version}";

    [ObservableProperty]
    private string _usernameOrEmail = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>Voir retour utilisateur — "au launcher pouvoir voir le mot de passe que l'on tape" : bascule entre PasswordBox (masqué) et TextBox (en clair), voir MainWindow.xaml.cs.</summary>
    [ObservableProperty]
    private bool _isPasswordVisible;

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
    private string _serverStatusText = Tr("Vérification du serveur...");

    /// <summary>Vrai si le serveur tourne une version différente de ce Launcher (voir GDD/demande utilisateur — afficher "Mettre à jour" à la place de "Jouer").</summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string? _serverVersion;

    /// <summary>Voir GDD/demande utilisateur — "mise à jour en cours (14%)" : vrai pendant le téléchargement/application de la mise à jour (voir <see cref="UpdateAsync"/>).</summary>
    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private int _updateProgressPercent;

    /// <summary>Texte du bouton de mise à jour (voir GDD/demande utilisateur — "Mise à jour / Mise à jour en cours (14%) / Jouer").</summary>
    public string UpdateButtonText => IsUpdating ? $"{Tr("Mise à jour en cours")} ({UpdateProgressPercent}%)" : Tr("Mise à jour");

    /// <summary>Le gros bouton du bas est soit JOUER, soit MISE À JOUR — jamais les deux (voir GDD).</summary>
    public bool ShowPlayButton => IsLoggedIn && !IsUpdateAvailable;
    public bool ShowUpdateButton => IsLoggedIn && IsUpdateAvailable;

    [ObservableProperty]
    private bool _isSettingsOpen;

    /// <summary>Voir GDD/demande utilisateur — bouton "Communauté" et panneau d'administration réservés aux comptes admin/fondateur (renseigné à la connexion, voir <see cref="Login"/>).</summary>
    [ObservableProperty]
    private bool _isAdminAccount;

    /// <summary>Voir GDD/demande utilisateur — "quand on n'est pas modo, l'adresse du serveur ne doit pas être affichée" : modérateur/admin/fondateur, plus large que <see cref="IsAdminAccount"/> (qui réserve l'ÉDITION à l'admin/fondateur ; ceci réserve juste la VISIBILITÉ du champ).</summary>
    [ObservableProperty]
    private bool _isStaffAccount;

    /// <summary>Voir GDD/demande utilisateur — "seul le compte admin (avec le grade fondateur) peut modifier l'adresse du serveur" : plus étroit que <see cref="IsAdminAccount"/> (qui reste utilisé pour le panneau d'administration, ouvert à tout admin).</summary>
    [ObservableProperty]
    private bool _isFondateurAccount;

    [ObservableProperty]
    private bool _isAdminPanelOpen;

    /// <summary>Voir GDD/demande utilisateur — "les admin peut voir les report sur une page sur le launcher".</summary>
    [ObservableProperty]
    private bool _isReportsPanelOpen;

    [ObservableProperty]
    private KeyboardLayoutPreference _keyboardLayoutPreference;

    /// <summary>Voir GDD/demande utilisateur — "ajoute un parametre pour changer la langue... francais (actuel), japonais, anglais". Japonais volontairement absent de <see cref="AvailableLanguages"/> ci-dessous (voir retour utilisateur — pas de police japonaise, voir Shared/Localization/Localization.cs), l'énumération le garde pour ne pas retoucher le type plus tard.</summary>
    [ObservableProperty]
    private Language _languagePreference;

    public IReadOnlyList<Language> AvailableLanguages { get; } = [Language.Francais, Language.Anglais];

    /// <summary>Adresse du serveur (voir GDD/demande utilisateur — jeu installé ailleurs, serveur hébergé chez l'utilisateur). Réglée par défaut sur l'IP publique du serveur (voir GameSettings.ServerHost) : fonctionne aussi bien en local que depuis un autre réseau, sans réglage ngrok supplémentaire. Écrasée par la valeur persistée dans le constructeur ; ce champ n'est qu'un défaut avant chargement.</summary>
    [ObservableProperty]
    private string _serverHost = new GameSettings().ServerHost;

    public IReadOnlyList<KeyboardLayoutPreference> AvailableKeyboardLayouts { get; } = Enum.GetValues<KeyboardLayoutPreference>();

    public string DetectedLayoutText => KeyboardLayoutResolver.IsAzertyDetected()
        ? $"{Tr("Détecté sur cette machine")} : AZERTY"
        : $"{Tr("Détecté sur cette machine")} : QWERTY";

    [ObservableProperty]
    private bool _isNewsDetailOpen;

    [ObservableProperty]
    private bool _isAllNewsOpen;

    [ObservableProperty]
    private NewsItem? _selectedNews;

    /// <summary>Les trois plus récentes, affichées directement dans le panneau (voir GDD — page "toutes les actualités").</summary>
    public ObservableCollection<NewsItem> RecentNews { get; } = [];

    public ObservableCollection<NewsItem> AllNews { get; } = [];

    /// <summary>
    /// Nettoie l'adresse serveur tapée/persistée : le code construit toujours l'URL sous la
    /// forme "http://{ServerHost}:{port}" (voir <see cref="GameInfo.DefaultAccountApiPort"/>) —
    /// si l'utilisateur colle une adresse qui contient déjà un schéma ou un port (ex.
    /// "192.168.1.12:7777", le port TCP du jeu plutôt que celui de l'API), l'URL obtenue a deux
    /// ports et <see cref="Uri"/> lève "Invalid URI: Invalid port specified" dans le constructeur
    /// de <see cref="Services.AccountApiClient"/> — non rattrapable puisqu'il s'exécute avant que
    /// la fenêtre (et donc le filet de sécurité <c>App.ShowFatalError</c>) n'existe, ce qui
    /// empêchait le Launcher de démarrer tant que le fichier settings.json n'était pas corrigé à
    /// la main (voir retour utilisateur - "erreur au lancement du launcher" après avoir modifié
    /// l'adresse du serveur dans les Paramètres).
    /// </summary>
    private static string NormalizeHost(string host)
    {
        host = host.Trim();

        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            host = host["http://".Length..];
        }
        else if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            host = host["https://".Length..];
        }

        host = host.TrimEnd('/');

        var colonIndex = host.LastIndexOf(':');
        if (colonIndex > 0 && host[(colonIndex + 1)..].Length > 0 && host[(colonIndex + 1)..].All(char.IsDigit))
        {
            host = host[..colonIndex];
        }

        return host;
    }

    public MainViewModel()
    {
        var settings = GameSettings.Load();
        _keyboardLayoutPreference = settings.KeyboardLayout;
        _languagePreference = settings.Language;
        _serverHost = NormalizeHost(settings.ServerHost);
        _accountApi = new AccountApiClient($"http://{_serverHost}:{GameInfo.DefaultAccountApiPort}");

        _ = CheckServerStatusAsync();
        LoadNews();

        // Voir GDD/demande utilisateur — "après la connexion à un compte, on y reste connecté
        // jusqu'à ce que l'on s'y déconnecte" : revalide le jeton persisté au démarrage plutôt
        // que de redemander les identifiants. Échoue silencieusement (retombe sur l'écran de
        // connexion normal) si le serveur a redémarré depuis (SessionTokenStore en mémoire) ou
        // si le compte a été banni/supprimé entre-temps.
        if (!string.IsNullOrWhiteSpace(settings.SessionToken))
        {
            _ = TryRestoreSessionAsync(settings.SessionToken);
        }
    }

    private async Task TryRestoreSessionAsync(string sessionToken)
    {
        var result = await _accountApi.ValidateSessionAsync(sessionToken);
        if (!result.IsSuccess)
        {
            var settings = GameSettings.Load();
            settings.SessionToken = null;
            settings.Save();
            return;
        }

        SessionToken = sessionToken;
        IsLoggedIn = true;
        IsAdminAccount = result.Value!.IsAdmin || result.Value.Rank == UserRank.Fondateur;
        IsStaffAccount = result.Value.IsAdmin || result.Value.Rank is UserRank.Moderateur or UserRank.Fondateur;
        IsFondateurAccount = result.Value.Rank == UserRank.Fondateur;
        _adminApi = new AdminApiClient($"http://{ServerHost}:{GameInfo.DefaultAccountApiPort}");
    }

    /// <summary>Contenu statique de démonstration (voir Docs/README.md) — pas encore de flux géré côté serveur.</summary>
    private void LoadNews()
    {
        var items = new List<NewsItem>
        {
            new()
            {
                Title = "Le Launcher arrive sur Linux",
                ShortDescription = "Compte, connexion et mise à jour automatique désormais disponibles sur Linux (.deb/.tar.gz).",
                FullContent = "Le Launcher (création de compte, connexion, mise à jour automatique) est désormais " +
                    "disponible sur Linux en plus de Windows, et non plus seulement le Client seul en mode démo. " +
                    "Il est distribué avec le Client dans les mêmes paquets .deb et .tar.gz : lancez simplement " +
                    "\"aetheria\" pour démarrer le Launcher, qui lance ensuite le jeu à côté de lui.",
                PublishedAtUtc = new DateTime(2026, 8, 12, 2, 30, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Grosse mise à jour : économie, guerres de royaumes et plus",
                ShortDescription = "Gemmes, grades, boosts, guerre de royaumes en direct, starters, duel PvP en équipe et bien plus.",
                FullContent = "Une vague de mises à jour vient d'arriver : une économie premium avec gemmes, grades " +
                    "et pass personnel, la guerre de royaumes désormais suivie en direct, des consommables de boost, " +
                    "de nouveaux starters, le duel PvP en équipe, ainsi que de nombreux fixes d'interface. La base de " +
                    "données est aussi persistée par défaut pour éviter toute perte de progression.",
                PublishedAtUtc = new DateTime(2026, 7, 29, 9, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Le monde d'Aetheria s'agrandit",
                ShortDescription = "Nouvelle carte, bâtiments visitables, PNJ et donjon de test à explorer dès aujourd'hui.",
                FullContent = "La carte du monde a été agrandie et redessinée en isométrique 2D. Les bâtiments " +
                    "(capitale, village, hôtel des ventes, forge, guilde) sont désormais visitables, des PNJ vous " +
                    "accueillent avec leurs propres dialogues, et un premier donjon de test est accessible via son " +
                    "portail animé aux abords de la carte.",
                PublishedAtUtc = new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Choisissez votre premier compagnon",
                ShortDescription = "Une dizaine de créatures communes vous attendent pour débuter votre collection.",
                FullContent = "La création de personnage se fait désormais entièrement en jeu : personnalisez " +
                    "l'apparence de votre personnage, puis rencontrez un vieux gardien qui vous proposera de choisir " +
                    "votre premier compagnon parmi une dizaine de créatures communes, chacune avec son élément et " +
                    "son histoire. Ce choix est définitif, alors observez bien avant de valider !",
                PublishedAtUtc = new DateTime(2026, 7, 27, 9, 30, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Combat tactique et boutique en jeu",
                ShortDescription = "Affrontez des monstres sauvages sur une grille tactique et équipez-vous en boutique.",
                FullContent = "Le système de combat tactique est maintenant jouable : engagez un monstre sauvage " +
                    "depuis l'entrée d'un donjon, déplacez vos combattants sur une grille 7x7, attaquez, capturez ou " +
                    "passez votre tour. Une boutique accessible à tout moment (touche B en jeu) vous permet " +
                    "d'acheter potions, armes et armures de départ contre de l'or.",
                PublishedAtUtc = new DateTime(2026, 7, 27, 9, 15, 0, DateTimeKind.Utc),
            },
            new()
            {
                Title = "Guerres de royaumes",
                ShortDescription = "Rejoignez un royaume et participez aux batailles pour le contrôle des territoires.",
                FullContent = "Chaque semaine, les royaumes s'affrontent pour le contrôle de mines, villages, forts " +
                    "et donjons rares. Le royaume qui contrôle un territoire en tire des bonus passifs pour tous ses " +
                    "citoyens. Choisissez votre camp à la création de votre personnage et faites pencher la balance.",
                PublishedAtUtc = new DateTime(2026, 7, 27, 9, 0, 0, DateTimeKind.Utc),
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

    // Voir GDD/demande utilisateur — "une page sur le launcher avec un i pour accéder au à
    // propos, avec un bouton pour aller sur les CGU du site" + crédits.
    [ObservableProperty]
    private bool _isAboutOpen;

    [RelayCommand]
    private void ToggleAbout() => IsAboutOpen = !IsAboutOpen;

    [RelayCommand]
    private void OpenTermsOfService()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(GameInfo.TermsOfServiceUrl) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Pas de navigateur par défaut configuré sur cette machine — rien de plus à faire,
            // l'utilisateur reste sur le panneau À propos.
        }
    }

    // Voir GDD/demande utilisateur — "dans le launcher quand on appuie sur un pseudo on doit
    // avoir ces réseaux et les liens pour y accéder" (voir CreatorCredits, même registre que la
    // fiche créateur en jeu). Seuls les pseudos reconnus l'ouvrent — les autres restent inertes.
    [ObservableProperty]
    private bool _isCreatorInfoOpen;

    [ObservableProperty]
    private CreatorProfile? _creatorInfo;

    [RelayCommand]
    private void ShowCreatorInfo(string? username)
    {
        if (username is null)
        {
            return;
        }

        var profile = CreatorCredits.Find(username);
        if (profile is null)
        {
            return;
        }

        CreatorInfo = profile;
        IsCreatorInfoOpen = true;
    }

    [RelayCommand]
    private void CloseCreatorInfo() => IsCreatorInfoOpen = false;

    [RelayCommand]
    private void OpenCreatorLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    // Voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher".
    [ObservableProperty]
    private bool _isLeaderboardOpen;

    [ObservableProperty]
    private LeaderboardCategory _selectedLeaderboardCategory = LeaderboardCategory.Pvp;

    public IReadOnlyList<LeaderboardCategory> AvailableLeaderboardCategories { get; } =
        [LeaderboardCategory.Pvp, LeaderboardCategory.Richesse, LeaderboardCategory.Metiers, LeaderboardCategory.MonstresCaptures, LeaderboardCategory.Donjons];

    public ObservableCollection<LeaderboardRowDisplay> LeaderboardRows { get; } = [];

    [RelayCommand]
    private async Task ToggleLeaderboard()
    {
        IsLeaderboardOpen = !IsLeaderboardOpen;
        if (IsLeaderboardOpen)
        {
            await LoadLeaderboardAsync();
        }
    }

    partial void OnSelectedLeaderboardCategoryChanged(LeaderboardCategory value) => _ = LoadLeaderboardAsync();

    private async Task LoadLeaderboardAsync()
    {
        var result = await _accountApi.GetLeaderboardAsync(SelectedLeaderboardCategory);
        LeaderboardRows.Clear();
        if (!result.IsSuccess || result.Value is null)
        {
            return;
        }

        var rank = 1;
        foreach (var row in result.Value)
        {
            LeaderboardRows.Add(new LeaderboardRowDisplay(rank++, row.CharacterName, row.Score));
        }
    }

    /// <summary>Persiste immédiatement — le fichier de préférences est partagé avec le Client (voir GDD).</summary>
    partial void OnKeyboardLayoutPreferenceChanged(KeyboardLayoutPreference value)
    {
        var settings = GameSettings.Load();
        settings.KeyboardLayout = value;
        settings.Save();
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute un parametre pour changer la langue" — persiste
    /// immédiatement, partagé avec le Client comme la disposition clavier ci-dessus. Voir aussi
    /// GDD/demande utilisateur — "traduit le launcher en anglais aussi" : met aussi à jour
    /// LauncherLanguageService pour retraduire le Launcher lui-même immédiatement (voir loc:Tr
    /// dans MainWindow.xaml), sans redémarrage.
    /// </summary>
    partial void OnLanguagePreferenceChanged(Language value)
    {
        var settings = GameSettings.Load();
        settings.Language = value;
        settings.Save();
        Aetheria.Launcher.Localization.LauncherLanguageService.Instance.Current = value;
        OnPropertyChanged(nameof(UpdateButtonText));
        OnPropertyChanged(nameof(DetectedLayoutText));
    }

    /// <summary>
    /// Persiste l'adresse du serveur et reconstruit le client HTTP en conséquence (voir GDD —
    /// jouer depuis un autre PC/réseau contre un serveur distant hébergé ailleurs). Sans ceci,
    /// changer l'adresse dans les Paramètres n'aurait aucun effet tant que le Launcher n'est pas
    /// relancé.
    /// </summary>
    partial void OnServerHostChanged(string value)
    {
        var normalized = NormalizeHost(value);
        if (normalized != value)
        {
            // Réécrit le champ avec la valeur nettoyée (voir NormalizeHost) : évite de repartir
            // en boucle infinie (le setter généré ignore une valeur identique) tout en corrigeant
            // ce que l'utilisateur voit à l'écran, pas seulement ce qui est utilisé/persisté.
            ServerHost = normalized;
            return;
        }

        var settings = GameSettings.Load();
        settings.ServerHost = value;
        settings.Save();

        _accountApi.Dispose();
        _accountApi = new AccountApiClient($"http://{value}:{GameInfo.DefaultAccountApiPort}");

        if (_adminApi is not null)
        {
            _adminApi.Dispose();
            _adminApi = new AdminApiClient($"http://{value}:{GameInfo.DefaultAccountApiPort}");
        }

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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            var response = await http.GetAsync($"http://{ServerHost}:{GameInfo.DefaultAccountApiPort}/api/health");
            IsServerOnline = response.IsSuccessStatusCode;
            ServerStatusText = Tr(IsServerOnline ? "Serveur en ligne" : "Serveur hors ligne");

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
            ServerStatusText = Tr("Serveur hors ligne");
        }
        catch (TaskCanceledException)
        {
            IsServerOnline = false;
            IsUpdateAvailable = false;
            ServerStatusText = Tr("Serveur hors ligne");
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
            StatusMessage = Tr("L'email doit être au format exemple@domaine.com.");
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _accountApi.RegisterAsync(UsernameOrEmail, Email, Password);
            StatusMessage = result.IsSuccess
                ? Tr("Compte créé. Vous pouvez maintenant vous connecter.")
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

            // Voir retour utilisateur — "apres la connexion effacer le mot de passe" : plus besoin
            // de le garder en mémoire une fois la session ouverte (le SessionToken suffit pour
            // toute action ultérieure) — laissé intact en cas d'échec pour ne pas forcer une
            // retape après une simple faute de frappe.
            Password = string.Empty;

            // Voir GDD/demande utilisateur — panneau "Communauté" réservé aux admin/fondateur.
            IsAdminAccount = result.Value.IsAdmin || result.Value.Rank == UserRank.Fondateur;
            IsStaffAccount = result.Value.IsAdmin || result.Value.Rank is UserRank.Moderateur or UserRank.Fondateur;
            IsFondateurAccount = result.Value.Rank == UserRank.Fondateur;
            _adminApi = new AdminApiClient($"http://{ServerHost}:{GameInfo.DefaultAccountApiPort}");

            // Voir GDD/demande utilisateur — "rester connecté jusqu'à la déconnexion".
            var settings = GameSettings.Load();
            settings.SessionToken = SessionToken;
            settings.Save();
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
        IsPasswordVisible = false;
        StatusMessage = null;
        IsAdminAccount = false;
        IsStaffAccount = false;
        IsFondateurAccount = false;
        IsAdminPanelOpen = false;
        AdminUsers.Clear();
        IsReportsPanelOpen = false;
        Reports.Clear();

        // Voir GDD/demande utilisateur — "rester connecté jusqu'à ce que l'on s'y déconnecte" :
        // seule une déconnexion explicite efface le jeton persisté.
        var settings = GameSettings.Load();
        settings.SessionToken = null;
        settings.Save();
    }

    private bool CanPlay() => SessionToken is not null && !IsUpdateAvailable;

    /// <summary>
    /// Voir GDD/demande utilisateur — "quand il faut faire une maj on peut quand même join, fait
    /// en sorte que il ait l'obligation de mettre à jour avant de pouvoir lancer le jeu" :
    /// <see cref="IsUpdateAvailable"/> n'est recalculé qu'au démarrage/changement d'adresse
    /// serveur (voir CheckServerStatusAsync), donc un Launcher resté ouvert pendant qu'une
    /// nouvelle version est déployée gardait un état périmé et laissait passer "Jouer". Revérifie
    /// donc la version serveur juste avant de lancer, plutôt que de faire confiance à ce cache.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task Play()
    {
        if (SessionToken is null)
        {
            return;
        }

        await CheckServerStatusAsync();
        if (IsUpdateAvailable)
        {
            StatusMessage = Tr("Une nouvelle version est disponible : mettez à jour avant de jouer.");
            return;
        }

        if (!ClientLauncher.TryLaunch(SessionToken, ServerHost, out var error))
        {
            StatusMessage = error;
        }
    }

    partial void OnSessionTokenChanged(string? value) => PlayCommand.NotifyCanExecuteChanged();

    private bool CanUpdate() => !IsUpdating;

    /// <summary>
    /// Voir GDD/demande utilisateur — "obligé de faire la dernière mise à jour pour lancer le
    /// jeu" : télécharge et applique le paquet servi par le serveur (voir
    /// <see cref="SelfUpdateService"/>), puis ferme ce Launcher — le script détaché le relance
    /// une fois les fichiers remplacés.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task Update()
    {
        IsUpdating = true;
        UpdateProgressPercent = 0;
        StatusMessage = null;

        var progress = new Progress<int>(p => UpdateProgressPercent = p);
        var error = await SelfUpdateService.DownloadAndApplyAsync(ServerHost, GameInfo.DefaultAccountApiPort, progress);

        if (error is not null)
        {
            IsUpdating = false;
            StatusMessage = error;
            return;
        }

        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(nameof(UpdateButtonText));
        UpdateCommand.NotifyCanExecuteChanged();
    }

    partial void OnUpdateProgressPercentChanged(int value) => OnPropertyChanged(nameof(UpdateButtonText));

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

    // ===== Panneau "Communauté" (voir GDD/demande utilisateur — bouton Communauté, liste des
    // utilisateurs avec email/avatar, grade/mute/ban/ban IP/reset profil, réservé aux comptes
    // admin/fondateur) : même logique que AdminPanel/ViewModels/MainViewModel.cs, dupliquée ici
    // plutôt que partagée entre les deux projets WPF pour un client HTTP aussi simple. **Avatar**
    // simplifié en pastille de couleur + initiale (pas d'image de profil, voir Docs/README.md).

    public ObservableCollection<AdminUserSummary> AdminUsers { get; } = [];

    [ObservableProperty]
    private string _adminSearchText = string.Empty;

    [ObservableProperty]
    private AdminUserSummary? _adminSelectedUser;

    [ObservableProperty]
    private string _adminBanReason = string.Empty;

    [ObservableProperty]
    private string _adminNewUsername = string.Empty;

    [ObservableProperty]
    private string _adminNewEmail = string.Empty;

    [ObservableProperty]
    private UserRank _adminSelectedRank;

    public IReadOnlyList<UserRank> AdminAvailableRanks { get; } = Enum.GetValues<UserRank>();

    [ObservableProperty]
    private string? _adminStatusMessage;

    [ObservableProperty]
    private bool _adminIsBusy;

    private bool CanToggleAdminPanel() => IsAdminAccount;

    [RelayCommand(CanExecute = nameof(CanToggleAdminPanel))]
    private async Task ToggleAdminPanel()
    {
        IsAdminPanelOpen = !IsAdminPanelOpen;
        if (IsAdminPanelOpen)
        {
            await AdminSearch();
        }
    }

    [RelayCommand]
    private async Task AdminSearch()
    {
        if (_adminApi is null || SessionToken is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.GetUsersAsync(AdminSearchText);
            AdminUsers.Clear();
            if (result.IsSuccess)
            {
                foreach (var user in result.Value!)
                {
                    AdminUsers.Add(user);
                }
            }
            else
            {
                AdminStatusMessage = result.Error;
            }
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute un bouton pour copier tout les mail et le coller
    /// dans gmail pour envoyer les mail a tout le monde simplement" : copie les emails de la
    /// liste actuellement chargée (voir <see cref="AdminUsers"/>, tous les comptes par défaut à
    /// l'ouverture du panneau — voir <see cref="ToggleAdminPanel"/>, ou le résultat filtré d'une
    /// recherche) séparés par une virgule, format directement collable dans le champ "Cci"
    /// (copie cachée) de Gmail pour envoyer un message à tout le monde sans exposer les adresses
    /// des uns aux autres.
    /// </summary>
    [RelayCommand]
    private async Task AdminCopyAllEmailsAsync()
    {
        if (AdminUsers.Count == 0)
        {
            AdminStatusMessage = "Aucun email à copier.";
            return;
        }

        var emails = string.Join(", ", AdminUsers.Select(u => u.Email));
        await ClipboardService.SetTextAsync(emails);
        AdminStatusMessage = $"{AdminUsers.Count} email(s) copié(s) dans le presse-papiers.";
    }

    // ===== Panneau "Signalements" (voir GDD/demande utilisateur — "les admin peut voir les
    // report sur une page sur le launcher") : même modèle que le panneau Communauté ci-dessus.

    public ObservableCollection<ReportSummary> Reports { get; } = [];

    [ObservableProperty]
    private string? _reportsStatusMessage;

    [ObservableProperty]
    private bool _reportsIsBusy;

    private bool CanToggleReportsPanel() => IsAdminAccount;

    [RelayCommand(CanExecute = nameof(CanToggleReportsPanel))]
    private async Task ToggleReportsPanel()
    {
        IsReportsPanelOpen = !IsReportsPanelOpen;
        if (IsReportsPanelOpen)
        {
            await LoadReports();
        }
    }

    [RelayCommand]
    private async Task LoadReports()
    {
        if (_adminApi is null || SessionToken is null)
        {
            return;
        }

        ReportsIsBusy = true;
        ReportsStatusMessage = null;
        try
        {
            var result = await _adminApi.GetReportsAsync(SessionToken);
            Reports.Clear();
            if (result.IsSuccess)
            {
                foreach (var report in result.Value!)
                {
                    Reports.Add(report);
                }
            }
            else
            {
                ReportsStatusMessage = result.Error;
            }
        }
        finally
        {
            ReportsIsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResolveReport(ReportSummary? report)
    {
        if (_adminApi is null || SessionToken is null || report is null)
        {
            return;
        }

        var result = await _adminApi.ResolveReportAsync(SessionToken, report.Id);
        if (result.IsSuccess)
        {
            await LoadReports();
        }
        else
        {
            ReportsStatusMessage = result.Error;
        }
    }

    private bool CanAdminBan() => AdminSelectedUser is { IsBanned: false } && AdminBanReason.Trim().Length > 0 && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanAdminBan))]
    private async Task AdminBan()
    {
        if (_adminApi is null || AdminSelectedUser is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.BanAsync(AdminSelectedUser.Id, AdminBanReason.Trim());
            if (!result.IsSuccess)
            {
                AdminStatusMessage = result.Error;
                return;
            }

            AdminBanReason = string.Empty;
            await AdminSearch();
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    private bool CanAdminUnban() => AdminSelectedUser is { IsBanned: true };

    [RelayCommand(CanExecute = nameof(CanAdminUnban))]
    private async Task AdminUnban()
    {
        if (_adminApi is null || AdminSelectedUser is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.UnbanAsync(AdminSelectedUser.Id);
            if (!result.IsSuccess)
            {
                AdminStatusMessage = result.Error;
                return;
            }

            await AdminSearch();
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    private bool CanAdminModify() => AdminSelectedUser is not null && SessionToken is not null
        && (AdminNewUsername.Trim().Length > 0 || AdminNewEmail.Trim().Length > 0);

    [RelayCommand(CanExecute = nameof(CanAdminModify))]
    private async Task AdminModify()
    {
        if (_adminApi is null || AdminSelectedUser is null || SessionToken is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.ModifyUserAsync(
                AdminSelectedUser.Id, SessionToken,
                AdminNewUsername.Trim().Length > 0 ? AdminNewUsername.Trim() : null,
                AdminNewEmail.Trim().Length > 0 ? AdminNewEmail.Trim() : null);

            if (!result.IsSuccess)
            {
                AdminStatusMessage = result.Error;
                return;
            }

            AdminNewUsername = string.Empty;
            AdminNewEmail = string.Empty;
            await AdminSearch();
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    private bool CanAdminSetRank() => AdminSelectedUser is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanAdminSetRank))]
    private async Task AdminSetRank()
    {
        if (_adminApi is null || AdminSelectedUser is null || SessionToken is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.SetRankAsync(AdminSelectedUser.Id, SessionToken, AdminSelectedRank);
            if (!result.IsSuccess)
            {
                AdminStatusMessage = result.Error;
                return;
            }

            await AdminSearch();
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    private bool CanAdminToggleMute() => AdminSelectedUser is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanAdminToggleMute))]
    private async Task AdminToggleMute()
    {
        if (_adminApi is null || AdminSelectedUser is null || SessionToken is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.SetMuteAsync(AdminSelectedUser.Id, SessionToken, !AdminSelectedUser.IsMuted);
            if (!result.IsSuccess)
            {
                AdminStatusMessage = result.Error;
                return;
            }

            await AdminSearch();
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    private bool CanAdminBanIp() => AdminSelectedUser is { LastKnownIp.Length: > 0 } && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanAdminBanIp))]
    private async Task AdminBanIp()
    {
        if (_adminApi is null || AdminSelectedUser is null || SessionToken is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.BanIpAsync(AdminSelectedUser.Id, SessionToken);
            AdminStatusMessage = result.IsSuccess ? $"IP {AdminSelectedUser.LastKnownIp} bannie." : result.Error;
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    private bool CanAdminResetProfile() => AdminSelectedUser is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanAdminResetProfile))]
    private async Task AdminResetProfile()
    {
        if (_adminApi is null || AdminSelectedUser is null || SessionToken is null)
        {
            return;
        }

        AdminIsBusy = true;
        AdminStatusMessage = null;
        try
        {
            var result = await _adminApi.ResetProfileAsync(AdminSelectedUser.Id, SessionToken);
            if (!result.IsSuccess)
            {
                AdminStatusMessage = result.Error;
                return;
            }

            await AdminSearch();
        }
        finally
        {
            AdminIsBusy = false;
        }
    }

    partial void OnIsAdminAccountChanged(bool value)
    {
        ToggleAdminPanelCommand.NotifyCanExecuteChanged();
        ToggleReportsPanelCommand.NotifyCanExecuteChanged();
    }

    partial void OnAdminSelectedUserChanged(AdminUserSummary? value)
    {
        AdminBanCommand.NotifyCanExecuteChanged();
        AdminUnbanCommand.NotifyCanExecuteChanged();
        AdminModifyCommand.NotifyCanExecuteChanged();
        AdminSetRankCommand.NotifyCanExecuteChanged();
        AdminToggleMuteCommand.NotifyCanExecuteChanged();
        AdminBanIpCommand.NotifyCanExecuteChanged();
        AdminResetProfileCommand.NotifyCanExecuteChanged();
        AdminSelectedRank = value?.Rank ?? UserRank.Joueur;
    }

    partial void OnAdminBanReasonChanged(string value) => AdminBanCommand.NotifyCanExecuteChanged();

    partial void OnAdminNewUsernameChanged(string value) => AdminModifyCommand.NotifyCanExecuteChanged();

    partial void OnAdminNewEmailChanged(string value) => AdminModifyCommand.NotifyCanExecuteChanged();
}
