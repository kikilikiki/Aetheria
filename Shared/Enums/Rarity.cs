namespace Aetheria.Shared.Enums;

/// <summary>Rareté d'une créature ou d'un objet (bestiaire, loot de donjon, hôtel des ventes).</summary>
public enum Rarity
{
    Commun,
    PeuCommun,
    Rare,
    Epique,
    Legendaire,
    Mythique,
    Ancestral,
    Divin,

    /// <summary>Voir GDD/demande utilisateur — "OBJETS ADMIN (IMPOSSIBLES À OBTENIR)" : jamais choisie par le tirage aléatoire de rencontre sauvage/donjon (voir CombatService.RarityForLevel et ResolveDungeonEncounterSpeciesAsync, qui ne la référencent jamais), seulement distribuable via le panel admin.</summary>
    Admin,
}
