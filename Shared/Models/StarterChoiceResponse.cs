namespace Aetheria.Shared.Models;

/// <summary>Réponse JSON de <c>POST /api/characters/{id}/starter</c>.</summary>
public sealed class StarterChoiceResponse
{
    public required bool Success { get; init; }
    public Guid? MonsterId { get; init; }
    public required string Message { get; init; }
}
