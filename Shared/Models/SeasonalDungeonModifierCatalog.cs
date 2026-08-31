using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Voir demande utilisateur — "spécial saison : modificateur tournant selon la saison". Le
/// numéro de la saison active (voir <c>SeasonEntity.Number</c>) choisit un effet parmi cette
/// liste (cycle sur <c>number % Count</c>). Purement déterministe et partagé Client/Serveur : le
/// Client affiche <see cref="Name"/>/<see cref="Description"/> dans le panneau de choix à
/// l'entrée du donjon, le Serveur applique les effets dans
/// <c>CombatService.StartFromDungeonAsync</c>.
/// </summary>
public sealed record SeasonalDungeonModifier(
    string Name,
    string Description,
    double StatMultiplier,
    double XpMultiplier,
    MonsterVariant? ForcedVariant);

public static class SeasonalDungeonModifierCatalog
{
    /// <summary>Les effets possibles, dans l'ordre du cycle. Un par saison, en boucle.</summary>
    public static IReadOnlyList<SeasonalDungeonModifier> All { get; } =
    [
        new("Tempête",
            "Monstres +30 % de statistiques, XP ×2.5. 3 vies.",
            StatMultiplier: 1.30, XpMultiplier: 2.5, ForcedVariant: null),
        new("Disette",
            "Monstres +50 % de statistiques, XP ×3. 3 vies.",
            StatMultiplier: 1.50, XpMultiplier: 3.0, ForcedVariant: null),
        new("Éclat cristallin",
            "Tous les monstres sont de variante Cristallin (statistiques majorées), XP ×2. 3 vies.",
            StatMultiplier: 1.0, XpMultiplier: 2.0, ForcedVariant: MonsterVariant.Cristallin),
        new("Léthargie",
            "Monstres +15 % de statistiques, XP ×2. 3 vies.",
            StatMultiplier: 1.15, XpMultiplier: 2.0, ForcedVariant: null),
    ];

    public static SeasonalDungeonModifier ForSeason(int seasonNumber)
    {
        var index = ((seasonNumber % All.Count) + All.Count) % All.Count;
        return All[index];
    }
}
