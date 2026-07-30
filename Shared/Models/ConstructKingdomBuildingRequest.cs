namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/kingdoms/construct</c> — voir GDD/demande utilisateur "construction de bâtiments" (réservé au roi élu, voir GDD "élections du roi").</summary>
public sealed class ConstructKingdomBuildingRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
