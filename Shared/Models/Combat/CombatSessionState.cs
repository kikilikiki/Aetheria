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
    Guid? LootId = null,
    /// <summary>
    /// Vrai pour un combat déclenché dans un donjon (voir GDD/demande utilisateur — "impossible
    /// de fuir un combat en donjon") : source de vérité pour l'affichage du bouton Fuir côté
    /// client, plutôt que l'état de scène local du client (qui peut rester périmé après être
    /// retourné à l'extérieur — voir <c>Client/Program.cs</c>, <c>interiorIsDungeon</c>).
    /// </summary>
    bool IsDungeonCombat = false);
