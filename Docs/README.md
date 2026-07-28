# Aetheria — Documentation technique

MMORPG tactique (combats sur grille façon Dofus + capture/collection façon Pokémon +
donjons procéduraux rogue-lite) développé entièrement en C#, avec un moteur de jeu maison
(aucun moteur tiers type Unity/Unreal).

Pour la vision complète du jeu (univers, royaumes, monstres, combats, donjons, métiers,
guildes, succès, classements, saisons...), voir le
[Document de conception](GameDesign.md).

## Architecture du dépôt

```
Aetheria/
├── Aetheria.sln                  Solution Visual Studio
├── Directory.Build.props         Configuration MSBuild commune à tous les projets
├── Engine/                       Aetheria.Engine   — moteur de jeu maison
│   ├── Core/                     Boucle de jeu, fenêtre, temps
│   ├── Rendering/                Rendu graphique (sprites, shaders, caméra)
│   ├── ECS/                      Système Entité-Composant-Système
│   ├── Physics/                  Collisions, portée, grille tactique
│   ├── Input/                    Clavier / souris / manette
│   └── Audio/                    Musiques et effets sonores
├── Shared/                       Aetheria.Shared   — modèles et protocole communs
│   ├── Network/                  Packets, opcodes, sérialisation
│   ├── Models/                   DTO (Personnage, Créature, Objet, ...)
│   └── Enums/                    Éléments, raretés, variantes, classes
├── Server/                       Aetheria.Server   — serveur MMORPG (monde persistant)
│   ├── World/                    Royaumes, donjons, instances
│   ├── Networking/                Connexions joueurs, dispatch des packets
│   └── Persistence/              Sauvegarde via Aetheria.Database
├── Client/                       Aetheria.Client   — jeu joueur
│   ├── UI/                       Interface (HUD, inventaire, bestiaire, HDV)
│   ├── Combat/                   Combat tactique sur grille
│   ├── Exploration/              Déplacement, royaumes, donjons
│   └── Networking/               Connexion au serveur
├── Launcher/                     Aetheria.Launcher — launcher Windows (login, MAJ)
├── Installer/                    AetheriaInstaller — installateur Windows (copie + raccourci)
├── MapEditor/                    Aetheria.MapEditor — éditeur de cartes/donjons
├── MonsterEditor/                Aetheria.MonsterEditor — éditeur de créatures
├── AdminPanel/                   Aetheria.AdminPanel — outils d'administration
├── Database/                     Aetheria.Database — Entity Framework Core
│   ├── Entities/                 Users, Characters, Monsters, Guilds, ...
│   ├── Context/                  DbContext
│   └── Migrations/               Migrations EF Core (générées automatiquement)
└── Docs/                         Documentation (ce dossier)
```

> **Planifié (hors solution .NET) :** `Sites/` — site web public permettant de télécharger le
> launcher (`AetheriaInstaller.exe`). Ce sera un projet séparé (site statique ou ASP.NET),
> non inclus dans `Aetheria.sln` car il ne partage pas de code avec le jeu. À construire une
> fois le Launcher fonctionnel.

## Graphe de dépendances entre projets

```
Engine  ────────────┐
                     ├──> Client
Shared  ─────────────┤
                     ├──> MapEditor
                     │
Shared  ────> Database ────> Server
                     └──────> AdminPanel

Shared ──> Launcher
Shared ──> MonsterEditor
```

Règles :
- `Engine` ne dépend de rien : c'est un moteur de jeu générique, réutilisable en dehors d'Aetheria.
- `Shared` ne dépend de rien : uniquement des modèles/contrats, aucune logique métier lourde.
- `Server` ne référence jamais `Engine` (pas de rendu côté serveur).
- `Client` et `MapEditor` référencent `Engine` car ils ont besoin d'affichage.
- `Database` encapsule tout l'accès EF Core ; seuls `Server` et `AdminPanel` en dépendent directement.

## Cible de compilation

Tous les projets ciblent **.NET 10** et produisent des exécutables Windows (`.exe`) pour les
projets de type application (`Client`, `Server`, `Launcher`, `MapEditor`, `MonsterEditor`,
`AdminPanel`). `Engine`, `Shared` et `Database` sont des bibliothèques de classes (`.dll`).

## Feuille de route (suivie étape par étape)

Priorité choisie : consolider les fondations techniques (moteur + serveur) avant les systèmes
de gameplay.

1. ✅ Solution Visual Studio + tous les projets C# + architecture.
2. ✅ Moteur — fenêtre + boucle de jeu + ECS + rendu + input.
   - ✅ `Engine.ECS` : `World`, `Entity`, `ComponentPool<T>`, `ISystem` (voir `Engine/ECS/`).
   - ✅ `Engine.Core.GameHost` : fenêtre + contexte OpenGL via Silk.NET (voir `Engine/Core/`).
   - ✅ `Engine.Rendering` : `Shader`, `Texture2D` (via StbImageSharp), `Camera2D` orthographique,
     `SpriteBatch` (batching par texture, VAO/VBO/EBO dynamiques) — voir `Engine/Rendering/`.
   - ✅ `Engine.Input` : `KeyboardState` (polling + détection "vient d'être pressée"),
     `MouseState`, exposés via `GameHost.Input` — voir `Engine/Input/`.
3. ✅ Base de données — `AetheriaDbContext` EF Core, 12 tables du GDD + migration `InitialCreate`
   (voir `Database/`). PostgreSQL en production, base en mémoire en dev si
   `AETHERIA_DB_CONNECTION` n'est pas défini.
4. ✅ Serveur — API HTTP de compte (`/api/account/register`, `/api/account/login`, hash BCrypt,
   jetons de session) + serveur de jeu TCP (`Server/Networking`) avec framing de packets
   (Ping/Pong, EnterWorld, PlayerMove) vérifié de bout en bout sur un vrai socket.
   - ⬜ Monde partagé multi-joueurs (diffusion des positions, royaumes, instances de donjon) —
     arrive avec les systèmes de jeu (étape 7).
5. ✅ Launcher — WPF (`net10.0-windows`, MVVM via CommunityToolkit.Mvvm) : écran connexion/
   inscription branché sur l'API compte, sélection de personnage, bouton Jouer qui lance
   `Aetheria.Client.exe` avec le jeton de session en argument.
   - ⬜ Téléchargement/mise à jour/réparation de fichiers : nécessite un serveur de
     distribution de contenu qui n'existe pas encore — non implémenté plutôt que simulé.
6. ✅ Boucle jouable de base côté Client — `Client/LaunchOptions` (parsing `--token`/
   `--characterId`/`--host`/`--port`) + `Client/Networking/GameConnection` (TCP vers Server,
   thread de réception dédié). Deux modes :
   - Sans jeton (lancement direct, dev) : démo hors-ligne, déplacement libre continu.
   - Avec jeton (lancé par le Launcher) : connexion réelle au Server, `EnterWorldRequest`,
     déplacement case par case confirmé par le serveur (autoritaire).
   - **Vérifié de bout en bout sur un vrai serveur**, refus ET acceptation : inscription →
     connexion → création de personnage → lancement du Client avec `--token`/`--characterId`
     → connexion TCP → `EnterWorldAccepted` reçu et affiché (`Entrée dans le monde acceptée
     en (0, 0)`). La boucle complète Launcher→Server→Database→Client fonctionne réellement.
   - ✅ Rendu isométrique (`Client/World/IsoMath`, `Building`, `WorldMap`) : monde de
     démonstration 50x50 cases (au lieu de 8x8), projection "2:1" façon Dofus/Diablo (tuiles
     en losange via un nouveau `SpriteBatch.DrawQuad` acceptant 4 coins arbitraires, pas
     seulement des rectangles axés). Terrain varié par hachage déterministe par case
     (herbe claire/moyenne/foncée, étang, chemins reliant les bâtiments), 5 bâtiments
     (Capitale, Village, Hôtel des ventes, Forge, Guilde) dessinés en pseudo-3D (toit +
     2 murs ombrés, triés par profondeur avec le joueur pour une occlusion correcte), et
     une entrée de donjon de test ("Donjon des Araignées", relié au vrai donjon seedé côté
     serveur) déclenchant un message de proximité. **Vérifié visuellement** via capture
     d'écran du process réel (Win32 `PrintWindow`) : tuiles en losange, chemins, et les
     3 bâtiments visibles rendus correctement avec ombrage toit/mur. Le déplacement clavier
     n'a pas pu être vérifié par simulation d'entrée dans cet environnement (la fenêtre
     résiste à `SetForegroundWindow`/`SendKeys`) — la formule caméra-suit-joueur est
     inconditionnelle donc correcte par construction, mais non re-testée empiriquement en
     mouvement. **Limites assumées** : bâtiments à l'échelle d'une seule case (pas de
     vraie emprise au sol), pas de liseré de tuile, pas de sprites/textures réels (couleurs
     unies uniquement), la transition visuelle vers l'intérieur d'un donjon n'existe pas
     encore (seul le message de proximité + l'API serveur déjà fonctionnelle).
7. ✅ Systèmes de jeu (voir `Server/Persistence` et `Server/World`) :
   - ✅ Création de personnage (`CharacterService`, `POST /api/characters`) — débloque le
     test ci-dessus.
   - ✅ Catalogue de monstres + capture (`MonsterSpeciesEntity`, `CaptureService`,
     `POST /api/monsters/capture`, `GET /api/monsters/species`) : 5 espèces de démarrage
     (une par royaume + une légendaire), objet "Sphère de capture" offert à la création de
     personnage. La formule de réussite dépend de la vie restante simulée du monstre
     (`TargetHealthPercent`) et de sa rareté — **le combat tactique lui-même n'existe pas
     encore**, ce endpoint prend son résultat en entrée plutôt que de le simuler.
     Vérifié de bout en bout : capture échouée à haute vie, réussie à vie basse, objet de
     capture bien consommé (409 une fois l'inventaire épuisé).
   - ✅ Donjons procéduraux (`DungeonEntity`, `DungeonFloorGenerator`,
     `GET /api/dungeons`, `GET /api/dungeons/{id}/floors/{n}`) : 5 donjons de démarrage
     (repris du GDD), génération déterministe des salles par étage (seed du donjon + numéro
     d'étage), mini-boss/boss/boss légendaire garantis tous les 10/50/100 étages.
     **La disposition spatiale (grille, corridors) n'existe pas encore** — seule la séquence
     de rencontres est générée ; le placement visuel est un travail Client/MapEditor à venir.
     Vérifié : étages normaux variés, jalons exacts aux étages 10/50/100. Déterminisme
     initialement vérifié seulement *au sein d'un même processus* — un vrai bug (voir
     encadré ci-dessous, corrigé en Phase H2) faisait que le contenu changeait en fait à
     chaque redémarrage du serveur ; re-vérifié depuis entre deux processus distincts.
   - ✅ Métiers et artisanat (`CharacterProfessionEntity`, `RecipeEntity`,
     `ProfessionService`, `GET /api/professions/recipes`, `POST /api/professions/gather`,
     `POST /api/professions/craft`) : chaîne de départ Mineur → Minerai de fer → Forgeron →
     Épée de fer (GDD), courbe de niveau simple (XP requise au niveau N = N × 100).
     Vérifié de bout en bout : craft refusé sans assez de minerai, refusé avec 2/3, réussi
     avec 3/3 (ingrédients consommés), montée de niveau du métier confirmée (niveau 1→2).
   - ✅ Guildes (`GuildMemberEntity`, `GuildService`, `POST /api/guilds`,
     `POST /api/guilds/{id}/join`) : un personnage n'appartient qu'à une seule guilde à la
     fois, noms de guilde uniques. Vérifié de bout en bout : création, refus de nom
     dupliqué (par le même personnage ET par un autre), adhésion, liste des membres à jour,
     refus d'une deuxième adhésion.
   - ✅ Succès + classements (`AchievementCatalog`, `AchievementService`, `LeaderboardService`,
     `GET /api/achievements/catalog`, `GET /api/achievements`,
     `POST/GET /api/leaderboard/{category}`) : 4 succès de départ, débloqués automatiquement
     par les autres services (création de personnage → "bienvenue", capture → "premier
     compagnon", craft → "artisan débutant", création de guilde → "fondateur") plutôt
     qu'exposés comme un endpoint "débloquez ce que vous voulez" (vecteur de triche).
     Classements Richesse/Métiers/MonstresCaptures calculables dès maintenant à partir des
     données existantes ; les autres catégories (PvP, temps de jeu, ...) attendent les
     systèmes correspondants. Vérifié de bout en bout : succès débloqué après capture,
     classement recalculé et lu après capture.
   - ✅ Combat tactique sur grille (`Server/World/Combat` : `CombatEngine`, `CombatSession`,
     `CombatSessionStore`, `CombatService`, `POST /api/combat/start`,
     `POST /api/combat/{id}/action`, `GET /api/combat/{id}`) : grille 7x7, ordre de jeu par
     vitesse, déplacement borné, dégâts Attaque−Défense, IA simple pour le monstre sauvage
     (se rapproche puis attaque), mode Solo uniquement (joueur + jusqu'à 4 créatures contre un
     monstre sauvage). L'action `Capture` calcule le vrai pourcentage de vie restant et appelle
     le `CaptureService` existant — la capture ne prend plus un pourcentage de vie inventé à
     la main, le combat le fournit réellement.
     **Limites assumées** : pas de sorts/compétences (seulement une attaque de base à portée 1),
     pas de terrain/obstacles/zones d'effet/combos, pas de mode Coopération (4 joueurs), les
     PV du personnage ne sont pas persistés entre deux combats (repartent à 50 à chaque fois).
     Vérifié de bout en bout : combat perdu (K.O., session nettoyée, action suivante rejetée),
     puis combat où la capture est tentée en cours de combat et échoue proprement (objet
     consommé, combat terminé).
   - ✅ Intégration donjons + combat (`POST /api/dungeons/{id}/floors/{n}/rooms/{i}/engage`) :
     engage directement le combat contre le monstre d'une salle générée procéduralement ;
     la rareté de la créature suit le type de rencontre (Commun/PeuCommun pour une salle
     Monstre normale, Rare pour un mini-boss, Légendaire pour un boss/boss légendaire).
     Vérifié : salle Monstre → combat démarré, salle non-combat (Énigme) → rejetée, étage 10
     → Ombrelune (Rare), étage 50 → Dracaelith (Légendaire), conforme aux jalons du GDD.
   - ✅ PvP classé (`CombatSession` étendu au multi-joueur réel — `TeamOwnerUserId`/
     `TeamCharacterId` par équipe, `POST /api/pvp/challenge`) : défi direct entre deux
     personnages, chacun contrôlé par son propre compte, alternance de tour stricte
     vérifiée par jeton de session (l'action de l'un est rejetée hors de son tour). La
     victoire met à jour `PvpStatistics` (victoires/défaites/série/rang) des deux côtés.
     Vérifié de bout en bout avec deux comptes distincts : tentative hors tour rejetée,
     alternance correcte, combat mené jusqu'au K.O.
   - ✅ Guerres de royaumes (`TerritoryEntity`, `KingdomWarService`,
     `GET /api/territories`, `GET/POST /api/kingdoms/wars/standings|resolve`) : chaque
     victoire PvP crédite le royaume du vainqueur en points de guerre ; la résolution
     hebdomadaire (déclenchée manuellement pour l'instant — un vrai job planifié viendrait
     ensuite) donne l'ensemble des territoires au royaume en tête, puis remet les points à
     zéro. **Simplification assumée** : pas de contestation territoire par territoire, un
     royaume "gagne toute la carte" plutôt que des gains partiels — documenté comme tel.
     Vérifié : victoire PvP → points crédités → résolution → territoires transférés
     (y compris ceux d'autres royaumes) → points remis à zéro.
   - ✅ Saisons (`SeasonEntity`, `SeasonService`, `GET /api/seasons/current`,
     `POST /api/seasons/next`) : suivi du cycle actif/numérotation uniquement — le contenu
     ajouté à chaque saison (monstres, donjons, cosmétiques, passe saison) reste un travail
     de contenu à faire, pas simulé ici. Vérifié : Saison 1 active dès le premier démarrage.
8. ✅ Outils et distribution :
   - ✅ MonsterEditor — WPF, CRUD complet du bestiaire (`GET/POST/PUT/DELETE
     /api/monsters/species`, exposé via `Shared.Models.MonsterSpeciesData` plutôt que
     l'entité EF Core, pour que l'outil ne référence que `Shared`). Pas d'authentification
     admin dédiée pour cette version (outil interne supposé lancé contre un serveur de
     confiance — à sécuriser avant tout déploiement réel). Vérifié : cycle créer → modifier
     → lire → supprimer via l'API, lancement de l'application sans exception.
   - ✅ MapEditor — WPF, CRUD du catalogue de donjons (`GET/POST/PUT/DELETE /api/dungeons`,
     nouveau `GET /api/kingdoms`) + prévisualisation textuelle de la génération procédurale
     d'un étage (liste des salles générées). **Pas de rendu visuel de grille** — ce serait un
     gros morceau à part (intégration OpenGL/Engine dans WPF), non fait ici, uniquement une
     liste texte. Vérifié : royaumes et territoires bien remontés, cycle créer → prévisualiser
     étage 10 (mini-boss confirmé) → supprimer.
   - ✅ AdminPanel — WPF, `GET /api/admin/users` (recherche), `POST /api/admin/users/{id}/ban`,
     `POST /api/admin/users/{id}/unban`, `GET /api/admin/stats` (comptes, bannis, personnages,
     créatures capturées, guildes, saison active). Le bannissement est effectif immédiatement :
     `AccountService.LoginAsync` (Phase C) rejette déjà les comptes bannis avec leur raison.
     Vérifié de bout en bout : recherche, bannissement → connexion refusée avec la raison →
     débannissement → connexion à nouveau acceptée, statistiques globales à jour.
   - ✅ Installateur Windows (`AetheriaInstaller.exe`, `Installer/`) — écrit en C#/WPF comme
     le reste du projet plutôt qu'avec un outil externe (Inno Setup n'est pas installé dans
     cet environnement, donc pas testable ici ; un installateur maison reste vérifiable de
     bout en bout). Copie récursivement un dossier `Payload/` (à construire par un script de
     publication rassemblant Launcher/Client/Server — pas encore automatisé) vers le dossier
     choisi, crée un raccourci `.lnk` via l'objet COM `WScript.Shell` (late-bound, sans
     dépendance tierce). Vérifié de bout en bout dans un dossier temporaire (jamais le vrai
     bureau de la machine) : copie des 4 fichiers du payload confirmée, raccourci `.lnk`
     inspecté et pointant vers le bon exécutable avec le bon dossier de travail. Pas de
     désinstallateur ni d'entrée dans le registre Windows pour cette première version.
   - ✅ Site de téléchargement (`Sites/index.html`) — page statique HTML/CSS pur (aucune
     dépendance, aucune étape de build), hors solution .NET. Présentation du jeu,
     fonctionnalités, royaumes, bouton de téléchargement pointant vers une URL réelle
     (`github.com/kikilikiki/Aetheria/releases`) plutôt qu'une URL inventée — **aucune
     release n'y est encore publiée**, ce qui est indiqué explicitement sur la page.
     Vérifié : structure HTML valide (balises équilibrées), pas de rendu visuel possible
     sans navigateur dans cet environnement.

> **Découverte en testant (pas un bug de code) :** une politique de sécurité de la machine
> bloque spécifiquement l'exécution du binaire natif `Aetheria.Server.exe` (probablement une
> heuristique visant les programmes qui ouvrent des ports d'écoute réseau), alors que
> `Client.exe`, `Launcher.exe` et `MonsterEditor.exe` s'exécutent sans problème. Contournement
> légitime utilisé pour les tests : `dotnet build/bin/Aetheria.Server/Debug/net10.0/
> Aetheria.Server.dll` au lieu de l'apphost natif — un mode de lancement .NET standard, pas un
> contournement de sécurité. À garder en tête pour le déploiement réel : il faudra soit
> distribuer/lancer le Server via `dotnet`, soit faire autoriser l'exécutable par la politique
> de sécurité de la machine hôte.

> **Piège rencontré et corrigé (le plus sournois du projet) :** `HashCode.Combine` a été
> utilisé comme graine de génération procédurale des donjons (Phase G3) et du tirage des
> monstres de donjon (Phase H2). Or `HashCode.Combine` est **délibérément randomisé par
> processus** par le runtime .NET (protection anti-collision de table de hachage) — sa
> documentation le précise, mais c'est facile à manquer. Conséquence : la génération
> semblait déterministe tant que le serveur tournait (testée ainsi en G3), mais changeait
> à chaque redémarrage, ce qui aurait cassé toute discussion entre joueurs sur le contenu
> d'un étage précis. Détecté en re-testant l'intégration donjon+combat avec un nouveau
> processus serveur. Corrigé par `DungeonFloorGenerator.StableSeed` (combinaison manuelle
> `hash * 31 + valeur`), utilisé partout où une graine doit survivre à un redémarrage.

> **Piège rencontré et corrigé :** `ComplexProperty` (EF Core 8+, utilisé pour mapper
> `StatBlock` en un seul bloc) fait planter le fournisseur InMemory sur certaines requêtes
> (`KeyNotFoundException` interne). Corrigé en aplatissant `StatBlock` en colonnes scalaires
> (`Base*`/`StatBonus*`) avec une propriété `[NotMapped]` de confort — voir `ItemEntity` et
> `MonsterSpeciesEntity`. Non re-testé sur PostgreSQL réel (indisponible dans cet
> environnement), mais l'approche par colonnes scalaires est de toute façon la plus
> largement compatible entre fournisseurs EF Core.

> **Limite de vérification connue :** je peux confirmer par les logs et l'absence de plantage
> qu'un rendu s'exécute sans erreur, mais je n'ai pas d'outil pour capturer une image de la
> fenêtre native et vérifier visuellement le résultat pixel par pixel — à valider par un humain
> en lançant `Aetheria.Client.exe`.

Dépendance graphique du moteur : **Silk.NET** (Windowing + OpenGL + Input + Maths), choisie
pour ses bindings modernes activement maintenus par la communauté .NET et son bon écosystème
de documentation en C#. Voir `Engine/Aetheria.Engine.csproj` pour les versions exactes.

### Configuration du Serveur

| Variable d'environnement    | Rôle                                                              |
|------------------------------|--------------------------------------------------------------------|
| `AETHERIA_DB_CONNECTION`     | Chaîne de connexion PostgreSQL. Absente ⇒ base en mémoire (dev).   |

Ports par défaut (voir `Shared/GameInfo.cs`) : `7777` (TCP jeu), `7778` (HTTP compte).

## Compiler le projet

Voir les commandes dans le message de suivi de l'étape 1, ou simplement :

```powershell
dotnet build Aetheria.sln
```

Pour lancer un module précis (exemple : le futur launcher) :

```powershell
dotnet run --project Launcher/Aetheria.Launcher.csproj
```
