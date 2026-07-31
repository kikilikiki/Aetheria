using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Saisons (voir <c>Docs/GameDesign.md</c> — section Saisons). Ne gère que le cycle
/// actif/inactif, la numérotation, et depuis peu la récompense de fin de saison (voir GDD/demande
/// utilisateur — "Classements saisonniers avec récompenses cosmétiques (skins, titres) plutôt que
/// power creep") ; le reste du contenu ajouté à chaque saison (monstres, donjons, passe saison)
/// reste un travail de contenu à faire, pas une responsabilité de ce service.
/// </summary>
public sealed class SeasonService(AetheriaDbContext db)
{
    public async Task<SeasonEntity> GetCurrentAsync(CancellationToken ct = default)
        => await db.Seasons.FirstOrDefaultAsync(s => s.IsActive, ct)
            ?? throw new AccountOperationException("Aucune saison active.");

    /// <summary>
    /// Voir GDD/demande utilisateur — "classements... saisonniers" (contenu end-game) : le
    /// classement ELO courant (<c>PvpStatistics.CurrentRank</c>) redevient donc lui-même le
    /// classement de la nouvelle saison après reset, plutôt que de dupliquer un second système de
    /// classement figé — <c>BestRank</c> (déjà utilisé par <see cref="TitleCatalog"/>) reste la
    /// trace "de tous les temps", jamais réinitialisée.
    /// </summary>
    public async Task<SeasonEntity> StartNextSeasonAsync(CancellationToken ct = default)
    {
        var current = await db.Seasons.FirstOrDefaultAsync(s => s.IsActive, ct);
        var nextNumber = 1;

        if (current is not null)
        {
            // Voir GDD/demande utilisateur — récompense cosmétique AVANT le reset, sur le
            // classement tel qu'il était encore à la fin de la saison qui se termine.
            await GrantSeasonRewardsAsync(current.Number, ct);
            current.IsActive = false;
            current.EndedAtUtc = DateTime.UtcNow;
            nextNumber = current.Number + 1;
        }

        var season = new SeasonEntity { Number = nextNumber, IsActive = true };
        db.Seasons.Add(season);

        var allStats = await db.Statistics.ToListAsync(ct);
        foreach (var stats in allStats)
        {
            stats.Pvp.CurrentRank = 1000;
            stats.Pvp.Season = nextNumber;
        }

        await db.SaveChangesAsync(ct);

        return season;
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — "Classements saisonniers avec récompenses cosmétiques
    /// (skins, titres) plutôt que power creep" : un titre par catégorie de classement pour le
    /// premier de cette catégorie, plutôt qu'un bonus de statistiques — même mécanisme
    /// d'attribution que <see cref="TitleCatalog.AwardForBestRankAsync"/>/
    /// <see cref="BattlePassService.GrantTitleAsync"/> (existence check puis ajout à
    /// <c>CharacterTitles</c>), aucun nouveau système requis.
    /// </summary>
    private async Task GrantSeasonRewardsAsync(int seasonNumber, CancellationToken ct)
    {
        var leaderboard = new LeaderboardService(db);
        var categories = new[] { LeaderboardCategory.Richesse, LeaderboardCategory.Metiers, LeaderboardCategory.MonstresCaptures, LeaderboardCategory.Pvp, LeaderboardCategory.Donjons };

        foreach (var category in categories)
        {
            var top = await leaderboard.GetTopAsync(category, 1, ct);
            if (top.Count == 0 || top[0].Score <= 0)
            {
                continue;
            }

            var champion = await db.Characters.FirstOrDefaultAsync(c => c.Name == top[0].CharacterName, ct);
            if (champion is null)
            {
                continue;
            }

            var titleKey = $"Champion {category} - Saison {seasonNumber}";
            var alreadyOwned = await db.CharacterTitles.AnyAsync(t => t.CharacterId == champion.Id && t.TitleKey == titleKey, ct);
            if (!alreadyOwned)
            {
                db.CharacterTitles.Add(new CharacterTitleEntity { Id = Guid.NewGuid(), CharacterId = champion.Id, TitleKey = titleKey });
            }
        }
    }
}
