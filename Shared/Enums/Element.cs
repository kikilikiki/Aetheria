namespace Aetheria.Shared.Enums;

/// <summary>
/// Élément d'une créature ou d'un sort. Détermine les affinités/faiblesses en combat
/// (voir <c>Docs/GameDesign.md</c> — section Combats).
/// </summary>
public enum Element
{
    Neutre,
    Feu,
    Eau,
    Nature,
    Glace,
    Foudre,
    Terre,
    Air,
    Lumiere,
    Ombre,

    // Voir GDD/demande utilisateur — bestiaire étendu ("types" : Poison, Métal, Spectre, Dragon,
    // Cristal, Arcane). "Vent"/"Ténèbres"/"Lumière" du même document correspondent aux valeurs
    // existantes Air/Ombre/Lumiere ci-dessus (pas de doublon créé).
    Poison,
    Metal,
    Spectre,
    Dragon,
    Cristal,
    Arcane,
}
