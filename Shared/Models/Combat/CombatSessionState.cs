namespace Aetheria.Shared.Models.Combat;

/// <summary>Photographie complète d'un combat en cours, renvoyée après chaque action.</summary>
public sealed record CombatSessionState(
    Guid CombatId,
    int GridWidth,
    int GridHeight,
    IReadOnlyList<CombatantState> Combatants,
    Guid? CurrentTurnCombatantId,
    bool IsFinished,
    int? WinningTeam,
    string? LastMessage);
