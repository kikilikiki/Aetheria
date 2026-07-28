namespace Aetheria.Server.World.Combat;

/// <summary>Un joueur en file d'attente pour une arène classée (voir <c>ArenaQueueService</c>).</summary>
public sealed class ArenaTicket
{
    public required Guid UserId { get; init; }
    public required Guid CharacterId { get; init; }
    public required IReadOnlyList<Guid> MonsterIds { get; init; }
}
