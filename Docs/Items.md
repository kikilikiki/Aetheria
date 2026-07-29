# Catalogue des objets

Généré depuis la base de données (voir `Server/Persistence/MonsterCatalogSeeder.cs`, `Server/Persistence/ProfessionCatalogSeeder.cs`, `Server/Persistence/EquipmentCatalogSeeder.cs`). Réservé aux admins/fondateur — sert de référence pour `/give <pseudo> <idObjet> <quantite>` (voir aussi le panel admin en jeu, F2).

Les identifiants correspondent à la base de développement au moment de la génération (30/07/2026) — ils sont stables tant que les seeders ne sont pas rejoués sur une base vidée, mais peuvent changer si de nouveaux objets sont insérés avant ceux listés ici. Regénérer ce fichier après tout ajout au catalogue.

Les potions de boost (`Potion d'expérience`, `Potion de fortune`, `Potion de chance`) se consomment avec `/use <idObjet>` (voir TemporaryBoostService/ConsumableService), pas en combat.

| Id | Nom | Type | Rareté | Obtention |
|---|---|---|---|---|
| 13 | Anneau de vitalité | Accessoire | Commun | Boutique (150 or) |
| 14 | Amulette de vitesse | Accessoire | PeuCommun | Boutique (180 or) |
| 117 | Anneau de Chance | Accessoire | Rare | Craft — métier Enchanteur niv.6 |
| 118 | Anneau du Mage | Accessoire | Rare | Craft — métier Enchanteur niv.7 |
| 120 | Collier Royal | Accessoire | Rare | Craft — métier Enchanteur niv.13 |
| 119 | Anneau du Guerrier | Accessoire | Epique | Craft — métier Enchanteur niv.11 |
| 122 | Cape Astrale | Accessoire | Epique | Craft — métier Enchanteur niv.17 |
| 123 | Cape Draconique | Accessoire | Legendaire | Craft — métier Enchanteur niv.27 |
| 121 | Collier Stellaire | Accessoire | Mythique | Craft — métier Enchanteur niv.26 |
| 125 | Livre Interdit | Accessoire | Mythique | Craft — métier Enchanteur niv.36 |
| 126 | Orbe de l'Infini | Accessoire | Mythique | Craft — métier Enchanteur niv.38 |
| 127 | Cœur d'Aether | Accessoire | Mythique | Craft — métier Enchanteur niv.37 |
| 128 | Éclat du Monde | Accessoire | Mythique | Craft — métier Enchanteur niv.42 |
| 132 | Anneau de l'Infini | Accessoire | Admin | **ADMIN UNIQUEMENT** — Contient un pouvoir sans limite. |
| 133 | Ailes de l'Architecte | Accessoire | Admin | **ADMIN UNIQUEMENT** — Permettent de voir la structure même du monde. |
| 134 | Grimoire Absolu | Accessoire | Admin | **ADMIN UNIQUEMENT** — Contient un savoir qu'aucun mortel ne devrait lire. |
| 3 | Épée courte | Arme | Commun | Boutique (120 or) |
| 20 | Épée de fer | Arme | Commun | Craft — métier Forgeron niv.1 |
| 8 | Hache de guerre | Arme | PeuCommun | Boutique (220 or) |
| 9 | Arc en bois renforcé | Arme | PeuCommun | Boutique (200 or) |
| 10 | Bâton d'apprenti | Arme | PeuCommun | Boutique (200 or) |
| 27 | Épée d'argent | Arme | PeuCommun | Craft — métier Forgeron niv.4 |
| 105 | Arc Elfique | Arme | PeuCommun | Craft — métier Chasseur niv.8 |
| 109 | Dagues Jumelles | Arme | PeuCommun | Craft — métier Forgeron niv.6 |
| 28 | Épée en or | Arme | Rare | Craft — métier Forgeron niv.7 |
| 102 | Épée Royale | Arme | Rare | Craft — métier Forgeron niv.12 |
| 107 | Lance Royale | Arme | Rare | Craft — métier Forgeron niv.13 |
| 103 | Épée Mythril | Arme | Epique | Craft — métier Forgeron niv.18 |
| 106 | Arc Stellaire | Arme | Epique | Craft — métier Chasseur niv.24 |
| 108 | Marteau Titan | Arme | Epique | Craft — métier Forgeron niv.20 |
| 104 | Épée Draconique | Arme | Legendaire | Craft — métier Forgeron niv.28 |
| 110 | Bâton d'Aether | Arme | Legendaire | Craft — métier Enchanteur niv.25 |
| 35 | Sceptre légendaire | Arme | Mythique | **ADMIN UNIQUEMENT** — Une arme d'un pouvoir inégalé, jamais lâchée par le hasard. |
| 111 | Faux du Néant | Arme | Mythique | Craft — métier Enchanteur niv.32 |
| 129 | Épée du Créateur | Arme | Admin | **ADMIN UNIQUEMENT** — Une lame qui n'aurait jamais dû exister. |
| 4 | Armure de cuir | Armure | Commun | Boutique (100 or) |
| 11 | Armure de plates | Armure | PeuCommun | Boutique (240 or) |
| 12 | Robe d'enchanteur | Armure | PeuCommun | Boutique (200 or) |
| 29 | Armure de fer | Armure | PeuCommun | Craft — métier Forgeron niv.2 |
| 112 | Armure d'Argent | Armure | PeuCommun | Craft — métier Forgeron niv.5 |
| 30 | Armure d'or | Armure | Rare | Craft — métier Forgeron niv.8 |
| 113 | Armure Royale | Armure | Rare | Craft — métier Forgeron niv.14 |
| 114 | Armure Mythril | Armure | Epique | Craft — métier Forgeron niv.21 |
| 115 | Armure Draconique | Armure | Legendaire | Craft — métier Forgeron niv.30 |
| 116 | Armure Stellaire | Armure | Mythique | Craft — métier Forgeron niv.34 |
| 124 | Couronne du Premier Roi | Armure | Mythique | Craft — métier Enchanteur niv.40 |
| 130 | Armure du Créateur | Armure | Admin | **ADMIN UNIQUEMENT** — Une armure forgée hors du temps. |
| 131 | Couronne des Dieux | Armure | Admin | **ADMIN UNIQUEMENT** — Symbole d'une autorité qui dépasse ce monde. |
| 2 | Potion de soin | Consommable | Commun | Boutique (25 or) |
| 6 | Antidote | Consommable | Commun | Boutique (20 or) |
| 5 | Grande potion de soin | Consommable | PeuCommun | Boutique (60 or) |
| 31 | Potion de soin supérieure | Consommable | PeuCommun | Boutique (25 or) |
| 16 | Potion d'expérience | Consommable | Rare | Boutique (150 or) |
| 17 | Potion de fortune | Consommable | Rare | Boutique (150 or) |
| 18 | Potion de chance | Consommable | Rare | Boutique (150 or) |
| 32 | Élixir de force | Consommable | Rare | Boutique (60 or) |
| 33 | Couronne du Fondateur | Cosmetique | Mythique | **ADMIN UNIQUEMENT** — Un objet honorifique remis à la main par un Fondateur. |
| 34 | Trophée du Champion | Cosmetique | Mythique | **ADMIN UNIQUEMENT** — Récompense honorifique du classement PvP, remise manuellement. |
| 1 | Carte de capture | ObjetDeCapture | Commun | Boutique (50 or) |
| 7 | Sphère de capture renforcée | ObjetDeCapture | PeuCommun | Boutique (80 or) |
| 15 | Blé | Ressource | Commun | Boutique (5 or) |
| 19 | Minerai de fer | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 25 | Herbe médicinale | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 26 | Bois ancien | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 36 | Bois | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 39 | Pierre | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 43 | Minerai de Cuivre | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 57 | Peau de Loup | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 58 | Cuir Épais | Ressource | Commun | Récolte (mine/champ) ou butin de combat |
| 89 | Lingot de Fer | Ressource | Commun | Craft — métier Forgeron niv.1 |
| 90 | Lingot de Cuivre | Ressource | Commun | Craft — métier Forgeron niv.1 |
| 96 | Bois Traité | Ressource | Commun | Craft — métier Artisan niv.1 |
| 21 | Minerai d'argent | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 40 | Pierre Runique | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 47 | Cristal Bleu | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 48 | Cristal Rouge | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 49 | Cristal Vert | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 50 | Cristal Violet | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 53 | Fleur Lunaire | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 54 | Fleur Solaire | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 55 | Champignon Lumineux | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 59 | Soie d'Araignée | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 63 | Essence de Feu | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 64 | Essence d'Eau | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 65 | Essence de Terre | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 66 | Essence de Vent | Ressource | PeuCommun | Récolte (mine/champ) ou butin de combat |
| 91 | Lingot d'Argent | Ressource | PeuCommun | Craft — métier Forgeron niv.3 |
| 97 | Bois Enchanté | Ressource | PeuCommun | Craft — métier Artisan niv.8 |
| 98 | Cuir Renforcé | Ressource | PeuCommun | Craft — métier Artisan niv.5 |
| 22 | Minerai d'or | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 23 | Cristal de mana | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 37 | Bois Sombre | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 41 | Obsidienne | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 56 | Racine Ancienne | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 61 | Corne de Minotaure | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 62 | Dent de Wyverne | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 67 | Essence de Lumière | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 68 | Essence des Ténèbres | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 69 | Essence Spirituelle | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 76 | Rune Antique | Ressource | Rare | Récolte (mine/champ) ou butin de combat |
| 92 | Lingot d'Or | Ressource | Rare | Craft — métier Forgeron niv.6 |
| 99 | Fil Magique | Ressource | Rare | Craft — métier Enchanteur niv.9 |
| 100 | Cristal Purifié | Ressource | Rare | Craft — métier Alchimiste niv.7 |
| 38 | Bois Sacré | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 42 | Marbre Blanc | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 44 | Minerai de Mythril | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 72 | Noyau de Golem | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 73 | Larme du Kraken | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 77 | Rune Sacrée | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 78 | Rune Maudite | Ressource | Epique | Récolte (mine/champ) ou butin de combat |
| 93 | Lingot de Mythril | Ressource | Epique | Craft — métier Forgeron niv.10 |
| 101 | Pierre d'Amélioration | Ressource | Epique | Craft — métier Enchanteur niv.12 |
| 24 | Écaille de dragon | Ressource | Legendaire | Récolte (mine/champ) ou butin de combat |
| 45 | Minerai d'Aether | Ressource | Legendaire | Récolte (mine/champ) ou butin de combat |
| 51 | Cristal d'Aether | Ressource | Legendaire | Récolte (mine/champ) ou butin de combat |
| 60 | Plume de Phénix | Ressource | Legendaire | Récolte (mine/champ) ou butin de combat |
| 70 | Cœur de Dragon | Ressource | Legendaire | Récolte (mine/champ) ou butin de combat |
| 71 | Œil du Titan | Ressource | Legendaire | Récolte (mine/champ) ou butin de combat |
| 94 | Lingot d'Aether | Ressource | Legendaire | Craft — métier Forgeron niv.16 |
| 46 | Minerai Stellaire | Ressource | Mythique | Récolte (mine/champ) ou butin de combat |
| 52 | Cristal Stellaire | Ressource | Mythique | Récolte (mine/champ) ou butin de combat |
| 74 | Fragment du Temps | Ressource | Mythique | Récolte (mine/champ) ou butin de combat |
| 75 | Fragment Stellaire | Ressource | Mythique | Récolte (mine/champ) ou butin de combat |
| 95 | Lingot Stellaire | Ressource | Mythique | Craft — métier Forgeron niv.22 |
| 79 | Essence Divine | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Essence Divine. |
| 80 | Pierre Oméga | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Pierre Oméga. |
| 81 | Fragment de Création | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Fragment de Création. |
| 82 | Cube du Créateur | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Cube du Créateur. |
| 83 | Cœur du Créateur | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Cœur du Créateur. |
| 84 | Rune Suprême | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Rune Suprême. |
| 85 | Étoile Primordiale | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Étoile Primordiale. |
| 86 | Âme Originelle | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Âme Originelle. |
| 87 | Cristal Administrateur | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Cristal Administrateur. |
| 88 | Clé des Dieux | Ressource | Admin | **ADMIN UNIQUEMENT** — Ressource brute : Clé des Dieux. |
