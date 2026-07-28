using Aetheria.Shared;
using Microsoft.Win32;

namespace Aetheria.Installer.Services;

/// <summary>
/// Enregistre/retire l'entrée "Applications" / "Programmes et fonctionnalités" de Windows (voir
/// GDD/demande utilisateur — "quand on installe le jeu avec l'installer il doit être affiché dans
/// les programs"). Clé sous <c>HKEY_CURRENT_USER</c> (pas <c>HKEY_LOCAL_MACHINE</c>) car
/// l'installation par défaut se fait dans <c>%LocalAppData%</c>, un emplacement propre à
/// l'utilisateur qui ne nécessite pas de droits administrateur — cohérent avec un désinstallateur
/// qui n'en demande pas non plus.
/// </summary>
public static class UninstallRegistryService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Aetheria";

    public static void Register(string installPath, string uninstallExePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue("DisplayName", GameInfo.Name);
        key.SetValue("DisplayVersion", GameInfo.Version);
        key.SetValue("Publisher", "Aetheria Studio");
        key.SetValue("InstallLocation", installPath);
        key.SetValue("UninstallString", $"\"{uninstallExePath}\" --uninstall --path=\"{installPath}\"");
        key.SetValue("DisplayIcon", uninstallExePath);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public static void Unregister() => Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);
}
