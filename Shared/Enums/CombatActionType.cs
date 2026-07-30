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

    /// <summary>
    /// Voir GDD/demande utilisateur — "il doit y avoir l'attaque spéciale ... en plus de
    /// l'attaque ultime si le monstre est lvl max" : action distincte de <see cref="SpecialAbility"/>
    /// (pas un simple remplacement/relabel) — disponible uniquement au niveau max (voir
    /// <c>CombatEngine.ResolveUltimateAbility</c>), avec son propre cooldown, en plus de la
    /// capacité spéciale normale qui reste utilisable comme avant.
    /// </summary>
    UltimateAbility,
}
