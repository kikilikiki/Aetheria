using Aetheria.Database.Context;
using Aetheria.Server.Discord;
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
public sealed class KingdomWarScheduler(IDbContextFactory<AetheriaDbContext> dbContextFactory, ILogger<KingdomWarScheduler> logger)
{
    private static readonly string StatePath = RepoPath.Resolve(".kingdom-war-last-week");

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ResolveIfDueAsync(ct);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Erreur de fichier lors de la vérification de la guerre de royaumes hebdomadaire.");
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

    private async Task ResolveIfDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (now.DayOfWeek != DayOfWeek.Saturday)
        {
            return;
        }

        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var currentWeekBucket = $"{now.Year}-W{calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):00}";
        var lastResolvedWeekBucket = File.Exists(StatePath) ? (await File.ReadAllTextAsync(StatePath, ct)).Trim() : null;

        if (lastResolvedWeekBucket == currentWeekBucket)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var message = await new KingdomWarService(db).ResolveWeeklyWarAsync(ct);
        logger.LogInformation("Guerre de royaumes résolue automatiquement (samedi) : {Message}", message);

        await File.WriteAllTextAsync(StatePath, currentWeekBucket, ct);
    }
}
