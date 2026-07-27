namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON de <c>POST /api/monsters/capture</c>.</summary>
public sealed class CaptureAttemptResponse
{
    public required bool Success { get; init; }
    public Guid? CapturedMonsterId { get; init; }
    public required string Message { get; init; }
}
