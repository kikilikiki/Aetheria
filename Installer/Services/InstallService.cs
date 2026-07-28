using System.IO;

namespace Aetheria.Installer.Services;

/// <summary>Résultat d'une installation.</summary>
public sealed record InstallResult(bool Success, string? InstalledPath, string? Error);

/// <summary>
/// Copie les fichiers du jeu (Launcher, Client, Server publiés — voir <c>Payload/</c> à côté
/// de l'exécutable de l'installateur) vers le dossier choisi par l'utilisateur, et crée
/// optionnellement un raccourci sur le bureau. Pas de désinstallateur/registre Windows pour
/// cette première version (voir Docs/README.md).
/// </summary>
public sealed class InstallService
{
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

            return new InstallResult(true, targetDirectory, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new InstallResult(false, null, $"Échec de l'installation : {ex.Message}");
        }
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
