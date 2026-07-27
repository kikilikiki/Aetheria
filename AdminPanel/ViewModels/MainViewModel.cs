using System.Collections.ObjectModel;
using Aetheria.AdminPanel.Services;
using Aetheria.Shared.Models.Admin;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.AdminPanel.ViewModels;

/// <summary>
/// Gestion des joueurs et statistiques globales (voir <c>Docs/GameDesign.md</c> — section
/// AdminPanel). Pas d'authentification admin dédiée pour cette version (voir Docs/README.md).
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AdminApiClient _api = new();

    public ObservableCollection<AdminUserSummary> Users { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private AdminUserSummary? _selectedUser;

    [ObservableProperty]
    private string _banReason = string.Empty;

    [ObservableProperty]
    private AdminGlobalStats? _stats;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

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

    partial void OnSelectedUserChanged(AdminUserSummary? value)
    {
        BanCommand.NotifyCanExecuteChanged();
        UnbanCommand.NotifyCanExecuteChanged();
    }

    partial void OnBanReasonChanged(string value) => BanCommand.NotifyCanExecuteChanged();
}
