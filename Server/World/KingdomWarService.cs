using Aetheria.Database.Context;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Guerres de royaumes (voir <c>Docs/GameDesign.md</c> — "chaque semaine [le samedi], les
/// royaumes s'affrontent pour le contrôle des territoires"). Les victoires en combat de guerre
/// (voir <see cref="KingdomWarQueueService"/>, <c>CombatService.ApplyArenaResultAsync</c>)
/// alimentent les points de guerre du royaume du vainqueur. La résolution hebdomadaire classe les
/// 4 royaumes par points et distribue une récompense à paliers (voir GDD/demande utilisateur —
/// "le premier gagne 2 bâtiments, le second 1, le troisième rien, le quatrième en perd 1") : voir
/// <see cref="KingdomEntity.BonusTerritoryCount"/> pour pourquoi ceci prend la forme d'un bonus de
/// rendement plutôt que de bâtiments apparaissant à des coordonnées aléatoires sur la carte.
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
        return kingdoms.Select(k => new KingdomWarStanding(k.Name, k.WarPoints, k.BonusTerritoryCount)).ToList();
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — classement final de la semaine par points de guerre : le
    /// 1er gagne +2 au bonus de rendement, le 2e +1, le 3e rien, le 4e -1 (jamais sous zéro). En
    /// cas d'égalité de points, l'ordre entre royaumes ex-æquo n'est pas garanti stable (voir
    /// Docs/README.md) — simplification assumée pour cette première version.
    /// </summary>
    public async Task<string> ResolveWeeklyWarAsync(CancellationToken ct = default)
    {
        var kingdoms = await db.Kingdoms.OrderByDescending(k => k.WarPoints).ToListAsync(ct);
        if (kingdoms.Count == 0)
        {
            return "Aucun royaume enregistré.";
        }

        var summary = new List<string>();
        for (var rank = 0; rank < kingdoms.Count; rank++)
        {
            var kingdom = kingdoms[rank];
            var delta = rank switch
            {
                0 => 2,
                1 => 1,
                var r when r == kingdoms.Count - 1 => -1,
                _ => 0,
            };

            kingdom.BonusTerritoryCount = Math.Max(0, kingdom.BonusTerritoryCount + delta);
            summary.Add($"{kingdom.Name} ({kingdom.WarPoints} pts) : {(delta >= 0 ? "+" : "")}{delta} bonus de territoire");
            kingdom.WarPoints = 0;
        }

        // Voir GDD/demande utilisateur — "capture de territoires" : le premier prend réellement
        // un territoire (mine/champ en priorité, pour que sa team ait un nouvel endroit où
        // "aller faire des quêtes de minage") au dernier, plutôt qu'un simple bonus de rendement.
        // Ignoré s'il n'y a qu'un seul royaume classé (rien à capturer) ou si le dernier n'a plus
        // aucun territoire.
        var captureSummary = await CaptureTerritoryAsync(kingdoms[0], kingdoms[^1], ct);
        if (captureSummary is not null)
        {
            summary.Add(captureSummary);
        }

        await db.SaveChangesAsync(ct);

        return $"Guerre de royaumes résolue — {string.Join(", ", summary)}.";
    }

    private async Task<string?> CaptureTerritoryAsync(Database.Entities.KingdomEntity winner, Database.Entities.KingdomEntity loser, CancellationToken ct)
    {
        if (winner.Id == loser.Id)
        {
            return null;
        }

        var loserTerritories = await db.Territories.Where(t => t.ControllingKingdomId == loser.Id).ToListAsync(ct);
        if (loserTerritories.Count == 0)
        {
            return null;
        }

        // Priorité aux territoires de ressources (Mine/Champ) — c'est là que "la team qui gagne"
        // profite concrètement de la capture (voir GDD/demande utilisateur).
        var target = loserTerritories.FirstOrDefault(t => t.TerritoryType is TerritoryType.Mine or TerritoryType.Champ)
            ?? loserTerritories[Random.Shared.Next(loserTerritories.Count)];

        target.ControllingKingdomId = winner.Id;
        return $"{winner.Name} capture \"{target.Name}\" sur {loser.Name}";
    }
}
