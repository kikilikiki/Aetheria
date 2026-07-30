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

    /// <summary>Voir GDD/demande utilisateur — "Collections : montures, ailes, titres" : "Monture" ou "Ailes" pour l'instant (voir <see cref="Server.World.AchievementService"/>) ; vide pour les entrées historiques génériques.</summary>
    public string Category { get; set; } = string.Empty;

    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}
