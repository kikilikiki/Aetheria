using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Royaume : capitale, territoires contrôlés, statistiques de guerre hebdomadaire.</summary>
public sealed class KingdomData
{
    public required int Id { get; init; }
    public required KingdomType Type { get; init; }
    public required string Name { get; init; }
    public string CapitalName { get; init; } = string.Empty;

    /// <summary>Identifiants des territoires (mines, villages, forts, donjons) contrôlés cette semaine.</summary>
    public IReadOnlyList<int> ControlledTerritoryIds { get; set; } = Array.Empty<int>();

    /// <summary>Voir GDD/demande utilisateur — "ajoute un UI pour les kingdom".</summary>
    public long WarPoints { get; init; }
    public int BonusTerritoryCount { get; init; }
    public int MemberCount { get; init; }
}
