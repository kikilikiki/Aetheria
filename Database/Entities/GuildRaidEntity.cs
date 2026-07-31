using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Boss de raid de guilde (table <c>GuildRaids</c>) — voir GDD/demande utilisateur "Raids de
/// guilde (boss coopératif nécessitant plusieurs joueurs, distinct du world boss solo/petit
/// groupe)". Même schéma que <see cref="WorldBossEntity"/>, scopé à une guilde précise
/// (<see cref="GuildId"/>) plutôt que global — voir Server.World.GuildRaidService.
/// </summary>
public sealed class GuildRaidEntity
{
    public Guid Id { get; set; }

    public Guid GuildId { get; set; }
    public GuildEntity? Guild { get; set; }

    public required string Name { get; set; }
    public int SpeciesId { get; set; }
    public Element BossElement { get; set; } = Element.Neutre;
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public bool IsAlive { get; set; } = true;
    public DateTime SpawnedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? KilledAtUtc { get; set; }
    public string? KillerCharacterName { get; set; }

    public List<GuildRaidDamageEntity> DamageEntries { get; set; } = [];
}
