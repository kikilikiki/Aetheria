namespace Aetheria.Shared.Models;

/// <summary>Corps commun à l'achat/l'annulation d'une annonce (voir AuctionService.BuyAsync/CancelAsync).</summary>
public sealed class AuctionActionRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required Guid ListingId { get; init; }
}
