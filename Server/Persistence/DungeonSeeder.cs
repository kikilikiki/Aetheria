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
        var kingdomsByType = await db.Kingdoms.ToDictionaryAsync(k => k.Type, ct);
        if (!kingdomsByType.TryGetValue(KingdomType.Feu, out var kingdomDuFeu))
        {
            return;
        }

        // Voir GDD/demande utilisateur — "de nouveaux donjons avec leur niveau min pour rentrer
        // (toujours 1 donjon de niveau 1 min pour les débutants)" et "des donjons hardcore
        // (niv 15+), en dessous il n'y aura pas de hardcore" : liste complète (existants +
        // nouveaux) plutôt qu'un simple AddRange bloqué par un flag "déjà seedé" — permet
        // d'ajouter nos nouveaux donjons sur une base déjà seedée, comme les autres backfills
        // idempotents de ce fichier de seed (voir MonsterCatalogSeeder pour le même choix).
        var wanted = new List<DungeonEntity>
        {
            new()
            {
                Name = "Donjon des Araignées", KingdomId = kingdomDuFeu.Id,
                Description = "Des galeries obscures tissées de toiles épaisses.", Seed = 1001,
            },
            new()
            {
                Name = "Donjon des Glaces", KingdomId = kingdomsByType[KingdomType.Glaces].Id,
                Description = "Cavernes de glace éternelle, glissantes et mortelles.", Seed = 1002,
            },
            new()
            {
                Name = "Donjon du Dragon", KingdomId = kingdomDuFeu.Id,
                Description = "L'antre d'un dragon ancestral, très convoité.", Seed = 1003,
            },
            new()
            {
                Name = "Donjon des Ruines", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Vestiges d'une civilisation oubliée.", Seed = 1004,
            },
            new()
            {
                Name = "Donjon Sans Fin", KingdomId = kingdomsByType[KingdomType.Nature].Id,
                Description = "Aucun étage final connu à ce jour.", Seed = 1005,
            },
            new()
            {
                Name = "Volcan Rugissant", KingdomId = kingdomDuFeu.Id,
                Description = "Un donjon hardcore : la lave elle-même semble vouloir votre perte.",
                Seed = 1006, MinLevel = 15, IsHardcore = true,
            },
            new()
            {
                Name = "Crypte du Néant", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Un donjon hardcore où même les ombres évitent de s'aventurer.",
                Seed = 1007, MinLevel = 20, IsHardcore = true,
            },
        };

        var existingNames = (await db.Dungeons.Select(d => d.Name).ToListAsync(ct)).ToHashSet();
        var missing = wanted.Where(d => !existingNames.Contains(d.Name)).ToList();
        if (missing.Count > 0)
        {
            db.Dungeons.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }
}
