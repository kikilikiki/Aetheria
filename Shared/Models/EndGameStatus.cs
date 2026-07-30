namespace Aetheria.Shared.Models;

/// <summary>
/// Voir GDD/demande utilisateur — "contenu end-game... gated behind owning every monster at max
/// level + every gameplay achievement, leaderboards excluded" (voir Server/World/EndGameService).
/// </summary>
public sealed class EndGameStatus
{
    public required bool IsEligible { get; init; }
    public required int SpeciesAtMaxLevel { get; init; }
    public required int TotalRequiredSpecies { get; init; }
    public required int AchievementsUnlocked { get; init; }
    public required int TotalAchievements { get; init; }
}
