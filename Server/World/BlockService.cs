using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Blocage entre personnages (voir demande utilisateur — menu d'interaction en jeu). Blocage
/// « complet » : le bloqueur ne voit plus les messages du bloqué, et aucun des deux ne peut
/// envoyer à l'autre une demande d'ami ou un défi (<see cref="AreBlockedEitherWayAsync"/>).
/// </summary>
public sealed class BlockService(AetheriaDbContext db)
{
    public async Task<string> BlockAsync(Guid blockerCharacterId, Guid blockedCharacterId, CancellationToken ct = default)
    {
        if (blockerCharacterId == blockedCharacterId)
        {
            return "Impossible de se bloquer soi-même.";
        }

        var blocked = await db.Characters.FirstOrDefaultAsync(c => c.Id == blockedCharacterId, ct);
        if (blocked is null)
        {
            return "Personnage introuvable.";
        }

        var existing = await db.Blocks.AnyAsync(
            b => b.BlockerCharacterId == blockerCharacterId && b.BlockedCharacterId == blockedCharacterId, ct);
        if (existing)
        {
            return $"{blocked.Name} est déjà bloqué(e).";
        }

        db.Blocks.Add(new BlockEntity
        {
            Id = Guid.NewGuid(),
            BlockerCharacterId = blockerCharacterId,
            BlockedCharacterId = blockedCharacterId,
            BlockedCharacterName = blocked.Name,
        });

        // Un blocage annule une amitié éventuelle (dans les deux sens).
        var friendships = await db.Friendships
            .Where(f => (f.RequesterCharacterId == blockerCharacterId && f.TargetCharacterId == blockedCharacterId)
                || (f.RequesterCharacterId == blockedCharacterId && f.TargetCharacterId == blockerCharacterId))
            .ToListAsync(ct);
        db.Friendships.RemoveRange(friendships);

        await db.SaveChangesAsync(ct);
        return $"{blocked.Name} est maintenant bloqué(e).";
    }

    public async Task<string> UnblockAsync(Guid blockerCharacterId, string blockedName, CancellationToken ct = default)
    {
        var block = await db.Blocks
            .Include(b => b.BlockedCharacter)
            .FirstOrDefaultAsync(b => b.BlockerCharacterId == blockerCharacterId && b.BlockedCharacterName == blockedName, ct);
        if (block is null)
        {
            return $"{blockedName} n'est pas bloqué(e).";
        }

        db.Blocks.Remove(block);
        await db.SaveChangesAsync(ct);
        return $"{blockedName} n'est plus bloqué(e).";
    }

    /// <summary>Vrai si <paramref name="a"/> a bloqué <paramref name="b"/> OU l'inverse.</summary>
    public async Task<bool> AreBlockedEitherWayAsync(Guid a, Guid b, CancellationToken ct = default) =>
        await db.Blocks.AnyAsync(
            x => (x.BlockerCharacterId == a && x.BlockedCharacterId == b)
                || (x.BlockerCharacterId == b && x.BlockedCharacterId == a), ct);

    /// <summary>Identifiants des personnages avec qui <paramref name="characterId"/> a une relation de blocage (dans un sens ou l'autre) — pour le filtrage du tchat côté client.</summary>
    public async Task<List<Guid>> GetBlockRelationsAsync(Guid characterId, CancellationToken ct = default) =>
        await db.Blocks
            .Where(b => b.BlockerCharacterId == characterId || b.BlockedCharacterId == characterId)
            .Select(b => b.BlockerCharacterId == characterId ? b.BlockedCharacterId : b.BlockerCharacterId)
            .Distinct()
            .ToListAsync(ct);
}
