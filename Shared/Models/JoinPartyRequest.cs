namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/parties/{partyId}/join</c>.</summary>
public sealed class JoinPartyRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
