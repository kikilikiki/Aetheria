namespace Aetheria.Database.Entities;

/// <summary>Guilde de joueurs (table <c>Guilds</c>).</summary>
public sealed class GuildEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public Guid LeaderCharacterId { get; set; }
    public CharacterEntity? LeaderCharacter { get; set; }

    public int Level { get; set; } = 1;

    /// <summary>Voir GDD/demande utilisateur — "Niveau de guilde" : monte avec l'or déposé à la Banque (voir GuildService.DepositGoldAsync), même formule que les autres systèmes de niveau (XP requise au niveau N = N × 1000 — palier plus large qu'un personnage/métier, une guilde compte plusieurs membres).</summary>
    public long GuildExperience { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "Banque de guilde" : or déposé par les membres, dépensable par le chef (pour l'instant, aucune dépense implémentée au-delà du niveau qu'il fait automatiquement monter).</summary>
    public long TreasuryGold { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "Guerres de guildes" : points accumulés depuis la dernière résolution hebdomadaire (voir GuildService.AwardWarPointsAsync/ResolveWeeklyWarAsync), même mécanique que <c>KingdomEntity.WarPoints</c>.</summary>
    public long WarPoints { get; set; }

    /// <summary>Voir GDD/demande utilisateur — "Quêtes de guilde" : objectif hebdomadaire simple (déposer des objets au coffre partagé), voir GuildService.DepositItemAsync. Vide/0 tant qu'aucun dépôt n'a eu lieu cette semaine.</summary>
    public string WeeklyQuestWeekBucket { get; set; } = string.Empty;
    public int WeeklyQuestItemsDeposited { get; set; }
    public bool WeeklyQuestCompleted { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
