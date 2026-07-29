namespace Aetheria.Shared.Models.Premium;

/// <summary>Voir GDD/demande utilisateur — "transformer 100 millions de coins en 10 gems" : <see cref="GoldAmount"/> doit être un multiple de <c>PremiumService.GoldPerGemBlock</c>.</summary>
public sealed class ExchangeGoldForGemsRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required long GoldAmount { get; init; }
}
