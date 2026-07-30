using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Bâtiment Reproduction (voir GDD/demande utilisateur — "un batiment pour faire de la
/// reproduction avec heritage de statistiques, et des monstres que l'on peut avoir que en
/// reproduction") : les deux parents survivent (contrairement à <see cref="FusionService"/>), un
/// nouveau bébé (niveau 1) naît d'une espèce exclusivement obtenable ainsi (voir
/// <c>MonsterSpeciesEntity.BreedingOnly</c>). "Héritage de statistiques" est porté par la
/// <see cref="MonsterVariant"/> — le seul système de variance de statistiques individuelle du jeu
/// (voir MonsterVariantCatalog) : le bébé hérite de la MEILLEURE variante des deux parents plutôt
/// que d'en tirer une nouvelle au hasard.
///
/// Voir retour utilisateur — "la couveuse doit ajouter un temps et une validation avant de le
/// faire ... plus le monstre que l'on obtient ... plus ça prendra de temps" + "ajoute un cooldown
/// si une personne a reproduit" : en deux temps désormais (<see cref="StartAsync"/> tire le bébé
/// et détermine le délai en fonction de sa rareté, <see cref="ClaimAsync"/> le fait naître
/// réellement une fois le délai écoulé) plutôt qu'instantané, avec un court cooldown après
/// récupération avant de pouvoir relancer une reproduction.
/// </summary>
public sealed class BreedingService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private static readonly Random Random = new();

    /// <summary>Court délai après récupération avant de pouvoir relancer une reproduction (en plus du fait qu'un seul slot en attente existe déjà à la fois).</summary>
    private static readonly TimeSpan PostClaimCooldown = TimeSpan.FromSeconds(60);

    /// <summary>30s par palier de rareté de l'espèce du bébé, entre 30s et 10 minutes.</summary>
    private static TimeSpan DurationFor(Rarity offspringRarity) => TimeSpan.FromSeconds(Math.Clamp(((int)offspringRarity + 1) * 30, 30, 600));

    public async Task<PendingBreedStatus> StartAsync(string sessionToken, Guid characterId, Guid parentMonsterId1, Guid parentMonsterId2, CancellationToken ct = default)
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

        if (character.PendingBreedCompletesAtUtc is not null)
        {
            throw new AccountOperationException("Une reproduction est déjà en cours.");
        }

        if (character.NextBreedAllowedAtUtc is { } nextAllowed && DateTime.UtcNow < nextAllowed)
        {
            var remaining = nextAllowed - DateTime.UtcNow;
            throw new AccountOperationException($"Il faut encore attendre {Math.Ceiling(remaining.TotalSeconds)}s avant de reproduire à nouveau.");
        }

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

        var completesAt = DateTime.UtcNow + DurationFor(offspringSpecies.BaseRarity);

        character.PendingBreedParentId1 = parent1.Id;
        character.PendingBreedParentId2 = parent2.Id;
        character.PendingBreedOffspringSpeciesId = offspringSpecies.Id;
        character.PendingBreedOffspringVariant = inheritedVariant;
        character.PendingBreedOffspringPassiveTalent = PassiveTalentCatalog.RollRandom(Random);
        character.PendingBreedCompletesAtUtc = completesAt;
        await db.SaveChangesAsync(ct);

        return new PendingBreedStatus
        {
            ParentMonsterId1 = parent1.Id,
            ParentMonsterId2 = parent2.Id,
            OffspringSpeciesName = offspringSpecies.Name,
            CompletesAtUtc = completesAt,
            IsReady = false,
        };
    }

    public async Task<PendingBreedStatus?> GetStatusAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        if (character.PendingBreedCompletesAtUtc is not { } completesAt
            || character.PendingBreedParentId1 is not { } parentId1
            || character.PendingBreedParentId2 is not { } parentId2
            || character.PendingBreedOffspringSpeciesId is not { } offspringSpeciesId)
        {
            return null;
        }

        var offspringSpecies = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Id == offspringSpeciesId, ct);
        if (offspringSpecies is null)
        {
            character.PendingBreedParentId1 = null;
            character.PendingBreedParentId2 = null;
            character.PendingBreedOffspringSpeciesId = null;
            character.PendingBreedOffspringVariant = null;
            character.PendingBreedOffspringPassiveTalent = null;
            character.PendingBreedCompletesAtUtc = null;
            await db.SaveChangesAsync(ct);
            return null;
        }

        return new PendingBreedStatus
        {
            ParentMonsterId1 = parentId1,
            ParentMonsterId2 = parentId2,
            OffspringSpeciesName = offspringSpecies.Name,
            CompletesAtUtc = completesAt,
            IsReady = DateTime.UtcNow >= completesAt,
        };
    }

    public async Task<MonsterInstanceData> ClaimAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        if (character.PendingBreedCompletesAtUtc is not { } completesAt
            || character.PendingBreedOffspringSpeciesId is not { } offspringSpeciesId
            || character.PendingBreedOffspringVariant is not { } offspringVariant)
        {
            throw new AccountOperationException("Aucune reproduction en cours.");
        }

        if (DateTime.UtcNow < completesAt)
        {
            throw new AccountOperationException("La reproduction n'est pas encore terminée.");
        }

        var offspringSpecies = await db.MonsterSpecies.FirstAsync(s => s.Id == offspringSpeciesId, ct);
        var offspring = new MonsterEntity
        {
            Id = Guid.NewGuid(),
            OwnerCharacterId = character.Id,
            SpeciesId = offspringSpeciesId,
            Variant = offspringVariant,
            Nickname = offspringSpecies.Name,
            Level = 1,
            PassiveTalent = character.PendingBreedOffspringPassiveTalent ?? PassiveTalentCatalog.RollRandom(Random),
        };

        db.Monsters.Add(offspring);
        character.PendingBreedParentId1 = null;
        character.PendingBreedParentId2 = null;
        character.PendingBreedOffspringSpeciesId = null;
        character.PendingBreedOffspringVariant = null;
        character.PendingBreedOffspringPassiveTalent = null;
        character.PendingBreedCompletesAtUtc = null;
        character.NextBreedAllowedAtUtc = DateTime.UtcNow + PostClaimCooldown;
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
