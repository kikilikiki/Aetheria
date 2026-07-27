namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "Monstres" de <see cref="StatisticsEntity"/>.</summary>
public sealed class MonsterStatistics
{
    public int MonstersCaptured { get; set; }
    public int SpeciesDiscovered { get; set; }
    public int EvolutionsPerformed { get; set; }
    public int LegendariesObtained { get; set; }
    public int ShiniesFound { get; set; }
}
