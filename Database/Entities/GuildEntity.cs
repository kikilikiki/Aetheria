namespace Aetheria.Database.Entities;

/// <summary>Guilde de joueurs (table <c>Guilds</c>).</summary>
public sealed class GuildEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public Guid LeaderCharacterId { get; set; }
    public CharacterEntity? LeaderCharacter { get; set; }

    public int Level { get; set; } = 1;
    public long TreasuryGold { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
