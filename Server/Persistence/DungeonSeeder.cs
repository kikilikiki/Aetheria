using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Peuple un premier jeu de donjons, repris tel quel de l'exemple du GDD (Royaume du Nord).
/// Doit tourner après <see cref="DatabaseSeeder"/> (a besoin des royaumes déjà en base).
/// </summary>
public static class DungeonSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        if (await db.Dungeons.AnyAsync(ct))
        {
            return;
        }

        var kingdomsByType = await db.Kingdoms.ToDictionaryAsync(k => k.Type, ct);
        if (!kingdomsByType.TryGetValue(KingdomType.Feu, out var kingdomDuFeu))
        {
            return;
        }

        db.Dungeons.AddRange(
            new DungeonEntity
            {
                Name = "Donjon des Araignées", KingdomId = kingdomDuFeu.Id,
                Description = "Des galeries obscures tissées de toiles épaisses.", Seed = 1001,
            },
            new DungeonEntity
            {
                Name = "Donjon des Glaces", KingdomId = kingdomsByType[KingdomType.Glaces].Id,
                Description = "Cavernes de glace éternelle, glissantes et mortelles.", Seed = 1002,
            },
            new DungeonEntity
            {
                Name = "Donjon du Dragon", KingdomId = kingdomDuFeu.Id,
                Description = "L'antre d'un dragon ancestral, très convoité.", Seed = 1003,
            },
            new DungeonEntity
            {
                Name = "Donjon des Ruines", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Vestiges d'une civilisation oubliée.", Seed = 1004,
            },
            new DungeonEntity
            {
                Name = "Donjon Sans Fin", KingdomId = kingdomsByType[KingdomType.Nature].Id,
                Description = "Aucun étage final connu à ce jour.", Seed = 1005,
            });

        await db.SaveChangesAsync(ct);
    }
}
