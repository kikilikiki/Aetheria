namespace Aetheria.Shared.Enums;

/// <summary>
/// Classe de personnage jouable. Ensemble de départ, pensé pour être étendu (le combat
/// tactique repose surtout sur les créatures, la classe du joueur influence son propre
/// rôle de support/dégâts sur la grille).
/// </summary>
public enum CharacterClass
{
    Guerrier,
    Mage,
    Archer,
    Soigneur,
}
