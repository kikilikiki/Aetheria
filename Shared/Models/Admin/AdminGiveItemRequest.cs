namespace Aetheria.Shared.Models.Admin;

/// <summary>Corps JSON de <c>POST /api/admin/game/give-item</c> — voir GDD/demande utilisateur, "ils peuvent donner des item".</summary>
public sealed class AdminGiveItemRequest
{
    public required string SessionToken { get; init; }
    public required string TargetCharacterName { get; init; }
    public required int ItemId { get; init; }
    public int Quantity { get; init; } = 1;
}
