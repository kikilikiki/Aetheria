namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/dungeons/{id}/entry-status</c>.</summary>
public sealed class DungeonEntryStatusRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
