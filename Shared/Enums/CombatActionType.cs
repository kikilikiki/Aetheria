namespace Aetheria.Shared.Enums;

/// <summary>Action jouable pendant son tour de combat (voir <c>Docs/GameDesign.md</c> — section Combats).</summary>
public enum CombatActionType
{
    Move,
    Attack,

    /// <summary>Tente une capture (voir GDD — Capture des Créatures) : termine l'affrontement.</summary>
    Capture,
    Pass,

    /// <summary>Capacité spéciale selon le type du combattant (voir GDD/demande utilisateur — "ajoute des capacités spéciales").</summary>
    SpecialAbility,

    /// <summary>Fuite (voir GDD/demande utilisateur — "un bouton pour fuir les combats, impossible en donjon") : termine le combat sans vainqueur, refusé si <c>CombatSession.IsDungeonCombat</c>.</summary>
    Flee,
}
