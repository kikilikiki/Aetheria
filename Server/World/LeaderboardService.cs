using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Classements (voir <c>Docs/GameDesign.md</c> — section Classements). <see cref="RefreshAsync"/>
/// recalcule les scores d'une catégorie ; dans une vraie exploitation ce serait un job planifié
/// plutôt qu'un appel manuel, mais l'API le permet en attendant. Seules les catégories
/// directement calculables avec les données existantes sont supportées pour l'instant
/// (les autres dépendent de systèmes qui n'existent pas encore : PvP, temps de jeu, ...).
/// </summary>
public sealed class LeaderboardService(AetheriaDbContext db)
{
    private static readonly LeaderboardCategory[] SupportedCategories =
    [
        LeaderboardCategory.Richesse,
        LeaderboardCategory.Metiers,
        LeaderboardCategory.MonstresCaptures,
        LeaderboardCategory.Pvp,
        LeaderboardCategory.Donjons,
    ];

    public async Task RefreshAsync(LeaderboardCategory category, CancellationToken ct = default)
    {
        if (!SupportedCategories.Contains(category))
        {
            throw new AccountOperationException($"Le classement '{category}' n'est pas encore calculable.");
        }

        var characterIds = await db.Characters.Select(c => c.Id).ToListAsync(ct);
        var scores = new Dictionary<Guid, long>();

        switch (category)
        {
            case LeaderboardCategory.Richesse:
                foreach (var character in await db.Characters.ToListAsync(ct))
                {
                    scores[character.Id] = character.Gold;
                }

                break;

            case LeaderboardCategory.Metiers:
                var professions = await db.CharacterProfessions.ToListAsync(ct);
                foreach (var characterId in characterIds)
                {
                    scores[characterId] = professions.Where(p => p.CharacterId == characterId).Sum(p => (long)p.Level);
                }

                break;

            case LeaderboardCategory.MonstresCaptures:
                var monsters = await db.Monsters.ToListAsync(ct);
                foreach (var characterId in characterIds)
                {
                    scores[characterId] = monsters.Count(m => m.OwnerCharacterId == characterId);
                }

                break;

            // Voir GDD/demande utilisateur — "des titres que l'on peut obtenir en pvp dans des
            // classements" : le classement PvP était référencé (enum, commentaire "dépend de
            // systèmes qui n'existent pas encore") mais jamais implémenté alors que PvpStatistics
            // existe bel et bien (voir CombatService.ApplyPvpResultAsync/ApplyArenaResultAsync).
            case LeaderboardCategory.Pvp:
                var pvpStats = await db.Statistics.ToListAsync(ct);
                foreach (var stats in pvpStats)
                {
                    scores[stats.CharacterId] = stats.Pvp.BestRank;
                }

                break;

            // Voir GDD/demande utilisateur — "ajoute un leaderboard pour la personne qui est
            // arrivée le plus haut [en donjon]" (voir CombatService.StartFromDungeonAsync, qui
            // renseigne DeepestFloorReached).
            case LeaderboardCategory.Donjons:
                var explorationStats = await db.Statistics.ToListAsync(ct);
                foreach (var stats in explorationStats)
                {
                    scores[stats.CharacterId] = stats.Exploration.DeepestFloorReached;
                }

                break;
        }

        foreach (var (characterId, score) in scores)
        {
            var existing = await db.Leaderboard
                .FirstOrDefaultAsync(l => l.CharacterId == characterId && l.Category == category, ct);

            if (existing is null)
            {
                db.Leaderboard.Add(new LeaderboardEntity
                {
                    Id = Guid.NewGuid(),
                    CharacterId = characterId,
                    Category = category,
                    Score = score,
                    UpdatedAtUtc = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Score = score;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LeaderboardRow>> GetTopAsync(LeaderboardCategory category, int limit, CancellationToken ct = default)
    {
        var rows = await db.Leaderboard
            .Where(l => l.Category == category)
            .OrderByDescending(l => l.Score)
            .Take(limit)
            .ToListAsync(ct);

        var characterNames = await db.Characters
            .Where(c => rows.Select(r => r.CharacterId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return rows
            .Select(r => new LeaderboardRow(characterNames.GetValueOrDefault(r.CharacterId, "?"), r.Score))
            .ToList();
    }
}
