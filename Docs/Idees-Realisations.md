# Suivi de réalisation — Docs/Idees.md

Résultat de la session du 2026-08-26 : implémentation de la liste d'idées de `Docs/Idees.md`
(elle-même établie après audit du code réel, voir l'en-tête de ce fichier). Chaque idée du
fichier source est reprise ci-dessous avec `[x]` (faite) ou `[ ]` (pas faite), suivie du détail
de ce qui a réellement été modifié/ajouté — fichiers, migrations, endpoints. L'ordre suit
exactement celui de `Docs/Idees.md`.

> Vérification : chaque changement de code a été validé par une compilation ciblée du projet
> concerné (`dotnet build`, RID `linux-x64` pour Server/Client/Shared/Database/Launcher, RID
> `win-x64` + `EnableWindowsTargeting=true` pour MonsterEditor/MapEditor/AdminPanel/Installer —
> ces quatre derniers ciblant Windows, leur XAML se compile mais n'a pas pu être testé
> visuellement depuis cet environnement Linux). Aucune vérification de bout en bout avec un vrai
> serveur/client lancés (hors de portée de cette session) — à confirmer par un humain.

## Gameplay

- [ ] **Arbre de talents/compétences général** — non fait, comme prévu (hors scope, système neuf
  qui mérite sa propre session de conception).

- [x] **Capacité spéciale dédiée pour Tank/Assassin/Support/Invocateur/Berserker** —
  `Server/World/Combat/CombatEngine.cs` (`ResolveSpecialAbility`/`ResolveUltimateAbility`) :
  Tank = coup + auto-soin de 25 % des dégâts infligés ; Assassin = ×2.5 au lieu de ×1.8 si la
  cible est sous 50 % PV ; Berserker = multiplicateur croissant selon ses PV manquants ;
  Support = pose un bonus de dégâts en attente sur la prochaine attaque de base d'un allié
  (nouveau champ `Combatant.NextAttackBonusAmount`, consommé dans `ResolveAttack`) ; Invocateur =
  frappe principale + onde de choc à mi-dégâts sur les ennemis orthogonalement adjacents (pas
  d'obstacle persistant à faire participer au tour par tour, jugé hors scope). IA mise à jour
  (`RunAiTurn`) pour utiliser la capacité Support en priorité comme le Soigneur.

- [x] **Garde-fou anti-auto-appairage en arène** — `Server/World/Combat/ArenaQueueService.cs` :
  dédoublonnage par `UserId` en plus de `CharacterId` (un compte ne peut plus avoir deux tickets
  simultanés pour un même format, ce qui empêche structurellement de se retrouver sur deux
  équipes opposées).

- [x] **Vrai lobby d'arène** — implémenté sous la forme "groupe entier rejoint la file comme un
  bloc d'équipe" plutôt qu'une invitation P2P séparée (comme proposé dans `Idees.md`) :
  `ArenaQueueService.EnqueueGroupAndTryMatch` (fait face à un autre groupe ou complète avec des
  joueurs solo), nouvel endpoint `POST /api/pvp/arena/queue-party`
  (`Shared/Models/Combat/QueueGroupForArenaRequest.cs`), chaque membre engageant son équipe
  active (`EquippedSlot`).

- [x] **Verrou contre la double création de combat de groupe** — `CombatSessionStore` expose
  `GetPartyCreationLock(partyId)` (un `SemaphoreSlim` par groupe), utilisé dans
  `CombatService.StartAsync` autour de la vérification+création de session PvE. Le
  "réajustement du nombre d'ennemis" mentionné dans la même idée n'a pas été touché : le
  comportement existant (ajouter le membre à l'équipe 0 sans recalculer l'équipe 1) s'est avéré
  déjà correct à la relecture, pas un bug à corriger.

- [x] **Pathfinding évitant les obstacles** — IA de combat : `CombatEngine.FindStepTowardTarget`
  (BFS 4-directionnel sur la grille 7x7, en excluant obstacles/combattants). Déplacement
  extérieur : `Client/Program.cs` `BuildOrthogonalPath` remplacé par un BFS 4-directionnel sur la
  carte 50x50 excluant les cases bâtiment.

- [x] **Probabilité de rencontre sauvage dépendante du terrain** —
  `Client/World/WorldMap.cs` : nouvel enum `TerrainType` + `GetTerrain(x,y)`, les 3 variantes
  d'herbe (jusqu'ici cosmétiques) pèsent désormais 0.08/0.11/0.16 dans
  `Client/Program.cs` (`WildEncounterChanceByTerrain`), chemin/étang restant à 0.

- [x] **Effets différenciés par objet donné à une créature** — `Server/World/MonsterCareService.cs` :
  "Élixir de force" (déjà craftable par l'Alchimiste, mais sans aucun effet mécanique jusqu'ici)
  accorde désormais +10 EV Attaque permanent au lieu de l'XP fixe. Portée volontairement réduite
  par rapport à la Proposition (un enum `GiveEffect` générique sur `ItemEntity` n'a pas été
  ajouté) — un seul objet différencié comme preuve du mécanisme plutôt qu'un nouveau système
  complet inventé sans contenu à y mettre.

- [x] **Contrepartie en créature côté joueur ciblé (Échange)** — `TradeOfferEntity.RequestedMonsterId`
  (migration `AddTradeOfferRequestedMonster`), `TradeService.ProposeAsync`/`RespondAsync` validés
  côté serveur (appartenance vérifiée à la proposition ET à l'acceptation), affiché côté client
  (`Client/Program.cs`, panneau Échange).

- [x] **Table de butin dédiée aux matériaux de boss** — `Server/World/CombatService.cs` :
  `CombatSession.RoomEncounterType` (renseigné par `StartFromDungeonAsync`), mapping
  `BossMaterialByDungeonName` (Essences élémentaires déjà seedées) accordé en plus du butin
  aléatoire habituel sur une victoire en salle Boss/Boss légendaire.

- [ ] **Embranchements/choix dans la chaîne de quêtes tutoriel** — non fait, comme prévu (le
  mécanisme seul, sans nouveau contenu narratif à y brancher, n'aurait rien apporté de jouable).

- [ ] **Vraie géographie pour les îles volantes/aquatiques** — non fait, comme prévu (gros
  chantier moteur, dépend des sprites réels).

- [ ] **Persistance des PV des créatures entre deux combats** — non fait : question posée à
  l'utilisateur en cours de session, réponse = laisser tel quel (choix assumé, pas un oubli).

## Contenu

- [ ] **Contenu de saison réel** — non fait au sens "nouvelles espèces/donjon". En auditant
  `Server/Persistence/MonsterCatalogSeeder.cs` pour l'enrichir, le bestiaire s'est révélé déjà
  extrêmement complet (~80 espèces, tous rôles de combat et toutes raretés Commun→Divin déjà
  représentés, y compris Tank/Berserker/Invocateur pour lesquels des capacités viennent d'être
  ajoutées ci-dessus) — ajouter encore des espèces aurait été de la redondance plutôt que du
  contenu manquant. Le vrai manque restant est constaté ailleurs : sprites/assets (non fait, voir
  plus bas) et narration/quêtes (hors scope). Un palier cosmétique de fin de Passe de Niveau a
  été ajouté à la place (voir "Récompenses cosmétiques..." plus bas), qui répond à une partie de
  l'esprit de cette idée sans dupliquer un bestiaire déjà riche.

- [x] **Vraie récompense mécanique pour les salles Énigme/Piège/Événement/Salle secrète** —
  `Server/World/DungeonRoomService.cs` : `TriggerTrapAsync` (perte d'or), `ResolvePuzzleAsync`
  (choix binaire résolu serveur, gain/perte d'or), `TriggerEventAsync` (bonus or+XP instantané —
  pas de buff porté sur le reste de l'étage, aucun état de progression d'étage n'existant côté
  serveur pour le stocker), `OpenChestAsync` étendu à `SalleSecrete` (taux d'objet/or supérieurs).
  3 nouveaux endpoints (`trigger-trap`, `resolve-puzzle`, `trigger-event`), câblés côté client
  (`Client/Program.cs`, `UpdateDungeonCorridor`) et dans `GameDataApiClient`.

- [ ] **Vraie scène d'intérieur** — non fait, comme prévu (dépend des sprites réels).

- [ ] **Sprites/textures réels** — non fait, comme prévu (aucun asset graphique disponible dans
  cet environnement — pas un problème de code).

## Technique

- [x] **Rafraîchissement en session de la position des donjons** — `Client/Program.cs` : nouveau
  `dungeonPositionPollClock` (toutes les 120 s en extérieur), appelle
  `RefreshDungeonPositionAsync` déjà existante.

- [x] **Validation serveur de portée/collision sur les déplacements** —
  `Server/Networking/PlayerSession.cs` (`HandlePlayerMove`) : la case ciblée doit être adjacente
  (8 directions) à la dernière position connue, SAUF deux sauts légitimes qui empruntent le même
  packet — le point d'arrivée du Téléporteur (`CapitalSpawnPoint`, calculé via `TownLayout`, même
  formule que `WorldMap.SpawnPosition`) et la position réelle diffusée d'un autre joueur connecté
  (téléport modérateur "localiser un joueur signalé").

- [x] **Authentification admin dédiée pour MonsterEditor/MapEditor** — 6 endpoints gatés côté
  serveur (`POST`/`PUT`/`DELETE` sur `/api/monsters/species` et `/api/dungeons`, paramètre
  `sessionToken` + `AdminAuthService.RequireAdminAsync`, `Server/Program.cs`) — **jusqu'ici ces
  endpoints n'exigeaient absolument aucune authentification**, un vrai trou de sécurité corrigé
  au passage, pas seulement un confort d'outil. Écran de connexion ajouté aux deux outils
  (`MonsterEditor/`/`MapEditor/` : `MainWindow.xaml`, `MainViewModel.cs`,
  `InverseBooleanToVisibilityConverter.cs`, réutilise `POST /api/account/login`), Créer/Modifier/
  Supprimer désactivés (`CanExecute`) tant qu'aucun compte admin/fondateur n'est connecté.

- [x] **Historique de tchat persisté** — nouvelle entité `ChatMessageEntity`
  (`Database/Entities/ChatMessageEntity.cs`, migration `AddChatMessages`), écriture synchrone
  dans `PlayerSession.HandleChatMessage` (canaux Global/Guilde uniquement, pas les messages
  privés — ce sont les deux seuls onglets réels du panneau Tchat), purge opportuniste (>7 jours,
  tirage 1 %). Nouvel endpoint `GET /api/chat/history`, chargé côté client à la première
  ouverture de chaque onglet (`Client/Program.cs`, `UpdateChatPanel`).

- [x] **Rendu visuel de la grille de donjon dans le MapEditor** — `MapEditor/MainWindow.xaml` +
  `MainViewModel.cs` : `RoomVisual`/`DoorLineVisual`, `Canvas` positionné à partir de
  `DungeonRoom.GridX`/`GridY` (déjà calculés côté serveur, jusqu'ici jetés au profit d'une simple
  liste texte), connecteurs de porte Est/Sud, couleur par type de rencontre (même palette que le
  Client).

- [x] **Déconnexion forcée immédiate d'un joueur banni** — `Server/Program.cs`
  (`POST /api/admin/users/{userId}/ban`) appelle désormais `.Kick()` sur toutes les sessions du
  compte visé, aligné sur `POST /api/admin/game/ban` qui le faisait déjà. Le mute reste différé
  (comme prévu par la Proposition).

- [ ] **Vrai désinstallateur MSI** — non fait, comme prévu (toolchain WiX indisponible dans cet
  environnement).

## UI / UX

- [ ] **Vraie image de profil** — partiellement fait, marqué non fait car la partie visible
  utilisateur manque encore. Fait : `UserEntity.AvatarUrl` (migration `AddUserAvatarUrl`),
  endpoint `POST /api/account/avatar` (upload multipart, 2 Mo max, PNG/JPEG, stocké sur disque
  serveur sous `avatars/`, servi en statique via `app.UseStaticFiles`), `AvatarUrl` exposé dans
  `LoginResponse`/`AdminUserSummary`. **Pas fait** : affichage réel de l'image côté
  Launcher/AdminPanel (remplacement de la pastille générée par `AvatarConverters.cs`) et bouton
  d'upload dans l'UI — l'infrastructure serveur est prête, l'écran qui l'utilise reste à faire.

- [x] **Suivi "tutoriel déjà vu"** — `CharacterEntity.HasSeenTutorial` (migration
  `AddCharacterHasSeenTutorial`), endpoint `POST /api/characters/{id}/mark-tutorial-seen`, exposé
  via `CharacterSummary.HasSeenTutorial`. Client (`Client/Program.cs`) : ouverture automatique du
  tutoriel juste après création/sélection de personnage si `false`, marqué vu à la première
  fermeture (F1 ou Échap).

- [x] **Curseur de sélection PNJ multiples** — `Client/Program.cs` : `interiorNpcCursor`, flèches
  gauche/droite pour cycler (guardé par `interiorNpcs.Count > 1` et aucun dialogue en cours),
  indicateur "N/total" affiché uniquement si plusieurs PNJ.

- [x] **Édition du type de monstre dans MonsterEditor** — `MonsterEditor/ViewModels/MainViewModel.cs` :
  `AvailableTypes`/`Type` (miroir exact du pattern `Element`/`Rarity`), `ComboBox` ajouté dans
  `MainWindow.xaml`.

## Autres

- [x] **Notifications Discord (guerre de royaumes + saison)** — `Server/Program.cs` :
  `POST /api/kingdoms/wars/resolve` et `POST /api/seasons/next` appellent désormais
  `DiscordAnnouncer.PostUpdateAsync` en plus du récapitulatif quotidien/de l'annonce admin
  manuelle déjà en place.

- [ ] **Système de réputation/grade militaire + zones PvP sauvage** — non fait, comme prévu
  (système de gameplay neuf entier, à cadrer avec l'utilisateur avant de coder).

- [x] **Récompenses cosmétiques exclusives au-delà de "Rare" dans le Passe de Niveau premium** —
  `Server/World/BattlePassService.cs` : nouveau titre exclusif "Élu du Passe" au palier maximum
  (50) de la piste premium, en plus (pas à la place) de la récompense objet existante à ce
  palier — aucun objet de puissance au-delà de Rare, pour éviter le pay-to-win.

## Migrations EF Core ajoutées cette session

Dans l'ordre, sous `Database/Migrations/` :
1. `AddTradeOfferRequestedMonster` — colonne `RequestedMonsterId` (Guid?) sur `TradeOffers`.
2. `AddChatMessages` — nouvelle table `ChatMessages`.
3. `AddUserAvatarUrl` — colonne `AvatarUrl` (string?) sur `Users`.
4. `AddCharacterHasSeenTutorial` — colonne `HasSeenTutorial` (bool) sur `Characters`.

## Note d'environnement (pour la suite)

`dotnet ef`/`dotnet build` échouaient dans cet environnement (RID auto-détecté `arch-x64` non
reconnu par le SDK .NET 10 preview installé, et une erreur "prune package data" séparée). Recette
qui fonctionne, utilisée pour toute cette session :
- Server/Client/Shared/Database/Launcher (net10.0) :
  `dotnet build <projet> -r linux-x64 --self-contained false -p:AllowMissingPrunePackageData=true`
- MonsterEditor/MapEditor/AdminPanel/Installer (net10.0-windows, WPF — se compile bel et bien
  sur Linux, seul un lancement/test visuel réel nécessite Windows) :
  `dotnet build <projet> -p:EnableWindowsTargeting=true -p:AllowMissingPrunePackageData=true -r win-x64 --self-contained false`
- Migrations : `dotnet tool install --global dotnet-ef` (absent par défaut), puis
  `export AllowMissingPrunePackageData=true && dotnet ef migrations add <Nom> --project Database/Aetheria.Database.csproj --startup-project Database/Aetheria.Database.csproj`
  (MSBuild lit une propriété non définie dans le projet depuis la variable d'environnement du
  même nom, ce qui permet de la faire passer à travers `dotnet ef` sans argument `--` dédié).
