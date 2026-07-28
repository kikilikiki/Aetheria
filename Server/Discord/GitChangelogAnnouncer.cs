using System.Diagnostics;
using Aetheria.Shared;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Poste automatiquement les nouveaux commits Git dans le salon Discord du projet à chaque
/// démarrage du serveur (voir GDD/demande utilisateur — "à chaque mise à jour que tu fais, le
/// bot envoie un message automatiquement", sans étape manuelle). Le flux de travail réel est
/// "modifier le code → reconstruire → relancer le serveur" : relancer le serveur EST la mise à
/// jour, donc détecter les commits ajoutés depuis le dernier démarrage annoncé suffit — pas
/// besoin d'un hook Git séparé ni d'un service de surveillance de fichiers.
/// </summary>
public static class GitChangelogAnnouncer
{
    private const string StateFileName = ".discord-last-announced";
    private const int MaxCommitsOnFirstRun = 20;

    public static async Task AnnounceNewCommitsAsync(DiscordAnnouncer announcer, ILogger logger, CancellationToken ct = default)
    {
        if (!announcer.IsConfigured)
        {
            // Pas de jeton configuré (voir .env.exemple) : rien à annoncer, DiscordAnnouncer
            // journalise déjà ce cas en détail si on l'appelle explicitement.
            return;
        }

        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            logger.LogWarning("Dépôt Git introuvable depuis {Cwd} : annonce automatique de mise à jour ignorée.", Directory.GetCurrentDirectory());
            return;
        }

        var stateFilePath = Path.Combine(repoRoot, StateFileName);
        var lastAnnouncedSha = File.Exists(stateFilePath) ? (await File.ReadAllTextAsync(stateFilePath, ct)).Trim() : null;

        var currentSha = RunGit(repoRoot, "rev-parse HEAD")?.Trim();
        if (string.IsNullOrEmpty(currentSha))
        {
            logger.LogWarning("Impossible de lire le commit HEAD courant : annonce automatique de mise à jour ignorée.");
            return;
        }

        if (currentSha == lastAnnouncedSha)
        {
            return; // Déjà annoncé lors d'un démarrage précédent — pas de nouveau commit depuis.
        }

        var logArgs = lastAnnouncedSha is null
            ? $"log -{MaxCommitsOnFirstRun} --pretty=format:%s"
            : $"log {lastAnnouncedSha}..HEAD --pretty=format:%s";

        var commitSubjects = (RunGit(repoRoot, logArgs) ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (commitSubjects.Count > 0)
        {
            var description = lastAnnouncedSha is null
                ? $"Démarrage du serveur — {commitSubjects.Count} commit(s) récent(s)."
                : $"Démarrage du serveur — {commitSubjects.Count} nouveau(x) commit(s) depuis la dernière annonce.";

            await announcer.PostUpdateAsync($"{GameInfo.Name} mis à jour", description, commitSubjects, ct);
        }

        await File.WriteAllTextAsync(stateFilePath, currentSha, ct);
    }

    /// <summary>Cherche un dossier <c>.git</c> à partir du répertoire courant, puis ses parents — le serveur est lancé depuis la racine du dépôt en développement (voir README), mais on remonte par sécurité.</summary>
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
        }

        return null;
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
