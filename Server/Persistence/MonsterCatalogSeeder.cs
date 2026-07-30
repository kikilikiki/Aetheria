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
                // Voir GDD/demande utilisateur — "10 choix de starter, pas 18" : IsStarter=true
                // réservé à ces 10 espèces d'origine, le bestiaire étendu (H40) ajoute d'autres
                // espèces Commun mais qui ne doivent apparaître qu'en rencontre sauvage.
                new MonsterSpeciesEntity
                {
                    Name = "Braisillon", Element = Element.Feu, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Royaume du Feu", Lore = "Petite salamandre qui couve des braises sous ses écailles.",
                    BaseStats = new StatBlock(30, 12, 8, 10, 6, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Racinelle", Element = Element.Nature, Type = MonsterType.Soigneur, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Royaume de la Nature", Lore = "Esprit végétal né des vieilles forêts.",
                    BaseStats = new StatBlock(34, 8, 12, 6, 8, 10),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Aquapouss", Element = Element.Eau, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Rives et étangs", Lore = "Petite créature gélatineuse qui ne quitte jamais l'eau bien longtemps.",
                    BaseStats = new StatBlock(32, 9, 11, 9, 7, 9),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Fulgurin", Element = Element.Foudre, Type = MonsterType.Archer, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Plaines orageuses", Lore = "Sa crinière crépite au moindre orage.",
                    BaseStats = new StatBlock(28, 12, 7, 13, 7, 6),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Rocaillon", Element = Element.Terre, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Collines rocheuses", Lore = "Une carapace de pierre qui durcit avec l'âge.",
                    BaseStats = new StatBlock(36, 9, 15, 5, 6, 8),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Zéphyrin", Element = Element.Air, Type = MonsterType.Archer, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Falaises et courants ascendants", Lore = "Plane des heures entières sans un battement d'aile.",
                    BaseStats = new StatBlock(26, 10, 7, 15, 8, 7),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Lumignon", Element = Element.Lumiere, Type = MonsterType.Soigneur, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Clairières ensoleillées", Lore = "Diffuse une lueur douce et rassurante la nuit venue.",
                    BaseStats = new StatBlock(30, 8, 9, 8, 12, 9),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Pénombrelle", Element = Element.Ombre, Type = MonsterType.Archer, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Sous-bois et greniers", Lore = "Se glisse entre les ombres sans jamais faire de bruit.",
                    BaseStats = new StatBlock(29, 11, 8, 11, 9, 7),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Bouboule", Element = Element.Neutre, Type = MonsterType.Guerrier, BaseRarity = Rarity.Commun, IsStarter = true,
                    Habitat = "Un peu partout", Lore = "Compagnon passe-partout, apprécié des débutants pour son caractère facile.",
                    BaseStats = new StatBlock(33, 10, 10, 10, 8, 8),
                },
                new MonsterSpeciesEntity
                {
                    Name = "Frimousse", Element = Element.Glace, Type = MonsterType.Soigneur, BaseRarity = Rarity.Commun, IsStarter = true,
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

            // Voir GDD/demande utilisateur — bestiaire étendu (rôles/types/raretés élargis, voir
            // Shared/Enums). Stats identiques au sein d'un même palier de rareté plutôt
            // qu'individuellement équilibrées par espèce (voir Docs/README.md pour cette limite
            // assumée) — seuls l'élément et le rôle varient d'une espèce à l'autre.
            var commonStats = new StatBlock(30, 9, 7, 9, 6, 6);
            var uncommonStats = new StatBlock(42, 12, 10, 11, 8, 8);
            var rareStats = new StatBlock(58, 16, 13, 13, 11, 11);
            var epicStats = new StatBlock(78, 21, 17, 16, 15, 15);
            var legendaryStats = new StatBlock(105, 27, 22, 19, 20, 20);
            var mythicStats = new StatBlock(140, 34, 28, 22, 26, 26);
            var ancestralStats = new StatBlock(185, 42, 35, 26, 33, 33);
            var divineStats = new StatBlock(240, 52, 44, 30, 42, 42);
            var adminStats = new StatBlock(999, 150, 120, 80, 120, 120);

            MonsterSpeciesEntity Beast(string name, Element element, MonsterType role, Rarity rarity, StatBlock stats, string habitat, string lore) => new()
            {
                Name = name, Element = element, Type = role, BaseRarity = rarity, Habitat = habitat, Lore = lore, BaseStats = stats,
            };

            var extendedBestiary = new List<MonsterSpeciesEntity>
            {
                // Communs
                Beast("Slimo", Element.Nature, MonsterType.Tank, Rarity.Commun, commonStats, "Marécages", "Une gelée verdâtre étonnamment coriace."),
                Beast("Flambino", Element.Feu, MonsterType.Guerrier, Rarity.Commun, commonStats, "Volcans endormis", "Toujours prêt à en découdre, quitte à se brûler lui-même."),
                Beast("Aquili", Element.Eau, MonsterType.Soigneur, Rarity.Commun, commonStats, "Rivières calmes", "Apaise ses compagnons d'un simple contact humide."),
                Beast("Voltik", Element.Foudre, MonsterType.Archer, Rarity.Commun, commonStats, "Plaines orageuses", "Décoche ses attaques à la vitesse de l'éclair."),
                Beast("Roclin", Element.Terre, MonsterType.Tank, Rarity.Commun, commonStats, "Collines rocheuses", "Une carapace de pierre qui encaisse sans broncher."),
                Beast("Corbaciel", Element.Air, MonsterType.Archer, Rarity.Commun, commonStats, "Falaises", "Repère sa cible bien avant qu'elle ne le voie."),
                Beast("Champi", Element.Poison, MonsterType.Support, Rarity.Commun, commonStats, "Sous-bois humides", "Ses spores soutiennent ses alliés à distance."),
                Beast("Flocon", Element.Glace, MonsterType.Mage, Rarity.Commun, commonStats, "Sommets tempérés", "Canalise le froid environnant en petites bourrasques."),

                // Peu Communs
                Beast("Loup Cendré", Element.Ombre, MonsterType.Assassin, Rarity.PeuCommun, uncommonStats, "Forêts calcinées", "Chasse en silence dans les cendres encore tièdes."),
                Beast("Dryade", Element.Nature, MonsterType.Soigneur, Rarity.PeuCommun, uncommonStats, "Bosquets sacrés", "Veille sur la forêt et ceux qui la respectent."),
                Beast("Golem Rocheux", Element.Terre, MonsterType.Tank, Rarity.PeuCommun, uncommonStats, "Carrières abandonnées", "Une sentinelle de pierre que rien ne fait plier."),
                Beast("Salamandre", Element.Feu, MonsterType.Mage, Rarity.PeuCommun, uncommonStats, "Failles volcaniques", "Manipule les flammes avec une précision surprenante."),
                Beast("Harpie", Element.Air, MonsterType.Archer, Rarity.PeuCommun, uncommonStats, "Pics escarpés", "Fond sur ses proies depuis les hauteurs."),
                Beast("Serpent Marin", Element.Eau, MonsterType.Guerrier, Rarity.PeuCommun, uncommonStats, "Récifs profonds", "Un prédateur redoutable dans les eaux troubles."),
                Beast("Fantôme", Element.Spectre, MonsterType.Controleur, Rarity.PeuCommun, uncommonStats, "Ruines oubliées", "Brouille l'esprit de quiconque s'aventure trop près."),
                Beast("Scarabée Doré", Element.Metal, MonsterType.Tank, Rarity.PeuCommun, uncommonStats, "Déserts anciens", "Sa carapace métallique brille sous le soleil."),

                // Rares
                Beast("Phénix", Element.Feu, MonsterType.Soigneur, Rarity.Rare, rareStats, "Pics enflammés", "Renaît de ses cendres, au sens propre comme au figuré."),
                Beast("Basilic", Element.Poison, MonsterType.Assassin, Rarity.Rare, rareStats, "Cavernes toxiques", "Son regard seul suffit à paralyser d'effroi."),
                Beast("Yéti", Element.Glace, MonsterType.Tank, Rarity.Rare, rareStats, "Sommets enneigés", "Une montagne de fourrure et de glace vivante."),
                Beast("Griffon", Element.Air, MonsterType.Guerrier, Rarity.Rare, rareStats, "Pics escarpés", "Moitié aigle, moitié lion, entièrement redoutable."),
                Beast("Kraken", Element.Eau, MonsterType.Mage, Rarity.Rare, rareStats, "Abysses", "Ses tentacules dissimulent une intelligence aiguisée."),
                Beast("Chevalier Spectral", Element.Spectre, MonsterType.Guerrier, Rarity.Rare, rareStats, "Champs de bataille oubliés", "Continue de monter la garde bien après sa mort."),
                Beast("Cerf Sacré", Element.Lumiere, MonsterType.Support, Rarity.Rare, rareStats, "Clairières bénies", "Sa présence seule rassure et guérit ses alliés."),
                Beast("Golem de Cristal", Element.Cristal, MonsterType.Tank, Rarity.Rare, rareStats, "Grottes scintillantes", "Chaque coup porté résonne comme du verre."),

                // Épiques
                Beast("Dragon Rouge", Element.Feu, MonsterType.Berserker, Rarity.Epique, epicStats, "Cratères actifs", "Sa rage ne s'éteint jamais, tout comme ses flammes."),
                Beast("Dragon Bleu", Element.Eau, MonsterType.Mage, Rarity.Epique, epicStats, "Lagons profonds", "Maîtrise les courants comme une extension de lui-même."),
                Beast("Dragon Vert", Element.Nature, MonsterType.Tank, Rarity.Epique, epicStats, "Jungles primitives", "Une forteresse vivante recouverte d'écailles végétales."),
                Beast("Ange Gardien", Element.Lumiere, MonsterType.Soigneur, Rarity.Epique, epicStats, "Sanctuaires célestes", "Veille sur les âmes égarées avec une patience infinie."),
                Beast("Démon Abyssal", Element.Ombre, MonsterType.Berserker, Rarity.Epique, epicStats, "Failles abyssales", "Son fureur grandit à mesure que le combat s'éternise."),
                Beast("Titan de Pierre", Element.Terre, MonsterType.Tank, Rarity.Epique, epicStats, "Montagnes ancestrales", "Certains disent qu'il EST la montagne."),
                Beast("Liche", Element.Arcane, MonsterType.Mage, Rarity.Epique, epicStats, "Cryptes maudites", "A échangé sa mortalité contre un savoir interdit."),
                Beast("Reine Araignée", Element.Poison, MonsterType.Invocateur, Rarity.Epique, epicStats, "Antres tissées", "Ne combat jamais seule bien longtemps."),

                // Légendaires
                Beast("Aetherion", Element.Arcane, MonsterType.Mage, Rarity.Legendaire, legendaryStats, "Failles de l'Aether", "Un être fait de pure énergie arcanique."),
                Beast("Solarys", Element.Lumiere, MonsterType.Guerrier, Rarity.Legendaire, legendaryStats, "Temple du Soleil", "Porte la lumière comme une arme et un bouclier."),
                Beast("Noctyss", Element.Ombre, MonsterType.Assassin, Rarity.Legendaire, legendaryStats, "Voile des Ombres", "Frappe une seule fois — cela suffit toujours."),
                Beast("Leviathor", Element.Eau, MonsterType.Tank, Rarity.Legendaire, legendaryStats, "Fosses océaniques", "Les marins racontent son passage depuis des siècles."),
                Beast("Ignis Rex", Element.Feu, MonsterType.Berserker, Rarity.Legendaire, legendaryStats, "Couronne de magma", "Régnait sur les volcans bien avant les royaumes actuels."),
                Beast("Tempestia", Element.Air, MonsterType.Support, Rarity.Legendaire, legendaryStats, "Yeux de la tempête", "Chaque bourrasque qu'elle soulève protège ses alliés."),

                // Mythiques
                Beast("Chronos", Element.Arcane, MonsterType.Controleur, Rarity.Mythique, mythicStats, "Hors du temps", "On dit qu'il a déjà vu la fin de ce combat."),
                Beast("Gaia", Element.Terre, MonsterType.Tank, Rarity.Mythique, mythicStats, "Cœur du monde", "La terre elle-même semble répondre à sa volonté."),
                Beast("Zephyria", Element.Air, MonsterType.Archer, Rarity.Mythique, mythicStats, "Sommets invisibles", "Ses flèches voyagent plus vite que le vent lui-même."),
                Beast("Raijin", Element.Foudre, MonsterType.Mage, Rarity.Mythique, mythicStats, "Nuages d'orage éternels", "Chaque éclair du ciel pourrait être le sien."),
                Beast("Nerea", Element.Eau, MonsterType.Soigneur, Rarity.Mythique, mythicStats, "Sources primordiales", "Ses larmes auraient le pouvoir de guérir n'importe quelle blessure."),

                // Ancestraux
                Beast("Bahamut", Element.Dragon, MonsterType.Berserker, Rarity.Ancestral, ancestralStats, "Origine des dragons", "Le premier dragon, dit-on, et le dernier à tomber."),
                Beast("Célestion", Element.Lumiere, MonsterType.Support, Rarity.Ancestral, ancestralStats, "Voûte céleste", "Une constellation vivante veillant sur le monde."),
                Beast("Umbragon", Element.Ombre, MonsterType.Assassin, Rarity.Ancestral, ancestralStats, "Néant primordial", "Existe autant dans l'ombre que dans le silence."),
                Beast("Atlas", Element.Terre, MonsterType.Tank, Rarity.Ancestral, ancestralStats, "Piliers du monde", "Porterait le poids du monde sur ses épaules, littéralement."),

                // Divins
                Beast("Astrael", Element.Lumiere, MonsterType.Mage, Rarity.Divin, divineStats, "Au-delà des étoiles", "Sa simple présence réécrit les lois du combat."),
                Beast("Nyxara", Element.Ombre, MonsterType.Controleur, Rarity.Divin, divineStats, "Néant absolu", "Contrôle jusqu'au silence qui l'entoure."),
                Beast("Eonar", Element.Nature, MonsterType.Soigneur, Rarity.Divin, divineStats, "Origine de la vie", "On dit qu'elle a soigné le monde lui-même, autrefois."),

                // Admin (voir GDD — "IMPOSSIBLES À OBTENIR", jamais choisis par RarityForLevel/le tirage de donjon)
                Beast("Le Créateur", Element.Arcane, MonsterType.Mage, Rarity.Admin, adminStats, "En dehors du jeu", "Celui qui a écrit les règles peut aussi les briser."),
                Beast("L'Architecte", Element.Cristal, MonsterType.Support, Rarity.Admin, adminStats, "En dehors du jeu", "A conçu chaque pierre de ce monde, une à une."),
                Beast("Le Gardien du Code", Element.Metal, MonsterType.Tank, Rarity.Admin, adminStats, "En dehors du jeu", "Rien ne passe sans son autorisation."),
                Beast("Le Développeur", Element.Arcane, MonsterType.Invocateur, Rarity.Admin, adminStats, "En dehors du jeu", "Peut faire apparaître ou disparaître n'importe quoi."),
                Beast("L'Observateur", Element.Spectre, MonsterType.Controleur, Rarity.Admin, adminStats, "En dehors du jeu", "Voit tout, partout, tout le temps."),

                // Voir GDD/demande utilisateur — "ajoute des monstres que l'on peut avoir que en
                // reproduction" : jamais en rencontre naturelle (voir BreedingOnly ci-dessous),
                // seule la Couvée (voir BreedingService) peut en produire.
                new MonsterSpeciesEntity
                {
                    Name = "Chimèrion", Element = Element.Arcane, Type = MonsterType.Support, BaseRarity = Rarity.Epique,
                    Habitat = "Né en Couvée uniquement", Lore = "Un mélange improbable de deux lignées, jamais vu à l'état sauvage.",
                    BaseStats = epicStats, BreedingOnly = true,
                },
            };

            var missingExtended = extendedBestiary.Where(s => !existingNames.Contains(s.Name)).ToList();
            if (missingExtended.Count > 0)
            {
                db.MonsterSpecies.AddRange(missingExtended);
            }

            var missing = wanted.Where(s => !existingNames.Contains(s.Name)).ToList();
            if (missing.Count > 0)
            {
                db.MonsterSpecies.AddRange(missing);
            }

            // Sauvegarde intermédiaire : sur une base fraîchement créée, les espèces ci-dessus
            // n'existent encore que dans le suivi de changements EF Core (jamais interrogeables
            // via une requête tant qu'elles ne sont pas persistées) — sans ce SaveChanges, le
            // marquage "donjon uniquement" juste en dessous ne trouverait jamais rien au tout
            // premier démarrage sur une base vide.
            await db.SaveChangesAsync(ct);

            // Voir GDD/demande utilisateur — "ajoute des monstres que l'on peut avoir que en
            // donjon" : marque quelques espèces Rare/Légendaire déjà existantes comme exclusives
            // au donjon (mini-boss/boss, voir CombatService) plutôt que d'en inventer de
            // nouvelles — idempotent (revérifié à chaque démarrage, pas seulement à la création).
            var dungeonOnlyNames = new HashSet<string> { "Ombrelune", "Chevalier Spectral", "Dracaelith", "Tempestia" };
            var toFlag = await db.MonsterSpecies.Where(s => dungeonOnlyNames.Contains(s.Name) && !s.DungeonOnly).ToListAsync(ct);
            foreach (var species in toFlag)
            {
                species.DungeonOnly = true;
            }

            // Voir GDD/demande utilisateur — "Évolution des monstres" : quelques chaînes
            // d'évolution entre espèces déjà existantes (même élément, palier de rareté suivant),
            // à titre de démonstration du mécanisme (voir MonsterEvolutionService) — le reste du
            // bestiaire peut être configuré de la même façon depuis Aetheria.MonsterEditor.
            // Idempotent, revérifié à chaque démarrage comme le marquage DungeonOnly ci-dessus.
            (string From, string To, int Level)[] evolutionChains =
            [
                ("Braisillon", "Salamandre", 10),
                ("Racinelle", "Dryade", 10),
                ("Aquapouss", "Serpent Marin", 10),
            ];

            foreach (var (fromName, toName, level) in evolutionChains)
            {
                var from = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Name == fromName, ct);
                var to = await db.MonsterSpecies.FirstOrDefaultAsync(s => s.Name == toName, ct);
                if (from is not null && to is not null && (from.EvolvesIntoSpeciesId != to.Id || from.EvolutionLevel != level))
                {
                    from.EvolvesIntoSpeciesId = to.Id;
                    from.EvolutionLevel = level;
                }
            }
        }

        if (!await db.Items.AnyAsync(i => i.ItemType == ItemType.ObjetDeCapture, ct))
        {
            db.Items.Add(new ItemEntity
            {
                Name = "Carte de capture",
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
            // Voir GDD/demande utilisateur — "ajoute des item dans le shop" : élargit le catalogue
            // de départ (3 objets) avec des consommables/équipements de milieu de partie.
            new()
            {
                Name = "Grande potion de soin", Description = "Restaure une grande partie des points de vie en combat.",
                ItemType = ItemType.Consommable, Rarity = Rarity.PeuCommun, IsStackable = true, MaxStackSize = 20, Price = 60,
            },
            new()
            {
                Name = "Antidote", Description = "Neutralise les effets d'un poison.",
                ItemType = ItemType.Consommable, Rarity = Rarity.Commun, IsStackable = true, MaxStackSize = 20, Price = 20,
            },
            new()
            {
                Name = "Sphère de capture renforcée", Description = "Meilleures chances de capture qu'une sphère standard.",
                ItemType = ItemType.ObjetDeCapture, Rarity = Rarity.PeuCommun, IsStackable = true, MaxStackSize = 20, Price = 80,
            },
            new()
            {
                Name = "Hache de guerre", Description = "Une arme lourde qui favorise la puissance brute.",
                ItemType = ItemType.Arme, Rarity = Rarity.PeuCommun, IsStackable = false, MaxStackSize = 1, Price = 220,
                StatBonus = new StatBlock(0, 12, -2, -1, 0, 0),
            },
            new()
            {
                Name = "Arc en bois renforcé", Description = "Une arme de tir adaptée aux archers.",
                ItemType = ItemType.Arme, Rarity = Rarity.PeuCommun, IsStackable = false, MaxStackSize = 1, Price = 200,
                StatBonus = new StatBlock(0, 9, 0, 2, 0, 0),
            },
            new()
            {
                Name = "Bâton d'apprenti", Description = "Un bâton qui canalise l'intelligence de son porteur.",
                ItemType = ItemType.Arme, Rarity = Rarity.PeuCommun, IsStackable = false, MaxStackSize = 1, Price = 200,
                StatBonus = new StatBlock(0, 3, 0, 0, 9, 0),
            },
            new()
            {
                Name = "Armure de plates", Description = "Une lourde protection en acier.",
                ItemType = ItemType.Armure, Rarity = Rarity.PeuCommun, IsStackable = false, MaxStackSize = 1, Price = 240,
                StatBonus = new StatBlock(10, 0, 12, -2, 0, 0),
            },
            new()
            {
                Name = "Robe d'enchanteur", Description = "Un vêtement léger qui renforce la résistance magique.",
                ItemType = ItemType.Armure, Rarity = Rarity.PeuCommun, IsStackable = false, MaxStackSize = 1, Price = 200,
                StatBonus = new StatBlock(0, 0, 3, 0, 4, 6),
            },
            new()
            {
                Name = "Anneau de vitalité", Description = "Un anneau simple qui renforce l'endurance de la créature.",
                ItemType = ItemType.Accessoire, Rarity = Rarity.Commun, IsStackable = false, MaxStackSize = 1, Price = 150,
                StatBonus = new StatBlock(15, 0, 0, 0, 0, 0),
            },
            new()
            {
                Name = "Amulette de vitesse", Description = "Une amulette qui aiguise les réflexes.",
                ItemType = ItemType.Accessoire, Rarity = Rarity.PeuCommun, IsStackable = false, MaxStackSize = 1, Price = 180,
                StatBonus = new StatBlock(0, 0, 0, 6, 0, 0),
            },
            // Voir GDD/demande utilisateur — "ajoute des bâtiments dans les villes (mine, champs
            // etc) pour avoir des objets" : ressource récoltée au Champ (voir WorldMap, métier
            // Agriculteur), pendant du Minerai de fer pour la Mine.
            new()
            {
                Name = "Blé", Description = "Récolté aux champs — utilisé par les cuisiniers et alchimistes.",
                ItemType = ItemType.Ressource, Rarity = Rarity.Commun, IsStackable = true, MaxStackSize = 99, Price = 5,
            },
            // Voir GDD/demande utilisateur — "ajoute des consommables pour booster la luck l'xp la
            // money" : voir TemporaryBoostService/ConsumableService (/use <idObjet>).
            new()
            {
                Name = "Potion d'expérience", Description = "+50% d'expérience gagnée pendant 30 minutes. S'utilise avec /use.",
                ItemType = ItemType.Consommable, Rarity = Rarity.Rare, IsStackable = true, MaxStackSize = 20, Price = 150,
            },
            new()
            {
                Name = "Potion de fortune", Description = "+50% d'or gagné pendant 30 minutes. S'utilise avec /use.",
                ItemType = ItemType.Consommable, Rarity = Rarity.Rare, IsStackable = true, MaxStackSize = 20, Price = 150,
            },
            new()
            {
                Name = "Potion de chance", Description = "Réduit les malus de récolte hors territoire pendant 30 minutes. S'utilise avec /use.",
                ItemType = ItemType.Consommable, Rarity = Rarity.Rare, IsStackable = true, MaxStackSize = 20, Price = 150,
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
