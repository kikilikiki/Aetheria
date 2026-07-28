using System.Diagnostics;
using System.IO;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Localise et démarre Aetheria.Client.exe avec le jeton de session et le personnage choisi.
/// En version packagée, Client.exe est attendu à côté de Launcher.exe (même dossier
/// d'installation) ; en développement, on retombe sur l'arborescence de build du dépôt.
/// </summary>
public static class ClientLauncher
{
    public static bool TryLaunch(string sessionToken, out string? error)
    {
        var clientPath = ResolveClientExecutablePath();
        if (clientPath is null)
        {
            error = "Aetheria.Client.exe introuvable. Compilez le Client (dotnet build Client/Aetheria.Client.csproj) avant de jouer.";
            return false;
        }

        // Le personnage n'est plus choisi ici : le Client affiche sa propre scène de
        // sélection/création en jeu (voir Client/Program.cs — SceneMode.CharacterSelect).
        Process.Start(new ProcessStartInfo
        {
            FileName = clientPath,
            Arguments = $"--token=\"{sessionToken}\"",
            UseShellExecute = false,
        });

        error = null;
        return true;
    }

    private static string? ResolveClientExecutablePath()
    {
        var launcherDirectory = AppContext.BaseDirectory;

        // Version packagée : Client.exe à côté de Launcher.exe.
        var sideBySide = Path.Combine(launcherDirectory, "Aetheria.Client.exe");
        if (File.Exists(sideBySide))
        {
            return sideBySide;
        }

        // Développement : sortie de build du dépôt (voir Directory.Build.props — BaseOutputPath).
        // launcherDirectory = <repo>/build/bin/Aetheria.Launcher/Debug/net10.0/
        var devPath = Path.GetFullPath(Path.Combine(
            launcherDirectory, "..", "..", "..", "Aetheria.Client", "Debug", "net10.0", "Aetheria.Client.exe"));

        return File.Exists(devPath) ? devPath : null;
    }
}
