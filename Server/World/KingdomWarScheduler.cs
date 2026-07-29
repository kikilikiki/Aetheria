using Aetheria.Database.Context;
using Aetheria.Server.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute un système de guerre de territoire et le gagnant gagne
/// du territoire" : <see cref="KingdomWarService.ResolveWeeklyWarAsync"/> existait déjà mais
/// n'était déclenché par personne (il fallait appeler l'endpoint à la main). Résout
/// automatiquement une fois par semaine ISO (année+numéro de semaine), même mécanique de fichier
/// témoin qu'un <see cref="DigestScheduler"/> pour éviter une double résolution au redémarrage.
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
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var currentWeekBucket = $"{now.Year}-W{calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday):00}";
        var lastResolvedWeekBucket = File.Exists(StatePath) ? (await File.ReadAllTextAsync(StatePath, ct)).Trim() : null;

        if (lastResolvedWeekBucket == currentWeekBucket)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var message = await new KingdomWarService(db).ResolveWeeklyWarAsync(ct);
        logger.LogInformation("Guerre de royaumes résolue automatiquement : {Message}", message);

        await File.WriteAllTextAsync(StatePath, currentWeekBucket, ct);
    }
}
