using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Article proposé à l'achat dans la boutique en jeu (voir GDD — bouton Boutique).</summary>
public sealed class ShopItem
{
    public required int ItemId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public ItemType ItemType { get; init; }
    public Rarity Rarity { get; init; }
    public required int Price { get; init; }
}
