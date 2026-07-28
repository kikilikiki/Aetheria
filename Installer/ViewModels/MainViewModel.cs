using System.IO;
using Aetheria.Installer.Services;
using Aetheria.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aetheria.Installer.ViewModels;

/// <summary>Écran unique de l'installateur : dossier cible, raccourci bureau, installer.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly InstallService _installService = new();

    [ObservableProperty]
    private string _title = $"Installation de {GameInfo.Name} — v{GameInfo.Version}";

    [ObservableProperty]
    private string _installPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aetheria");

    [ObservableProperty]
    private bool _createDesktopShortcut = true;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isInstalled;

    [RelayCommand]
    private async Task Install()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var payloadDirectory = Path.Combine(AppContext.BaseDirectory, "Payload");

            var result = await Task.Run(() =>
                _installService.Install(payloadDirectory, InstallPath, CreateDesktopShortcut));

            if (result.Success)
            {
                IsInstalled = true;
                StatusMessage = $"Aetheria a été installé dans {result.InstalledPath}.";
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
}
