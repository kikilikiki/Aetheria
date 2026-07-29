using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Titres obtenables via le classement PvP (voir GDD/demande utilisateur — "des titres que l'on
/// peut obtenir en pvp dans des classements"). Paliers sur l'ELO (<c>PvpStatistics.BestRank</c>,
/// le record personnel plutôt que le rang courant — voir CombatService) : un titre débloqué reste
/// acquis même si l'ELO redescend ensuite, un peu comme les rangs saisonniers d'un jeu compétitif.
/// </summary>
public static class TitleCatalog
{
    private static readonly (int Threshold, string Key)[] Tiers =
    [
        (1050, "Guerrier confirmé"),
        (1150, "Vétéran de l'arène"),
        (1250, "Champion"),
        (1400, "Légende de l'arène"),
    ];

    /// <summary>Débloque tous les paliers désormais atteints par <paramref name="bestRank"/> qui ne l'étaient pas déjà (voir CombatService.ApplyPvpResultAsync/ApplyArenaResultAsync, appelé après chaque combat PvP/Arène classé).</summary>
    public static async Task AwardForBestRankAsync(AetheriaDbContext db, Guid characterId, int bestRank, CancellationToken ct = default)
    {
        var unlockedKeys = Tiers.Where(t => bestRank >= t.Threshold).Select(t => t.Key).ToList();
        if (unlockedKeys.Count == 0)
        {
            return;
        }

        var alreadyOwned = await db.CharacterTitles
            .Where(t => t.CharacterId == characterId && unlockedKeys.Contains(t.TitleKey))
            .Select(t => t.TitleKey)
            .ToListAsync(ct);

        foreach (var key in unlockedKeys.Except(alreadyOwned))
        {
            db.CharacterTitles.Add(new CharacterTitleEntity { Id = Guid.NewGuid(), CharacterId = characterId, TitleKey = key });
        }
    }
}
