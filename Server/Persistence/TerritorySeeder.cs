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
        if (await db.Territories.AnyAsync(ct))
        {
            return;
        }

        var kingdoms = await db.Kingdoms.ToDictionaryAsync(k => k.Type, ct);
        if (kingdoms.Count == 0)
        {
            return;
        }

        db.Territories.AddRange(
            new TerritoryEntity { Name = "Mine de Braise", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Feu].Id },
            new TerritoryEntity { Name = "Village d'Ignaria", TerritoryType = TerritoryType.Village, ControllingKingdomId = kingdoms[KingdomType.Feu].Id },
            new TerritoryEntity { Name = "Fort de Sylvandre", TerritoryType = TerritoryType.Fort, ControllingKingdomId = kingdoms[KingdomType.Nature].Id },
            // Voir GDD/demande utilisateur — "guerre de territoire... pour que les joueurs de sa
            // team puissent aller faire des quêtes de minage" : une mine par royaume, pour que la
            // mécanique (voir ProfessionService.GatherAsync — accès réservé au royaume contrôleur)
            // s'applique de façon symétrique aux 4 royaumes, pas seulement Feu/Glaces.
            new TerritoryEntity { Name = "Mine de Sylvandre", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Nature].Id },
            new TerritoryEntity { Name = "Mine de Frimavel", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Glaces].Id },
            new TerritoryEntity { Name = "Village de Nocturne", TerritoryType = TerritoryType.Village, ControllingKingdomId = kingdoms[KingdomType.Ombres].Id },
            new TerritoryEntity { Name = "Mine des Ombres", TerritoryType = TerritoryType.Mine, ControllingKingdomId = kingdoms[KingdomType.Ombres].Id });

        await db.SaveChangesAsync(ct);
    }
}
