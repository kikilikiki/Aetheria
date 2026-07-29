namespace Aetheria.Shared.Enums;

/// <summary>
/// Variante d'apparition d'une créature (voir <c>Docs/GameDesign.md</c> — section Monstres).
/// Une même espèce peut apparaître sous plusieurs variantes, avec des statistiques et un
/// taux de capture différents.
/// </summary>
public enum MonsterVariant
{
    Normal,
    Shiny,
    Alpha,
    Corrompu,
    Ancestral,

    // Voir demande utilisateur — "ajoute [12 variantes]" (voir MonsterVariantCatalog pour les
    // statistiques/taux d'apparition/taux de capture de chacune).
    Cristallin,
    Spectral,
    Dore,
    Maudit,
    Eternel,
    Divin,
    Infernal,
    Celeste,
    Mutant,
    Chromatique,
    Primordial,
    Titan,
}
