using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Voir GDD/demande utilisateur — "mise à jour obligatoire du Launcher" : télécharge le paquet
/// servi par <c>GET /api/updates/launcher-package</c> (voir Server/Program.cs — Payload.zip,
/// même contenu que celui embarqué dans AetheriaSetup.exe, fichiers directement à la racine du
/// zip, PAS de sous-dossier "Payload/"), l'extrait, puis délègue la copie par-dessus
/// l'installation actuelle à un script détaché : impossible d'écraser les binaires du Launcher
/// pendant qu'ils sont chargés par CE processus, il faut donc que ce soit fait par un AUTRE
/// processus, une fois celui-ci terminé. Script PowerShell sous Windows, script shell POSIX sous
/// Linux (voir Sites/README.md, section "Paquet Linux") — même séquence attendre/copier/relancer/
/// nettoyer dans les deux cas.
/// </summary>
public static class SelfUpdateService
{
    private static readonly string LauncherExecutableName = OperatingSystem.IsWindows() ? "Aetheria.Launcher.exe" : "Aetheria.Launcher";

    public static async Task<string?> DownloadAndApplyAsync(string serverHost, int port, IProgress<int> progress, CancellationToken ct = default)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "AetheriaUpdate_" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(stagingRoot, "AetheriaPayload.zip");
        var extractDir = Path.Combine(stagingRoot, "extracted");
        Directory.CreateDirectory(stagingRoot);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await http.GetAsync(
                $"http://{serverHost}:{port}/api/updates/launcher-package",
                HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                return $"Téléchargement impossible (serveur : {(int)response.StatusCode}).";
            }

            var totalBytes = response.Content.Headers.ContentLength;
            await using (var httpStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = File.Create(zipPath))
            {
                var buffer = new byte[81920];
                long readSoFar = 0;
                int read;
                while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    readSoFar += read;
                    if (totalBytes is > 0)
                    {
                        // Le téléchargement seul compte pour 90% de la barre, les 10% restants
                        // couvrent l'extraction/préparation du script (voir plus bas) — évite que
                        // la barre reste bloquée à 100% pendant plusieurs secondes après la fin du
                        // transfert réseau, ce qui donnait l'impression d'un plantage.
                        progress.Report((int)(readSoFar * 90 / totalBytes.Value));
                    }
                }
            }

            progress.Report(92);
            // Voir EmbeddedPayloadExtractor (même Payload.zip, même problème) : les sorties
            // Launcher+Client fusionnées à plat contiennent des dépendances partagées en double
            // au même chemin (Aetheria.Shared.dll, etc.) - overwriteFiles: true nécessaire, sinon
            // ZipFile.ExtractToDirectory lève une IOException "already exists" sur la deuxième copie.
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            var extractedLauncherPath = Path.Combine(extractDir, LauncherExecutableName);
            if (!File.Exists(extractedLauncherPath))
            {
                return $"Paquet de mise à jour invalide ({LauncherExecutableName} introuvable).";
            }

            // L'extraction ZIP ne restaure pas le bit exécutable POSIX sous Linux (contrairement
            // à Windows, où l'extension .exe suffit) — sans ça, le script de relance ci-dessous
            // échouerait silencieusement à démarrer le nouveau Launcher.
            if (!OperatingSystem.IsWindows())
            {
                TrySetExecutable(extractedLauncherPath);
                var extractedClientPath = Path.Combine(extractDir, "Aetheria.Client");
                if (File.Exists(extractedClientPath))
                {
                    TrySetExecutable(extractedClientPath);
                }
            }

            progress.Report(96);

            var installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            var currentProcessId = Environment.ProcessId;
            var launcherExePath = Path.Combine(installDir, LauncherExecutableName);

            var (scriptPath, startInfo) = OperatingSystem.IsWindows()
                ? PrepareWindowsScript(stagingRoot, extractDir, installDir, launcherExePath, currentProcessId)
                : PrepareLinuxScript(stagingRoot, extractDir, installDir, launcherExePath, currentProcessId);

            await File.WriteAllTextAsync(scriptPath, startInfo.Script, ct);
            if (!OperatingSystem.IsWindows())
            {
                TrySetExecutable(scriptPath);
            }

            progress.Report(100);

            // Pas de -WindowStyle Hidden / -ExecutionPolicy Bypass ni CreateNoWindow : ce
            // combo (fenetre cachee + contournement de policy) fait qu'un script qui remplace
            // silencieusement les binaires de l'appli puis la relance ressemble a un dropper de
            // malware auto-replicant, et se fait bloquer par Smart App Control / la detection
            // comportementale de Defender independamment de la signature du binaire. RemoteSigned
            // suffit a executer ce script local (pas telecharge) sans Bypass.
            Process.Start(new ProcessStartInfo
            {
                FileName = startInfo.FileName,
                Arguments = startInfo.Arguments,
                UseShellExecute = false,
            });

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return $"Échec de la mise à jour : {ex.Message}";
        }
    }

    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void TrySetExecutable(string path)
    {
        try
        {
            File.SetUnixFileMode(path, UnixFileModeExecutable);
        }
        catch (IOException)
        {
            // Best-effort : si ça échoue, le script de relance ci-dessous refera un chmod +x.
        }
    }

    private const UnixFileMode UnixFileModeExecutable =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static (string ScriptPath, (string FileName, string Arguments, string Script) Info) PrepareWindowsScript(
        string stagingRoot, string extractDir, string installDir, string launcherExePath, int currentProcessId)
    {
        var scriptPath = Path.Combine(stagingRoot, "apply-update.ps1");

        // Attend que le PID actuel disparaisse, copie le nouveau Payload par-dessus
        // l'installation, relance le Launcher, puis se nettoie lui-même.
        var script = $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            $deadline = (Get-Date).AddSeconds(30)
            while ((Get-Process -Id {{currentProcessId}} -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 250
            }
            Start-Sleep -Milliseconds 500
            robocopy "{{extractDir}}" "{{installDir}}" /MIR /NFL /NDL /NJH /NJS /NC /NS
            Start-Process -FilePath "{{launcherExePath}}"
            Remove-Item -Recurse -Force "{{stagingRoot}}"
            """;

        var arguments = $"-NoProfile -ExecutionPolicy RemoteSigned -File \"{scriptPath}\"";
        return (scriptPath, ("powershell.exe", arguments, script));
    }

    private static (string ScriptPath, (string FileName, string Arguments, string Script) Info) PrepareLinuxScript(
        string stagingRoot, string extractDir, string installDir, string launcherExePath, int currentProcessId)
    {
        var scriptPath = Path.Combine(stagingRoot, "apply-update.sh");

        // Équivalent POSIX du script PowerShell ci-dessus : `kill -0` teste juste l'existence du
        // PID (aucun signal réellement envoyé) plutôt que Get-Process, `cp -a` mirror avec
        // suppression préalable du contenu existant plutôt que robocopy /MIR (pas d'équivalent
        // direct en simple copie récursive POSIX).
        var script = $$"""
            #!/bin/sh
            set -e
            deadline=$(( $(date +%s) + 30 ))
            while kill -0 {{currentProcessId}} 2>/dev/null && [ "$(date +%s)" -lt "$deadline" ]; do
                sleep 0.25
            done
            sleep 0.5
            rm -rf "{{installDir}}"/*
            cp -a "{{extractDir}}"/. "{{installDir}}"/
            chmod +x "{{launcherExePath}}"
            nohup "{{launcherExePath}}" >/dev/null 2>&1 &
            rm -rf "{{stagingRoot}}"
            """;

        return (scriptPath, ("/bin/sh", $"\"{scriptPath}\"", script));
    }
}
