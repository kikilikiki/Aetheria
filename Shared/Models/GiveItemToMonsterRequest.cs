namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{monsterId}/give-item</c> — voir GDD, UI de gestion des créatures.</summary>
public sealed class GiveItemToMonsterRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }
    public required int ItemId { get; init; }
}
