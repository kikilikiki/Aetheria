using System.Collections.Concurrent;

namespace Aetheria.Server.Networking;

/// <summary>
/// Invitations de duel PvP en attente (voir GDD/demande utilisateur — "ajouter les demandes en
/// duel pour le pvp"). En mémoire, comme <see cref="WorldSessionRegistry"/>/<c>ArenaQueueService</c>
/// — pas de persistance nécessaire, une invitation expirée ou refusée disparaît simplement. Une
/// seule invitation active à la fois par destinataire (la plus récente écrase la précédente).
/// </summary>
public sealed class DuelInviteService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromSeconds(30);

    private sealed record PendingInvite(Guid FromCharacterId, string FromCharacterName, DateTime ExpiresAtUtc);

    private readonly ConcurrentDictionary<Guid, PendingInvite> _invitesByTargetCharacterId = new();

    public void SendInvite(Guid fromCharacterId, string fromCharacterName, Guid targetCharacterId) =>
        _invitesByTargetCharacterId[targetCharacterId] = new PendingInvite(fromCharacterId, fromCharacterName, DateTime.UtcNow + InviteLifetime);

    public bool TryConsume(Guid targetCharacterId, out Guid fromCharacterId, out string fromCharacterName)
    {
        if (_invitesByTargetCharacterId.TryRemove(targetCharacterId, out var invite) && invite.ExpiresAtUtc > DateTime.UtcNow)
        {
            fromCharacterId = invite.FromCharacterId;
            fromCharacterName = invite.FromCharacterName;
            return true;
        }

        fromCharacterId = Guid.Empty;
        fromCharacterName = string.Empty;
        return false;
    }
}
