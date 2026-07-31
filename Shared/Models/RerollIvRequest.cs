namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/reroll-iv</c> — voir GDD/demande utilisateur "ajoute un item pour changer les iv".</summary>
public sealed class RerollIvRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }
    public required int ItemId { get; init; }
}
