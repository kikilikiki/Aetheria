using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Ligne d'inventaire affichée au joueur (voir GDD — bouton Inventaire en jeu).</summary>
public sealed class InventoryItemSummary
{
    public required int ItemId { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public ItemType ItemType { get; init; }
    public Rarity Rarity { get; init; }
    public required int Quantity { get; init; }
}
