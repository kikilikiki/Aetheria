using System.Collections.ObjectModel;
using Aetheria.AdminPanel.Services;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models.Admin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.AdminPanel.ViewModels;

/// <summary>
/// Gestion des joueurs et statistiques globales (voir <c>Docs/GameDesign.md</c> — section
/// AdminPanel). Un compte administrateur (voir <c>AdminAccountSeeder</c> côté serveur) doit se
/// connecter avant de pouvoir agir — les actions destructives (suppression, permissions) exigent
/// ce jeton de session ; ban/débannir restent ouverts (limite historique assumée, voir Docs/README.md).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AdminApiClient _api = new();

    public ObservableCollection<AdminUserSummary> Users { get; } = [];

    [ObservableProperty]
    private string _adminUsernameOrEmail = string.Empty;

    [ObservableProperty]
    private string _adminPassword = string.Empty;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string? _sessionToken;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private AdminUserSummary? _selectedUser;

    [ObservableProperty]
    private string _banReason = string.Empty;

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newEmail = string.Empty;

    [ObservableProperty]
    private AdminGlobalStats? _stats;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task AdminLogin()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.LoginAsync(AdminUsernameOrEmail, AdminPassword);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            SessionToken = result.Value!.SessionToken;
            IsLoggedIn = true;
            AdminPassword = string.Empty;
            await Load();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AdminLogout()
    {
        IsLoggedIn = false;
        SessionToken = null;
        Users.Clear();
        Stats = null;
    }

    [RelayCommand]
    private async Task Load()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var statsResult = await _api.GetStatsAsync();
            Stats = statsResult.IsSuccess ? statsResult.Value : null;
            if (!statsResult.IsSuccess)
            {
                StatusMessage = statsResult.Error;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Search()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.GetUsersAsync(SearchText);
            Users.Clear();
            if (result.IsSuccess)
            {
                foreach (var user in result.Value!)
                {
                    Users.Add(user);
                }
            }
            else
            {
                StatusMessage = result.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBan() => SelectedUser is { IsBanned: false } && BanReason.Trim().Length > 0;

    [RelayCommand(CanExecute = nameof(CanBan))]
    private async Task Ban()
    {
        if (SelectedUser is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.BanAsync(SelectedUser.Id, BanReason.Trim());
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            BanReason = string.Empty;
            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanUnban() => SelectedUser is { IsBanned: true };

    [RelayCommand(CanExecute = nameof(CanUnban))]
    private async Task Unban()
    {
        if (SelectedUser is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.UnbanAsync(SelectedUser.Id);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanDelete() => SelectedUser is { IsAdmin: false, IsDeleted: false } && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteUser()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.DeleteUserAsync(SelectedUser.Id, SessionToken);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRestore() => SelectedUser is { IsDeleted: true } && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreUser()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.RestoreUserAsync(SelectedUser.Id, SessionToken);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanModify() => SelectedUser is not null && SessionToken is not null
        && (NewUsername.Trim().Length > 0 || NewEmail.Trim().Length > 0);

    [RelayCommand(CanExecute = nameof(CanModify))]
    private async Task ModifyUser()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.ModifyUserAsync(
                SelectedUser.Id, SessionToken,
                NewUsername.Trim().Length > 0 ? NewUsername.Trim() : null,
                NewEmail.Trim().Length > 0 ? NewEmail.Trim() : null);

            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            NewUsername = string.Empty;
            NewEmail = string.Empty;
            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanTogglePermission() => SelectedUser is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanTogglePermission))]
    private async Task ToggleAdminPermission()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.SetAdminAsync(SelectedUser.Id, SessionToken, !SelectedUser.IsAdmin);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Grade choisi dans le sélecteur (voir GDD/demande utilisateur — "le grade peut être donné par l'admin") — initialisé au grade actuel du joueur sélectionné.</summary>
    [ObservableProperty]
    private UserRank _selectedRank;

    public IReadOnlyList<UserRank> AvailableRanks { get; } = Enum.GetValues<UserRank>();

    private bool CanSetRank() => SelectedUser is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanSetRank))]
    private async Task SetRank()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.SetRankAsync(SelectedUser.Id, SessionToken, SelectedRank);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanToggleMute() => SelectedUser is not null && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanToggleMute))]
    private async Task ToggleMute()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.SetMuteAsync(SelectedUser.Id, SessionToken, !SelectedUser.IsMuted);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBanIp() => SelectedUser is { LastKnownIp.Length: > 0 } && SessionToken is not null;

    [RelayCommand(CanExecute = nameof(CanBanIp))]
    private async Task BanIp()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.BanIpAsync(SelectedUser.Id, SessionToken);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            StatusMessage = $"IP {SelectedUser.LastKnownIp} bannie.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanResetProfile() => SelectedUser is not null && SessionToken is not null;

    /// <summary>Voir GDD/demande utilisateur — "possibilité de reset le profil en jeu de quelqu'un" : supprime tous ses personnages, pas le compte lui-même.</summary>
    [RelayCommand(CanExecute = nameof(CanResetProfile))]
    private async Task ResetProfile()
    {
        if (SelectedUser is null || SessionToken is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.ResetProfileAsync(SelectedUser.Id, SessionToken);
            if (!result.IsSuccess)
            {
                StatusMessage = result.Error;
                return;
            }

            await Search();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedUserChanged(AdminUserSummary? value)
    {
        BanCommand.NotifyCanExecuteChanged();
        UnbanCommand.NotifyCanExecuteChanged();
        DeleteUserCommand.NotifyCanExecuteChanged();
        RestoreUserCommand.NotifyCanExecuteChanged();
        ModifyUserCommand.NotifyCanExecuteChanged();
        ToggleAdminPermissionCommand.NotifyCanExecuteChanged();
        SetRankCommand.NotifyCanExecuteChanged();
        ToggleMuteCommand.NotifyCanExecuteChanged();
        BanIpCommand.NotifyCanExecuteChanged();
        ResetProfileCommand.NotifyCanExecuteChanged();
        SelectedRank = value?.Rank ?? UserRank.Joueur;
    }

    partial void OnBanReasonChanged(string value) => BanCommand.NotifyCanExecuteChanged();

    partial void OnNewUsernameChanged(string value) => ModifyUserCommand.NotifyCanExecuteChanged();

    partial void OnNewEmailChanged(string value) => ModifyUserCommand.NotifyCanExecuteChanged();
}
