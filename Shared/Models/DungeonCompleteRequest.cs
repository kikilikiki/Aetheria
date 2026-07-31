namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/dungeons/{id}/complete</c> — voir GDD/demande utilisateur "a la fin des 10 etage termine le dongon".</summary>
public sealed class DungeonCompleteRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
