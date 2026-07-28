using Aetheria.Shared;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Poste une fois par jour, à 23h heure locale, le contenu accumulé de
/// <see cref="PendingChangesLog"/>, puis le vide (voir demande utilisateur — "tous les jours à
/// 23h les modifications sont postées, après l'envoi le fichier se retransforme en rien du tout,
/// et si le fichier est vide le message ne doit pas être transmis"). Tourne en tâche de fond
/// pendant toute la durée de vie du serveur (voir <c>Program.cs</c>), avec une vérification
/// simple toutes les minutes plutôt qu'une vraie planification au tick près — largement
/// suffisant pour une fenêtre d'une minute.
/// </summary>
public sealed class DailyDigestScheduler(DiscordAnnouncer announcer, ILogger<DailyDigestScheduler> logger)
{
    private const int DigestHour = 23;
    private const string LastDigestDateFileName = ".discord-last-digest-date";

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckAndPostIfDueAsync(ct);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Erreur de fichier lors de la vérification du digest Discord quotidien.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckAndPostIfDueAsync(CancellationToken ct)
    {
        var now = DateTime.Now;
        if (now.Hour < DigestHour)
        {
            return;
        }

        var statePath = RepoPath.Resolve(LastDigestDateFileName);
        var today = now.ToString("yyyy-MM-dd");
        var lastDigestDate = File.Exists(statePath) ? (await File.ReadAllTextAsync(statePath, ct)).Trim() : null;

        if (lastDigestDate == today)
        {
            return; // Déjà traité aujourd'hui (envoyé ou ignoré si vide) — une seule fois par jour.
        }

        var pending = PendingChangesLog.ReadAndClear();
        if (pending.Count > 0)
        {
            await announcer.PostUpdateAsync(
                $"{GameInfo.Name} — récapitulatif du jour",
                $"{pending.Count} changement(s) aujourd'hui.",
                pending,
                ct);
        }

        await File.WriteAllTextAsync(statePath, today, ct);
    }
}
