namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{monsterId}/equip</c> — voir GDD/demande utilisateur "les objets équipés donnent des avantages à nos monstres".</summary>
public sealed class EquipItemRequest
{
    public required string SessionToken { get; init; }
    public required int ItemId { get; init; }
}
