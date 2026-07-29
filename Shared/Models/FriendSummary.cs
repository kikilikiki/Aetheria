namespace Aetheria.Shared.Models;

/// <summary>Voir GDD/demande utilisateur — "ajouter les amis (online/offline, niveau, équipe équipée...)".</summary>
public sealed class FriendSummary
{
    public Guid CharacterId { get; init; }
    public required string Name { get; init; }
    public int Level { get; init; }
    public bool IsOnline { get; init; }
}

/// <summary>Demande d'ami en attente reçue (voir GDD — accepter/refuser).</summary>
public sealed class FriendRequestSummary
{
    public required string RequesterName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
