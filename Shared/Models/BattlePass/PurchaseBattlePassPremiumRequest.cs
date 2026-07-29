namespace Aetheria.Shared.Models.BattlePass;

/// <summary>Débloque le pass premium du Passe de Niveau contre des gemmes (voir BattlePassService.PurchasePremiumAsync).</summary>
public sealed class PurchaseBattlePassPremiumRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
