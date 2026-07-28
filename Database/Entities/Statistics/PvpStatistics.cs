namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "PvP" de <see cref="StatisticsEntity"/>.</summary>
public sealed class PvpStatistics
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int WinStreak { get; set; }

    /// <summary>
    /// Note ELO (voir GDD — "ligues ELO", <c>CombatService.ComputeNewElo</c>). 1000 par défaut
    /// (valeur de départ standard) plutôt que 0 : un rating à 0 fausserait complètement la
    /// formule ELO contre un premier adversaire proche de 1000.
    /// </summary>
    public int CurrentRank { get; set; } = 1000;
    public int BestRank { get; set; } = 1000;
    public int Season { get; set; }
}
