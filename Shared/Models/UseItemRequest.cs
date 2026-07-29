namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/inventory/use</c> — voir GDD/demande utilisateur "ajoute des consommables pour booster la luck l'xp la money" (voir ConsumableService).</summary>
public sealed class UseItemRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int ItemId { get; init; }
}
