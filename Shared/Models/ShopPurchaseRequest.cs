namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/shop/buy</c>.</summary>
public sealed class ShopPurchaseRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
}
