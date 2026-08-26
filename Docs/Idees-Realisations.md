# Suivi de réalisation — Docs/Idees.md

Résultat de la session du 2026-08-26 : implémentation de la liste d'idées de `Docs/Idees.md`
(elle-même établie après audit du code réel, voir l'en-tête de ce fichier). Chaque idée du
fichier source est reprise ci-dessous avec `[x]` (faite) ou `[ ]` (pas faite), suivie du détail
de ce qui a réellement été modifié/ajouté — fichiers, migrations, endpoints. L'ordre suit
exactement celui de `Docs/Idees.md`.

**Mise à jour du 2026-08-27** : reprise des idées précédemment marquées `[ ]` "hors scope"/"trop
grosses" — arbre de talents, embranchements de quêtes, géographie des îles, PvP sauvage +
réputation militaire, vraie scène d'intérieur, fin de l'image de profil. Voir le détail par idée
ci-dessous ; les nouvelles sections sont marquées **(2026-08-27)**. Deux idées restent
volontairement non faites (sprites/textures réels, désinstallateur MSI) — raisons inchangées,
détaillées à leur entrée respective.

> Vérification : chaque changement de code a été validé par une compilation ciblée du projet
> concerné (`dotnet build`, RID `linux-x64` pour Server/Client/Shared/Database/Launcher, RID
> `win-x64` + `EnableWindowsTargeting=true` pour MonsterEditor/MapEditor/AdminPanel/Installer —
> ces quatre derniers ciblant Windows, leur XAML se compile mais n'a pas pu être testé
> visuellement depuis cet environnement Linux). Aucune vérification de bout en bout avec un vrai
> serveur/client lancés (hors de portée de cette session) — à confirmer par un humain.

## Gameplay

- [x] **Arbre de talents/compétences général (2026-08-27)** — arbre partagé (pas par espèce, un
  seul arbre à 9 nœuds) : `Shared/Models/TalentTreeCatalog.cs`, `MonsterEntity.TalentPoints`/
  `UnlockedTalentNodeKeys` (migration `AddMonsterTalents`), +1 point par montée de niveau,
  `Server/World/MonsterTalentService.cs` (`GetStatusAsync`/`UnlockNodeAsync`, validation
  possession/prérequis/points). Bonus appliqués en pourcentage sur les stats de combat
  (`CombatService.BuildTeamCombatantsAsync`, avant le bonus plat de l'équipement). Côté client :
  touche Y depuis la fiche créature ouvre `PanelKind.Talents` (liste des nœuds avec statut
  DEBLOQUE/DISPONIBLE/VERROUILLE, `Client/Program.cs`).

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

- [x] **Embranchements/choix dans la chaîne de quêtes tutoriel (2026-08-27)** —
  `QuestEntity.ChoiceNextQuestId` (migration `AddQuestChoiceNextQuestId`, int? optionnel) : un
  embranchement ponctuel après la quête tutoriel principale, pas un arbre complet, comme prévu
  par la Proposition. Deux nouvelles quêtes ajoutées comme contenu réel du choix
  (`QuestCatalogSeeder`) : "La voie du guerrier" (option par défaut, un nouveau combat) et
  "La voie du marchand" (option alternative, une nouvelle transaction), toutes deux
  `SequenceOrder = 7`. `QuestService.GetActiveQuestAsync` détecte l'embranchement en attente et
  renvoie les deux options (`QuestSummary.IsChoice`) au lieu d'une quête active classique ; nouvel
  endpoint `POST /api/quests/choose` (`ChooseNextQuestAsync`) marque l'option rejetée comme
  complétée sans récompense. Câblé côté client comme deux lignes cliquables dans le panneau de
  quête existant (`Client/Program.cs`, `DrawQuestPanel`) plutôt que dans la boîte de dialogue PNJ
  proposée à l'origine — écart assumé : pas de PNJ existant naturellement rattaché à ce choix,
  le panneau de quête est la surface la plus simple et cohérente pour l'exposer.

- [x] **Vraie géographie pour les îles volantes/aquatiques (2026-08-27)** — pas de nouvelle
  notion d'élévation/eau traversable dans le moteur (toujours hors scope) : une île est une
  `WorldMap` distincte sur la même grille 50x50 (`Client/World/WorldMap.cs`, nouveau constructeur
  `WorldMap(int size, MountKind islandKind)`, palette ciel/océan dédiée), sans bâtiment sauf un
  point "Retour" au point d'apparition partagé. `EnterIsland`/`LeaveIsland`
  (`Client/Program.cs`) réutilisent exactement le mécanisme de téléportation déjà en place entre
  royaumes. Bonus trouvé au passage : `RebuildWorldMapForKingdom` n'appelait jamais
  `connection.SendMove(...)`, désynchronisant silencieusement la position suivie côté serveur
  après chaque téléportation de royaume — corrigé.


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

- [x] **Vraie scène d'intérieur (2026-08-27)** — l'intérieur des bâtiments (hors donjon) est
  maintenant projeté en isométrique avec les mêmes primitives que l'extérieur (`IsoMath`/
  `DrawQuad`, voir `DrawBuilding`) plutôt qu'un aplat de rectangles en coordonnées écran
  relatives : sol en losanges (`Client/Program.cs`, `DrawInteriorScene`), deux murs de fond en
  "L", meubles réinterprétés comme des cases de la grille de la pièce et rendus en petits pavés
  isométriques extrudés, PNJ rendu via `DrawFigure` (même silhouette que les PNJ extérieurs,
  nouveau paramètre optionnel `screenOffset` ajouté à `DrawFigure`/`DrawIsoDiamond` pour
  permettre ce recentrage écran, sans changer le rendu extérieur existant). L'intérieur des
  donjons (`DrawDungeonCorridor`, salle rectangulaire avec portes) n'a volontairement pas été
  touché : sa géométrie et son déplacement (`dungeonPlayerPos` 0..1) sont indépendants et une
  conversion isométrique y aurait demandé de refaire le mouvement en salle, un risque de
  régression déraisonnable pour un gain purement cosmétique.

- [ ] **Sprites/textures réels** — toujours pas fait dans le moteur (décision explicite : aucune
  texture réelle câblée dans le rendu, pour ne pas s'engager sur des assets non produits/achetés).
  À la place **(2026-08-27, complété le même jour)**, `Docs/Image/` contient des maquettes PNG
  générées par code (`generate_concepts.py`, Pillow, couleurs exactes déjà utilisées en jeu :
  `CombatTypeColor`, `CharacterAppearancePalette`, `Npc(...)` de `WorldMap.cs`) pour montrer à
  quoi les sprites pourraient ressembler sans en faire de vrais assets de production — couvre
  désormais un exemple par rôle de monstre (10, voir `MonsterType`), 3 variantes de
  personnalisation du joueur, 4 PNJ nommés et 5 bâtiments visitables, voir `Docs/Image/README.md`.

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

- [x] **Vraie image de profil (2026-08-27)** — partie serveur inchangée (`UserEntity.AvatarUrl`,
  `POST /api/account/avatar`). Partie visible désormais faite : `Launcher/AvatarConverters.cs`
  (`AvatarUrlToBitmapConverter`, téléchargement synchrone + cache mémoire par URL, utilisé comme
  image réelle quand `AvatarUrl` est renseigné, la pastille/initiale générée reste le repli
  sinon), `Launcher/Services/FilePickerService.cs` (sélection de fichier via le `StorageProvider`
  Avalonia), `AccountApiClient.UploadAvatarAsync`, bouton "Changer d'avatar" dans le panneau
  "Compte connecté" (`MainWindow.axaml`/`.xaml.cs`, `MainViewModel.cs`). Colonne avatar ajoutée à
  la liste Communauté du Launcher et à l'AdminPanel (`AdminPanel/AvatarUrlToBitmapConverter.cs`,
  version WPF `BitmapImage.UriSource`). Toujours hors scope, comme précisé dans la Proposition
  d'origine : affichage d'image en jeu (le moteur de rendu maison n'a pas de pipeline de texture
  câblé, voir "Sprites/textures réels" plus haut) — le tchat en jeu reste en texte/tag coloré.

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

- [x] **Système de réputation/grade militaire + zones PvP sauvage (2026-08-27)** — conçu comme
  une file d'attente (pas une attaque directe/embuscade) délibérément, pour éviter le grief sans
  système de consentement/notification : `Server/World/Combat/WildPvpQueueService.cs` (même forme
  que `KingdomWarQueueService`), `Server/Program.cs` (`/api/pvp/wild/queue`,
  `/queue/status`, `/queue/cancel`, `/api/pvp/wild/reputation`), "zone à risque" = distance de
  Manhattan > 15 depuis la capitale, vérifiée côté serveur sur la position réellement suivie du
  joueur (`WorldSessionRegistry.FindByCharacterId`), pas une coordonnée envoyée par le client.
  `CharacterEntity.MilitaryReputation` (migration `AddCharacterMilitaryReputation`), +1 par
  victoire (`CombatService.ApplyArenaResultAsync`), grade calculé par
  `Shared/Models/MilitaryRankCatalog.cs` (6 paliers). Côté client : `PanelKind.WildPvp` (nouveau
  bouton HUD "PVP SAUVAGE"), file d'attente avec sondage périodique identique au panneau Guerre de
  royaumes, affichage du grade/de la réputation (`Client/Program.cs`,
  `UpdateWildPvpPanel`/`DrawWildPvpPanel`, `CombatApiClient`).

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
5. `AddMonsterTalents` (2026-08-27) — colonnes `TalentPoints` (int) et
   `UnlockedTalentNodeKeys` (string) sur `Monsters`.
6. `AddCharacterMilitaryReputation` (2026-08-27) — colonne `MilitaryReputation` (int) sur
   `Characters`.
7. `AddQuestChoiceNextQuestId` (2026-08-27) — colonne `ChoiceNextQuestId` (int?) sur `Quests`.

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
