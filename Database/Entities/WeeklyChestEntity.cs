namespace Aetheria.Database.Entities;

/// <summary>
/// Coffre caché hebdomadaire d'un royaume (table <c>WeeklyChests</c>) — voir GDD/demande
/// utilisateur "Exploration : coffres cachés hebdomadaires par royaume". Une ligne par royaume ×
/// semaine, créée à la demande (voir <c>Server/World/WeeklyChestService</c>) plutôt que par un job
/// de seed, réclamable une seule fois par le premier personnage à le faire.
/// </summary>
public sealed class WeeklyChestEntity
{
    public Guid Id { get; set; }

    public int KingdomId { get; set; }
    public KingdomEntity? Kingdom { get; set; }

    /// <summary>Semaine ISO (format <c>"AAAA-Wnn"</c>), même style de clé que <c>KingdomWarScheduler</c>.</summary>
    public required string WeekBucket { get; set; }

    public Guid? ClaimedByCharacterId { get; set; }
    public string? ClaimedByCharacterName { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public long RewardGold { get; set; }

    /// <summary>
    /// Voir GDD/demande utilisateur — "le coffre de la semaine doit etre cache sur la map" : position
    /// tiree au sort une fois par royaume x semaine (voir WeeklyChestService.GetOrCreateAsync), a
    /// trouver en explorant la carte (case en jaune, voir WorldMap cote client) plutot que reclame
    /// depuis un panneau.
    /// </summary>
    public int PositionX { get; set; }
    public int PositionY { get; set; }
}
