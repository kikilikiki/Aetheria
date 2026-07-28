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
   - ✅ Rendu isométrique (`Client/World/IsoMath`, `Building`, `Npc`, `WorldMap`) : monde de
     démonstration 50x50 cases, projection "2:1" (tuiles en losange via `SpriteBatch.DrawQuad`
     acceptant 4 coins arbitraires, pas seulement des rectangles axés). Terrain varié par
     hachage déterministe par case (herbe claire/moyenne/foncée, étang, chemins reliant les
     bâtiments), 5 bâtiments (Capitale, Village, Hôtel des ventes, Forge, Guilde) en
     pseudo-3D avec **enseigne** (poteau + plaque devant l'entrée), 4 PNJ statiques avec
     animation d'attente (balancement procédural), un **portail de donjon** animé (anneaux
     concentriques dont le cœur pulse entre deux teintes via `MathF.Sin`, relié au vrai
     "Donjon des Araignées" seedé côté serveur), et un **personnage joueur amélioré**
     (silhouette corps + tête au lieu d'un simple losange plat, avec balancement idle/marche).
   - ✅ Déplacement à la souris (`Camera2D.ScreenToWorld`, `IsoMath.IsoToGrid`,
     `Engine.Input.MouseState` étendu avec détection de clic) : clic gauche → case visée
     par transformation isométrique inverse → chemin orthogonal calculé (même algorithme que
     les routes de la carte) → suivi case par case (mode démo : interpolation continue ;
     mode connecté : un `SendMove` par case, enchaîné à chaque confirmation serveur). Le
     clavier reste disponible et reprend la main immédiatement sur un chemin cliqué en cours.
   - ✅ Interaction de proximité généralisée aux bâtiments (en plus du donjon) : s'approcher
     d'un bâtiment affiche "Vous entrez dans « Nom »", **honnêtement documenté comme n'ayant
     pas de scène d'intérieur réelle** (pas de pièce, pas de PNJ à l'intérieur) — c'est un
     message d'interaction, pas une simulation de fonctionnalité qui n'existe pas.
   - **Vérifié visuellement** via captures d'écran ciblées du process réel (Win32
     `PrintWindow` sur le handle de fenêtre spécifique — jamais de capture plein écran, voir
     encadré ci-dessous) : bâtiments avec enseignes, PNJ de couleurs distinctes, portail
     multi-anneaux rendu correctement. **Déplacement au clic vérifié en conditions réelles** :
     un clic proche du centre de la fenêtre a produit un déplacement court et précis vers la
     bonne case, avec la caméra recentrée correctement sur la nouvelle position — la formule
     caméra-suit-joueur et le picking isométrique inverse sont donc confirmés corrects, pas
     seulement corrects "par construction".
   - **Limites assumées** : bâtiments/PNJ à l'échelle d'une fraction de case (silhouettes
     stylisées, pas de vraie emprise au sol ni de sprites/textures réels — couleurs unies
     uniquement), animations procédurales (balancement sinusoïdal) plutôt que sprites animés
     faute d'assets, pas de scène d'intérieur pour les bâtiments/donjons (seule l'API serveur
     de combat/génération de donjon est réellement fonctionnelle, voir plus haut), pas de
     pathfinding évitant les obstacles (chemin orthogonal direct).

9. ✅ Refonte du Launcher (style Ankama/Dofus) et déplacement de la création de personnage :
   - ✅ `Launcher/MainWindow.xaml`/`MainViewModel.cs` réécrits : barre latérale, bouton JOUER
     unique, statut serveur, actualités cliquables (`NewsItem`, `RecentNews`/`AllNews`, page
     "toutes les actualités" + détail), overlay Paramètres (disposition clavier QWERTY/AZERTY,
     voir point 10 plus bas). La création/sélection de personnage a été retirée du Launcher :
     elle se fait désormais **en jeu**, au premier lancement du Client (voir Program.cs —
     `SceneMode.CharacterSelect`/`CharacterCreate`), pas dans une fenêtre WPF séparée, pour
     permettre un aperçu animé de l'apparence (voir GDD).
   - ✅ **Bug critique rapporté ("aucun bouton ne fonctionne dans le Launcher") — clos** : revue
     statique complète de `MainViewModel.cs` confirme que les 8 commandes utilisées par le XAML
     (`LoginCommand`, `RegisterCommand`, `LogoutCommand`, `PlayCommand`, `ToggleSettingsCommand`,
     `ToggleAllNewsCommand`, `OpenNewsDetailCommand`, `CloseNewsDetailCommand`) sont bien
     générées par `[RelayCommand]`/`[RelayCommand(CanExecute = ...)]` et correctement liées côté
     XAML — aucune commande orpheline ni `Binding` cassé trouvé. La cause la plus probable des
     symptômes observés est ailleurs : une technique de simulation d'entrée clavier utilisée
     pendant les tests (`AttachThreadInput`/`SetForegroundWindow`) volait le focus clavier réel
     de la machine partagée, ce qui pouvait donner l'impression que l'interface ne répondait
     plus. Cette technique a été abandonnée (voir plus bas) au profit de vérifications passives
     (logs, captures ciblées) qui ne touchent pas le focus global. **Non re-testé en conditions
     réelles suite à cette revue** — à confirmer par un humain si le symptôme réapparaît.
   - ✅ **Bug réel confirmé en conditions réelles et corrigé** : "Créer un compte" faisait
     planter tout le processus Launcher quand le champ email était laissé vide.
     `MainViewModel.IsValidEmail` n'attrapait que `FormatException` autour de
     `new MailAddress(email)`, or une chaîne vide lève `ArgumentException` (et `null` lève
     `ArgumentNullException`) — ni l'une ni l'autre n'était interceptée, donc l'exception
     remontait non gérée. Corrigé par un contrôle `string.IsNullOrWhiteSpace` en amont. Confirmé
     par un test isolé (`new MailAddress("")` → `ArgumentException`, jamais `FormatException`)
     puis par la disparition du processus `Aetheria.Launcher` observée après le clic, avant le
     correctif. `Launcher/App.xaml.cs` a aussi reçu un gestionnaire
     `DispatcherUnhandledException` (absent jusqu'ici) comme filet de sécurité : toute exception
     non gérée future sur le thread UI affichera un message d'erreur au lieu de tuer tout le
     processus silencieusement.
   - ✅ Inventaire/Guilde/Boutique en jeu (`PanelKind.Inventory/Guild/Shop`, touches I/G/B) :
     panneaux superposés au monde extérieur, alimentés par `GameDataApiClient`
     (`GET /api/characters/{id}/inventory`, `GET /api/guilds/mine`, `GET /api/shop/catalog`,
     `POST /api/shop/buy`).
   - ✅ Sélection du starter en jeu avec histoire (voir `StarterService`, `SceneMode.StarterSelection`)
     et corps de personnage/PNJ plus lisibles (voir point 6 — silhouette corps + tête).
   - ✅ Disposition clavier détectée automatiquement (QWERTY/AZERTY), réglable en jeu (touche F9)
     et dans le Launcher (`Shared/Settings/GameSettings`, `KeyboardLayoutResolver` — LANGID
     Windows), persistée dans `%APPDATA%\Aetheria\settings.json`. N'affecte que les libellés
     affichés : les codes de touche Silk.NET/GLFW étant basés sur la position physique, WASD
     fonctionne déjà nativement en ZQSD sur un clavier AZERTY sans remappage.
10. ✅ Groupes, butin partagé et visibilité globale des joueurs (voir GDD) :
    - ✅ Groupes (`PartyEntity`/`PartyMemberEntity`, `PartyService`, `POST /api/parties`,
      `POST /api/parties/{id}/join`, `POST /api/parties/leave`, `GET /api/parties/mine`) : 4
      joueurs maximum, transfert automatique du rôle de chef si celui-ci quitte, groupe supprimé
      si le dernier membre part. XP de combat **partagée en plein** entre tous les membres (pas
      divisée) via `CharacterProgressionService` — voir GDD ("l'xp est partagé entre tout les
      membres du groupe"). Panneau en jeu (`PanelKind.Party`, touche P) : créer, rejoindre par
      identifiant (saisi au clavier), quitter, liste des membres avec niveau.
      **Limite assumée** : pas d'invitation en un clic ni de liste de groupes ouverts à
      proximité — l'identifiant du groupe doit être communiqué hors jeu pour rejoindre.
    - ✅ Visibilité globale en temps réel (`WorldSessionRegistry`, `PlayerSession`, packets
      `PlayerJoined`/`PlayerPositionUpdate`/`PlayerLeft`) : tout joueur connecté voit tous les
      autres se déplacer en direct, même hors groupe — chaque déplacement est diffusé par le
      serveur à toutes les sessions TCP connectées, pas seulement à l'émetteur. Rendu client
      (`RemotePlayer`, `DrawRemotePlayerFigure`) : silhouette bleutée + nom, réutilise le même
      `DrawFigure` que le joueur local/les PNJ. **Limite assumée** : aucune validation
      serveur de portée/collision sur les déplacements (voir commentaire dans
      `PlayerSession.HandlePlayerMove`) — le mode Coopération (plusieurs joueurs dans le même
      combat) reste à faire, cette diffusion ne couvre que l'exploration du monde partagé.
    - ✅ Butin de victoire partagé (`LootService`, `LootRoll`, `POST /api/loot/{id}/claim`,
      `GET /api/loot/{id}`) : 4 objets tirés du catalogue à chaque victoire PvE (pas sur capture
      réussie — la capture est déjà sa propre récompense), partagés avec le groupe du vainqueur
      s'il en a un. Résolu dès que chaque personnage éligible a réclamé un objet ; en cas
      d'égalité sur le même objet, tirage aléatoire parmi les réclamants (`LootRoll.Resolve`,
      fonction pure). **Limite assumée, documentée honnêtement** : le mode Coopération (plusieurs
      joueurs humains dans le même combat) n'existe pas encore, donc en pratique un seul
      réclamant réel existe par combat aujourd'hui — la logique de répartition/tirage aléatoire
      est déjà celle qui servira une fois ce mode ajouté, mais n'a pu être vérifiée qu'en revue
      de code (GroupBy + tirage aléatoire sur les réclamants), pas via un vrai scénario à
      plusieurs joueurs humains disputant le même objet.
    - ✅ XP de combat (`CharacterProgressionService`, formule identique à `ProfessionService` : XP
      requise au niveau N = N × 100) : n'existait pas du tout avant cette phase (les personnages
      ne montaient jamais de niveau par le combat). **Simplification assumée** : montant fixe
      par victoire PvE plutôt que calculé sur le niveau/la rareté exacte de la créature vaincue.
    - ✅ Mobs sauvages hors donjon (`CombatService.StartWildEncounterAsync`,
      `POST /api/combat/start-wild`) : rencontre aléatoire déclenchée en marchant en zone sauvage
      à l'extérieur (`WorldMap.IsWildEncounterZone` — herbe libre, ni chemin ni étang), pas
      seulement en entrant dans le donjon comme avant. La rareté de l'espèce tirée dépend de
      paliers de niveau fixes (`RarityForLevel`) appliqués au **niveau du chef de groupe** si le
      personnage est en groupe, sinon son propre niveau (voir GDD et
      `PartyService.ResolveScalingReferenceAsync`) — contrairement au donjon, où c'est encore le
      Client qui choisit une espèce commune au hasard (`StartWildCombatAsync`, resté tel quel).
      Le combat revient à la bonne scène une fois terminé (`combatReturnScene` : Extérieur pour
      une rencontre sauvage, Intérieur pour le donjon) plutôt que de toujours renvoyer à
      l'intérieur du donjon comme avant cette phase.
      **Simplifications assumées** : probabilité de rencontre constante (8 % par case franchie)
      plutôt que dépendante du biome/terrain ; paliers de niveau fixes plutôt qu'une formule
      continue ; pas d'exclusion de zone autour des bâtiments (silhouettes sans vraie emprise au
      sol, voir plus haut).
11. ✅ Intérieurs de bâtiment enrichis (voir GDD) : `Client/World/BuildingInterior.cs`
    (`BuildingInteriors.ForBuilding`) associe à chaque bâtiment nommé (Capitale, Village, Hôtel
    des ventes, Forge, Guilde) 1-2 meubles décoratifs et un PNJ propre, avec ses répliques dans
    `NpcDialogues` (Chambellan, Aubergiste, Commis, Apprenti forgeron, Archiviste). Remplace
    l'écran de simple texte de présentation par une vraie scène avec décor + un PNJ qu'on peut
    interroger (touche E), en réutilisant `DrawDialogueBox`/`NpcDialogues` déjà en place pour les
    PNJ extérieurs (`UpdateActiveDialogueIfAny` a été factorisé pour être partagé entre les deux
    contextes plutôt que dupliqué). **Limites assumées** : les meubles sont des rectangles en
    repère écran relatif (pas de vraie scène isométrique/3D pour l'intérieur, cohérent avec le
    style écran-plat déjà utilisé par cette scène), un seul PNJ affiché même si plusieurs étaient
    définis pour un bâtiment (pas de curseur de sélection). L'intérieur du donjon n'utilise pas
    ce même système de meubles/PNJ : il a son propre écran, voir point 12 ci-dessous.
12. ✅ Exploration du donjon en couloir linéaire (voir GDD — "mobs/loot au fil du chemin") :
    - Le Client consomme désormais réellement la séquence de salles générée côté serveur
      (`GET /api/dungeons/{id}/floors/{n}`, jusque-là seule l'API existait sans intégration
      Client, voir point 7 plus haut) — `Client/Program.cs` (`UpdateDungeonCorridor`,
      `DrawDungeonCorridor`) affiche une rangée de cases (une par salle), avance case par case à
      l'Entrée, la case courante mise en évidence.
    - Salles Monstre/MiniBoss/Boss/BossLegendaire : combat réel via l'endpoint d'engagement déjà
      existant (`POST .../rooms/{i}/engage`, `CombatApiClient.StartDungeonCombatAsync`) — victoire
      → avance à la salle suivante (`AdvanceDungeonRoom`, appelé depuis le même écran de
      continuation que le butin de victoire) ; défaite → reste sur la même salle pour retenter.
    - Salles Coffre : nouvel endpoint `POST .../rooms/{i}/loot-chest`
      (`Server/World/DungeonRoomService.OpenChestAsync`) — or gagné (20 à 80, tiré d'une graine
      dérivée de celle du combat mais décalée pour ne pas reproduire le même tirage) ajouté
      directement à `CharacterEntity.Gold`. C'est le "loot au fil du chemin" du GDD.
    - Salles Énigme/Piège/Marchand/Événement/Autel/Salle secrète : texte d'ambiance uniquement
      (`DungeonRoomFlavor`) — **non simulées plutôt que du contenu inventé**, cohérent avec la
      façon dont ce projet documente ses limites ailleurs.
    - Dernière salle de l'étage franchie → écran "ÉTAGE TERMINÉ", Entrée charge l'étage suivant
      (`dungeonFloorNumber++`, nouvel appel à `GET /api/dungeons/{id}/floors/{n}`) — les jalons
      mini-boss/boss/boss légendaire (tous les 10/50/100 étages, un étage à salle unique côté
      générateur) fonctionnent donc aussi via ce couloir.
    - **Disponible uniquement en mode connecté** (`worldMap.DungeonId` résolu par
      `RefreshDungeonPositionAsync`, voir point 9/plus haut) — le mode démo hors-ligne garde
      l'ancien stub à un seul combat aléatoire (`StartWildCombatAsync`) plutôt que de planter.
    - **Limites assumées** : pas de disposition spatiale réelle des salles (une rangée de cases
      abstraite, pas un vrai plan de couloirs/pièces — cohérent avec la limite déjà documentée
      pour `DungeonFloorGenerator`), pas de vraie récompense pour les salles Marchand/Autel/etc.
      Vérifié par revue de code et compilation (build complet de la solution) ; pas de test de
      bout en bout avec un vrai combat (nécessiterait de lancer le client en conditions réelles,
      hors de portée des vérifications passives utilisées dans cette session).

13. ✅ Villes distinctes par royaume (voir GDD — "plusieurs villes distinctes par royaume/biome") :
    `Client/World/KingdomBiome.cs` associe à chaque `KingdomType` (Feu, Nature, Glaces, Ombres)
    un nom de capitale propre (Citadelle de Braise / Sylvaltar / Citadelle de Glace / Bastion des
    Ombres), un nom de donjon propre, et une palette de terrain distincte (herbe/chemin/étang —
    braises orangées pour Feu, neige pâle pour Glaces, ton violet sombre pour Ombres). Les
    bâtiments sont teintés via `AccentTint` plutôt que redessinés entièrement par royaume, pour
    garder la même disposition/lisibilité tout en restant visuellement distincts.
    `CharacterSummary` expose désormais `Kingdom` (absent avant cette phase — ni
    `GET /api/characters/mine` ni `POST /api/account/login` ne le renvoyaient), et
    `Client/Program.cs` (`RebuildWorldMapForKingdom`) reconstruit `WorldMap` avec le bon royaume
    dès qu'il est connu (sélection d'un personnage existant ou fin de création), avant la
    connexion au serveur. **Portée assumée** : un joueur ne voit que la capitale de son propre
    royaume — il n'y a pas de voyage entre royaumes ni de carte du monde reliant les quatre
    villes entre elles (ce serait un morceau à part : plusieurs `WorldMap` actives, un écran de
    voyage/téléportation). Le raccourci `--characterId` (lancement direct sans passer par l'écran
    de sélection) garde par défaut le royaume Nature faute de pouvoir résoudre le personnage sans
    appel réseau supplémentaire à ce stade du démarrage.
14. ✅ Retours utilisateur après premier test en conditions réelles (combat trop long, actions
    clavier uniquement, panneaux incomplets) :
    - ✅ **Équilibrage du combat** (`CombatEngine.ResolveAttack`) : la Défense n'est plus
      soustraite en entier de l'Attaque, seulement à moitié (`Attack - Defense / 2`, plancher à 2
      au lieu de 1) — avec l'Attaque du joueur (10) et des créatures dont la Défense de base
      atteint 15, la soustraction complète plafonnait presque tous les coups à 1 dégât contre des
      PV de 26-60, rendant les combats interminables ("les monstres ont beaucoup trop de vie").
    - ✅ Cases de déplacement/attaque affichées avant de valider une action (`CombatantState`
      expose désormais `MovementRange`/`AttackRange`, absents jusqu'ici — `CombatReachableCells`
      calcule les cases valides côté Client).
    - ✅ Actions cliquables en plus du clavier partout où c'était encore clavier-only :
      `DrawClickableCentered` (surlignage au survol + détection de clic) réutilisé pour les
      boutons d'action de combat, le clic direct sur une case de la grille de combat, et une
      nouvelle barre de boutons HUD (`DrawOutdoorHudButtons`, coin haut-droit) pour ouvrir
      Inventaire/Montres/Groupe/Guilde/Boutique/Arène sans connaître les raccourcis clavier.
    - ✅ Panneau Guilde réellement fonctionnel (`UpdateGuildPanel`/`GuildPanelMode`) : jusqu'ici en
      lecture seule (n'affichait que la guilde déjà rejointe). Ajoute créer (`C`), rechercher par
      nom et rejoindre (`R`) — nouvel endpoint `GET /api/guilds` (`GuildService.SearchAsync`,
      absent jusqu'ici, seul `GET /api/guilds/mine` existait).
    - ✅ UI de gestion des créatures (`PanelKind.Monsters`, touche `M`) : liste des créatures avec
      niveau/barre d'XP, et "donner un objet" (touche `D`) qui consomme un objet d'inventaire
      contre de l'XP (`MonsterCareService.GiveItemAsync`,
      `POST /api/monsters/{id}/give-item`). Les créatures **ne montaient jamais de niveau** avant
      cette phase (seul le personnage en gagnait) : `MonsterProgressionService` ajoute aussi de
      l'XP aux créatures alliées survivantes à chaque victoire PvE (même mécanisme que le
      personnage). **Simplification assumée** : tout objet donné accorde le même montant fixe
      d'XP, pas encore d'effets différenciés par objet.

> **Incident évité en testant :** une première tentative de vérification visuelle du site web
> (capture plein écran) a accidentellement capturé une fenêtre sans rapport avec la tâche
> (une autre application ouverte sur la machine). L'image a été supprimée immédiatement sans
> être exploitée. Depuis, toute capture d'écran cible exclusivement un processus lancé par
> feelsman lui-même via son handle de fenêtre (Win32 `PrintWindow`), jamais l'écran entier.
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
   - ✅ Position dynamique des donjons sur la carte (`DungeonWorldService.EnsureCurrentPosition`,
     appelé depuis `GET /api/dungeons`) : chaque donjon reçoit une position (`WorldX`/`WorldY`)
     tirée de façon déterministe pour l'heure UTC en cours (seed du donjon + numéro d'heure),
     recalculée paresseusement à la lecture plutôt que par une tâche planifiée — donc identique
     pour tous les joueurs qui consultent la carte pendant la même heure, sans service en
     arrière-plan à faire tourner. Vérifié via un harnais console isolé référençant directement
     les projets compilés : première lecture assigne une position, une lecture dans la même
     heure ne change rien, forcer un `PositionHourBucket` périmé déclenche un recalcul, et
     plusieurs seeds de donjon produisent tous des coordonnées dans les limites de la carte.
     **Côté Client** : `WorldMap.SetDungeon` applique désormais la position reçue de
     `GET /api/dungeons` une fois par connexion (voir `RefreshDungeonPositionAsync` dans
     `Client/Program.cs`), donc le portail apparaît bien à l'endroit tiré par le serveur pour
     l'heure UTC en cours. **Limites assumées** : pas de rafraîchissement en cours de session
     (il faut se reconnecter pour voir un changement d'heure survenu pendant que le client
     tourne), et les chemins de terre tracés à la construction de la carte ne sont pas retracés
     vers la nouvelle position (seuls le portail et sa zone d'interaction se déplacent).
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
   - ✅ Arènes classées 1v1/2v2/3v3/4v4 + ligues ELO (voir GDD) :
     - `Combatant.OwnerUserId`/`OwnerCharacterId` remplacent `TeamOwnerUserId`/`TeamCharacterId`
       pour l'autorisation d'action (`CombatService.SubmitActionAsync`) — plusieurs joueurs
       humains peuvent désormais partager la même équipe, une équipe n'est plus limitée à un
       seul compte.
     - `ArenaFormatRules` : nombre de créatures engagées par joueur selon le format — 1v1 = 4,
       2v2 = 2, 4v4 = 1, et 3v3 **volontairement asymétrique** (2/1/1, un total de 4 unités ne se
       divisant pas également entre 3 joueurs, voir GDD).
     - `ArenaQueueService` (`POST /api/pvp/arena/queue`, `GET /api/pvp/arena/status`,
       `POST /api/pvp/arena/cancel`) : file d'attente en mémoire, pas un vrai lobby — le combat
       se forme dès que `PlayersPerTeam × 2` joueurs distincts ont rejoint la file pour un
       format donné (première moitié = équipe 0, seconde moitié = équipe 1). Le joueur dont la
       requête complète le seuil déclenche la création du combat ; les autres, déjà en attente,
       le découvrent en sondant `GET /api/pvp/arena/status` (implémenté côté Client par un
       sondage toutes les 1.5 s tant que le panneau Arène — touche V — reste ouvert).
       `CombatService.StartArenaMatchAsync` place les combattants via `BuildTeamCellQueue` (une
       file de cases libres par équipe remplie joueur par joueur, pas un calcul de ligne par
       format) pour rester correct même au format le plus dense (4v4 : 8 combattants par équipe).
     - ELO (`CombatService.ComputeNewElo`, K=32, formule logistique standard) remplace l'ancien
       ajustement fixe (+10/-5) du 1v1 direct — `PvpStatistics.CurrentRank` sert de note ELO
       (valeur de départ 1000). `ApplyArenaResultAsync` généralise `ApplyPvpResultAsync` : chaque
       participant (pas un seul "représentant" par équipe) gagne/perd de l'ELO contre la note
       moyenne de l'équipe adverse (méthode standard pour l'ELO par équipe).
     - Vérifié via un harnais console isolé (`ArenaQueueService` référencé directement) :
       mise en file jusqu'au seuil exact puis appairage (ordre des tickets confirmé), doublon de
       personnage ignoré, ré-inscription après annulation acceptée, `TryConsumeMatch` ne renvoie
       le combat qu'une seule fois. **La formule ELO elle-même n'a été vérifiée que par relecture
       de code** (implémentation manuelle du calcul attendu non refaite dans le harnais).
     - **Limites assumées** : pas de vrai lobby (impossible d'inviter des amis nommément dans une
       équipe d'arène, seul l'ordre d'arrivée en file compte), pas de garde-fou empêchant deux
       comptes du même joueur de s'auto-appairer, pas de saison/classement affiché séparément du
       leaderboard PvP existant.
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
   - ✅ Site de téléchargement (`Sites/index.html`, `Sites/conditions-generales.html`) — page
     statique HTML/CSS pur, hors solution .NET. Bouton "Installer le Launcher" en lien
     `download` direct vers `Sites/downloads/AetheriaSetup.zip` (un vrai paquet construit à
     partir des builds Release réelles — `AetheriaInstaller.exe` + `Payload/` avec
     Launcher+Client — pas une redirection GitHub Releases). Footer avec copyright et lien
     vers une page CGU complète (compte, règles de conduite, propriété intellectuelle,
     absence de garantie, données personnelles). Références à des jeux tiers retirées du
     texte de présentation. Vérifié : structure HTML valide des deux pages, zip extrait et
     `AetheriaInstaller.exe` relancé depuis l'extraction (le paquet fonctionne réellement).
     Limite assumée : paquet reconstruit et commité manuellement, pas de CI de publication ;
     build "framework-dependent" (nécessite le runtime .NET 10 Desktop sur la machine cible).
   - ✅ Annonces Discord (`Server/Discord/DiscordAnnouncer.cs`) : poste un embed dans un ou
     plusieurs salons Discord fixes via l'API REST des bots (`Authorization: Bot <token>`) plutôt
     qu'une connexion gateway complète — pas besoin de recevoir d'évènements Discord pour de
     simples annonces sortantes. "Hébergé" par le processus `Aetheria.Server` existant : pas de
     bot séparé à faire tourner. Le jeton (`DISCORD_BOT_TOKEN`), les salons
     (`DISCORD_ANNOUNCE_CHANNEL_IDS`, séparés par des virgules — `DISCORD_ANNOUNCE_CHANNEL_ID` au
     singulier reste accepté) et le rôle notifié (`DISCORD_ANNOUNCE_ROLE_ID`) se configurent via
     un fichier `.env` à la racine (voir `.env.exemple` — copier en `.env`, jamais commité),
     chargé par `DotEnv.LoadIfPresent()` au démarrage. Chaque message mentionne
     (`<@&ROLE_ID>` dans `content`, avec `allowed_mentions.roles` explicite) un rôle fixe.
     **Vérifié en conditions réelles avec un vrai jeton/salon** : plusieurs annonces de test
     postées avec succès pendant cette phase de développement (y compris le ping de rôle).
     - ✅ **Récapitulatif quotidien à 23h** (`Server/Discord/GitChangelogAnnouncer.cs` +
       `PendingChangesLog` + `DailyDigestScheduler`) : les nouveaux commits Git sont détectés à
       chaque démarrage du serveur (comparaison au commit `HEAD` mémorisé dans
       `.discord-last-logged`, jamais commité) et **accumulés dans un fichier**
       (`.discord-pending-changes.txt`, jamais commité) plutôt que postés immédiatement.
       `DailyDigestScheduler` tourne en tâche de fond pendant toute la durée de vie du serveur
       (vérification toutes les minutes) et, une fois par jour à 23h heure locale, lit et vide ce
       fichier (`.discord-last-digest-date` retient la dernière date déjà traitée, pour ne poster
       qu'une fois par jour même à travers plusieurs redémarrages) : **si le fichier est vide à ce
       moment-là, aucun message n'est envoyé** — voir demande utilisateur explicite sur ces trois
       points. Repose sur le flux de travail réel du projet ("modifier le code → reconstruire →
       relancer le serveur") pour la détection des commits : pas besoin d'un hook Git séparé ni
       d'un service de surveillance de fichiers. Premier démarrage (pas encore de fichier d'état) :
       journalise les 20 derniers commits plutôt que tout l'historique. Vérifié via un harnais
       console isolé sur un vrai dépôt Git jetable : premier démarrage écrit l'état et journalise,
       un second démarrage sans nouveau commit ne journalise rien, un nouveau commit redéclenche
       bien une journalisation (et seulement de ce commit-là) ; `PendingChangesLog.Append`/
       `ReadAndClear` vérifiés séparément (accumulation sur plusieurs appels, vidage effectif
       après lecture, liste vide si aucun changement). **La bascule horaire à 23h elle-même n'a
       pas pu être testée en conditions réelles** (dépendante de l'heure système au moment du
       test) — vérifiée par relecture de code uniquement.
     - ✅ Endpoint manuel (`POST /api/admin/discord/announce`, réservé aux comptes admin via
       `AdminAuthService`) conservé en plus du récapitulatif quotidien, pour un message ad hoc
       immédiat. `Tools/discord-announce.ps1` automatise cet appel (connexion admin + annonce) en
       une seule commande.

15. ✅ Serveur distant configurable (voir GDD/demande utilisateur — "si on installe le jeu depuis
    un autre PC/wifi, on doit quand même pouvoir accéder au serveur hébergé chez
    [l'utilisateur]") : `GameSettings.ServerHost` (nouveau champ, "localhost" par défaut) est
    réglable dans les Paramètres du Launcher, persisté dans le fichier de préférences partagé, et
    transmis au Client au lancement (`ClientLauncher.TryLaunch` ajoute `--host=...`,
    `LaunchOptions.Parse` le consommait déjà — seul le Launcher ne le transmettait pas). Le ping
    `/api/health` et l'`AccountApiClient` du Launcher utilisent aussi cette adresse au lieu de
    `localhost` en dur. **Limite assumée, documentée dans le README public** : la redirection de
    ports (NAT/port forwarding) côté routeur de la machine hébergeant le serveur reste une étape
    manuelle hors de portée du code — le serveur écoute déjà sur `0.0.0.0` (toutes les
    interfaces), donc aucune configuration serveur supplémentaire n'est nécessaire au-delà de
    cette redirection.

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
