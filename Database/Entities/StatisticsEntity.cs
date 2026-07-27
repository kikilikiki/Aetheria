using Aetheria.Database.Entities.Statistics;

namespace Aetheria.Database.Entities;

/// <summary>
/// Profil de statistiques d'un personnage (table <c>Statistics</c>), une ligne par personnage.
/// Regroupé en sous-catégories (types "owned" EF Core) reprenant exactement les familles du
/// GDD : Combat, Exploration, Monstres, Économie, PvP, Social.
/// </summary>
public sealed class StatisticsEntity
{
    public Guid Id { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public CombatStatistics Combat { get; set; } = new();
    public ExplorationStatistics Exploration { get; set; } = new();
    public MonsterStatistics Monsters { get; set; } = new();
    public EconomyStatistics Economy { get; set; } = new();
    public PvpStatistics Pvp { get; set; } = new();
    public SocialStatistics Social { get; set; } = new();
}
