using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Prestige d'une créature (voir GDD/demande utilisateur — "Prestige après niveau maximum") :
/// une fois <see cref="MonsterProgressionService.MaxLevel"/> atteint, remet le niveau à 1 contre
/// un bonus de statistiques permanent de +5% (voir <see cref="MonsterStatMath"/>, cumulable —
/// classique "prestige" de jeu incrémental).
/// </summary>
public sealed class PrestigeService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<MonsterInstanceData> PrestigeAsync(string sessionToken, Guid characterId, Guid monsterId, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId && m.OwnerCharacterId == character.Id, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        if (monster.Level < MonsterProgressionService.MaxLevel)
        {
            throw new AccountOperationException($"Cette créature doit être niveau {MonsterProgressionService.MaxLevel} pour prestiger (actuellement niveau {monster.Level}).");
        }

        monster.Level = 1;
        monster.Experience = 0;
        monster.PrestigeLevel++;

        await db.SaveChangesAsync(ct);
        await new AchievementService(db).UnlockAsync(character.UserId, "prestige_legendaire", ct);

        return ToMonsterInstanceData(monster);
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
