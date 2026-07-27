namespace Aetheria.Shared.Models;

/// <summary>Guilde de joueurs pouvant posséder une ville et lancer des guerres.</summary>
public sealed class GuildData
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid LeaderCharacterId { get; init; }
    public int Level { get; set; } = 1;
    public long TreasuryGold { get; set; }
}
