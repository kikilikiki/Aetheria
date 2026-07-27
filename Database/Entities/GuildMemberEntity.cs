namespace Aetheria.Database.Entities;

/// <summary>
/// Appartenance d'un personnage à une guilde. Un personnage n'appartient qu'à une seule
/// guilde à la fois (voir index unique dans <c>AetheriaDbContext</c>). Le chef de guilde
/// (<see cref="GuildEntity.LeaderCharacterId"/>) possède aussi une ligne ici, pour que la
/// liste des membres soit complète.
/// </summary>
public sealed class GuildMemberEntity
{
    public Guid Id { get; set; }

    public Guid GuildId { get; set; }
    public GuildEntity? Guild { get; set; }

    public Guid CharacterId { get; set; }
    public CharacterEntity? Character { get; set; }

    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
