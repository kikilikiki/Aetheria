namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/kingdoms/vote</c> — voir GDD/demande utilisateur "élections du roi".</summary>
public sealed class VoteForKingRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string CandidateName { get; init; }
}
