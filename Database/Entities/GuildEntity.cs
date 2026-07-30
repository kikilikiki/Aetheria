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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
