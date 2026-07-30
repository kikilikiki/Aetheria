namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/auction/bid</c> — voir GDD/demande utilisateur "la possibilité de le mettre aux enchères".</summary>
public sealed class AuctionBidRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required Guid ListingId { get; init; }
    public required long BidAmount { get; init; }
}
