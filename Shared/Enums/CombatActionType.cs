namespace Aetheria.Shared.Enums;

/// <summary>Action jouable pendant son tour de combat (voir <c>Docs/GameDesign.md</c> — section Combats).</summary>
public enum CombatActionType
{
    Move,
    Attack,

    /// <summary>Tente une capture (voir GDD — Capture des Créatures) : termine l'affrontement.</summary>
    Capture,
    Pass,
}
