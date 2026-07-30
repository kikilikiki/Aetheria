namespace Aetheria.Database.Entities;

/// <summary>
/// Boss mondial (table <c>WorldBosses</c>) — voir GDD/demande utilisateur "un boss monde ou le
/// but est de faire un max de degat, plus on fait de degat plus on a de point, il a une barre de
/// vie et peut etre tue". Un seul actif à la fois (voir <see cref="Server.World.WorldBossService"/>).
/// </summary>
public sealed class WorldBossEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public bool IsAlive { get; set; } = true;
    public DateTime SpawnedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? KilledAtUtc { get; set; }
    public string? KillerCharacterName { get; set; }

    public List<WorldBossDamageEntity> DamageEntries { get; set; } = [];
}
