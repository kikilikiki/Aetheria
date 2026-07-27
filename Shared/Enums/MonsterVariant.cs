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
}
