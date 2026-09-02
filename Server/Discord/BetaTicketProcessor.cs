using Aetheria.Database.Context;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.Discord;

/// <summary>
/// Traite les candidatures bêta soumises sur le portail web (<c>Aetheria.Web</c>) : le portail se
/// contente d'écrire la ligne en base (l'IP partagée de son hébergeur est rate-limitée par
/// Discord), c'est ici — sur la machine qui héberge le bot — que se font les appels Discord.
///
/// À chaque passage (toutes les 30 s) :
/// <list type="bullet">
///   <item>candidature non traitée (<c>ProcessedAtUtc == null</c>) → vérifie la présence Discord ;
///     si le pseudo est introuvable, refus automatique avec la raison ; sinon création du salon
///     <c>beta-test-&lt;pseudo&gt;</c>.</item>
///   <item>candidature dont le <c>Status</c> a changé depuis le dernier <c>SyncedStatus</c>
///     (validée / refusée par un admin sur le site) → poste la mise à jour dans le salon, et
///     attribue le rôle Discord « Testeur » si acceptée.</item>
/// </list>
/// </summary>
public sealed class BetaTicketProcessor(
    IDbContextFactory<AetheriaDbContext> dbFactory,
    BetaTicketService tickets,
    ILogger<BetaTicketProcessor> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        if (!tickets.IsConfigured)
        {
            logger.LogInformation("BetaTicketProcessor : Discord non configuré, candidatures bêta non synchronisées.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Erreur pendant le traitement des candidatures bêta.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessOnceAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // 1. Nouvelles candidatures : vérification Discord + création du salon (ou refus auto).
        var unprocessed = await db.BetaApplications
            .Where(a => a.ProcessedAtUtc == null)
            .OrderBy(a => a.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        foreach (var application in unprocessed)
        {
            var linkedId = await db.Users
                .Where(u => u.Id == application.UserId)
                .Select(u => u.DiscordUserId)
                .FirstOrDefaultAsync(ct);

            var resolution = await tickets.ResolveMemberAsync(linkedId, application.DiscordHandle, ct);

            if (!resolution.Found || resolution.DiscordUserId is null)
            {
                // Erreur temporaire (rate limit, réseau) → on retentera au prochain passage.
                if (resolution.Error is "erreur temporaire de vérification Discord" or "vérification Discord non configurée côté serveur de jeu")
                {
                    logger.LogInformation("Candidature {Id} : vérification Discord reportée ({Reason}).", application.Id, resolution.Error);
                    continue;
                }

                application.Status = BetaApplicationStatus.Rejected;
                application.SyncedStatus = BetaApplicationStatus.Rejected;
                application.AdminNote = $"Refus automatique : {resolution.Error}.";
                application.ReviewedByUsername = "Vérification automatique";
                application.ReviewedAtUtc = DateTime.UtcNow;
                application.ProcessedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Candidature {Id} refusée automatiquement : {Reason}.", application.Id, resolution.Error);
                continue;
            }

            application.ResolvedDiscordUserId = resolution.DiscordUserId;
            var channelId = await tickets.CreateTicketAsync(application, resolution.DiscordUserId, ct);
            application.DiscordTicketChannelId = channelId;
            application.ProcessedAtUtc = DateTime.UtcNow;
            application.SyncedStatus = BetaApplicationStatus.Pending;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                channelId is null
                    ? "Candidature {Id} traitée mais salon Discord non créé (voir warnings ci-dessus)."
                    : "Candidature {Id} : salon Discord créé ({Channel}).",
                application.Id, channelId);
        }

        // 2. Décisions admin (validée / refusée sur le site) à répercuter dans le salon.
        var toSync = await db.BetaApplications
            .Where(a => a.ProcessedAtUtc != null
                && a.DiscordTicketChannelId != null
                && a.Status != BetaApplicationStatus.Pending
                && a.SyncedStatus != a.Status)
            .OrderBy(a => a.ReviewedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        foreach (var application in toSync)
        {
            var reviewer = string.IsNullOrWhiteSpace(application.ReviewedByUsername) ? "le staff" : application.ReviewedByUsername;

            if (application.Status == BetaApplicationStatus.Approved)
            {
                await tickets.PostToTicketAsync(application.DiscordTicketChannelId!,
                    $"✅ Candidature **acceptée** par {reviewer}. Bienvenue en bêta !", ct);

                if (application.ResolvedDiscordUserId is { Length: > 0 } discordId)
                {
                    await tickets.GrantTesterRoleAsync(discordId, ct);
                }
            }
            else if (application.Status == BetaApplicationStatus.Rejected)
            {
                var reason = string.IsNullOrWhiteSpace(application.AdminNote) ? "" : $" Raison : {application.AdminNote}";
                await tickets.PostToTicketAsync(application.DiscordTicketChannelId!,
                    $"❌ Candidature **refusée** par {reviewer}.{reason}", ct);
            }

            application.SyncedStatus = application.Status;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Candidature {Id} : décision « {Status} » répercutée sur Discord.", application.Id, application.Status);
        }
    }
}
