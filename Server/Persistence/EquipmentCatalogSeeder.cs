using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Voir GDD/demande utilisateur — grand catalogue d'artisanat (ressources naturelles, essences,
/// matériaux de boss, matériaux craftés, armes/armures/accessoires, objets légendaires, objets
/// admin non obtenables) et "les items équipés donnent des avantages à nos monstres" (voir
/// <see cref="ItemEntity.StatBonus"/>, appliqué en combat par
/// <c>CombatService.GetEquipmentBonusAsync</c>). Idempotent par nom (voir
/// <c>ProfessionCatalogSeeder</c>) — jamais un simple garde-fou "si du contenu existe déjà", pour
/// que le catalogue puisse continuer à grossir sur une base déjà seedée (prod incluse).
/// </summary>
/// <remarks>
/// Simplification assumée (voir Docs/README.md) : les matériaux de boss/essences sont de simples
/// objets "Ressource" obtenables via le même tirage de butin aléatoire que le reste (pas de table
/// de butin dédiée par type de rencontre) — la distinction "matériau de boss" est donc pour
/// l'instant purement narrative (nom/lore), pas mécanique.
/// </remarks>
public static class EquipmentCatalogSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        var itemsByName = await db.Items.ToDictionaryAsync(i => i.Name, ct);

        ItemEntity GetOrCreateItem(string name, ItemType type, Rarity rarity, string description, StatBlock? statBonus = null, bool isObtainable = true, int price = 0)
        {
            if (itemsByName.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var entity = new ItemEntity
            {
                Name = name,
                Description = description,
                ItemType = type,
                Rarity = rarity,
                StatBonus = statBonus ?? StatBlock.Zero,
                IsObtainable = isObtainable,
                Price = price,
            };
            db.Items.Add(entity);
            itemsByName[name] = entity;
            return entity;
        }

        // Voir GDD — "Ressources naturelles" : obtenues par le minage/la cueillette/le butin de
        // combat, aucune recette ne les produit (matières premières).
        ItemEntity Material(string name, Rarity rarity, bool isObtainable = true) =>
            GetOrCreateItem(name, ItemType.Ressource, rarity, $"Ressource brute : {name}.", isObtainable: isObtainable);

        // Bois
        Material("Bois", Rarity.Commun);
        Material("Bois ancien", Rarity.PeuCommun); // voir GDD — casse alignée sur ProfessionCatalogSeeder
        Material("Bois Sombre", Rarity.Rare);
        Material("Bois Sacré", Rarity.Epique);
        // Pierre
        Material("Pierre", Rarity.Commun);
        Material("Pierre Runique", Rarity.PeuCommun);
        Material("Obsidienne", Rarity.Rare);
        Material("Marbre Blanc", Rarity.Epique);
        // Minerais — voir GDD/demande utilisateur : "Minerai de fer"/"d'argent"/"d'or" existent
        // déjà (casse exacte différente dans ProfessionCatalogSeeder) ; noms alignés ici pour
        // réutiliser le même objet plutôt que d'en créer un doublon quasi-identique.
        var ironOre = Material("Minerai de fer", Rarity.Commun);
        var copperOre = Material("Minerai de Cuivre", Rarity.Commun);
        var silverOre = Material("Minerai d'argent", Rarity.PeuCommun);
        var goldOre = Material("Minerai d'or", Rarity.Rare);
        var mythrilOre = Material("Minerai de Mythril", Rarity.Epique);
        var aetherOre = Material("Minerai d'Aether", Rarity.Legendaire);
        var stellarOre = Material("Minerai Stellaire", Rarity.Mythique);
        // Cristaux
        var blueCrystal = Material("Cristal Bleu", Rarity.PeuCommun);
        Material("Cristal Rouge", Rarity.PeuCommun);
        Material("Cristal Vert", Rarity.PeuCommun);
        Material("Cristal Violet", Rarity.PeuCommun);
        var aetherCrystal = Material("Cristal d'Aether", Rarity.Legendaire);
        var stellarCrystal = Material("Cristal Stellaire", Rarity.Mythique);
        // Flore
        Material("Fleur Lunaire", Rarity.PeuCommun);
        Material("Fleur Solaire", Rarity.PeuCommun);
        Material("Herbe médicinale", Rarity.Commun);
        Material("Champignon Lumineux", Rarity.PeuCommun);
        Material("Racine Ancienne", Rarity.Rare);
        // Faune
        var wolfPelt = Material("Peau de Loup", Rarity.Commun);
        var thickLeather = Material("Cuir Épais", Rarity.Commun);
        var spiderSilk = Material("Soie d'Araignée", Rarity.PeuCommun);
        Material("Plume de Phénix", Rarity.Legendaire);
        var dragonScale = Material("Écaille de dragon", Rarity.Epique);
        Material("Corne de Minotaure", Rarity.Rare);
        Material("Dent de Wyverne", Rarity.Rare);

        // Essences
        Material("Essence de Feu", Rarity.PeuCommun);
        Material("Essence d'Eau", Rarity.PeuCommun);
        Material("Essence de Terre", Rarity.PeuCommun);
        var windEssence = Material("Essence de Vent", Rarity.PeuCommun);
        var lightEssence = Material("Essence de Lumière", Rarity.Rare);
        var darknessEssence = Material("Essence des Ténèbres", Rarity.Rare);
        var spiritEssence = Material("Essence Spirituelle", Rarity.Rare);

        // Matériaux de boss
        var dragonHeart = Material("Cœur de Dragon", Rarity.Legendaire);
        var titanEye = Material("Œil du Titan", Rarity.Legendaire);
        Material("Noyau de Golem", Rarity.Epique);
        Material("Larme du Kraken", Rarity.Epique);
        var timeFragment = Material("Fragment du Temps", Rarity.Mythique);
        var stellarFragment = Material("Fragment Stellaire", Rarity.Mythique);
        var ancientRune = Material("Rune Antique", Rarity.Rare);
        var sacredRune = Material("Rune Sacrée", Rarity.Epique);
        var cursedRune = Material("Rune Maudite", Rarity.Epique);

        // Voir GDD — "OBJETS ADMIN (IMPOSSIBLES À OBTENIR)" : matériaux, IsObtainable=false (donc
        // exclus du tirage de butin, voir LootService), uniquement distribuables via le panel admin.
        var divineEssence = Material("Essence Divine", Rarity.Admin, isObtainable: false);
        var omegaStone = Material("Pierre Oméga", Rarity.Admin, isObtainable: false);
        var creationFragment = Material("Fragment de Création", Rarity.Admin, isObtainable: false);
        var creatorCube = Material("Cube du Créateur", Rarity.Admin, isObtainable: false);
        var creatorHeart = Material("Cœur du Créateur", Rarity.Admin, isObtainable: false);
        var supremeRune = Material("Rune Suprême", Rarity.Admin, isObtainable: false);
        var primordialStar = Material("Étoile Primordiale", Rarity.Admin, isObtainable: false);
        var originalSoul = Material("Âme Originelle", Rarity.Admin, isObtainable: false);
        var adminCrystal = Material("Cristal Administrateur", Rarity.Admin, isObtainable: false);
        var godsKey = Material("Clé des Dieux", Rarity.Admin, isObtainable: false);

        // Matériaux craftés (intermédiaires — voir "crafted_materials")
        var ironIngot = Material("Lingot de Fer", Rarity.Commun);
        var copperIngot = Material("Lingot de Cuivre", Rarity.Commun);
        var silverIngot = Material("Lingot d'Argent", Rarity.PeuCommun);
        var goldIngot = Material("Lingot d'Or", Rarity.Rare);
        var mythrilIngot = Material("Lingot de Mythril", Rarity.Epique);
        var aetherIngot = Material("Lingot d'Aether", Rarity.Legendaire);
        var stellarIngot = Material("Lingot Stellaire", Rarity.Mythique);
        var treatedWood = Material("Bois Traité", Rarity.Commun);
        var enchantedWood = Material("Bois Enchanté", Rarity.PeuCommun);
        var reinforcedLeather = Material("Cuir Renforcé", Rarity.PeuCommun);
        var magicThread = Material("Fil Magique", Rarity.Rare);
        var purifiedCrystal = Material("Cristal Purifié", Rarity.Rare);
        var upgradeStone = Material("Pierre d'Amélioration", Rarity.Epique);

        // Voir GDD — armes/armures/accessoires/légendaires/admin (équipables, avec bonus de stats
        // en combat — voir GDD/demande utilisateur "une épée en fer donne plus de dégâts").
        // Progression volontairement simple par palier de rareté plutôt que des valeurs
        // individuellement équilibrées (voir Docs/README.md pour cette limite assumée).
        // "Épée d'argent" existe déjà (ProfessionCatalogSeeder) — réutilisée telle quelle (nom aligné en casse) plutôt que dupliquée.
        var silverSword = GetOrCreateItem("Épée d'argent", ItemType.Arme, Rarity.PeuCommun, "Une lame affûtée, plus tranchante que le fer.", new StatBlock(0, 12, 0, 1, 0, 0));
        var royalSword = GetOrCreateItem("Épée Royale", ItemType.Arme, Rarity.Rare, "L'arme d'apparat de la garde royale, redoutable au combat.", new StatBlock(10, 20, 0, 2, 0, 0));
        var mythrilSword = GetOrCreateItem("Épée Mythril", ItemType.Arme, Rarity.Epique, "Forgée dans un métal aussi léger que résistant.", new StatBlock(0, 28, 0, 3, 0, 0));
        var draconicSword = GetOrCreateItem("Épée Draconique", ItemType.Arme, Rarity.Legendaire, "Une lame trempée dans le sang d'un dragon vaincu.", new StatBlock(20, 45, 0, 4, 0, 0));
        var elvenBow = GetOrCreateItem("Arc Elfique", ItemType.Arme, Rarity.PeuCommun, "Un arc gracieux taillé dans du bois enchanté.", new StatBlock(0, 15, 0, 5, 0, 0));
        var stellarBow = GetOrCreateItem("Arc Stellaire", ItemType.Arme, Rarity.Epique, "Ses flèches semblent tracées d'une traînée d'étoiles.", new StatBlock(0, 25, 0, 8, 0, 0));
        var royalLance = GetOrCreateItem("Lance Royale", ItemType.Arme, Rarity.Rare, "Une lance ornée des armoiries du royaume.", new StatBlock(0, 22, 5, 0, 0, 0));
        var titanHammer = GetOrCreateItem("Marteau Titan", ItemType.Arme, Rarity.Epique, "Un marteau si lourd que seul un titan pourrait le manier.", new StatBlock(0, 35, 8, 0, 0, 0));
        var twinDaggers = GetOrCreateItem("Dagues Jumelles", ItemType.Arme, Rarity.PeuCommun, "Une paire de dagues faites pour frapper vite et fort.", new StatBlock(0, 16, 0, 10, 0, 0));
        var aetherStaff = GetOrCreateItem("Bâton d'Aether", ItemType.Arme, Rarity.Legendaire, "Un bâton qui canalise l'énergie brute de l'Aether.", new StatBlock(0, 20, 0, 0, 15, 0));
        var voidScythe = GetOrCreateItem("Faux du Néant", ItemType.Arme, Rarity.Mythique, "Une faux qui semble trancher la réalité elle-même.", new StatBlock(15, 50, 0, 5, 0, 0));

        // "Armure de fer" existe déjà (ProfessionCatalogSeeder) — réutilisée telle quelle (nom aligné en casse).
        var ironArmor = GetOrCreateItem("Armure de fer", ItemType.Armure, Rarity.PeuCommun, "Une protection solide pour l'aventurier prudent.", new StatBlock(15, 0, 6, 0, 0, 2));
        var silverArmor = GetOrCreateItem("Armure d'Argent", ItemType.Armure, Rarity.PeuCommun, "Une armure brillante offrant une bonne protection.", new StatBlock(25, 0, 14, 0, 0, 4));
        var royalArmor = GetOrCreateItem("Armure Royale", ItemType.Armure, Rarity.Rare, "L'armure portée par les champions du royaume.", new StatBlock(40, 0, 20, 0, 0, 6));
        var mythrilArmor = GetOrCreateItem("Armure Mythril", ItemType.Armure, Rarity.Epique, "Légère mais quasiment impénétrable.", new StatBlock(55, 0, 28, 0, 0, 9));
        var draconicArmor = GetOrCreateItem("Armure Draconique", ItemType.Armure, Rarity.Legendaire, "Forgée à partir d'écailles de dragon.", new StatBlock(90, 0, 45, 0, 0, 14));
        var stellarArmor = GetOrCreateItem("Armure Stellaire", ItemType.Armure, Rarity.Mythique, "Semble scintiller comme un ciel étoilé.", new StatBlock(110, 0, 55, 0, 0, 18));

        var luckRing = GetOrCreateItem("Anneau de Chance", ItemType.Accessoire, Rarity.Rare, "Porte-bonheur qui semble attirer les bonnes occasions.", new StatBlock(5, 0, 0, 5, 0, 0));
        var mageRing = GetOrCreateItem("Anneau du Mage", ItemType.Accessoire, Rarity.Rare, "Amplifie les capacités mentales de son porteur.", new StatBlock(10, 0, 0, 0, 10, 0));
        var warriorRing = GetOrCreateItem("Anneau du Guerrier", ItemType.Accessoire, Rarity.Epique, "Renforce la poigne et la garde au combat.", new StatBlock(0, 10, 5, 0, 0, 0));
        var royalNecklace = GetOrCreateItem("Collier Royal", ItemType.Accessoire, Rarity.Rare, "Un bijou porté par la noblesse depuis des générations.", new StatBlock(20, 0, 5, 0, 0, 0));
        var stellarNecklace = GetOrCreateItem("Collier Stellaire", ItemType.Accessoire, Rarity.Mythique, "Serti d'un fragment d'étoile scintillant.", new StatBlock(35, 10, 10, 0, 0, 0));
        var astralCape = GetOrCreateItem("Cape Astrale", ItemType.Accessoire, Rarity.Epique, "Flotte au vent comme si elle défiait la gravité.", new StatBlock(0, 0, 0, 12, 8, 0));
        var draconicCape = GetOrCreateItem("Cape Draconique", ItemType.Accessoire, Rarity.Legendaire, "Taillée dans le cuir renforcé d'un dragon.", new StatBlock(25, 0, 15, 0, 0, 0));

        var firstKingCrown = GetOrCreateItem("Couronne du Premier Roi", ItemType.Armure, Rarity.Mythique, "La couronne du tout premier souverain d'Aetheria.", new StatBlock(150, 20, 70, 0, 0, 20));
        var forbiddenBook = GetOrCreateItem("Livre Interdit", ItemType.Accessoire, Rarity.Mythique, "Un grimoire dont la lecture n'est pas sans danger.", new StatBlock(0, 15, 0, 0, 40, 0));
        var infinityOrb = GetOrCreateItem("Orbe de l'Infini", ItemType.Accessoire, Rarity.Mythique, "Un orbe qui semble contenir un univers entier.", new StatBlock(60, 20, 20, 10, 0, 0));
        var aetherHeart = GetOrCreateItem("Cœur d'Aether", ItemType.Accessoire, Rarity.Mythique, "Bat encore d'une énergie primordiale.", new StatBlock(100, 25, 25, 0, 0, 0));
        var worldShard = GetOrCreateItem("Éclat du Monde", ItemType.Accessoire, Rarity.Mythique, "Un fragment du monde lui-même, cristallisé.", new StatBlock(80, 40, 40, 20, 0, 0));

        // Voir GDD — "OBJETS ADMIN (IMPOSSIBLES À OBTENIR)" : IsObtainable=false, aucune recette
        // n'est réellement complétable (leurs ingrédients sont eux-mêmes non obtenables), gardées
        // pour la cohérence narrative du panel admin ("DONNER UN OBJET").
        var creatorSword = GetOrCreateItem("Épée du Créateur", ItemType.Arme, Rarity.Admin, "Une lame qui n'aurait jamais dû exister.", new StatBlock(50, 200, 0, 20, 0, 0), isObtainable: false);
        var creatorArmor = GetOrCreateItem("Armure du Créateur", ItemType.Armure, Rarity.Admin, "Une armure forgée hors du temps.", new StatBlock(400, 0, 200, 0, 0, 50), isObtainable: false);
        var godsCrown = GetOrCreateItem("Couronne des Dieux", ItemType.Armure, Rarity.Admin, "Symbole d'une autorité qui dépasse ce monde.", new StatBlock(300, 100, 150, 0, 0, 40), isObtainable: false);
        var infinityRing = GetOrCreateItem("Anneau de l'Infini", ItemType.Accessoire, Rarity.Admin, "Contient un pouvoir sans limite.", new StatBlock(200, 100, 100, 50, 0, 0), isObtainable: false);
        var architectWings = GetOrCreateItem("Ailes de l'Architecte", ItemType.Accessoire, Rarity.Admin, "Permettent de voir la structure même du monde.", new StatBlock(100, 0, 0, 100, 0, 0), isObtainable: false);
        var absoluteGrimoire = GetOrCreateItem("Grimoire Absolu", ItemType.Accessoire, Rarity.Admin, "Contient un savoir qu'aucun mortel ne devrait lire.", new StatBlock(0, 150, 0, 0, 200, 0), isObtainable: false);

        await db.SaveChangesAsync(ct);

        var recipesByName = await db.Recipes.Select(r => r.Name).ToHashSetAsync(ct);

        void AddRecipe(string name, ProfessionType profession, int resultItemId, int resultQuantity, int requiredLevel, params (int ItemId, int Quantity)[] ingredients)
        {
            if (!recipesByName.Add(name))
            {
                return;
            }

            db.Recipes.Add(new RecipeEntity
            {
                Name = name,
                Profession = profession,
                RequiredLevel = requiredLevel,
                ResultItemId = resultItemId,
                ResultQuantity = resultQuantity,
                Ingredients = ingredients.Select(i => new RecipeIngredientEntity { Id = Guid.NewGuid(), ItemId = i.ItemId, Quantity = i.Quantity }).ToList(),
            });
        }

        // Recettes des matériaux craftés (voir "recipes"). Note : "Épée de fer"/"Armure de fer"
        // existent déjà avec une recette différente et plus simple (voir ProfessionCatalogSeeder,
        // chaîne du tutoriel du forgeron) — volontairement laissées telles quelles ici pour ne pas
        // complexifier ce tutoriel, plutôt que de dupliquer un nom de recette déjà pris.
        AddRecipe("Lingot de Fer", ProfessionType.Forgeron, ironIngot.Id, 1, 1, (ironOre.Id, 5));
        AddRecipe("Lingot de Cuivre", ProfessionType.Forgeron, copperIngot.Id, 1, 1, (copperOre.Id, 5));
        AddRecipe("Lingot d'Argent", ProfessionType.Forgeron, silverIngot.Id, 1, 3, (silverOre.Id, 5));
        AddRecipe("Lingot d'Or", ProfessionType.Forgeron, goldIngot.Id, 1, 6, (goldOre.Id, 5));
        AddRecipe("Lingot de Mythril", ProfessionType.Forgeron, mythrilIngot.Id, 1, 10, (mythrilOre.Id, 5), (blueCrystal.Id, 2));
        AddRecipe("Lingot d'Aether", ProfessionType.Forgeron, aetherIngot.Id, 1, 16, (aetherOre.Id, 5), (aetherCrystal.Id, 2), (lightEssence.Id, 1));
        AddRecipe("Lingot Stellaire", ProfessionType.Forgeron, stellarIngot.Id, 1, 22, (stellarOre.Id, 5), (stellarFragment.Id, 2));

        var woodMaterial = itemsByName["Bois"];
        var ancientWoodMaterial = itemsByName["Bois ancien"];
        var runicStone = itemsByName["Pierre Runique"];

        AddRecipe("Bois Traité", ProfessionType.Artisan, treatedWood.Id, 1, 1, (woodMaterial.Id, 5));
        AddRecipe("Bois Enchanté", ProfessionType.Artisan, enchantedWood.Id, 1, 8, (ancientWoodMaterial.Id, 5), (windEssence.Id, 2));
        AddRecipe("Cuir Renforcé", ProfessionType.Artisan, reinforcedLeather.Id, 1, 5, (wolfPelt.Id, 5), (thickLeather.Id, 2));
        AddRecipe("Fil Magique", ProfessionType.Enchanteur, magicThread.Id, 1, 9, (spiderSilk.Id, 5), (spiritEssence.Id, 2));
        AddRecipe("Cristal Purifié", ProfessionType.Alchimiste, purifiedCrystal.Id, 1, 7, (blueCrystal.Id, 5), (lightEssence.Id, 1));
        AddRecipe("Pierre d'Amélioration", ProfessionType.Enchanteur, upgradeStone.Id, 1, 12, (runicStone.Id, 3), (purifiedCrystal.Id, 1));

        // Voir GDD — "weapon_recipes" (note : "Épée de Fer" existe déjà via ProfessionCatalogSeeder
        // avec sa recette du tutoriel, volontairement conservée telle quelle).
        // "Épée d'argent" a déjà une recette simple (4x Minerai d'argent, voir
        // ProfessionCatalogSeeder) — volontairement laissée telle quelle (même logique que
        // "Épée de fer"/"Armure de fer" plus haut), cette recette-ci n'est donc jamais ajoutée.
        AddRecipe("Épée d'argent", ProfessionType.Forgeron, silverSword.Id, 1, 4, (silverIngot.Id, 10), (enchantedWood.Id, 2));
        AddRecipe("Épée Royale", ProfessionType.Forgeron, royalSword.Id, 1, 12, (goldIngot.Id, 10), (purifiedCrystal.Id, 5), (ancientRune.Id, 2));
        AddRecipe("Épée Mythril", ProfessionType.Forgeron, mythrilSword.Id, 1, 18, (mythrilIngot.Id, 15), (purifiedCrystal.Id, 5), (lightEssence.Id, 2));
        AddRecipe("Épée Draconique", ProfessionType.Forgeron, draconicSword.Id, 1, 28, (aetherIngot.Id, 20), (dragonScale.Id, 10), (dragonHeart.Id, 5));
        AddRecipe("Arc Elfique", ProfessionType.Chasseur, elvenBow.Id, 1, 8, (enchantedWood.Id, 10), (magicThread.Id, 5));
        AddRecipe("Arc Stellaire", ProfessionType.Chasseur, stellarBow.Id, 1, 24, (enchantedWood.Id, 15), (stellarCrystal.Id, 10), (windEssence.Id, 5));
        AddRecipe("Lance Royale", ProfessionType.Forgeron, royalLance.Id, 1, 13, (goldIngot.Id, 15), (enchantedWood.Id, 5));
        AddRecipe("Marteau Titan", ProfessionType.Forgeron, titanHammer.Id, 1, 20, (mythrilIngot.Id, 25), (titanEye.Id, 3));
        AddRecipe("Dagues Jumelles", ProfessionType.Forgeron, twinDaggers.Id, 1, 6, (silverIngot.Id, 10), (purifiedCrystal.Id, 5));
        AddRecipe("Bâton d'Aether", ProfessionType.Enchanteur, aetherStaff.Id, 1, 25, (aetherIngot.Id, 15), (aetherCrystal.Id, 10), (spiritEssence.Id, 5));
        AddRecipe("Faux du Néant", ProfessionType.Enchanteur, voidScythe.Id, 1, 32, (stellarIngot.Id, 20), (timeFragment.Id, 10), (darknessEssence.Id, 5));

        // Voir GDD — "armor_recipes" (note : "Armure de Fer" existe déjà, voir plus haut).
        AddRecipe("Armure d'Argent", ProfessionType.Forgeron, silverArmor.Id, 1, 5, (silverIngot.Id, 35), (reinforcedLeather.Id, 10));
        AddRecipe("Armure Royale", ProfessionType.Forgeron, royalArmor.Id, 1, 14, (goldIngot.Id, 40), (purifiedCrystal.Id, 10));
        AddRecipe("Armure Mythril", ProfessionType.Forgeron, mythrilArmor.Id, 1, 21, (mythrilIngot.Id, 45), (purifiedCrystal.Id, 10), (ancientRune.Id, 5));
        AddRecipe("Armure Draconique", ProfessionType.Forgeron, draconicArmor.Id, 1, 30, (aetherIngot.Id, 50), (dragonScale.Id, 30), (dragonHeart.Id, 10));
        AddRecipe("Armure Stellaire", ProfessionType.Forgeron, stellarArmor.Id, 1, 34, (stellarIngot.Id, 50), (stellarFragment.Id, 20), (lightEssence.Id, 10));

        // Voir GDD — "accessory_recipes".
        AddRecipe("Anneau de Chance", ProfessionType.Enchanteur, luckRing.Id, 1, 6, (goldIngot.Id, 5), (purifiedCrystal.Id, 5));
        AddRecipe("Anneau du Mage", ProfessionType.Enchanteur, mageRing.Id, 1, 7, (silverIngot.Id, 5), (spiritEssence.Id, 5));
        AddRecipe("Anneau du Guerrier", ProfessionType.Enchanteur, warriorRing.Id, 1, 11, (mythrilIngot.Id, 5), (ancientRune.Id, 5));
        AddRecipe("Collier Royal", ProfessionType.Enchanteur, royalNecklace.Id, 1, 13, (goldIngot.Id, 10), (blueCrystal.Id, 5));
        AddRecipe("Collier Stellaire", ProfessionType.Enchanteur, stellarNecklace.Id, 1, 26, (stellarIngot.Id, 10), (stellarFragment.Id, 10));
        AddRecipe("Cape Astrale", ProfessionType.Enchanteur, astralCape.Id, 1, 17, (magicThread.Id, 20), (windEssence.Id, 10));
        AddRecipe("Cape Draconique", ProfessionType.Enchanteur, draconicCape.Id, 1, 27, (reinforcedLeather.Id, 20), (dragonScale.Id, 15));

        // Voir GDD — "legendary_recipes" (ingrédients mêlant objets craftés et matériaux de boss).
        AddRecipe("Couronne du Premier Roi", ProfessionType.Enchanteur, firstKingCrown.Id, 1, 40, (stellarArmor.Id, 1), (stellarFragment.Id, 20), (sacredRune.Id, 10));
        AddRecipe("Livre Interdit", ProfessionType.Enchanteur, forbiddenBook.Id, 1, 36, (aetherCrystal.Id, 20), (spiritEssence.Id, 20), (cursedRune.Id, 10));
        AddRecipe("Orbe de l'Infini", ProfessionType.Enchanteur, infinityOrb.Id, 1, 38, (stellarCrystal.Id, 25), (timeFragment.Id, 10));
        AddRecipe("Cœur d'Aether", ProfessionType.Enchanteur, aetherHeart.Id, 1, 37, (aetherCrystal.Id, 25), (lightEssence.Id, 20), (spiritEssence.Id, 10));
        AddRecipe("Éclat du Monde", ProfessionType.Enchanteur, worldShard.Id, 1, 42, (stellarFragment.Id, 50), (sacredRune.Id, 20), (timeFragment.Id, 10));

        // Voir GDD — "admin_recipes" : jamais réellement complétables (ingrédients IsObtainable=false), gardées pour la cohérence des données.
        AddRecipe("Épée du Créateur", ProfessionType.Enchanteur, creatorSword.Id, 1, 1, (draconicSword.Id, 1), (omegaStone.Id, 1), (divineEssence.Id, 5), (creationFragment.Id, 5), (adminCrystal.Id, 3));
        AddRecipe("Armure du Créateur", ProfessionType.Enchanteur, creatorArmor.Id, 1, 1, (stellarArmor.Id, 1), (primordialStar.Id, 1), (divineEssence.Id, 5), (creationFragment.Id, 5), (creatorHeart.Id, 1));
        AddRecipe("Couronne des Dieux", ProfessionType.Enchanteur, godsCrown.Id, 1, 1, (firstKingCrown.Id, 1), (divineEssence.Id, 10), (supremeRune.Id, 5), (godsKey.Id, 1));
        AddRecipe("Anneau de l'Infini", ProfessionType.Enchanteur, infinityRing.Id, 1, 1, (stellarNecklace.Id, 1), (originalSoul.Id, 5), (creatorCube.Id, 1));
        AddRecipe("Ailes de l'Architecte", ProfessionType.Enchanteur, architectWings.Id, 1, 1, (astralCape.Id, 1), (divineEssence.Id, 10), (primordialStar.Id, 5), (creatorHeart.Id, 1));
        AddRecipe("Grimoire Absolu", ProfessionType.Enchanteur, absoluteGrimoire.Id, 1, 1, (forbiddenBook.Id, 1), (creatorCube.Id, 1), (creationFragment.Id, 10), (supremeRune.Id, 10));

        await db.SaveChangesAsync(ct);
    }
}
