namespace Aetheria.Database.Entities;

/// <summary>Une pile d'objets dans l'inventaire d'un personnage (table <c>Inventory</c>).</summary>
public sealed class InventoryItemEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public int ItemId { get; set; }
    public ItemEntity? Item { get; set; }

    public int Quantity { get; set; } = 1;
    public DateTime AcquiredAtUtc { get; set; } = DateTime.UtcNow;
}
