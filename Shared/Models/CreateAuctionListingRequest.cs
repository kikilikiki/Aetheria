namespace Aetheria.Shared.Models;

public sealed class CreateAuctionListingRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemId { get; init; }
    public required int Quantity { get; init; }
    public required long PricePerUnit { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "la possibilité de le mettre aux enchères".</summary>
    public bool IsAuction { get; init; }

    /// <summary>Durée de l'enchère, sans effet si <see cref="IsAuction"/> est faux.</summary>
    public int AuctionDurationHours { get; init; } = 24;
}
