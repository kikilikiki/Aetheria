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

        // Voir GDD/demande utilisateur — "Succès cachés" : nom/description masqués côté Client
        // tant qu'ils ne sont pas débloqués (voir IsHidden).
        new("terrasseur_de_boss_mondial", "Terrasseur", "Portez le coup fatal à un boss mondial.", AchievementCategory.Combat, IsHidden: true),
        new("maitre_fusionneur", "Fusion réussie", "Fusionnez deux créatures au bâtiment Fusion.", AchievementCategory.Collection, IsHidden: true),
        new("eleveur", "Éleveur", "Faites naître une créature à la Couvée.", AchievementCategory.Collection, IsHidden: true),
        new("prestige_legendaire", "Au-delà des limites", "Faites prestiger une créature au niveau maximum.", AchievementCategory.Collection, IsHidden: true),

        // Voir GDD/demande utilisateur — "contenu end-game".
        new("conquerant_du_sanctuaire", "Conquérant du Sanctuaire", "Triomphez du donjon mythique Sanctuaire Ultime.", AchievementCategory.Combat, IsHidden: true),
    ];

    public static AchievementDefinition? Find(string key) => All.FirstOrDefault(a => a.Key == key);
}
