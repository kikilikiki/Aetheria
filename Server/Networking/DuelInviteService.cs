using System.Collections.Concurrent;

namespace Aetheria.Server.Networking;

/// <summary>
/// Invitations de duel PvP en attente (voir GDD/demande utilisateur — "ajouter les demandes en
/// duel pour le pvp", puis "propose un pvp, si la personne est en team tout les membres doivent
/// accepter"). En mémoire, comme <see cref="WorldSessionRegistry"/>/<c>ArenaQueueService</c> —
/// pas de persistance nécessaire, une invitation expirée ou refusée disparaît simplement.
/// </summary>
public sealed class DuelInviteService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Un duel proposé, éventuellement à plusieurs joueurs de chaque côté (voir GDD — groupe vs
    /// groupe). Seul le côté ciblé doit accepter individuellement (voir <see cref="AcceptedCharacterIds"/>) ;
    /// le côté défieur est réputé consentant du simple fait d'avoir lancé <c>/duel</c>.
    /// </summary>
    public sealed class PendingDuel
    {
        public required Guid InviteId { get; init; }
        public required Guid ChallengerCharacterId { get; init; }
        public required string ChallengerCharacterName { get; init; }
        public required IReadOnlyList<Guid> ChallengerTeamCharacterIds { get; init; }
        public required IReadOnlyList<Guid> TargetTeamCharacterIds { get; init; }
        public required DateTime ExpiresAtUtc { get; init; }

        /// <summary>Vrai pour un duel classé (ELO ajusté à la fin) — voir demande utilisateur, "duel classé".</summary>
        public bool Ranked { get; init; }

        public HashSet<Guid> AcceptedCharacterIds { get; } = [];
    }

    private readonly ConcurrentDictionary<Guid, PendingDuel> _invitesById = new();
    private readonly ConcurrentDictionary<Guid, Guid> _inviteIdByTargetCharacterId = new();

    public PendingDuel CreateInvite(Guid challengerCharacterId, string challengerCharacterName, IReadOnlyList<Guid> challengerTeamCharacterIds, IReadOnlyList<Guid> targetTeamCharacterIds, bool ranked = false)
    {
        var invite = new PendingDuel
        {
            InviteId = Guid.NewGuid(),
            ChallengerCharacterId = challengerCharacterId,
            ChallengerCharacterName = challengerCharacterName,
            ChallengerTeamCharacterIds = challengerTeamCharacterIds,
            TargetTeamCharacterIds = targetTeamCharacterIds,
            ExpiresAtUtc = DateTime.UtcNow + InviteLifetime,
            Ranked = ranked,
        };

        _invitesById[invite.InviteId] = invite;
        foreach (var memberId in targetTeamCharacterIds)
        {
            _inviteIdByTargetCharacterId[memberId] = invite.InviteId;
        }

        return invite;
    }

    /// <summary>Une seule invitation active à la fois par destinataire (la plus récente écrase la précédente pour ce membre).</summary>
    public bool TryGetPendingForTarget(Guid targetCharacterId, out PendingDuel invite)
    {
        if (_inviteIdByTargetCharacterId.TryGetValue(targetCharacterId, out var inviteId)
            && _invitesById.TryGetValue(inviteId, out var found)
            && found.ExpiresAtUtc > DateTime.UtcNow)
        {
            invite = found;
            return true;
        }

        invite = null!;
        return false;
    }

    public void RemoveInvite(Guid inviteId)
    {
        if (!_invitesById.TryRemove(inviteId, out var invite))
        {
            return;
        }

        foreach (var memberId in invite.TargetTeamCharacterIds)
        {
            // Ne retire l'entrée du destinataire que si elle pointe toujours vers CETTE invitation
            // (il pourrait déjà en avoir reçu une nouvelle d'un autre défieur entre-temps).
            _inviteIdByTargetCharacterId.TryGetValue(memberId, out var currentInviteId);
            if (currentInviteId == invite.InviteId)
            {
                _inviteIdByTargetCharacterId.TryRemove(memberId, out _);
            }
        }
    }
}
