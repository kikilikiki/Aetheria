using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Server.Persistence;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>Voir Docs/Idees.md — "Arbre de talents/compétences général" : déblocage de nœuds contre des points gagnés par montée de niveau (voir <c>MonsterProgressionService.GrantExperience</c>, <c>TalentTreeCatalog</c> pour la définition des nœuds).</summary>
public sealed class MonsterTalentService(AetheriaDbContext db, SessionTokenStore tokenStore)
{
    public async Task<MonsterTalentStatus> GetStatusAsync(Guid monsterId, string sessionToken, CancellationToken ct = default)
    {
        var monster = await ResolveOwnedMonsterAsync(monsterId, sessionToken, ct);
        return ToStatus(monster);
    }

    public async Task<MonsterTalentStatus> UnlockNodeAsync(Guid monsterId, UnlockTalentNodeRequest request, CancellationToken ct = default)
    {
        var monster = await ResolveOwnedMonsterAsync(monsterId, request.SessionToken, ct);
        var node = TalentTreeCatalog.Find(request.NodeKey)
            ?? throw new AccountOperationException("Nœud de talent inconnu.");

        var unlocked = TalentTreeCatalog.ParseUnlocked(monster.UnlockedTalentNodeKeys);
        if (unlocked.Contains(node.Key))
        {
            throw new AccountOperationException("Ce talent est déjà débloqué.");
        }

        if (monster.TalentPoints < 1)
        {
            throw new AccountOperationException("Pas assez de points de talent.");
        }

        if (node.Requires.Any(required => !unlocked.Contains(required)))
        {
            throw new AccountOperationException("Prérequis non débloqués pour ce talent.");
        }

        unlocked.Add(node.Key);
        monster.UnlockedTalentNodeKeys = string.Join(',', unlocked);
        monster.TalentPoints -= 1;
        await db.SaveChangesAsync(ct);

        return ToStatus(monster);
    }

    private async Task<MonsterEntity> ResolveOwnedMonsterAsync(Guid monsterId, string sessionToken, CancellationToken ct)
    {
        if (!tokenStore.TryValidate(sessionToken, out var userId))
        {
            throw new AccountOperationException("Session invalide ou expirée.");
        }

        var monster = await db.Monsters.FirstOrDefaultAsync(m => m.Id == monsterId, ct)
            ?? throw new AccountOperationException("Créature introuvable.");

        var owned = await db.Characters.AnyAsync(c => c.Id == monster.OwnerCharacterId && c.UserId == userId, ct);
        if (!owned)
        {
            throw new AccountOperationException("Cette créature n'appartient pas à un personnage de ce compte.");
        }

        return monster;
    }

    private static MonsterTalentStatus ToStatus(MonsterEntity monster) => new()
    {
        MonsterId = monster.Id,
        TalentPoints = monster.TalentPoints,
        UnlockedNodeKeys = TalentTreeCatalog.ParseUnlocked(monster.UnlockedTalentNodeKeys).ToList(),
    };
}
