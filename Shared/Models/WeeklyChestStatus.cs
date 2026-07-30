namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "Exploration : coffres cachés hebdomadaires par royaume".</summary>
public sealed class WeeklyChestStatus
{
    public required string WeekBucket { get; init; }
    public required bool IsClaimed { get; init; }
    public string? ClaimedByCharacterName { get; init; }
    public long RewardGold { get; init; }
}
