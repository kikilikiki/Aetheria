namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/parties/leave</c>.</summary>
public sealed class LeavePartyRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
