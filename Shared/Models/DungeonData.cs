namespace Aetheria.Shared.Models;

/// <summary>
/// Description d'un donjon (une région entière). La génération procédurale des étages se
/// fait à partir de <see cref="Seed"/> (voir Server/World et <c>Docs/GameDesign.md</c> —
/// section Donjons : mini-boss tous les 10 étages, boss tous les 50, boss légendaire tous les 100).
/// </summary>
public sealed class DungeonData
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int KingdomId { get; init; }
    public string Description { get; init; } = string.Empty;
    public int Seed { get; init; }

    /// <summary>
    /// Position actuelle sur la carte du monde (voir GDD — les donjons apparaissent
    /// aléatoirement et changent de place toutes les heures, voir DungeonWorldService).
    /// </summary>
    public int WorldX { get; init; }
    public int WorldY { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "niveau min pour rentrer" : 1 = accessible à tout le monde.</summary>
    public int MinLevel { get; init; } = 1;

    /// <summary>Voir GDD/demande utilisateur — "donjons hardcore (niv 15+)".</summary>
    public bool IsHardcore { get; init; }
}
