using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models.Admin;

/// <summary>Voir GDD/demande utilisateur — "la possibilité de se téléporter a la personne qui a report et a la personne qui a été report".</summary>
public sealed class PlayerLocationSummary
{
    public required string CharacterName { get; init; }
    public required KingdomType Kingdom { get; init; }
    public required int PositionX { get; init; }
    public required int PositionY { get; init; }
    public required bool IsOnline { get; init; }
}
