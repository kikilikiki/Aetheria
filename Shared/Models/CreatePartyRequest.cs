namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/parties</c>. Le personnage fondateur devient chef de groupe.</summary>
public sealed class CreatePartyRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
