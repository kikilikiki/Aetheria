using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>
/// Corps JSON de <c>POST /api/professions/gather</c> — récolte d'une ressource brute
/// (voir <c>Docs/GameDesign.md</c> — chaîne Mineur → Minerai → Forgeron → ...).
/// </summary>
public sealed class GatherRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required ProfessionType Profession { get; init; }
    public required int ResourceItemId { get; init; }
    public int Quantity { get; init; } = 1;
}
