using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Hôtel des ventes entre joueurs (voir GDD/demande utilisateur — "les joueurs mettent en vente
/// et achètent, moins cher que chez la marchande") : contrairement à <see cref="ShopService"/>
/// (catalogue fixe, stock infini contre de l'or), ici un joueur dépose réellement ses objets, un
/// autre les achète — l'objet quitte l'inventaire du vendeur dès la mise en vente (évite la
/// double-vente si le vendeur se déconnecte) et l'or n'est crédité qu'à la vente effective.
/// </summary>
public sealed class AuctionService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<IReadOnlyList<AuctionListingSummary>> GetActiveListingsAsync(Guid? viewerCharacterId, CancellationToken ct = default)
    {
        await ResolveExpiredAuctionsAsync(ct);

        var listings = await db.AuctionListings
            .Include(l => l.Item)
            .Include(l => l.SellerCharacter)
            .OrderBy(l => l.PricePerUnit)
            .ToListAsync(ct);

        return listings.Select(l => new AuctionListingSummary
        {
            ListingId = l.Id,
            ItemId = l.ItemId,
            ItemName = l.Item?.Name ?? "Objet inconnu",
            Quantity = l.Quantity,
            PricePerUnit = l.PricePerUnit,
            SellerName = l.SellerCharacter?.Name ?? "?",
            IsMine = viewerCharacterId is not null && l.SellerCharacterId == viewerCharacterId,
            IsAuction = l.IsAuction,
            CurrentBid = l.CurrentBid,
            CurrentBidderName = l.CurrentBidderName,
            IsMyBid = viewerCharacterId is not null && l.CurrentBidderCharacterId == viewerCharacterId,
            AuctionEndsAtUtc = l.AuctionEndsAtUtc,
        }).ToList();
    }

    public async Task<AuctionResponse> CreateListingAsync(CreateAuctionListingRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        if (request.Quantity <= 0 || request.PricePerUnit <= 0)
        {
            throw new AccountOperationException("Quantité et prix doivent être positifs.");
        }

        var entry = await db.InventoryItems.Include(i => i.Item)
            .FirstOrDefaultAsync(i => i.CharacterId == character.Id && i.ItemId == request.ItemId, ct);

        if (entry is null || entry.Quantity < request.Quantity)
        {
            throw new AccountOperationException("Vous n'avez pas assez de cet objet.");
        }

        entry.Quantity -= request.Quantity;
        if (entry.Quantity <= 0)
        {
            db.InventoryItems.Remove(entry);
        }

        db.AuctionListings.Add(new AuctionListingEntity
        {
            Id = Guid.NewGuid(),
            SellerCharacterId = character.Id,
            ItemId = request.ItemId,
            Quantity = request.Quantity,
            PricePerUnit = request.PricePerUnit,
            IsAuction = request.IsAuction,
            CurrentBid = request.IsAuction ? request.PricePerUnit * request.Quantity : 0,
            AuctionEndsAtUtc = request.IsAuction ? DateTime.UtcNow.AddHours(Math.Clamp(request.AuctionDurationHours, 1, 168)) : null,
        });

        await db.SaveChangesAsync(ct);

        return new AuctionResponse { Success = true, Message = request.IsAuction ? "Objet mis aux enchères." : "Objet mis en vente.", RemainingGold = character.Gold };
    }

    /// <summary>Voir GDD/demande utilisateur — "la possibilité de le mettre aux enchères" : enchère strictement supérieure à la précédente, aucun or prélevé tant que l'enchère n'est pas gagnée (voir <see cref="ResolveExpiredAuctionsAsync"/>).</summary>
    public async Task<AuctionResponse> PlaceBidAsync(AuctionBidRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var listing = await db.AuctionListings.FirstOrDefaultAsync(l => l.Id == request.ListingId, ct)
            ?? throw new AccountOperationException("Cette enchère n'existe plus.");

        if (!listing.IsAuction)
        {
            throw new AccountOperationException("Cette annonce n'est pas une enchère.");
        }

        if (listing.SellerCharacterId == character.Id)
        {
            throw new AccountOperationException("Vous ne pouvez pas enchérir sur votre propre annonce.");
        }

        if (listing.AuctionEndsAtUtc is { } endsAt && endsAt <= DateTime.UtcNow)
        {
            throw new AccountOperationException("Cette enchère est terminée.");
        }

        if (request.BidAmount <= listing.CurrentBid)
        {
            throw new AccountOperationException($"L'enchère doit dépasser {listing.CurrentBid} or.");
        }

        if (character.Gold < request.BidAmount)
        {
            throw new AccountOperationException("Pas assez d'or pour cette enchère.");
        }

        listing.CurrentBid = request.BidAmount;
        listing.CurrentBidderCharacterId = character.Id;
        listing.CurrentBidderName = character.Name;
        await db.SaveChangesAsync(ct);

        return new AuctionResponse { Success = true, Message = "Enchère placée.", RemainingGold = character.Gold };
    }

    /// <summary>
    /// Voir GDD/demande utilisateur — résolution paresseuse (appelée à chaque consultation de la
    /// liste, voir <see cref="GetActiveListingsAsync"/>) plutôt qu'un scheduler dédié : l'objet
    /// revient au vendeur si personne n'a enchéri, ou si le meilleur enchérisseur n'a plus assez
    /// d'or au moment de la résolution (simplification assumée — pas d'or mis en réserve à
    /// l'enchère, voir Docs/README.md).
    /// </summary>
    private async Task ResolveExpiredAuctionsAsync(CancellationToken ct)
    {
        var expired = await db.AuctionListings.Where(l => l.IsAuction && l.AuctionEndsAtUtc <= DateTime.UtcNow).ToListAsync(ct);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var listing in expired)
        {
            var winner = listing.CurrentBidderCharacterId is { } winnerId
                ? await db.Characters.FirstOrDefaultAsync(c => c.Id == winnerId, ct)
                : null;

            if (winner is not null && winner.Gold >= listing.CurrentBid)
            {
                winner.Gold -= listing.CurrentBid;

                var seller = await db.Characters.FirstOrDefaultAsync(c => c.Id == listing.SellerCharacterId, ct);
                if (seller is not null)
                {
                    seller.Gold += await KingdomPoliticsService.ApplyTaxAsync(db, seller, listing.CurrentBid, ct);
                }

                var maxStack = await db.Items.Where(i => i.Id == listing.ItemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);
                await InventoryStackingService.AddQuantityAsync(db, winner.Id, listing.ItemId, listing.Quantity, maxStack <= 0 ? 99 : maxStack, ct);
            }
            else
            {
                // Personne n'a enchéri (ou le meilleur enchérisseur n'a plus les moyens) : l'objet revient au vendeur.
                var maxStack = await db.Items.Where(i => i.Id == listing.ItemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);
                await InventoryStackingService.AddQuantityAsync(db, listing.SellerCharacterId, listing.ItemId, listing.Quantity, maxStack <= 0 ? 99 : maxStack, ct);
            }

            db.AuctionListings.Remove(listing);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<AuctionResponse> BuyAsync(AuctionActionRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var listing = await db.AuctionListings.FirstOrDefaultAsync(l => l.Id == request.ListingId, ct)
            ?? throw new AccountOperationException("Cette annonce n'existe plus.");

        if (listing.IsAuction)
        {
            throw new AccountOperationException("Cette annonce est une enchère : utilisez PlaceBidAsync.");
        }

        if (listing.SellerCharacterId == character.Id)
        {
            throw new AccountOperationException("Vous ne pouvez pas acheter votre propre annonce.");
        }

        var totalPrice = listing.PricePerUnit * listing.Quantity;
        if (character.Gold < totalPrice)
        {
            return new AuctionResponse { Success = false, Message = "Pas assez d'or.", RemainingGold = character.Gold };
        }

        var seller = await db.Characters.FirstOrDefaultAsync(c => c.Id == listing.SellerCharacterId, ct);

        character.Gold -= totalPrice;
        if (seller is not null)
        {
            // Voir GDD/demande utilisateur — "taxes" : prélevées sur l'or gagné à la vente, au profit du trésor du royaume du vendeur (exemption au palier premium 3).
            seller.Gold += await KingdomPoliticsService.ApplyTaxAsync(db, seller, totalPrice, ct);
        }

        // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire".
        var boughtItemMaxStack = await db.Items.Where(i => i.Id == listing.ItemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);
        await InventoryStackingService.AddQuantityAsync(db, character.Id, listing.ItemId, listing.Quantity, boughtItemMaxStack <= 0 ? 99 : boughtItemMaxStack, ct);

        db.AuctionListings.Remove(listing);
        await db.SaveChangesAsync(ct);

        return new AuctionResponse { Success = true, Message = "Achat réussi !", RemainingGold = character.Gold };
    }

    public async Task<AuctionResponse> CancelAsync(AuctionActionRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnedCharacterAsync(request.SessionToken, request.CharacterId, ct);

        var listing = await db.AuctionListings.FirstOrDefaultAsync(l => l.Id == request.ListingId, ct)
            ?? throw new AccountOperationException("Cette annonce n'existe plus.");

        if (listing.SellerCharacterId != character.Id)
        {
            throw new AccountOperationException("Ce n'est pas votre annonce.");
        }

        if (listing.IsAuction && listing.CurrentBidderCharacterId is not null)
        {
            throw new AccountOperationException("Impossible d'annuler une enchère qui a déjà reçu une offre.");
        }

        // Voir GDD/demande utilisateur — "limite de stack d'item à 99 par item dans l'inventaire".
        var returnedItemMaxStack = await db.Items.Where(i => i.Id == listing.ItemId).Select(i => i.MaxStackSize).FirstOrDefaultAsync(ct);
        await InventoryStackingService.AddQuantityAsync(db, character.Id, listing.ItemId, listing.Quantity, returnedItemMaxStack <= 0 ? 99 : returnedItemMaxStack, ct);

        db.AuctionListings.Remove(listing);
        await db.SaveChangesAsync(ct);

        return new AuctionResponse { Success = true, Message = "Annonce annulée, objet rendu.", RemainingGold = character.Gold };
    }

    private async Task<CharacterEntity> ResolveOwnedCharacterAsync(string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct);
        return character ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");
    }
}
