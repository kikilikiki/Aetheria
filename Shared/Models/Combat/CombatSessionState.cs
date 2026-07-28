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
    string? LastMessage,
    /// <summary>Renseigné uniquement à la victoire en PvE (voir GDD — butin de 4 objets) — voir <c>LootSessionState</c>.</summary>
    Guid? LootId = null);
