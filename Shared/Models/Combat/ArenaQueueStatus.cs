namespace Aetheria.Shared.Models.Combat;

/// <summary>Réponse de <c>GET /api/pvp/arena/status</c> — le combat n'existe qu'une fois <see cref="IsMatched"/> vrai.</summary>
public sealed class ArenaQueueStatus
{
    public required bool IsMatched { get; init; }
    public Guid? CombatId { get; init; }
}
