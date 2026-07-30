using System.Windows;
using System.Windows.Threading;
using Aetheria.Installer.Services;

namespace Aetheria.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Voir retour utilisateur — "l'installateur crash quand on appuie sur le bouton
        // installer" : sans ce handler, une exception non gérée (ex. ZipArchive.ExtractToDirectory
        // sur un doublon d'entrée, voir EmbeddedPayloadExtractor) termine tout le processus sans
        // le moindre message — juste une disparition silencieuse de la fenêtre. Affiche l'erreur
        // à la place de planter dans le vide, quelle que soit la cause exacte.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Une erreur inattendue est survenue :\n\n{args.Exception.Message}",
                "Erreur d'installation", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

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
