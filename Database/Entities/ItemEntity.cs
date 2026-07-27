using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;

namespace Aetheria.Database.Entities;

/// <summary>
/// Catalogue des objets du jeu (table <c>Items</c>) — contenu géré par les designers via
/// l'AdminPanel/MapEditor plutôt que codé en dur, pour pouvoir ajouter des objets sans recompiler.
/// </summary>
public sealed class ItemEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public ItemType ItemType { get; set; }
    public Rarity Rarity { get; set; } = Rarity.Commun;

    public StatBlock StatBonus { get; set; } = StatBlock.Zero;

    public bool IsStackable { get; set; } = true;
    public int MaxStackSize { get; set; } = 99;
}
