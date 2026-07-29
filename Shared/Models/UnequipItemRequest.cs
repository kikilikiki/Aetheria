using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/{monsterId}/unequip</c>.</summary>
public sealed class UnequipItemRequest
{
    public required string SessionToken { get; init; }
    public required EquipmentSlot Slot { get; init; }
}
