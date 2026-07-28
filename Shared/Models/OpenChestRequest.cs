namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/dungeons/{dungeonId}/floors/{floorNumber}/rooms/{roomIndex}/loot-chest</c> — voir GDD, exploration en couloir linéaire ("loot au fil du chemin").</summary>
public sealed class OpenChestRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
}
