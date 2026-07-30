using Aetheria.Database.Context;
using Aetheria.Server.Discord;
using Aetheria.Server.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "la guerre des territoires doit être tous les samedis" :
/// <see cref="KingdomWarService.ResolveWeeklyWarAsync"/> existait déjà mais se déclenchait au
/// premier changement de semaine ISO (n'importe quel jour) plutôt qu'un jour précis. Résout
/// désormais uniquement le samedi (UTC), même mécanique de fichier témoin qu'un
/// <see cref="DigestScheduler"/> pour éviter une double résolution au redémarrage/à chaque
/// vérification dans la même journée.
/// </summary>
public sealed class KingdomWarScheduler(IDbContextFactory<AetheriaDbContext> dbContextFactory, ILogger<KingdomWarScheduler> logger, SessionTokenStore tokenStore)
{
    private static readonly string WarStatePath = RepoPath.Resolve(".kingdom-war-last-week");

    /// <summary>Voir GDD/demande utilisateur — "le roi tous les semaines (dimanche)" : fichier témoin séparé de celui de la guerre (samedi), pour un jour de résolution indépendant.</summary>
    private static readonly string ElectionStatePath = RepoPath.Resolve(".kingdom-election-last-week");

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ResolveWarIfDueAsync(ct);
                await ResolveElectionIfDueAsync(ct);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Erreur de fichier lors de la vérification hebdomadaire des royaumes.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static string CurrentWeekBucket(DateTime now)
    {
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        return $"{now.Year}-W{calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):00}";
    }

    private async Task ResolveWarIfDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now.DayOfWeek != DayOfWeek.Saturday)
        {
            return;
        }

        var currentWeekBucket = CurrentWeekBucket(now);
        var lastResolvedWeekBucket = File.Exists(WarStatePath) ? (await File.ReadAllTextAsync(WarStatePath, ct)).Trim() : null;
        if (lastResolvedWeekBucket == currentWeekBucket)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var message = await new KingdomWarService(db).ResolveWeeklyWarAsync(ct);
        logger.LogInformation("Guerre de royaumes résolue automatiquement (samedi) : {Message}", message);

        // Voir GDD/demande utilisateur — "Guerres de guildes" : même cadence hebdomadaire (samedi).
        var guildWarMessage = await new GuildService(db, tokenStore).ResolveWeeklyWarAsync(ct);
        logger.LogInformation("Guerre de guildes résolue automatiquement (samedi) : {Message}", guildWarMessage);

        await File.WriteAllTextAsync(WarStatePath, currentWeekBucket, ct);
    }

    /// <summary>Voir GDD/demande utilisateur — "élections du roi tous les dimanches".</summary>
    private async Task ResolveElectionIfDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now.DayOfWeek != DayOfWeek.Sunday)
        {
            return;
        }

        var currentWeekBucket = CurrentWeekBucket(now);
        var lastResolvedWeekBucket = File.Exists(ElectionStatePath) ? (await File.ReadAllTextAsync(ElectionStatePath, ct)).Trim() : null;
        if (lastResolvedWeekBucket == currentWeekBucket)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var electionMessage = await new KingdomPoliticsService(db, tokenStore).ResolveElectionsAsync(ct);
        logger.LogInformation("Élections de royaume résolues automatiquement (dimanche) : {Message}", electionMessage);

        await File.WriteAllTextAsync(ElectionStatePath, currentWeekBucket, ct);
    }
}
