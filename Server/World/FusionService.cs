using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Bâtiment Fusion (voir GDD/demande utilisateur — "un batiment pour fusionner des monstres, leur
/// niveau sera leur 2 niveaux additionnés puis divisé par 2"). Une des deux créatures survit (avec
/// le niveau fusionné), l'autre est consommée — pas de nouvelle espèce ni de choix de variante :
/// une simple redistribution de niveau entre deux créatures déjà possédées.
///
/// Voir retour utilisateur — "ajoute un temps et une validation avant de le faire ... plus le
/// monstre que l'on obtient ... plus ça prendra de temps" : en deux temps désormais
/// (<see cref="StartAsync"/> détermine le résultat et le délai, <see cref="ClaimAsync"/>
/// l'applique réellement une fois le délai écoulé) plutôt qu'instantané — un seul slot en attente
/// à la fois par personnage (voir <c>CharacterEntity.PendingFusionSurvivorId</c>).
/// </summary>
public sealed class FusionService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    /// <summary>2 secondes par niveau du résultat, entre 15s et 5 minutes.</summary>
    private static TimeSpan DurationFor(int resultingLevel) => TimeSpan.FromSeconds(Math.Clamp(resultingLevel * 2, 15, 300));

    public async Task<PendingFusionStatus> StartAsync(string sessionToken, Guid characterId, Guid survivorMonsterId, Guid consumedMonsterId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        if (survivorMonsterId == consumedMonsterId)
        {
            throw new AccountOperationException("Choisissez deux créatures différentes.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        if (character.PendingFusionCompletesAtUtc is not null)
        {
            throw new AccountOperationException("Une fusion est déjà en cours.");
        }

        var survivor = await db.Monsters.FirstOrDefaultAsync(m => m.Id == survivorMonsterId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");
        var consumed = await db.Monsters.FirstOrDefaultAsync(m => m.Id == consumedMonsterId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var resultingLevel = Math.Max(1, (survivor.Level + consumed.Level) / 2);
        var completesAt = DateTime.UtcNow + DurationFor(resultingLevel);

        character.PendingFusionSurvivorId = survivor.Id;
        character.PendingFusionConsumedId = consumed.Id;
        character.PendingFusionCompletesAtUtc = completesAt;
        await db.SaveChangesAsync(ct);

        return new PendingFusionStatus
        {
            SurvivorMonsterId = survivor.Id,
            ConsumedMonsterId = consumed.Id,
            SurvivorName = survivor.Nickname,
            ConsumedName = consumed.Nickname,
            ResultingLevel = resultingLevel,
            CompletesAtUtc = completesAt,
            IsReady = false,
        };
    }

    public async Task<PendingFusionStatus?> GetStatusAsync(string sessionToken, Guid characterId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        if (character.PendingFusionCompletesAtUtc is not { } completesAt || character.PendingFusionSurvivorId is not { } survivorId || character.PendingFusionConsumedId is not { } consumedId)
        {
            return null;
        }

        var survivor = await db.Monsters.FirstOrDefaultAsync(m => m.Id == survivorId, ct);
        var consumed = await db.Monsters.FirstOrDefaultAsync(m => m.Id == consumedId, ct);
        if (survivor is null || consumed is null)
        {
            // Voir GDD/demande utilisateur — cas limite : une des deux créatures a disparu entre
            // temps (donnée/échangée par un autre système) - on efface l'attente plutôt que de
            // laisser un état bloqué que ClaimAsync ne pourrait jamais résoudre.
            character.PendingFusionSurvivorId = null;
            character.PendingFusionConsumedId = null;
            character.PendingFusionCompletesAtUtc = null;
            await db.SaveChangesAsync(ct);
            return null;
        }

        return new PendingFusionStatus
        {
            SurvivorMonsterId = survivorId,
            ConsumedMonsterId = consumedId,
            SurvivorName = survivor.Nickname,
            ConsumedName = consumed.Nickname,
            ResultingLevel = Math.Max(1, (survivor.Level + consumed.Level) / 2),
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

        if (character.PendingFusionCompletesAtUtc is not { } completesAt || character.PendingFusionSurvivorId is not { } survivorId || character.PendingFusionConsumedId is not { } consumedId)
        {
            throw new AccountOperationException("Aucune fusion en cours.");
        }

        if (DateTime.UtcNow < completesAt)
        {
            throw new AccountOperationException("La fusion n'est pas encore terminée.");
        }

        var survivor = await db.Monsters.FirstOrDefaultAsync(m => m.Id == survivorId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");
        var consumed = await db.Monsters.FirstOrDefaultAsync(m => m.Id == consumedId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        survivor.Level = Math.Max(1, (survivor.Level + consumed.Level) / 2);
        survivor.Experience = 0;

        db.Monsters.Remove(consumed);
        character.PendingFusionSurvivorId = null;
        character.PendingFusionConsumedId = null;
        character.PendingFusionCompletesAtUtc = null;
        await db.SaveChangesAsync(ct);
        await new AchievementService(db).UnlockAsync(character.UserId, "maitre_fusionneur", ct);

        return ToMonsterInstanceData(survivor);
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
