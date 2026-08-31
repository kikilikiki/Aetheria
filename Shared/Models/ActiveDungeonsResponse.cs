namespace Aetheria.Shared.Models;

/// <summary>
/// Réponse de <c>GET /api/dungeons/active</c> — voir demande utilisateur : "toujours un donjon de
/// niveau 1 et un donjon d'un niveau aléatoire", rotation toutes les heures, plus un 3ᵉ portail
/// éventuel invoqué par un admin. Sert à la fois à placer les portails sur la carte du monde et à
/// remplir le panneau de choix de modificateur (Hardcore / Spécial saison).
/// </summary>
public sealed class ActiveDungeonsResponse
{
    public IReadOnlyList<ActiveDungeonPortal> Portals { get; init; } = [];

    /// <summary>Détail affiché pour le mode Hardcore dans le panneau de choix.</summary>
    public DungeonModifierInfo Hardcore { get; init; } = new();

    /// <summary>Détail (nom + effets) du modificateur de la saison active (voir <see cref="SeasonalDungeonModifierCatalog"/>).</summary>
    public DungeonModifierInfo Seasonal { get; init; } = new();
}

public sealed class ActiveDungeonPortal
{
    public required DungeonData Dungeon { get; init; }

    /// <summary>1 = donjon niveau 1, 2 = donjon aléatoire de l'heure, 3 = portail invoqué par un admin.</summary>
    public int Slot { get; init; }

    public bool IsAdminSpawned { get; init; }
}

public sealed class DungeonModifierInfo
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
