using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>Démarre la Saison 1 au tout premier lancement du serveur.</summary>
public static class SeasonSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        if (await db.Seasons.AnyAsync(ct))
        {
            return;
        }

        db.Seasons.Add(new SeasonEntity { Number = 1, IsActive = true });
        await db.SaveChangesAsync(ct);
    }
}
