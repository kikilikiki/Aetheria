namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON décrivant une guilde et ses membres.</summary>
public sealed class GuildSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required int Level { get; init; }
    public required long TreasuryGold { get; init; }
    public required Guid LeaderCharacterId { get; init; }
    public required IReadOnlyList<string> MemberNames { get; init; }
}
