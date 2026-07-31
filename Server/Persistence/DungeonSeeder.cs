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
        //
        // Voir GDD/demande utilisateur — CORRECTION : "le niveau requis pour aller en donjon c'est
        // le niveau des monstres, pas celui du personnage" : MinLevel/MaxMonsterLevel décrivent
        // désormais le niveau des monstres rencontrés (étage 1 -> dernier étage, voir
        // DungeonMonsterLevel), plus un prérequis de niveau de personnage pour entrer.
        var wanted = new List<DungeonEntity>
        {
            new()
            {
                Name = "Donjon des Araignées", KingdomId = kingdomDuFeu.Id,
                Description = "Des galeries obscures tissées de toiles épaisses.", Seed = 1001,
                MinLevel = 1, MaxMonsterLevel = 8,
            },
            new()
            {
                Name = "Donjon des Glaces", KingdomId = kingdomsByType[KingdomType.Glaces].Id,
                Description = "Cavernes de glace éternelle, glissantes et mortelles.", Seed = 1002,
                MinLevel = 5, MaxMonsterLevel = 15,
            },
            new()
            {
                // Voir GDD/demande utilisateur — "donjon des dragons plus haut niveau" : monstres
                // bien plus costauds qu'à l'origine (l'antre d'un dragon ancestral ne devrait pas
                // se visiter en début de partie).
                Name = "Donjon du Dragon", KingdomId = kingdomDuFeu.Id,
                Description = "L'antre d'un dragon ancestral, très convoité.", Seed = 1003,
                MinLevel = 40, MaxMonsterLevel = 60,
            },
            new()
            {
                Name = "Donjon des Ruines", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Vestiges d'une civilisation oubliée.", Seed = 1004,
                MinLevel = 10, MaxMonsterLevel = 20,
            },
            new()
            {
                Name = "Donjon Sans Fin", KingdomId = kingdomsByType[KingdomType.Nature].Id,
                Description = "Aucun étage final connu à ce jour.", Seed = 1005,
                MinLevel = 1, MaxMonsterLevel = 30,
            },
            new()
            {
                Name = "Volcan Rugissant", KingdomId = kingdomDuFeu.Id,
                Description = "Un donjon hardcore : la lave elle-même semble vouloir votre perte.",
                Seed = 1006, MinLevel = 15, MaxMonsterLevel = 25, IsHardcore = true,
            },
            new()
            {
                Name = "Crypte du Néant", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Un donjon hardcore où même les ombres évitent de s'aventurer.",
                Seed = 1007, MinLevel = 20, MaxMonsterLevel = 35, IsHardcore = true,
            },
            // Voir GDD/demande utilisateur — "contenu end-game... donjons mythiques avec
            // modificateurs... boss impossibles" : réservé aux comptes ayant déjà tout complété
            // (voir EndGameService, CombatService.StartFromDungeonAsync).
            new()
            {
                Name = "Sanctuaire Ultime", KingdomId = kingdomDuFeu.Id,
                Description = "Un donjon mythique, hors du temps, réservé à ceux qui ont déjà tout accompli.",
                Seed = 1008, MinLevel = 1, MaxMonsterLevel = 1, IsMythic = true,
                MythicModifierDescription = "Modificateur : statistiques des créatures rencontrées multipliées par 3.",
            },
            // Voir GDD/demande utilisateur — "ajoute une zone réservée aux personnages niveau
            // 100+" : donjon exclusif de haut niveau, statistiques majorées (réutilise le
            // multiplicateur "hardcore" existant plutôt qu'un quatrième palier de difficulté).
            new()
            {
                Name = "Terres Interdites", KingdomId = kingdomDuFeu.Id,
                Description = "Un territoire hors des cartes, où seuls les plus expérimentés s'aventurent.",
                Seed = 1009, MinLevel = 100, MaxMonsterLevel = 130, IsHardcore = true,
            },
            // Voir GDD/demande utilisateur — "ajoute 5 nouveaux dongon avec des niveaux
            // personnalisé" : couvre la courbe de niveau intermédiaire, entre les donjons de
            // début de partie ci-dessus et Terres Interdites/le donjon 150+ ci-dessous.
            new()
            {
                Name = "Donjon des Sables", KingdomId = kingdomsByType[KingdomType.Nature].Id,
                Description = "Des dunes mouvantes qui engloutissent lentement tout ce qui s'y aventure.",
                Seed = 1010, MinLevel = 25, MaxMonsterLevel = 45,
            },
            new()
            {
                Name = "Antre du Kraken", KingdomId = kingdomsByType[KingdomType.Glaces].Id,
                Description = "Des grottes englouties où rôde une créature des profondeurs.",
                Seed = 1011, MinLevel = 50, MaxMonsterLevel = 70,
            },
            new()
            {
                Name = "Nid des Griffons", KingdomId = kingdomDuFeu.Id,
                Description = "Des pics escarpés où nichent des créatures ailées agressives.",
                Seed = 1012, MinLevel = 70, MaxMonsterLevel = 90,
            },
            new()
            {
                Name = "Labyrinthe Oublié", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Un dédale sans fin apparent, où les murs semblent parfois bouger.",
                Seed = 1013, MinLevel = 35, MaxMonsterLevel = 55,
            },
            new()
            {
                Name = "Citadelle des Ombres", KingdomId = kingdomsByType[KingdomType.Ombres].Id,
                Description = "Une forteresse déchue, plongée dans une obscurité permanente.",
                Seed = 1014, MinLevel = 90, MaxMonsterLevel = 110,
            },
            // Voir GDD/demande utilisateur — "ajoute un dongon avec les monstres niv 150+" :
            // niveau plafond (voir MonsterProgressionService.MaxLevel), gauntlet de fin de jeu.
            new()
            {
                Name = "Abysse Sans Nom", KingdomId = kingdomsByType[KingdomType.Nature].Id,
                Description = "Un gouffre au fond invisible, où seules les créatures les plus puissantes survivent.",
                Seed = 1015, MinLevel = 150, MaxMonsterLevel = 150,
            },
        };

        var existingByName = await db.Dungeons.ToDictionaryAsync(d => d.Name, ct);
        var missing = wanted.Where(d => !existingByName.ContainsKey(d.Name)).ToList();
        if (missing.Count > 0)
        {
            db.Dungeons.AddRange(missing);
        }

        // Voir GDD/demande utilisateur — "donjon des dragons plus haut niveau" : synchronise les
        // niveaux/difficulté d'un donjon déjà seedé sur une base existante (sinon un changement
        // ci-dessus ne s'appliquerait jamais aux serveurs dev/prod déjà lancés une première fois).
        var changed = false;
        foreach (var wantedDungeon in wanted)
        {
            if (!existingByName.TryGetValue(wantedDungeon.Name, out var existing))
            {
                continue;
            }

            if (existing.MinLevel != wantedDungeon.MinLevel || existing.MaxMonsterLevel != wantedDungeon.MaxMonsterLevel
                || existing.IsHardcore != wantedDungeon.IsHardcore || existing.IsMythic != wantedDungeon.IsMythic)
            {
                existing.MinLevel = wantedDungeon.MinLevel;
                existing.MaxMonsterLevel = wantedDungeon.MaxMonsterLevel;
                existing.IsHardcore = wantedDungeon.IsHardcore;
                existing.IsMythic = wantedDungeon.IsMythic;
                changed = true;
            }
        }

        if (missing.Count > 0 || changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
