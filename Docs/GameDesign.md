# Aetheria — Document de conception (Game Design Document)

> Ce document décrit la vision complète du jeu. Il sert de référence de conception ;
> l'état d'avancement technique réel est suivi dans [README.md](README.md) (feuille de route).

## Présentation

Aetheria est un MMORPG tactique mélangeant plusieurs genres :

- MMORPG
- Tactical RPG
- Rogue-lite
- Collection Game
- Monster Capture
- Crafting
- PvP compétitif
- Guerre de royaumes

Inspirations principales :

- **Dofus** — combats tactiques sur grille, stratégie, progression
- **Pokémon** — capture, évolution, collection de créatures
- **Rogue-lite** — donjons procéduraux, exploration différente à chaque partie
- **MMORPG** — économie, guildes, métiers, territoires, classements

L'objectif est de créer un monde où chaque joueur possède une progression unique : son
personnage, ses monstres, ses métiers, ses collections, ses succès, son royaume, sa réputation.

## L'histoire

Le monde d'Aetheria est composé de plusieurs **Royaumes**. Chaque royaume possède une capitale
sécurisée, plusieurs villages, un château, des guildes, des marchands, des métiers, des zones
spéciales.

Contrairement aux MMORPG classiques, le monde extérieur n'est pas rempli de zones vides. En
dehors des villes, il existe principalement des **donjons**. Chaque donjon représente une
région entière.

Exemple :

```
Royaume du Nord
│
├── Capitale
├── Village
├── Hôtel des ventes
├── Forge
├── Guilde
├── Donjon des Araignées
├── Donjon des Glaces
├── Donjon du Dragon
├── Donjon des Ruines
└── Donjon Sans Fin
```

Chaque sortie de ville mène directement vers une aventure.

## Les Royaumes

Au début du jeu, chaque joueur choisit un royaume (Feu, Nature, Glaces, Ombres, ...). Chaque
royaume possède sa capitale, son histoire, ses créatures exclusives, ses ressources, ses bonus.

Les royaumes peuvent contrôler des mines, villages, donjons, forts, territoires rares. Chaque
semaine : les territoires peuvent changer de propriétaire, les joueurs participent aux guerres
de royaumes, la carte du monde évolue. Les actions des joueurs influencent réellement le monde.

## Les Monstres

Toutes les créatures du jeu sont capturables. Elles peuvent servir comme compagnons, montures,
invocations, ouvriers, ressources économiques ou partenaires de combat.

**Informations générales :** nom, élément, rareté, niveau, expérience, histoire, habitat.

**Statistiques :** vie, attaque, défense, vitesse, intelligence, résistance.

**Systèmes avancés :** compétences, évolution, talent passif, personnalité, affinités, variantes.

**Variantes :** Normal, Shiny, Alpha, Corrompu, Ancestral.

Le joueur peut posséder énormément de monstres, mais seulement **4 créatures peuvent participer
au combat**.

## Les Combats

Combats tour par tour, sur grille, tactiques.

Composition d'équipe :

- **Solo** : Joueur + 4 créatures
- **Coopération** : 4 joueurs, chacun avec sa créature

Les combats prennent en compte : éléments, terrain, obstacles, placement, portée, bonus de
position, combos, synergies.

## Les Donjons

Contenu principal du jeu. Générés procéduralement. Chaque étage peut contenir : monstres,
événements, énigmes, coffres, pièges, marchands, salles secrètes, autels, événements aléatoires.

Progression :

```
Étage 10  → Mini Boss
Étage 50  → Boss
Étage 100 → Boss légendaire
```

Chaque donjon possède des monstres exclusifs, objets uniques, récompenses rares, mécaniques
spécifiques.

## Capture des Créatures

La capture n'est pas automatique :

1. Combattre le monstre
2. L'affaiblir
3. Utiliser un objet adapté
4. Posséder assez de maîtrise

Les créatures rares nécessitent des conditions spéciales, objets rares, événements. Certaines
n'apparaissent que la nuit, pendant une saison, un événement, après une quête, ou lors d'une
invasion mondiale.

## Les Métiers

Exemple de chaîne de production :

```
Mineur → Minerai → Forgeron → Arme → Enchantement → Vente Hôtel des ventes
```

Chaque métier possède niveau, expérience, spécialisations, recettes, objets rares.

Métiers possibles : Mineur, Forgeron, Alchimiste, Chasseur, Cuisinier, Enchanteur, Artisan.
L'économie est entièrement contrôlée par les joueurs.

## Les Guildes

Les guildes peuvent posséder une ville, construire des bâtiments, améliorer leurs installations,
débloquer des bonus, lancer des guerres, réaliser des quêtes, débloquer des technologies,
participer aux classements.

## Statistiques Joueur

- **Combat** : niveau, XP totale, combats gagnés/perdus, boss vaincus, donjons terminés, étage
  maximum atteint, dégâts infligés/reçus, soins réalisés, coups critiques.
- **Exploration** : donjons visités, cartes découvertes, coffres ouverts, secrets trouvés,
  téléporteurs débloqués.
- **Monstres** : monstres capturés, espèces découvertes, évolutions réalisées, légendaires
  obtenus, shiny trouvés, temps avec chaque créature.
- **Économie** : argent gagné/dépensé, objets vendus/achetés, échanges, artisanat.
- **PvP** : victoires, défaites, série de victoires, rang, meilleur rang, saison.
- **Social** : guilde, amis, joueurs aidés, temps de jeu, messages envoyés.

## Succès (Achievements)

Des centaines de succès, catégorisés : Combat, Capture, Exploration, Métiers, Collection,
Social. Récompenses : titres, cosmétiques, montures, cadres de profil, icônes, objets exclusifs.

## Bestiaire

Chaque monstre possède une fiche complète : illustration, habitat, rareté, statistiques,
compétences, évolutions, variantes, histoire, nombre de captures, récompenses. Compléter le
bestiaire offre des récompenses.

## Collections

Monstres, boss, objets, armes, armures, montures, familiers, titres, succès, musiques,
apparences. Chaque collection terminée donne une récompense unique.

## Classements

Niveau, puissance, donjons, PvP, guildes, richesse, métiers, monstres capturés, succès, temps de
jeu, collections, boss vaincus. Portées : mondial, royaume, guilde, amis.

## Saisons

Durée : 3 à 4 mois. Chaque saison ajoute : nouveaux monstres, nouveaux donjons, nouveaux objets,
boss mondial, nouvelles quêtes, cosmétiques, passe saison gratuit/premium, nouveaux succès,
nouveaux classements.

## End Game

Donjons infinis, guerres de royaumes, boss mondiaux, PvP classé, tournois, élevage,
reproduction, chasse aux légendaires, artisanat avancé, événements mondiaux, raids de guildes.

## Architecture technique

Projet entièrement développé en **C#**, sans moteur externe (moteur maison). Objectifs :
meilleure maîtrise du projet, architecture évolutive, code partagé entre client et serveur.

## Launcher

Launcher Windows en C#. Fonctions : création de compte, connexion, sauvegarde progression,
installation du jeu, téléchargement des fichiers, mise à jour automatique, réparation des
fichiers, vérification de version, paramètres, lancement du jeu.

## Installateur

`AetheriaInstaller.exe` : installation du launcher, création de raccourci bureau, installation
des dépendances, configuration initiale.

## Système de compte

**Inscription :** pseudo, email, mot de passe. **Connexion :** email ou pseudo + mot de passe.
**Sécurité :** mot de passe hashé, sessions sécurisées, tokens.

Le compte sauvegarde : personnage, monstres, statistiques, succès, collections, inventaire,
progression.

## Base de données

Technologies : C#, Entity Framework Core, PostgreSQL ou SQL Server.

Tables principales : Users, Characters, Monsters, Inventory, Achievements, Statistics, Guilds,
Kingdoms, Items, Quests, Collections, Leaderboard.

## Réseau

Architecture Client ↔ Serveur. Systèmes : TCP, UDP si nécessaire, synchronisation temps réel,
système de packets, sauvegarde serveur.

## Objectif final

Créer un MMORPG complet où chaque joueur peut explorer, capturer, combattre, fabriquer,
commercer, rejoindre une guilde, participer aux guerres, compléter son bestiaire, obtenir des
succès, laisser son nom dans les classements. Aetheria doit être un monde vivant avec une
progression permanente.

## Addendum (2026-07-28) — précisions apportées par le porteur de projet

Ces points précisent/étendent les sections ci-dessus suite à des échanges ultérieurs ; en cas de
divergence, cet addendum fait foi sur les sections précédentes.

- **Création de personnage** : entièrement en jeu (pas dans le Launcher), scène animée avec
  caméra dynamique, personnalisation (visage, cheveux, couleurs, vêtements, accessoires),
  aperçu temps réel.
- **Visibilité globale** : tous les joueurs connectés sont visibles en temps réel dans le monde
  ouvert, pas seulement les membres du groupe.
- **Groupe (party)** : jusqu'à 4 joueurs, XP partagée entre tous les membres.
- **Butin de groupe** : 4 objets générés par combat/donjon, les joueurs cliquent pour réclamer ;
  en cas de choix identique entre plusieurs joueurs, attribution aléatoire parmi eux.
- **UI en jeu** : boutons Inventaire, Guilde, Boutique (Shop), Party — accessibles à tout moment.
- **Donjons dynamiques** : apparition aléatoire sur la carte, rotation toutes les heures
  (disparition/réapparition ailleurs). Exploration en couloir (ligne droite + embranchements),
  pas en monde ouvert libre.
- **Monstres sauvages hors donjon** : niveau calé sur le chef de groupe (pas la moyenne du groupe).
- **Villes** : plusieurs villes distinctes par royaume/emplacement (identité visuelle liée au
  lieu, pas au joueur), bâtiments visitables avec PNJ + meubles/décorations.
- **PvP Arènes classées** (nouveau) : matchmaking instancié, répartition des unités contrôlées
  selon la taille d'équipe — 1v1 : 1 joueur contrôle 4 unités ; 2v2 : 2 unités chacun ;
  3v3 : asymétrique (un joueur 2 unités, les deux autres 1 unité chacun) ; 4v4 : 1 unité chacun.
  Classement en ligues (Bronze/Argent/Or/...) basé sur un score ELO. Récompenses : titres,
  cosmétiques, monnaie spéciale PvP.
- **Guerre de royaumes** : sièges/escarmouches hebdomadaires pour mines/villages/forts/donjons
  rares ; le royaume contrôlant un territoire donne des bonus passifs à ses citoyens (récolte,
  créatures exclusives, taxes HDV locales).
- **PvP sauvage** : zones à risque hors des arènes instanciées, ressources/monstres rares,
  système de réputation/grade militaire pour les joueurs qui y combattent pour leur royaume.
