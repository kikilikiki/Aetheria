namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/trade/{offerId}/respond</c> — accepter/refuser/annuler une offre d'échange.</summary>
public sealed class RespondTradeRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required bool Accept { get; init; }
}
