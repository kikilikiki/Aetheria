using System.Windows;
using Aetheria.Installer.Services;

namespace Aetheria.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Mode silencieux invoqué depuis l'entrée "Applications" de Windows (voir
        // UninstallRegistryService, InstallService.Install) — désinstalle sans afficher la
        // fenêtre de l'installateur, puis quitte immédiatement.
        if (e.Args.Contains("--uninstall"))
        {
            var installPath = e.Args
                .FirstOrDefault(a => a.StartsWith("--path=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1].Trim('"');

            if (!string.IsNullOrWhiteSpace(installPath))
            {
                new InstallService().Uninstall(installPath);
            }

            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
