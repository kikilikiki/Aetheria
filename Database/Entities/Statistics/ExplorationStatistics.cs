namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "Exploration" de <see cref="StatisticsEntity"/>.</summary>
public sealed class ExplorationStatistics
{
    public int DungeonsVisited { get; set; }
    public int MapsDiscovered { get; set; }
    public int ChestsOpened { get; set; }
    public int SecretsFound { get; set; }
    public int TeleportersUnlocked { get; set; }
}
