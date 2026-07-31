namespace Aetheria.Shared.Enums;

/// <summary>
/// Voir GDD/demande utilisateur — "Talents/capacités passives uniques par monstre (comme les
/// 'natures' Pokémon, influençant les stats)". Tirée une seule fois à la capture/naissance (voir
/// MonsterNatureCatalog.RollRandom), jamais modifiée sauf reroll explicite. Chaque nature (hors
/// Neutre) booste une statistique de +10% et en réduit une autre de -10% — voir MonsterStatMath.
/// </summary>
public enum MonsterNature
{
    Neutre,
    Fonceur,
    Rocailleux,
    Fulgurant,
    Reflechi,
    Endurant,
    Robuste,
}
