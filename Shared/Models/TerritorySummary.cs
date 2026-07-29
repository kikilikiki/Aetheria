using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "guerre de territoire" : territoire (mine, village, fort, donjon) et son royaume contrôleur actuel.</summary>
public sealed class TerritorySummary
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required TerritoryType TerritoryType { get; init; }
    public required int ControllingKingdomId { get; init; }
    public required string ControllingKingdomName { get; init; }
}
