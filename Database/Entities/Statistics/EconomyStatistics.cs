namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "Économie" de <see cref="StatisticsEntity"/>.</summary>
public sealed class EconomyStatistics
{
    public long GoldEarned { get; set; }
    public long GoldSpent { get; set; }
    public int ItemsSold { get; set; }
    public int ItemsBought { get; set; }
    public int TradesCompleted { get; set; }
    public int ItemsCrafted { get; set; }
}
