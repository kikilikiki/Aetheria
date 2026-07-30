using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Aetheria.Installer.Services;

/// <summary>
/// Voir GDD/demande utilisateur — "au lieu d'un zip, fait en sorte que ce soit juste un exécutable
/// pour télécharger le Launcher avec le jeu" : la version publiée en fichier unique auto-suffisant
/// (voir Sites/README.md, <c>dotnet publish -p:PublishSingleFile=true --self-contained</c>) embarque
/// <c>Payload.zip</c> comme ressource (voir <c>Aetheria.Installer.csproj</c>) au lieu d'un dossier
/// <c>Payload/</c> à côté de l'exécutable — il n'y a alors plus qu'un seul fichier à distribuer.
/// Le dossier <c>Payload/</c> à côté de l'exécutable reste prioritaire s'il existe (mode
/// développement/zip classique, voir <see cref="Aetheria.Client.LaunchOptions"/> pour la même
/// convention "packagé vs dev" côté Client).
/// </summary>
public static class EmbeddedPayloadExtractor
{
    private const string ResourceName = "Aetheria.Installer.Resources.Payload.zip";

    /// <summary>Extrait la ressource embarquée vers un dossier temporaire et retourne son chemin, ou <c>null</c> si l'exécutable n'a pas été publié avec le Payload embarqué (voir <see cref="ResourceName"/>).</summary>
    public static string? ExtractToTempDirectory()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return null;
        }

        var targetDirectory = Path.Combine(Path.GetTempPath(), "AetheriaInstallerPayload_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        // Le zip contient les sorties de publication du Launcher ET du Client fusionnées à plat
        // (voir Sites/README.md — Compress-Archive avec deux sources) : les dépendances partagées
        // (Aetheria.Shared.dll, etc.) apparaissent deux fois au même chemin. overwriteFiles: true
        // est donc correct ici (pas juste un contournement) — les deux copies sont identiques et
        // doivent de toute façon fusionner dans le même dossier d'installation final.
        archive.ExtractToDirectory(targetDirectory, overwriteFiles: true);

        return targetDirectory;
    }
}
