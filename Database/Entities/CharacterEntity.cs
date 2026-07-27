using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>Personnage jouable (table <c>Characters</c>), appartenant à un <see cref="UserEntity"/>.</summary>
public sealed class CharacterEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    public required string Name { get; set; }
    public CharacterClass Class { get; set; }
    public KingdomType Kingdom { get; set; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public long Gold { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MonsterEntity> Monsters { get; set; } = new();
    public List<InventoryItemEntity> InventoryItems { get; set; } = new();
    public StatisticsEntity? Statistics { get; set; }
}
