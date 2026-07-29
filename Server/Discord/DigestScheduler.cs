using Aetheria.Shared;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Poste toutes les heures (voir GDD/demande utilisateur — "au lieu de 23h, fait en sorte que ça
/// soit toutes les heures") le contenu accumulé de <see cref="PendingChangesLog"/>, puis le vide
/// (si le fichier est vide, le message n'est pas transmis). Tourne en tâche de fond pendant toute
/// la durée de vie du serveur (voir <c>Program.cs</c>), avec une vérification simple toutes les
/// minutes plutôt qu'une vraie planification au tick près — largement suffisant pour une fenêtre
/// d'une minute.
/// </summary>
public sealed class DigestScheduler(DiscordAnnouncer announcer, ILogger<DigestScheduler> logger)
{
    private const string LastDigestHourFileName = ".discord-last-digest-hour";

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
                logger.LogWarning(ex, "Erreur de fichier lors de la vérification du digest Discord.");
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
        var statePath = RepoPath.Resolve(LastDigestHourFileName);
        var currentHourBucket = now.ToString("yyyy-MM-dd-HH");
        var lastDigestHourBucket = File.Exists(statePath) ? (await File.ReadAllTextAsync(statePath, ct)).Trim() : null;

        if (lastDigestHourBucket == currentHourBucket)
        {
            return; // Déjà traité cette heure-ci (envoyé ou ignoré si vide) — une seule fois par heure.
        }

        var pending = PendingChangesLog.ReadAndClear();
        if (pending.Count > 0)
        {
            // Voir GDD/demande utilisateur — "désactive le ping [sur] les récapitulatifs" : un
            // récapitulatif horaire automatique n'a pas besoin de notifier le rôle à chaque fois,
            // contrairement à une annonce manuelle (voir DiscordAnnouncer.PostUpdateAsync).
            await announcer.PostUpdateAsync(
                $"{GameInfo.Name} — récapitulatif de l'heure",
                $"{pending.Count} changement(s) cette heure-ci.",
                pending,
                pingRole: false,
                ct: ct);
        }

        await File.WriteAllTextAsync(statePath, currentHourBucket, ct);
    }
}
