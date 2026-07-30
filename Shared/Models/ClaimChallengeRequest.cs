namespace Aetheria.Shared.Models;

/// <summary>Corps JSON de <c>POST /api/challenges/claim</c> — voir GDD/demande utilisateur "Défis hebdomadaires".</summary>
public sealed class ClaimChallengeRequest
{
    public required string SessionToken { get; init; }
    public required Guid CharacterId { get; init; }
    public required string ChallengeKey { get; init; }
}
