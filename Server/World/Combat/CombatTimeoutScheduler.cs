using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Server.World;
using Aetheria.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheria.Server.World.Combat;

/// <summary>
/// Fait passer automatiquement le tour d'un joueur qui n'agit pas dans le délai imparti (voir
/// GDD/demande utilisateur — "timer de 10 secondes entre chaque tour"), et résout automatiquement
/// un butin non entièrement réclamé après le même genre de délai (voir GDD/demande utilisateur —
/// "timer de 10 secondes pour le choix des gains"). Tourne en tâche de fond pendant toute la durée
/// de vie du serveur (voir <c>Program.cs</c>), avec une vérification par seconde — largement
/// suffisant pour un délai de l'ordre de la dizaine de secondes.
/// </summary>
public sealed class CombatTimeoutScheduler(
    CombatSessionStore combatStore,
    LootSessionStore lootStore,
    SessionTokenStore tokenStore,
    IDbContextFactory<AetheriaDbContext> dbContextFactory,
    ILogger<CombatTimeoutScheduler> logger)
{
    private static readonly TimeSpan TurnTimeout = TimeSpan.FromSeconds(GameInfo.CombatTurnTimeoutSeconds);
    private static readonly TimeSpan LootTimeout = TimeSpan.FromSeconds(GameInfo.LootChoiceTimeoutSeconds);

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckCombatTimeoutsAsync(ct);
                await CheckLootTimeoutsAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Erreur lors de la vérification des délais de combat/butin.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckCombatTimeoutsAsync(CancellationToken ct)
    {
        foreach (var session in combatStore.All())
        {
            if (session.IsFinished)
            {
                continue;
            }

            await using var db = await dbContextFactory.CreateDbContextAsync(ct);
            var combatService = new CombatService(db, tokenStore, combatStore, lootStore);
            await combatService.AutoPassIfTimedOutAsync(session, TurnTimeout, ct);
        }
    }

    private async Task CheckLootTimeoutsAsync(CancellationToken ct)
    {
        var pending = lootStore.All().Where(s => !s.IsResolved).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var lootService = new LootService(db, lootStore, new PartyService(db, tokenStore));
        await lootService.ResolveTimedOutAsync(pending, LootTimeout, ct);
    }
}
