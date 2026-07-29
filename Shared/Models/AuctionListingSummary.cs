namespace Aetheria.Shared.Models;

/// <summary>Une annonce active sur l'Hôtel des ventes, telle que renvoyée au client.</summary>
public sealed class AuctionListingSummary
{
    public required Guid ListingId { get; init; }
    public required int ItemId { get; init; }
    public required string ItemName { get; init; }
    public required int Quantity { get; init; }
    public required long PricePerUnit { get; init; }
    public required string SellerName { get; init; }
    public required bool IsMine { get; init; }
}
