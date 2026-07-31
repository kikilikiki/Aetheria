namespace Aetheria.Database.Entities;

/// <summary>Dégâts cumulés d'un membre de guilde sur une instance de raid (table <c>GuildRaidDamageEntries</c>) — même schéma que <see cref="WorldBossDamageEntity"/>.</summary>
public sealed class GuildRaidDamageEntity
{
    public Guid Id { get; set; }

    public Guid GuildRaidId { get; set; }
    public GuildRaidEntity? GuildRaid { get; set; }

    public Guid CharacterId { get; set; }
    public required string CharacterName { get; set; }
    public long TotalDamage { get; set; }
}
