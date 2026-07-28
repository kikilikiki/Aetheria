using System.Net.Http;
using Aetheria.Launcher.Services;
using Aetheria.Shared;
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

    public MainViewModel()
    {
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
