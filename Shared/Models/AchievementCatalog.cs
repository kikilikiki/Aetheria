using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Catalogue de départ des succès (voir <c>Docs/GameDesign.md</c> — section Succès). Contenu
/// statique pour cette première version ; à terme géré par l'AdminPanel comme les autres
/// catalogues (objets, espèces, recettes).
/// </summary>
public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementDefinition> All { get; } =
    [
        new("bienvenue", "Bienvenue à Aetheria", "Créez votre premier personnage.", AchievementCategory.Social),
        new("premiere_capture", "Premier compagnon", "Capturez votre première créature.", AchievementCategory.Capture),
        new("premier_craft", "Artisan débutant", "Fabriquez votre premier objet.", AchievementCategory.Metiers),
        new("fondateur_de_guilde", "Fondateur", "Créez une guilde.", AchievementCategory.Social),
    ];

    public static AchievementDefinition? Find(string key) => All.FirstOrDefault(a => a.Key == key);
}
