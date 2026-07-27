namespace Aetheria.Database.Entities;

/// <summary>
/// Entrée de collection complétée par un compte (table <c>Collections</c>) — monstres, boss,
/// objets, armes, montures, titres, musiques, apparences, etc. (voir GDD — section Collections).
/// </summary>
public sealed class CollectionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    public required string CollectionKey { get; set; }
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}
