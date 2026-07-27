using Aetheria.Database.Context;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Guerres de royaumes (voir <c>Docs/GameDesign.md</c> — "chaque semaine, les territoires
/// peuvent changer de propriétaire"). Les victoires PvP alimentent les points de guerre du
/// royaume du vainqueur (voir <see cref="CombatService"/>). Simplification assumée pour
/// cette première version : pas de contestation territoire par territoire — la résolution
/// hebdomadaire donne TOUS les territoires au royaume qui a le plus de points de guerre.
/// Dans une vraie exploitation, <see cref="ResolveWeeklyWarAsync"/> serait un job planifié
/// plutôt qu'un appel manuel.
/// </summary>
public sealed class KingdomWarService(AetheriaDbContext db)
{
    public async Task AwardWarPointsAsync(KingdomType kingdomType, long points, CancellationToken ct = default)
    {
        var kingdom = await db.Kingdoms.FirstOrDefaultAsync(k => k.Type == kingdomType, ct);
        if (kingdom is null)
        {
            return;
        }

        kingdom.WarPoints += points;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<KingdomWarStanding>> GetStandingsAsync(CancellationToken ct = default)
    {
        var kingdoms = await db.Kingdoms.OrderByDescending(k => k.WarPoints).ToListAsync(ct);
        return kingdoms.Select(k => new KingdomWarStanding(k.Name, k.WarPoints)).ToList();
    }

    public async Task<string> ResolveWeeklyWarAsync(CancellationToken ct = default)
    {
        var kingdoms = await db.Kingdoms.ToListAsync(ct);
        var leader = kingdoms.Where(k => k.WarPoints > 0).OrderByDescending(k => k.WarPoints).FirstOrDefault();

        if (leader is null)
        {
            return "Aucun royaume n'a marqué de points cette semaine : aucun territoire ne change de main.";
        }

        var territories = await db.Territories.ToListAsync(ct);
        foreach (var territory in territories)
        {
            territory.ControllingKingdomId = leader.Id;
        }

        foreach (var kingdom in kingdoms)
        {
            kingdom.WarPoints = 0;
        }

        await db.SaveChangesAsync(ct);

        return $"{leader.Name} domine la guerre de cette semaine et contrôle désormais tous les territoires.";
    }
}
