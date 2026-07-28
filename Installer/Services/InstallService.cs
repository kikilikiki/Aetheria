using System.IO;

namespace Aetheria.Installer.Services;

/// <summary>Résultat d'une installation.</summary>
public sealed record InstallResult(bool Success, string? InstalledPath, string? Error);

/// <summary>
/// Copie les fichiers du jeu (Launcher, Client, Server publiés — voir <c>Payload/</c> à côté
/// de l'exécutable de l'installateur) vers le dossier choisi par l'utilisateur, crée
/// optionnellement un raccourci sur le bureau, et enregistre l'entrée "Applications" de Windows
/// (voir GDD/demande utilisateur — apparaître dans les programmes installés) avec un
/// désinstallateur fonctionnel (voir <see cref="Uninstall"/>, <c>App.xaml.cs</c> pour le mode
/// silencieux <c>--uninstall</c>).
/// </summary>
public sealed class InstallService
{
    private const string UninstallExeName = "Uninstall.exe";

    public InstallResult Install(string sourcePayloadDirectory, string targetDirectory, bool createDesktopShortcut, string? desktopDirectoryOverride = null)
    {
        if (!Directory.Exists(sourcePayloadDirectory))
        {
            return new InstallResult(false, null,
                $"Dossier d'installation source introuvable : {sourcePayloadDirectory}. " +
                "Le paquet (Payload) n'a pas été construit à côté de l'installateur.");
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
            CopyDirectory(sourcePayloadDirectory, targetDirectory);

            if (createDesktopShortcut)
            {
                var desktopDirectory = desktopDirectoryOverride
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                var launcherExePath = Path.Combine(targetDirectory, "Aetheria.Launcher.exe");
                if (File.Exists(launcherExePath))
                {
                    CreateShortcut(Path.Combine(desktopDirectory, "Aetheria.lnk"), launcherExePath, targetDirectory);
                }
            }

            // Copie l'installateur lui-même dans le dossier cible sous le nom "Uninstall.exe" :
            // le désinstallateur reste disponible même si l'utilisateur supprime le fichier
            // d'installation téléchargé d'origine. Relancé plus tard avec --uninstall (voir
            // App.xaml.cs), il tourne en mode silencieux sans afficher de fenêtre.
            var currentExePath = Environment.ProcessPath;
            var uninstallExePath = Path.Combine(targetDirectory, UninstallExeName);
            if (currentExePath is not null && File.Exists(currentExePath))
            {
                File.Copy(currentExePath, uninstallExePath, overwrite: true);
                UninstallRegistryService.Register(targetDirectory, uninstallExePath);
            }

            return new InstallResult(true, targetDirectory, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new InstallResult(false, null, $"Échec de l'installation : {ex.Message}");
        }
    }

    /// <summary>Supprime les fichiers installés, le raccourci bureau et l'entrée de désinstallation Windows — voir <c>App.xaml.cs</c> (mode <c>--uninstall</c>).</summary>
    public void Uninstall(string installPath, string? desktopDirectoryOverride = null)
    {
        try
        {
            var desktopDirectory = desktopDirectoryOverride
                ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var shortcutPath = Path.Combine(desktopDirectory, "Aetheria.lnk");
            if (File.Exists(shortcutPath))
            {
                File.Delete(shortcutPath);
            }

            UninstallRegistryService.Unregister();

            if (Directory.Exists(installPath))
            {
                // Le désinstallateur (Uninstall.exe) tourne depuis ce même dossier : on ne peut
                // pas se supprimer soi-même pendant l'exécution sous Windows. On efface donc tout
                // le reste, puis on programme la suppression du dossier restant (juste
                // Uninstall.exe et le dossier vide) via une commande différée après la sortie du
                // processus.
                foreach (var entry in Directory.GetFileSystemEntries(installPath))
                {
                    if (string.Equals(Path.GetFileName(entry), UninstallExeName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, recursive: true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }

                ScheduleSelfDelete(installPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Désinstallation silencieuse (voir App.xaml.cs) : rien à afficher à l'utilisateur ici.
        }
    }

    /// <summary>
    /// Programme la suppression de <c>Uninstall.exe</c> lui-même et du dossier d'installation
    /// après un court délai, via une commande <c>cmd.exe</c> détachée — un exécutable Windows ne
    /// peut pas supprimer son propre fichier pendant qu'il tourne, technique classique pour tout
    /// désinstallateur auto-suffisant (le délai laisse le temps à ce processus de se terminer).
    /// </summary>
    private static void ScheduleSelfDelete(string installPath)
    {
        var command = $"/C ping 127.0.0.1 -n 3 >NUL & rmdir /S /Q \"{installPath}\"";

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        });
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    /// <summary>
    /// Crée un raccourci .lnk via l'objet COM WScript.Shell (late-bound, aucune référence COM
    /// dédiée nécessaire) — la seule façon standard de créer un raccourci Windows sans
    /// bibliothèque tierce.
    /// </summary>
    private static void CreateShortcut(string shortcutPath, string targetExePath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell indisponible sur cette machine.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            var shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExePath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }
}
