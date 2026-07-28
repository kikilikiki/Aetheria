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
}
