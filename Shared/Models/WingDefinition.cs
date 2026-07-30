namespace Aetheria.Shared.Models;

/// <summary>Définition d'ailes cosmétiques collectionnables (voir GDD/demande utilisateur — "Collections : ailes").</summary>
public sealed record WingDefinition(string Key, string Name, string UnlockedByAchievementKey);

/// <summary>Catalogue d'ailes, débloquées automatiquement avec un succès existant (voir <see cref="MountCatalog"/> pour le même principe).</summary>
public static class WingCatalog
{
    public static IReadOnlyList<WingDefinition> All { get; } =
    [
        new("ailes_de_novice", "Ailes de Novice", "bienvenue"),
        new("ailes_artisan", "Ailes d'Artisan", "premier_craft"),
        new("ailes_de_guilde", "Ailes de Guilde", "fondateur_de_guilde"),
        new("ailes_de_flammes", "Ailes de Flammes", "terrasseur_de_boss_mondial"),
        new("ailes_legendaires", "Ailes Légendaires", "prestige_legendaire"),
    ];

    public static WingDefinition? FindByAchievement(string achievementKey) => All.FirstOrDefault(w => w.UnlockedByAchievementKey == achievementKey);
    public static WingDefinition? Find(string key) => All.FirstOrDefault(w => w.Key == key);
}
