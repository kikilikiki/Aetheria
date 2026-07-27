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
        if (!await db.MonsterSpecies.AnyAsync(ct))
        {
            db.MonsterSpecies.AddRange(
                new MonsterSpeciesEntity
                {
                    Name = "Braisillon", Element = Element.Feu, BaseRarity = Rarity.Commun,
                    Habitat = "Royaume du Feu", Lore = "Petite salamandre qui couve des braises sous ses écailles.",
                    BaseStats = new StatBlock(30, 12, 8, 10, 6, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Racinelle", Element = Element.Nature, BaseRarity = Rarity.Commun,
                    Habitat = "Royaume de la Nature", Lore = "Esprit végétal né des vieilles forêts.",
                    BaseStats = new StatBlock(34, 8, 12, 6, 8, 10),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Glacillon", Element = Element.Glace, BaseRarity = Rarity.PeuCommun,
                    Habitat = "Royaume des Glaces", Lore = "Créature cristalline qui hiberne dans les congères.",
                    BaseStats = new StatBlock(28, 10, 14, 8, 10, 8),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Ombrelune", Element = Element.Ombre, BaseRarity = Rarity.Rare,
                    Habitat = "Royaume des Ombres", Lore = "N'apparaît qu'aux heures les plus sombres de la nuit.",
                    BaseStats = new StatBlock(26, 14, 8, 14, 12, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Dracaelith", Element = Element.Feu, BaseRarity = Rarity.Legendaire,
                    Habitat = "Donjon du Dragon", Lore = "Descendant présumé des dragons ancestraux.",
                    BaseStats = new StatBlock(60, 24, 20, 14, 18, 16),
                });
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
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
