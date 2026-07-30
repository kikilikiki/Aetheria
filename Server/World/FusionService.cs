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
/// </summary>
public sealed class FusionService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<MonsterInstanceData> FuseAsync(string sessionToken, Guid characterId, Guid survivorMonsterId, Guid consumedMonsterId, CancellationToken ct = default)
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

        var survivor = await db.Monsters.FirstOrDefaultAsync(m => m.Id == survivorMonsterId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");
        var consumed = await db.Monsters.FirstOrDefaultAsync(m => m.Id == consumedMonsterId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        survivor.Level = Math.Max(1, (survivor.Level + consumed.Level) / 2);
        survivor.Experience = 0;

        db.Monsters.Remove(consumed);
        await db.SaveChangesAsync(ct);

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
    };
}
