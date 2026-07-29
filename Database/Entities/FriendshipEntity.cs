using Aetheria.Shared.Enums;

namespace Aetheria.Database.Entities;

/// <summary>
/// Relation d'amitié entre deux personnages (table <c>Friendships</c>) — voir GDD/demande
/// utilisateur "ajouter les amis (online/offline, discussion privée, niveau, équipe...)". Une
/// seule ligne par paire, orientée demandeur → destinataire (voir <see cref="FriendService"/>
/// côté serveur pour la résolution bidirectionnelle une fois acceptée).
/// </summary>
public sealed class FriendshipEntity
{
    public Guid Id { get; set; }

    public Guid RequesterCharacterId { get; set; }
    public CharacterEntity? RequesterCharacter { get; set; }

    public Guid TargetCharacterId { get; set; }
    public CharacterEntity? TargetCharacter { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
