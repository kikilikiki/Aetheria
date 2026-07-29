namespace Aetheria.Shared.Models;

public sealed class CreateAuctionListingRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemId { get; init; }
    public required int Quantity { get; init; }
    public required long PricePerUnit { get; init; }
}
