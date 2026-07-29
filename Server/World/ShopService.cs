using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Boutique en jeu (voir <c>Docs/GameDesign.md</c>) : un catalogue fixe d'objets vendus contre de
/// l'or (<see cref="CharacterEntity.Gold"/>). Pas de stock limité ni d'économie dynamique dans
/// cette première version — voir Docs/README.md pour les évolutions prévues (hôtel des ventes
/// entre joueurs, déjà partiellement modélisé via ItemEntity, distinct de cette boutique).
/// </summary>
public sealed class ShopService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    /// <summary>
    /// Voir GDD/demande utilisateur — vendre un objet à la marchande rapporte moins que de le
    /// déposer à l'Hôtel des ventes (<see cref="AuctionService"/>), en échange d'une vente
    /// immédiate sans attendre un acheteur.
    /// </summary>
    private const double SellBackRatio = 0.4;

    public async Task<IReadOnlyList<ShopItem>> GetCatalogAsync(CancellationToken ct = default)
    {
        var items = await db.Items.Where(i => i.Price > 0).ToListAsync(ct);
        return items.Select(i => new ShopItem
        {
            ItemId = i.Id,
            Name = i.Name,
            Description = i.Description,
            ItemType = i.ItemType,
            Rarity = i.Rarity,
            Price = i.Price,
        }).ToList();
    }

    public async Task<ShopPurchaseResponse> BuyAsync(ShopPurchaseRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == request.ItemId && i.Price > 0, ct)
            ?? throw new AccountOperationException("Cet objet n'est pas en vente.");

        var quantity = Math.Max(1, request.Quantity);
        var totalPrice = (long)item.Price * quantity;

        if (character.Gold < totalPrice)
        {
            return new ShopPurchaseResponse { Success = false, Message = "Pas assez d'or.", RemainingGold = character.Gold };
        }

        character.Gold -= totalPrice;

        var existingStack = item.IsStackable
            ? await db.InventoryItems.FirstOrDefaultAsync(inv => inv.CharacterId == character.Id && inv.ItemId == item.Id, ct)
            : null;

        if (existingStack is not null)
        {
            existingStack.Quantity += quantity;
        }
        else
        {
            db.InventoryItems.Add(new InventoryItemEntity
            {
                Id = Guid.NewGuid(),
                CharacterId = character.Id,
                ItemId = item.Id,
                Quantity = quantity,
            });
        }

        await db.SaveChangesAsync(ct);

        return new ShopPurchaseResponse
        {
            Success = true,
            Message = $"{item.Name} x{quantity} acheté(s) !",
            RemainingGold = character.Gold,
        };
    }

    public async Task<ShopPurchaseResponse> SellAsync(ShopSellRequest request, CancellationToken ct = default)
    {
        if (!tokenStore.TryValidate(request.SessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == request.CharacterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");

        var quantity = Math.Max(1, request.Quantity);
        var entry = await db.InventoryItems.Include(i => i.Item)
            .FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct);

        if (entry?.Item is null || entry.Quantity < quantity)
        {
            throw new AccountOperationException("Vous n'avez pas assez de cet objet.");
        }

        var totalPrice = (long)(entry.Item.Price * SellBackRatio) * quantity;
        character.Gold += totalPrice;

        entry.Quantity -= quantity;
        if (entry.Quantity <= 0)
        {
            db.InventoryItems.Remove(entry);
        }

        await db.SaveChangesAsync(ct);

        return new ShopPurchaseResponse
        {
            Success = true,
            Message = $"{entry.Item.Name} x{quantity} vendu(s) pour {totalPrice} or.",
            RemainingGold = character.Gold,
        };
    }
}
