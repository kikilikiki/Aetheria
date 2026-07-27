using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Combat;

/// <summary>Corps JSON de <c>POST /api/combat/{combatId}/action</c>.</summary>
public sealed class CombatActionRequest
{
    public required string SessionToken { get; init; }
    public required CombatActionType ActionType { get; init; }

    /// <summary>Case cible pour <see cref="CombatActionType.Move"/> ou <see cref="CombatActionType.Attack"/>.</summary>
    public int TargetX { get; init; }
    public int TargetY { get; init; }

    /// <summary>Requis pour <see cref="CombatActionType.Capture"/>.</summary>
    public int? CaptureItemId { get; init; }
}
