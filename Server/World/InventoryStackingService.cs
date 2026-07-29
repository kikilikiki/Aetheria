using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Voir GDD/demande utilisateur — "ajoute la limite de stack d'item à 99 par item dans
/// l'inventaire" : <see cref="ItemEntity.MaxStackSize"/> existait déjà dans le schéma (99 par
/// défaut, moins pour certains consommables) mais n'était jamais appliqué — chaque site d'ajout
/// incrémentait <see cref="InventoryItemEntity.Quantity"/> sans aucune limite. Centralisé ici :
/// remplit d'abord les piles existantes qui ont de la place, puis crée autant de nouvelles piles
/// que nécessaire pour le reste — ne perd jamais d'objets même pour une quantité qui dépasse une
/// seule pile.
/// </summary>
public static class InventoryStackingService
{
    public static void AddQuantity(AetheriaDbContext db, Guid characterId, int itemId, int quantityToAdd, int maxStackSize)
    {
        if (quantityToAdd <= 0)
        {
            return;
        }

        var cap = Math.Max(1, maxStackSize);
        var remaining = quantityToAdd;

        var stacks = db.InventoryItems.Where(i => i.CharacterId == characterId && i.ItemId == itemId).ToList();
        foreach (var stack in stacks)
        {
            if (remaining <= 0)
            {
                break;
            }

            var space = cap - stack.Quantity;
            if (space <= 0)
            {
                continue;
            }

            var add = Math.Min(space, remaining);
            stack.Quantity += add;
            remaining -= add;
        }

        while (remaining > 0)
        {
            var add = Math.Min(cap, remaining);
            db.InventoryItems.Add(new InventoryItemEntity { Id = Guid.NewGuid(), CharacterId = characterId, ItemId = itemId, Quantity = add });
            remaining -= add;
        }
    }

    public static async Task AddQuantityAsync(AetheriaDbContext db, Guid characterId, int itemId, int quantityToAdd, int maxStackSize, CancellationToken ct = default)
    {
        if (quantityToAdd <= 0)
        {
            return;
        }

        var cap = Math.Max(1, maxStackSize);
        var remaining = quantityToAdd;

        var stacks = await db.InventoryItems.Where(i => i.CharacterId == characterId && i.ItemId == itemId).ToListAsync(ct);
        foreach (var stack in stacks)
        {
            if (remaining <= 0)
            {
                break;
            }

            var space = cap - stack.Quantity;
            if (space <= 0)
            {
                continue;
            }

            var add = Math.Min(space, remaining);
            stack.Quantity += add;
            remaining -= add;
        }

        while (remaining > 0)
        {
            var add = Math.Min(cap, remaining);
            db.InventoryItems.Add(new InventoryItemEntity { Id = Guid.NewGuid(), CharacterId = characterId, ItemId = itemId, Quantity = add });
            remaining -= add;
        }
    }
}
