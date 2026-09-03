using Aetheria.Database.Context;
using Aetheria.Database.Services;
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

        await EnsureReferralCodesAsync(db, ct);

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
            var ticket = await tickets.CreateTicketAsync(application, resolution.DiscordUserId, ct);
            application.DiscordTicketChannelId = ticket.ChannelId;
            application.DiscordTicketMessageId = ticket.MessageId;
            application.ProcessedAtUtc = DateTime.UtcNow;
            application.SyncedStatus = BetaApplicationStatus.Pending;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                ticket.ChannelId is null
                    ? "Candidature {Id} traitée mais salon Discord non créé (voir warnings ci-dessus)."
                    : "Candidature {Id} : salon Discord créé ({Channel}).",
                application.Id, ticket.ChannelId);
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
                    // Voir demande utilisateur — annonce publique de bienvenue à l'acceptation
                    // (le message ne part plus à l'arrivée sur le serveur Discord).
                    await tickets.PostWelcomeAsync(discordId, ct);
                }

                // Voir demande utilisateur — récap dans le salon des acceptés (mêmes infos, sans boutons).
                await tickets.PostAcceptedApplicationAsync(application, reviewer, ct);
            }
            else if (application.Status == BetaApplicationStatus.Rejected)
            {
                var reason = string.IsNullOrWhiteSpace(application.AdminNote) ? "" : $" Raison : {application.AdminNote}";
                await tickets.PostToTicketAsync(application.DiscordTicketChannelId!,
                    $"❌ Candidature **refusée** par {reviewer}.{reason}", ct);
            }

            // Retire les boutons du message de ticket (décision prise sur le site) et propose une
            // fermeture. Le ticket reste ouvert — c'est au staff de cliquer « Fermer le ticket ».
            if (application.DiscordTicketMessageId is { Length: > 0 } messageId)
            {
                await tickets.DisableTicketButtonsAsync(application.DiscordTicketChannelId!, messageId, application.Id, ct);
            }

            await tickets.PostCloseProposalAsync(application.DiscordTicketChannelId!, ct);

            application.SyncedStatus = application.Status;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Candidature {Id} : décision « {Status} » répercutée sur Discord.", application.Id, application.Status);
        }
    }

    /// <summary>
    /// Attribue un code de parrainage aux comptes qui y ont droit (grade Testeur ou plus) mais
    /// n'en ont pas encore — et journalise chaque nouveau code dans le salon « inscriptions »
    /// (voir demande utilisateur). Couvre les acceptations faites depuis le site (qui ne peut pas
    /// parler à Discord) et les promotions de grade manuelles.
    /// </summary>
    private async Task EnsureReferralCodesAsync(AetheriaDbContext db, CancellationToken ct)
    {
        var eligibleRanks = new[] { UserRank.Testeur, UserRank.Ami, UserRank.Moderateur, UserRank.Fondateur };
        var candidates = await db.Users
            .Where(u => u.ReferralCode == null && !u.IsDeleted && (u.IsAdmin || eligibleRanks.Contains(u.Rank)))
            .Take(20)
            .ToListAsync(ct);

        foreach (var user in candidates)
        {
            var code = await ReferralService.EnsureCodeAsync(db, user, ct);
            if (code is null)
            {
                continue;
            }

            await db.SaveChangesAsync(ct);
            DiscordEventLog.LogReferral(user.Username, user.Id, code);
            logger.LogInformation("Code de parrainage attribué à {Username} : {Code}.", user.Username, code);
        }
    }
}
