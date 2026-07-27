using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>Peuple les données de référence qui doivent exister dès le premier démarrage (les 4 royaumes).</summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        if (await db.Kingdoms.AnyAsync(ct))
        {
            return;
        }

        db.Kingdoms.AddRange(
            new KingdomEntity { Type = KingdomType.Feu, Name = "Royaume du Feu", CapitalName = "Ignaria" },
            new KingdomEntity { Type = KingdomType.Nature, Name = "Royaume de la Nature", CapitalName = "Sylvandre" },
            new KingdomEntity { Type = KingdomType.Glaces, Name = "Royaume des Glaces", CapitalName = "Frimavel" },
            new KingdomEntity { Type = KingdomType.Ombres, Name = "Royaume des Ombres", CapitalName = "Nocturne" });

        await db.SaveChangesAsync(ct);
    }
}
