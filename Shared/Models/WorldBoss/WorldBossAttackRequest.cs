namespace Aetheria.Shared.Models.WorldBoss;

/// <summary>Corps JSON de <c>POST /api/worldboss/attack</c>.</summary>
public sealed class WorldBossAttackRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
