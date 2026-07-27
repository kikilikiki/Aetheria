namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "PvP" de <see cref="StatisticsEntity"/>.</summary>
public sealed class PvpStatistics
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int WinStreak { get; set; }
    public int CurrentRank { get; set; }
    public int BestRank { get; set; }
    public int Season { get; set; }
}
