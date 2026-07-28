namespace Aetheria.Shared.Models.Combat;

/// <summary>Corps JSON de <c>POST /api/loot/{lootId}/claim</c>.</summary>
public sealed class LootClaimRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemIndex { get; init; }
}
