using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Combat;

/// <summary>
/// L'état public d'un combattant (personnage, créature ou monstre sauvage) tel que renvoyé
/// au client — pas de données serveur internes (IA, graine aléatoire, ...).
/// </summary>
public sealed record CombatantState(
    Guid Id,
    string Name,
    int Team,
    int PositionX,
    int PositionY,
    int CurrentHealth,
    int MaxHealth,
    bool IsAlive,
    /// <summary>Voir GDD — affichage des cases de déplacement/attaque possibles avant de valider une action.</summary>
    int MovementRange = 0,
    int AttackRange = 0,
    /// <summary>Voir GDD — couleur en combat selon le type (soigneur/guerrier/archer).</summary>
    MonsterType Type = MonsterType.Guerrier,
    /// <summary>Voir GDD — avantages/faiblesses de type.</summary>
    Element Element = Element.Neutre);
