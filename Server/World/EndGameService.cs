using Aetheria.Database.Context;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "contenu end-game (donjons mythiques avec modificateurs,
/// classements mondial/saisonnier, défis hebdomadaires, boss impossibles, équipement légendaire,
/// reliques uniques) — gated behind owning every monster at max level + every gameplay
/// achievement, leaderboards excluded". Le classement mondial est déjà <c>LeaderboardEntity</c> et
/// le classement saisonnier découle du reset d'ELO (voir <see cref="SeasonService"/>) : aucun des
/// deux n'est donc une condition d'accès, conformément à "leaderboards excluded".
/// </summary>
public sealed class EndGameService(AetheriaDbContext db)
{
    public async Task<EndGameStatus> GetStatusAsync(Guid characterId, CancellationToken ct = default)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct)
            ?? throw new AccountOperationException("Personnage introuvable.");

        // Voir GDD/demande utilisateur — "owning every monster at max level" : les espèces
        // exclusives à la reproduction (voir MonsterSpeciesEntity.BreedingOnly) sont exclues du
        // décompte, sinon la condition serait mathématiquement impossible pour un compte qui n'a
        // jamais eu les deux parents nécessaires.
        var requiredSpeciesIds = await db.MonsterSpecies.Where(s => !s.BreedingOnly).Select(s => s.Id).ToListAsync(ct);

        var maxLevelSpeciesOwned = await db.Monsters
            .Where(m => m.OwnerCharacter!.UserId == character.UserId && m.Level >= MonsterProgressionService.MaxLevel)
            .Select(m => m.SpeciesId)
            .Distinct()
            .ToListAsync(ct);

        var speciesAtMaxLevel = requiredSpeciesIds.Intersect(maxLevelSpeciesOwned).Count();

        // Voir GDD/demande utilisateur — "every gameplay achievement" : exclut le succès de
        // récompense du donjon mythique lui-même (voir CombatService.ApplyPveVictoryRewardsAsync),
        // sinon la condition d'accès serait circulaire (impossible à remplir avant d'y être entré).
        var requiredAchievementKeys = AchievementCatalog.All.Select(a => a.Key).Where(k => k != "conquerant_du_sanctuaire").ToList();
        var unlockedAchievementKeys = await db.Achievements.Where(a => a.UserId == character.UserId).Select(a => a.AchievementKey).ToListAsync(ct);
        var totalAchievements = requiredAchievementKeys.Count;
        var achievementsUnlocked = requiredAchievementKeys.Intersect(unlockedAchievementKeys).Count();

        return new EndGameStatus
        {
            IsEligible = speciesAtMaxLevel >= requiredSpeciesIds.Count && achievementsUnlocked >= totalAchievements,
            SpeciesAtMaxLevel = speciesAtMaxLevel,
            TotalRequiredSpecies = requiredSpeciesIds.Count,
            AchievementsUnlocked = achievementsUnlocked,
            TotalAchievements = totalAchievements,
        };
    }
}
