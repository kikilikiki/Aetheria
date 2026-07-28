namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON de <c>POST /api/shop/buy</c>.</summary>
public sealed class ShopPurchaseResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public long RemainingGold { get; init; }
}
