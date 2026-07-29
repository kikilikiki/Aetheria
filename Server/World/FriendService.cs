using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Networking;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Système d'amis (voir GDD/demande utilisateur — "ajouter les amis : online/offline, discussion
/// privée, niveau, équipe équipée"). La discussion privée réutilise le tchat existant (voir
/// <c>PlayerSession.HandleChatMessage</c>/ChatChannel.Prive) plutôt qu'un système dédié ; ce
/// service ne couvre que la relation elle-même (demande/acceptation/liste/suppression).
/// </summary>
public sealed class FriendService(AetheriaDbContext db, SessionTokenStore tokenStore, WorldSessionRegistry registry)
{
    public async Task<string> SendRequestAsync(FriendActionRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnCharacterAsync(request.SessionToken, request.CharacterId, ct);
        var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName, ct)
            ?? throw new AccountOperationException("Joueur introuvable.");

        if (target.Id == character.Id)
        {
            throw new AccountOperationException("Impossible de s'ajouter soi-même.");
        }

        var existing = await db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterCharacterId == character.Id && f.TargetCharacterId == target.Id)
            || (f.RequesterCharacterId == target.Id && f.TargetCharacterId == character.Id), ct);

        if (existing is { Status: FriendshipStatus.Accepted })
        {
            throw new AccountOperationException($"{target.Name} est déjà dans votre liste d'amis.");
        }

        if (existing is { Status: FriendshipStatus.Pending })
        {
            throw new AccountOperationException("Une demande est déjà en attente entre vous.");
        }

        db.Friendships.Add(new FriendshipEntity { Id = Guid.NewGuid(), RequesterCharacterId = character.Id, TargetCharacterId = target.Id });
        await db.SaveChangesAsync(ct);

        return $"Demande d'ami envoyée à {target.Name}.";
    }

    public async Task<string> RespondAsync(FriendRespondRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnCharacterAsync(request.SessionToken, request.CharacterId, ct);
        var requester = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.RequesterCharacterName, ct)
            ?? throw new AccountOperationException("Joueur introuvable.");

        var friendship = await db.Friendships.FirstOrDefaultAsync(f =>
            f.RequesterCharacterId == requester.Id && f.TargetCharacterId == character.Id && f.Status == FriendshipStatus.Pending, ct)
            ?? throw new AccountOperationException("Aucune demande en attente de ce joueur.");

        if (request.Accept)
        {
            friendship.Status = FriendshipStatus.Accepted;
            await db.SaveChangesAsync(ct);
            return $"{requester.Name} a été ajouté à vos amis.";
        }

        db.Friendships.Remove(friendship);
        await db.SaveChangesAsync(ct);
        return $"Demande de {requester.Name} refusée.";
    }

    public async Task<string> RemoveAsync(FriendActionRequest request, CancellationToken ct = default)
    {
        var character = await ResolveOwnCharacterAsync(request.SessionToken, request.CharacterId, ct);
        var target = await db.Characters.FirstOrDefaultAsync(c => c.Name == request.TargetCharacterName, ct)
            ?? throw new AccountOperationException("Joueur introuvable.");

        var friendships = await db.Friendships.Where(f =>
            (f.RequesterCharacterId == character.Id && f.TargetCharacterId == target.Id)
            || (f.RequesterCharacterId == target.Id && f.TargetCharacterId == character.Id)).ToListAsync(ct);

        if (friendships.Count == 0)
        {
            throw new AccountOperationException($"{target.Name} n'est pas dans votre liste d'amis.");
        }

        db.Friendships.RemoveRange(friendships);
        await db.SaveChangesAsync(ct);
        return $"{target.Name} a été retiré de vos amis.";
    }

    public async Task<IReadOnlyList<FriendSummary>> GetFriendsAsync(Guid characterId, CancellationToken ct = default)
    {
        var friendships = await db.Friendships
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.RequesterCharacterId == characterId || f.TargetCharacterId == characterId))
            .ToListAsync(ct);

        var friendIds = friendships
            .Select(f => f.RequesterCharacterId == characterId ? f.TargetCharacterId : f.RequesterCharacterId)
            .ToList();

        var friends = await db.Characters.Where(c => friendIds.Contains(c.Id)).ToListAsync(ct);

        return friends
            .Select(c => new FriendSummary { CharacterId = c.Id, Name = c.Name, Level = c.Level, IsOnline = registry.IsOnline(c.Id) })
            .OrderByDescending(f => f.IsOnline)
            .ThenBy(f => f.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<FriendRequestSummary>> GetPendingRequestsAsync(Guid characterId, CancellationToken ct = default)
    {
        var pending = await db.Friendships
            .Where(f => f.TargetCharacterId == characterId && f.Status == FriendshipStatus.Pending)
            .ToListAsync(ct);

        var requesterIds = pending.Select(f => f.RequesterCharacterId).ToList();
        var names = await db.Characters.Where(c => requesterIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return pending
            .Select(f => new FriendRequestSummary { RequesterName = names.GetValueOrDefault(f.RequesterCharacterId, "?"), CreatedAtUtc = f.CreatedAtUtc })
            .ToList();
    }

    private async Task<CharacterEntity> ResolveOwnCharacterAsync(string sessionToken, Guid characterId, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        return await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId, ct)
            ?? throw new AccountOperationException("Personnage introuvable pour ce compte.");
    }
}
