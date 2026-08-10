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
     `TeamCharacterId` par équipe, `POST /api/pvp/team-challenge`) : défi direct entre deux
     personnages, chacun contrôlé par son propre compte, alternance de tour stricte
     vérifiée par jeton de session (l'action de l'un est rejetée hors de son tour). La
     victoire met à jour `PvpStatistics` (victoires/défaites/série/rang) des deux côtés.
     Vérifié de bout en bout avec deux comptes distincts : tentative hors tour rejetée,
     alternance correcte, combat mené jusqu'au K.O.
   - ✅ Duel amical groupe vs groupe (`/duel <pseudo>` + bouton UI DUEL, `DuelInviteService`,
     `POST /api/pvp/team-challenge`, `CombatService.StartFriendlyTeamDuelAsync`) : si le
     personnage défié est en groupe, tous ses membres connectés doivent accepter avant que le
     combat ne démarre ; chaque participant engage son équipe active (`EquippedSlot`), pas de
     sélection manuelle par combat (impossible à coordonner entre plusieurs joueurs humains).
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
     inspecté et pointant vers le bon exécutable avec le bon dossier de travail.
     - ✅ **Entrée "Applications"/"Programmes et fonctionnalités" de Windows** (voir GDD/demande
       utilisateur — "quand on installe le jeu avec l'installer il doit être affiché dans les
       programs") : `UninstallRegistryService` enregistre une clé sous
       `HKEY_CURRENT_USER\...\Uninstall\Aetheria` (pas `HKEY_LOCAL_MACHINE` — cohérent avec une
       installation par défaut dans `%LocalAppData%`, sans droits administrateur). L'installateur
       se copie lui-même dans le dossier cible sous `Uninstall.exe` (reste disponible même si le
       fichier d'installation d'origine est supprimé), et l'entrée Windows pointe dessus avec
       `--uninstall --path=...`. Relancé ainsi (voir `App.xaml.cs` — intercepté avant
       `StartupUri`), il désinstalle en silence (fichiers, raccourci bureau, clé de registre) sans
       afficher de fenêtre. **Limite assumée** : un exécutable ne peut pas supprimer son propre
       fichier pendant qu'il tourne sous Windows — `Uninstall.exe` se supprime donc via une
       commande `cmd.exe` détachée avec un court délai (`ping` comme minuterie, technique
       classique), pas un vrai désinstallateur MSI. Vérifié par compilation et relecture de code
       uniquement (pas de test d'installation/désinstallation réelle sur cette machine partagée,
       pour ne pas modifier son registre Windows sans nécessité).
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
16. ✅ Détection de mise à jour dans le Launcher (voir GDD/demande utilisateur — "affiche dans le
    launcher quand il y a une mise à jour au lieu du bouton jouer, pour que la personne le fasse
    puis aille jouer") : le ping `/api/health` existant (`{status, version}`, déjà exposé par le
    serveur) est maintenant lu côté Launcher — si `GameInfo.Version` du serveur diffère de celui
    compilé dans ce Launcher, le bouton JOUER est remplacé par un bouton METTRE À JOUR désactivé
    (`ShowPlayButton`/`ShowUpdateButton`, `CanPlay` bloque aussi la commande) plutôt que de
    laisser jouer un client potentiellement incompatible avec le serveur. **Limite assumée,
    documentée honnêtement** : pas de vrai mécanisme de téléchargement/mise à jour automatique
    (il n'existe toujours pas de serveur de distribution de contenu, voir point 5 plus haut) — le
    bouton informe seulement qu'une nouvelle version tourne côté serveur et invite à
    retélécharger le Launcher manuellement, il ne fait rien d'automatique.
17. ✅ Tutoriel en jeu (voir GDD/demande utilisateur — "ajoute un tutoriel pour expliquer comment
    jouer") : ouvrable/fermable à tout moment avec F1 (mentionné dans le rappel de touches en bas
    à gauche), pas seulement au premier lancement — 6 pages courtes (bienvenue, déplacement,
    interaction, panneaux en jeu, combat, donjons), navigables aux flèches/Entrée, superposées en
    plein écran comme une scène d'intérieur. **Simplification assumée** : pas de suivi "déjà vu"
    persisté ni d'affichage automatique à la toute première connexion — F1 reste la seule façon
    de l'ouvrir, ce qui est honnête plutôt que de simuler un onboarding plus élaboré non demandé
    explicitement.
18. ✅ Système de grade communautaire (voir GDD/demande utilisateur — "ajouter un système de
    grade, le grade peut être donné par l'admin") : `UserRank` (`Joueur`/`Vétéran`/`Modérateur`/
    `Administrateur`), nouvelle colonne sur `UserEntity` (migration `AddUserRank`), distincte du
    flag technique `IsAdmin` (permission) — le grade est un statut communautaire affiché, pas une
    permission. Assignable depuis l'AdminPanel (colonne "Grade" dans le tableau des joueurs +
    sélecteur/bouton "Définir le grade" à côté du bouton de permission admin existant), via
    `POST /api/admin/users/{userId}/set-rank`. **Limite assumée** : le grade n'est pas encore
    affiché en jeu (tchat, liste des joueurs en ligne) — voir point 19 ci-dessous.
19. ✅ Tchat global, tchat de guilde et liste des joueurs en ligne avec leur grade (voir
    GDD/demande utilisateur) : panneau unique ouvert avec T (ou le bouton HUD "TCHAT"), deux
    onglets cliquables (aussi basculables avec Tab) partageant le même historique en mémoire
    (borné à 100 lignes, non persisté) filtré par canal. `ChatMessagePacket` (déjà présent dans
    le framing réseau mais jusqu'ici non câblé côté serveur) porte maintenant un `Channel`
    (`Global`/`Guild`) et un `Rank` — le serveur ignore le nom/grade envoyés par le client
    (usurpation impossible) et les renseigne depuis la session (mis en cache à l'entrée dans le
    monde). Le tchat de guilde résout les membres via `GuildMemberEntity` et ne diffuse qu'aux
    sessions correspondantes ; un joueur sans guilde recevant un message "Système" l'en informe.
    La liste des joueurs en ligne réutilise `remotePlayers`/`PlayerJoinedPacket` (déjà en place
    pour la visibilité globale sur la carte), désormais étendu pour porter le grade de chacun.
    **Limite assumée** : pas d'historique persisté entre connexions, pas de tchat privé/de
    groupe séparé (uniquement global et guilde, comme demandé).
20. ✅ Accès distant via ngrok, bases dev/prod et gros lot de retours de combat (voir GDD/demande
    utilisateur) :
    - **Ngrok** : `GameSettings.AccountApiBaseUrl` (nouveau champ, réglable dans les Paramètres du
      Launcher) permet de faire pointer l'API de compte (port 7778) vers un tunnel ngrok
      (`https://xxxx.ngrok-free.dev`) plutôt que `http://ServerHost:7778` — transmis au Client via
      `--apiUrl=`, distinct de `--host=` qui reste la connexion TCP de jeu (port 7777, toujours
      via redirection de ports classique côté routeur : les tunnels TCP ngrok exigent une carte
      bancaire vérifiée sur le compte, non activée ici sur décision de l'utilisateur).
    - **Bases dev/prod** : `AETHERIA_DB_CONNECTION` reconnaît maintenant aussi une chaîne SQLite
      (préfixe `Data Source=`), en plus de PostgreSQL (Npgsql) — choisi comme base fichier
      zéro-installation en l'absence de serveur PostgreSQL sur la machine hébergeant le serveur.
      `start-server-dev.bat`/`start-server-prod.bat` (racine du dépôt) lancent le serveur avec
      respectivement `aetheria-dev.db`/`aetheria-prod.db` (non versionnés, créés/migrés
      automatiquement au premier lancement) ; `start-launcher.bat` lance le Launcher. **Piège
      rencontré et corrigé** : le fournisseur SQLite déclenchait un faux
      `PendingModelChangesWarning` bloquant au démarrage (annotations de génération de valeur
      Npgsql absentes du modèle SQLite, alors qu'aucune migration ne manque réellement) — ignoré
      spécifiquement pour SQLite dans `Server/Program.cs`, pas pour Npgsql.
    - **Fuite de combat** (`CombatActionType.Flee`) : bouton/touche 6, absent plutôt que désactivé
      quand `CombatSession.IsDungeonCombat` (impossible de fuir un combat de donjon, possible en
      dehors), refusé aussi côté serveur si contourné.
    - **Types de monstres** (`MonsterType` : Guerrier/Archer/Soigneur, voir
      `MonsterCatalogSeeder`) déterminent une capacité spéciale (`CombatActionType.SpecialAbility`,
      touche 4) — Soigneur soigne l'allié le plus affaibli sans viser, Archer transperce en
      ignorant la Défense (portée +1), Guerrier déclenche un coup à dégâts majorés — utilisées
      aussi par l'IA adverse (un tour sur trois environ). Couleur en combat selon le type, avec un
      contour bleu (allié)/rouge (ennemi) simulé par un losange légèrement plus grand derrière le
      portrait.
    - **Avantages/faiblesses de type** : `Element` (déjà présent mais jamais branché) influence
      maintenant les dégâts (×1.5 en avantage, ×0.67 en désavantage) via un triangle de forces
      simplifié (`CombatEngine.StrongAgainst`), avec un message "(efficace !)"/"(peu efficace...)".
    - **4 ennemis plutôt qu'1** en combat PvE (rencontre sauvage et donjon), même espèce tirée,
      formation fixe sur la grille — pas de mise à l'échelle individuelle des stats.
    - **Aperçu de l'ennemi avant le combat** (voir GDD/demande utilisateur — "comme Pokémon
      Épée") : `GET .../rooms/{roomIndex}/encounter-preview`, même tirage exact (graine stable)
      que le combat réel, affiché (portrait + nom + élément) dès l'arrivée dans une salle à
      monstre, avant d'appuyer sur Entrée pour engager.
    - **Butin plus lisible** (voir retour utilisateur — "le choix d'objet ne se voit pas bien") :
      chaque objet a sa propre rangée cliquable avec fond/bordure de sélection, plus un badge
      indiquant combien de joueurs l'ont actuellement choisi (`LootSessionState.ClaimCountsByItemIndex`).
    - **Touche pour quitter le donjon hors combat** : Échap le faisait déjà, seul un rappel à
      l'écran manquait — ajouté, aucune nouvelle touche nécessaire.
    - **Limites assumées** : un seul palier de capacité spéciale par type (pas d'arbre de
      compétences), le personnage joueur est toujours de type Guerrier (pas de choix), et le
      MonsterEditor ne permet pas encore d'éditer le type d'une espèce depuis son interface
      (toujours modifiable via l'API/le seeder).
21. ✅ Corrections suite au premier test du point 20, et retrait du personnage joueur des combats
    (voir GDD/demande utilisateur) :
    - **Le personnage humain ne combat plus jamais directement** — "je ne veux pas que notre
      personnage soit présent en combat" — seules ses créatures sont désormais combattantes,
      aussi bien en PvE (rencontre sauvage/donjon) qu'en PvP/Arène. Le nombre d'ennemis se
      synchronise sur le nombre de créatures emmenées (1 à 4) plutôt qu'un total fixe de 4. Le
      personnage reste identifié via `CombatSession.TeamCharacterId` pour l'attribution des
      récompenses (XP/butin), sans figurer sur la grille. **Piège rencontré et corrigé** :
      `ResolveCaptureAsync` retrouvait l'espèce d'un monstre sauvage par son nom — cassé par le
      nommage numéroté des ennemis multiples ("Braisillon 1", "Braisillon 2", ...) introduit au
      point 20. Corrigé en ajoutant `Combatant.SpeciesId` (identifiant explicite) plutôt que de
      re-déduire l'espèce depuis un nom d'affichage.
    - **Piège rencontré et corrigé (bouton Fuir invisible hors donjon)** : le client décidait
      d'afficher le bouton Fuir à partir de son état de scène local (`interiorIsDungeon`), qui
      n'était jamais remis à `false` en quittant un donjon vers l'extérieur — un joueur ayant
      visité un donjon plus tôt dans sa session voyait le bouton Fuir durablement absent, même
      lors d'une rencontre sauvage hors donjon. Corrigé en ajoutant `IsDungeonCombat` à
      `CombatSessionState` (renvoyé par le serveur, toujours à jour) et en l'utilisant à la place
      de l'état de scène local.
    - **Diagnostic "connexion impossible à la création du personnage, mais fonctionne après un
      redémarrage du jeu"** : cause identifiée comme une erreur de configuration locale, pas un
      bug de code — `AccountApiBaseUrl` (voir point 20) avait été réglé sur le tunnel ngrok
      *sur la machine hébergeant elle-même le serveur*, alors que ce réglage n'a de sens que pour
      des joueurs distants. Faire transiter le trafic local par un aller-retour vers les serveurs
      ngrok (au lieu d'un appel direct à `localhost`) ajoutait une latence/fragilité inutile,
      cohérente avec des échecs intermittents qui se résolvaient au hasard d'un nouvel essai.
      Corrigé en remettant `ServerHost`/`AccountApiBaseUrl` sur `localhost`/vide pour les tests
      sur cette machine — `AccountApiBaseUrl` reste un réglage à ne renseigner que côté joueurs
      distants, jamais sur la machine du serveur lui-même.
    - **Paquet d'installation reconstruit** (`Sites/downloads/AetheriaSetup.zip`) avec le Launcher
      et le Client à jour (configuration Release) — voir `Sites/README.md` pour la procédure
      manuelle de reconstruction (pas encore de chaîne de publication automatisée).
22. ✅ Vrai bug derrière la "connexion impossible" persistante (le diagnostic ngrok du point 21
    n'était qu'une cause partielle) : `Launcher/Services/AccountApiClient.cs` et
    `AdminPanel/Services/AdminApiClient.cs` désérialisaient les réponses JSON du serveur
    (`LoginResponse`, `AdminUserSummary`, ...) **sans** le `JsonStringEnumConverter` que le
    serveur utilise pour sérialiser ses enums en toutes lettres (voir
    `ConfigureHttpJsonOptions` dans `Server/Program.cs`). Résultat : `System.Text.Json` échouait
    avec *"The JSON value could not be converted to Aetheria.Shared.Enums.KingdomType"* dès
    qu'un compte se reconnectait avec **au moins un personnage existant** (le champ
    `Characters[].Kingdom` de la réponse de connexion) — un compte flambant neuf sans personnage
    ne déclenchait jamais ce chemin de code, ce qui expliquait pourquoi "les autres comptes
    n'avaient pas de problème" alors que le compte réutilisé (dont le compte admin) plantait à
    chaque connexion. Les autres clients (`Client/Networking/*`, `MapEditor`, `MonsterEditor`)
    avaient déjà ce convertisseur ; seuls le Launcher et l'AdminPanel en manquaient. Corrigé en
    alignant leurs `JsonSerializerOptions` sur le même modèle que les autres clients.
23. ✅ Groupe : code à 5 chiffres, bouton copier, et combat/butin réellement partagés (voir
    GDD/demande utilisateur) :
    - **Code de groupe à 5 chiffres** (`PartyEntity.JoinCode`, unique, tiré aléatoirement à la
      création) remplace le GUID interne comme identifiant à communiquer entre joueurs — bien
      plus court à lire/retaper. `POST /api/parties/join` prend désormais ce code au lieu d'un
      `partyId` dans l'URL. Un bouton "COPIER" dans le panneau Groupe copie le code dans le
      presse-papiers système (`KeyboardState.SetClipboardText`, via la propriété `ClipboardText`
      de Silk.NET.Input plutôt qu'une dépendance WinForms/WPF).
    - **Combat de groupe réellement partagé** (voir retour utilisateur — "en groupe les 2 sont
      bien dans un combat mais 2 combats différents au lieu de se voir") : un membre de groupe
      qui engage un combat PvE (rencontre sauvage ou salle de donjon) alors qu'un combat de ce
      groupe est déjà en cours (`CombatSession.PartyId`) y ajoute directement ses créatures
      plutôt que de démarrer un second combat isolé — dans les cases encore libres de son côté
      de la grille. Comme le partage du butin (`LootService`) et de l'XP (`PartyService`)
      reposait déjà sur l'appartenance au groupe plutôt que sur la session de combat, corriger le
      partage du combat corrige du même coup l'affichage séparé du vote d'objets rapporté par
      l'utilisateur — c'était une conséquence du bug, pas un second bug distinct.
      `BuildPlayerCombatantsAsync` et `BuildArenaTeamCombatantsAsync` (Arène) ont été unifiées en
      une seule `BuildTeamCombatantsAsync` pour partager cette logique de placement.
      **Simplification assumée** : pas de verrou explicite contre une double création si deux
      membres engagent exactement au même instant (fenêtre de course très étroite) ; le nombre
      d'ennemis reste fixé au moment de la création du combat, pas réajusté si un membre rejoint
      ensuite.
24. ✅ Accès réseau simplifié (retrait de ngrok) et système de modération complet (voir
    GDD/demande utilisateur) :
    - **Retrait du tunnel ngrok** : `GameSettings.AccountApiBaseUrl` et le champ correspondant
      dans les Paramètres du Launcher ont été retirés — un seul réglage (`ServerHost`) suffit
      désormais, pour l'API de compte comme pour la connexion TCP de jeu. `GameSettings.ServerHost`
      est réglé par défaut sur l'IP publique du serveur (plus "localhost") : que ce soit en local
      ou depuis un autre réseau, la même adresse fonctionne sans réglage supplémentaire (le
      serveur écoute sur `0.0.0.0`, voir redirection de ports classique côté routeur).
    - **Grades étendus** (`UserRank` : Joueur/VIP/Ami/Testeur/Modérateur/Fondateur, remplace
      l'ancien jeu Joueur/Vétéran/Modérateur/Administrateur) : affichés en jeu (tchat, liste des
      joueurs en ligne) sous la forme `[GRADE] Pseudo`, avec une couleur dédiée par grade. Un
      compte Fondateur a désormais aussi accès aux actions d'administration (voir
      `AdminAuthService`), sans nécessiter le flag technique séparé `IsAdmin`.
    - **Mute** (`UserEntity.IsMuted`) : un message envoyé par un compte muet est silencieusement
      refusé côté serveur (`PlayerSession.HandleChatMessage`), avec un message "Système" visible
      seulement par l'expéditeur.
    - **Ban IP** (`BannedIpEntity`, distinct du bannissement de compte) : bloque la connexion
      depuis une IP bannie quel que soit le compte utilisé ensuite (vérifié dans
      `AccountService.LoginAsync`, avant même de vérifier les identifiants). L'IP appelante est
      mémorisée sur le compte à chaque connexion réussie (`UserEntity.LastKnownIp`) — c'est cette
      dernière IP connue qui est bannie par l'action "Bannir la dernière IP".
    - **Réinitialisation de profil** (voir GDD/demande utilisateur — "possibilité de reset le
      profil en jeu de quelqu'un") : supprime tous les personnages d'un compte (et leurs
      dépendances) sans toucher au compte/login. Retire d'abord les personnages des
      groupes/guildes (transfert de leadership ou suppression si plus personne, même logique que
      `PartyService.LeaveAsync`) pour respecter les clés étrangères en `DeleteBehavior.Restrict`.
    - **Commandes en jeu réservées modérateur/administrateur/fondateur** (voir
      `PlayerSession.HandleChatCommand`) : `/ban <pseudo> [raison]`, `/mute <pseudo>`,
      `/unmute <pseudo>`, `/nick <pseudo> <nouveau_pseudo>` — tapées dans le tchat, résolues par
      nom de personnage, réponse (confirmation/erreur) visible uniquement par l'expéditeur.
      **Simplification assumée** : un compte banni/mute en jeu n'est pas déconnecté de force s'il
      est déjà connecté — l'effet s'applique au message suivant/à la prochaine connexion.
    - **Panneau "Communauté" dans le Launcher** (voir GDD/demande utilisateur — "le tout peut
      aussi se faire via le launcher [...] seulement pour les admin/fondateur") : bouton dédié
      dans la barre latérale (masqué pour les comptes sans droit), reprenant la liste des
      utilisateurs (pseudo, email, grade, muet, dernière IP) avec toutes les actions ci-dessus
      (grade, ban compte/IP, mute, reset profil, renommage). **Simplification assumée** : pas
      d'image de profil réelle (aucun pipeline d'upload/stockage n'existe) — l'"avatar" est une
      pastille de couleur dérivée du pseudo (déterministe) avec son initiale.
25. ✅ Correctif majeur : combat de groupe désynchronisé ("la synchronisation est comme si elle
    était inexistante", voir retour utilisateur) — le client ne rafraîchissait `combatState`
    QUE lorsqu'il soumettait lui-même une action (réponse HTTP de sa propre requête). Dès qu'un
    combat est partagé entre plusieurs joueurs humains (voir point 23 — combat de groupe), le
    tour d'un allié ne provenait d'aucune requête de CE client : rien ne le déclenchait jamais,
    et l'affichage restait figé indéfiniment, y compris une fois redevenu son tour (puisque
    `combatState.CurrentTurnCombatantId` lui-même était périmé). Corrigé en sondant
    `GET /api/combat/{id}` toutes les 0,35s pendant tout combat en cours (`UpdateCombat`, voir
    Client/Program.cs) — déjà exposé côté serveur (utilisé jusqu'ici uniquement pour les
    appairages d'arène) mais jamais interrogé pendant un combat normal.
    - Mesuré au passage (voir retour utilisateur sur la latence) : le détour par l'IP publique
      pour tester en local sur la machine du serveur elle-même (redirection de ports + retour via
      le routeur, "NAT hairpin") n'ajoute qu'environ 5-10ms par rapport à `localhost` sur cette
      installation — négligeable, la latence rapportée venait bien du défaut de sondage ci-dessus,
      pas du réseau.
26. ✅ Correctif : joueur bloqué après la fin d'un combat de groupe qu'il n'a pas terminé lui-même
    (voir retour utilisateur — "bloqué contre la cible, ça ne fonctionne pas"). Deux causes
    combinées, révélées par le sondage ajouté au point 25 :
    - `CombatService.SubmitActionAsync` retirait la session du `CombatSessionStore` dès la fin du
      combat — un coéquipier dont le client sondait encore l'état après coup recevait "Combat
      introuvable" au lieu d'un état "terminé", et restait bloqué sur l'écran de combat figé
      indéfiniment (toute tentative d'action échouait ensuite, puisque le combat n'existait plus).
      Corrigé : une session terminée (`CombatSession.FinishedAtUtc`) n'est plus retirée
      immédiatement, mais purgée après 3 minutes (`CombatSessionStore.PruneFinished`, appelée
      opportunément à chaque nouveau combat plutôt que via une tâche d'arrière-plan dédiée).
    - Le butin de victoire (`LootId`) n'était renvoyé qu'au joueur ayant porté le coup final —
      jamais persisté sur la session elle-même. Un coéquipier récupérant l'état via sondage ne
      voyait donc jamais le butin partagé. Corrigé en stockant `LootId` sur `CombatSession`.
    - Corrige au passage la même classe de bug pour l'Arène (2+ joueurs par équipe) et le PvP,
      qui partagent la même mécanique de fin de combat.
27. ✅ Corrections d'interface du Launcher (retours utilisateur) :
    - Menus déroulants (grade, format d'arène, etc.) illisibles — le popup du `ComboBox`
      n'héritait pas du thème sombre (fond clair par défaut) alors que le texte héritait quand
      même du `Foreground` clair du contrôle fermé, donnant du texte clair sur fond clair. Corrigé
      par un style `ComboBoxItem` explicite (fond sombre, texte clair, surbrillance au survol).
    - Barre de défilement ajoutée (toujours visible, pas seulement "Auto") sur le panneau
      d'actions du panneau Communauté.
28. ✅ Correctif : sondage du butin + timers de combat/gains + connexion persistante du Launcher
    (voir GDD/demande utilisateur) :
    - **Vrai correctif du choix d'objet bloqué en groupe** : comme pour le combat (point 25), le
      client ne rafraîchissait `activeLoot` que via sa PROPRE réclamation — un coéquipier
      n'apprenait jamais qu'un autre joueur avait choisi (ni qu'un timer avait résolu le butin,
      voir ci-dessous) tant qu'il ne réclamait pas lui-même. Corrigé par le même principe de
      sondage périodique (`GET /api/loot/{id}` toutes les 0,35s) que le combat.
    - **Timer de 10 secondes entre chaque tour** (`GameInfo.CombatTurnTimeoutSeconds`) : un
      combattant humain qui n'agit pas dans le délai voit son tour automatiquement passé
      (`CombatService.AutoPassIfTimedOutAsync`, appelé par le nouveau `CombatTimeoutScheduler`,
      tâche de fond vérifiée chaque seconde — même mécanique que `DailyDigestScheduler`). Compte
      à rebours affiché en combat (approximatif côté client, le serveur fait foi).
    - **Timer de 10 secondes pour le choix des gains** (`GameInfo.LootChoiceTimeoutSeconds`) :
      un butin non entièrement réclamé est résolu automatiquement après ce délai
      (`LootService.ResolveTimedOutAsync`, même `CombatTimeoutScheduler`) — les joueurs n'ayant
      pas choisi ne remportent simplement aucun objet (`LootRoll.Resolve` tolère déjà les
      réclamations partielles). Compte à rebours affiché sur l'écran de butin.
    - **Connexion persistante du Launcher** (voir GDD/demande utilisateur — "on y reste connecté
      jusqu'à ce que l'on s'y déconnecte") : le jeton de session est persisté
      (`GameSettings.SessionToken`) et revalidé au démarrage via le nouvel endpoint léger
      `GET /api/account/session` — évite de redemander les identifiants à chaque lancement.
      Effacé uniquement à la déconnexion explicite, ou automatiquement si la revalidation échoue
      (serveur redémarré depuis — `SessionTokenStore` vit en mémoire — ou compte banni/supprimé
      entre-temps).

29. ✅ Correctif : "connexion au serveur impossible" pour le premier joueur à avoir choisi son
    butin (voir GDD/demande utilisateur — "la première personne à avoir fait le choix a connexion
    au serveur impossible") :
    - Même bug que celui déjà corrigé pour le combat (point 25/`CombatSessionStore`), mais côté
      butin : `LootService.ResolveAsync` retirait la session du `LootSessionStore` dès que tous
      les joueurs éligibles avaient réclamé un objet. Le sondage périodique du butin (ajouté au
      point 28 pour corriger "le joueur qui n'a pas donné le dernier coup ne peut pas choisir")
      continuait ensuite d'interroger `GET /api/loot/{id}` côté premier réclamant — recevait un
      404 (butin introuvable), que le client traduisait à tort en "Connexion au serveur
      impossible."
    - Corrigé à l'identique du combat : ajout de `LootSession.ResolvedAtUtc`, renseigné par
      `ResolveAsync` au lieu de retirer la session immédiatement ; `LootSessionStore` conserve
      désormais la session résolue et la purge seulement après une rétention de 3 minutes
      (`PruneResolved`, appelée à chaque nouvel ajout — même mécanique que `CombatSessionStore.
      PruneFinished`).
30. ✅ UI Métiers + notification de montée de niveau, et Passe de Niveau (voir GDD/demande
    utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de chaque métier" +
    "une petite notification quand on monte un niveau dans un métier" + "un pass de niveaux de
    joueur ou chaque xp que tu gagne est ajouté dedans aussi ... si il paie le pass premium alors
    il auront accès à des trucs plus exclusif") :
    - Panneau Métiers (touche B) listant les 8 métiers avec niveau/XP actuels, y compris ceux
      jamais pratiqués (voir `ProfessionService.GetSummaryAsync`, `/api/professions/{characterId}`).
    - Notification générique en haut de l'écran (`PushSystemToast`/`DrawSystemToasts`, même
      mécanique que les toasts de tchat existants) à chaque montée de niveau de métier (récolte
      ou craft).
    - Passe de Niveau (touche N, `Server/World/BattlePassService.cs`) : progression alimentée par
      la MÊME XP que le niveau de personnage (quêtes, combat PvE solo/groupe — voir les appels
      jumeaux à `CharacterProgressionService.GrantExperience`), avec récompense automatique à
      chaque palier (or, gemmes, objet tous les 5 paliers). Palier premium (500 gemmes, achat
      dans le panneau) débloquant des récompenses nettement plus généreuses (objets jusqu'à
      Rare uniquement — voir Docs/Items.md, jamais d'objet réservé admin ni de créature), avec
      rattrapage rétroactif des paliers déjà atteints au moment de l'achat.

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
| `DISCORD_BOT_TOKEN`          | Jeton du bot Discord (annonces + link de compte, voir `Server/Discord/`). |
| `DISCORD_APPLICATION_ID`     | Identifiant d'application Discord — requis pour enregistrer la commande `/link`. |
| `DISCORD_GUILD_IDS`          | Serveurs Discord (guildes) où `/link` est actif, séparés par des virgules. |
| `DISCORD_ROLE_ID_<GRADE>`    | Rôle Discord attribué automatiquement par grade (`JOUEUR`/`VIP`/`TESTEUR`/`AMI`/`MODERATEUR`/`FONDATEUR`), voir `.env.exemple`. |

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
