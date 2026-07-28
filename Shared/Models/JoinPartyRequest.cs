namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/parties/join</c> — identifie le groupe par son code à 5 chiffres plutôt que son GUID interne (voir GDD/demande utilisateur).</summary>
public sealed class JoinPartyRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string JoinCode { get; init; }
}
