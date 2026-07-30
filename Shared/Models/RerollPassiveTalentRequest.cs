namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/monsters/reroll-passive</c> — voir GDD/demande utilisateur "on peut changer la compétence avec un objet".</summary>
public sealed class RerollPassiveTalentRequest
{
    public required string SessionToken { get; init; }
    public required Guid MonsterId { get; init; }
    public required int ItemId { get; init; }
}
