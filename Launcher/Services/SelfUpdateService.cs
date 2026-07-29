using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Voir GDD/demande utilisateur — "mise à jour obligatoire du Launcher" : télécharge le paquet
/// servi par <c>GET /api/updates/launcher-package</c> (voir Server/Program.cs, même zip que le
/// site — <c>Sites/downloads/AetheriaSetup.zip</c>), l'extrait, puis délègue la copie par-dessus
/// l'installation actuelle à un script PowerShell détaché : impossible d'écraser
/// Aetheria.Launcher.exe/.dll pendant qu'ils sont chargés par CE processus (fichier verrouillé
/// par Windows), il faut donc que ce soit fait par un AUTRE processus, une fois celui-ci terminé.
/// </summary>
public static class SelfUpdateService
{
    public static async Task<string?> DownloadAndApplyAsync(string serverHost, int port, IProgress<int> progress, CancellationToken ct = default)
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), "AetheriaUpdate_" + Guid.NewGuid().ToString("N"));
        var zipPath = Path.Combine(stagingRoot, "AetheriaSetup.zip");
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
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var payloadDir = Path.Combine(extractDir, "Payload");
            if (!Directory.Exists(payloadDir))
            {
                return "Paquet de mise à jour invalide (dossier Payload introuvable).";
            }

            progress.Report(96);

            var installDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            var currentProcessId = Environment.ProcessId;
            var launcherExePath = Path.Combine(installDir, "Aetheria.Launcher.exe");
            var scriptPath = Path.Combine(stagingRoot, "apply-update.ps1");

            // Voir remarque en tête de fichier : ce script tourne APRÈS la fermeture du Launcher
            // (attend que le PID actuel disparaisse), copie le nouveau Payload par-dessus
            // l'installation, relance le Launcher, puis se nettoie lui-même.
            var script = $$"""
                $ErrorActionPreference = 'SilentlyContinue'
                $deadline = (Get-Date).AddSeconds(30)
                while ((Get-Process -Id {{currentProcessId}} -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
                    Start-Sleep -Milliseconds 250
                }
                Start-Sleep -Milliseconds 500
                robocopy "{{payloadDir}}" "{{installDir}}" /E /NFL /NDL /NJH /NJS /NC /NS
                Start-Process -FilePath "{{launcherExePath}}"
                Remove-Item -Recurse -Force "{{stagingRoot}}"
                """;
            await File.WriteAllTextAsync(scriptPath, script, ct);

            progress.Report(100);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return $"Échec de la mise à jour : {ex.Message}";
        }
    }
}
