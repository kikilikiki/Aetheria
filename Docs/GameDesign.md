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

## Addendum (2026-08-26) — précisions issues de l'implémentation de `Docs/Idees.md`

Voir `Docs/Idees-Realisations.md` pour le détail technique complet. Points qui affinent la
conception ci-dessus :

- **Rôles de combat (types de monstres)** : chacun des 9 rôles (Guerrier, Archer, Soigneur, Tank,
  Mage, Assassin, Support, Invocateur, Berserker) a désormais une capacité spéciale/ultime propre
  (voir section Les Combats) — plus de "coup puissant" générique par défaut. Tank encaisse et se
  soigne d'une partie des dégâts infligés, Assassin exécute plus fort une cible affaiblie,
  Berserker frappe plus fort à mesure que ses PV baissent, Support renforce la prochaine attaque
  d'un allié, Invocateur frappe en petite zone.
- **Échange entre joueurs** : la contrepartie demandée au joueur ciblé peut désormais être une de
  ses propres créatures (en plus/à la place d'or), pas seulement de l'or comme prévu initialement.
- **Arènes classées** : un groupe entier peut désormais rejoindre la file comme un seul bloc
  d'équipe (garantit de rester ensemble), et deux personnages du même compte ne peuvent plus se
  retrouver appairés l'un contre l'autre.
- **Donjons** : les salles Piège/Énigme/Événement/Salle secrète ont un effet mécanique réel
  (perte/gain d'or selon le cas, bonus XP), pas seulement du texte d'ambiance — seuls
  Marchand/Autel restent narratifs pour l'instant. Un matériau de boss thématique est désormais
  garanti à la victoire d'une salle Boss/Boss légendaire, en plus du butin aléatoire.
- **Modération** : un bannissement de compte déconnecte désormais immédiatement toutes ses
  sessions actives (auparavant l'effet n'était visible qu'à la reconnexion).
- **Outils internes** (MonsterEditor/MapEditor) : protégés par une authentification admin/
  fondateur, comme l'AdminPanel — jusqu'ici ouverts à quiconque pouvait atteindre le serveur.

## Addendum (2026-08-27) — idées précédemment "trop grosses", maintenant implémentées

Voir `Docs/Idees-Realisations.md` (mise à jour du 2026-08-27) pour le détail technique complet.

- **Arbre de talents** : un seul arbre partagé de 9 nœuds (pas un arbre par espèce) — chaque
  montée de niveau d'une créature octroie 1 point, dépensé pour débloquer des nœuds donnant des
  bonus en pourcentage sur les stats de combat (PV/Attaque/Défense/Vitesse), appliqués avant le
  bonus plat de l'équipement.
- **PvP sauvage** : concrétisé comme une file d'attente déclenchée quand le joueur se trouve en
  zone à risque (loin de la capitale), **pas une attaque directe ou une embuscade** — choix de
  conception délibéré tant qu'aucun système de consentement/notification n'existe, pour ne pas
  ouvrir de grief non consenti. Une victoire octroie un point de réputation militaire ; 6 grades
  successifs, affichés dans le panneau dédié.
- **Îles volantes/aquatiques** : accessibles comme avant via une monture adaptée, mais mènent
  maintenant à une vraie carte dédiée (toujours sur la grille 50x50 existante, le moteur n'a
  toujours pas de notion d'élévation/eau traversable) plutôt qu'à un simple succès caché.
- **Quêtes tutoriel** : la chaîne reste courte et linéaire dans l'ensemble, mais se termine
  désormais par un unique embranchement à deux choix ("voie du guerrier" orientée combat, "voie
  du marchand" orientée commerce) — pas un arbre de dialogue complet, un embranchement ponctuel
  comme prévu.
- **Intérieurs de bâtiments** : rendus en isométrique (même style que l'extérieur) au lieu d'un
  écran à plat — l'intérieur des donjons (salle rectangulaire avec portes) garde son style propre,
  volontairement pas unifié pour éviter de retoucher son système de déplacement.
- **Image de profil** : visible pour de vrai dans le Launcher et l'AdminPanel désormais, pas
  seulement stockée côté serveur.
- **Toujours pas de vrais sprites/textures** dans le moteur (aucun asset produit) : `Docs/Image/`
  contient à la place des maquettes générées par code pour donner une direction visuelle sans
  engager de vrais assets de production.
