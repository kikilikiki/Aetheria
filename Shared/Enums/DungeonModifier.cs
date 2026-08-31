namespace Aetheria.Shared.Enums;

/// <summary>
/// Voir demande utilisateur — "quand on rentre ajoute le choix entre hardcore, normal ou spécial
/// saison". Modificateur choisi à l'entrée d'un donjon (voir <c>StartDungeonCombatRequest</c>,
/// <c>CombatService.StartFromDungeonAsync</c>). Remplace l'ancien booléen <c>HardcoreRequested</c>.
/// </summary>
public enum DungeonModifier
{
    /// <summary>3 vies, statistiques normales (voir <c>DungeonLivesEntity.MaxLives</c>).</summary>
    Normal,

    /// <summary>Voir GDD — "donjon hardcore" : 1 seule vie, monstres +50 %.</summary>
    Hardcore,

    /// <summary>
    /// Voir demande utilisateur — "spécial saison avec les détails de chaque modification" :
    /// effet tournant selon le numéro de la saison active (voir
    /// <see cref="Aetheria.Shared.Models.SeasonalDungeonModifierCatalog"/>).
    /// </summary>
    Saison,
}
