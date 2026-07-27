namespace Aetheria.Database.Entities.Statistics;

/// <summary>Sous-ensemble "Combat" de <see cref="StatisticsEntity"/> (voir GDD — Statistiques Joueur).</summary>
public sealed class CombatStatistics
{
    public int FightsWon { get; set; }
    public int FightsLost { get; set; }
    public int BossesDefeated { get; set; }
    public int DungeonsCompleted { get; set; }
    public int MaxFloorReached { get; set; }
    public long DamageDealt { get; set; }
    public long DamageTaken { get; set; }
    public long HealingDone { get; set; }
    public int CriticalHits { get; set; }
}
