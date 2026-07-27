using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Peuple la première chaîne de production du GDD : Mineur → Minerai de fer → Forgeron →
/// Épée de fer → Vente Hôtel des ventes (l'enchantement viendra étoffer cette chaîne plus tard).
/// </summary>
public static class ProfessionCatalogSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        if (await db.Recipes.AnyAsync(ct))
        {
            return;
        }

        var ironOre = new ItemEntity
        {
            Name = "Minerai de fer",
            Description = "Ressource brute extraite par les mineurs.",
            ItemType = ItemType.Ressource,
            Rarity = Rarity.Commun,
        };

        var ironSword = new ItemEntity
        {
            Name = "Épée de fer",
            Description = "Une épée simple mais fiable, forgée à partir de minerai de fer.",
            ItemType = ItemType.Arme,
            Rarity = Rarity.Commun,
            StatBonus = new Shared.Models.StatBlock(0, 5, 0, 0, 0, 0),
        };

        db.Items.AddRange(ironOre, ironSword);
        await db.SaveChangesAsync(ct);

        db.Recipes.Add(new RecipeEntity
        {
            Name = "Épée de fer",
            Profession = ProfessionType.Forgeron,
            RequiredLevel = 1,
            ResultItemId = ironSword.Id,
            ResultQuantity = 1,
            Ingredients =
            [
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = ironOre.Id, Quantity = 3 },
            ],
        });

        await db.SaveChangesAsync(ct);
    }
}
