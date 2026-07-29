namespace Aetheria.Shared.Models;

public sealed class AuctionResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public long RemainingGold { get; init; }
}
