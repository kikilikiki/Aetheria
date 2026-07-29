using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Peuple la première chaîne de production du GDD : Mineur → Minerai de fer → Forgeron →
/// Épée de fer → Vente Hôtel des ventes (l'enchantement viendra étoffer cette chaîne plus tard).
/// </summary>
/// <remarks>
/// Voir GDD/demande utilisateur — "ajoute un maximum d'items et de craft" : idempotent PAR OBJET/
/// RECETTE (recherche par nom, création seulement si absent) plutôt qu'un simple garde-fou
/// "si une recette existe déjà, ne rien faire" — sans quoi élargir ce catalogue plus tard
/// n'aurait aucun effet sur une base déjà seedée une première fois (ex. la prod, jamais wipée
/// entre deux redéploiements contrairement à la dev — voir Docs/README.md).
/// </remarks>
public static class ProfessionCatalogSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        var itemsByName = await db.Items.ToDictionaryAsync(i => i.Name, ct);

        ItemEntity GetOrCreateItem(ItemEntity template)
        {
            if (itemsByName.TryGetValue(template.Name, out var existing))
            {
                return existing;
            }

            db.Items.Add(template);
            itemsByName[template.Name] = template;
            return template;
        }

        var ironOre = GetOrCreateItem(new ItemEntity { Name = "Minerai de fer", Description = "Ressource brute extraite par les mineurs.", ItemType = ItemType.Ressource, Rarity = Rarity.Commun });
        var ironSword = GetOrCreateItem(new ItemEntity { Name = "Épée de fer", Description = "Une épée simple mais fiable, forgée à partir de minerai de fer.", ItemType = ItemType.Arme, Rarity = Rarity.Commun, StatBonus = new Shared.Models.StatBlock(0, 5, 0, 0, 0, 0) });
        var silverOre = GetOrCreateItem(new ItemEntity { Name = "Minerai d'argent", Description = "Ressource brute plus rare que le fer.", ItemType = ItemType.Ressource, Rarity = Rarity.PeuCommun });
        var goldOre = GetOrCreateItem(new ItemEntity { Name = "Minerai d'or", Description = "Un métal précieux prisé des forgerons expérimentés.", ItemType = ItemType.Ressource, Rarity = Rarity.Rare });
        var manaCrystal = GetOrCreateItem(new ItemEntity { Name = "Cristal de mana", Description = "Un cristal pulsant d'énergie magique brute.", ItemType = ItemType.Ressource, Rarity = Rarity.Rare });
        var dragonScale = GetOrCreateItem(new ItemEntity { Name = "Écaille de dragon", Description = "Écaille arrachée à un dragon vaincu — extrêmement résistante.", ItemType = ItemType.Ressource, Rarity = Rarity.Legendaire });
        var medicinalHerb = GetOrCreateItem(new ItemEntity { Name = "Herbe médicinale", Description = "Une plante aux vertus curatives reconnues.", ItemType = ItemType.Ressource, Rarity = Rarity.Commun });
        var ancientWood = GetOrCreateItem(new ItemEntity { Name = "Bois ancien", Description = "Bois noueux prélevé sur un arbre centenaire.", ItemType = ItemType.Ressource, Rarity = Rarity.Commun });
        var silverSword = GetOrCreateItem(new ItemEntity { Name = "Épée d'argent", Description = "Une lame affûtée, plus tranchante que le fer.", ItemType = ItemType.Arme, Rarity = Rarity.PeuCommun, StatBonus = new Shared.Models.StatBlock(0, 9, 0, 1, 0, 0) });
        var goldSword = GetOrCreateItem(new ItemEntity { Name = "Épée en or", Description = "Une arme somptueuse, aussi belle que mortelle.", ItemType = ItemType.Arme, Rarity = Rarity.Rare, StatBonus = new Shared.Models.StatBlock(0, 14, 0, 2, 0, 0) });
        var ironArmor = GetOrCreateItem(new ItemEntity { Name = "Armure de fer", Description = "Une protection solide pour l'aventurier prudent.", ItemType = ItemType.Armure, Rarity = Rarity.PeuCommun, StatBonus = new Shared.Models.StatBlock(15, 0, 6, 0, 0, 2) });
        var goldArmor = GetOrCreateItem(new ItemEntity { Name = "Armure d'or", Description = "Aussi éclatante que protectrice.", ItemType = ItemType.Armure, Rarity = Rarity.Rare, StatBonus = new Shared.Models.StatBlock(25, 0, 10, 0, 0, 4) });
        var greaterHealPotion = GetOrCreateItem(new ItemEntity { Name = "Potion de soin supérieure", Description = "Une version concentrée de la potion de soin classique.", ItemType = ItemType.Consommable, Rarity = Rarity.PeuCommun, StatBonus = new Shared.Models.StatBlock(40, 0, 0, 0, 0, 0), Price = 25 });
        var strengthElixir = GetOrCreateItem(new ItemEntity { Name = "Élixir de force", Description = "Un breuvage qui décuple temporairement la force musculaire.", ItemType = ItemType.Consommable, Rarity = Rarity.Rare, Price = 60 });

        // Voir GDD/demande utilisateur — "ajoute des objets que l'on ne peut pas obtenir ni
        // fabriquer" : aucune recette ne les produit, prix à 0 (pas en Boutique) et
        // IsObtainable=false (pas dans le tirage de butin, voir LootService) — uniquement
        // distribuables par un Fondateur/admin via le panel admin en jeu ("DONNER UN OBJET").
        GetOrCreateItem(new ItemEntity { Name = "Couronne du Fondateur", Description = "Un objet honorifique remis à la main par un Fondateur.", ItemType = ItemType.Cosmetique, Rarity = Rarity.Mythique, IsObtainable = false });
        GetOrCreateItem(new ItemEntity { Name = "Trophée du Champion", Description = "Récompense honorifique du classement PvP, remise manuellement.", ItemType = ItemType.Cosmetique, Rarity = Rarity.Mythique, IsObtainable = false });
        GetOrCreateItem(new ItemEntity { Name = "Sceptre légendaire", Description = "Une arme d'un pouvoir inégalé, jamais lâchée par le hasard.", ItemType = ItemType.Arme, Rarity = Rarity.Mythique, StatBonus = new Shared.Models.StatBlock(0, 40, 0, 5, 5, 5), IsObtainable = false });

        await db.SaveChangesAsync(ct);

        var recipesByName = await db.Recipes.Select(r => r.Name).ToHashSetAsync(ct);

        void AddRecipeIfMissing(RecipeEntity recipe)
        {
            if (recipesByName.Add(recipe.Name))
            {
                db.Recipes.Add(recipe);
            }
        }

        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Épée de fer", Profession = ProfessionType.Forgeron, RequiredLevel = 1, ResultItemId = ironSword.Id, ResultQuantity = 1,
            Ingredients = [new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = ironOre.Id, Quantity = 3 }],
        });
        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Armure de fer", Profession = ProfessionType.Forgeron, RequiredLevel = 2, ResultItemId = ironArmor.Id, ResultQuantity = 1,
            Ingredients = [new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = ironOre.Id, Quantity = 5 }],
        });
        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Épée d'argent", Profession = ProfessionType.Forgeron, RequiredLevel = 4, ResultItemId = silverSword.Id, ResultQuantity = 1,
            Ingredients = [new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = silverOre.Id, Quantity = 4 }],
        });
        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Épée en or", Profession = ProfessionType.Forgeron, RequiredLevel = 7, ResultItemId = goldSword.Id, ResultQuantity = 1,
            Ingredients =
            [
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = goldOre.Id, Quantity = 3 },
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = silverOre.Id, Quantity = 2 },
            ],
        });
        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Armure d'or", Profession = ProfessionType.Forgeron, RequiredLevel = 8, ResultItemId = goldArmor.Id, ResultQuantity = 1,
            Ingredients =
            [
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = goldOre.Id, Quantity = 4 },
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = dragonScale.Id, Quantity = 1 },
            ],
        });
        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Potion de soin supérieure", Profession = ProfessionType.Alchimiste, RequiredLevel = 1, ResultItemId = greaterHealPotion.Id, ResultQuantity = 1,
            Ingredients = [new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = medicinalHerb.Id, Quantity = 2 }],
        });
        AddRecipeIfMissing(new RecipeEntity
        {
            Name = "Élixir de force", Profession = ProfessionType.Alchimiste, RequiredLevel = 5, ResultItemId = strengthElixir.Id, ResultQuantity = 1,
            Ingredients =
            [
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = manaCrystal.Id, Quantity = 2 },
                new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = ancientWood.Id, Quantity = 1 },
            ],
        });

        await db.SaveChangesAsync(ct);
    }
}
