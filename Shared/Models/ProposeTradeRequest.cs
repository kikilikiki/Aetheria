namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/trade/propose</c> — voir GDD/demande utilisateur "Système d'échange (trade) entre joueurs".</summary>
public sealed class ProposeTradeRequest
{
    public required string SessionToken { get; init; }
    public required Guid InitiatorCharacterId { get; init; }
    public required string TargetCharacterName { get; init; }
    public Guid? OfferedMonsterId { get; init; }
    public long OfferedGold { get; init; }
    public long RequestedGold { get; init; }
}
