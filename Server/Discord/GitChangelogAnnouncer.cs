using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Note les nouveaux commits Git dans <see cref="PendingChangesLog"/> à chaque démarrage du
/// serveur (voir GDD/demande utilisateur — le flux de travail réel est "modifier le code →
/// reconstruire → relancer le serveur" : relancer le serveur EST la mise à jour). Ne poste plus
/// rien directement sur Discord — l'envoi groupé quotidien à 23h est géré par
/// <see cref="DailyDigestScheduler"/>, qui vide ce journal après l'avoir posté.
/// </summary>
public static class GitChangelogAnnouncer
{
    private const string StateFileName = ".discord-last-logged";
    private const int MaxCommitsOnFirstRun = 20;

    public static async Task LogNewCommitsAsync(ILogger logger, CancellationToken ct = default)
    {
        var stateFilePath = RepoPath.Resolve(StateFileName);
        var repoRoot = Path.GetDirectoryName(stateFilePath)!;
        var lastLoggedSha = File.Exists(stateFilePath) ? (await File.ReadAllTextAsync(stateFilePath, ct)).Trim() : null;

        var currentSha = RunGit(repoRoot, "rev-parse HEAD")?.Trim();
        if (string.IsNullOrEmpty(currentSha))
        {
            logger.LogWarning("Impossible de lire le commit HEAD courant : journalisation des commits ignorée.");
            return;
        }

        if (currentSha == lastLoggedSha)
        {
            return; // Déjà journalisé lors d'un démarrage précédent — pas de nouveau commit depuis.
        }

        var logArgs = lastLoggedSha is null
            ? $"log -{MaxCommitsOnFirstRun} --pretty=format:%s"
            : $"log {lastLoggedSha}..HEAD --pretty=format:%s";

        var commitSubjects = (RunGit(repoRoot, logArgs) ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        PendingChangesLog.Append(commitSubjects);

        await File.WriteAllTextAsync(stateFilePath, currentSha, ct);
    }

    private static string? RunGit(string workingDirectory, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
