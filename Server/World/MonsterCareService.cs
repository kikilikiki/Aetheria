using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Donner un objet à une créature (voir GDD — UI de gestion des montres : "monter de niveau,
/// objet à donner"). **Simplification assumée** : tout objet donné consomme 1 exemplaire de
/// l'inventaire et octroie un montant fixe d'XP à la créature, quel que soit le type d'objet —
/// pas encore d'effets différenciés par objet (statistiques, évolution, ...).
/// </summary>
public sealed class MonsterCareService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    private const int GiveItemExperience = 20;

    public async Task<MonsterInstanceData> GiveItemAsync(GiveItemToMonsterRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == request.MonsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas cet objet.");

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        MonsterProgressionService.GrantExperience(monster, GiveItemExperience);
        await db.SaveChangesAsync(ct);

        return ToMonsterInstanceData(monster);
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — bâtiment "où l'on peut voir tout nos monstres et déplacer
    /// ce que l'on a dans notre team" : bascule l'appartenance à l'équipe active (max 4, voir
    /// GDD — "4 créatures maximum participent au combat"), refusé au-delà pour forcer à en
    /// retirer une d'abord plutôt que de silencieusement en ignorer une au combat.
    /// </summary>
    public async Task<MonsterInstanceData> SetActiveTeamAsync(string sessionToken, Guid monsterId, bool isInActiveTeam, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        if (isInActiveTeam && !monster.IsInActiveTeam)
        {
            var activeCount = await db.Monsters.CountAsync(m => m.OwnerCharacterId == character.Id && m.IsInActiveTeam, ct);
            if (activeCount >= 4)
            {
                throw new AccountOperationException("L'équipe active est déjà complète (4 créatures maximum).");
            }
        }

        monster.IsInActiveTeam = isInActiveTeam;
        await db.SaveChangesAsync(ct);

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
    };
}
