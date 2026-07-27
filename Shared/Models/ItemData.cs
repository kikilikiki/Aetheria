using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Objet manipulable : équipement, ressource de métier, objet de capture, etc.</summary>
public sealed class ItemData
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public ItemType ItemType { get; init; }
    public Rarity Rarity { get; init; } = Rarity.Commun;

    /// <summary>Bonus de statistiques apporté si équipé (armes/armures uniquement).</summary>
    public StatBlock StatBonus { get; init; } = StatBlock.Zero;

    public bool IsStackable { get; init; } = true;
    public int MaxStackSize { get; init; } = 99;
}
