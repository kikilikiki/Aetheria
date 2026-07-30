using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Définition d'une monture collectionnable (voir GDD/demande utilisateur — "Collections : montures").</summary>
public sealed record MountDefinition(string Key, string Name, MountKind Kind, string UnlockedByAchievementKey);

/// <summary>
/// Catalogue de montures, débloquées automatiquement en même temps qu'un succès existant (voir
/// <c>Server/World/AchievementService.UnlockAsync</c>) plutôt qu'un second système de conditions à
/// maintenir en parallèle. Comme <see cref="PassiveTalentCatalog"/>, contenu statique pour cette
/// première version.
/// </summary>
public static class MountCatalog
{
    public static IReadOnlyList<MountDefinition> All { get; } =
    [
        new("poney_des_plaines", "Poney des Plaines", MountKind.Terrestre, "bienvenue"),
        new("loup_sauvage", "Loup Sauvage", MountKind.Terrestre, "premiere_capture"),
        new("sanglier_blinde", "Sanglier Blindé", MountKind.Terrestre, "fondateur_de_guilde"),
        new("griffon_dore", "Griffon Doré", MountKind.Volant, "terrasseur_de_boss_mondial"),
        new("phenix_aile", "Phénix Ailé", MountKind.Volant, "prestige_legendaire"),
        new("hippocampe_bleu", "Hippocampe Bleu", MountKind.Aquatique, "eleveur"),
        new("leviathan_miniature", "Léviathan Miniature", MountKind.Aquatique, "maitre_fusionneur"),
    ];

    public static MountDefinition? FindByAchievement(string achievementKey) => All.FirstOrDefault(m => m.UnlockedByAchievementKey == achievementKey);
    public static MountDefinition? Find(string key) => All.FirstOrDefault(m => m.Key == key);
}
