using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.World;

/// <summary>
/// Déblocage des succès (voir <c>Docs/GameDesign.md</c> — section Succès). Appelé par les
/// autres services de gameplay (capture, artisanat, guildes, ...) au moment approprié plutôt
/// qu'exposé comme un endpoint "débloquez ce que vous voulez", qui serait un vecteur de triche.
/// </summary>
public sealed class AchievementService(AetheriaDbContext db)
{
    /// <summary>
    /// Débloque un succès s'il ne l'est pas déjà. Retourne <c>true</c> s'il vient d'être débloqué.
    /// Voir GDD/demande utilisateur — "Collections : montures, ailes" : réutilise ce point d'entrée
    /// unique (appelé par tous les services de gameplay, voir <see cref="MountCatalog"/>/
    /// <see cref="WingCatalog"/>) plutôt que de dupliquer une condition de déblocage par service.
    /// </summary>
    public async Task<bool> UnlockAsync(Guid userId, string achievementKey, CancellationToken ct = default)
    {
        var alreadyUnlocked = await db.Achievements
            .AnyAsync(a => a.UserId == userId && a.AchievementKey == achievementKey, ct);

        if (alreadyUnlocked)
        {
            return false;
        }

        db.Achievements.Add(new AchievementEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AchievementKey = achievementKey,
        });

        if (MountCatalog.FindByAchievement(achievementKey) is { } mount)
        {
            db.Collections.Add(new CollectionEntity { Id = Guid.NewGuid(), UserId = userId, CollectionKey = mount.Key, Category = "Monture" });
        }

        if (WingCatalog.FindByAchievement(achievementKey) is { } wing)
        {
            db.Collections.Add(new CollectionEntity { Id = Guid.NewGuid(), UserId = userId, CollectionKey = wing.Key, Category = "Ailes" });
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<string>> GetUnlockedKeysAsync(Guid userId, CancellationToken ct = default)
        => await db.Achievements.Where(a => a.UserId == userId).Select(a => a.AchievementKey).ToListAsync(ct);
}
