namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "Social" de <see cref="StatisticsEntity"/>.</summary>
public sealed class SocialStatistics
{
    public int FriendsCount { get; set; }
    public int PlayersHelped { get; set; }
    public long PlayTimeSeconds { get; set; }
    public int MessagesSent { get; set; }
}
