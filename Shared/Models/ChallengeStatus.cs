using Aetheria.Shared.Enums;

namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "Défis hebdomadaires" + UI dédiée.</summary>
public sealed class ChallengeStatus
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ChallengePeriod Period { get; init; }
    public required long Progress { get; init; }
    public required long TargetValue { get; init; }
    public required long RewardGold { get; init; }
    public required bool IsCompleted { get; init; }
    public required bool IsClaimed { get; init; }
}
