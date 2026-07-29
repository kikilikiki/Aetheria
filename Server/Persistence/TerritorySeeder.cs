using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>Peuple un premier jeu de territoires (mines, villages, forts), un ou deux par royaume.</summary>
public static class TerritorySeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        var kingdoms = await db.Kingdoms.ToDictionaryAsync(k => k.Type, ct);
        if (kingdoms.Count == 0)
        {
            return;
        }

        // Idempotent par nom (voir convention établie cette session) plutôt qu'un simple garde
        // "déjà peuplé" : permet d'ajouter de nouveaux territoires (voir "Champ", GDD/demande
        // utilisateur — "guerre de territoire... des bâtiments (mine, champs etc)") à une base
        // déjà seedée sans avoir à la vider.
        var existingNames = (await db.Territories.Select(t => t.Name).ToListAsync(ct)).ToHashSet();
        var wanted = new List<TerritoryEntity>
        {
            new() { Name = "Mine de Braise", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Feu].Id },
            new() { Name = "Village d'Ignaria", TerritoryType = TerritoryType.Village, ControllingKingdomId = kingdoms[KingdomType.Feu].Id },
            new() { Name = "Fort de Sylvandre", TerritoryType = TerritoryType.Fort, ControllingKingdomId = kingdoms[KingdomType.Nature].Id },
            // Voir GDD/demande utilisateur — "guerre de territoire... pour que les joueurs de sa
            // team puissent aller faire des quêtes de minage" : une mine par royaume, pour que la
            // mécanique (voir ProfessionService.GatherAsync — rendement réduit hors royaume
            // contrôleur) s'applique de façon symétrique aux 4 royaumes, pas seulement Feu/Glaces.
            new() { Name = "Mine de Sylvandre", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Nature].Id },
            new() { Name = "Mine de Frimavel", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Glaces].Id },
            new() { Name = "Village de Nocturne", TerritoryType = TerritoryType.Village, ControllingKingdomId = kingdoms[KingdomType.Ombres].Id },
            new() { Name = "Mine des Ombres", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Ombres].Id },
            // Voir GDD/demande utilisateur — "guerre de territoire... des bâtiments (mine, champs
            // etc)" : un champ par royaume, mêmes noms que KingdomBiome.FieldName.
            new() { Name = "Champ de Braise", TerritoryType = TerritoryType.Champ, ControllingKingdomId = kingdoms[KingdomType.Feu].Id },
            new() { Name = "Champ de Sylvandre", TerritoryType = TerritoryType.Champ, ControllingKingdomId = kingdoms[KingdomType.Nature].Id },
            new() { Name = "Champ de Frimavel", TerritoryType = TerritoryType.Champ, ControllingKingdomId = kingdoms[KingdomType.Glaces].Id },
            new() { Name = "Champ des Ombres", TerritoryType = TerritoryType.Champ, ControllingKingdomId = kingdoms[KingdomType.Ombres].Id },
        };

        var missing = wanted.Where(t => !existingNames.Contains(t.Name)).ToList();
        if (missing.Count > 0)
        {
            db.Territories.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }
}
