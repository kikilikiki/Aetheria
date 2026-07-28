using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Combat;

/// <summary>Corps JSON de <c>POST /api/pvp/arena/queue</c>.</summary>
public sealed class QueueForArenaRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }
    public required ArenaFormat Format { get; init; }
}
