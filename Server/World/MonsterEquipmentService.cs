using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Équipement des créatures (voir GDD/demande utilisateur — "les items équipés peuvent donner
/// des avantages à nos monstres, exemple si mon monstre a une épée en fer il fera plus de
/// dégâts"). Un objet équipé est retiré de l'inventaire (exclusif à cette créature) tant qu'il
/// reste équipé, et rendu à l'inventaire au déséquipement ou en cas de remplacement — distinct de
/// <see cref="MonsterCareService.GiveItemAsync"/> qui, lui, consomme définitivement l'objet
/// contre de l'XP. Les bonus (<c>ItemEntity.StatBonus</c>) sont appliqués aux stats de combat par
/// <see cref="CombatService.BuildTeamCombatantsAsync"/>, pas ici.
/// </summary>
public sealed class MonsterEquipmentService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<MonsterInstanceData> EquipAsync(Guid monsterId, EquipItemRequest request, CancellationToken ct = default)
    {
        var (monster, character) = await ResolveOwnedMonsterAsync(monsterId, request.SessionToken, ct);

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId, ct)
            ?? throw new AccountOperationException("Objet introuvable.");

        var slot = item.ItemType switch
        {
            ItemType.Arme => EquipmentSlot.Weapon,
            ItemType.Armure => EquipmentSlot.Armor,
            ItemType.Accessoire => EquipmentSlot.Accessory,
            _ => throw new AccountOperationException("Cet objet ne peut pas être équipé (seuls les armes/armures/accessoires le peuvent)."),
        };

        var inventoryItem = await db.InventoryItems.FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct)
            ?? throw new AccountOperationException("Vous ne possédez pas cet objet.");

        // Rend l'objet précédemment équipé à cet emplacement à l'inventaire avant de le remplacer
        // (voir doc de classe — jamais perdu, juste échangé).
        await ReturnEquippedItemToInventoryAsync(monster, character.Id, slot, ct);

        inventoryItem.Quantity--;
        if (inventoryItem.Quantity <= 0)
        {
            db.InventoryItems.Remove(inventoryItem);
        }

        switch (slot)
        {
            case EquipmentSlot.Weapon: monster.EquippedWeaponItemId = item.Id; break;
            case EquipmentSlot.Armor: monster.EquippedArmorItemId = item.Id; break;
            case EquipmentSlot.Accessory: monster.EquippedAccessoryItemId = item.Id; break;
        }

        await db.SaveChangesAsync(ct);
        return await ToMonsterInstanceDataAsync(monster, ct);
    }

    public async Task<MonsterInstanceData> UnequipAsync(Guid monsterId, UnequipItemRequest request, CancellationToken ct = default)
    {
        var (monster, character) = await ResolveOwnedMonsterAsync(monsterId, request.SessionToken, ct);

        var hadItem = await ReturnEquippedItemToInventoryAsync(monster, character.Id, request.Slot, ct);
        if (!hadItem)
        {
            throw new AccountOperationException("Rien n'est équipé à cet emplacement.");
        }

        await db.SaveChangesAsync(ct);
        return await ToMonsterInstanceDataAsync(monster, ct);
    }

    /// <summary>Rend à l'inventaire l'objet actuellement équipé à <paramref name="slot"/> (le cas échéant) et vide l'emplacement. Retourne faux si l'emplacement était déjà vide.</summary>
    private async Task<bool> ReturnEquippedItemToInventoryAsync(MonsterEntity monster, Guid characterId, EquipmentSlot slot, CancellationToken ct)
    {
        var previousItemId = slot switch
        {
            EquipmentSlot.Weapon => monster.EquippedWeaponItemId,
            EquipmentSlot.Armor => monster.EquippedArmorItemId,
            EquipmentSlot.Accessory => monster.EquippedAccessoryItemId,
            _ => null,
        };

        if (previousItemId is not { } itemId)
        {
            return false;
        }

        // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire".
        var maxStackSize = await db.Items.Where(i => i.Id == itemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);
        await InventoryStackingService.AddQuantityAsync(db, characterId, itemId, 1, maxStackSize <= 0 ? 99 : maxStackSize, ct);

        switch (slot)
        {
            case EquipmentSlot.Weapon: monster.EquippedWeaponItemId = null; break;
            case EquipmentSlot.Armor: monster.EquippedArmorItemId = null; break;
            case EquipmentSlot.Accessory: monster.EquippedAccessoryItemId = null; break;
        }

        return true;
    }

    private async Task<(MonsterEntity Monster, CharacterEntity Character)> ResolveOwnedMonsterAsync(Guid monsterId, string sessionToken, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");

        return (monster, character);
    }

    private async Task<MonsterInstanceData> ToMonsterInstanceDataAsync(MonsterEntity entity, CancellationToken ct)
    {
        var equippedIds = new[] { entity.EquippedWeaponItemId, entity.EquippedArmorItemId, entity.EquippedAccessoryItemId }
            .Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        var itemNames = equippedIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Items.Where(i => equippedIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);

        return new MonsterInstanceData
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
            EquippedWeaponName = entity.EquippedWeaponItemId is { } w ? itemNames.GetValueOrDefault(w) : null,
            EquippedArmorItemId = entity.EquippedArmorItemId,
            EquippedArmorName = entity.EquippedArmorItemId is { } a ? itemNames.GetValueOrDefault(a) : null,
            EquippedAccessoryItemId = entity.EquippedAccessoryItemId,
            EquippedAccessoryName = entity.EquippedAccessoryItemId is { } acc ? itemNames.GetValueOrDefault(acc) : null,
            CapturedAtUtc = entity.CapturedAtUtc,
            PrestigeLevel = entity.PrestigeLevel,
        };
    }
}
