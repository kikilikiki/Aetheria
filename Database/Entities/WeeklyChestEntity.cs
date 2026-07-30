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
}
