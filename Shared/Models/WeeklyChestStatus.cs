namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "Exploration : coffres cachés hebdomadaires par royaume".</summary>
public sealed class WeeklyChestStatus
{
    public required string WeekBucket { get; init; }
    public required bool IsClaimed { get; init; }
    public string? ClaimedByCharacterName { get; init; }
    public long RewardGold { get; init; }

    /// <summary>Voir GDD/demande utilisateur — "le coffre de la semaine doit etre cache sur la map" : position a trouver en explorant, affichee en case jaune (voir WorldMap cote client).</summary>
    public int PositionX { get; init; }
    public int PositionY { get; init; }
}
