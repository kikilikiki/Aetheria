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
    Element Element = Element.Neutre,
    /// <summary>Voir GDD/demande utilisateur — "cooldown pour le spécial" : 0 si utilisable ce tour-ci.</summary>
    int SpecialAbilityCooldownRemaining = 0,
    /// <summary>Voir GDD/demande utilisateur — variantes de créature (voir MonsterVariantCatalog, affichage badge côté Client).</summary>
    MonsterVariant Variant = MonsterVariant.Normal,
    /// <summary>Voir GDD/demande utilisateur — "Compétences passives" (voir PassiveTalentCatalog), vide pour un combattant sans passif.</summary>
    string PassiveTalent = "",
    /// <summary>Voir GDD/demande utilisateur — "un bouton pour la capacité ultime... affiché seulement quand c'est le tour d'un monstre au niveau max".</summary>
    int Level = 1,
    /// <summary>Voir GDD/demande utilisateur — "l'attaque spéciale ... en plus de l'attaque ultime si le monstre est lvl max" : cooldown séparé de <see cref="SpecialAbilityCooldownRemaining"/>, 0 si utilisable ce tour-ci.</summary>
    int UltimateAbilityCooldownRemaining = 0);
