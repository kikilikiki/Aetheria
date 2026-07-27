namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/professions/craft</c>.</summary>
public sealed class CraftRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required int RecipeId { get; init; }
}
