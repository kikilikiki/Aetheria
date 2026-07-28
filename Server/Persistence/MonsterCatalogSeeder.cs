using Aetheria.Database.Context;
using Aetheria.Database.Entities;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Aetheria.Server.Persistence;

/// <summary>
/// Peuple un premier bestiaire (une espèce par royaume + une légendaire) et l'objet de
/// capture de base. Contenu de démarrage : à terme géré par le MonsterEditor plutôt que codé
/// ici (voir <c>Docs/GameDesign.md</c> — section Bestiaire).
/// </summary>
public static class MonsterCatalogSeeder
{
    public static async Task SeedAsync(AetheriaDbContext db, CancellationToken ct = default)
    {
        var existingNames = (await db.MonsterSpecies.Select(s => s.Name).ToListAsync(ct)).ToHashSet();

        {
            var wanted = new List<MonsterSpeciesEntity>
            {
                new MonsterSpeciesEntity
                {
                    Name = "Braisillon", Element = Element.Feu, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun,
                    Habitat = "Royaume du Feu", Lore = "Petite salamandre qui couve des braises sous ses écailles.",
                    BaseStats = new StatBlock(30, 12, 8, 10, 6, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Racinelle", Element = Element.Nature, Type = MonsterType.Soigneur, BaseRarity = Rarity.Commun,
                    Habitat = "Royaume de la Nature", Lore = "Esprit végétal né des vieilles forêts.",
                    BaseStats = new StatBlock(34, 8, 12, 6, 8, 10),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Aquapouss", Element = Element.Eau, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun,
                    Habitat = "Rives et étangs", Lore = "Petite créature gélatineuse qui ne quitte jamais l'eau bien longtemps.",
                    BaseStats = new StatBlock(32, 9, 11, 9, 7, 9),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Fulgurin", Element = Element.Foudre, Type = MonsterType.Archer, BaseRarity = Rarity.Commun,
                    Habitat = "Plaines orageuses", Lore = "Sa crinière crépite au moindre orage.",
                    BaseStats = new StatBlock(28, 12, 7, 13, 7, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Rocaillon", Element = Element.Terre, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun,
                    Habitat = "Collines rocheuses", Lore = "Une carapace de pierre qui durcit avec l'âge.",
                    BaseStats = new StatBlock(36, 9, 15, 5, 6, 8),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Zéphyrin", Element = Element.Air, Type = MonsterType.Archer, BaseRarity = Rarity.Commun,
                    Habitat = "Falaises et courants ascendants", Lore = "Plane des heures entières sans un battement d'aile.",
                    BaseStats = new StatBlock(26, 10, 7, 15, 8, 7),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Lumignon", Element = Element.Lumiere, Type = MonsterType.Soigneur, BaseRarity = Rarity.Commun,
                    Habitat = "Clairières ensoleillées", Lore = "Diffuse une lueur douce et rassurante la nuit venue.",
                    BaseStats = new StatBlock(30, 8, 9, 8, 12, 9),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Pénombrelle", Element = Element.Ombre, Type = MonsterType.Archer, BaseRarity = Rarity.Commun,
                    Habitat = "Sous-bois et greniers", Lore = "Se glisse entre les ombres sans jamais faire de bruit.",
                    BaseStats = new StatBlock(29, 11, 8, 11, 9, 7),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Bouboule", Element = Element.Neutre, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun,
                    Habitat = "Un peu partout", Lore = "Compagnon passe-partout, apprécié des débutants pour son caractère facile.",
                    BaseStats = new StatBlock(33, 10, 10, 10, 8, 8),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Frimousse", Element = Element.Glace, Type = MonsterType.Soigneur, BaseRarity = Rarity.Commun,
                    Habitat = "Sommets tempérés", Lore = "Boule de poils qui adore rouler dans la neige fraîche.",
                    BaseStats = new StatBlock(31, 8, 12, 7, 9, 10),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Glacillon", Element = Element.Glace, Type = MonsterType.Guerrier, BaseRarity = Rarity.PeuCommun,
                    Habitat = "Royaume des Glaces", Lore = "Créature cristalline qui hiberne dans les congères.",
                    BaseStats = new StatBlock(28, 10, 14, 8, 10, 8),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Ombrelune", Element = Element.Ombre, Type = MonsterType.Archer, BaseRarity = Rarity.Rare,
                    Habitat = "Royaume des Ombres", Lore = "N'apparaît qu'aux heures les plus sombres de la nuit.",
                    BaseStats = new StatBlock(26, 14, 8, 14, 12, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Dracaelith", Element = Element.Feu, Type = MonsterType.Guerrier, BaseRarity = Rarity.Legendaire,
                    Habitat = "Donjon du Dragon", Lore = "Descendant présumé des dragons ancestraux.",
                    BaseStats = new StatBlock(60, 24, 20, 14, 18, 16),
                },
            };

            var missing = wanted.Where(s => !existingNames.Contains(s.Name)).ToList();
            if (missing.Count > 0)
            {
                db.MonsterSpecies.AddRange(missing);
            }
        }

        if (!await db.Items.AnyAsync(i => i.ItemType == ItemType.ObjetDeCapture, ct))
        {
            db.Items.Add(new ItemEntity
            {
                Name = "Sphère de capture",
                Description = "Objet nécessaire pour capturer une créature suffisamment affaiblie.",
                ItemType = ItemType.ObjetDeCapture,
                Rarity = Rarity.Commun,
                IsStackable = true,
                MaxStackSize = 99,
                Price = 50,
            });
        }

        // Quelques objets de boutique de démarrage (voir GDD — bouton Boutique en jeu).
        var existingItemNames = (await db.Items.Select(i => i.Name).ToListAsync(ct)).ToHashSet();
        var wantedShopItems = new List<ItemEntity>
        {
            new()
            {
                Name = "Potion de soin", Description = "Restaure une partie des points de vie en combat.",
                ItemType = ItemType.Consommable, Rarity = Rarity.Commun, IsStackable = true, MaxStackSize = 20, Price = 25,
            },
            new()
            {
                Name = "Épée courte", Description = "Une lame simple mais fiable, pour débuter l'aventure.",
                ItemType = ItemType.Arme, Rarity = Rarity.Commun, IsStackable = false, MaxStackSize = 1, Price = 120,
                StatBonus = new StatBlock(0, 6, 0, 0, 0, 0),
            },
            new()
            {
                Name = "Armure de cuir", Description = "Une protection légère adaptée aux débutants.",
                ItemType = ItemType.Armure, Rarity = Rarity.Commun, IsStackable = false, MaxStackSize = 1, Price = 100,
                StatBonus = new StatBlock(0, 0, 6, 0, 0, 0),
            },
        };

        var missingShopItems = wantedShopItems.Where(i => !existingItemNames.Contains(i.Name)).ToList();
        if (missingShopItems.Count > 0)
        {
            db.Items.AddRange(missingShopItems);
        }

        await db.SaveChangesAsync(ct);
    }
}
