namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — vendre un objet à la marchande (moins qu'à l'Hôtel des ventes).</summary>
public sealed class ShopSellRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
}
