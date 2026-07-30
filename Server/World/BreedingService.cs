using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Bâtiment Couvée (voir GDD/demande utilisateur — "un batiment pour faire de la reproduction avec
/// heritage de statistiques, et des monstres que l'on peut avoir que en reproduction") : les deux
/// parents survivent (contrairement à <see cref="FusionService"/>), un nouveau bébé (niveau 1)
/// naît d'une espèce exclusivement obtenable ainsi (voir <c>MonsterSpeciesEntity.BreedingOnly</c>).
/// "Héritage de statistiques" est porté par la <see cref="Aetheria.Shared.Enums.MonsterVariant"/> —
/// le seul système de variance de statistiques individuelle du jeu (voir MonsterVariantCatalog) :
/// le bébé hérite de la MEILLEURE variante des deux parents plutôt que d'en tirer une nouvelle au
/// hasard.
/// </summary>
public sealed class BreedingService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static readonly Random Random = new();

    public async Task<MonsterInstanceData> BreedAsync(string sessionToken, Guid characterId, Guid parentMonsterId1, Guid parentMonsterId2, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        if (parentMonsterId1 == parentMonsterId2)
        {
            throw new AccountOperationException("Choisissez deux créatures différentes.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var parent1 = await db.Monsters.FirstOrDefaultAsync(m => m.Id == parentMonsterId1 && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");
        var parent2 = await db.Monsters.FirstOrDefaultAsync(m => m.Id == parentMonsterId2 && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var breedingSpecies = await db.MonsterSpecies.Where(s => s.BreedingOnly).ToListAsync(ct);
        if (breedingSpecies.Count == 0)
        {
            throw new AccountOperationException("Aucune espèce de reproduction disponible pour le moment.");
        }

        var offspringSpecies = breedingSpecies[Random.Next(breedingSpecies.Count)];

        var parent1Multiplier = MonsterVariantCatalog.Get(parent1.Variant).StatMultiplier;
        var parent2Multiplier = MonsterVariantCatalog.Get(parent2.Variant).StatMultiplier;
        var inheritedVariant = parent1Multiplier >= parent2Multiplier ? parent1.Variant : parent2.Variant;

        var offspring = new MonsterEntity
        {
            Id = Guid.NewGuid(),
            OwnerCharacterId = character.Id,
            SpeciesId = offspringSpecies.Id,
            Variant = inheritedVariant,
            Nickname = offspringSpecies.Name,
            Level = 1,
            // Voir GDD/demande utilisateur — "Compétences passives" : tirée au hasard comme pour une capture.
            PassiveTalent = PassiveTalentCatalog.RollRandom(Random),
        };

        db.Monsters.Add(offspring);
        await db.SaveChangesAsync(ct);
        await new AchievementService(db).UnlockAsync(character.UserId, "eleveur", ct);

        return ToMonsterInstanceData(offspring);
    }

    private static MonsterInstanceData ToMonsterInstanceData(MonsterEntity entity) => new()
    {
        Id = entity.Id,
        SpeciesId = entity.SpeciesId,
        OwnerCharacterId = entity.OwnerCharacterId,
        Variant = entity.Variant,
        Nickname = entity.Nickname,
        Level = entity.Level,
        Experience = entity.Experience,
        Personality = entity.Personality,
        PassiveTalent = entity.PassiveTalent,
        IsInActiveTeam = entity.IsInActiveTeam,
        EquippedWeaponItemId = entity.EquippedWeaponItemId,
        EquippedArmorItemId = entity.EquippedArmorItemId,
        EquippedAccessoryItemId = entity.EquippedAccessoryItemId,
        CapturedAtUtc = entity.CapturedAtUtc,
        PrestigeLevel = entity.PrestigeLevel,
    };
}
