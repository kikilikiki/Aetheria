using System.Collections.ObjectModel;
using Aetheria.Launcher.Services;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Account;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.Launcher.ViewModels;

/// <summary>
/// État et logique de l'écran unique du Launcher : formulaire de connexion/inscription,
/// puis sélection du personnage et lancement du Client (voir <c>Docs/GameDesign.md</c> —
/// section Launcher).
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
    private CharacterSummary? _selectedCharacter;

    [ObservableProperty]
    private string _newCharacterName = string.Empty;

    [ObservableProperty]
    private CharacterClass _selectedClass = CharacterClass.Guerrier;

    [ObservableProperty]
    private KingdomType _selectedKingdom = KingdomType.Feu;

    public ObservableCollection<CharacterSummary> Characters { get; } = new();

    public IReadOnlyList<CharacterClass> AvailableClasses { get; } = Enum.GetValues<CharacterClass>();

    public IReadOnlyList<KingdomType> AvailableKingdoms { get; } = Enum.GetValues<KingdomType>();

    [RelayCommand]
    private async Task Register()
    {
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

            var response = result.Value!;
            SessionToken = response.SessionToken;
            Characters.Clear();
            foreach (var character in response.Characters)
            {
                Characters.Add(character);
            }

            SelectedCharacter = Characters.FirstOrDefault();
            IsLoggedIn = true;
            StatusMessage = Characters.Count == 0
                ? "Connecté. Créez votre premier personnage ci-dessous."
                : null;
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
        Characters.Clear();
        SelectedCharacter = null;
        Password = string.Empty;
        StatusMessage = null;
    }

    private bool CanCreateCharacter() => SessionToken is not null && NewCharacterName.Trim().Length >= 3;

    [RelayCommand(CanExecute = nameof(CanCreateCharacter))]
    private async Task CreateCharacter()
    {
        if (SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _accountApi.CreateCharacterAsync(SessionToken, NewCharacterName.Trim(), SelectedClass, SelectedKingdom);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            Characters.Add(result.Value!);
            SelectedCharacter = result.Value;
            NewCharacterName = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnNewCharacterNameChanged(string value) => CreateCharacterCommand.NotifyCanExecuteChanged();

    private bool CanPlay() => SelectedCharacter is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        if (SessionToken is null || SelectedCharacter is null)
        {
            return;
        }

        if (!ClientLauncher.TryLaunch(SessionToken, SelectedCharacter.Id, out var error))
        {
            StatusMessage = error;
        }
    }

    partial void OnSelectedCharacterChanged(CharacterSummary? value) => PlayCommand.NotifyCanExecuteChanged();

    partial void OnSessionTokenChanged(string? value)
    {
        PlayCommand.NotifyCanExecuteChanged();
        CreateCharacterCommand.NotifyCanExecuteChanged();
    }
}
