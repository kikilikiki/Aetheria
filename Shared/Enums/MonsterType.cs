namespace Aetheria.Shared.Enums;

/// <summary>
/// Rôle de combat d'un monstre (voir GDD/demande utilisateur — "ajoute des type (soigneur,
/// guerrier, archer etc) aux monstres"), distinct de l'<see cref="Element"/> : détermine sa
/// capacité spéciale (voir <c>CombatEngine.ResolveSpecialAbility</c>) et sa couleur en combat
/// (voir GDD — "couleur des personnages en fonction de leur type"). Volontairement un petit
/// ensemble fixe pour cette première version plutôt qu'un système de rôles extensible.
/// </summary>
public enum MonsterType
{
    Guerrier,
    Archer,
    Soigneur,

    // Voir GDD/demande utilisateur — bestiaire étendu ("roles"). Simplification assumée (voir
    // Docs/README.md) : ces nouveaux rôles n'ont pas encore de capacité spéciale dédiée dans
    // CombatEngine.ResolveSpecialAbility — comme tout type autre qu'Archer/Soigneur, ils utilisent
    // la capacité générique "coup puissant" (branche par défaut déjà existante).
    Tank,
    Mage,
    Assassin,
    Support,
    Invocateur,
    Berserker,
    Controleur,
}
