using System.Diagnostics;
using System.IO;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Localise et démarre le Client avec le jeton de session et le personnage choisi. En version
/// packagée, le Client est attendu à côté du Launcher (même dossier d'installation) ; en
/// développement, on retombe sur l'arborescence de build du dépôt. Nom d'exécutable
/// conditionnel à l'OS : "Aetheria.Client.exe" sous Windows, "Aetheria.Client" (sans extension)
/// sous Linux — voir Sites/README.md, section "Paquet Linux".
/// </summary>
public static class ClientLauncher
{
    private static readonly string ClientExecutableName = OperatingSystem.IsWindows() ? "Aetheria.Client.exe" : "Aetheria.Client";

    public static bool TryLaunch(string sessionToken, string serverHost, out string? error)
    {
        var clientPath = ResolveClientExecutablePath();
        if (clientPath is null)
        {
            error = $"{ClientExecutableName} introuvable. Compilez le Client (dotnet build Client/Aetheria.Client.csproj) avant de jouer.";
            return false;
        }

        // Le personnage n'est plus choisi ici : le Client affiche sa propre scène de
        // sélection/création en jeu (voir Client/Program.cs — SceneMode.CharacterSelect).
        // --host transmet l'adresse du serveur configurée dans les Paramètres (voir GDD/demande
        // utilisateur — jouer depuis un autre réseau contre le serveur distant) : sans ça, le
        // Client retombait toujours sur "localhost" par défaut (LaunchOptions.Parse), même quand
        // le Launcher pointait ailleurs.
        Process.Start(new ProcessStartInfo
        {
            FileName = clientPath,
            Arguments = $"--token=\"{sessionToken}\" --host=\"{serverHost}\"",
            UseShellExecute = false,
        });

        error = null;
        return true;
    }

    private static string? ResolveClientExecutablePath()
    {
        var launcherDirectory = AppContext.BaseDirectory;

        // Version packagée : Client à côté du Launcher.
        var sideBySide = Path.Combine(launcherDirectory, ClientExecutableName);
        if (File.Exists(sideBySide))
        {
            return sideBySide;
        }

        // Développement : sortie de build du dépôt (voir Directory.Build.props — BaseOutputPath).
        // launcherDirectory = <repo>/build/bin/Aetheria.Launcher/Debug/net10.0/
        var devPath = Path.GetFullPath(Path.Combine(
            launcherDirectory, "..", "..", "..", "Aetheria.Client", "Debug", "net10.0", ClientExecutableName));

        return File.Exists(devPath) ? devPath : null;
    }
}
