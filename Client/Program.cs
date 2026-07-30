using System.Net.Http;
using System.Numerics;
using System.Text;
using Aetheria.Client;
using Aetheria.Client.Networking;
using Aetheria.Client.World;
using Aetheria.Engine.Core;
using Aetheria.Engine.Input;
using Aetheria.Engine.Rendering;
using Aetheria.Shared;
using Aetheria.Shared.Enums;
using Aetheria.Shared.Models;
using Aetheria.Shared.Models.Account;
using Aetheria.Shared.Models.Admin;
using Aetheria.Shared.Models.BattlePass;
using Aetheria.Shared.Models.Combat;
using Aetheria.Shared.Models.Premium;
using Aetheria.Shared.Models.WorldBoss;
using Aetheria.Shared.Network.Packets;
using Aetheria.Shared.Settings;
using Silk.NET.Input;
using Silk.NET.OpenGL;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"{GameInfo.Name} Client v{GameInfo.Version}");

var options = LaunchOptions.Parse(args);

// Disposition clavier (voir GDD) : détectée automatiquement, réglable en jeu avec F9 (persistée,
// partagée avec le Launcher). N'affecte que les libellés affichés (ex. "ZQSD" vs "WASD") — les
// codes de touche Silk.NET/GLFW étant basés sur la position physique, WASD fonctionne déjà
// nativement en ZQSD sur un clavier AZERTY, voir KeyboardLayoutResolver.
var gameSettings = GameSettings.Load();
var isAzerty = KeyboardLayoutResolver.ShouldUseAzerty(gameSettings.KeyboardLayout);
Console.WriteLine($"Disposition clavier : {gameSettings.KeyboardLayout} ({(isAzerty ? "ZQSD" : "WASD")}) — F9 pour changer.");

var worldMap = new WorldMap(size: 50);
Console.WriteLine($"Monde généré : {worldMap.Size}x{worldMap.Size} cases, {worldMap.Buildings.Count} bâtiments, " +
    $"{worldMap.Npcs.Count} PNJ, entrée de donjon « {worldMap.DungeonName} » en " +
    $"({worldMap.DungeonEntrance.X}, {worldMap.DungeonEntrance.Y}). Clic gauche pour se déplacer, E pour interagir.");

const int StarterColumns = 5;

/// <summary>Doit rester cohérent avec <c>Server/World/PartyService.MaxMembers</c>.</summary>
const int PartyMaxMembers = 4;

/// <summary>Probabilité de rencontre sauvage par case franchie en zone sauvage (voir GDD, StartWildEncounterOutdoorAsync). Simplification assumée : constante plutôt que dépendante du biome/terrain.</summary>
const double WildEncounterChance = 0.08;

var stateLock = new object();
var gridPosition = new Vector2(worldMap.SpawnPosition.X, worldMap.SpawnPosition.Y);

// Position confirmée par le serveur (mode connecté) — voir GDD ("des animations quand on se
// déplace au lieu d'un TP") : gridPosition se rapproche de celle-ci case par case au lieu de s'y
// téléporter, comme le fait déjà le mode démo hors-ligne pour un chemin cliqué.
var serverConfirmedPosition = gridPosition;
var statusMessage = string.Empty;

var moveQueue = new Queue<(int X, int Y)>();
var isAwaitingServerStep = false;
var animationClock = 0f;
var isPlayerMoving = false;

// Scène active : le monde extérieur, une scène d'intérieur plein écran (bâtiment/donjon), ou la
// sélection du premier compagnon. Voir Docs/README.md pour les limites assumées de chacune.
var sceneMode = SceneMode.Outdoor;
NearbyInteraction? nearbyInteraction = null;

// Tutoriel (voir GDD/demande utilisateur — "ajoute un tutoriel pour expliquer comment jouer") :
// superposé au monde extérieur comme le dialogue PNJ, ouvert/fermé avec F1 à tout moment (pas
// seulement au premier lancement), navigable page par page.
var showTutorial = false;
var tutorialPage = 0;

// Intérieur (bâtiment ou donjon) affiché quand sceneMode == Interior — pas de vraie scène 3D/2D
// détaillée, un fond stylisé + un texte, voir DrawInteriorScene.
var interiorTitle = string.Empty;
var interiorBodyLines = Array.Empty<string>();
var interiorAccent = new Vector4(0.5f, 0.5f, 0.5f, 1f);
var interiorIsDungeon = false;

// Meubles/PNJ d'un intérieur de bâtiment (voir GDD — intérieurs enrichis, BuildingInteriors).
// Vide pour l'intérieur du donjon.
List<InteriorFurniture> interiorFurniture = [];
List<InteriorNpc> interiorNpcs = [];

// Exploration du donjon façon Binding of Isaac (voir GDD/demande utilisateur — "des salles
// aléatoires avec coffre/monstre etc mais où on se déplace nous-même de salle en salle") : les
// salles d'un étage (voir DungeonFloorGenerator côté serveur, disposées sur une grille) sont
// traversées en marchant à travers les portes, pas en appuyant sur Entrée pour "avancer". Combat
// pour les salles Monstre/MiniBoss/Boss/BossLegendaire (déclenché automatiquement à l'entrée),
// coffre d'or pour les salles Coffre (touche E), texte d'ambiance pour les autres types (non
// simulés, voir Docs/README.md). Disponible uniquement en mode connecté (worldMap.DungeonId
// résolu) — le mode démo hors-ligne garde l'ancien stub (un seul combat aléatoire, voir
// StartWildCombatAsync).
var dungeonFloorNumber = 1;
DungeonFloor? dungeonFloor = null;
var dungeonRoomIndex = 0;
Task<DungeonFloor?>? dungeonFloorTask = null;
Task<int?>? dungeonChestTask = null;
string? dungeonRoomMessage = null;

/// <summary>Position du joueur dans la salle courante (0..1 relatif, voir DrawDungeonRoom) — recentrée à chaque changement de salle.</summary>
var dungeonPlayerPos = new Vector2(0.5f, 0.5f);

/// <summary>Voir GDD/demande utilisateur — "dans les donjons ajoute le déplacement au clic" : cible (0..1 relatif à la salle) vers laquelle marcher, effacée à l'arrivée ou dès qu'une touche de déplacement est utilisée.</summary>
Vector2? dungeonClickTarget = null;

/// <summary>Voir GDD/demande utilisateur — "avant de quitter le donjon ajoute un texte pour demander si il est sûr".</summary>
var dungeonExitConfirmOpen = false;

/// <summary>Indices de salles déjà résolues (combat gagné, coffre ouvert, évènement vu) — pour ne pas re-déclencher en repassant.</summary>
HashSet<int> dungeonClearedRooms = [];

/// <summary>
/// Voir GDD/demande utilisateur — évite qu'une défaite ne redéclenche instantanément le même
/// combat en boucle (la salle reste "non nettoyée" après une défaite, voir plus bas) : le combat
/// automatique à l'entrée ne se déclenche qu'une fois par visite de la salle. Remis à -1 en
/// changeant de salle (voir <see cref="TransitionDungeonRoom"/>) — sortir puis revenir permet de
/// retenter, comme la marche à suivre précédente ("une défaite laisse le joueur retenter").
/// </summary>
var dungeonLastAutoFightRoomIndex = -1;

// Aperçu du monstre d'une salle avant de l'affronter (voir GDD/demande utilisateur — "voir les
// ennemis avant de les combattre, comme Pokémon Épée") : chargé paresseusement à l'arrivée dans
// une salle à monstre, indexé par salle pour ne jamais afficher l'aperçu d'une autre salle.
MonsterSpeciesData? dungeonEncounterPreview = null;
Task<MonsterSpeciesData?>? dungeonEncounterPreviewTask = null;
var dungeonEncounterPreviewRoomIndex = -1;

// Combat tactique (voir Server/World/CombatService.cs) : déclenché depuis l'intérieur du
// donjon (Entrée pour affronter un monstre sauvage) — voir GDD section Combats. Grille 7x7,
// actions Move/Attack/Pass/Capture, IA gérée côté serveur entre deux tours joueur.
CombatApiClient? combatApi = null;
CombatSessionState? combatState = null;

/// <summary>Scène à laquelle revenir une fois le combat (et son butin éventuel) terminé — Interior pour le donjon, Outdoor pour une rencontre sauvage en extérieur (voir GDD).</summary>
var combatReturnScene = SceneMode.Interior;
var combatCursorX = 0;
var combatCursorY = 0;
CombatActionType? combatSelectedAction = null;
string? combatMessage = null;
int? captureSphereItemId = null;
Task<CombatResult>? combatStartTask = null;
Task<CombatResult>? combatActionTask = null;

// Sondage périodique de l'état de combat (voir GDD/demande utilisateur — "la synchronisation est
// comme si elle était inexistante, je veux de la synchro instantanée") : en combat de groupe, le
// tour d'un autre joueur (allié dans le même groupe, voir CombatSession.PartyId) n'était jamais
// répercuté sur ce client tant qu'il ne soumettait pas sa propre action — combatState restait
// figé indéfiniment, y compris une fois redevenu son tour. Sondé même pendant son propre tour
// (un adversaire PvP humain peut aussi agir en parallèle), à un rythme court sans marteler le
// serveur.
Task<CombatSessionState?>? combatPollTask = null;
var combatPollClock = 0f;
const float CombatPollIntervalSeconds = 0.35f;

// Butin de victoire (voir GDD — 4 objets à départager, tirage aléatoire en cas d'égalité) :
// affiché après un combat gagné, avant de revenir à la scène d'intérieur.
LootSessionState? activeLoot = null;
var lootCursor = 0;
Task<LootSessionState?>? lootTask = null;
string? lootMessage = null;

// Sondage périodique de l'état du butin (voir GDD/demande utilisateur — un coéquipier peut
// réclamer, ou le serveur peut résoudre automatiquement après le délai imparti, voir
// GameInfo.LootChoiceTimeoutSeconds, sans qu'aucune action de CE client ne le déclenche).
var lootPollClock = 0f;

// Dialogue PNJ, superposé au monde extérieur (le déplacement se fige tant qu'il est ouvert).
Npc? activeDialogueNpc = null;
var dialogueLineIndex = 0;

// Voir GDD/demande utilisateur — "affichage de quête à gauche" (ex. le forgeron qui explique ce
// qu'il lui faut) : un panneau discret, persistant à l'écran une fois renseigné (contrairement
// aux dialogues/panneaux modaux), jusqu'à fermeture explicite (touche Q) ou nouvelle quête.
string? questTitle = null;
List<string> questLines = [];

// Voir GDD/demande utilisateur — "on doit aller voir le forgeron pour fabriquer, pas le faire en
// une touche où on veut" : la liste de craft du Forgeron vit maintenant dans son propre panneau
// (PanelKind.Craft, ouvert seulement en parlant à l'Apprenti forgeron), complètement découplée du
// panneau de quête d'histoire — voir UpdateCraftPanel/DrawCraftPanel.
List<RecipeSummary> forgeronRecipes = [];
List<(string Text, int RecipeIndex)> craftRows = [];
const float CraftPanelWidth = 460f;
var questRecipeCursor = 0;
string? questMessage = null;
string? craftMessage = null;
Task<List<RecipeSummary>>? questRecipeTask = null;

// Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui
// expliquent le jeu" et "une histoire avec des dialogues cohérents".
QuestSummary? activeStoryQuest = null;

/// <summary>Voir GDD/demande utilisateur — masquage explicite du panneau de quête par le joueur (touche Q), distinct de "rien à afficher" — voir <see cref="ToggleQuestPanel"/>.</summary>
var isQuestPanelHidden = false;

// Voir GDD/demande utilisateur — "un UI pour afficher TOUTES les quêtes en cours et en choisir 1
// à épingler pour qu'elle soit affichée à gauche" (touche J). Le système de quêtes ne suit qu'une
// seule quête d'histoire à la fois (voir QuestService — chaîne linéaire, GDD/README.md pour cette
// limite assumée), donc cette liste ne contient jamais plus d'une entrée pour l'instant — mais
// c'est déjà une vraie liste (pas juste renommée), prête pour plusieurs quêtes simultanées si ce
// système évolue.
var questListCursor = 0;
var combatVictoryQuestFired = false;
var lastSubmittedCombatAction = CombatActionType.Pass;

// Voir GDD/demande utilisateur — "guerre de territoire... pour que les joueurs de sa team
// puissent aller faire des quêtes de minage" : panneau ouvert en entrant dans la mine du royaume
// (voir KingdomBiome.MineName) montrant qui la contrôle actuellement (peut avoir changé de main).
var isMinePanelOpen = false;
Task<(TerritorySummary? Territory, ShopItem? Ore, int? MyKingdomId)>? mineLoadTask = null;
TerritorySummary? mineTerritory = null;
ShopItem? mineOreItem = null;
int? myKingdomId = null;
string? mineMessage = null;
Task<ProfessionActionResponse?>? mineGatherTask = null;

// Voir GDD/demande utilisateur — "guerre de territoire... des bâtiments (mine, champs etc)" : le
// Champ récolte du Blé, avec la même mécanique de capture/contrôle de territoire que la Mine
// (voir LoadFieldInfoAsync, GameDataApiClient.GatherCropAsync).
var isFieldPanelOpen = false;
Task<(TerritorySummary? Territory, ShopItem? Crop)>? fieldLoadTask = null;
TerritorySummary? fieldTerritory = null;
ShopItem? fieldCropItem = null;
string? fieldMessage = null;
Task<ProfessionActionResponse?>? fieldGatherTask = null;

// Sélection du starter (voir Server/World/StarterService.cs) : Introduction (texte narratif) ->
// Choosing (grille de ~10 créatures communes) -> Confirming (gros plan animé + lore) -> Sending
// (appel HTTP en cours) -> retour à Confirming en cas d'échec, ou Outdoor en cas de succès.
var starterStage = StarterStage.Introduction;
List<MonsterSpeciesData> starterChoices = [];
var starterCursor = 0;
int? starterConfirmIndex = null;
var starterAnimClock = 0f;
string? starterErrorMessage = null;
Task<StarterChoiceResponse>? starterRequestTask = null;
StarterApiClient? starterApi = null;

// Création de personnage (voir GDD) : Name (saisie du nom) -> Appearance (personnalisation,
// aperçu animé) -> ClassKingdom -> Confirm -> Sending. Pas de vraie caméra 3D (le moteur est
// isométrique 2D) : l'effet "caméra dynamique" est approximé par une rotation/zoom simulés sur
// l'aperçu — voir DrawCharacterPreview et Docs/README.md pour cette limite assumée.
var createStage = CreateStage.Name;
var createName = string.Empty;
var createClassIndex = 0;
var createKingdomIndex = 0;
var createSkinIndex = 0;
var createHairStyleIndex = 0;
var createHairColorIndex = 0;
var createClothesColorIndex = 0;
var createAccessoryIndex = 0;
var createAppearanceField = 0;
var createPreviewClock = 0f;
string? createErrorMessage = null;
Task<CreateCharacterResult>? createTask = null;
var classValues = Enum.GetValues<CharacterClass>();
var kingdomValues = Enum.GetValues<KingdomType>();

GameConnection? connection = null;
CharacterApiClient? characterApi = null;
GameDataApiClient? gameDataApi = null;

// Autres joueurs connectés (voir GDD — visibilité globale même hors groupe) : positions reçues du
// serveur via PlayerJoined/PlayerPositionUpdate/PlayerLeft (diffusion TCP, voir PlayerSession côté
// serveur), protégées par stateLock comme gridPosition puisque mises à jour depuis le thread de
// réception réseau et lues depuis le thread de rendu.
var remotePlayers = new Dictionary<Guid, RemotePlayer>();

// Panneaux en jeu (voir GDD — boutons Inventaire/Guilde/Boutique) : superposés au monde
// extérieur comme le dialogue PNJ, ouverts via I/G/B, fermés via Échap.
var activePanel = PanelKind.None;
List<InventoryItemSummary> inventoryItems = [];
// Voir GDD/demande utilisateur — "les items dépassent, ajoute une barre de scroll dans
// l'inventaire" : indice du premier objet visible, avec un défilement clavier/molette.
var inventoryScrollOffset = 0;
GuildSummary? myGuild = null;
var guildLoaded = false;

// Tchat global/guilde et grade du joueur (voir GDD/demande utilisateur — "un tchat global, un
// tchat de guilde, une liste des joueurs en ligne avec leur grade"). Historique borné en mémoire
// uniquement (pas de persistance des messages), reçu/renseigné par le serveur via
// ChatMessagePacket (voir PlayerSession.HandleChatMessage côté serveur).
var myRank = UserRank.Joueur;

/// <summary>Voir GDD/demande utilisateur — "le panel admin en jeu est pour les admins" : flag technique IsAdmin, distinct du grade Fondateur, donnant lui aussi accès au panel admin en jeu (F2).</summary>
var myIsAdmin = false;
var chatChannel = ChatChannel.Global;
var chatTextInput = string.Empty;
var chatMessages = new List<ChatLine>();
const int MaxChatLines = 100;

/// <summary>Voir GDD/demande utilisateur — "discussion privée" avec un ami (voir DrawFriendsPanel) : non-null tant qu'on chuchote à ce joueur, prioritaire sur <see cref="chatChannel"/> à l'envoi.</summary>
string? chatWhisperTarget = null;

// Voir GDD/demande utilisateur — "afficher les messages du tchat transmis en bas à droite" :
// notifications éphémères en plus du panneau Tchat (T), affichées même si celui-ci est fermé.
var chatToasts = new List<(ChatLine Line, DateTime ExpiresAtUtc)>();
const int MaxChatToasts = 5;
var chatToastLifetime = TimeSpan.FromSeconds(6);

// Voir GDD/demande utilisateur — "ajoute une petite notification quand on monte un niveau dans un
// métier" : notification générique en haut de l'écran, réutilisable pour d'autres évènements de
// progression (voir PushSystemToast) — même mécanique que chatToasts, position/durée différentes.
var systemToasts = new List<(string Text, Vector4 Color, DateTime ExpiresAtUtc)>();
const int MaxSystemToasts = 4;
var systemToastLifetime = TimeSpan.FromSeconds(4);

void PushSystemToast(string text, Vector4 color)
{
    lock (stateLock)
    {
        systemToasts.Add((text, color, DateTime.UtcNow + systemToastLifetime));
        if (systemToasts.Count > MaxSystemToasts)
        {
            systemToasts.RemoveAt(0);
        }
    }
}

// Voir GDD/demande utilisateur — "panel admin en jeu... afficher un message en haut de l'écran
// en gros à tout les joueurs" et "transformer le skin de tout les joueurs en panneau [...]
// pendant 5min" : reçus via AdminEffectPacket (voir GameConnection.AdminEffectReceived), affichés
// tant que non expirés plutôt que sur un évènement ponctuel (le rendu tourne en continu).
string? adminBannerMessage = null;
var adminBannerExpiresAtUtc = DateTime.MinValue;
var signModeExpiresAtUtc = DateTime.MinValue;
var woodPanelColor = new Vector4(0.55f, 0.38f, 0.22f, 1f);
var woodPanelOutline = new Vector4(0.35f, 0.22f, 0.12f, 1f);
// Voir GDD/demande utilisateur — "un téléporteur pour se déplacer de ville en ville mais notre
// team ne change pas" : liste des royaumes autres que le nôtre (voir currentKingdom, mis à jour
// par RebuildWorldMapForKingdom), l'équipe/l'inventaire restent des données serveur inchangées.
var isTeleportPanelOpen = false;
var teleportCursor = 0;
var currentKingdom = KingdomType.Nature;

var isAdminPanelOpen = false;
var adminPanelCursor = 0;
var adminPanelTyping = false;
var adminPanelTextInput = string.Empty;
string? adminPanelMessage = null;
Task<AdminGameActionResponse>? adminPanelActionTask = null;

/// <summary>
/// Libellés des commandes du panel admin en jeu (voir <see cref="UpdateAdminGamePanel"/>/
/// <see cref="DrawAdminGamePanel"/>) : les indices 0, 2, 3 et 4 demandent une saisie, 1 s'exécute
/// immédiatement. Fonction (pas un tableau figé) car l'indice 4 — voir GDD/demande utilisateur,
/// "le fondateur ajoute un bouton que seul eux peuvent voir" — n'apparaît que pour ce grade.
/// </summary>
string[] AdminPanelCommands() => myRank == UserRank.Fondateur
    ?
    [
        "MESSAGE A TOUS (texte)",
        "MODE PANNEAU 5 MIN (aucune saisie)",
        "DONNER UN OBJET (perso;id;qte)",
        "EXPULSER (nom du personnage)",
        "BANNIR (nom du personnage)",
        "TRANSFORMER EN PANNEAU (nom du personnage)",
        "DONNER UN MONSTRE (perso;espece)",
        "NIVEAU MAX EQUIPE (nom du personnage)",
        "DONNER DE L'ARGENT (perso;montant)",
        "DONNER DE L'XP (perso;montant)",
        "DEFINIR NIVEAU (perso;niveau)",
        "DEBANNIR (nom du personnage)",
        "INVOQUER BOSS MONDIAL (nom;pv)",
        "DONNER DES GEMMES (perso;montant)",
        "PROMOUVOIR/RETROGRADER ADMIN (nom du personnage)",
    ]
    :
    [
        "MESSAGE A TOUS (texte)",
        "MODE PANNEAU 5 MIN (aucune saisie)",
        "DONNER UN OBJET (perso;id;qte)",
        "EXPULSER (nom du personnage)",
        "BANNIR (nom du personnage)",
        "TRANSFORMER EN PANNEAU (nom du personnage)",
        "DONNER UN MONSTRE (perso;espece)",
        "NIVEAU MAX EQUIPE (nom du personnage)",
        "DONNER DE L'ARGENT (perso;montant)",
        "DONNER DE L'XP (perso;montant)",
        "DEFINIR NIVEAU (perso;niveau)",
        "DEBANNIR (nom du personnage)",
        "INVOQUER BOSS MONDIAL (nom;pv)",
    ];

// Voir GDD/demande utilisateur — "ajouter les amis (online/offline, discussion privée, niveau,
// équipe équipée)" : panneau Amis (touche F).
List<FriendSummary> friendsList = [];
List<FriendRequestSummary> friendPendingRequests = [];
var friendsLoaded = false;
var friendCursor = 0;
var friendAddMode = false;
var friendTextInput = string.Empty;
string? friendMessage = null;
Task<List<FriendSummary>>? friendListTask = null;
Task<List<FriendRequestSummary>>? friendPendingTask = null;
Task<AdminGameActionResponse>? friendActionTask = null;

// Voir GDD/demande utilisateur — "shop avec des gems" : gemmes (monnaie premium), conversion de
// pièces, palier de grade (bonus XP/or cosmétique) et pass d'emplacement de personnage. L'achat
// de gemmes contre argent réel est affiché mais désactivé (voir GDD, "bloque la page pour le
// moment") — aucune passerelle de paiement n'est branchée.
// Voir GDD/demande utilisateur — "quand on clique sur un pseudo on a ces informations" : fiche
// affichée pour les pseudos reconnus comme créateurs du jeu (voir CreatorCredits) — les autres
// joueurs restent non cliquables (pas de fiche à afficher pour eux).
string? creatorCardTarget = null;

PremiumStatus? premiumStatus = null;
string? premiumMessage = null;
Task<PremiumStatus?>? premiumLoadTask = null;
Task<ShopPurchaseResponse>? premiumActionTask = null;

// Voir GDD/demande utilisateur — "un endroit pour modifier son profil (description, item à
// montrer, titre, grade)" : panneau Profil (touche U), toujours le sien propre pour cette version
// (pas de consultation du profil d'un autre joueur — voir Docs/README.md).
ProfileSummary? myProfile = null;
var profileEditMode = false;
var profileTextInput = string.Empty;
string? profileMessage = null;
Task<ProfileSummary?>? profileLoadTask = null;
Task<ProfileSummary?>? profileActionTask = null;

// Voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher".
List<LeaderboardRow> leaderboardRows = [];
var leaderboardCategoryCursor = 0;
Task<List<LeaderboardRow>>? leaderboardLoadTask = null;
LeaderboardCategory[] leaderboardCategories =
[
    LeaderboardCategory.Pvp, LeaderboardCategory.Richesse, LeaderboardCategory.Metiers, LeaderboardCategory.MonstresCaptures, LeaderboardCategory.Donjons,
];

// Voir GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de chaque métier".
List<ProfessionSummary> professionRows = [];
Task<List<ProfessionSummary>>? professionLoadTask = null;

// Voir GDD/demande utilisateur — "un pass de niveaux de joueur ... si il paie le pass premium alors il auront accès à des trucs plus exclusif".
BattlePassStatus? battlePassStatus = null;
Task<BattlePassStatus?>? battlePassLoadTask = null;
Task<ShopPurchaseResponse>? battlePassPurchaseTask = null;
string? battlePassMessage = null;

// Voir GDD/demande utilisateur — "un boss monde... barre de vie... leaderboard du boss actuel et de toujours".
WorldBossStatus? worldBossStatus = null;
Task<WorldBossStatus?>? worldBossLoadTask = null;
Task<WorldBossAttackResponse>? worldBossAttackTask = null;
List<WorldBossLeaderboardRow> worldBossCurrentLeaderboard = [];
List<WorldBossLeaderboardRow> worldBossAllTimeLeaderboard = [];
Task<List<WorldBossLeaderboardRow>>? worldBossLeaderboardLoadTask = null;
var worldBossShowAllTime = false;
string? worldBossMessage = null;

// Recherche/création de guilde (voir GDD — panneau Guilde : rejoindre/rechercher/créer).
var guildMode = GuildPanelMode.None;
var guildTextInput = string.Empty;
List<GuildSummary> guildSearchResults = [];
var guildSearchCursor = 0;
var guildSearchDone = false;
Task<List<GuildSummary>>? guildSearchTask = null;
Task<GuildSummary?>? guildActionTask = null;
string? guildActionMessage = null;
List<ShopItem> shopCatalog = [];
var shopCursor = 0;
string? shopMessage = null;
Task<ShopPurchaseResponse>? shopBuyTask = null;

// Voir GDD/demande utilisateur — "dans la marchande, un UI pour l'achat/vente d'objet" : Tab
// bascule Achat/Vente, la Vente parcourt l'inventaire (au lieu du catalogue) et rapporte moins
// que de déposer l'objet à l'Hôtel des ventes (voir ShopService.SellAsync).
var shopSellMode = false;
var shopSellCursor = 0;

// Hôtel des ventes entre joueurs (voir GDD/demande utilisateur — panneau ouvert directement en
// entrant dans le bâtiment du même nom, pas via un dialogue).
List<AuctionListingSummary> auctionListings = [];
Task<List<AuctionListingSummary>>? auctionLoadTask = null;
Task<AuctionResponse>? auctionActionTask = null;
string? auctionMessage = null;
var auctionCursor = 0;
var auctionSellMode = false;
var auctionSellCursor = 0;
var auctionSellPrice = 10L;

// Groupe (voir GDD — bouton Groupe, XP partagée, visibilité globale même hors groupe).
PartySummary? myParty = null;
var partyLoaded = false;
var partyJoinPromptOpen = false;
var partyJoinInput = string.Empty;
string? partyMessage = null;
Task<PartySummary?>? partyActionTask = null;
var partyCodeCopied = false;

// Gestion des créatures (voir GDD — UI montres : monter de niveau, objet à donner).
List<MonsterInstanceData> ownedMonsters = [];
Dictionary<int, MonsterSpeciesData> speciesById = [];
var monsterCursor = 0;
var monstersLoaded = false;
var monsterGiveItemMode = false;
var monsterGiveItemCursor = 0;
Task<MonsterInstanceData?>? monsterGiveItemTask = null;
Task<MonsterInstanceData?>? monsterTeamToggleTask = null;
string? monsterMessage = null;

// Voir GDD/demande utilisateur — "les items équipés peuvent donner des avantages à nos monstres".
var monsterEquipMode = false;
var monsterEquipCursor = 0;
Task<MonsterInstanceData?>? monsterEquipTask = null;

// Arène classée (voir GDD — formats 1v1/2v2/3v3/4v4, ligues ELO). File d'attente serveur
// (ArenaQueueService), sondée régulièrement tant que le joueur attend un appairage.
var arenaFormats = Enum.GetValues<ArenaFormat>();
var arenaFormatCursor = 0;
var arenaQueued = false;
var arenaPollClock = 0f;
string? arenaMessage = null;
Task<bool>? arenaQueueTask = null;
Task<ArenaQueueStatus?>? arenaPollTask = null;
Task<CombatSessionState?>? arenaMatchStateTask = null;

// Voir GDD/demande utilisateur — "il y a un bâtiment nommé Guerre... une UI pour dire quand on
// est prêt, si on est prêt ça va chercher un match contre les autres camps". Même mécanique de
// file d'attente sondée que l'Arène (voir KingdomWarQueueService côté serveur).
var isWarRoomOpen = false;
var warReady = false;
var warPollClock = 0f;
string? warMessage = null;
Task<bool>? warQueueTask = null;
Task<ArenaQueueStatus?>? warPollTask = null;
Task<CombatSessionState?>? warMatchStateTask = null;
List<KingdomWarStanding> warStandings = [];
Task<List<KingdomWarStanding>>? warStandingsTask = null;

// Voir GDD/demande utilisateur — "classement de team (le meilleur de la team ombre etc), visible
// seulement si on est dans la même équipe" : royaume résolu côté serveur, jamais choisi par ce
// client (voir GameDataApiClient.GetKingdomLeaderboardAsync).
List<LeaderboardRow> warKingdomLeaderboard = [];
Task<List<LeaderboardRow>>? warKingdomLeaderboardTask = null;

// Voir GDD/demande utilisateur — "ajoute un UI pour les kingdom".
List<KingdomData> kingdomPanelData = [];
List<TerritorySummary> kingdomPanelTerritories = [];
Task<(List<KingdomData> Kingdoms, List<TerritorySummary> Territories)>? kingdomPanelLoadTask = null;

// Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp", puis "propose un
// pvp, si la personne est en team tout les membres doivent accepter" : invitation reçue (bouton
// DUEL ou "/duel <pseudo>" dans le tchat pour en envoyer une), avec une limite de temps miroir de
// celle du serveur (voir DuelInviteService.InviteLifetime, 30s) pour ne pas laisser l'invite
// affichée indéfiniment si la réponse s'est perdue.
string? pendingDuelInviteFrom = null;
var pendingDuelInviteTeamSize = 1;
var duelInviteExpiresAtUtc = DateTime.MinValue;
Task<CombatSessionState?>? duelMatchStateTask = null;
var duelTextInput = string.Empty;

// Sélection/création de personnage (voir GDD) : ne se fait plus dans le Launcher, mais en jeu,
// avant la connexion TCP proprement dite. `--characterId` reste accepté pour compatibilité
// (anciens raccourcis) : dans ce cas on saute directement l'écran de sélection.
Guid? chosenCharacterId = options.CharacterId;
List<CharacterSummary> myCharacters = [];
var characterCursor = 0;

var isConnectedMode = options.SessionToken is not null;

if (isConnectedMode)
{
    var apiBaseUrl = $"http://{options.Host}:{options.AccountApiPort}";
    starterApi = new StarterApiClient(apiBaseUrl);
    characterApi = new CharacterApiClient(apiBaseUrl);
    gameDataApi = new GameDataApiClient(apiBaseUrl);
    combatApi = new CombatApiClient(apiBaseUrl);

    if (chosenCharacterId is null)
    {
        try
        {
            myCharacters = await characterApi.GetMyCharactersAsync(options.SessionToken!);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[Personnage] Impossible de récupérer la liste des personnages : {ex.Message}");
        }

        sceneMode = myCharacters.Count > 0 ? SceneMode.CharacterSelect : SceneMode.CharacterCreate;
    }
    else
    {
        // Compatibilité : --characterId fourni directement (anciens raccourcis) — on se
        // connecte tout de suite, sans passer par l'écran de sélection.
        ConnectAndEnterWorld(chosenCharacterId.Value);
        await CheckStarterNeedAsync(chosenCharacterId.Value);
    }
}
else
{
    Console.WriteLine("Mode démo hors-ligne (lancez via le Launcher pour vous connecter au serveur).");
}

using var host = new GameHost($"{GameInfo.Name} — v{GameInfo.Version}", 1280, 720);

SpriteBatch spriteBatch = null!;
Texture2D whiteTexture = null!;
KeyboardState keyboard = null!;
MouseState mouse = null!;
var camera = new Camera2D { ViewportWidth = 1280, ViewportHeight = 720, Zoom = 1.4f };
var uiCamera = new Camera2D { ViewportWidth = 1280, ViewportHeight = 720, Zoom = 1f };

host.Load += () =>
{
    spriteBatch = new SpriteBatch(host.Gl);
    whiteTexture = Texture2D.CreateSolidColor(host.Gl, 255, 255, 255);
    keyboard = new KeyboardState(host.Input);
    mouse = new MouseState(host.Input);

    host.Gl.Enable(EnableCap.Blend);
    host.Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

    Console.WriteLine("Moteur initialisé : fenêtre, OpenGL, sprite batch, monde et input prêts.");
};

host.Resize += (width, height) =>
{
    host.Gl.Viewport(0, 0, (uint)width, (uint)height);
    camera.ViewportWidth = width;
    camera.ViewportHeight = height;
    uiCamera.ViewportWidth = width;
    uiCamera.ViewportHeight = height;
};

host.Update += deltaTime =>
{
    keyboard.Update();
    mouse.Update();
    animationClock += deltaTime;

    // Voir GDD/demande utilisateur — "quand on appuie sur les touches ça s'affiche aussi dans
    // le tchat même s'il n'est pas ouvert" : les touches de déplacement produisent aussi des
    // évènements de saisie de texte, purgés uniquement par les panneaux qui en ont besoin (tchat,
    // création de personnage, etc.) via KeyboardState.DrainTypedChars. Le try/finally garantit
    // que la file est bien vidée à chaque frame malgré les nombreux "return" anticipés ci-dessous
    // (un seul scénario/panneau est mis à jour par frame), sans quoi les touches tapées pendant
    // le jeu s'accumulaient indéfiniment puis se déversaient d'un coup à l'ouverture du tchat.
    try
    {

    // F9 : cycle la préférence de disposition clavier (Auto -> QWERTY -> AZERTY -> Auto),
    // disponible partout et persistée pour que le Launcher la reflète aussi (voir GDD).
    if (keyboard.WasJustPressed(Key.F9))
    {
        gameSettings.KeyboardLayout = gameSettings.KeyboardLayout switch
        {
            KeyboardLayoutPreference.Auto => KeyboardLayoutPreference.Qwerty,
            KeyboardLayoutPreference.Qwerty => KeyboardLayoutPreference.Azerty,
            _ => KeyboardLayoutPreference.Auto,
        };
        gameSettings.Save();
        isAzerty = KeyboardLayoutResolver.ShouldUseAzerty(gameSettings.KeyboardLayout);
        Console.WriteLine($"[Parametres] Disposition clavier : {gameSettings.KeyboardLayout} ({(isAzerty ? "ZQSD" : "WASD")}).");
    }

    // Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp" : sondé ici (hors
    // de toute scène particulière) car le combat peut démarrer côté défieur (voir
    // ChallengeDuelOpponentAsync) ou côté accepteur (voir DuelStartedReceived) alors que le joueur
    // est n'importe où dans le monde, pas forcément dans un panneau.
    if (duelMatchStateTask is { IsCompleted: true } duelStateTask)
    {
        var state = duelStateTask.IsFaulted ? null : duelStateTask.Result;
        duelMatchStateTask = null;

        if (state is not null)
        {
            combatState = state;
            combatSelectedAction = null;
            combatMessage = null;
            combatReturnScene = SceneMode.Outdoor;
            activePanel = PanelKind.None;
            combatVictoryQuestFired = false;
            sceneMode = SceneMode.Combat;
        }
    }

    if (pendingDuelInviteFrom is not null && DateTime.UtcNow > duelInviteExpiresAtUtc)
    {
        pendingDuelInviteFrom = null;
    }

    if (pendingDuelInviteFrom is not null && sceneMode is SceneMode.Outdoor or SceneMode.Interior && activeDialogueNpc is null)
    {
        if (keyboard.WasJustPressed(Key.Enter))
        {
            connection?.SendDuelResponse(true);
            pendingDuelInviteFrom = null;
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            connection?.SendDuelResponse(false);
            pendingDuelInviteFrom = null;
        }

        return;
    }

    // Voir GDD/demande utilisateur — chargement des recettes du panneau Craft (voir OpenPanel,
    // PanelKind.Craft), sondé ici pour continuer à se résoudre même si le joueur ressort du
    // bâtiment avant que la requête HTTP ne revienne.
    if (questRecipeTask is { IsCompleted: true } recipeTask)
    {
        forgeronRecipes = recipeTask.IsFaulted ? [] : [.. recipeTask.Result.Where(r => r.Profession == ProfessionType.Forgeron)];
        questRecipeCursor = 0;
        BuildForgeronRecipeLines();
        questRecipeTask = null;
    }

    if (activeDialogueNpc is null && activePanel == PanelKind.None && sceneMode is SceneMode.Outdoor or SceneMode.Interior
        && keyboard.WasJustPressed(Key.Q))
    {
        ToggleQuestPanel();
    }

    // Voir GDD/demande utilisateur — "le panel admin en jeu [est] pour les admins" : F2, ouvert
    // aux comptes IsAdmin ET au grade Fondateur (le Fondateur a en plus un bouton exclusif dans
    // le panel, voir AdminPanelCommands/UpdateAdminGamePanel), disponible hors connexion/combat.
    if ((myIsAdmin || myRank == UserRank.Fondateur) && keyboard.WasJustPressed(Key.F2)
        && sceneMode is SceneMode.Outdoor or SceneMode.Interior && activeDialogueNpc is null)
    {
        isAdminPanelOpen = !isAdminPanelOpen;
        adminPanelCursor = 0;
        adminPanelTyping = false;
        adminPanelTextInput = string.Empty;
        adminPanelMessage = null;
    }

    if (isAdminPanelOpen)
    {
        UpdateAdminGamePanel();
        return;
    }

    if (isTeleportPanelOpen)
    {
        UpdateTeleportPanel();
        return;
    }

    if (isMinePanelOpen)
    {
        UpdateMinePanel();
        return;
    }

    if (isFieldPanelOpen)
    {
        UpdateFieldPanel();
        return;
    }

    if (isWarRoomOpen)
    {
        UpdateWarRoomPanel(deltaTime);
        return;
    }

    if (sceneMode == SceneMode.CharacterSelect)
    {
        UpdateCharacterSelect();
        return;
    }

    if (sceneMode == SceneMode.CharacterCreate)
    {
        createPreviewClock += deltaTime;
        UpdateCharacterCreate();
        return;
    }

    if (sceneMode == SceneMode.Loading)
    {
        return;
    }

    if (sceneMode == SceneMode.StarterSelection)
    {
        UpdateStarterSelection(deltaTime);
        return;
    }

    // Vérifié avant la répartition par scène (au lieu de dans le seul bloc Interior) : un combat
    // peut désormais aussi démarrer depuis l'extérieur (rencontre sauvage aléatoire hors donjon,
    // voir GDD et StartWildEncounterOutdoorAsync), pas seulement depuis l'intérieur du donjon.
    if (combatStartTask is { IsCompleted: true } startedTask)
    {
        if (startedTask.IsFaulted)
        {
            combatMessage = "Connexion au serveur impossible.";
        }
        else if (startedTask.Result.IsSuccess)
        {
            combatState = startedTask.Result.State;
            combatSelectedAction = null;
            combatMessage = null;
            combatPollClock = 0f;
            combatPollTask = null;
            combatVictoryQuestFired = false;
            sceneMode = SceneMode.Combat;
        }
        else
        {
            combatMessage = startedTask.Result.Error;
        }

        combatStartTask = null;
    }

    if (sceneMode == SceneMode.Interior)
    {
        if (UpdateActiveDialogueIfAny())
        {
            return;
        }

        if (interiorIsDungeon && worldMap.DungeonId >= 0 && gameDataApi is not null)
        {
            UpdateDungeonCorridor(deltaTime);
            return;
        }

        if (keyboard.WasJustPressed(Key.Escape))
        {
            sceneMode = SceneMode.Outdoor;
        }
        else if (interiorIsDungeon && keyboard.WasJustPressed(Key.Enter) && combatStartTask is null)
        {
            combatMessage = null;
            combatReturnScene = SceneMode.Interior;
            combatStartTask = StartWildCombatAsync();
        }
        else if (!interiorIsDungeon && interiorNpcs.Count > 0 && keyboard.WasJustPressed(Key.E))
        {
            var npc = interiorNpcs[0];
            activeDialogueNpc = new Npc(npc.Name, 0, 0, npc.BodyColor, npc.HeadColor, 0f);
            dialogueLineIndex = 0;
        }

        return;
    }

    if (sceneMode == SceneMode.Combat)
    {
        UpdateCombat(deltaTime);
        return;
    }

    // À partir d'ici, sceneMode == SceneMode.Outdoor.
    if (UpdateActiveDialogueIfAny())
    {
        return; // Le monde se fige pendant un dialogue, comme dans un RPG classique.
    }

    if (UpdateTutorial())
    {
        return; // Le monde se fige pendant le tutoriel, comme pendant un dialogue/panneau.
    }

    if (activePanel != PanelKind.None)
    {
        UpdatePanel(deltaTime);
        return;
    }

    // Voir GDD/demande utilisateur — "ajoute un bâtiment marchand au lieu de l'UI Boutique en
    // haut à droite" : plus de raccourci B, la Boutique ne s'ouvre plus qu'en visitant le
    // bâtiment "Boutique" ou en parlant à la Marchande (voir InteractionKind.Building/Npc).
    if (keyboard.WasJustPressed(Key.I)) OpenPanel(PanelKind.Inventory);
    else if (keyboard.WasJustPressed(Key.G)) OpenPanel(PanelKind.Guild);
    else if (keyboard.WasJustPressed(Key.P)) OpenPanel(PanelKind.Party);
    else if (keyboard.WasJustPressed(Key.V)) OpenPanel(PanelKind.Arena);
    else if (keyboard.WasJustPressed(Key.M)) OpenPanel(PanelKind.Monsters);
    else if (keyboard.WasJustPressed(Key.T)) OpenPanel(PanelKind.Chat);
    else if (keyboard.WasJustPressed(Key.F)) OpenPanel(PanelKind.Friends);
    else if (keyboard.WasJustPressed(Key.U)) OpenPanel(PanelKind.Profile);
    else if (keyboard.WasJustPressed(Key.K)) OpenPanel(PanelKind.Leaderboard);
    else if (keyboard.WasJustPressed(Key.J)) OpenPanel(PanelKind.QuestList);
    else if (keyboard.WasJustPressed(Key.B)) OpenPanel(PanelKind.Professions);
    else if (keyboard.WasJustPressed(Key.N)) OpenPanel(PanelKind.BattlePass);
    else if (keyboard.WasJustPressed(Key.H)) OpenPanel(PanelKind.WorldBoss);
    // Voir GDD/demande utilisateur — "ajoute un raccourci clavier" (panneau Duel).
    else if (keyboard.WasJustPressed(Key.Y)) OpenPanel(PanelKind.Duel);
    // Voir GDD/demande utilisateur — "ajoute un UI pour les kingdom".
    else if (keyboard.WasJustPressed(Key.R)) OpenPanel(PanelKind.Kingdom);

    Vector2 positionBeforeInput;
    lock (stateLock)
    {
        positionBeforeInput = gridPosition;
    }

    // Clic gauche : calcule la case visée (transformation isométrique inverse) et y trace un chemin
    // — sauf si le clic tombe sur un bouton du HUD en haut à droite (voir GDD/demande utilisateur
    // et IsPointOverOutdoorHudButtons), sans quoi ouvrir un panneau déplaçait aussi le personnage.
    if (mouse.WasButtonJustPressed(MouseButton.Left) && !IsPointOverOutdoorHudButtons(mouse.Position, uiCamera.ViewportWidth))
    {
        var worldPoint = camera.ScreenToWorld(mouse.Position);
        var clickedGrid = IsoMath.IsoToGrid(worldPoint);
        var targetX = (int)MathF.Round(clickedGrid.X);
        var targetY = (int)MathF.Round(clickedGrid.Y);

        if (worldMap.IsWithinBounds(targetX, targetY))
        {
            var from = ((int)MathF.Round(positionBeforeInput.X), (int)MathF.Round(positionBeforeInput.Y));
            moveQueue = BuildOrthogonalPath(from, (targetX, targetY));

            if (connection is not null && !isAwaitingServerStep && moveQueue.Count > 0)
            {
                isAwaitingServerStep = true;
                var next = moveQueue.Dequeue();
                connection.SendMove(next.X, next.Y);
            }
        }
    }

    if (connection is null)
    {
        // Mode démo : le clavier reprend la main sur un chemin cliqué en cours ; sinon on suit le chemin.
        var direction = Vector2.Zero;
        if (keyboard.IsDown(Key.W) || keyboard.IsDown(Key.Up)) direction.Y -= 1;
        if (keyboard.IsDown(Key.S) || keyboard.IsDown(Key.Down)) direction.Y += 1;
        if (keyboard.IsDown(Key.A) || keyboard.IsDown(Key.Left)) direction.X -= 1;
        if (keyboard.IsDown(Key.D) || keyboard.IsDown(Key.Right)) direction.X += 1;

        if (direction != Vector2.Zero)
        {
            moveQueue.Clear();
            var next = positionBeforeInput + (Vector2.Normalize(direction) * 4.5f * deltaTime);
            lock (stateLock)
            {
                gridPosition = new Vector2(
                    Math.Clamp(next.X, 0, worldMap.Size - 1),
                    Math.Clamp(next.Y, 0, worldMap.Size - 1));
            }
        }
        else if (moveQueue.Count > 0)
        {
            var target = new Vector2(moveQueue.Peek().X, moveQueue.Peek().Y);
            var toTarget = target - positionBeforeInput;

            if (toTarget.LengthSquared() < 0.02f)
            {
                moveQueue.Dequeue();
            }
            else
            {
                var step = Vector2.Normalize(toTarget) * 4.5f * deltaTime;
                if (step.LengthSquared() > toTarget.LengthSquared())
                {
                    step = toTarget;
                }

                lock (stateLock)
                {
                    gridPosition = positionBeforeInput + step;
                }
            }
        }
    }
    else
    {
        // Mode connecté : WASD envoie un déplacement d'une case, confirmé par le serveur (autoritaire).
        var (dx, dy) = (0, 0);
        if (keyboard.WasJustPressed(Key.W) || keyboard.WasJustPressed(Key.Up)) dy = -1;
        else if (keyboard.WasJustPressed(Key.S) || keyboard.WasJustPressed(Key.Down)) dy = 1;
        else if (keyboard.WasJustPressed(Key.A) || keyboard.WasJustPressed(Key.Left)) dx = -1;
        else if (keyboard.WasJustPressed(Key.D) || keyboard.WasJustPressed(Key.Right)) dx = 1;
        else if (!isAwaitingServerStep && moveQueue.Count == 0)
        {
            // Voir GDD/demande utilisateur — "rester appuyé pour se déplacer" : une fois la case
            // en cours confirmée/animée (voir plus bas), enchaîne automatiquement tant que la
            // touche reste enfoncée, au même rythme qu'un appui répété.
            if (keyboard.IsDown(Key.W) || keyboard.IsDown(Key.Up)) dy = -1;
            else if (keyboard.IsDown(Key.S) || keyboard.IsDown(Key.Down)) dy = 1;
            else if (keyboard.IsDown(Key.A) || keyboard.IsDown(Key.Left)) dx = -1;
            else if (keyboard.IsDown(Key.D) || keyboard.IsDown(Key.Right)) dx = 1;
        }

        if (dx != 0 || dy != 0)
        {
            moveQueue.Clear();
            isAwaitingServerStep = true;
            var targetX = Math.Clamp((int)positionBeforeInput.X + dx, 0, worldMap.Size - 1);
            var targetY = Math.Clamp((int)positionBeforeInput.Y + dy, 0, worldMap.Size - 1);
            connection.SendMove(targetX, targetY);
        }

        // Anime gridPosition case par case vers la dernière position confirmée par le serveur
        // (voir GDD — "des animations quand on se déplace au lieu d'un TP") au lieu d'y sauter
        // instantanément. Une fois l'animation arrivée, enchaîne l'étape suivante d'un chemin
        // cliqué (voir GameConnection.PositionUpdated, qui ne fait plus que mettre à jour
        // serverConfirmedPosition sans avancer la file lui-même).
        Vector2 confirmedTarget;
        lock (stateLock)
        {
            confirmedTarget = serverConfirmedPosition;
        }

        var toConfirmed = confirmedTarget - positionBeforeInput;
        if (toConfirmed.LengthSquared() > 0.0001f)
        {
            var step = Vector2.Normalize(toConfirmed) * 6f * deltaTime;
            if (step.LengthSquared() > toConfirmed.LengthSquared())
            {
                step = toConfirmed;
            }

            lock (stateLock)
            {
                gridPosition = positionBeforeInput + step;
            }
        }
        else if (moveQueue.Count > 0)
        {
            var next = moveQueue.Dequeue();
            connection.SendMove(next.X, next.Y);
        }
        else
        {
            isAwaitingServerStep = false;
        }
    }

    Vector2 positionAfterInput;
    lock (stateLock)
    {
        positionAfterInput = gridPosition;
    }

    isPlayerMoving = Vector2.DistanceSquared(positionBeforeInput, positionAfterInput) > 0.0001f;

    // Interaction la plus proche (bâtiment, donjon, PNJ) : un texte "Appuyez sur E" apparaît, et
    // E déclenche réellement l'action (entrer, parler) — voir DrawOutdoorHud.
    nearbyInteraction = ComputeNearbyInteraction(positionAfterInput);

    if (nearbyInteraction is { } interaction && keyboard.WasJustPressed(Key.E))
    {
        switch (interaction.Kind)
        {
            // Voir GDD/demande utilisateur — "l'UI de fabrication, affiche-la dans le bâtiment, pas
            // seulement quand on rentre dedans" : E ouvre directement le panneau Craft à chaque
            // fois qu'on interagit avec l'Apprenti forgeron (pas seulement à la première visite
            // via le dialogue) — même logique de raccourci que les bâtiments à accès direct
            // (Pension/Boutique/Hôtel des ventes) plus bas.
            case InteractionKind.Npc when interaction.Npc!.Name == "Apprenti forgeron":
                OpenPanel(PanelKind.Craft);
                break;
            case InteractionKind.Npc:
                activeDialogueNpc = interaction.Npc;
                dialogueLineIndex = 0;
                break;
            case InteractionKind.Building when interaction.Building!.Name == "Téléporteur":
                // Voir GDD/demande utilisateur — "un téléporteur pour se déplacer de ville en
                // ville mais notre team ne change pas" : panneau dédié plutôt qu'une scène
                // d'intérieur, la téléportation elle-même ne touche à rien de plus que le
                // WorldMap affiché côté client (voir UpdateTeleportPanel) — personnage, équipe et
                // inventaire restent des données serveur, jamais réinitialisées ici.
                isTeleportPanelOpen = true;
                teleportCursor = 0;
                break;
            case InteractionKind.Building when interaction.Building!.Name.StartsWith("Mine"):
                // Voir GDD/demande utilisateur — "guerre de territoire... quêtes de minage".
                isMinePanelOpen = true;
                mineMessage = null;
                mineTerritory = null;
                mineOreItem = null;
                mineLoadTask = LoadMineInfoAsync(interaction.Building.Name);
                break;
            case InteractionKind.Building when interaction.Building!.Name.StartsWith("Champ"):
                // Voir GDD/demande utilisateur — "guerre de territoire... des bâtiments (mine,
                // champs etc)" : même mécanique de capture que la Mine.
                isFieldPanelOpen = true;
                fieldMessage = null;
                fieldTerritory = null;
                fieldCropItem = null;
                fieldLoadTask = LoadFieldInfoAsync(interaction.Building.Name);
                break;
            case InteractionKind.Building when interaction.Building!.Name == "Guerre":
                // Voir GDD/demande utilisateur — "un bâtiment nommé Guerre où on rentre dedans,
                // ça nous met une UI pour dire quand on est prêt".
                isWarRoomOpen = true;
                warMessage = null;
                warStandingsTask = combatApi?.GetWarStandingsAsync();
                warKingdomLeaderboardTask = chosenCharacterId is null || options.SessionToken is null
                    ? null
                    : gameDataApi?.GetKingdomLeaderboardAsync(LeaderboardCategory.Pvp, options.SessionToken, chosenCharacterId.Value);
                break;
            case InteractionKind.Building when interaction.Building!.Name == "Pension":
                // Voir GDD/demande utilisateur — bâtiment "où l'on peut voir tout nos monstres et
                // déplacer ce que l'on a dans notre team" : réutilise le panneau Monstres existant
                // (touche M), qui a maintenant la gestion d'équipe (touche T).
                OpenPanel(PanelKind.Monsters);
                break;
            case InteractionKind.Building when interaction.Building!.Name == "Boutique":
                // Voir GDD/demande utilisateur — "ajoute un bâtiment d'un marchand où on peut
                // acheter/vendre au lieu de l'UI Boutique en haut à droite" : ouvre directement le
                // panneau Boutique, comme les autres bâtiments à accès direct.
                OpenPanel(PanelKind.Shop);
                break;
            case InteractionKind.Building when interaction.Building!.Name == "Hôtel des ventes":
                // Voir GDD/demande utilisateur — "l'ui de l'hdv doit s'afficher quand on rentre
                // dedans, pas après avoir parlé au Commis" : ouvre directement le panneau, comme
                // le Téléporteur/la Mine/la Pension, plutôt que de passer par un dialogue d'abord.
                OpenPanel(PanelKind.Auction);
                break;
            case InteractionKind.Building:
                sceneMode = SceneMode.Interior;
                interiorTitle = interaction.Building!.Name;
                interiorBodyLines = BuildingFlavor(interaction.Building.Name);
                interiorAccent = interaction.Building.RoofColor;
                interiorIsDungeon = false;
                var layout = BuildingInteriors.ForBuilding(interaction.Building.Name);
                interiorFurniture = [.. layout.Furniture];
                interiorNpcs = [.. layout.Npcs];
                break;
            case InteractionKind.Dungeon:
                sceneMode = SceneMode.Interior;
                interiorTitle = worldMap.DungeonName;
                interiorBodyLines = DungeonFlavor();
                interiorAccent = WorldMap.PortalMidColorBright;
                interiorIsDungeon = true;
                interiorFurniture = [];
                interiorNpcs = [];
                dungeonFloorNumber = 1;
                dungeonRoomIndex = 0;
                dungeonFloor = null;
                dungeonClearedRooms = [];
                dungeonLastAutoFightRoomIndex = -1;
                dungeonClickTarget = null;
                dungeonExitConfirmOpen = false;
                dungeonPlayerPos = new Vector2(0.5f, 0.5f);
                dungeonRoomMessage = null;
                dungeonEncounterPreview = null;
                dungeonEncounterPreviewTask = null;
                dungeonEncounterPreviewRoomIndex = -1;
                if (worldMap.DungeonId >= 0 && gameDataApi is not null)
                {
                    dungeonFloorTask = gameDataApi.GetDungeonFloorAsync(worldMap.DungeonId, dungeonFloorNumber);
                }

                // Voir GDD/demande utilisateur — quête 6 "Les échos du donjon".
                _ = CompleteStoryQuestAsync("Les échos du donjon");
                break;
        }
    }
    }
    finally
    {
        keyboard.DiscardTypedChars();
    }
};

host.Render += _ =>
{
    host.Gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);
    host.Gl.Clear(ClearBufferMask.ColorBufferBit);

    if (sceneMode == SceneMode.Outdoor)
    {
        Vector2 currentGridPosition;
        lock (stateLock)
        {
            currentGridPosition = gridPosition;
        }

        camera.Position = IsoMath.GridToIso(currentGridPosition.X, currentGridPosition.Y);

        spriteBatch.Begin(camera);

        // Le sol : les tuiles ne se chevauchent jamais entre elles, aucun tri de profondeur requis.
        for (var y = 0; y < worldMap.Size; y++)
        {
            for (var x = 0; x < worldMap.Size; x++)
            {
                DrawIsoDiamond(new Vector2(x, y), 1f, worldMap.TileColors[x, y]);
            }
        }

        // Éléments en hauteur (bâtiments, portail, PNJ, joueur) : triés par profondeur pour une occlusion correcte.
        var playerBob = MathF.Sin(animationClock * (isPlayerMoving ? 9f : 2.4f)) * (isPlayerMoving ? 3.2f : 1.1f);

        var depthJobs = new List<(float Depth, Action Draw)>(worldMap.Buildings.Count + worldMap.Npcs.Count + 2)
        {
            (currentGridPosition.X + currentGridPosition.Y + 0.5f, () => DrawPlayerFigure(currentGridPosition, playerBob)),
            (worldMap.DungeonEntrance.X + worldMap.DungeonEntrance.Y,
                () => DrawPortal(new Vector2(worldMap.DungeonEntrance.X, worldMap.DungeonEntrance.Y), animationClock)),
        };

        foreach (var building in worldMap.Buildings)
        {
            depthJobs.Add((building.GridX + building.GridY, () => DrawBuilding(building)));
        }

        foreach (var npc in worldMap.Npcs)
        {
            depthJobs.Add((npc.GridX + npc.GridY + 0.3f, () => DrawNpcFigure(npc, animationClock)));
        }

        List<KeyValuePair<Guid, RemotePlayer>> currentRemotePlayers;
        lock (stateLock)
        {
            currentRemotePlayers = remotePlayers.ToList();
        }

        foreach (var (_, remote) in currentRemotePlayers)
        {
            depthJobs.Add((remote.Position.X + remote.Position.Y + 0.4f, () => DrawRemotePlayerFigure(remote, animationClock)));
        }

        foreach (var job in depthJobs.OrderBy(j => j.Depth))
        {
            job.Draw();
        }

        spriteBatch.End();
    }

    // Superposition écran (HUD, dialogue, scène d'intérieur, sélection du starter) : toujours
    // par-dessus le monde, dans un repère écran indépendant du zoom/de la position de la caméra.
    uiCamera.Position = new Vector2(uiCamera.ViewportWidth / 2f, uiCamera.ViewportHeight / 2f);
    spriteBatch.Begin(uiCamera);

    switch (sceneMode)
    {
        case SceneMode.CharacterSelect:
            DrawCharacterSelect();
            break;
        case SceneMode.CharacterCreate:
            DrawCharacterCreate();
            break;
        case SceneMode.Loading:
            DrawLoading();
            break;
        case SceneMode.StarterSelection:
            DrawStarterSelection();
            break;
        case SceneMode.Interior:
            DrawInteriorScene();
            break;
        case SceneMode.Combat:
            DrawCombat();
            break;
        case SceneMode.Outdoor:
            DrawOutdoorHud();
            break;
    }

    // Voir GDD/demande utilisateur — "afficher les messages du tchat transmis en bas à droite" :
    // superposé à toutes les scènes (pas seulement le panneau Tchat), pour être vu même fermé.
    DrawChatToasts(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    DrawSystemToasts(uiCamera.ViewportWidth, uiCamera.ViewportHeight);

    // Voir GDD/demande utilisateur — "affichage de quête à gauche" : superposé au monde/aux
    // intérieurs (pas en combat/dialogue/character select, où l'écran est déjà chargé).
    if (questTitle is not null && sceneMode is SceneMode.Outdoor or SceneMode.Interior)
    {
        DrawQuestPanel(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    }

    // Voir GDD/demande utilisateur — panel admin en jeu : la bannière est visible dans toutes les
    // scènes (c'est le but — tous les joueurs la voient, pas seulement l'admin qui l'a envoyée) ;
    // le panel de commandes lui-même seulement quand ouvert (F2, Fondateur uniquement).
    DrawAdminBanner(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    if (isAdminPanelOpen)
    {
        DrawAdminGamePanel(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    }

    // Voir GDD/demande utilisateur — "ajouter les demandes en duel pour le pvp".
    if (pendingDuelInviteFrom is { } duelChallengerName && sceneMode is SceneMode.Outdoor or SceneMode.Interior)
    {
        DrawDuelInvitePopup(uiCamera.ViewportWidth, uiCamera.ViewportHeight, duelChallengerName, pendingDuelInviteTeamSize);
    }

    if (isTeleportPanelOpen)
    {
        DrawTeleportPanel(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    }

    if (isMinePanelOpen)
    {
        DrawMinePanel(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    }

    if (isFieldPanelOpen)
    {
        DrawFieldPanel(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    }

    if (isWarRoomOpen)
    {
        DrawWarRoomPanel(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
    }

    spriteBatch.End();
};

host.Run();

connection?.Dispose();
starterApi?.Dispose();
characterApi?.Dispose();
gameDataApi?.Dispose();
combatApi?.Dispose();

static Queue<(int X, int Y)> BuildOrthogonalPath((int X, int Y) from, (int X, int Y) to)
{
    var queue = new Queue<(int X, int Y)>();
    var x = from.X;
    var y = from.Y;

    while (x != to.X)
    {
        x += Math.Sign(to.X - x);
        queue.Enqueue((x, y));
    }

    while (y != to.Y)
    {
        y += Math.Sign(to.Y - y);
        queue.Enqueue((x, y));
    }

    return queue;
}

NearbyInteraction? ComputeNearbyInteraction(Vector2 position)
{
    const float threshold = 1.6f;
    NearbyInteraction? best = null;
    var bestDistance = threshold;

    foreach (var building in worldMap.Buildings)
    {
        var distance = Vector2.Distance(position, new Vector2(building.GridX, building.GridY));
        if (distance < bestDistance)
        {
            bestDistance = distance;
            best = new NearbyInteraction(InteractionKind.Building, building.Name, building, null);
        }
    }

    var dungeonDistance = Vector2.Distance(position, new Vector2(worldMap.DungeonEntrance.X, worldMap.DungeonEntrance.Y));
    if (dungeonDistance < bestDistance)
    {
        bestDistance = dungeonDistance;
        best = new NearbyInteraction(InteractionKind.Dungeon, worldMap.DungeonName, null, null);
    }

    foreach (var npc in worldMap.Npcs)
    {
        var distance = Vector2.Distance(position, new Vector2(npc.GridX, npc.GridY));
        if (distance < bestDistance)
        {
            bestDistance = distance;
            best = new NearbyInteraction(InteractionKind.Npc, npc.Name, null, npc);
        }
    }

    return best;
}

/// <summary>
/// Avance/ferme le dialogue PNJ actif (voir <see cref="DrawDialogueBox"/>) — partagé entre le
/// monde extérieur et l'intérieur d'un bâtiment (voir GDD — PNJ à l'intérieur des bâtiments)
/// plutôt que dupliqué. Retourne vrai si un dialogue était actif (le monde/la scène doit se
/// figer pendant qu'il est affiché, comme dans un RPG classique).
/// </summary>
bool UpdateActiveDialogueIfAny()
{
    if (activeDialogueNpc is null)
    {
        return false;
    }

    if (keyboard.WasJustPressed(Key.E) || keyboard.WasJustPressed(Key.Enter))
    {
        var lines = NpcDialogues.Lines.GetValueOrDefault(activeDialogueNpc.Name, ["..."]);
        dialogueLineIndex++;
        if (dialogueLineIndex >= lines.Length)
        {
            OnDialogueFinished(activeDialogueNpc.Name);
            activeDialogueNpc = null;
            dialogueLineIndex = 0;
        }
    }
    else if (keyboard.WasJustPressed(Key.Escape))
    {
        activeDialogueNpc = null;
        dialogueLineIndex = 0;
    }

    return true;
}

/// <summary>
/// Voir GDD/demande utilisateur — le Forgeron affiche sa liste de craft "comme une quête" à
/// gauche, la Marchande ouvre directement son UI d'achat/vente : réactions au dialogue plutôt
/// qu'un système de quêtes à embranchements complet (voir Docs/README.md pour cette limite
/// assumée), déclenchées seulement en fin de dialogue (pas sur Echap, pour laisser le joueur
/// juste lire les répliques sans déclencher l'action s'il ferme tout de suite).
/// </summary>
/// <summary>
/// Construit les lignes du panneau de craft (<see cref="PanelKind.Craft"/>) à partir de
/// <see cref="forgeronRecipes"/> et de l'inventaire courant (voir GDD/demande utilisateur —
/// exemple donné : "le forgeron te dit de ramener 3 de fer et 1 bâton pour te faire une épée en
/// fer"). Rappelé après un craft réussi et à chaque rechargement d'inventaire (voir
/// LoadInventoryAsync) pour que les quantités possédées restent à jour (minage, butin de combat...).
/// </summary>
void BuildForgeronRecipeLines()
{
    craftRows = [];

    if (forgeronRecipes.Count == 0)
    {
        craftRows.Add(("Rien à fabriquer pour l'instant.", -1));
        return;
    }

    // Voir GDD/demande utilisateur — "l'UI du forgeron, le texte dépasse, fait en sorte que ça
    // ne dépasse pas" : les recettes à plusieurs ingrédients (voir catalogue étendu, H40) peuvent
    // largement dépasser la largeur de la boîte sur une seule ligne. Seul le nom+statut reste une
    // ligne cliquable (déclenche le craft) ; la liste d'ingrédients est repliée sur plusieurs
    // lignes simples en dessous, à la largeur réelle du panneau (voir DrawCraftPanel).
    for (var recipeIndex = 0; recipeIndex < forgeronRecipes.Count; recipeIndex++)
    {
        var recipe = forgeronRecipes[recipeIndex];
        var canCraft = recipe.Ingredients.All(i => (inventoryItems.FirstOrDefault(inv => inv.ItemId == i.ItemId)?.Quantity ?? 0) >= i.Quantity);
        var status = canCraft ? "[PRET]" : "[MANQUE]";
        craftRows.Add(($"{recipe.Name} {status}", recipeIndex));

        var ingredientText = string.Join(", ", recipe.Ingredients.Select(i =>
        {
            var have = inventoryItems.FirstOrDefault(inv => inv.ItemId == i.ItemId)?.Quantity ?? 0;
            var name = i.Item?.Name ?? $"Objet #{i.ItemId}";
            return $"{i.Quantity}x {name} ({have}/{i.Quantity})";
        }));

        foreach (var line in WrapTextToLines(ingredientText, CraftPanelWidth - 48f, 1.4f))
        {
            craftRows.Add(($"   {line}", -1));
        }
    }
}

async Task CraftSelectedRecipeAsync()
{
    if (chosenCharacterId is null || gameDataApi is null || questRecipeCursor >= forgeronRecipes.Count)
    {
        return;
    }

    var recipe = forgeronRecipes[questRecipeCursor];
    try
    {
        var result = await gameDataApi.CraftAsync(options.SessionToken!, chosenCharacterId.Value, recipe.Id);
        craftMessage = result?.Message ?? "Connexion au serveur impossible.";
        if (result is { LeveledUp: true })
        {
            PushSystemToast($"Métier {result.Profession} : niveau {result.Level} !", new Vector4(0.55f, 0.9f, 0.6f, 1f));
        }

        await LoadInventoryAsync();
        BuildForgeronRecipeLines();

        // Voir GDD/demande utilisateur — quête 4 "Le forgeron a besoin de bras".
        if (result?.Message is not null)
        {
            _ = CompleteStoryQuestAsync("Le forgeron a besoin de bras");
        }
    }
    catch (HttpRequestException)
    {
        craftMessage = "Connexion au serveur impossible.";
    }
}

/// <summary>Panneau Craft (voir <see cref="PanelKind.Craft"/>), ouvert uniquement en parlant à l'Apprenti forgeron — voir <see cref="OnDialogueFinished"/>.</summary>
void UpdateCraftPanel()
{
    if (questRecipeTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (forgeronRecipes.Count == 0)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) questRecipeCursor = Math.Min(questRecipeCursor + 1, forgeronRecipes.Count - 1);
    else if (keyboard.WasJustPressed(Key.Up)) questRecipeCursor = Math.Max(questRecipeCursor - 1, 0);
    else if ((keyboard.WasJustPressed(Key.C) || keyboard.WasJustPressed(Key.Enter)) && chosenCharacterId is not null && gameDataApi is not null)
    {
        craftMessage = null;
        _ = CraftSelectedRecipeAsync();
    }
}

void DrawCraftPanel(int w, int h)
{
    var lineHeight = TextRenderer.LineHeight(1.4f);
    var displayRows = craftMessage is not null ? [.. craftRows, ("", -1), (craftMessage, -1)] : craftRows;
    var boxHeight = Math.Min(h * 0.85f, 100f + displayRows.Count * (lineHeight + 6f));
    var topLeft = new Vector2(w / 2f - CraftPanelWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(CraftPanelWidth, boxHeight), new Vector4(0.08f, 0.06f, 0.05f, 0.95f));
    DrawPanel(topLeft, new Vector2(CraftPanelWidth, 4f), new Vector4(0.85f, 0.6f, 0.3f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "LE FORGERON PROPOSE :", new Vector2(w / 2f, topLeft.Y + 24f), 2f, new Vector4(0.95f, 0.75f, 0.4f, 1f));

    // Voir GDD/demande utilisateur — "le texte dépasse, fait en sorte que ça ne dépasse pas" :
    // au-delà de la hauteur disponible (boxHeight plafonnée ci-dessus), défile plutôt que de
    // continuer à dessiner hors du panneau — bien plus probable maintenant qu'un recette peut
    // avoir 5 ingrédients (voir catalogue étendu, H40).
    var y = topLeft.Y + 60f;
    var bottomLimit = topLeft.Y + boxHeight - 40f;
    for (var i = 0; i < displayRows.Count; i++)
    {
        if (y > bottomLimit)
        {
            break;
        }

        var (text, recipeIndex) = displayRows[i];
        var isRecipeRow = recipeIndex >= 0;
        var color = isRecipeRow && recipeIndex == questRecipeCursor ? new Vector4(0.6f, 0.95f, 0.65f, 1f) : new Vector4(0.85f, 0.85f, 0.9f, 1f);

        if (isRecipeRow && DrawClickableRow(text, topLeft + new Vector2(16f, y - topLeft.Y), CraftPanelWidth - 32f, 1.5f, color)
            && chosenCharacterId is not null && gameDataApi is not null)
        {
            questRecipeCursor = recipeIndex;
            craftMessage = null;
            _ = CraftSelectedRecipeAsync();
        }
        else if (!isRecipeRow)
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, text, topLeft + new Vector2(16f, y - topLeft.Y), 1.4f, color);
        }

        y += lineHeight + 6f;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "HAUT/BAS : choisir - C OU CLIC : fabriquer - ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight + 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Panneau Amis (touche F, voir GDD/demande utilisateur — "ajouter les amis : online/offline,
/// discussion privée, niveau, équipe équipée"). Liste combinée : demandes reçues en attente
/// d'abord (Entrée accepte, N refuse), puis les amis (Entrée ouvre une discussion privée, voir
/// UpdateChatPanel/chatWhisperTarget ; Suppr retire l'ami). A bascule vers la saisie d'un pseudo
/// pour envoyer une nouvelle demande.
/// </summary>
void UpdateFriendsPanel()
{
    if (friendListTask is { IsCompleted: true } listTask)
    {
        friendsList = listTask.IsFaulted ? [] : listTask.Result;
        friendListTask = null;
    }

    if (friendPendingTask is { IsCompleted: true } pendingTask)
    {
        friendPendingRequests = pendingTask.IsFaulted ? [] : pendingTask.Result;
        friendPendingTask = null;
        friendsLoaded = true;
    }

    if (friendActionTask is { IsCompleted: true } actionTask)
    {
        friendMessage = actionTask.IsFaulted ? "Connexion au serveur impossible." : actionTask.Result.Message;
        friendActionTask = null;
        friendCursor = 0;
        if (chosenCharacterId is not null && gameDataApi is not null)
        {
            friendListTask = gameDataApi.GetFriendsAsync(chosenCharacterId.Value);
            friendPendingTask = gameDataApi.GetPendingFriendRequestsAsync(chosenCharacterId.Value);
        }

        return;
    }

    if (friendActionTask is not null || friendListTask is not null && !friendsLoaded)
    {
        return;
    }

    if (friendAddMode)
    {
        foreach (var typed in keyboard.DrainTypedChars())
        {
            if (friendTextInput.Length < 40 && !char.IsControl(typed))
            {
                friendTextInput += typed;
            }
        }

        if (keyboard.WasJustPressed(Key.Backspace) && friendTextInput.Length > 0)
        {
            friendTextInput = friendTextInput[..^1];
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            friendAddMode = false;
            friendTextInput = string.Empty;
        }
        else if (keyboard.WasJustPressed(Key.Enter) && friendTextInput.Trim().Length > 0 && chosenCharacterId is not null && gameDataApi is not null)
        {
            friendMessage = null;
            friendActionTask = gameDataApi.SendFriendRequestAsync(options.SessionToken!, chosenCharacterId.Value, friendTextInput.Trim());
            friendAddMode = false;
            friendTextInput = string.Empty;
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (keyboard.WasJustPressed(Key.A))
    {
        friendAddMode = true;
        friendTextInput = string.Empty;
        friendMessage = null;
        return;
    }

    var totalRows = friendPendingRequests.Count + friendsList.Count;
    if (totalRows == 0)
    {
        return;
    }

    friendCursor = Math.Clamp(friendCursor, 0, totalRows - 1);

    if (keyboard.WasJustPressed(Key.Down)) friendCursor = Math.Min(friendCursor + 1, totalRows - 1);
    else if (keyboard.WasJustPressed(Key.Up)) friendCursor = Math.Max(friendCursor - 1, 0);
    else if (chosenCharacterId is not null && gameDataApi is not null)
    {
        if (friendCursor < friendPendingRequests.Count)
        {
            var request = friendPendingRequests[friendCursor];
            if (keyboard.WasJustPressed(Key.Enter))
            {
                friendMessage = null;
                friendActionTask = gameDataApi.RespondFriendRequestAsync(options.SessionToken!, chosenCharacterId.Value, request.RequesterName, accept: true);
            }
            else if (keyboard.WasJustPressed(Key.N))
            {
                friendMessage = null;
                friendActionTask = gameDataApi.RespondFriendRequestAsync(options.SessionToken!, chosenCharacterId.Value, request.RequesterName, accept: false);
            }
        }
        else
        {
            var friend = friendsList[friendCursor - friendPendingRequests.Count];
            if (keyboard.WasJustPressed(Key.Enter))
            {
                chatWhisperTarget = friend.Name;
                chatChannel = ChatChannel.Prive;
                OpenPanel(PanelKind.Chat);
            }
            else if (keyboard.WasJustPressed(Key.Delete))
            {
                friendMessage = null;
                friendActionTask = gameDataApi.RemoveFriendAsync(options.SessionToken!, chosenCharacterId.Value, friend.Name);
            }
        }
    }
}

void DrawFriendsPanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 420f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.08f, 0.1f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.5f, 0.8f, 0.9f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AMIS", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.6f, 0.85f, 0.95f, 1f));

    if (friendAddMode)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, "Nom du personnage a ajouter :", new Vector2(topLeft.X + 20f, topLeft.Y + 70f), 1.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        TextRenderer.Draw(spriteBatch, whiteTexture, friendTextInput + "_", new Vector2(topLeft.X + 20f, topLeft.Y + 100f), 1.9f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : ENVOYER - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    var y = topLeft.Y + 60f;
    var row = 0;

    if (friendPendingRequests.Count > 0)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, "DEMANDES RECUES :", new Vector2(topLeft.X + 20f, y), 1.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        y += 26f;

        foreach (var request in friendPendingRequests)
        {
            var selected = row == friendCursor;
            var color = selected ? new Vector4(0.95f, 0.85f, 0.5f, 1f) : Vector4.One;
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{(selected ? "> " : "  ")}{request.RequesterName} (ENTREE : accepter, N : refuser)", new Vector2(topLeft.X + 20f, y), 1.5f, color);
            y += 24f;
            row++;
        }

        y += 10f;
    }

    TextRenderer.Draw(spriteBatch, whiteTexture, "AMIS :", new Vector2(topLeft.X + 20f, y), 1.6f, new Vector4(0.7f, 0.9f, 0.75f, 1f));
    y += 26f;

    if (friendsList.Count == 0)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, "Aucun ami pour l'instant.", new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }

    foreach (var friend in friendsList)
    {
        var selected = row == friendCursor;
        var dotColor = friend.IsOnline ? new Vector4(0.4f, 0.9f, 0.45f, 1f) : new Vector4(0.5f, 0.5f, 0.55f, 1f);
        var textColor = selected ? new Vector4(0.95f, 0.85f, 0.5f, 1f) : Vector4.One;
        var status = friend.IsOnline ? "EN LIGNE" : "HORS LIGNE";
        TextRenderer.Draw(spriteBatch, whiteTexture, (selected ? "> " : "  ") + "●", new Vector2(topLeft.X + 20f, y), 1.5f, dotColor);
        TextRenderer.Draw(spriteBatch, whiteTexture, $"{friend.Name} (Nv.{friend.Level}) - {status}", new Vector2(topLeft.X + 42f, y), 1.5f, textColor);
        y += 24f;
        row++;
    }

    if (friendMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, friendMessage, new Vector2(w / 2f, topLeft.Y + boxHeight - 46f), 1.6f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "HAUT/BAS : choisir - ENTREE : MP/accepter - SUPPR : retirer - A : ajouter - ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight - 18f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Panneau Profil (touche U, voir GDD/demande utilisateur — "un endroit pour modifier son profil
/// : description, item à montrer, titre, grade"). Le grade est affiché en lecture seule
/// (<see cref="UserRank"/>, jamais modifiable par le joueur) ; description et titre actif
/// s'éditent au clavier, l'objet à montrer se choisit parmi l'inventaire courant.
/// </summary>
void UpdateProfilePanel()
{
    if (profileLoadTask is { IsCompleted: true } loadTask)
    {
        myProfile = loadTask.IsFaulted ? null : loadTask.Result;
        profileLoadTask = null;
    }

    if (profileActionTask is { IsCompleted: true } actionTask)
    {
        if (!actionTask.IsFaulted && actionTask.Result is not null)
        {
            myProfile = actionTask.Result;
            profileMessage = "Profil mis à jour.";
        }
        else
        {
            profileMessage = "Connexion au serveur impossible.";
        }

        profileActionTask = null;
        return;
    }

    if (profileActionTask is not null)
    {
        return;
    }

    if (profileEditMode)
    {
        foreach (var typed in keyboard.DrainTypedChars())
        {
            if (profileTextInput.Length < 200 && !char.IsControl(typed))
            {
                profileTextInput += typed;
            }
        }

        if (keyboard.WasJustPressed(Key.Backspace) && profileTextInput.Length > 0)
        {
            profileTextInput = profileTextInput[..^1];
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            profileEditMode = false;
            profileTextInput = string.Empty;
        }
        else if (keyboard.WasJustPressed(Key.Enter) && chosenCharacterId is not null && gameDataApi is not null && myProfile is not null)
        {
            profileEditMode = false;
            profileActionTask = gameDataApi.UpdateProfileAsync(options.SessionToken!, chosenCharacterId.Value, profileTextInput, myProfile.ShowcaseItemId, myProfile.ActiveTitle);
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (myProfile is null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.D))
    {
        profileEditMode = true;
        profileTextInput = myProfile.Description;
        profileMessage = null;
    }
    else if (keyboard.WasJustPressed(Key.Left) && myProfile.OwnedTitles.Count > 0 && chosenCharacterId is not null && gameDataApi is not null)
    {
        var index = myProfile.ActiveTitle is null ? -1 : myProfile.OwnedTitles.ToList().IndexOf(myProfile.ActiveTitle);
        var newTitle = index <= 0 ? null : myProfile.OwnedTitles[index - 1];
        profileActionTask = gameDataApi.UpdateProfileAsync(options.SessionToken!, chosenCharacterId.Value, myProfile.Description, myProfile.ShowcaseItemId, newTitle);
    }
    else if (keyboard.WasJustPressed(Key.Right) && myProfile.OwnedTitles.Count > 0 && chosenCharacterId is not null && gameDataApi is not null)
    {
        var index = myProfile.ActiveTitle is null ? -1 : myProfile.OwnedTitles.ToList().IndexOf(myProfile.ActiveTitle);
        var newTitle = index + 1 >= myProfile.OwnedTitles.Count ? myProfile.OwnedTitles[^1] : myProfile.OwnedTitles[index + 1];
        profileActionTask = gameDataApi.UpdateProfileAsync(options.SessionToken!, chosenCharacterId.Value, myProfile.Description, myProfile.ShowcaseItemId, newTitle);
    }
}

void DrawProfilePanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 340f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.08f, 0.07f, 0.1f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.75f, 0.55f, 0.95f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "PROFIL", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.8f, 0.65f, 0.98f, 1f));

    if (myProfile is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "Chargement...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 1.8f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    if (profileEditMode)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, "Description (200 caracteres max) :", new Vector2(topLeft.X + 20f, topLeft.Y + 70f), 1.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        foreach (var line in WrapTextToLines(profileTextInput + "_", boxWidth - 40f, 1.6f))
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, line, new Vector2(topLeft.X + 20f, topLeft.Y + 100f), 1.6f, Vector4.One);
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : VALIDER - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    var y = topLeft.Y + 66f;
    TextRenderer.Draw(spriteBatch, whiteTexture, $"{myProfile.CharacterName} - Nv.{myProfile.Level} - {myProfile.Rank}", new Vector2(topLeft.X + 20f, y), 1.8f, new Vector4(0.95f, 0.85f, 0.5f, 1f));
    y += 34f;

    TextRenderer.Draw(spriteBatch, whiteTexture, $"Titre actif : {myProfile.ActiveTitle ?? "(aucun)"}", new Vector2(topLeft.X + 20f, y), 1.6f, Vector4.One);
    y += 26f;
    TextRenderer.Draw(spriteBatch, whiteTexture, $"Objet à montrer : {myProfile.ShowcaseItemName ?? "(aucun)"}", new Vector2(topLeft.X + 20f, y), 1.6f, Vector4.One);
    y += 34f;

    TextRenderer.Draw(spriteBatch, whiteTexture, "Description :", new Vector2(topLeft.X + 20f, y), 1.6f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
    y += 24f;
    var descriptionLines = myProfile.Description.Length > 0 ? WrapTextToLines(myProfile.Description, boxWidth - 40f, 1.5f) : ["(vide)"];
    foreach (var line in descriptionLines)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, line, new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        y += 22f;
    }

    if (profileMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, profileMessage, new Vector2(w / 2f, topLeft.Y + boxHeight - 46f), 1.6f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "D : modifier description - GAUCHE/DROITE : titre actif - ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight - 18f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>Panneau Classement (bouton HUD/touche K, voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher").</summary>
void UpdateLeaderboardPanel()
{
    if (leaderboardLoadTask is { IsCompleted: true } loadTask)
    {
        leaderboardRows = loadTask.IsFaulted ? [] : loadTask.Result;
        leaderboardLoadTask = null;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (leaderboardLoadTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Left) || keyboard.WasJustPressed(Key.Right))
    {
        var delta = keyboard.WasJustPressed(Key.Right) ? 1 : -1;
        leaderboardCategoryCursor = ((leaderboardCategoryCursor + delta) % leaderboardCategories.Length + leaderboardCategories.Length) % leaderboardCategories.Length;
        leaderboardRows = [];
        leaderboardLoadTask = gameDataApi?.GetLeaderboardAsync(leaderboardCategories[leaderboardCategoryCursor]);
    }
}

string LeaderboardCategoryLabel(LeaderboardCategory category) => category switch
{
    LeaderboardCategory.Pvp => "PVP (ELO)",
    LeaderboardCategory.Richesse => "RICHESSE",
    LeaderboardCategory.Metiers => "METIERS",
    LeaderboardCategory.MonstresCaptures => "CREATURES CAPTUREES",
    LeaderboardCategory.Donjons => "ETAGE DE DONJON MAX",
    _ => category.ToString().ToUpperInvariant(),
};

void DrawLeaderboardPanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 440f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.09f, 0.08f, 0.04f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.95f, 0.8f, 0.35f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CLASSEMENT", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.95f, 0.85f, 0.5f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"< {LeaderboardCategoryLabel(leaderboardCategories[leaderboardCategoryCursor])} >", new Vector2(w / 2f, topLeft.Y + 58f), 2f, Vector4.One);

    var y = topLeft.Y + 96f;
    if (leaderboardRows.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "Aucune donnée pour ce classement.", new Vector2(w / 2f, y), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }

    for (var i = 0; i < leaderboardRows.Count; i++)
    {
        var row = leaderboardRows[i];
        var color = i == 0 ? new Vector4(0.95f, 0.85f, 0.4f, 1f) : Vector4.One;
        TextRenderer.Draw(spriteBatch, whiteTexture, $"{i + 1}. {row.CharacterName} - {row.Score}", new Vector2(topLeft.X + 24f, y), 1.7f, color);
        y += 28f;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "GAUCHE/DROITE : categorie - ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight - 18f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>Panneau Métiers (touche B, voir GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de chaque métier"), simple lecture seule — un par ProfessionType, y compris ceux jamais pratiqués (niveau 1).</summary>
void UpdateProfessionsPanel()
{
    if (professionLoadTask is { IsCompleted: true } loadTask)
    {
        professionRows = loadTask.IsFaulted ? [] : loadTask.Result;
        professionLoadTask = null;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
    }
}

void DrawProfessionsPanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 460f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.09f, 0.07f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.45f, 0.85f, 0.55f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "METIERS", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.55f, 0.9f, 0.6f, 1f));

    var y = topLeft.Y + 64f;
    if (professionLoadTask is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, y + 100f), 2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        foreach (var row in professionRows)
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, row.Profession.ToString().ToUpperInvariant(), new Vector2(topLeft.X + 24f, y), 1.8f, Vector4.One);
            TextRenderer.Draw(spriteBatch, whiteTexture, $"Niveau {row.Level}", new Vector2(topLeft.X + 240f, y), 1.6f, new Vector4(0.55f, 0.9f, 0.6f, 1f));
            y += 26f;
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{row.Experience} / {row.ExperienceForNextLevel} XP", new Vector2(topLeft.X + 24f, y), 1.3f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
            y += 30f;
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 18f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Panneau Passe de Niveau (touche N, voir GDD/demande utilisateur — "un pass de niveaux de
/// joueur ou chaque xp que tu gagne est ajouté dedans aussi ou chaque passage te fait gagner
/// quelque chose ... si il paie le pass premium alors il auront accès à des trucs plus exclusif").
/// Les récompenses sont octroyées automatiquement côté serveur à chaque palier (voir
/// BattlePassService) — ce panneau ne fait qu'afficher la progression et proposer le
/// déblocage du palier premium contre des gemmes.
/// </summary>
void UpdateBattlePassPanel()
{
    if (battlePassLoadTask is { IsCompleted: true } loadTask)
    {
        battlePassStatus = loadTask.IsFaulted ? null : loadTask.Result;
        battlePassLoadTask = null;
    }

    if (battlePassPurchaseTask is { IsCompleted: true } purchaseTask)
    {
        battlePassMessage = purchaseTask.IsFaulted ? "Connexion au serveur impossible." : purchaseTask.Result.Message;
        battlePassPurchaseTask = null;
        battlePassLoadTask = chosenCharacterId is null ? null : gameDataApi?.GetBattlePassStatusAsync(chosenCharacterId.Value);
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (keyboard.WasJustPressed(Key.Enter) && battlePassStatus is { PremiumCostGems: not null } && battlePassPurchaseTask is null
        && chosenCharacterId is not null && gameDataApi is not null)
    {
        battlePassMessage = null;
        battlePassPurchaseTask = gameDataApi.PurchaseBattlePassPremiumAsync(options.SessionToken!, chosenCharacterId.Value);
    }
}

void DrawBattlePassPanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 300f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.09f, 0.07f, 0.04f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.95f, 0.75f, 0.35f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "PASSE DE NIVEAU", new Vector2(w / 2f, topLeft.Y + 24f), 2.4f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    if (battlePassLoadTask is not null || battlePassStatus is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var status = battlePassStatus;
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"NIVEAU {status.Level}", new Vector2(w / 2f, topLeft.Y + 70f), 2.2f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{status.Experience} / {status.ExperienceForNextLevel} XP", new Vector2(w / 2f, topLeft.Y + 104f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));

        // Voir demande utilisateur — "renomme le pass gratuit et le pass premium : pass aventure =
        // pass gratuit, pass premium [inchangé]".
        var premiumLabel = status.HasPremium ? "PASS PREMIUM ACTIF" : "Pass Aventure — récompenses de base à chaque niveau.";
        var premiumColor = status.HasPremium ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : new Vector4(0.7f, 0.7f, 0.75f, 1f);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, premiumLabel, new Vector2(w / 2f, topLeft.Y + 140f), 1.7f, premiumColor);

        if (battlePassMessage is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, battlePassMessage, new Vector2(w / 2f, topLeft.Y + 168f), 1.5f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
        }

        if (status.PremiumCostGems is { } cost && battlePassPurchaseTask is null)
        {
            if (DrawClickableCentered($"DEBLOQUER LE PASS PREMIUM ({cost} GEMMES) - ENTREE", new Vector2(w / 2f, topLeft.Y + 204f), 1.7f, new Vector4(0.95f, 0.8f, 0.4f, 1f))
                && chosenCharacterId is not null && gameDataApi is not null)
            {
                battlePassMessage = null;
                battlePassPurchaseTask = gameDataApi.PurchaseBattlePassPremiumAsync(options.SessionToken!, chosenCharacterId.Value);
            }
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 18f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Panneau Boss Mondial (touche H, voir GDD/demande utilisateur — "un boss monde ou le but est de
/// faire un max de degat, plus on fait de degat plus on a de point, ajoute un leaderboard... du
/// boss actuel et de toujours, il a une barre de vie et peut etre tue"). Bouton ATTAQUER avec
/// cooldown côté serveur (voir WorldBossService) — pas de combat sur grille ici, volontairement
/// simplifié (voir commentaire de WorldBossService).
/// </summary>
void UpdateWorldBossPanel()
{
    if (worldBossLoadTask is { IsCompleted: true } loadTask)
    {
        worldBossStatus = loadTask.IsFaulted ? null : loadTask.Result;
        worldBossLoadTask = null;
    }

    if (worldBossLeaderboardLoadTask is { IsCompleted: true } leaderboardTask)
    {
        var rows = leaderboardTask.IsFaulted ? [] : leaderboardTask.Result;
        if (worldBossShowAllTime)
        {
            worldBossAllTimeLeaderboard = rows;
        }
        else
        {
            worldBossCurrentLeaderboard = rows;
        }

        worldBossLeaderboardLoadTask = null;
    }

    if (worldBossAttackTask is { IsCompleted: true } attackTask)
    {
        worldBossMessage = attackTask.IsFaulted ? "Connexion au serveur impossible." : attackTask.Result.Message;
        worldBossAttackTask = null;
        worldBossLoadTask = gameDataApi?.GetWorldBossStatusAsync();
        worldBossLeaderboardLoadTask = gameDataApi?.GetWorldBossLeaderboardAsync(worldBossShowAllTime);
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (keyboard.WasJustPressed(Key.Left) || keyboard.WasJustPressed(Key.Right))
    {
        worldBossShowAllTime = !worldBossShowAllTime;
        worldBossLeaderboardLoadTask = gameDataApi?.GetWorldBossLeaderboardAsync(worldBossShowAllTime);
    }

    if (keyboard.WasJustPressed(Key.Enter) && worldBossStatus is { IsAlive: true } && worldBossAttackTask is null
        && chosenCharacterId is not null && gameDataApi is not null)
    {
        worldBossMessage = null;
        worldBossAttackTask = gameDataApi.AttackWorldBossAsync(options.SessionToken!, chosenCharacterId.Value);
    }
}

void DrawWorldBossPanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 460f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.1f, 0.05f, 0.05f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.9f, 0.3f, 0.25f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "BOSS MONDIAL", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.95f, 0.45f, 0.4f, 1f));

    if (worldBossLoadTask is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + 100f), 2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else if (worldBossStatus is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUN BOSS MONDIAL ACTIF POUR LE MOMENT", new Vector2(w / 2f, topLeft.Y + 100f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var status = worldBossStatus;
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, status.Name.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + 62f), 2.2f, Vector4.One);

        var barTop = new Vector2(topLeft.X + 30f, topLeft.Y + 92f);
        var barSize = new Vector2(boxWidth - 60f, 22f);
        var healthRatio = status.MaxHealth <= 0 ? 0f : Math.Clamp((float)status.CurrentHealth / status.MaxHealth, 0f, 1f);
        DrawPanel(barTop, barSize, new Vector4(0.2f, 0.08f, 0.08f, 1f));
        DrawPanel(barTop, new Vector2(barSize.X * healthRatio, barSize.Y), new Vector4(0.85f, 0.25f, 0.2f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{status.CurrentHealth} / {status.MaxHealth} PV", barTop + new Vector2(barSize.X / 2f, barSize.Y / 2f - 8f), 1.5f, Vector4.One);

        if (!status.IsAlive)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"VAINCU PAR {status.KillerCharacterName?.ToUpperInvariant()}", new Vector2(w / 2f, topLeft.Y + 130f), 1.7f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
        }
        else if (DrawClickableCentered("ATTAQUER (ENTREE)", new Vector2(w / 2f, topLeft.Y + 134f), 2f, new Vector4(0.95f, 0.45f, 0.4f, 1f))
            && worldBossAttackTask is null && chosenCharacterId is not null && gameDataApi is not null)
        {
            worldBossMessage = null;
            worldBossAttackTask = gameDataApi.AttackWorldBossAsync(options.SessionToken!, chosenCharacterId.Value);
        }

        if (worldBossMessage is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, worldBossMessage, new Vector2(w / 2f, topLeft.Y + 166f), 1.4f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
        }
    }

    var leaderboardTitle = worldBossShowAllTime ? "< CLASSEMENT DE TOUJOURS >" : "< CLASSEMENT DU BOSS ACTUEL >";
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, leaderboardTitle, new Vector2(w / 2f, topLeft.Y + 210f), 1.8f, new Vector4(0.85f, 0.85f, 0.9f, 1f));

    var leaderboard = worldBossShowAllTime ? worldBossAllTimeLeaderboard : worldBossCurrentLeaderboard;
    var y = topLeft.Y + 244f;
    if (leaderboard.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE DONNEE POUR CE CLASSEMENT", new Vector2(w / 2f, y), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        for (var i = 0; i < leaderboard.Count; i++)
        {
            var row = leaderboard[i];
            var color = i == 0 ? new Vector4(0.95f, 0.75f, 0.4f, 1f) : Vector4.One;
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{i + 1}. {row.CharacterName} - {row.TotalDamage} degats", new Vector2(topLeft.X + 30f, y), 1.6f, color);
            y += 24f;
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "GAUCHE/DROITE : classement - ENTREE : attaquer - ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight - 18f), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Voir GDD/demande utilisateur — "panel admin en jeu... peuvent afficher un message en haut de
/// l'écran, donner des items, transformer le skin de tout les joueurs en panneau, ban/mute/kick" :
/// ban/mute existent déjà via les commandes de tchat <c>/ban</c>/<c>/mute</c> (voir
/// PlayerSession.HandleChatCommand), ce panel couvre les actions qui n'avaient pas d'équivalent
/// (diffusion, mode panneau, don d'objet, kick) avec une vraie UI plutôt que du texte à taper.
/// </summary>
void UpdateAdminGamePanel()
{
    if (adminPanelActionTask is { IsCompleted: true } actionTask)
    {
        adminPanelMessage = actionTask.IsFaulted ? "Connexion au serveur impossible." : actionTask.Result.Message;
        adminPanelActionTask = null;
        return;
    }

    if (adminPanelActionTask is not null)
    {
        return;
    }

    if (adminPanelTyping)
    {
        foreach (var typed in keyboard.DrainTypedChars())
        {
            if (adminPanelTextInput.Length < 80 && !char.IsControl(typed))
            {
                adminPanelTextInput += typed;
            }
        }

        if (keyboard.WasJustPressed(Key.Backspace) && adminPanelTextInput.Length > 0)
        {
            adminPanelTextInput = adminPanelTextInput[..^1];
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            adminPanelTyping = false;
            adminPanelTextInput = string.Empty;
        }
        else if (keyboard.WasJustPressed(Key.Enter) && adminPanelTextInput.Trim().Length > 0)
        {
            SubmitAdminPanelCommand(adminPanelCursor, adminPanelTextInput.Trim());
            adminPanelTyping = false;
            adminPanelTextInput = string.Empty;
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        isAdminPanelOpen = false;
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) adminPanelCursor = Math.Min(adminPanelCursor + 1, AdminPanelCommands().Length - 1);
    else if (keyboard.WasJustPressed(Key.Up)) adminPanelCursor = Math.Max(adminPanelCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter))
    {
        if (adminPanelCursor == 1)
        {
            adminPanelMessage = null;
            adminPanelActionTask = gameDataApi!.ActivateSignModeAsync(options.SessionToken!, 300);
        }
        else
        {
            adminPanelTyping = true;
            adminPanelTextInput = string.Empty;
            adminPanelMessage = null;
        }
    }
}

void SubmitAdminPanelCommand(int commandIndex, string input)
{
    switch (commandIndex)
    {
        case 0:
            adminPanelActionTask = gameDataApi!.BroadcastAdminMessageAsync(options.SessionToken!, input);
            break;
        case 2:
        {
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var itemId))
            {
                var quantity = parts.Length >= 3 && int.TryParse(parts[2], out var q) ? q : 1;
                adminPanelActionTask = gameDataApi!.GiveItemToPlayerAsync(options.SessionToken!, parts[0], itemId, quantity);
            }
            else
            {
                adminPanelMessage = "Format attendu : personnage;idObjet;quantite";
            }

            break;
        }
        case 3:
            adminPanelActionTask = gameDataApi!.KickPlayerAsync(options.SessionToken!, input);
            break;
        case 4:
            adminPanelActionTask = gameDataApi!.BanPlayerAsync(options.SessionToken!, input);
            break;
        case 5:
            adminPanelActionTask = gameDataApi!.TransformPlayerAsync(options.SessionToken!, input);
            break;
        case 6:
        {
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                adminPanelActionTask = gameDataApi!.GiveMonsterToPlayerAsync(options.SessionToken!, parts[0], parts[1]);
            }
            else
            {
                adminPanelMessage = "Format attendu : personnage;espece";
            }

            break;
        }
        case 7:
            adminPanelActionTask = gameDataApi!.MaxLevelTeamAsync(options.SessionToken!, input);
            break;
        case 8:
        {
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], out var moneyAmount))
            {
                adminPanelActionTask = gameDataApi!.GiveMoneyAsync(options.SessionToken!, parts[0], moneyAmount);
            }
            else
            {
                adminPanelMessage = "Format attendu : personnage;montant";
            }

            break;
        }
        case 9:
        {
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], out var xpAmount))
            {
                adminPanelActionTask = gameDataApi!.GiveXpAsync(options.SessionToken!, parts[0], xpAmount);
            }
            else
            {
                adminPanelMessage = "Format attendu : personnage;montant";
            }

            break;
        }
        case 10:
        {
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var newLevel))
            {
                adminPanelActionTask = gameDataApi!.SetLevelAsync(options.SessionToken!, parts[0], newLevel);
            }
            else
            {
                adminPanelMessage = "Format attendu : personnage;niveau";
            }

            break;
        }
        case 11:
            adminPanelActionTask = gameDataApi!.UnbanCharacterAsync(options.SessionToken!, input);
            break;
        case 12:
        {
            // Voir GDD/demande utilisateur — "boss geant mondial" : disponible à tout admin
            // (le serveur revérifie IsAdmin, pas seulement Fondateur — voir /api/admin/game/spawn-world-boss).
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[1], out var bossHealth))
            {
                adminPanelActionTask = gameDataApi!.SpawnWorldBossAsync(options.SessionToken!, parts[0], bossHealth);
            }
            else
            {
                adminPanelMessage = "Format attendu : nom;pv";
            }

            break;
        }
        case 13:
        {
            // Voir GDD/demande utilisateur — "/givegems" exclusif au Fondateur ; le serveur
            // revérifie de toute façon le grade de l'appelant (voir /api/admin/game/give-gems).
            var parts = input.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[1], out var gemsAmount))
            {
                adminPanelActionTask = gameDataApi!.GiveGemsAsync(options.SessionToken!, parts[0], gemsAmount);
            }
            else
            {
                adminPanelMessage = "Format attendu : personnage;montant";
            }

            break;
        }
        case 14:
            // Voir GDD/demande utilisateur — bouton exclusif au Fondateur ; le serveur revérifie
            // de toute façon le grade de l'appelant (voir /api/admin/game/toggle-admin).
            adminPanelActionTask = gameDataApi!.ToggleAdminAsync(options.SessionToken!, input);
            break;
    }
}

/// <summary>Royaumes accessibles depuis le téléporteur (voir <see cref="UpdateTeleportPanel"/>) : tous sauf celui où l'on se trouve déjà.</summary>
List<KingdomType> TeleportDestinations() => Enum.GetValues<KingdomType>().Where(k => k != currentKingdom).ToList();

/// <summary>
/// Voir GDD/demande utilisateur — "un téléporteur pour se déplacer de ville en ville mais notre
/// team ne change pas" : reconstruit juste la carte locale (<see cref="RebuildWorldMapForKingdom"/>,
/// déjà utilisée à la connexion) sur un autre royaume et repositionne au point d'apparition —
/// personnage/équipe/inventaire sont des données serveur, jamais touchées ici.
/// </summary>
void UpdateTeleportPanel()
{
    var destinations = TeleportDestinations();

    if (keyboard.WasJustPressed(Key.Escape))
    {
        isTeleportPanelOpen = false;
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) teleportCursor = Math.Min(teleportCursor + 1, destinations.Count - 1);
    else if (keyboard.WasJustPressed(Key.Up)) teleportCursor = Math.Max(teleportCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter) && destinations.Count > 0)
    {
        RebuildWorldMapForKingdom(destinations[teleportCursor]);
        _ = RefreshDungeonPositionAsync();
        isTeleportPanelOpen = false;
    }
}

void DrawTeleportPanel(int w, int h)
{
    var destinations = TeleportDestinations();

    const float boxWidth = 420f;
    const float boxHeight = 260f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.08f, 0.06f, 0.12f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.7f, 0.5f, 0.95f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "TELEPORTEUR", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.75f, 0.6f, 0.98f, 1f));

    var y = topLeft.Y + 64f;
    for (var i = 0; i < destinations.Count; i++)
    {
        var selected = i == teleportCursor;
        var color = selected ? new Vector4(0.75f, 0.6f, 0.98f, 1f) : Vector4.One;
        var prefix = selected ? "> " : "  ";
        var text = $"{prefix}{KingdomBiome.For(destinations[i]).CapitalName} ({destinations[i]})";
        if (DrawClickableRow(text, new Vector2(topLeft.X + 24f, y), boxWidth - 48f, 2f, color))
        {
            teleportCursor = i;
            RebuildWorldMapForKingdom(destinations[i]);
            _ = RefreshDungeonPositionAsync();
            isTeleportPanelOpen = false;
        }

        y += 32f;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CLIC OU ENTREE : VOYAGER - HAUT/BAS : CHOISIR - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

async Task<(TerritorySummary? Territory, ShopItem? Ore, int? MyKingdomId)> LoadMineInfoAsync(string mineName)
{
    if (gameDataApi is null)
    {
        return (null, null, null);
    }

    var territories = await gameDataApi.GetTerritoriesAsync();
    var territory = territories.FirstOrDefault(t => t.Name == mineName);
    var ore = await gameDataApi.GetGatherableItemAsync();
    var kingdoms = await gameDataApi.GetKingdomsAsync();
    var myKingdom = kingdoms.FirstOrDefault(k => k.Type == currentKingdom);
    return (territory, ore, myKingdom?.Id);
}

/// <summary>Voir GDD/demande utilisateur — panneau de la mine (voir <see cref="LoadMineInfoAsync"/>).</summary>
void UpdateMinePanel()
{
    if (keyboard.WasJustPressed(Key.Escape))
    {
        isMinePanelOpen = false;
        return;
    }

    if (mineLoadTask is { IsCompleted: true } loadTask)
    {
        (mineTerritory, mineOreItem, myKingdomId) = loadTask.IsFaulted ? (null, null, null) : loadTask.Result;
        mineLoadTask = null;
        return;
    }

    if (mineGatherTask is { IsCompleted: true } gatherTask)
    {
        mineMessage = gatherTask.IsFaulted ? "Connexion au serveur impossible." : gatherTask.Result?.Message ?? "Récolte impossible.";
        if (gatherTask.Result is { LeveledUp: true } leveledResult)
        {
            PushSystemToast($"Métier {leveledResult.Profession} : niveau {leveledResult.Level} !", new Vector4(0.55f, 0.9f, 0.6f, 1f));
        }

        mineGatherTask = null;
        return;
    }

    if (mineLoadTask is not null || mineGatherTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.R) && mineTerritory is not null && mineOreItem is not null
        && chosenCharacterId is not null && gameDataApi is not null && mineTerritory.ControllingKingdomId == myKingdomId)
    {
        mineMessage = null;
        mineGatherTask = gameDataApi.GatherAsync(options.SessionToken!, chosenCharacterId.Value, mineOreItem.ItemId, mineTerritory.Id);
    }
}

void DrawMinePanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 240f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.09f, 0.08f, 0.06f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.6f, 0.55f, 0.45f, 1f));

    if (mineLoadTask is not null || mineTerritory is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, mineTerritory.Name.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.85f, 0.75f, 0.55f, 1f));

        var isMine = mineTerritory.ControllingKingdomId == myKingdomId;
        var controlColor = isMine ? new Vector4(0.6f, 0.9f, 0.6f, 1f) : new Vector4(0.9f, 0.5f, 0.45f, 1f);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"CONTROLEE PAR : {mineTerritory.ControllingKingdomName.ToUpperInvariant()}", new Vector2(w / 2f, topLeft.Y + 70f), 1.9f, controlColor);

        var status = isMine
            ? "Cette mine appartient à votre royaume — vous pouvez y récolter."
            : "Un royaume rival contrôle cette mine. Remportez la guerre pour la reprendre.";
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, status, new Vector2(w / 2f, topLeft.Y + 110f), 1.6f, Vector4.One);

        if (mineMessage is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, mineMessage, new Vector2(w / 2f, topLeft.Y + 150f), 1.7f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
        }

        if (isMine && mineGatherTask is null && mineOreItem is not null)
        {
            if (DrawClickableCentered("RECOLTER (R)", new Vector2(w / 2f, topLeft.Y + 190f), 2f, new Vector4(0.85f, 0.75f, 0.55f, 1f))
                && chosenCharacterId is not null && gameDataApi is not null)
            {
                mineMessage = null;
                mineGatherTask = gameDataApi.GatherAsync(options.SessionToken!, chosenCharacterId.Value, mineOreItem.ItemId, mineTerritory.Id);
            }
        }
    }

    var footer = mineTerritory?.ControllingKingdomId == myKingdomId ? "R OU CLIC : RECOLTER - ECHAP : FERMER" : "ECHAP : FERMER";
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, footer, new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

async Task<(TerritorySummary? Territory, ShopItem? Crop)> LoadFieldInfoAsync(string fieldName)
{
    if (gameDataApi is null)
    {
        return (null, null);
    }

    var territories = await gameDataApi.GetTerritoriesAsync();
    var territory = territories.FirstOrDefault(t => t.Name == fieldName);
    var crop = await gameDataApi.GetGatherableCropItemAsync();
    return (territory, crop);
}

/// <summary>Voir GDD/demande utilisateur — "guerre de territoire... des bâtiments (mine, champs etc)" : pendant de <see cref="UpdateMinePanel"/>, même mécanique de capture/contrôle de territoire — voir <see cref="LoadFieldInfoAsync"/>.</summary>
void UpdateFieldPanel()
{
    if (keyboard.WasJustPressed(Key.Escape))
    {
        isFieldPanelOpen = false;
        return;
    }

    if (fieldLoadTask is { IsCompleted: true } loadTask)
    {
        (fieldTerritory, fieldCropItem) = loadTask.IsFaulted ? (null, null) : loadTask.Result;
        fieldLoadTask = null;
        return;
    }

    if (fieldGatherTask is { IsCompleted: true } gatherTask)
    {
        fieldMessage = gatherTask.IsFaulted ? "Connexion au serveur impossible." : gatherTask.Result?.Message ?? "Récolte impossible.";
        if (gatherTask.Result is { LeveledUp: true } leveledResult)
        {
            PushSystemToast($"Métier {leveledResult.Profession} : niveau {leveledResult.Level} !", new Vector4(0.55f, 0.9f, 0.6f, 1f));
        }

        fieldGatherTask = null;
        return;
    }

    if (fieldLoadTask is not null || fieldGatherTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.R) && fieldTerritory is not null && fieldCropItem is not null
        && chosenCharacterId is not null && gameDataApi is not null)
    {
        fieldMessage = null;
        fieldGatherTask = gameDataApi.GatherCropAsync(options.SessionToken!, chosenCharacterId.Value, fieldCropItem.ItemId, fieldTerritory.Id);
    }
}

void DrawFieldPanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 240f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.09f, 0.08f, 0.04f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.78f, 0.68f, 0.28f, 1f));

    if (fieldLoadTask is not null || fieldTerritory is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, fieldTerritory.Name.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.85f, 0.78f, 0.4f, 1f));

        var isOwn = fieldTerritory.ControllingKingdomId == myKingdomId;
        var controlColor = isOwn ? new Vector4(0.6f, 0.9f, 0.6f, 1f) : new Vector4(0.9f, 0.5f, 0.45f, 1f);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"CONTROLE PAR : {fieldTerritory.ControllingKingdomName.ToUpperInvariant()}", new Vector2(w / 2f, topLeft.Y + 70f), 1.9f, controlColor);

        var status = isOwn
            ? "Ce champ appartient à votre royaume — récolte à taux plein."
            : "Un royaume rival contrôle ce champ — vous pouvez toujours y récolter, mais avec moins de rendement et moins de chances d'obtenir du blé.";
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, status, new Vector2(w / 2f, topLeft.Y + 110f), 1.5f, Vector4.One);

        if (fieldMessage is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, fieldMessage, new Vector2(w / 2f, topLeft.Y + 150f), 1.7f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
        }

        if (fieldGatherTask is null && fieldCropItem is not null)
        {
            if (DrawClickableCentered("RECOLTER (R)", new Vector2(w / 2f, topLeft.Y + 190f), 2f, new Vector4(0.85f, 0.78f, 0.4f, 1f))
                && chosenCharacterId is not null && gameDataApi is not null)
            {
                fieldMessage = null;
                fieldGatherTask = gameDataApi.GatherCropAsync(options.SessionToken!, chosenCharacterId.Value, fieldCropItem.ItemId, fieldTerritory.Id);
            }
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "R OU CLIC : RECOLTER - ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Voir GDD/demande utilisateur — bâtiment "Guerre", UI "prêt" : mêmes mécaniques de file
/// d'attente sondée que <see cref="UpdateArenaPanel"/>, mais l'appairage se fait contre un
/// personnage d'un AUTRE royaume plutôt qu'un format à effectif fixe (voir KingdomWarQueueService).
/// </summary>
void UpdateWarRoomPanel(float deltaTime)
{
    if (warMatchStateTask is { IsCompleted: true } stateTask)
    {
        var state = stateTask.IsFaulted ? null : stateTask.Result;
        warMatchStateTask = null;

        if (state is not null)
        {
            combatState = state;
            combatSelectedAction = null;
            combatMessage = null;
            combatReturnScene = SceneMode.Outdoor;
            warReady = false;
            isWarRoomOpen = false;
            activePanel = PanelKind.None;
            combatVictoryQuestFired = false;
            sceneMode = SceneMode.Combat;
        }
        else
        {
            warMessage = "Impossible de récupérer le combat appairé.";
        }

        return;
    }

    if (warPollTask is { IsCompleted: true } pollTask)
    {
        var status = pollTask.IsFaulted ? null : pollTask.Result;
        warPollTask = null;

        if (status is { IsMatched: true, CombatId: { } combatId })
        {
            warMatchStateTask = combatApi!.GetStateAsync(combatId);
        }

        return;
    }

    if (warQueueTask is { IsCompleted: true } queueTask)
    {
        warReady = !queueTask.IsFaulted && queueTask.Result;
        warMessage = warReady ? null : "Connexion au serveur impossible.";
        warQueueTask = null;
        return;
    }

    if (warStandingsTask is { IsCompleted: true } standingsTask)
    {
        warStandings = standingsTask.IsFaulted ? [] : standingsTask.Result;
        warStandingsTask = null;
        return;
    }

    if (warKingdomLeaderboardTask is { IsCompleted: true } kingdomLeaderboardTask)
    {
        warKingdomLeaderboard = kingdomLeaderboardTask.IsFaulted ? [] : kingdomLeaderboardTask.Result;
        warKingdomLeaderboardTask = null;
        return;
    }

    if (warQueueTask is not null || warPollTask is not null || warMatchStateTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        if (warReady)
        {
            warReady = false;
            warMessage = null;
            _ = combatApi!.CancelWarQueueAsync(chosenCharacterId!.Value);
        }
        else
        {
            isWarRoomOpen = false;
        }

        return;
    }

    if (warReady)
    {
        warPollClock += deltaTime;
        if (warPollClock >= 1.5f)
        {
            warPollClock = 0f;
            warPollTask = combatApi!.GetWarQueueStatusAsync(chosenCharacterId!.Value);
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Enter) && chosenCharacterId is not null && options.SessionToken is not null && combatApi is not null)
    {
        warMessage = null;
        warPollClock = 0f;
        warQueueTask = combatApi.QueueForWarAsync(options.SessionToken, chosenCharacterId.Value);
    }
}

void DrawWarRoomPanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 460f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.09f, 0.05f, 0.05f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.75f, 0.25f, 0.22f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "GUERRE DE ROYAUMES", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.95f, 0.55f, 0.5f, 1f));

    if (warReady)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "PRET", new Vector2(w / 2f, topLeft.Y + 90f), 2.4f, new Vector4(0.9f, 0.8f, 0.4f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "RECHERCHE D'UN ADVERSAIRE...", new Vector2(w / 2f, topLeft.Y + 124f), 1.9f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
    }
    else
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "Affrontez un joueur d'un autre royaume.", new Vector2(w / 2f, topLeft.Y + 90f), 1.7f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "Une victoire rapporte des points de guerre a votre royaume.", new Vector2(w / 2f, topLeft.Y + 114f), 1.5f, new Vector4(0.8f, 0.8f, 0.85f, 1f));
    }

    if (warMessage is { Length: > 0 })
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, warMessage.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + 150f), 1.6f, new Vector4(0.95f, 0.6f, 0.5f, 1f));
    }

    // Voir GDD/demande utilisateur — "ajoute un leaderboard dans l'UI pour le ready, pour
    // afficher le nombre de points par team (meilleur a la pire)".
    var y = topLeft.Y + 178f;
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CLASSEMENT DES ROYAUMES", new Vector2(w / 2f, y), 1.8f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
    y += 30f;
    for (var i = 0; i < warStandings.Count; i++)
    {
        var standing = warStandings[i];
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{i + 1}. {standing.KingdomName.ToUpperInvariant()} — {standing.WarPoints} pts", new Vector2(w / 2f, y), 1.7f, i == 0 ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : Vector4.One);
        y += 24f;
    }

    // Voir GDD/demande utilisateur — "classement de team (le meilleur de la team ombre etc),
    // visible seulement si on est dans la même équipe" : le serveur ne renvoie que le royaume du
    // personnage authentifié (voir GetKingdomLeaderboardAsync), jamais un autre royaume.
    y += 16f;
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "MEILLEURS JOUEURS DE VOTRE ROYAUME (PVP)", new Vector2(w / 2f, y), 1.7f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
    y += 26f;
    if (warKingdomLeaderboard.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "Aucun rang PvP enregistre pour le moment.", new Vector2(w / 2f, y), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        for (var i = 0; i < warKingdomLeaderboard.Count; i++)
        {
            var row = warKingdomLeaderboard[i];
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{i + 1}. {row.CharacterName.ToUpperInvariant()} — {row.Score}", new Vector2(w / 2f, y), 1.6f, i == 0 ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : Vector4.One);
            y += 22f;
        }
    }

    var footer = warReady ? "ECHAP : ANNULER" : "ENTREE : PRET - ECHAP : FERMER";
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, footer, new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>Voir GDD/demande utilisateur — "ajoute un UI pour les kingdom" : capitale, membres, points de guerre/classement, bonus de rendement et territoires contrôlés, en un seul chargement.</summary>
async Task<(List<KingdomData> Kingdoms, List<TerritorySummary> Territories)> LoadKingdomPanelDataAsync()
{
    if (gameDataApi is null)
    {
        return ([], []);
    }

    var kingdoms = await gameDataApi.GetKingdomsAsync();
    var territories = await gameDataApi.GetTerritoriesAsync();
    return (kingdoms, territories);
}

void UpdateKingdomPanel()
{
    if (kingdomPanelLoadTask is { IsCompleted: true } loadTask)
    {
        (kingdomPanelData, kingdomPanelTerritories) = loadTask.IsFaulted ? ([], []) : loadTask.Result;
        kingdomPanelLoadTask = null;
        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
    }
}

void DrawKingdomPanel(int w, int h)
{
    const float boxWidth = 560f;
    const float boxHeight = 420f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.07f, 0.06f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.5f, 0.8f, 0.5f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ROYAUMES", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.7f, 0.95f, 0.7f, 1f));

    if (kingdomPanelLoadTask is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var ranked = kingdomPanelData.OrderByDescending(k => k.WarPoints).ToList();
        var y = topLeft.Y + 60f;

        foreach (var kingdom in ranked)
        {
            var rank = ranked.IndexOf(kingdom) + 1;
            var isMine = kingdom.Type == currentKingdom;
            var nameColor = isMine ? new Vector4(0.95f, 0.85f, 0.4f, 1f) : Vector4.One;
            var territoryCount = kingdomPanelTerritories.Count(t => t.ControllingKingdomId == kingdom.Id);

            TextRenderer.Draw(spriteBatch, whiteTexture,
                $"{rank}. {kingdom.Name.ToUpperInvariant()}{(isMine ? " (VOTRE ROYAUME)" : "")}",
                new Vector2(topLeft.X + 20f, y), 1.9f, nameColor);
            y += 26f;

            TextRenderer.Draw(spriteBatch, whiteTexture,
                $"Capitale : {kingdom.CapitalName}   -   {kingdom.MemberCount} membre(s)",
                new Vector2(topLeft.X + 34f, y), 1.5f, new Vector4(0.8f, 0.8f, 0.85f, 1f));
            y += 22f;

            TextRenderer.Draw(spriteBatch, whiteTexture,
                $"{kingdom.WarPoints} points de guerre   -   {territoryCount} territoire(s)   -   bonus de rendement +{kingdom.BonusTerritoryCount}",
                new Vector2(topLeft.X + 34f, y), 1.5f, new Vector4(0.7f, 0.9f, 0.7f, 1f));
            y += 34f;
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void OnDialogueFinished(string npcName)
{
    // Voir GDD/demande utilisateur — "l'Apprenti forgeron" ouvre maintenant directement le
    // panneau Craft sur E (voir ComputeNearbyInteraction/le switch d'interaction), sans passer
    // par un dialogue — ce cas n'arrive donc plus jamais ici.
    if (npcName == "Garde royal")
    {
        // Voir GDD/demande utilisateur — quête 1 "Une arrivée remarquée".
        _ = CompleteStoryQuestAsync("Une arrivée remarquée");
    }
    else if (npcName == "Marchande")
    {
        OpenPanel(PanelKind.Shop);
    }
}

/// <summary>
/// Tutoriel (touche F1, voir GDD/demande utilisateur) : ouvrable/fermable à tout moment depuis
/// l'extérieur, pas seulement au premier lancement — pas de suivi "déjà vu" persisté pour cette
/// version, F1 reste toujours disponible comme rappel. Retourne vrai si le tutoriel est affiché
/// (le monde se fige, comme un dialogue).
/// </summary>
bool UpdateTutorial()
{
    if (keyboard.WasJustPressed(Key.F1))
    {
        showTutorial = !showTutorial;
        tutorialPage = 0;
        return showTutorial;
    }

    if (!showTutorial)
    {
        return false;
    }

    var pages = TutorialPages();
    if (keyboard.WasJustPressed(Key.Escape))
    {
        showTutorial = false;
    }
    else if (keyboard.WasJustPressed(Key.Right) || keyboard.WasJustPressed(Key.Enter))
    {
        tutorialPage = Math.Min(tutorialPage + 1, pages.Length - 1);
    }
    else if (keyboard.WasJustPressed(Key.Left))
    {
        tutorialPage = Math.Max(tutorialPage - 1, 0);
    }

    return true;
}

static (string Title, string[] Lines)[] TutorialPages() =>
[
    ("BIENVENUE DANS AETHERIA",
    [
        "Ce tutoriel explique les bases du jeu.",
        "Flèches G/D ou Entrée pour avancer, Echap pour fermer.",
        "Rouvrable à tout moment avec F1.",
    ]),
    ("SE DEPLACER",
    [
        "WASD (ou ZQSD en clavier AZERTY) pour se déplacer,",
        "ou cliquez sur la carte pour tracer un chemin.",
        "F9 change la disposition clavier détectée.",
    ]),
    ("INTERAGIR",
    [
        "Approchez-vous d'un PNJ, d'un bâtiment ou d'un donjon :",
        "un message apparaît en bas de l'écran.",
        "Appuyez sur E pour parler ou entrer.",
    ]),
    ("PANNEAUX EN JEU",
    [
        "I : Inventaire   M : Monstres   P : Groupe",
        "G : Guilde   V : Arène classée",
        "Ou cliquez les boutons en haut à droite de l'écran.",
        "Boutique, Hôtel des ventes, Forge, Mine, Pension et",
        "Téléporteur s'ouvrent en visitant leur bâtiment en ville.",
    ]),
    ("COMBAT",
    [
        "Choisissez une action : 1 Déplacer, 2 Attaquer,",
        "3 Passer, 4 Capturer (avec une Carte de capture).",
        "Visez avec les flèches + Entrée, ou cliquez",
        "directement une case en surbrillance sur la grille.",
    ]),
    ("DONJONS",
    [
        "Un donjon apparaît à un endroit aléatoire de la carte",
        "et change de position toutes les heures.",
        "À l'intérieur : avancez de salle en salle avec Entrée",
        "(combats, coffres d'or, et autres événements).",
    ]),
    // Voir GDD/demande utilisateur — "menu F1 : liste des types, efficace/inefficace face à
    // quoi" : même triangle de types que CombatEngine.StrongAgainst côté serveur (dupliqué ici,
    // c'est de la donnée de game design statique, pas une raison d'exposer une API dédiée).
    ("TYPES ELEMENTAIRES",
    [
        "Feu > Nature, Glace   Eau > Feu, Terre",
        "Nature > Eau, Terre   Glace > Nature, Air",
        "Foudre > Eau, Air     Terre > Foudre, Feu",
        "Air > Terre, Nature   Lumière > Ombre",
        "Ombre > Lumière       Neutre : sans avantage",
        "'>' = 1.5x dégâts infligés, 0.67x dégâts subis en retour.",
    ]),
];

/// <summary>Rendu du tutoriel (voir <see cref="UpdateTutorial"/>) : une page de texte à la fois, façon écran d'intérieur.</summary>
void DrawTutorialOverlay(int w, int h)
{
    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.04f, 0.04f, 0.07f, 0.96f));

    var pages = TutorialPages();
    var page = pages[Math.Clamp(tutorialPage, 0, pages.Length - 1)];

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, page.Title.ToUpperInvariant(), new Vector2(w / 2f, h * 0.24f), 3.4f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    var lineY = h * 0.40f;
    foreach (var line in page.Lines)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, line, new Vector2(w / 2f, lineY), 2.2f, new Vector4(0.92f, 0.92f, 0.95f, 1f));
        lineY += TextRenderer.LineHeight(2.2f) + 8f;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"PAGE {tutorialPage + 1}/{pages.Length}", new Vector2(w / 2f, h * 0.78f), 1.7f, new Vector4(0.6f, 0.6f, 0.65f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "FLECHES : NAVIGUER - ECHAP OU F1 : FERMER", new Vector2(w / 2f, h - 40f), 2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
}

static string[] BuildingFlavor(string name) => name switch
{
    "Capitale" => ["Le hall du château résonne", "de conversations feutrées."],
    "Village" => ["Les villageois vaquent", "à leurs occupations."],
    "Hôtel des ventes" => ["Des étals présentent", "les objets à vendre."],
    "Forge" => ["La chaleur de la forge", "vous accueille."],
    "Guilde" => ["Les emblèmes des guildes", "ornent les murs."],
    _ => ["Vous entrez à l'intérieur."],
};

static string[] DungeonFlavor() =>
[
    "Le portail s'ouvre sur des couloirs sombres.",
    "Étages, combats et récompenses sont gérés",
    "par le système de donjons du serveur.",
];

static string WrapText(string text, int maxCharsPerLine)
{
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var builder = new StringBuilder();
    var lineLength = 0;

    foreach (var word in words)
    {
        if (lineLength > 0 && lineLength + 1 + word.Length > maxCharsPerLine)
        {
            builder.Append('\n');
            lineLength = 0;
        }
        else if (lineLength > 0)
        {
            builder.Append(' ');
            lineLength++;
        }

        builder.Append(word);
        lineLength += word.Length;
    }

    return builder.ToString();
}

static Vector4 ElementColor(Element element) => element switch
{
    Element.Feu => new Vector4(0.85f, 0.35f, 0.20f, 1f),
    Element.Eau => new Vector4(0.25f, 0.55f, 0.85f, 1f),
    Element.Nature => new Vector4(0.35f, 0.65f, 0.30f, 1f),
    Element.Glace => new Vector4(0.55f, 0.80f, 0.90f, 1f),
    Element.Foudre => new Vector4(0.90f, 0.85f, 0.25f, 1f),
    Element.Terre => new Vector4(0.60f, 0.45f, 0.30f, 1f),
    Element.Air => new Vector4(0.75f, 0.85f, 0.90f, 1f),
    Element.Lumiere => new Vector4(0.95f, 0.90f, 0.65f, 1f),
    Element.Ombre => new Vector4(0.40f, 0.30f, 0.50f, 1f),
    _ => new Vector4(0.65f, 0.65f, 0.65f, 1f),
};

/// <summary>Voir GDD/demande utilisateur — "affiche [le butin] d'une couleur différente en fonction de sa rareté".</summary>
static Vector4 RarityColor(Rarity rarity) => rarity switch
{
    Rarity.Commun => new Vector4(0.75f, 0.75f, 0.78f, 1f),
    Rarity.PeuCommun => new Vector4(0.4f, 0.85f, 0.45f, 1f),
    Rarity.Rare => new Vector4(0.35f, 0.6f, 0.95f, 1f),
    Rarity.Epique => new Vector4(0.65f, 0.4f, 0.9f, 1f),
    Rarity.Legendaire => new Vector4(0.95f, 0.65f, 0.25f, 1f),
    Rarity.Mythique => new Vector4(0.9f, 0.3f, 0.35f, 1f),
    Rarity.Ancestral => new Vector4(0.9f, 0.35f, 0.75f, 1f),
    Rarity.Divin => new Vector4(0.95f, 0.9f, 0.5f, 1f),
    Rarity.Admin => new Vector4(0.95f, 0.15f, 0.15f, 1f),
    _ => Vector4.One,
};

/// <summary>Voir GDD/demande utilisateur — "affiche la rareté à la fin du nom de l'objet".</summary>
static string RarityLabel(Rarity rarity) => rarity switch
{
    Rarity.PeuCommun => "Peu Commun",
    _ => rarity.ToString(),
};

/// <summary>
/// Reconstruit la carte du monde pour le royaume du personnage choisi (voir GDD — "plusieurs
/// villes distinctes par royaume/biome", <c>KingdomBiome</c>) : appelé une fois le royaume connu
/// (sélection d'un personnage existant ou fin de création), avant la connexion au serveur.
/// Réinitialise aussi la position locale sur le nouveau point d'apparition.
/// </summary>
void RebuildWorldMapForKingdom(KingdomType kingdom)
{
    worldMap = new WorldMap(size: 50, kingdom: kingdom);
    currentKingdom = kingdom;
    lock (stateLock)
    {
        gridPosition = new Vector2(worldMap.SpawnPosition.X, worldMap.SpawnPosition.Y);
    }
}

void ConnectAndEnterWorld(Guid characterId)
{
    Console.WriteLine($"Mode connecté : {options.Host}:{options.Port}, personnage {characterId}.");

    connection = new GameConnection();
    connection.EnterWorldAccepted += packet =>
    {
        lock (stateLock)
        {
            gridPosition = new Vector2(packet.PositionX, packet.PositionY);
            serverConfirmedPosition = gridPosition;
            statusMessage = "Connecté au monde.";
            myRank = packet.Rank;
            myIsAdmin = packet.IsAdmin;
        }

        Console.WriteLine($"[Réseau] Entrée dans le monde acceptée en ({packet.PositionX}, {packet.PositionY}).");
        _ = RefreshDungeonPositionAsync();
        _ = RefreshActiveQuestAsync();
    };
    connection.EnterWorldRejected += packet =>
    {
        lock (stateLock)
        {
            statusMessage = $"Connexion refusée : {packet.Reason}";
        }

        Console.WriteLine($"[Réseau] Entrée dans le monde refusée : {packet.Reason}");
    };
    connection.PositionUpdated += packet =>
    {
        if (packet.CharacterId != characterId)
        {
            // Position d'un autre joueur (diffusion serveur, voir GDD — visibilité globale).
            lock (stateLock)
            {
                if (remotePlayers.TryGetValue(packet.CharacterId, out var remote))
                {
                    remotePlayers[packet.CharacterId] = remote with { Position = new Vector2(packet.PositionX, packet.PositionY) };
                }
            }

            return;
        }

        // Ne téléporte plus gridPosition ici : la boucle Update anime le déplacement case par
        // case jusqu'à cette position (voir GDD — "des animations quand on se déplace au lieu
        // d'un TP"), et c'est elle qui enchaîne l'étape suivante d'un chemin cliqué une fois
        // l'animation arrivée à destination (pas immédiatement à la confirmation serveur, sinon
        // le serveur pourrait avancer plus vite que ce qui peut être animé à l'écran).
        lock (stateLock)
        {
            serverConfirmedPosition = new Vector2(packet.PositionX, packet.PositionY);
        }

        // Rencontre sauvage aléatoire hors donjon (voir GDD) : uniquement en extérieur, hors
        // dialogue/panneau/combat déjà en cours. Tiré à chaque case franchie en zone sauvage.
        if (sceneMode == SceneMode.Outdoor && combatStartTask is null && activeDialogueNpc is null
            && activePanel == PanelKind.None && worldMap.IsWildEncounterZone(packet.PositionX, packet.PositionY)
            && Random.Shared.NextDouble() < WildEncounterChance)
        {
            combatMessage = null;
            combatReturnScene = SceneMode.Outdoor;
            combatStartTask = StartWildEncounterOutdoorAsync();
        }
    };
    connection.PlayerJoined += packet =>
    {
        if (packet.CharacterId == characterId)
        {
            return;
        }

        lock (stateLock)
        {
            remotePlayers[packet.CharacterId] = new RemotePlayer(packet.Name, new Vector2(packet.PositionX, packet.PositionY), packet.Rank);
        }
    };
    connection.PlayerLeft += packet =>
    {
        lock (stateLock)
        {
            remotePlayers.Remove(packet.CharacterId);
        }
    };
    connection.ChatMessageReceived += packet =>
    {
        lock (stateLock)
        {
            var line = new ChatLine(packet.Channel, packet.SenderName, packet.Rank, packet.Message, packet.SenderGradeTier);
            chatMessages.Add(line);
            if (chatMessages.Count > MaxChatLines)
            {
                chatMessages.RemoveAt(0);
            }

            chatToasts.Add((line, DateTime.UtcNow + chatToastLifetime));
            if (chatToasts.Count > MaxChatToasts)
            {
                chatToasts.RemoveAt(0);
            }
        }
    };
    connection.AdminEffectReceived += packet =>
    {
        lock (stateLock)
        {
            if (packet.Kind == AdminEffectKind.Broadcast)
            {
                adminBannerMessage = packet.Message;
                adminBannerExpiresAtUtc = DateTime.UtcNow.AddSeconds(6);
            }
            else if (packet.Kind == AdminEffectKind.SignMode)
            {
                signModeExpiresAtUtc = DateTime.UtcNow.AddSeconds(packet.DurationSeconds);
            }
        }
    };
    connection.DuelInviteReceived += packet =>
    {
        lock (stateLock)
        {
            pendingDuelInviteFrom = packet.FromCharacterName;
            pendingDuelInviteTeamSize = packet.TargetTeamSize;
            duelInviteExpiresAtUtc = DateTime.UtcNow.AddSeconds(30);
        }
    };
    connection.TeamDuelReadyReceived += packet =>
    {
        lock (stateLock)
        {
            duelMatchStateTask = ChallengeTeamDuelAsync(packet.ChallengerTeamCharacterIds, packet.TargetTeamCharacterIds);
        }
    };
    connection.DuelStartedReceived += packet =>
    {
        lock (stateLock)
        {
            duelMatchStateTask = combatApi?.GetStateAsync(packet.CombatId);
        }
    };
    connection.Disconnected += () =>
    {
        Console.WriteLine("[Réseau] Déconnecté du serveur.");
        lock (stateLock)
        {
            statusMessage = "Déconnecté du serveur.";
            remotePlayers.Clear();
        }
    };

    try
    {
        connection.Connect(options.Host, options.Port);
        connection.RequestEnterWorld(options.SessionToken!, characterId);
    }
    catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException)
    {
        lock (stateLock)
        {
            statusMessage = $"Impossible de se connecter au serveur : {ex.Message}";
        }

        connection = null;
    }
}

/// <summary>
/// Récupère la position serveur du donjon (voir DungeonWorldService — tourne chaque heure UTC)
/// et la reflète sur la carte locale. Appelé une fois après connexion ; pas de rafraîchissement
/// périodique pour cette première intégration (le joueur doit se reconnecter pour voir un
/// déplacement de donjon survenu en cours de session) — voir Docs/README.md.
/// </summary>
/// <summary>Voir GDD/demande utilisateur — "un tutoriel qui force le joueur à faire des quêtes qui lui expliquent le jeu". Appelé à la connexion et après chaque complétion.</summary>
async Task RefreshActiveQuestAsync()
{
    if (gameDataApi is null || chosenCharacterId is null)
    {
        return;
    }

    try
    {
        activeStoryQuest = await gameDataApi.GetActiveQuestAsync(chosenCharacterId.Value);
        RefreshStoryQuestPanel();
    }
    catch (HttpRequestException)
    {
    }
}

/// <summary>
/// Voir GDD/demande utilisateur — "la première quête ne se finit jamais alors que je suis allé
/// parler au garde" : le serveur validait pourtant bien la quête (confirmé), mais le panneau ne se
/// rafraîchissait QUE s'il était vide (voir l'ancienne <c>ShowStoryQuestIfIdle</c>) — une fois la
/// quête 1 affichée, elle y restait indéfiniment même après complétion côté serveur. Rafraîchit
/// maintenant systématiquement le texte affiché (sauf panneau explicitement masqué par le joueur
/// — voir <see cref="isQuestPanelHidden"/>). Complètement indépendant du craft du Forgeron (voir
/// <see cref="PanelKind.Craft"/>) depuis que les deux ont été découplés.
/// </summary>
void RefreshStoryQuestPanel()
{
    if (isQuestPanelHidden)
    {
        return;
    }

    if (activeStoryQuest is { } quest)
    {
        questTitle = quest.Name;
        // Voir GDD/demande utilisateur — "le texte dépasse de l'UI" : la description peut être
        // plus longue que la largeur du panneau (voir DrawQuestPanel, panelWidth fixe), repliée
        // en plusieurs lignes distinctes plutôt qu'un seul \n interne (dont la hauteur ne serait
        // pas comptée par l'espacement ligne par ligne du panneau).
        questLines = [.. WrapTextToLines(quest.Description, 300f, 1.5f), "", "Q OU CLIC POUR MASQUER"];
    }
    else
    {
        questTitle = null;
        questLines = [];
    }
}

/// <summary>Découpe un texte en lignes qui tiennent dans <paramref name="maxWidth"/> (voir GDD/demande utilisateur — "le texte dépasse de l'UI"), mot par mot plutôt que caractère par caractère pour rester lisible.</summary>
static List<string> WrapTextToLines(string text, float maxWidth, float pixelSize)
{
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var lines = new List<string>();
    var current = "";

    foreach (var word in words)
    {
        var candidate = current.Length == 0 ? word : $"{current} {word}";
        if (TextRenderer.MeasureWidth(candidate, pixelSize) > maxWidth && current.Length > 0)
        {
            lines.Add(current);
            current = word;
        }
        else
        {
            current = candidate;
        }
    }

    if (current.Length > 0)
    {
        lines.Add(current);
    }

    return lines;
}

/// <summary>
/// Voir GDD/demande utilisateur — "il y a une touche pour masquer la quête mais pas la
/// réafficher" : Q (ou un clic, voir DrawOutdoorHudButtons/DrawQuestPanel) bascule maintenant
/// dans les deux sens plutôt que de seulement masquer.
/// </summary>
void ToggleQuestPanel()
{
    if (questTitle is not null)
    {
        isQuestPanelHidden = true;
        questTitle = null;
        questLines = [];
        return;
    }

    isQuestPanelHidden = false;
    RefreshStoryQuestPanel();
}

/// <summary>
/// Panneau Liste de quêtes (touche J, voir GDD/demande utilisateur — "un UI pour afficher TOUTES
/// les quêtes en cours et en choisir 1 à épingler"). Une seule entrée possible pour l'instant
/// (voir déclaration de <see cref="questListCursor"/>) — Entrée épingle/désépingle celle
/// sélectionnée, ce qui revient à afficher/masquer le panneau de quête de gauche.
/// </summary>
void UpdateQuestListPanel()
{
    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (activeStoryQuest is null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Enter))
    {
        ToggleQuestPanel();
    }
}

void DrawQuestListPanel(int w, int h)
{
    const float boxWidth = 420f;
    const float boxHeight = 220f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.07f, 0.07f, 0.1f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.9f, 0.8f, 0.4f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "QUETES EN COURS", new Vector2(w / 2f, topLeft.Y + 24f), 2.4f, new Vector4(0.95f, 0.85f, 0.5f, 1f));

    if (activeStoryQuest is not { } quest)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE QUETE EN COURS", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : fermer", new Vector2(w / 2f, topLeft.Y + boxHeight + 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    var isPinned = !isQuestPanelHidden;
    var rowColor = questListCursor == 0 ? new Vector4(0.6f, 0.95f, 0.65f, 1f) : Vector4.One;
    var pinLabel = isPinned ? "[EPINGLEE A GAUCHE]" : "[NON EPINGLEE]";
    if (DrawClickableRow($"{quest.Name.ToUpperInvariant()} {pinLabel}", topLeft + new Vector2(20f, 66f), boxWidth - 40f, 1.8f, rowColor))
    {
        ToggleQuestPanel();
    }

    var y = topLeft.Y + 100f;
    foreach (var line in WrapTextToLines(quest.Description, boxWidth - 40f, 1.4f))
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, line, new Vector2(topLeft.X + 20f, y), 1.4f, new Vector4(0.8f, 0.8f, 0.85f, 1f));
        y += 20f;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE OU CLIC : EPINGLER/DESEPINGLER - ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight + 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Voir GDD/demande utilisateur — points d'ancrage de la progression (parler au garde, gagner un
/// combat, capturer, fabriquer, échanger avec la marchande, entrer en donjon) : le serveur
/// n'accepte la complétion que si le nom correspond bien à l'étape courante (voir
/// QuestService.CompleteIfActiveAsync), donc appeler ceci "au cas où" à chaque action concernée
/// est sans risque même si ce n'est pas (ou plus) l'étape active.
/// </summary>
async Task CompleteStoryQuestAsync(string questName)
{
    if (gameDataApi is null || chosenCharacterId is null || options.SessionToken is null)
    {
        return;
    }

    try
    {
        await gameDataApi.CompleteQuestAsync(options.SessionToken, chosenCharacterId.Value, questName);
        await RefreshActiveQuestAsync();
    }
    catch (HttpRequestException)
    {
    }
}

async Task RefreshDungeonPositionAsync()
{
    if (gameDataApi is null)
    {
        return;
    }

    try
    {
        var dungeons = await gameDataApi.GetDungeonsAsync();
        var dungeon = dungeons.FirstOrDefault(d => d.Name == worldMap.DungeonName) ?? dungeons.FirstOrDefault();
        if (dungeon is not null)
        {
            worldMap.SetDungeon(dungeon.Id, dungeon.WorldX, dungeon.WorldY);
            Console.WriteLine($"[Donjon] « {dungeon.Name} » positionné en ({dungeon.WorldX}, {dungeon.WorldY}) pour cette heure.");
        }
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"[Donjon] Impossible de récupérer la position du donjon : {ex.Message}");
    }
}

/// <summary>
/// Scène d'introduction du starter (voir GDD) : seulement si ce personnage ne possède encore
/// aucune créature (sinon il a déjà fait son choix par le passé). Bascule <see cref="sceneMode"/>
/// elle-même en Outdoor ou StarterSelection une fois résolue.
/// </summary>
async Task CheckStarterNeedAsync(Guid characterId)
{
    if (starterApi is null)
    {
        sceneMode = SceneMode.Outdoor;
        return;
    }

    try
    {
        var existingMonsters = await starterApi.GetCharacterMonstersAsync(characterId);
        if (existingMonsters.Count == 0)
        {
            starterChoices = await starterApi.GetStarterSpeciesAsync();
            if (starterChoices.Count > 0)
            {
                sceneMode = SceneMode.StarterSelection;
                Console.WriteLine($"[Starter] {starterChoices.Count} compagnons communs disponibles pour le premier choix.");
                return;
            }
        }
        else
        {
            Console.WriteLine($"[Starter] Personnage déjà accompagné de {existingMonsters.Count} créature(s), pas de nouvelle sélection.");
        }
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"[Starter] Impossible de vérifier les compagnons existants : {ex.Message}");
    }

    sceneMode = SceneMode.Outdoor;
}

void ResetCreationState()
{
    createStage = CreateStage.Name;
    createName = string.Empty;
    createClassIndex = 0;
    createKingdomIndex = 0;
    createSkinIndex = 0;
    createHairStyleIndex = 0;
    createHairColorIndex = 0;
    createClothesColorIndex = 0;
    createAccessoryIndex = 0;
    createAppearanceField = 0;
    createErrorMessage = null;
}

void UpdateCharacterSelect()
{
    var optionCount = myCharacters.Count + 1; // +1 pour "Nouveau personnage"

    if (keyboard.WasJustPressed(Key.Down)) characterCursor = Math.Min(characterCursor + 1, optionCount - 1);
    else if (keyboard.WasJustPressed(Key.Up)) characterCursor = Math.Max(characterCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter))
    {
        if (characterCursor == myCharacters.Count)
        {
            ResetCreationState();
            sceneMode = SceneMode.CharacterCreate;
        }
        else
        {
            var chosen = myCharacters[characterCursor];
            chosenCharacterId = chosen.Id;
            sceneMode = SceneMode.Loading;
            RebuildWorldMapForKingdom(chosen.Kingdom);
            ConnectAndEnterWorld(chosen.Id);
            _ = CheckStarterNeedAsync(chosen.Id);
        }
    }
}

void UpdateCharacterCreate()
{
    switch (createStage)
    {
        case CreateStage.Name:
            // Caractères réellement tapés (respecte QWERTY/AZERTY/... via l'OS) plutôt qu'un
            // mapping par position de touche — voir KeyboardState.DrainTypedChars.
            foreach (var typed in keyboard.DrainTypedChars())
            {
                if (createName.Length < 16 && (char.IsLetter(typed) || char.IsDigit(typed) || typed == ' '))
                {
                    createName += typed;
                }
            }

            if (keyboard.WasJustPressed(Key.Backspace) && createName.Length > 0)
            {
                createName = createName[..^1];
            }
            else if (keyboard.WasJustPressed(Key.Enter) && createName.Trim().Length >= 3)
            {
                createStage = CreateStage.Appearance;
            }
            else if (keyboard.WasJustPressed(Key.Escape) && myCharacters.Count > 0)
            {
                sceneMode = SceneMode.CharacterSelect;
            }

            break;

        case CreateStage.Appearance:
            const int fieldCount = 5;
            if (keyboard.WasJustPressed(Key.Down)) createAppearanceField = (createAppearanceField + 1) % fieldCount;
            else if (keyboard.WasJustPressed(Key.Up)) createAppearanceField = (createAppearanceField - 1 + fieldCount) % fieldCount;
            else if (keyboard.WasJustPressed(Key.Right) || keyboard.WasJustPressed(Key.Left))
            {
                var delta = keyboard.WasJustPressed(Key.Right) ? 1 : -1;
                switch (createAppearanceField)
                {
                    case 0: createSkinIndex = Wrap(createSkinIndex + delta, CharacterAppearancePalette.SkinColors.Length); break;
                    case 1: createHairStyleIndex = Wrap(createHairStyleIndex + delta, CharacterAppearancePalette.HairStyleNames.Length); break;
                    case 2: createHairColorIndex = Wrap(createHairColorIndex + delta, CharacterAppearancePalette.HairColors.Length); break;
                    case 3: createClothesColorIndex = Wrap(createClothesColorIndex + delta, CharacterAppearancePalette.ClothesColors.Length); break;
                    case 4: createAccessoryIndex = Wrap(createAccessoryIndex + delta, CharacterAppearancePalette.AccessoryNames.Length); break;
                }
            }
            else if (keyboard.WasJustPressed(Key.Enter)) createStage = CreateStage.ClassKingdom;
            else if (keyboard.WasJustPressed(Key.Escape)) createStage = CreateStage.Name;

            break;

        case CreateStage.ClassKingdom:
            if (keyboard.WasJustPressed(Key.Right)) createClassIndex = Wrap(createClassIndex + 1, classValues.Length);
            else if (keyboard.WasJustPressed(Key.Left)) createClassIndex = Wrap(createClassIndex - 1, classValues.Length);
            else if (keyboard.WasJustPressed(Key.Down)) createKingdomIndex = Wrap(createKingdomIndex + 1, kingdomValues.Length);
            else if (keyboard.WasJustPressed(Key.Up)) createKingdomIndex = Wrap(createKingdomIndex - 1, kingdomValues.Length);
            else if (keyboard.WasJustPressed(Key.Enter))
            {
                createErrorMessage = null;
                createStage = CreateStage.Confirm;
            }
            else if (keyboard.WasJustPressed(Key.Escape)) createStage = CreateStage.Appearance;

            break;

        case CreateStage.Confirm:
            if (keyboard.WasJustPressed(Key.Enter))
            {
                createTask = characterApi!.CreateCharacterAsync(new CreateCharacterRequest
                {
                    SessionToken = options.SessionToken!,
                    Name = createName.Trim(),
                    Class = classValues[createClassIndex],
                    Kingdom = kingdomValues[createKingdomIndex],
                    SkinColorIndex = createSkinIndex,
                    HairStyleIndex = createHairStyleIndex,
                    HairColorIndex = createHairColorIndex,
                    ClothesColorIndex = createClothesColorIndex,
                    AccessoryIndex = createAccessoryIndex,
                });
                createStage = CreateStage.Sending;
            }
            else if (keyboard.WasJustPressed(Key.Escape)) createStage = CreateStage.ClassKingdom;

            break;

        case CreateStage.Sending:
            if (createTask is { IsCompleted: true } task)
            {
                if (task.IsFaulted)
                {
                    createErrorMessage = "Connexion au serveur impossible.";
                    createStage = CreateStage.Confirm;
                }
                else
                {
                    var result = task.Result;
                    if (result.Success)
                    {
                        chosenCharacterId = result.Character!.Id;
                        sceneMode = SceneMode.Loading;
                        RebuildWorldMapForKingdom(result.Character.Kingdom);
                        ConnectAndEnterWorld(chosenCharacterId.Value);
                        _ = CheckStarterNeedAsync(chosenCharacterId.Value);
                    }
                    else
                    {
                        createErrorMessage = result.Error;
                        createStage = CreateStage.Confirm;
                    }
                }

                createTask = null;
            }

            break;
    }
}

static int Wrap(int value, int count) => ((value % count) + count) % count;

async Task LoadInventoryAsync()
{
    if (gameDataApi is null || chosenCharacterId is null)
    {
        return;
    }

    try
    {
        inventoryItems = await gameDataApi.GetInventoryAsync(chosenCharacterId.Value);
    }
    catch (HttpRequestException)
    {
        inventoryItems = [];
    }

    // Voir GDD/demande utilisateur — "le minerai miné/obtenu en combat ne s'affiche pas à la
    // quête du forgeron" : le panneau de recettes affiche des quantités possédées figées au
    // moment du dialogue si l'inventaire change ensuite (minage, butin de combat, achat...)
    // sans que ce panneau soit reconstruit. On le reconstruit donc à chaque rechargement
    // d'inventaire tant qu'il est affiché.
    if (forgeronRecipes.Count > 0)
    {
        BuildForgeronRecipeLines();
    }
}

async Task LoadGuildAsync()
{
    if (gameDataApi is null || chosenCharacterId is null)
    {
        guildLoaded = true;
        return;
    }

    try
    {
        myGuild = await gameDataApi.GetMyGuildAsync(chosenCharacterId.Value);
    }
    catch (HttpRequestException)
    {
        myGuild = null;
    }

    guildLoaded = true;
}

async Task LoadShopCatalogAsync()
{
    if (gameDataApi is null)
    {
        return;
    }

    try
    {
        shopCatalog = await gameDataApi.GetShopCatalogAsync();
    }
    catch (HttpRequestException)
    {
        shopCatalog = [];
    }
}

async Task LoadPartyAsync()
{
    if (gameDataApi is null || chosenCharacterId is null)
    {
        partyLoaded = true;
        return;
    }

    try
    {
        myParty = await gameDataApi.GetMyPartyAsync(chosenCharacterId.Value);
    }
    catch (HttpRequestException)
    {
        myParty = null;
    }

    partyLoaded = true;
}

async Task LoadMonstersAsync()
{
    if (starterApi is null || gameDataApi is null || chosenCharacterId is null)
    {
        monstersLoaded = true;
        return;
    }

    try
    {
        ownedMonsters = await starterApi.GetCharacterMonstersAsync(chosenCharacterId.Value);

        if (speciesById.Count == 0)
        {
            var allSpecies = await gameDataApi.GetAllSpeciesAsync();
            speciesById = allSpecies.ToDictionary(s => s.Id);
        }
    }
    catch (HttpRequestException)
    {
        ownedMonsters = [];
    }

    monsterCursor = Math.Clamp(monsterCursor, 0, Math.Max(0, ownedMonsters.Count - 1));
    monstersLoaded = true;
}

/// <summary>
/// Ouvre un panneau en jeu (voir GDD — boutons Inventaire/Guilde/Boutique/Groupe/Arène/Monstres) :
/// partagé entre les raccourcis clavier (I/G/B/P/V/M) et les boutons cliquables du HUD (voir
/// <see cref="DrawOutdoorHudButtons"/>) pour ne pas dupliquer la logique d'ouverture/chargement.
/// </summary>
void OpenPanel(PanelKind kind)
{
    activePanel = kind;

    switch (kind)
    {
        case PanelKind.Inventory:
            inventoryScrollOffset = 0;
            _ = LoadInventoryAsync();
            break;
        case PanelKind.Guild:
            guildLoaded = false;
            guildMode = GuildPanelMode.None;
            guildTextInput = string.Empty;
            guildSearchResults = [];
            guildSearchDone = false;
            guildActionMessage = null;
            _ = LoadGuildAsync();
            break;
        case PanelKind.Shop:
            shopCursor = 0;
            shopSellCursor = 0;
            shopSellMode = false;
            shopMessage = null;
            _ = LoadShopCatalogAsync();
            _ = LoadInventoryAsync();
            break;
        case PanelKind.Party:
            partyLoaded = false;
            partyJoinPromptOpen = false;
            partyJoinInput = string.Empty;
            partyMessage = null;
            partyCodeCopied = false;
            _ = LoadPartyAsync();
            break;
        case PanelKind.Arena:
            arenaMessage = null;
            break;
        case PanelKind.Monsters:
            monstersLoaded = false;
            monsterGiveItemMode = false;
            monsterMessage = null;
            _ = LoadMonstersAsync();
            _ = LoadInventoryAsync();
            break;
        case PanelKind.Chat:
            chatTextInput = string.Empty;
            break;
        case PanelKind.Auction:
            auctionMessage = null;
            auctionCursor = 0;
            auctionSellMode = false;
            auctionSellCursor = 0;
            auctionSellPrice = 10L;
            _ = LoadInventoryAsync();
            auctionLoadTask = gameDataApi?.GetAuctionListingsAsync(chosenCharacterId ?? Guid.Empty);
            break;
        case PanelKind.Craft:
            forgeronRecipes = [];
            craftRows = [];
            questRecipeCursor = 0;
            craftMessage = null;
            questRecipeTask = gameDataApi?.GetRecipesAsync();
            break;
        case PanelKind.Friends:
            friendsLoaded = false;
            friendCursor = 0;
            friendAddMode = false;
            friendTextInput = string.Empty;
            friendMessage = null;
            friendListTask = chosenCharacterId is null ? null : gameDataApi?.GetFriendsAsync(chosenCharacterId.Value);
            friendPendingTask = chosenCharacterId is null ? null : gameDataApi?.GetPendingFriendRequestsAsync(chosenCharacterId.Value);
            break;
        case PanelKind.Profile:
            profileEditMode = false;
            profileTextInput = string.Empty;
            profileMessage = null;
            profileLoadTask = chosenCharacterId is null ? null : gameDataApi?.GetProfileAsync(chosenCharacterId.Value);
            break;
        case PanelKind.Leaderboard:
            leaderboardRows = [];
            leaderboardLoadTask = gameDataApi?.GetLeaderboardAsync(leaderboardCategories[leaderboardCategoryCursor]);
            break;
        case PanelKind.QuestList:
            questListCursor = 0;
            break;
        case PanelKind.Duel:
            duelTextInput = string.Empty;
            break;
        case PanelKind.GemShop:
            premiumMessage = null;
            premiumLoadTask = options.SessionToken is null ? null : gameDataApi?.GetPremiumStatusAsync(options.SessionToken);
            break;
        case PanelKind.Kingdom:
            kingdomPanelLoadTask = LoadKingdomPanelDataAsync();
            break;
        case PanelKind.Professions:
            professionRows = [];
            professionLoadTask = chosenCharacterId is null ? null : gameDataApi?.GetProfessionsAsync(chosenCharacterId.Value);
            break;
        case PanelKind.BattlePass:
            battlePassMessage = null;
            battlePassStatus = null;
            battlePassLoadTask = chosenCharacterId is null ? null : gameDataApi?.GetBattlePassStatusAsync(chosenCharacterId.Value);
            break;
        case PanelKind.WorldBoss:
            worldBossMessage = null;
            worldBossStatus = null;
            worldBossShowAllTime = false;
            worldBossLoadTask = gameDataApi?.GetWorldBossStatusAsync();
            worldBossLeaderboardLoadTask = gameDataApi?.GetWorldBossLeaderboardAsync(allTime: false);
            break;
    }
}

async Task<PartySummary?> LeavePartyAndClearAsync()
{
    await gameDataApi!.LeavePartyAsync(options.SessionToken!, chosenCharacterId!.Value);
    return null;
}

/// <summary>
/// Panneau Groupe (touche P) : créer un groupe (Entrée), en rejoindre un par identifiant (J,
/// saisie via <see cref="KeyboardState.DrainTypedChars"/> comme la création de personnage), ou
/// le quitter (L). Pas d'invitation en un clic ni de liste de groupes ouverts pour cette première
/// version — l'identifiant doit être communiqué hors jeu (voir Docs/README.md).
/// </summary>
void UpdatePartyPanel()
{
    if (partyActionTask is { IsCompleted: true } actionTask)
    {
        if (actionTask.IsFaulted)
        {
            partyMessage = "Connexion au serveur impossible.";
        }
        else
        {
            myParty = actionTask.Result;
            partyMessage = null;
            partyJoinPromptOpen = false;
            partyJoinInput = string.Empty;
        }

        partyActionTask = null;
        return;
    }

    if (partyActionTask is not null)
    {
        return;
    }

    if (partyJoinPromptOpen)
    {
        // Code à 5 chiffres (voir GDD/demande utilisateur), pas un GUID complet à copier/coller.
        foreach (var typed in keyboard.DrainTypedChars())
        {
            if (partyJoinInput.Length < 5 && char.IsDigit(typed))
            {
                partyJoinInput += typed;
            }
        }

        if (keyboard.WasJustPressed(Key.Backspace) && partyJoinInput.Length > 0)
        {
            partyJoinInput = partyJoinInput[..^1];
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            partyJoinPromptOpen = false;
            partyJoinInput = string.Empty;
            partyMessage = null;
        }
        else if (keyboard.WasJustPressed(Key.Enter))
        {
            if (partyJoinInput.Length == 5)
            {
                partyMessage = null;
                partyActionTask = gameDataApi!.JoinPartyAsync(options.SessionToken!, chosenCharacterId!.Value, partyJoinInput)!;
            }
            else
            {
                partyMessage = "Le code de groupe doit faire 5 chiffres.";
            }
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        partyMessage = null;
        return;
    }

    if (!partyLoaded)
    {
        return;
    }

    if (myParty is null)
    {
        if (keyboard.WasJustPressed(Key.Enter))
        {
            partyMessage = null;
            partyActionTask = gameDataApi!.CreatePartyAsync(options.SessionToken!, chosenCharacterId!.Value)!;
        }
        else if (keyboard.WasJustPressed(Key.J))
        {
            partyJoinPromptOpen = true;
            partyJoinInput = string.Empty;
            partyMessage = null;
        }
    }
    else if (keyboard.WasJustPressed(Key.L))
    {
        partyMessage = null;
        partyActionTask = LeavePartyAndClearAsync();
    }
}

/// <summary>
/// Panneau Monstres (touche M) : liste des créatures possédées, niveau/XP, et un mode "donner un
/// objet" qui consomme un objet d'inventaire contre de l'XP (voir GDD — UI de gestion des
/// montres). **Simplification assumée** : tout objet donne le même montant d'XP fixe, voir
/// <c>MonsterCareService</c> côté serveur.
/// </summary>
/// <summary>
/// Panneau Guilde (touche G) : rejoindre / rechercher / créer une guilde (voir GDD — était
/// jusqu'ici en lecture seule, se contentant d'afficher la guilde déjà rejointe). Pas de
/// fonctionnalité "quitter" côté serveur pour cette version (non demandée).
/// </summary>
void UpdateGuildPanel()
{
    if (guildActionTask is { IsCompleted: true } actionTask)
    {
        if (actionTask.IsFaulted)
        {
            guildActionMessage = "Connexion au serveur impossible.";
        }
        else
        {
            myGuild = actionTask.Result;
            guildActionMessage = null;
            guildMode = GuildPanelMode.None;
            guildTextInput = string.Empty;
        }

        guildActionTask = null;
        return;
    }

    if (guildActionTask is not null)
    {
        return;
    }

    if (guildSearchTask is { IsCompleted: true } searchTask)
    {
        guildSearchResults = searchTask.IsFaulted ? [] : searchTask.Result;
        guildSearchCursor = 0;
        guildSearchDone = true;
        guildSearchTask = null;
        return;
    }

    if (guildSearchTask is not null)
    {
        return;
    }

    if (myGuild is not null)
    {
        if (keyboard.WasJustPressed(Key.Escape))
        {
            activePanel = PanelKind.None;
        }

        return;
    }

    if (guildMode == GuildPanelMode.Create)
    {
        foreach (var typed in keyboard.DrainTypedChars())
        {
            if (guildTextInput.Length < 24 && (char.IsLetterOrDigit(typed) || typed == ' ' || typed == '-' || typed == '_'))
            {
                guildTextInput += typed;
            }
        }

        if (keyboard.WasJustPressed(Key.Backspace) && guildTextInput.Length > 0)
        {
            guildTextInput = guildTextInput[..^1];
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            guildMode = GuildPanelMode.None;
            guildTextInput = string.Empty;
            guildActionMessage = null;
        }
        else if (keyboard.WasJustPressed(Key.Enter) && guildTextInput.Trim().Length >= 3)
        {
            guildActionMessage = null;
            guildActionTask = gameDataApi!.CreateGuildAsync(options.SessionToken!, chosenCharacterId!.Value, guildTextInput.Trim())!;
        }

        return;
    }

    if (guildMode == GuildPanelMode.Search)
    {
        if (!guildSearchDone)
        {
            foreach (var typed in keyboard.DrainTypedChars())
            {
                if (guildTextInput.Length < 24 && (char.IsLetterOrDigit(typed) || typed == ' ' || typed == '-' || typed == '_'))
                {
                    guildTextInput += typed;
                }
            }

            if (keyboard.WasJustPressed(Key.Backspace) && guildTextInput.Length > 0)
            {
                guildTextInput = guildTextInput[..^1];
            }
            else if (keyboard.WasJustPressed(Key.Escape))
            {
                guildMode = GuildPanelMode.None;
                guildTextInput = string.Empty;
                guildActionMessage = null;
            }
            else if (keyboard.WasJustPressed(Key.Enter))
            {
                guildSearchTask = gameDataApi!.SearchGuildsAsync(guildTextInput.Trim().Length > 0 ? guildTextInput.Trim() : null);
            }

            return;
        }

        if (keyboard.WasJustPressed(Key.Escape))
        {
            guildSearchDone = false;
            guildSearchResults = [];
            guildTextInput = string.Empty;
        }
        else if (guildSearchResults.Count > 0)
        {
            if (keyboard.WasJustPressed(Key.Down)) guildSearchCursor = Math.Min(guildSearchCursor + 1, guildSearchResults.Count - 1);
            else if (keyboard.WasJustPressed(Key.Up)) guildSearchCursor = Math.Max(guildSearchCursor - 1, 0);
            else if (keyboard.WasJustPressed(Key.Enter))
            {
                guildActionMessage = null;
                guildActionTask = gameDataApi!.JoinGuildAsync(options.SessionToken!, chosenCharacterId!.Value, guildSearchResults[guildSearchCursor].Id)!;
            }
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
    }
    else if (keyboard.WasJustPressed(Key.C))
    {
        guildMode = GuildPanelMode.Create;
        guildTextInput = string.Empty;
        guildActionMessage = null;
    }
    else if (keyboard.WasJustPressed(Key.R))
    {
        guildMode = GuildPanelMode.Search;
        guildTextInput = string.Empty;
        guildSearchDone = false;
        guildSearchResults = [];
        guildActionMessage = null;
    }
}

void UpdateMonstersPanel()
{
    if (monsterGiveItemTask is { IsCompleted: true } giveTask)
    {
        if (!giveTask.IsFaulted && giveTask.Result is { } updated)
        {
            var index = ownedMonsters.FindIndex(m => m.Id == updated.Id);
            if (index >= 0)
            {
                ownedMonsters[index] = updated;
            }

            monsterMessage = $"{(updated.Nickname.Length > 0 ? updated.Nickname : "Créature")} est maintenant niveau {updated.Level}.";
            _ = LoadInventoryAsync();
        }
        else
        {
            monsterMessage = "Action impossible.";
        }

        monsterGiveItemTask = null;
        monsterGiveItemMode = false;
        return;
    }

    if (monsterGiveItemTask is not null)
    {
        return;
    }

    if (monsterTeamToggleTask is { IsCompleted: true } teamTask)
    {
        if (!teamTask.IsFaulted && teamTask.Result is { } updatedMonster)
        {
            var index = ownedMonsters.FindIndex(m => m.Id == updatedMonster.Id);
            if (index >= 0)
            {
                ownedMonsters[index] = updatedMonster;
            }

            monsterMessage = updatedMonster.IsInActiveTeam ? "Ajouté à l'équipe active." : "Retiré de l'équipe active.";
        }
        else
        {
            monsterMessage = "Équipe déjà complète (4 maximum) ou action impossible.";
        }

        monsterTeamToggleTask = null;
        return;
    }

    if (monsterEquipTask is { IsCompleted: true } equipTask)
    {
        if (!equipTask.IsFaulted && equipTask.Result is { } equipped)
        {
            var index = ownedMonsters.FindIndex(m => m.Id == equipped.Id);
            if (index >= 0)
            {
                ownedMonsters[index] = equipped;
            }

            monsterMessage = "Équipement mis à jour.";
            _ = LoadInventoryAsync();
        }
        else
        {
            monsterMessage = "Action impossible.";
        }

        monsterEquipTask = null;
        monsterEquipMode = false;
        return;
    }

    if (monsterEquipTask is not null)
    {
        return;
    }

    if (monsterGiveItemMode)
    {
        if (keyboard.WasJustPressed(Key.Escape))
        {
            monsterGiveItemMode = false;
        }
        else if (inventoryItems.Count > 0)
        {
            if (keyboard.WasJustPressed(Key.Down)) monsterGiveItemCursor = Math.Min(monsterGiveItemCursor + 1, inventoryItems.Count - 1);
            else if (keyboard.WasJustPressed(Key.Up)) monsterGiveItemCursor = Math.Max(monsterGiveItemCursor - 1, 0);
            else if (keyboard.WasJustPressed(Key.Enter) && ownedMonsters.Count > 0)
            {
                var item = inventoryItems[monsterGiveItemCursor];
                var monster = ownedMonsters[monsterCursor];
                monsterMessage = null;
                monsterGiveItemTask = gameDataApi!.GiveItemToMonsterAsync(options.SessionToken!, monster.Id, item.ItemId);
            }
        }

        return;
    }

    if (monsterEquipMode)
    {
        var equipableItems = inventoryItems.Where(i => i.ItemType is ItemType.Arme or ItemType.Armure or ItemType.Accessoire).ToList();
        if (keyboard.WasJustPressed(Key.Escape))
        {
            monsterEquipMode = false;
        }
        else if (equipableItems.Count > 0)
        {
            monsterEquipCursor = Math.Clamp(monsterEquipCursor, 0, equipableItems.Count - 1);
            if (keyboard.WasJustPressed(Key.Down)) monsterEquipCursor = Math.Min(monsterEquipCursor + 1, equipableItems.Count - 1);
            else if (keyboard.WasJustPressed(Key.Up)) monsterEquipCursor = Math.Max(monsterEquipCursor - 1, 0);
            else if (keyboard.WasJustPressed(Key.Enter) && ownedMonsters.Count > 0)
            {
                var item = equipableItems[monsterEquipCursor];
                var monster = ownedMonsters[monsterCursor];
                monsterMessage = null;
                monsterEquipTask = gameDataApi!.EquipItemAsync(options.SessionToken!, monster.Id, item.ItemId);
            }
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        monsterMessage = null;
        return;
    }

    if (!monstersLoaded || ownedMonsters.Count == 0)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) monsterCursor = Math.Min(monsterCursor + 1, ownedMonsters.Count - 1);
    else if (keyboard.WasJustPressed(Key.Up)) monsterCursor = Math.Max(monsterCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.D) && inventoryItems.Count > 0)
    {
        monsterGiveItemMode = true;
        monsterGiveItemCursor = 0;
        monsterMessage = null;
    }
    else if (keyboard.WasJustPressed(Key.E))
    {
        monsterEquipMode = true;
        monsterEquipCursor = 0;
        monsterMessage = null;
    }
    else if (keyboard.WasJustPressed(Key.R) && gameDataApi is not null)
    {
        // Voir GDD/demande utilisateur — retire l'équipement (arme, sinon armure, sinon
        // accessoire) de la créature sélectionnée.
        var monster = ownedMonsters[monsterCursor];
        var slot = monster.EquippedWeaponItemId is not null ? EquipmentSlot.Weapon
            : monster.EquippedArmorItemId is not null ? EquipmentSlot.Armor
            : monster.EquippedAccessoryItemId is not null ? (EquipmentSlot?)EquipmentSlot.Accessory
            : null;

        if (slot is { } slotToRemove)
        {
            monsterMessage = null;
            monsterEquipTask = gameDataApi.UnequipItemAsync(options.SessionToken!, monster.Id, slotToRemove);
        }
        else
        {
            monsterMessage = "Rien à retirer sur cette créature.";
        }
    }
    else if (keyboard.WasJustPressed(Key.L) && myRank == UserRank.Fondateur && gameDataApi is not null)
    {
        // Voir GDD/demande utilisateur — "ajoute au admin la possibilité d'augmenter le niveau de
        // ces monstres" : +5 niveaux d'un coup sur la créature sélectionnée, outil admin/debug,
        // pas la progression normale par XP.
        var monster = ownedMonsters[monsterCursor];
        monsterMessage = null;
        monsterGiveItemTask = LevelUpAndRefreshAsync(monster.Id);
    }
    else if (keyboard.WasJustPressed(Key.T) && gameDataApi is not null && monsterTeamToggleTask is null)
    {
        var monster = ownedMonsters[monsterCursor];
        monsterMessage = null;
        monsterTeamToggleTask = gameDataApi.SetMonsterActiveTeamAsync(options.SessionToken!, monster.Id, !monster.IsInActiveTeam);
    }
}

/// <summary>
/// Voir GDD/demande utilisateur — "déplacer ce que l'on a dans notre team" (panneau Monstres,
/// touche T) : jusqu'à 4 créatures marquées <see cref="MonsterInstanceData.IsInActiveTeam"/>
/// combattent. Si aucune n'est marquée (comptes existants avant cette fonctionnalité), retombe
/// sur les 4 premières comme avant — pas de changement de comportement pour qui n'y touche pas.
/// </summary>
static List<Guid> SelectActiveTeamIds(List<MonsterInstanceData> monsters)
{
    var active = monsters.Where(m => m.IsInActiveTeam).Select(m => m.Id).Take(4).ToList();
    return active.Count > 0 ? active : monsters.Select(m => m.Id).Take(4).ToList();
}

async Task<MonsterInstanceData?> LevelUpAndRefreshAsync(Guid monsterId)
{
    var result = await gameDataApi!.LevelUpMonsterAsync(options.SessionToken!, monsterId, 5);
    if (!result.Success)
    {
        return null;
    }

    var monsters = await starterApi!.GetCharacterMonstersAsync(chosenCharacterId!.Value);
    return monsters.FirstOrDefault(m => m.Id == monsterId);
}

/// <summary>
/// Panneau Arène (touche V) : choisir un format (1v1/2v2/3v3/4v4, voir GDD — ligues ELO), rejoindre
/// la file d'attente, puis sonder régulièrement le serveur jusqu'à l'appairage (voir
/// <c>ArenaQueueService</c> côté serveur — pas de notification push, un simple sondage toutes les
/// 1.5 secondes tant que la fenêtre Arène reste ouverte).
/// </summary>
void UpdateArenaPanel(float deltaTime)
{
    if (arenaMatchStateTask is { IsCompleted: true } stateTask)
    {
        var state = stateTask.IsFaulted ? null : stateTask.Result;
        arenaMatchStateTask = null;

        if (state is not null)
        {
            combatState = state;
            combatSelectedAction = null;
            combatMessage = null;
            combatReturnScene = SceneMode.Outdoor;
            arenaQueued = false;
            activePanel = PanelKind.None;
            combatVictoryQuestFired = false;
            sceneMode = SceneMode.Combat;
        }
        else
        {
            arenaMessage = "Impossible de recuperer le combat appaire.";
        }

        return;
    }

    if (arenaPollTask is { IsCompleted: true } pollTask)
    {
        var status = pollTask.IsFaulted ? null : pollTask.Result;
        arenaPollTask = null;

        if (status is { IsMatched: true, CombatId: { } combatId })
        {
            arenaMatchStateTask = combatApi!.GetStateAsync(combatId);
        }

        return;
    }

    if (arenaQueueTask is { IsCompleted: true } queueTask)
    {
        arenaQueued = !queueTask.IsFaulted && queueTask.Result;
        arenaMessage = arenaQueued ? null : "Connexion au serveur impossible.";
        arenaQueueTask = null;
        return;
    }

    if (arenaQueueTask is not null || arenaPollTask is not null || arenaMatchStateTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        if (arenaQueued)
        {
            arenaQueued = false;
            arenaMessage = null;
            _ = combatApi!.CancelArenaQueueAsync(chosenCharacterId!.Value);
        }
        else
        {
            activePanel = PanelKind.None;
        }

        return;
    }

    if (arenaQueued)
    {
        arenaPollClock += deltaTime;
        if (arenaPollClock >= 1.5f)
        {
            arenaPollClock = 0f;
            arenaPollTask = combatApi!.GetArenaStatusAsync(chosenCharacterId!.Value);
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) arenaFormatCursor = Math.Min(arenaFormatCursor + 1, arenaFormats.Length - 1);
    else if (keyboard.WasJustPressed(Key.Up)) arenaFormatCursor = Math.Max(arenaFormatCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter))
    {
        arenaMessage = null;
        arenaPollClock = 0f;
        arenaQueueTask = QueueForArenaAsync(arenaFormats[arenaFormatCursor]);
    }
}

async Task<bool> QueueForArenaAsync(ArenaFormat format)
{
    if (combatApi is null || starterApi is null || chosenCharacterId is null || options.SessionToken is null)
    {
        return false;
    }

    try
    {
        var monsters = await starterApi.GetCharacterMonstersAsync(chosenCharacterId.Value);
        var monsterIds = monsters.Select(m => m.Id).ToList();
        return await combatApi.QueueForArenaAsync(options.SessionToken, chosenCharacterId.Value, monsterIds, format);
    }
    catch (HttpRequestException)
    {
        return false;
    }
}

/// <summary>
/// Voir GDD/demande utilisateur — "propose un pvp, si la personne est en team tout les membres
/// doivent accepter" : appelé côté défieur une fois <see cref="TeamDuelReadyPacket"/> reçu (toute
/// l'équipe ciblée a accepté). Ni sa propre équipe de créatures ni celle des autres participants
/// ne sont envoyées : le serveur engage l'équipe active de chaque personnage lui-même (voir
/// <c>CombatService.StartFriendlyTeamDuelAsync</c>).
/// </summary>
async Task<CombatSessionState?> ChallengeTeamDuelAsync(IReadOnlyList<Guid> challengerTeamCharacterIds, IReadOnlyList<Guid> targetTeamCharacterIds)
{
    if (combatApi is null || chosenCharacterId is null || options.SessionToken is null)
    {
        return null;
    }

    try
    {
        var result = await combatApi.ChallengeTeamAsync(options.SessionToken, chosenCharacterId.Value, challengerTeamCharacterIds, targetTeamCharacterIds);
        return result.State;
    }
    catch (HttpRequestException)
    {
        return null;
    }
}

/// <summary>
/// Panneau Tchat (touche T) : Tab bascule entre canal global et canal de guilde, Entrée envoie
/// (rien ne se passe si le champ est vide — pas de message vide sur le réseau), Échap vide le
/// champ de saisie puis ferme le panneau au second appui, comme les autres panneaux avec saisie
/// (voir <see cref="UpdateGuildPanel"/>). La liste des joueurs en ligne (voir GDD — "avec leur
/// grade") est affichée à côté du tchat par <see cref="DrawChatPanel"/>, pas de touche dédiée.
/// </summary>
void UpdateChatPanel()
{
    // Voir GDD/demande utilisateur — "quand on clique sur un pseudo on a ces informations" :
    // bloque le reste de la saisie du tchat tant que la fiche créateur est ouverte, comme un
    // petit panneau modal par-dessus.
    if (creatorCardTarget is not null)
    {
        if (keyboard.WasJustPressed(Key.Escape))
        {
            creatorCardTarget = null;
        }

        return;
    }

    // Voir GDD/demande utilisateur — "discussion privée" avec un ami (voir DrawFriendsPanel) :
    // Tab annule le mode chuchotement et revient au canal global plutôt que de le faire
    // disparaître silencieusement.
    if (keyboard.WasJustPressed(Key.Tab))
    {
        if (chatWhisperTarget is not null)
        {
            chatWhisperTarget = null;
            chatChannel = ChatChannel.Global;
        }
        else
        {
            chatChannel = chatChannel == ChatChannel.Global ? ChatChannel.Guild : ChatChannel.Global;
        }

        return;
    }

    foreach (var typed in keyboard.DrainTypedChars())
    {
        if (chatTextInput.Length < 200 && !char.IsControl(typed))
        {
            chatTextInput += typed;
        }
    }

    if (keyboard.WasJustPressed(Key.Backspace) && chatTextInput.Length > 0)
    {
        chatTextInput = chatTextInput[..^1];
    }
    else if (keyboard.WasJustPressed(Key.Escape))
    {
        if (chatTextInput.Length > 0)
        {
            chatTextInput = string.Empty;
        }
        else
        {
            chatWhisperTarget = null;
            activePanel = PanelKind.None;
        }
    }
    else if (keyboard.WasJustPressed(Key.Enter) && chatTextInput.Trim().Length > 0)
    {
        if (chatWhisperTarget is { } target)
        {
            connection?.SendChatMessage(chatTextInput.Trim(), ChatChannel.Prive, target);
        }
        else
        {
            connection?.SendChatMessage(chatTextInput.Trim(), chatChannel);
        }

        chatTextInput = string.Empty;
    }
}

void UpdatePanel(float deltaTime)
{
    if (activePanel == PanelKind.Party)
    {
        UpdatePartyPanel();
        return;
    }

    if (activePanel == PanelKind.Arena)
    {
        UpdateArenaPanel(deltaTime);
        return;
    }

    if (activePanel == PanelKind.Monsters)
    {
        UpdateMonstersPanel();
        return;
    }

    if (activePanel == PanelKind.Guild)
    {
        UpdateGuildPanel();
        return;
    }

    if (activePanel == PanelKind.Chat)
    {
        UpdateChatPanel();
        return;
    }

    if (activePanel == PanelKind.Auction)
    {
        UpdateAuctionPanel();
        return;
    }

    if (activePanel == PanelKind.Craft)
    {
        UpdateCraftPanel();
        return;
    }

    if (activePanel == PanelKind.Friends)
    {
        UpdateFriendsPanel();
        return;
    }

    if (activePanel == PanelKind.Profile)
    {
        UpdateProfilePanel();
        return;
    }

    if (activePanel == PanelKind.Leaderboard)
    {
        UpdateLeaderboardPanel();
        return;
    }

    if (activePanel == PanelKind.QuestList)
    {
        UpdateQuestListPanel();
        return;
    }

    if (activePanel == PanelKind.Duel)
    {
        UpdateDuelPanel();
        return;
    }

    if (activePanel == PanelKind.GemShop)
    {
        UpdateGemShopPanel();
        return;
    }

    if (activePanel == PanelKind.Kingdom)
    {
        UpdateKingdomPanel();
        return;
    }

    if (activePanel == PanelKind.Professions)
    {
        UpdateProfessionsPanel();
        return;
    }

    if (activePanel == PanelKind.BattlePass)
    {
        UpdateBattlePassPanel();
        return;
    }

    if (activePanel == PanelKind.WorldBoss)
    {
        UpdateWorldBossPanel();
        return;
    }

    if (activePanel == PanelKind.Inventory)
    {
        UpdateInventoryPanel();
        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        shopMessage = null;
        return;
    }

    if (activePanel != PanelKind.Shop)
    {
        return;
    }

    if (shopBuyTask is { IsCompleted: true } task)
    {
        shopMessage = task.IsFaulted ? "Connexion au serveur impossible." : task.Result.Message;
        if (!task.IsFaulted && task.Result.Success)
        {
            // Voir GDD/demande utilisateur — la liste de vente reflète l'inventaire courant :
            // rafraîchi après un achat/une vente pour ne pas afficher des quantités périmées.
            _ = LoadInventoryAsync();
            // Voir GDD/demande utilisateur — quête 5 "Les rouages du commerce".
            _ = CompleteStoryQuestAsync("Les rouages du commerce");
        }

        shopBuyTask = null;
        return;
    }

    if (shopBuyTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Tab))
    {
        shopSellMode = !shopSellMode;
        shopSellCursor = 0;
        shopMessage = null;
        return;
    }

    if (shopSellMode)
    {
        if (inventoryItems.Count == 0)
        {
            return;
        }

        if (keyboard.WasJustPressed(Key.Down)) shopSellCursor = Math.Min(shopSellCursor + 1, inventoryItems.Count - 1);
        else if (keyboard.WasJustPressed(Key.Up)) shopSellCursor = Math.Max(shopSellCursor - 1, 0);
        else if (keyboard.WasJustPressed(Key.Enter))
        {
            shopMessage = null;
            var entry = inventoryItems[shopSellCursor];
            shopBuyTask = gameDataApi!.SellItemAsync(options.SessionToken!, chosenCharacterId!.Value, entry.ItemId, 1);
        }

        return;
    }

    if (shopCatalog.Count == 0)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) shopCursor = Math.Min(shopCursor + 1, shopCatalog.Count - 1);
    else if (keyboard.WasJustPressed(Key.Up)) shopCursor = Math.Max(shopCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter))
    {
        shopMessage = null;
        shopBuyTask = gameDataApi!.BuyItemAsync(options.SessionToken!, chosenCharacterId!.Value, shopCatalog[shopCursor].ItemId);
    }
}

/// <summary>
/// Hôtel des ventes entre joueurs (voir GDD/demande utilisateur — "un HDV où les joueurs mettent
/// en vente et achètent, moins cher que chez la marchande") : Tab bascule Parcourir/Vendre,
/// comme le panneau Boutique. En vente, le prix suggéré (70% du prix boutique, 10 or par défaut
/// si l'objet n'est pas en boutique) est ajustable avec Gauche/Droite avant de déposer TOUT le
/// stock sélectionné — pas de vente partielle pour cette première version.
/// </summary>
void UpdateAuctionPanel()
{
    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        auctionMessage = null;
        return;
    }

    if (auctionActionTask is { IsCompleted: true } actionTask)
    {
        auctionMessage = actionTask.IsFaulted ? "Connexion au serveur impossible." : actionTask.Result.Message;
        auctionActionTask = null;
        auctionLoadTask = gameDataApi?.GetAuctionListingsAsync(chosenCharacterId ?? Guid.Empty);
        _ = LoadInventoryAsync();
        return;
    }

    if (auctionLoadTask is { IsCompleted: true } loadTask)
    {
        auctionListings = loadTask.IsFaulted ? [] : loadTask.Result;
        auctionCursor = Math.Clamp(auctionCursor, 0, Math.Max(0, auctionListings.Count - 1));
        auctionLoadTask = null;
        return;
    }

    if (auctionActionTask is not null || auctionLoadTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Tab))
    {
        auctionSellMode = !auctionSellMode;
        auctionSellCursor = 0;
        auctionMessage = null;
        return;
    }

    if (auctionSellMode)
    {
        if (inventoryItems.Count == 0)
        {
            return;
        }

        if (keyboard.WasJustPressed(Key.Down)) auctionSellCursor = Math.Min(auctionSellCursor + 1, inventoryItems.Count - 1);
        else if (keyboard.WasJustPressed(Key.Up)) auctionSellCursor = Math.Max(auctionSellCursor - 1, 0);
        else if (keyboard.WasJustPressed(Key.Left)) auctionSellPrice = Math.Max(1, auctionSellPrice - 5);
        else if (keyboard.WasJustPressed(Key.Right)) auctionSellPrice += 5;
        else if (keyboard.WasJustPressed(Key.Enter))
        {
            var entry = inventoryItems[auctionSellCursor];
            auctionMessage = null;
            auctionActionTask = gameDataApi!.CreateAuctionListingAsync(
                options.SessionToken!, chosenCharacterId!.Value, entry.ItemId, entry.Quantity, auctionSellPrice);
        }

        return;
    }

    if (auctionListings.Count == 0)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) auctionCursor = Math.Min(auctionCursor + 1, auctionListings.Count - 1);
    else if (keyboard.WasJustPressed(Key.Up)) auctionCursor = Math.Max(auctionCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter))
    {
        var listing = auctionListings[auctionCursor];
        auctionMessage = null;
        auctionActionTask = listing.IsMine
            ? gameDataApi!.CancelAuctionListingAsync(options.SessionToken!, chosenCharacterId!.Value, listing.ListingId)
            : gameDataApi!.BuyAuctionListingAsync(options.SessionToken!, chosenCharacterId!.Value, listing.ListingId);
    }
}

async Task<CombatResult> StartWildCombatAsync()
{
    if (combatApi is null || starterApi is null || gameDataApi is null || chosenCharacterId is null || options.SessionToken is null)
    {
        return new CombatResult(null, "Connexion requise.");
    }

    try
    {
        var inventory = await gameDataApi.GetInventoryAsync(chosenCharacterId.Value);
        captureSphereItemId = inventory.FirstOrDefault(i => i.ItemType == ItemType.ObjetDeCapture)?.ItemId;

        var monsters = await starterApi.GetCharacterMonstersAsync(chosenCharacterId.Value);
        var monsterIds = SelectActiveTeamIds(monsters);

        var species = await starterApi.GetStarterSpeciesAsync();
        if (species.Count == 0)
        {
            return new CombatResult(null, "Aucune créature sauvage disponible.");
        }

        var wildSpecies = species[Random.Shared.Next(species.Count)];
        return await combatApi.StartAsync(options.SessionToken, chosenCharacterId.Value, monsterIds, wildSpecies.Id);
    }
    catch (HttpRequestException)
    {
        return new CombatResult(null, "Connexion au serveur impossible.");
    }
}

/// <summary>
/// Voir GDD/demande utilisateur — "les donjons doivent être comme The Binding of Isaac : des
/// salles aléatoires avec coffre/monstre etc, mais où on se déplace nous-même de salle en salle"
/// (voir DungeonFloorGenerator côté serveur — disposition en grille). Le combat démarre
/// automatiquement en entrant dans une salle Monstre/MiniBoss/Boss/BossLegendaire non résolue
/// (voir <see cref="StartDungeonRoomCombatAsync"/>) ; un coffre (salle Coffre) s'ouvre avec E ;
/// les autres types de salle (Énigme/Piège/Marchand/Événement/Autel/Salle secrète — non simulés,
/// voir Docs/README.md) se résolvent automatiquement, une seule fois, à l'entrée. Une fois toutes
/// les salles résolues, E descend à l'étage suivant (simplification assumée : pas d'escalier
/// positionné sur la grille, l'étage entier doit être "nettoyé" — voir Docs/README.md).
/// </summary>
void UpdateDungeonCorridor(float deltaTime)
{
    if (combatStartTask is not null)
    {
        return; // Démarrage de combat en cours — géré par le bloc scène-agnostique plus haut.
    }

    if (dungeonFloorTask is { IsCompleted: true } floorTask)
    {
        dungeonFloor = floorTask.IsFaulted ? null : floorTask.Result;
        dungeonFloorTask = null;
        dungeonRoomMessage = dungeonFloor is null ? "Impossible de charger cet étage." : null;

        if (dungeonFloor is not null)
        {
            var startRoom = dungeonFloor.Rooms.FirstOrDefault(r => r.IsStart) ?? dungeonFloor.Rooms[0];
            dungeonRoomIndex = startRoom.Index;
            dungeonPlayerPos = new Vector2(0.5f, 0.5f);
            dungeonClearedRooms = [];
            dungeonLastAutoFightRoomIndex = -1;
            dungeonClickTarget = null;
        }

        return;
    }

    if (dungeonChestTask is { IsCompleted: true } chestTask)
    {
        var gold = chestTask.IsFaulted ? null : chestTask.Result;
        dungeonChestTask = null;
        dungeonClearedRooms.Add(dungeonRoomIndex);
        dungeonRoomMessage = gold is { } g ? $"Vous trouvez {g} pieces d'or !" : "Le coffre est vide.";
        return;
    }

    if (dungeonFloorTask is not null || dungeonChestTask is not null || dungeonFloor is null)
    {
        return;
    }

    if (dungeonEncounterPreviewTask is { IsCompleted: true } previewTask)
    {
        dungeonEncounterPreview = previewTask.IsFaulted ? null : previewTask.Result;
        dungeonEncounterPreviewTask = null;
    }

    // Voir GDD/demande utilisateur — "avant de quitter le donjon ajoute un texte pour demander
    // si il est sûr" : Échap ouvre une confirmation plutôt que de sortir immédiatement ; Entrée
    // confirme, Échap à nouveau annule.
    if (dungeonExitConfirmOpen)
    {
        if (keyboard.WasJustPressed(Key.Enter))
        {
            dungeonExitConfirmOpen = false;
            sceneMode = SceneMode.Outdoor;
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            dungeonExitConfirmOpen = false;
        }

        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        dungeonExitConfirmOpen = true;
        return;
    }

    var room = dungeonFloor.Rooms.First(r => r.Index == dungeonRoomIndex);
    var isCleared = dungeonClearedRooms.Contains(dungeonRoomIndex);
    var allCleared = dungeonClearedRooms.Count >= dungeonFloor.Rooms.Count;

    // Voir GDD/demande utilisateur — "dans les donjons ajoute le déplacement au clic" : clic dans
    // la salle (voir DungeonRoomScreenRect, même géométrie que le rendu) = marcher vers ce point.
    if (mouse.WasButtonJustPressed(MouseButton.Left))
    {
        var (roomTopLeft, roomSize) = DungeonRoomScreenRect(uiCamera.ViewportWidth, uiCamera.ViewportHeight);
        var relative = (mouse.Position - roomTopLeft) / roomSize;
        if (relative.X is >= 0f and <= 1f && relative.Y is >= 0f and <= 1f)
        {
            dungeonClickTarget = relative;
        }
    }

    // Voir remarque ci-dessus — "l'étage entier nettoyé" tient lieu d'escalier pour cette
    // première version, accessible depuis n'importe quelle salle une fois toutes résolues.
    if (allCleared && keyboard.WasJustPressed(Key.E))
    {
        dungeonFloorNumber++;
        dungeonFloor = null;
        dungeonRoomMessage = null;
        dungeonEncounterPreview = null;
        dungeonEncounterPreviewTask = null;
        dungeonEncounterPreviewRoomIndex = -1;
        dungeonFloorTask = gameDataApi!.GetDungeonFloorAsync(worldMap.DungeonId, dungeonFloorNumber);
        return;
    }

    if (!isCleared)
    {
        var isMonsterRoom = room.EncounterType is DungeonEncounterType.Monstre or DungeonEncounterType.MiniBoss
            or DungeonEncounterType.Boss or DungeonEncounterType.BossLegendaire;

        if (isMonsterRoom)
        {
            // Voir GDD/demande utilisateur — "voir les ennemis avant de les combattre, comme
            // Pokémon Épée" : aperçu chargé dès l'entrée, avant même que le combat démarre.
            if (dungeonEncounterPreviewRoomIndex != dungeonRoomIndex && dungeonEncounterPreviewTask is null)
            {
                dungeonEncounterPreviewRoomIndex = dungeonRoomIndex;
                dungeonEncounterPreviewTask = gameDataApi!.GetDungeonEncounterPreviewAsync(worldMap.DungeonId, dungeonFloorNumber, dungeonRoomIndex);
            }

            // Le combat démarre tout seul en entrant (façon Isaac — les portes se "verrouillent"
            // tant que la salle n'est pas nettoyée), mais une seule fois par visite (voir
            // dungeonLastAutoFightRoomIndex) — sinon une défaite le redéclencherait aussitôt en
            // boucle puisque la salle reste "non nettoyée" tant qu'on ne l'a pas gagné.
            if (dungeonLastAutoFightRoomIndex != dungeonRoomIndex)
            {
                dungeonLastAutoFightRoomIndex = dungeonRoomIndex;
                combatMessage = null;
                combatReturnScene = SceneMode.Interior;
                combatStartTask = StartDungeonRoomCombatAsync(dungeonFloorNumber, dungeonRoomIndex);
                return;
            }

            MoveWithinDungeonRoom(room, deltaTime);
            return;
        }

        if (room.EncounterType == DungeonEncounterType.Coffre)
        {
            if (keyboard.WasJustPressed(Key.E))
            {
                dungeonChestTask = gameDataApi!.OpenChestAsync(options.SessionToken!, chosenCharacterId!.Value, worldMap.DungeonId, dungeonFloorNumber, dungeonRoomIndex);
            }
        }
        else
        {
            // Voir GDD/demande utilisateur — types de salle "non simulés" : résolus une seule
            // fois, automatiquement, à l'entrée (rien à appuyer, contrairement au coffre).
            dungeonClearedRooms.Add(dungeonRoomIndex);
            dungeonRoomMessage = room.IsStart ? null : DungeonRoomFlavor(room.EncounterType, false);
        }
    }

    MoveWithinDungeonRoom(room, deltaTime);
}

/// <summary>
/// Voir GDD/demande utilisateur — "on se déplace nous-même de salle en salle" : déplacement
/// continu (mêmes touches que l'extérieur) dans les limites de la salle courante, sauf près d'une
/// porte (voir <see cref="DungeonRoom.North"/>/.../<see cref="DungeonRoom.West"/>) où franchir le
/// bord fait passer à la salle voisine correspondante sur la grille.
/// </summary>
void MoveWithinDungeonRoom(DungeonRoom room, float deltaTime)
{
    var direction = Vector2.Zero;
    if (keyboard.IsDown(Key.W) || keyboard.IsDown(Key.Up)) direction.Y -= 1;
    if (keyboard.IsDown(Key.S) || keyboard.IsDown(Key.Down)) direction.Y += 1;
    if (keyboard.IsDown(Key.A) || keyboard.IsDown(Key.Left)) direction.X -= 1;
    if (keyboard.IsDown(Key.D) || keyboard.IsDown(Key.Right)) direction.X += 1;

    if (direction != Vector2.Zero)
    {
        // Une touche de déplacement annule un déplacement au clic en cours.
        dungeonClickTarget = null;
    }
    else if (dungeonClickTarget is { } target)
    {
        // Voir GDD/demande utilisateur — "dans les donjons ajoute le déplacement au clic".
        var toTarget = target - dungeonPlayerPos;
        if (toTarget.LengthSquared() < 0.0006f)
        {
            dungeonClickTarget = null;
        }
        else
        {
            direction = toTarget;
        }
    }

    if (direction == Vector2.Zero)
    {
        return;
    }

    const float speed = 0.6f;
    const float bound = 0.04f;
    var next = dungeonPlayerPos + Vector2.Normalize(direction) * speed * deltaTime;

    // Voir GDD/demande utilisateur — "on ne peut pas changer de salle" : le déplacement normal
    // borne la position à [bound, 1-bound] (voir le Clamp plus bas), qui n'atteint donc jamais
    // littéralement 0/1 — comparer à ces bornes réelles plutôt qu'à 0f/1f, sans quoi le joueur
    // restait bloqué contre le mur avant même de pouvoir déclencher une transition.
    if (next.Y < bound && room.North) { TransitionDungeonRoom(room.GridX, room.GridY - 1, new Vector2(next.X, 1f - bound - 0.02f)); return; }
    if (next.Y > 1f - bound && room.South) { TransitionDungeonRoom(room.GridX, room.GridY + 1, new Vector2(next.X, bound + 0.02f)); return; }
    if (next.X < bound && room.West) { TransitionDungeonRoom(room.GridX - 1, room.GridY, new Vector2(1f - bound - 0.02f, next.Y)); return; }
    if (next.X > 1f - bound && room.East) { TransitionDungeonRoom(room.GridX + 1, room.GridY, new Vector2(bound + 0.02f, next.Y)); return; }

    dungeonPlayerPos = new Vector2(Math.Clamp(next.X, bound, 1f - bound), Math.Clamp(next.Y, bound, 1f - bound));
}

void TransitionDungeonRoom(int gridX, int gridY, Vector2 enterAt)
{
    var target = dungeonFloor!.Rooms.FirstOrDefault(r => r.GridX == gridX && r.GridY == gridY);
    if (target is null)
    {
        return;
    }

    dungeonRoomIndex = target.Index;
    dungeonPlayerPos = new Vector2(Math.Clamp(enterAt.X, 0.04f, 0.96f), Math.Clamp(enterAt.Y, 0.04f, 0.96f));
    dungeonRoomMessage = null;
    dungeonLastAutoFightRoomIndex = -1;
    dungeonClickTarget = null;
}

async Task<CombatResult> StartDungeonRoomCombatAsync(int floorNumber, int roomIndex)
{
    if (combatApi is null || starterApi is null || gameDataApi is null || chosenCharacterId is null || options.SessionToken is null)
    {
        return new CombatResult(null, "Connexion requise.");
    }

    try
    {
        var inventory = await gameDataApi.GetInventoryAsync(chosenCharacterId.Value);
        captureSphereItemId = inventory.FirstOrDefault(i => i.ItemType == ItemType.ObjetDeCapture)?.ItemId;

        var monsters = await starterApi.GetCharacterMonstersAsync(chosenCharacterId.Value);
        var monsterIds = SelectActiveTeamIds(monsters);

        return await combatApi.StartDungeonCombatAsync(options.SessionToken, chosenCharacterId.Value, monsterIds, worldMap.DungeonId, floorNumber, roomIndex);
    }
    catch (HttpRequestException)
    {
        return new CombatResult(null, "Connexion au serveur impossible.");
    }
}

/// <summary>
/// Rencontre sauvage hors donjon (voir GDD — difficulté scalée sur le niveau du chef de groupe,
/// voir <c>PartyService.ResolveScalingReferenceAsync</c> côté serveur). Contrairement à
/// <see cref="StartWildCombatAsync"/> (stub du donjon, espèce commune tirée côté Client), c'est
/// le serveur qui choisit l'espèce via <c>POST /api/combat/start-wild</c>.
/// </summary>
async Task<CombatResult> StartWildEncounterOutdoorAsync()
{
    if (combatApi is null || starterApi is null || gameDataApi is null || chosenCharacterId is null || options.SessionToken is null)
    {
        return new CombatResult(null, "Connexion requise.");
    }

    try
    {
        var inventory = await gameDataApi.GetInventoryAsync(chosenCharacterId.Value);
        captureSphereItemId = inventory.FirstOrDefault(i => i.ItemType == ItemType.ObjetDeCapture)?.ItemId;

        var monsters = await starterApi.GetCharacterMonstersAsync(chosenCharacterId.Value);
        var monsterIds = SelectActiveTeamIds(monsters);

        return await combatApi.StartWildEncounterAsync(options.SessionToken, chosenCharacterId.Value, monsterIds);
    }
    catch (HttpRequestException)
    {
        return new CombatResult(null, "Connexion au serveur impossible.");
    }
}

void SendCombatAction(CombatActionType actionType, int x, int y, int? captureItemId = null)
{
    if (combatApi is null || combatState is null || options.SessionToken is null)
    {
        return;
    }

    combatActionTask = combatApi.SubmitActionAsync(options.SessionToken, combatState.CombatId, actionType, x, y, captureItemId);
    lastSubmittedCombatAction = actionType;
    combatSelectedAction = null;
}

void UpdateCombat(float deltaTime)
{
    if (combatActionTask is { IsCompleted: true } task)
    {
        if (task.IsFaulted || !task.Result.IsSuccess)
        {
            combatMessage = task.IsFaulted ? "Connexion au serveur impossible." : task.Result.Error;
        }
        else
        {
            combatState = task.Result.State;
            combatMessage = null;

            // Voir GDD/demande utilisateur — quête 3 "Un allié à quatre pattes" : approximation
            // assumée (la capture réussit selon un jet côté serveur, non exposé tel quel ici) —
            // déclenché dès qu'une Carte de capture est utilisée avec succès (action acceptée),
            // le serveur ignore silencieusement l'appel si ce n'est pas/plus l'étape active.
            if (lastSubmittedCombatAction == CombatActionType.Capture)
            {
                _ = CompleteStoryQuestAsync("Un allié à quatre pattes");
            }
        }

        combatActionTask = null;
        return;
    }

    if (combatState is null || combatActionTask is not null)
    {
        return;
    }

    if (combatPollTask is { IsCompleted: true } pollTask)
    {
        // combatActionTask reste prioritaire : si une action vient d'être soumise entre le
        // lancement de ce sondage et sa réponse, on ignore une réponse de sondage potentiellement
        // plus ancienne plutôt que d'écraser l'état frais qu'elle vient de renvoyer.
        if (!pollTask.IsFaulted && pollTask.Result is { } freshState && combatActionTask is null)
        {
            combatState = freshState;
        }

        combatPollTask = null;
    }

    if (combatState.IsFinished)
    {
        // Voir GDD/demande utilisateur — quête 2 "Faire ses preuves" : déclenché une seule fois
        // par combat (combatVictoryQuestFired réinitialisé à chaque nouveau combat démarré) pour
        // ne pas spammer l'appel serveur tant que l'écran de résultat reste affiché.
        if (combatState.WinningTeam == 0 && !combatVictoryQuestFired)
        {
            combatVictoryQuestFired = true;
            _ = CompleteStoryQuestAsync("Faire ses preuves");
        }

        UpdateLoot(deltaTime);
        return;
    }

    combatPollClock += deltaTime;
    if (combatPollClock >= CombatPollIntervalSeconds && combatPollTask is null && combatApi is not null)
    {
        combatPollClock = 0f;
        combatPollTask = combatApi.GetStateAsync(combatState.CombatId);
    }

    var myTurn = combatState.CurrentTurnCombatantId is { } currentId
        && combatState.Combatants.FirstOrDefault(c => c.Id == currentId) is { Team: 0 };

    if (!myTurn)
    {
        return;
    }

    if (combatSelectedAction is null)
    {
        var current = combatState.Combatants.First(c => c.Id == combatState.CurrentTurnCombatantId);
        var isImmediateAbility = current.Type == MonsterType.Soigneur;

        if (keyboard.WasJustPressed(Key.Number1))
        {
            combatSelectedAction = CombatActionType.Move;
            combatCursorX = current.PositionX;
            combatCursorY = current.PositionY;
        }
        else if (keyboard.WasJustPressed(Key.Number2))
        {
            combatSelectedAction = CombatActionType.Attack;
            combatCursorX = current.PositionX;
            combatCursorY = current.PositionY;
        }
        else if (keyboard.WasJustPressed(Key.Number3))
        {
            SendCombatAction(CombatActionType.Pass, 0, 0);
        }
        else if (keyboard.WasJustPressed(Key.Number4))
        {
            // Le Soigneur cible automatiquement l'allié le plus affaibli côté serveur (voir
            // CombatEngine.ResolveSpecialAbility) : pas de visée nécessaire, contrairement aux
            // autres types dont la capacité s'utilise comme une attaque ciblée.
            if (isImmediateAbility)
            {
                SendCombatAction(CombatActionType.SpecialAbility, 0, 0);
            }
            else
            {
                combatSelectedAction = CombatActionType.SpecialAbility;
                combatCursorX = current.PositionX;
                combatCursorY = current.PositionY;
            }
        }
        else if (keyboard.WasJustPressed(Key.Number5) && captureSphereItemId is not null)
        {
            combatSelectedAction = CombatActionType.Capture;
            combatCursorX = current.PositionX;
            combatCursorY = current.PositionY;
        }
        else if (keyboard.WasJustPressed(Key.Number6) && !combatState.IsDungeonCombat)
        {
            // Fuite (voir GDD/demande utilisateur — "un bouton pour fuir les combats, impossible
            // en donjon") : lu depuis combatState.IsDungeonCombat (autoritaire, renvoyé par le
            // serveur), pas depuis interiorIsDungeon (état de scène local resté périmé si le
            // joueur était déjà passé par un donjon plus tôt dans la session — voir bug corrigé).
            SendCombatAction(CombatActionType.Flee, 0, 0);
        }
    }
    else
    {
        if (keyboard.WasJustPressed(Key.Right)) combatCursorX = Math.Min(combatCursorX + 1, combatState.GridWidth - 1);
        else if (keyboard.WasJustPressed(Key.Left)) combatCursorX = Math.Max(combatCursorX - 1, 0);
        else if (keyboard.WasJustPressed(Key.Down)) combatCursorY = Math.Min(combatCursorY + 1, combatState.GridHeight - 1);
        else if (keyboard.WasJustPressed(Key.Up)) combatCursorY = Math.Max(combatCursorY - 1, 0);
        else if (keyboard.WasJustPressed(Key.Enter))
        {
            var action = combatSelectedAction.Value;
            SendCombatAction(action, combatCursorX, combatCursorY, action == CombatActionType.Capture ? captureSphereItemId : null);
        }
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            combatSelectedAction = null;
        }
        else if (mouse.WasButtonJustPressed(MouseButton.Left))
        {
            // Clic direct sur une case pour agir (voir retour utilisateur — "on doit pouvoir
            // cliquer pour faire les actions, pas seulement au clavier"), équivalent à déplacer
            // le curseur là puis appuyer sur Entrée.
            var (originX, originY, cellSize) = CombatGridGeometry();
            var clickedX = (int)MathF.Floor((mouse.Position.X - originX) / cellSize);
            var clickedY = (int)MathF.Floor((mouse.Position.Y - originY) / cellSize);

            if (clickedX >= 0 && clickedX < combatState.GridWidth && clickedY >= 0 && clickedY < combatState.GridHeight)
            {
                combatCursorX = clickedX;
                combatCursorY = clickedY;
                var action = combatSelectedAction.Value;
                SendCombatAction(action, clickedX, clickedY, action == CombatActionType.Capture ? captureSphereItemId : null);
            }
        }
    }
}

/// <summary>
/// Butin de victoire (voir GDD — 4 objets, tirage aléatoire en cas d'égalité) affiché après un
/// combat gagné, avant de revenir à la scène d'intérieur. Si le combat ne produit pas de butin
/// (capture, ou catalogue d'objets vide côté serveur) <see cref="activeLoot"/> reste `null` et le
/// joueur passe directement à l'écran "continuer", comme avant l'ajout de ce système.
/// </summary>
void UpdateLoot(float deltaTime)
{
    if (lootTask is { IsCompleted: true } task)
    {
        if (!task.IsFaulted && task.Result is { } result)
        {
            activeLoot = result;
            lootMessage = null;
        }
        else
        {
            lootMessage = "Connexion au serveur impossible.";
        }

        lootTask = null;
        return;
    }

    if (lootTask is not null)
    {
        return;
    }

    if (activeLoot is null && combatState is { LootId: { } lootId })
    {
        lootTask = combatApi!.GetLootAsync(lootId);
        return;
    }

    if (activeLoot is null || activeLoot.IsResolved)
    {
        if (keyboard.WasJustPressed(Key.Enter) || keyboard.WasJustPressed(Key.Escape))
        {
            // Voir GDD/demande utilisateur — donjon façon Isaac : la salle se "déverrouille"
            // (marquée résolue) uniquement sur victoire (team 0) ; une défaite laisse le joueur
            // retenter la même salle en y restant.
            if (combatReturnScene == SceneMode.Interior && interiorIsDungeon && combatState?.WinningTeam == 0)
            {
                dungeonClearedRooms.Add(dungeonRoomIndex);
                dungeonEncounterPreview = null;
                dungeonEncounterPreviewTask = null;
                dungeonEncounterPreviewRoomIndex = -1;
            }

            sceneMode = combatReturnScene;
            combatState = null;
            combatSelectedAction = null;
            activeLoot = null;
            lootMessage = null;
            lootCursor = 0;
            lootPollClock = 0f;
        }

        return;
    }

    // Sondage périodique (voir GDD/demande utilisateur — "le joueur qui n'a pas donné le dernier
    // coup ne peut pas choisir d'objet") : sans ça, ce client ne voyait jamais qu'un coéquipier
    // avait réclamé un objet (ni que le serveur avait résolu le butin après le délai imparti,
    // voir GameInfo.LootChoiceTimeoutSeconds) tant qu'il ne réclamait pas lui-même.
    lootPollClock += deltaTime;
    if (lootPollClock >= CombatPollIntervalSeconds)
    {
        lootPollClock = 0f;
        lootTask = combatApi!.GetLootAsync(activeLoot.LootId);
        return;
    }

    if (keyboard.WasJustPressed(Key.Down)) lootCursor = Math.Min(lootCursor + 1, activeLoot.Items.Count - 1);
    else if (keyboard.WasJustPressed(Key.Up)) lootCursor = Math.Max(lootCursor - 1, 0);
    else if (keyboard.WasJustPressed(Key.Enter))
    {
        lootMessage = null;
        lootTask = combatApi!.ClaimLootAsync(options.SessionToken!, activeLoot.LootId, chosenCharacterId!.Value, lootCursor);
    }
}

void UpdateStarterSelection(float deltaTime)
{
    starterAnimClock += deltaTime;

    switch (starterStage)
    {
        case StarterStage.Introduction:
            if (keyboard.WasJustPressed(Key.Enter) || keyboard.WasJustPressed(Key.E))
            {
                starterStage = StarterStage.Choosing;
            }

            break;

        case StarterStage.Choosing:
            if (keyboard.WasJustPressed(Key.Right)) starterCursor = Math.Min(starterCursor + 1, starterChoices.Count - 1);
            else if (keyboard.WasJustPressed(Key.Left)) starterCursor = Math.Max(starterCursor - 1, 0);
            else if (keyboard.WasJustPressed(Key.Down)) starterCursor = Math.Min(starterCursor + StarterColumns, starterChoices.Count - 1);
            else if (keyboard.WasJustPressed(Key.Up)) starterCursor = Math.Max(starterCursor - StarterColumns, 0);
            else if (keyboard.WasJustPressed(Key.Enter))
            {
                starterConfirmIndex = starterCursor;
                starterAnimClock = 0f;
                starterErrorMessage = null;
                starterStage = StarterStage.Confirming;
            }

            break;

        case StarterStage.Confirming:
            if (keyboard.WasJustPressed(Key.Enter))
            {
                var species = starterChoices[starterConfirmIndex!.Value];
                starterRequestTask = starterApi!.ChooseStarterAsync(options.SessionToken!, chosenCharacterId!.Value, species.Id);
                starterStage = StarterStage.Sending;
            }
            else if (keyboard.WasJustPressed(Key.Escape))
            {
                starterConfirmIndex = null;
                starterStage = StarterStage.Choosing;
            }

            break;

        case StarterStage.Sending:
            if (starterRequestTask is { IsCompleted: true } task)
            {
                if (task.IsFaulted)
                {
                    starterErrorMessage = "Connexion au serveur impossible.";
                    starterStage = StarterStage.Confirming;
                }
                else
                {
                    var result = task.Result;
                    if (result.Success)
                    {
                        lock (stateLock)
                        {
                            statusMessage = result.Message;
                        }

                        sceneMode = SceneMode.Outdoor;
                    }
                    else
                    {
                        starterErrorMessage = result.Message;
                        starterStage = StarterStage.Confirming;
                    }
                }

                starterRequestTask = null;
            }

            break;
    }
}

void DrawPanel(Vector2 topLeft, Vector2 size, Vector4 color) => spriteBatch.Draw(whiteTexture, topLeft, size, color);

/// <summary>
/// Comme <see cref="TextRenderer.DrawCentered"/> mais cliquable : surligne le texte au survol de
/// la souris et retourne vrai s'il vient d'être cliqué (bouton gauche). Voir retour utilisateur —
/// "on doit pouvoir cliquer pour faire les actions et pas seulement au clavier". Les raccourcis
/// clavier existants restent tous fonctionnels ; ceci ajoute une alternative à la souris sans les
/// remplacer.
/// </summary>
bool DrawClickableCentered(string text, Vector2 topCenter, float pixelSize, Vector4 color)
{
    var width = TextRenderer.MeasureWidth(text, pixelSize);
    var height = TextRenderer.LineHeight(pixelSize);
    const float pad = 8f;
    var topLeft = topCenter - new Vector2(width / 2f + pad, 0f);
    var boxSize = new Vector2(width + pad * 2, height + pad);

    var mousePos = mouse.Position;
    var isHovered = mousePos.X >= topLeft.X && mousePos.X <= topLeft.X + boxSize.X
        && mousePos.Y >= topLeft.Y - pad && mousePos.Y <= topLeft.Y + boxSize.Y;

    if (isHovered)
    {
        DrawPanel(topLeft, boxSize, new Vector4(1f, 1f, 1f, 0.12f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, text, topCenter, pixelSize, isHovered ? Vector4.Lerp(color, Vector4.One, 0.35f) : color);

    return isHovered && mouse.WasButtonJustPressed(MouseButton.Left);
}

/// <summary>
/// Voir GDD/demande utilisateur — "fait en sorte que tout puisse se faire au clic et pas que au
/// clavier" : équivalent de <see cref="DrawClickableCentered"/> pour une ligne de liste alignée à
/// gauche (Boutique, Hôtel des ventes, Monstres, Téléporteur, panel admin, ...) plutôt qu'un
/// bouton centré. Étend la zone cliquable sur toute la largeur du panneau (pas seulement le
/// texte) pour rester facile à cliquer même avec un libellé court.
/// </summary>
bool DrawClickableRow(string text, Vector2 topLeft, float rowWidth, float pixelSize, Vector4 color)
{
    var height = TextRenderer.LineHeight(pixelSize);
    var mousePos = mouse.Position;
    var isHovered = mousePos.X >= topLeft.X - 4f && mousePos.X <= topLeft.X + rowWidth
        && mousePos.Y >= topLeft.Y - 2f && mousePos.Y <= topLeft.Y + height + 2f;

    if (isHovered)
    {
        DrawPanel(topLeft - new Vector2(4f, 2f), new Vector2(rowWidth + 4f, height + 4f), new Vector4(1f, 1f, 1f, 0.08f));
    }

    TextRenderer.Draw(spriteBatch, whiteTexture, text, topLeft, pixelSize, isHovered ? Vector4.Lerp(color, Vector4.One, 0.35f) : color);

    return isHovered && mouse.WasButtonJustPressed(MouseButton.Left);
}

void DrawIsoDiamond(Vector2 gridPos, float scale, Vector4 color)
{
    var center = IsoMath.GridToIso(gridPos.X, gridPos.Y);
    var halfWidth = IsoMath.TileWidth * scale / 2f;
    var halfHeight = IsoMath.TileHeight * scale / 2f;

    var top = center + new Vector2(0, -halfHeight);
    var right = center + new Vector2(halfWidth, 0);
    var bottom = center + new Vector2(0, halfHeight);
    var left = center + new Vector2(-halfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, top, right, bottom, left, color);
}

void DrawBuilding(Building building)
{
    var groundCenter = IsoMath.GridToIso(building.GridX, building.GridY);
    var roofCenter = groundCenter - new Vector2(0, building.Height * IsoMath.TileHeight);

    var roofTop = roofCenter + new Vector2(0, -IsoMath.TileHeight / 2f);
    var roofRight = roofCenter + new Vector2(IsoMath.TileWidth / 2f, 0);
    var roofBottom = roofCenter + new Vector2(0, IsoMath.TileHeight / 2f);
    var roofLeft = roofCenter + new Vector2(-IsoMath.TileWidth / 2f, 0);

    var groundLeft = groundCenter + new Vector2(-IsoMath.TileWidth / 2f, 0);
    var groundBottom = groundCenter + new Vector2(0, IsoMath.TileHeight / 2f);
    var groundRight = groundCenter + new Vector2(IsoMath.TileWidth / 2f, 0);

    // Mur gauche puis mur droit (faces visibles d'un "cube" isométrique), et enfin le toit.
    spriteBatch.DrawQuad(whiteTexture, roofLeft, roofBottom, groundBottom, groundLeft, building.WallColorLeft);
    spriteBatch.DrawQuad(whiteTexture, roofBottom, roofRight, groundRight, groundBottom, building.WallColorRight);
    spriteBatch.DrawQuad(whiteTexture, roofTop, roofRight, roofBottom, roofLeft, building.RoofColor);

    // Enseigne : un poteau planté devant l'entrée (côté "sud", vers la caméra) et une plaque nommée.
    var postWidth = 3f;
    var postHeight = IsoMath.TileHeight * 0.55f;
    var postBase = groundCenter + new Vector2(0, IsoMath.TileHeight * 0.6f);
    var postTop = postBase - new Vector2(0, postHeight);
    spriteBatch.Draw(whiteTexture, new Vector2(postBase.X - postWidth / 2f, postTop.Y), new Vector2(postWidth, postHeight), WorldMap.SignpostColor);

    // Voir retour utilisateur — "le champ de braise le texte deborde de l'ui" : la plaque avait
    // une largeur fixe (0.46 tuile, ~29px) pensée pour un mot court, mais building.Name porte le
    // nom complet du territoire ("Champ de Braise", "Citadelle de Braise", ...) qui dépassait
    // largement à l'échelle 1.1. La plaque s'adapte désormais à la largeur mesurée du texte
    // (avec une largeur minimale pour ne pas rétrécir les enseignes à mot court).
    const float signTextScale = 0.85f;
    var signText = building.Name.ToUpperInvariant();
    var signTextWidth = TextRenderer.MeasureWidth(signText, signTextScale);
    var plaqueSize = new Vector2(MathF.Max(IsoMath.TileWidth * 0.46f, signTextWidth + 8f), IsoMath.TileHeight * 0.42f);
    var plaquePosition = postTop - new Vector2(plaqueSize.X / 2f, plaqueSize.Y * 0.75f);
    spriteBatch.Draw(whiteTexture, plaquePosition, plaqueSize, WorldMap.SignboardColor);
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, signText,
        plaquePosition + new Vector2(plaqueSize.X / 2f, plaqueSize.Y / 2f - 3f), signTextScale, new Vector4(0.25f, 0.17f, 0.09f, 1f));
}

void DrawPortal(Vector2 gridPos, float animClock)
{
    var pulse = (MathF.Sin(animClock * 2f) + 1f) / 2f;

    DrawIsoDiamond(gridPos, 1.3f, WorldMap.PortalOuterColor);
    DrawIsoDiamond(gridPos, 0.88f, Vector4.Lerp(WorldMap.PortalMidColorDark, WorldMap.PortalMidColorBright, pulse));
    DrawIsoDiamond(gridPos, 0.46f, Vector4.Lerp(WorldMap.PortalMidColorBright, WorldMap.PortalCoreColor, pulse));
}

void DrawFigure(Vector2 gridPos, float bodyHeight, Vector4 roofColor, Vector4 wallLeftColor, Vector4 wallRightColor, Vector4 headColor, float bobPixels, string? label = null)
{
    const float footprint = 0.40f;

    var groundCenter = IsoMath.GridToIso(gridPos.X, gridPos.Y);

    // Ombre au sol : toujours ancrée à la case (ignore le "bob") pour bien fixer le personnage au sol.
    DrawIsoDiamond(gridPos, footprint * 0.85f, new Vector4(0f, 0f, 0f, 0.28f));

    var bobbedGroundCenter = groundCenter - new Vector2(0, bobPixels);
    var halfWidth = IsoMath.TileWidth * footprint / 2f;
    var halfHeight = IsoMath.TileHeight * footprint / 2f;

    var bodyTopCenter = bobbedGroundCenter - new Vector2(0, bodyHeight * IsoMath.TileHeight);

    var bodyTop = bodyTopCenter + new Vector2(0, -halfHeight);
    var bodyRight = bodyTopCenter + new Vector2(halfWidth, 0);
    var bodyBottom = bodyTopCenter + new Vector2(0, halfHeight);
    var bodyLeft = bodyTopCenter + new Vector2(-halfWidth, 0);

    var groundLeft = bobbedGroundCenter + new Vector2(-halfWidth, 0);
    var groundBottom = bobbedGroundCenter + new Vector2(0, halfHeight);
    var groundRight = bobbedGroundCenter + new Vector2(halfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, bodyLeft, bodyBottom, groundBottom, groundLeft, wallLeftColor);
    spriteBatch.DrawQuad(whiteTexture, bodyBottom, bodyRight, groundRight, groundBottom, wallRightColor);
    spriteBatch.DrawQuad(whiteTexture, bodyTop, bodyRight, bodyBottom, bodyLeft, roofColor);

    // Bras : deux petits pavés de part et d'autre du corps, pour casser la silhouette "boîte".
    var armWidth = halfWidth * 0.34f;
    var armHeight = bodyHeight * IsoMath.TileHeight * 0.60f;
    var armTopY = bodyTopCenter.Y + halfHeight * 0.35f;
    spriteBatch.Draw(whiteTexture, new Vector2(bodyLeft.X - armWidth * 0.8f, armTopY), new Vector2(armWidth, armHeight), wallLeftColor);
    spriteBatch.Draw(whiteTexture, new Vector2(bodyRight.X - armWidth * 0.2f, armTopY), new Vector2(armWidth, armHeight), wallRightColor);

    // Tête : un losange nettement plus grand que le corps pour bien se lire comme une tête.
    var headHalfWidth = halfWidth * 0.78f;
    var headHalfHeight = halfHeight * 0.78f;
    var headCenter = bodyTopCenter - new Vector2(0, headHalfHeight * 0.95f);

    var headTop = headCenter + new Vector2(0, -headHalfHeight);
    var headRight = headCenter + new Vector2(headHalfWidth, 0);
    var headBottom = headCenter + new Vector2(0, headHalfHeight);
    var headLeft = headCenter + new Vector2(-headHalfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, headTop, headRight, headBottom, headLeft, headColor);

    // Deux petits points sombres : juste assez pour suggérer un visage sans sprite dédié.
    var eyeSize = new Vector2(headHalfWidth * 0.22f, headHalfWidth * 0.22f);
    var eyeY = headCenter.Y - eyeSize.Y * 0.3f;
    var eyeColor = new Vector4(0.15f, 0.12f, 0.10f, 1f);
    spriteBatch.Draw(whiteTexture, new Vector2(headCenter.X - eyeSize.X * 1.6f, eyeY), eyeSize, eyeColor);
    spriteBatch.Draw(whiteTexture, new Vector2(headCenter.X + eyeSize.X * 0.6f, eyeY), eyeSize, eyeColor);

    if (label is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, label.ToUpperInvariant(),
            new Vector2(headCenter.X, headTop.Y - 14f), 1.6f, new Vector4(1f, 1f, 1f, 0.92f));
    }
}

// Voir GDD/demande utilisateur — panel admin "transformer le skin de tout les joueurs en panneau
// [...] pendant 5min" : recolore tout le monde (soi compris) en bois/panneau et préfixe le nom,
// tant que l'effet diffusé par AdminEffectPacket n'a pas expiré (voir signModeExpiresAtUtc).
bool IsSignModeActive() => DateTime.UtcNow < signModeExpiresAtUtc;

void DrawPlayerFigure(Vector2 gridPos, float bobPixels)
{
    if (IsSignModeActive())
    {
        DrawFigure(gridPos, 0.55f, woodPanelColor, woodPanelOutline, woodPanelColor, woodPanelOutline, bobPixels, "[PANNEAU] Vous");
        return;
    }

    DrawFigure(
        gridPos, 0.55f,
        new Vector4(0.92f, 0.78f, 0.31f, 1f), new Vector4(0.60f, 0.48f, 0.15f, 1f), new Vector4(0.78f, 0.64f, 0.22f, 1f),
        new Vector4(0.92f, 0.80f, 0.68f, 1f), bobPixels, "Vous");
}

void DrawRemotePlayerFigure(RemotePlayer remote, float animClock)
{
    var bob = MathF.Sin(animClock * 2.0f) * 1.0f;

    if (IsSignModeActive())
    {
        DrawFigure(remote.Position, 0.55f, woodPanelColor, woodPanelOutline, woodPanelColor, woodPanelOutline, bob, $"[PANNEAU] {remote.Name}");
        return;
    }

    DrawFigure(
        remote.Position, 0.55f,
        new Vector4(0.35f, 0.62f, 0.88f, 1f), new Vector4(0.20f, 0.38f, 0.58f, 1f), new Vector4(0.28f, 0.50f, 0.72f, 1f),
        new Vector4(0.88f, 0.78f, 0.68f, 1f), bob, remote.Name);
}

void DrawNpcFigure(Npc npc, float animClock)
{
    var bob = MathF.Sin((animClock + npc.AnimationOffset) * 2.2f) * 1.0f;
    DrawFigure(
        new Vector2(npc.GridX, npc.GridY), 0.5f,
        npc.BodyColor, npc.BodyColor * 0.65f, npc.BodyColor * 0.85f,
        npc.HeadColor, bob, npc.Name);
}

void DrawOutdoorHud()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    if (showTutorial)
    {
        DrawTutorialOverlay(w, h);
        return;
    }

    if (activeDialogueNpc is not null)
    {
        DrawDialogueBox(w, h);
    }
    else if (activePanel != PanelKind.None)
    {
        switch (activePanel)
        {
            case PanelKind.Inventory: DrawInventoryPanel(w, h); break;
            case PanelKind.Guild: DrawGuildPanel(w, h); break;
            case PanelKind.Shop: DrawShopPanel(w, h); break;
            case PanelKind.Party: DrawPartyPanel(w, h); break;
            case PanelKind.Arena: DrawArenaPanel(w, h); break;
            case PanelKind.Monsters: DrawMonstersPanel(w, h); break;
            case PanelKind.Chat: DrawChatPanel(w, h); break;
            case PanelKind.Auction: DrawAuctionPanel(w, h); break;
            case PanelKind.Craft: DrawCraftPanel(w, h); break;
            case PanelKind.Friends: DrawFriendsPanel(w, h); break;
            case PanelKind.Profile: DrawProfilePanel(w, h); break;
            case PanelKind.Leaderboard: DrawLeaderboardPanel(w, h); break;
            case PanelKind.QuestList: DrawQuestListPanel(w, h); break;
            case PanelKind.Duel: DrawDuelPanel(w, h); break;
            case PanelKind.GemShop: DrawGemShopPanel(w, h); break;
            case PanelKind.Kingdom: DrawKingdomPanel(w, h); break;
            case PanelKind.Professions: DrawProfessionsPanel(w, h); break;
            case PanelKind.BattlePass: DrawBattlePassPanel(w, h); break;
            case PanelKind.WorldBoss: DrawWorldBossPanel(w, h); break;
        }
    }
    else if (nearbyInteraction is { } interaction)
    {
        var prompt = interaction.Kind switch
        {
            InteractionKind.Npc => $"APPUYEZ SUR E POUR PARLER A {interaction.Label.ToUpperInvariant()}",
            InteractionKind.Dungeon => "APPUYEZ SUR E POUR ENTRER DANS LE DONJON",
            _ => $"APPUYEZ SUR E POUR ENTRER : {interaction.Label.ToUpperInvariant()}",
        };

        DrawPanel(new Vector2(w / 2f - 260f, h - 56f), new Vector2(520f, 30f), new Vector4(0.05f, 0.05f, 0.07f, 0.75f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, prompt, new Vector2(w / 2f, h - 48f), 2.4f, new Vector4(1f, 0.92f, 0.6f, 1f));
    }

    // Rappel des touches en bas à gauche (adapte le libellé à la disposition détectée/choisie —
    // voir GDD, les touches elles-mêmes fonctionnent déjà nativement dans les deux cas).
    var moveKeysLabel = isAzerty ? "ZQSD" : "WASD";
    TextRenderer.Draw(spriteBatch, whiteTexture, $"{moveKeysLabel} : SE DEPLACER - F9 : CLAVIER - F1 : AIDE",
        new Vector2(12, h - 26f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 0.85f));

    if (activeDialogueNpc is null)
    {
        DrawOutdoorHudButtons(w, h);
    }
}

/// <summary>
/// Barre de boutons cliquables en haut à droite pour ouvrir les panneaux (voir retour
/// utilisateur — "il doit y avoir un bouton en plus où on peut cliquer dessus", en complément
/// des raccourcis clavier I/G/B/P/V/M qui restent tous actifs). Un clic sur le panneau déjà
/// ouvert le referme (bascule), comme Échap.
/// </summary>
/// <summary>Libellés des boutons du HUD en haut à droite (voir <see cref="DrawOutdoorHudButtons"/>), factorisé pour que <see cref="OutdoorHudButtonsBounds"/> utilise exactement la même liste.</summary>
(string Label, PanelKind Kind)[] OutdoorHudButtonLabels()
{
    List<(string, PanelKind)> labels =
    [
        ("INVENTAIRE (I)", PanelKind.Inventory),
        ("MONSTRES (M)", PanelKind.Monsters),
        ("GROUPE (P)", PanelKind.Party),
        ("GUILDE (G)", PanelKind.Guild),
        ("ARENE (V)", PanelKind.Arena),
        ("TCHAT (T)", PanelKind.Chat),
        ("AMIS (F)", PanelKind.Friends),
        ("PROFIL (U)", PanelKind.Profile),
        // Voir GDD/demande utilisateur — "un bouton pour le leaderboard en jeu et sur le launcher".
        ("CLASSEMENT (K)", PanelKind.Leaderboard),
        // Voir GDD/demande utilisateur — "un UI pour afficher toutes les quêtes en cours".
        ("QUETES (J)", PanelKind.QuestList),
        // Voir GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de chaque métier".
        ("METIERS (B)", PanelKind.Professions),
        // Voir GDD/demande utilisateur — "un pass de niveaux de joueur".
        ("PASSE (N)", PanelKind.BattlePass),
        // Voir GDD/demande utilisateur — "un boss monde".
        ("BOSS MONDIAL (H)", PanelKind.WorldBoss),
        // Voir GDD/demande utilisateur — "un bouton dans l'UI pour proposer un pvp, on doit écrire
        // son pseudo".
        ("DUEL (Y)", PanelKind.Duel),
        // Voir GDD/demande utilisateur — "ajoute un UI pour les kingdom".
        ("ROYAUME (R)", PanelKind.Kingdom),
    ];

    // Voir GDD/demande utilisateur — "masque le bouton des gems pour tout le monde sauf au
    // fondateur" : aucune passerelle de paiement réel n'est encore branchée, la boutique de
    // gemmes n'a donc rien d'utile à offrir aux joueurs ordinaires pour le moment.
    if (myRank == UserRank.Fondateur)
    {
        labels.Add(("GEMMES", PanelKind.GemShop));
    }

    return [.. labels];
}

/// <summary>
/// Voir GDD/demande utilisateur — "quand on appuie sur les boutons en haut à droite ça nous
/// déplace encore" : la case cliquée était calculée sans jamais vérifier si le clic tombait
/// plutôt sur un bouton du HUD (les deux zones se chevauchent à l'écran) — le clic sur un bouton
/// déclenchait DONC AUSSI un déplacement vers la case du monde sous ce bouton. Calcule le
/// rectangle englobant de la colonne de boutons (même géométrie que <see cref="DrawOutdoorHudButtons"/>)
/// pour que le déplacement au clic puisse l'ignorer.
/// </summary>
bool IsPointOverOutdoorHudButtons(Vector2 point, int w)
{
    const float pixelSize = 1.7f;
    var labels = OutdoorHudButtonLabels().Select(b => b.Label).ToList();
    if (activeStoryQuest is not null)
    {
        labels.Add(questTitle is not null ? "QUETE (Q)" : "QUETE (Q) [MASQUEE]");
    }

    if (myIsAdmin || myRank == UserRank.Fondateur)
    {
        labels.Add("ADMIN (F2)");
    }

    var maxWidth = labels.Count > 0 ? labels.Max(l => TextRenderer.MeasureWidth(l, pixelSize)) : 0f;
    var totalHeight = labels.Count * (TextRenderer.LineHeight(pixelSize) + 10f);
    const float pad = 10f;

    return point.X >= w - 16f - maxWidth - pad && point.X <= w + pad
        && point.Y >= 14f - pad && point.Y <= 14f + totalHeight + pad;
}

void DrawOutdoorHudButtons(int w, int h)
{
    var buttons = OutdoorHudButtonLabels();

    const float pixelSize = 1.7f;
    var y = 14f;

    foreach (var (label, kind) in buttons)
    {
        var width = TextRenderer.MeasureWidth(label, pixelSize);
        var center = new Vector2(w - 16f - width / 2f, y);
        var color = activePanel == kind ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : new Vector4(0.75f, 0.75f, 0.8f, 1f);

        if (DrawClickableCentered(label, center, pixelSize, color))
        {
            if (activePanel == kind)
            {
                activePanel = PanelKind.None;
            }
            else
            {
                OpenPanel(kind);
            }
        }

        y += TextRenderer.LineHeight(pixelSize) + 10f;
    }

    // Voir GDD/demande utilisateur — "il y a une touche pour masquer la quête mais pas la
    // réafficher, ajoute une UI pour l'afficher avec un bouton en haut à droite" : bouton distinct
    // des autres (pas un PanelKind, la quête reste visible par-dessus le monde, pas un panneau
    // modal) mais rendu au même endroit pour rester cohérent.
    if (activeStoryQuest is not null)
    {
        var questLabel = questTitle is not null ? "QUETE (Q)" : "QUETE (Q) [MASQUEE]";
        var questWidth = TextRenderer.MeasureWidth(questLabel, pixelSize);
        var questCenter = new Vector2(w - 16f - questWidth / 2f, y);
        var questColor = questTitle is not null ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : new Vector4(0.75f, 0.75f, 0.8f, 1f);

        if (DrawClickableCentered(questLabel, questCenter, pixelSize, questColor))
        {
            ToggleQuestPanel();
        }

        y += TextRenderer.LineHeight(pixelSize) + 10f;
    }

    // Voir GDD/demande utilisateur — "il n'y a toujours pas de bouton pour le fondateur et les
    // admin en haut à droite" : bouton dédié en plus du raccourci F2 (voir plus haut dans le
    // gestionnaire d'entrée outdoor), réservé aux comptes admin/Fondateur comme le panel lui-même.
    if (myIsAdmin || myRank == UserRank.Fondateur)
    {
        const string adminLabel = "ADMIN (F2)";
        var adminWidth = TextRenderer.MeasureWidth(adminLabel, pixelSize);
        var adminCenter = new Vector2(w - 16f - adminWidth / 2f, y);
        var adminColor = isAdminPanelOpen ? new Vector4(0.95f, 0.5f, 0.45f, 1f) : new Vector4(0.85f, 0.65f, 0.6f, 1f);

        if (DrawClickableCentered(adminLabel, adminCenter, pixelSize, adminColor))
        {
            isAdminPanelOpen = !isAdminPanelOpen;
            adminPanelCursor = 0;
            adminPanelTyping = false;
            adminPanelTextInput = string.Empty;
            adminPanelMessage = null;
        }
    }
}

const int InventoryVisibleRows = 9;

/// <summary>Voir GDD/demande utilisateur — "les items dépassent, ajoute une barre de scroll dans l'inventaire" : HAUT/BAS défilent d'une ligne, ECHAP ferme (et réinitialise le défilement, voir OpenPanel).</summary>
void UpdateInventoryPanel()
{
    var maxOffset = Math.Max(0, inventoryItems.Count - InventoryVisibleRows);
    inventoryScrollOffset = Math.Clamp(inventoryScrollOffset, 0, maxOffset);

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (keyboard.WasJustPressed(Key.Down))
    {
        inventoryScrollOffset = Math.Min(inventoryScrollOffset + 1, maxOffset);
    }
    else if (keyboard.WasJustPressed(Key.Up))
    {
        inventoryScrollOffset = Math.Max(inventoryScrollOffset - 1, 0);
    }
}

void DrawInventoryPanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 360f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.85f, 0.7f, 0.35f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "INVENTAIRE", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    if (inventoryItems.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "INVENTAIRE VIDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        const float rowHeight = 28f;
        const float listTop = 56f;
        var maxOffset = Math.Max(0, inventoryItems.Count - InventoryVisibleRows);
        var offset = Math.Clamp(inventoryScrollOffset, 0, maxOffset);

        var y = topLeft.Y + listTop;
        foreach (var item in inventoryItems.Skip(offset).Take(InventoryVisibleRows))
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{item.Name.ToUpperInvariant()} x{item.Quantity}", new Vector2(topLeft.X + 20f, y), 2f, Vector4.One);
            y += rowHeight;
        }

        // Voir GDD/demande utilisateur — "ajoute une barre de scroll" : piste + curseur proportionnel
        // au nombre d'objets visibles, seulement affichée si tout ne tient pas déjà à l'écran.
        if (inventoryItems.Count > InventoryVisibleRows)
        {
            const float trackWidth = 6f;
            var trackTop = topLeft.Y + listTop - 4f;
            var trackHeight = InventoryVisibleRows * rowHeight;
            var trackX = topLeft.X + boxWidth - 18f;

            DrawPanel(new Vector2(trackX, trackTop), new Vector2(trackWidth, trackHeight), new Vector4(1f, 1f, 1f, 0.12f));

            var thumbHeight = Math.Max(20f, trackHeight * InventoryVisibleRows / inventoryItems.Count);
            var thumbY = trackTop + (trackHeight - thumbHeight) * (maxOffset == 0 ? 0f : offset / (float)maxOffset);
            DrawPanel(new Vector2(trackX, thumbY), new Vector2(trackWidth, thumbHeight), new Vector4(0.9f, 0.75f, 0.35f, 0.9f));
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "HAUT/BAS : DEFILER - ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void DrawGuildPanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 360f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.85f, 0.7f, 0.35f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "GUILDE", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    if (!guildLoaded)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else if (myGuild is null)
    {
        DrawGuildJoinCreateUi(topLeft, boxWidth, boxHeight);
    }
    else
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, myGuild.Name.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + 60f), 2.4f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"NIVEAU {myGuild.Level} - {myGuild.TreasuryGold} OR", new Vector2(w / 2f, topLeft.Y + 88f), 2f, new Vector4(0.8f, 0.8f, 0.85f, 1f));

        var y = topLeft.Y + 122f;
        TextRenderer.Draw(spriteBatch, whiteTexture, "MEMBRES :", new Vector2(topLeft.X + 20f, y), 2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        y += 28f;
        foreach (var name in myGuild.MemberNames)
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, name.ToUpperInvariant(), new Vector2(topLeft.X + 30f, y), 2f, Vector4.One);
            y += 24f;
        }
    }

    if (myGuild is not null || guildMode == GuildPanelMode.None)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
}

/// <summary>Sous-écran du panneau Guilde quand le personnage n'appartient à aucune guilde (voir <see cref="UpdateGuildPanel"/>).</summary>
void DrawGuildJoinCreateUi(Vector2 topLeft, float boxWidth, float boxHeight)
{
    var w = uiCamera.ViewportWidth;

    switch (guildMode)
    {
        case GuildPanelMode.None:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "VOUS N'APPARTENEZ A AUCUNE GUILDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 30f), 2.1f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "C : CREER UNE GUILDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 6f), 2f, new Vector4(0.6f, 0.85f, 0.6f, 1f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "R : RECHERCHER / REJOINDRE UNE GUILDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 34f), 2f, new Vector4(0.6f, 0.75f, 0.9f, 1f));
            break;

        case GuildPanelMode.Create:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "NOM DE LA NOUVELLE GUILDE :", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 40f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
            DrawPanel(new Vector2(topLeft.X + 30f, topLeft.Y + boxHeight / 2f - 10f), new Vector2(boxWidth - 60f, 32f), new Vector4(0.12f, 0.12f, 0.16f, 1f));
            TextRenderer.Draw(spriteBatch, whiteTexture, guildTextInput.ToUpperInvariant(), new Vector2(topLeft.X + 38f, topLeft.Y + boxHeight / 2f - 3f), 1.8f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE POUR VALIDER - ECHAP POUR ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 40f), 1.7f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
            break;

        case GuildPanelMode.Search when !guildSearchDone:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "NOM A RECHERCHER (VIDE = TOUTES) :", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 40f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
            DrawPanel(new Vector2(topLeft.X + 30f, topLeft.Y + boxHeight / 2f - 10f), new Vector2(boxWidth - 60f, 32f), new Vector4(0.12f, 0.12f, 0.16f, 1f));
            TextRenderer.Draw(spriteBatch, whiteTexture, guildTextInput.ToUpperInvariant(), new Vector2(topLeft.X + 38f, topLeft.Y + boxHeight / 2f - 3f), 1.8f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE POUR RECHERCHER - ECHAP POUR ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 40f), 1.7f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
            break;

        case GuildPanelMode.Search:
            if (guildSearchResults.Count == 0)
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE GUILDE TROUVEE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.1f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
            }
            else
            {
                var y = topLeft.Y + 62f;
                for (var i = 0; i < guildSearchResults.Count; i++)
                {
                    var guild = guildSearchResults[i];
                    var isSelected = i == guildSearchCursor;
                    var prefix = isSelected ? "> " : "  ";
                    var color = isSelected ? new Vector4(0.6f, 0.85f, 0.95f, 1f) : Vector4.One;
                    TextRenderer.Draw(spriteBatch, whiteTexture, $"{prefix}{guild.Name.ToUpperInvariant()} (NIV. {guild.Level}, {guild.MemberNames.Count} MEMBRES)", new Vector2(topLeft.X + 24f, y), 1.9f, color);
                    y += 26f;
                }
            }

            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : REJOINDRE - ECHAP : NOUVELLE RECHERCHE", new Vector2(w / 2f, topLeft.Y + boxHeight - 46f), 1.7f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            break;
    }

    if (guildActionMessage is { Length: > 0 })
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, guildActionMessage.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + boxHeight - 66f), 1.7f, new Vector4(0.95f, 0.6f, 0.5f, 1f));
    }

    if (guildMode != GuildPanelMode.None)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR REVENIR", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
}

void DrawMonstersPanel(int w, int h)
{
    const float boxWidth = 520f;
    const float boxHeight = 380f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.4f, 0.75f, 0.5f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "MONSTRES", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.55f, 0.9f, 0.6f, 1f));

    if (!monstersLoaded)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else if (ownedMonsters.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE CREATURE POUR L'INSTANT", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.1f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
    }
    else if (monsterGiveItemMode)
    {
        var monster = ownedMonsters[monsterCursor];
        var monsterLabel = monster.Nickname.Length > 0 ? monster.Nickname : (speciesById.TryGetValue(monster.SpeciesId, out var s) ? s.Name : "Créature");
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"DONNER UN OBJET A {monsterLabel.ToUpperInvariant()}", new Vector2(w / 2f, topLeft.Y + 62f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));

        if (inventoryItems.Count == 0)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "INVENTAIRE VIDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        }
        else
        {
            var y = topLeft.Y + 100f;
            for (var i = 0; i < inventoryItems.Count; i++)
            {
                var isSelected = i == monsterGiveItemCursor;
                var prefix = isSelected ? "> " : "  ";
                var color = isSelected ? new Vector4(0.6f, 0.95f, 0.65f, 1f) : Vector4.One;
                var text = $"{prefix}{inventoryItems[i].Name.ToUpperInvariant()} x{inventoryItems[i].Quantity}";
                if (DrawClickableRow(text, new Vector2(topLeft.X + 30f, y), boxWidth - 60f, 2f, color) && monsterGiveItemTask is null && ownedMonsters.Count > 0)
                {
                    monsterGiveItemCursor = i;
                    monsterMessage = null;
                    monsterGiveItemTask = gameDataApi!.GiveItemToMonsterAsync(options.SessionToken!, ownedMonsters[monsterCursor].Id, inventoryItems[i].ItemId);
                }

                y += 26f;
            }
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : DONNER - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight + 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }
    else if (monsterEquipMode)
    {
        var monster = ownedMonsters[monsterCursor];
        var monsterLabel = monster.Nickname.Length > 0 ? monster.Nickname : (speciesById.TryGetValue(monster.SpeciesId, out var s) ? s.Name : "Créature");
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"EQUIPER {monsterLabel.ToUpperInvariant()}", new Vector2(w / 2f, topLeft.Y + 62f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));

        var equipableItems = inventoryItems.Where(i => i.ItemType is ItemType.Arme or ItemType.Armure or ItemType.Accessoire).ToList();
        if (equipableItems.Count == 0)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE ARME/ARMURE/ACCESSOIRE EN INVENTAIRE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        }
        else
        {
            var y = topLeft.Y + 100f;
            for (var i = 0; i < equipableItems.Count; i++)
            {
                var isSelected = i == monsterEquipCursor;
                var prefix = isSelected ? "> " : "  ";
                var color = isSelected ? new Vector4(0.6f, 0.95f, 0.65f, 1f) : Vector4.One;
                var text = $"{prefix}{equipableItems[i].Name.ToUpperInvariant()} x{equipableItems[i].Quantity} [{equipableItems[i].ItemType.ToString().ToUpperInvariant()}]";
                if (DrawClickableRow(text, new Vector2(topLeft.X + 30f, y), boxWidth - 60f, 1.8f, color) && monsterEquipTask is null)
                {
                    monsterEquipCursor = i;
                    monsterMessage = null;
                    monsterEquipTask = gameDataApi!.EquipItemAsync(options.SessionToken!, monster.Id, equipableItems[i].ItemId);
                }

                y += 26f;
            }
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : EQUIPER - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight + 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }
    else
    {
        var y = topLeft.Y + 70f;
        for (var i = 0; i < ownedMonsters.Count; i++)
        {
            var monster = ownedMonsters[i];
            var isSelected = i == monsterCursor;
            speciesById.TryGetValue(monster.SpeciesId, out var species);
            var name = monster.Nickname.Length > 0 ? monster.Nickname : (species?.Name ?? "Créature");
            var prefix = isSelected ? "> " : "  ";
            var color = isSelected ? new Vector4(0.6f, 0.95f, 0.65f, 1f) : Vector4.One;

            // Portrait (voir GDD/demande utilisateur — "voir à quoi il ressemble") : réutilise le
            // même rendu que la sélection du starter/le combat, coloré selon l'élément de
            // l'espèce, faute de vrais sprites (voir Docs/README.md pour cette limite assumée).
            var portraitColor = species is not null ? ElementColor(species.Element) : new Vector4(0.5f, 0.5f, 0.55f, 1f);
            var portraitCenter = new Vector2(topLeft.X + 44f, y + 8f);
            DrawStarterPortrait(portraitCenter, 22f, portraitColor);

            var textX = topLeft.X + 78f;
            // Voir GDD/demande utilisateur — "chaque monstre a un type affiché (archer, soigneur,
            // etc.)" : déjà déterminant pour les capacités/portées en combat (CombatEngine), donc
            // affiché ici en toutes lettres plutôt que par la seule couleur du portrait.
            var typeLabel = species is not null ? $" [{species.Type}]".ToUpperInvariant() : "";
            // Voir GDD/demande utilisateur — variantes de créature (voir MonsterVariantCatalog) :
            // affichée en toutes lettres, rien pour la variante Normal (immense majorité des
            // créatures) pour ne pas surcharger la liste.
            var variantLabel = monster.Variant == MonsterVariant.Normal ? "" : $" [{MonsterVariantCatalog.Get(monster.Variant).DisplayName}]".ToUpperInvariant();
            // Voir GDD/demande utilisateur — bâtiment pour "déplacer ce que l'on a dans notre team".
            var teamLabel = monster.IsInActiveTeam ? " [EQUIPE]" : "";
            var rowText = $"{prefix}{name.ToUpperInvariant()}{variantLabel}{typeLabel}{teamLabel} - NIV. {monster.Level}";
            if (DrawClickableRow(rowText, new Vector2(textX, y), boxWidth - textX + topLeft.X - 20f, 2f, color))
            {
                monsterCursor = i;
            }

            var xpForNextLevel = monster.Level * 100;
            var xpRatio = Math.Clamp((float)monster.Experience / Math.Max(1, xpForNextLevel), 0f, 1f);
            var barTop = new Vector2(textX, y + 24f);
            DrawPanel(barTop, new Vector2(190f, 6f), new Vector4(0.2f, 0.2f, 0.22f, 1f));
            DrawPanel(barTop, new Vector2(190f * xpRatio, 6f), new Vector4(0.4f, 0.85f, 0.5f, 1f));
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{monster.Experience}/{xpForNextLevel} XP", barTop + new Vector2(200f, -4f), 1.3f, new Vector4(0.7f, 0.7f, 0.75f, 1f));

            // Voir GDD/demande utilisateur — "les items équipés peuvent donner des avantages à nos
            // monstres" : équipement affiché sous la barre d'XP, seulement pour la créature
            // sélectionnée (sinon chaque ligne deviendrait trop chargée).
            if (isSelected)
            {
                var equipParts = new List<string>();
                if (monster.EquippedWeaponName is { } weaponName) equipParts.Add($"Arme: {weaponName}");
                if (monster.EquippedArmorName is { } armorName) equipParts.Add($"Armure: {armorName}");
                if (monster.EquippedAccessoryName is { } accessoryName) equipParts.Add($"Accessoire: {accessoryName}");
                var equipText = equipParts.Count > 0 ? string.Join(" - ", equipParts) : "Aucun équipement";
                TextRenderer.Draw(spriteBatch, whiteTexture, equipText, new Vector2(textX, y + 34f), 1.3f, new Vector4(0.65f, 0.85f, 0.95f, 1f));
            }

            y += isSelected ? 62f : 48f;
        }

        // Voir GDD/demande utilisateur — "pour les indications de touche, fais comme Amis/Profil"
        // : bannière pulsante en bas (DrawPromptBanner) plutôt qu'un texte simple dans la boîte,
        // pour rester cohérent avec les panneaux plus récents.
        var hint = myRank == UserRank.Fondateur
            ? "D:OBJET - E:EQUIPER - R:RETIRER - T:EQUIPE - L(ADMIN):+5 NIV."
            : "D:DONNER OBJET - E:EQUIPER - R:RETIRER EQUIPEMENT - T:EQUIPE";
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, hint, new Vector2(w / 2f, topLeft.Y + boxHeight + 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }

    if (monsterMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, monsterMessage.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + boxHeight - 66f), 1.7f, new Vector4(0.7f, 0.9f, 0.75f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void DrawPartyPanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 340f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.35f, 0.62f, 0.88f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "GROUPE", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.55f, 0.75f, 0.95f, 1f));

    if (partyJoinPromptOpen)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CODE DU GROUPE A REJOINDRE (5 CHIFFRES) :", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 40f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        DrawPanel(new Vector2(topLeft.X + 30f, topLeft.Y + boxHeight / 2f - 10f), new Vector2(boxWidth - 60f, 32f), new Vector4(0.12f, 0.12f, 0.16f, 1f));
        TextRenderer.Draw(spriteBatch, whiteTexture, partyJoinInput.ToUpperInvariant(), new Vector2(topLeft.X + 38f, topLeft.Y + boxHeight / 2f - 3f), 1.8f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE POUR VALIDER - ECHAP POUR ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 40f), 1.7f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else if (!partyLoaded)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else if (myParty is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "VOUS N'ETES DANS AUCUN GROUPE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 30f), 2.1f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : CREER UN GROUPE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 6f), 2f, new Vector4(0.6f, 0.85f, 0.6f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "J : REJOINDRE PAR IDENTIFIANT", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 34f), 2f, new Vector4(0.6f, 0.75f, 0.9f, 1f));
    }
    else
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{myParty.Members.Count}/{PartyMaxMembers} JOUEURS", new Vector2(w / 2f, topLeft.Y + 60f), 2.2f, new Vector4(0.7f, 0.85f, 1f, 1f));

        var y = topLeft.Y + 96f;
        foreach (var member in myParty.Members)
        {
            var isLeader = member.CharacterId == myParty.LeaderCharacterId;
            var label = $"{(isLeader ? "* " : "  ")}{member.Name.ToUpperInvariant()} (NIV. {member.Level})";
            TextRenderer.Draw(spriteBatch, whiteTexture, label, new Vector2(topLeft.X + 30f, y), 2f, isLeader ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : Vector4.One);
            y += 26f;
        }

        y += 14f;
        TextRenderer.Draw(spriteBatch, whiteTexture, $"CODE : {myParty.JoinCode}", new Vector2(topLeft.X + 20f, y), 1.7f, new Vector4(0.7f, 0.7f, 0.75f, 1f));

        // Bouton copier (voir GDD/demande utilisateur — "ajoute un bouton pour les copier") :
        // copie le code à 5 chiffres dans le presse-papiers système, plus simple à communiquer
        // à d'autres joueurs qu'à le recopier à la main.
        var copyLabel = partyCodeCopied ? "COPIE !" : "COPIER";
        var copyColor = partyCodeCopied ? new Vector4(0.5f, 0.9f, 0.5f, 1f) : new Vector4(0.6f, 0.75f, 0.9f, 1f);
        if (DrawClickableCentered(copyLabel, new Vector2(topLeft.X + boxWidth - 60f, y + 6f), 1.6f, copyColor))
        {
            keyboard.SetClipboardText(myParty.JoinCode);
            partyCodeCopied = true;
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "L : QUITTER LE GROUPE", new Vector2(w / 2f, topLeft.Y + boxHeight - 46f), 1.9f, new Vector4(0.9f, 0.55f, 0.5f, 1f));
    }

    if (!partyJoinPromptOpen && partyMessage is { Length: > 0 })
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, partyMessage.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + boxHeight - 66f), 1.8f, new Vector4(0.95f, 0.6f, 0.5f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void DrawArenaPanel(int w, int h)
{
    const float boxWidth = 480f;
    const float boxHeight = 320f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.85f, 0.35f, 0.35f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ARENE CLASSEE", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.95f, 0.55f, 0.5f, 1f));

    if (arenaQueued)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"EN FILE : {ArenaFormatLabel(arenaFormats[arenaFormatCursor])}", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 20f), 2.2f, new Vector4(0.9f, 0.8f, 0.4f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "RECHERCHE D'ADVERSAIRES...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 14f), 1.9f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight - 40f), 1.9f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var y = topLeft.Y + 70f;
        for (var i = 0; i < arenaFormats.Length; i++)
        {
            var isSelected = i == arenaFormatCursor;
            var prefix = isSelected ? "> " : "  ";
            var color = isSelected ? new Vector4(0.95f, 0.6f, 0.5f, 1f) : Vector4.One;
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{prefix}{ArenaFormatLabel(arenaFormats[i])}", new Vector2(w / 2f, y), 2.2f, color);
            y += 32f;
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "FLECHES : CHOISIR - ENTREE : REJOINDRE LA FILE", new Vector2(w / 2f, topLeft.Y + boxHeight - 40f), 1.8f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
    }

    if (arenaMessage is { Length: > 0 })
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, arenaMessage.ToUpperInvariant(), new Vector2(w / 2f, topLeft.Y + boxHeight - 64f), 1.8f, new Vector4(0.95f, 0.6f, 0.5f, 1f));
    }

    if (!arenaQueued)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
}

/// <summary>
/// Voir GDD/demande utilisateur — "afficher les messages du tchat transmis en bas à droite" :
/// notifications éphémères empilées (les plus récentes en bas), disparaissant après quelques
/// secondes (voir chatToastLifetime), visibles quelle que soit la scène/le panneau actif
/// contrairement à <see cref="DrawChatPanel"/> qui n'existe que derrière la touche T.
/// </summary>
/// <summary>
/// Voir GDD/demande utilisateur — "affichage de quête à gauche" (ex. "le forgeron te dit de
/// ramener 3 de fer et 1 bâton pour te faire une épée en fer"). Persistant jusqu'à fermeture
/// (Q) ou nouvelle quête, contrairement aux notifications de tchat éphémères.
/// </summary>
void DrawQuestPanel(int w, int h)
{
    if (questTitle is null)
    {
        return;
    }

    var displayLines = questMessage is not null ? [.. questLines, "", questMessage] : questLines;
    const float panelWidth = 340f;
    var lineHeight = TextRenderer.LineHeight(1.5f);
    var panelHeight = 40f + displayLines.Count * (lineHeight + 4f) + 12f;
    var topLeft = new Vector2(16f, h / 2f - panelHeight / 2f);

    DrawPanel(topLeft, new Vector2(panelWidth, panelHeight), new Vector4(0.08f, 0.08f, 0.12f, 0.9f));
    DrawPanel(topLeft, new Vector2(panelWidth, 3f), new Vector4(0.9f, 0.8f, 0.4f, 1f));

    // Voir GDD/demande utilisateur — "fait en sorte que l'on puisse le faire aussi au clic" :
    // cliquer le titre masque/réaffiche la quête, comme la touche Q.
    if (DrawClickableRow(questTitle, topLeft + new Vector2(12f, 10f), panelWidth - 24f, 1.6f, new Vector4(0.95f, 0.85f, 0.5f, 1f)))
    {
        ToggleQuestPanel();
    }

    var y = topLeft.Y + 40f;
    foreach (var line in displayLines)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, line, topLeft + new Vector2(12f, y - topLeft.Y), 1.5f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        y += lineHeight + 4f;
    }
}

/// <summary>Panel admin en jeu (touche F2, comptes IsAdmin et grade Fondateur) — voir <see cref="UpdateAdminGamePanel"/>.</summary>
void DrawAdminGamePanel(int w, int h)
{
    const float boxWidth = 600f;
    const float boxHeight = 480f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.1f, 0.05f, 0.05f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.9f, 0.35f, 0.3f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "PANEL ADMIN", new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.95f, 0.5f, 0.45f, 1f));

    var commands = AdminPanelCommands();

    if (adminPanelTyping)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, commands[adminPanelCursor], new Vector2(topLeft.X + 20f, topLeft.Y + 60f), 1.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        TextRenderer.Draw(spriteBatch, whiteTexture, adminPanelTextInput + "_", new Vector2(topLeft.X + 20f, topLeft.Y + 100f), 2f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : VALIDER - ECHAP : ANNULER LA SAISIE", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var y = topLeft.Y + 60f;
        for (var i = 0; i < commands.Length; i++)
        {
            var selected = i == adminPanelCursor;
            var color = selected ? new Vector4(0.95f, 0.6f, 0.55f, 1f) : Vector4.One;
            var prefix = selected ? "> " : "  ";
            if (DrawClickableRow(prefix + commands[i], new Vector2(topLeft.X + 20f, y), boxWidth - 40f, 1.6f, color) && adminPanelActionTask is null)
            {
                adminPanelCursor = i;
                if (i == 1)
                {
                    adminPanelMessage = null;
                    adminPanelActionTask = gameDataApi!.ActivateSignModeAsync(options.SessionToken!, 300);
                }
                else
                {
                    adminPanelTyping = true;
                    adminPanelTextInput = string.Empty;
                    adminPanelMessage = null;
                }
            }

            y += 30f;
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "HAUT/BAS : CHOISIR - ENTREE : VALIDER - ECHAP : FERMER (F2)", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }

    if (adminPanelMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, adminPanelMessage, new Vector2(w / 2f, topLeft.Y + boxHeight - 46f), 1.7f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
    }
}

/// <summary>Bannière plein écran (voir GDD/demande utilisateur — "afficher un message en haut de l'écran en gros à tout les joueurs"), visible tant que non expirée (voir adminBannerExpiresAtUtc).</summary>
void DrawAdminBanner(int w, int h)
{
    if (adminBannerMessage is null || DateTime.UtcNow >= adminBannerExpiresAtUtc)
    {
        return;
    }

    DrawPanel(new Vector2(0, 60f), new Vector2(w, 70f), new Vector4(0.15f, 0.05f, 0.05f, 0.85f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, adminBannerMessage, new Vector2(w / 2f, 95f), 2.6f, new Vector4(0.98f, 0.85f, 0.3f, 1f));
}

/// <summary>Voir GDD/demande utilisateur — "propose un pvp, si la personne est en team tout les membres doivent accepter" : popup accepter/refuser, envoyée via le bouton DUEL ou <c>/duel &lt;pseudo&gt;</c> dans le tchat (voir PlayerSession.HandleDuelCommand). Si <paramref name="teamSize"/> &gt; 1, précise que tout le groupe doit accepter pour que le combat démarre.</summary>
void DrawDuelInvitePopup(int w, int h, string challengerName, int teamSize)
{
    const float boxWidth = 460f;
    const float boxHeight = 150f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h * 0.3f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.1f, 0.05f, 0.12f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.9f, 0.4f, 0.85f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{challengerName} VOUS DEFIE EN DUEL !", new Vector2(w / 2f, topLeft.Y + 34f), 2f, new Vector4(0.95f, 0.7f, 0.9f, 1f));
    if (teamSize > 1)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"Votre groupe entier ({teamSize} joueurs) doit accepter.", new Vector2(w / 2f, topLeft.Y + 66f), 1.6f, new Vector4(0.85f, 0.75f, 0.9f, 1f));
    }

    DrawPromptBanner("ENTREE : ACCEPTER - ECHAP : REFUSER", new Vector2(w / 2f, topLeft.Y + boxHeight - 30f));
}

/// <summary>
/// Voir GDD/demande utilisateur — "shop avec des gems" : conversion de pièces, palier de grade
/// (bonus XP/or cosmétique) et pass d'emplacement de personnage, tous payés en gemmes. L'achat de
/// gemmes contre argent réel est affiché mais désactivé (voir GDD, "bloque la page pour le
/// moment") — aucune passerelle de paiement n'est branchée pour l'instant.
/// </summary>
void UpdateGemShopPanel()
{
    if (premiumLoadTask is { IsCompleted: true } loadTask)
    {
        premiumStatus = loadTask.IsFaulted ? null : loadTask.Result;
        premiumLoadTask = null;
    }

    if (premiumActionTask is { IsCompleted: true } actionTask)
    {
        premiumMessage = actionTask.IsFaulted ? "Connexion au serveur impossible." : actionTask.Result.Message;
        premiumActionTask = null;
        if (chosenCharacterId is not null && options.SessionToken is not null)
        {
            premiumLoadTask = gameDataApi?.GetPremiumStatusAsync(options.SessionToken);
        }

        return;
    }

    if (premiumActionTask is not null || premiumLoadTask is not null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        return;
    }

    if (chosenCharacterId is null || options.SessionToken is null || gameDataApi is null || premiumStatus is null)
    {
        return;
    }

    if (keyboard.WasJustPressed(Key.Enter))
    {
        premiumMessage = null;
        premiumActionTask = gameDataApi.ExchangeGoldForGemsAsync(options.SessionToken, chosenCharacterId.Value, premiumStatus.GoldPerGemBlock);
    }
    else if (keyboard.WasJustPressed(Key.G) && premiumStatus.NextGradeTierCostGems is not null)
    {
        premiumMessage = null;
        premiumActionTask = gameDataApi.UpgradePremiumGradeAsync(options.SessionToken, chosenCharacterId.Value);
    }
}

void DrawGemShopPanel(int w, int h)
{
    const float boxWidth = 520f;
    const float boxHeight = 320f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.08f, 0.06f, 0.1f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.7f, 0.5f, 0.95f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "BOUTIQUE GEMMES", new Vector2(w / 2f, topLeft.Y + 24f), 2.4f, new Vector4(0.85f, 0.75f, 0.98f, 1f));

    if (premiumStatus is null)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, "Chargement...", new Vector2(topLeft.X + 20f, topLeft.Y + 70f), 1.7f, Vector4.One);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    var y = topLeft.Y + 62f;
    TextRenderer.Draw(spriteBatch, whiteTexture, $"Gemmes : {premiumStatus.Gems}", new Vector2(topLeft.X + 20f, y), 2f, new Vector4(0.85f, 0.75f, 0.98f, 1f));
    y += 40f;

    TextRenderer.Draw(spriteBatch, whiteTexture,
        $"[ENTREE] Convertir {premiumStatus.GoldPerGemBlock:N0} pieces -> {premiumStatus.GemsPerGemBlock} gemmes",
        new Vector2(topLeft.X + 20f, y), 1.6f, new Vector4(0.9f, 0.85f, 0.6f, 1f));
    y += 34f;

    TextRenderer.Draw(spriteBatch, whiteTexture,
        $"Grade actuel : {premiumStatus.GradeName} (+{premiumStatus.GradeBonusPercent:0.0}% xp/or, max {premiumStatus.MaxCharacters} personnages)",
        new Vector2(topLeft.X + 20f, y), 1.6f, new Vector4(0.7f, 0.9f, 0.75f, 1f));
    y += 30f;

    var gradeLine = premiumStatus.NextGradeTierCostGems is { } gradeCost
        ? $"[G] Passer {premiumStatus.NextGradeTierName} : {gradeCost} gemmes"
        : "Grade au palier maximum (Légende).";
    TextRenderer.Draw(spriteBatch, whiteTexture, gradeLine, new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.9f, 0.75f, 0.98f, 1f));
    y += 44f;

    TextRenderer.Draw(spriteBatch, whiteTexture, "Acheter des gemmes avec de l'argent reel :", new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.55f, 0.55f, 0.6f, 1f));
    y += 26f;
    TextRenderer.Draw(spriteBatch, whiteTexture, "BIENTOT DISPONIBLE", new Vector2(topLeft.X + 20f, y), 1.7f, new Vector4(0.55f, 0.55f, 0.6f, 1f));

    if (premiumMessage is { } message)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, message, new Vector2(topLeft.X + 20f, topLeft.Y + boxHeight - 46f), 1.4f, new Vector4(0.95f, 0.9f, 0.5f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>
/// Voir GDD/demande utilisateur — "un bouton dans l'UI pour proposer un pvp, on doit écrire son
/// pseudo" : simple saisie de pseudo, envoyée comme <c>/duel &lt;pseudo&gt;</c> (voir
/// PlayerSession.HandleDuelCommand) — réutilise toute la logique serveur déjà en place (groupe,
/// invitation, expiration) plutôt que dupliquer un protocole dédié pour ce bouton.
/// </summary>
void UpdateDuelPanel()
{
    foreach (var typed in keyboard.DrainTypedChars())
    {
        if (duelTextInput.Length < 24 && !char.IsControl(typed))
        {
            duelTextInput += typed;
        }
    }

    if (keyboard.WasJustPressed(Key.Backspace) && duelTextInput.Length > 0)
    {
        duelTextInput = duelTextInput[..^1];
    }
    else if (keyboard.WasJustPressed(Key.Escape))
    {
        activePanel = PanelKind.None;
        duelTextInput = string.Empty;
    }
    else if (keyboard.WasJustPressed(Key.Enter) && duelTextInput.Trim().Length > 0)
    {
        connection?.SendChatMessage($"/duel {duelTextInput.Trim()}", ChatChannel.Global);
        duelTextInput = string.Empty;
        activePanel = PanelKind.None;
    }
}

void DrawDuelPanel(int w, int h)
{
    const float boxWidth = 460f;
    const float boxHeight = 210f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.08f, 0.1f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.9f, 0.4f, 0.85f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "DEFIER EN DUEL", new Vector2(w / 2f, topLeft.Y + 24f), 2.4f, new Vector4(0.95f, 0.7f, 0.9f, 1f));
    TextRenderer.Draw(spriteBatch, whiteTexture, "Pseudo du joueur a defier :", new Vector2(topLeft.X + 20f, topLeft.Y + 70f), 1.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
    TextRenderer.Draw(spriteBatch, whiteTexture, duelTextInput + "_", new Vector2(topLeft.X + 20f, topLeft.Y + 100f), 1.9f, Vector4.One);
    TextRenderer.Draw(spriteBatch, whiteTexture, "Si son groupe (ou le votre) compte plusieurs joueurs,", new Vector2(topLeft.X + 20f, topLeft.Y + 132f), 1.3f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    TextRenderer.Draw(spriteBatch, whiteTexture, "tous ses membres devront accepter pour lancer le combat.", new Vector2(topLeft.X + 20f, topLeft.Y + 148f), 1.3f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : DEFIER - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void DrawChatToasts(int w, int h)
{
    List<(ChatLine Line, DateTime ExpiresAtUtc)> toasts;
    lock (stateLock)
    {
        var now = DateTime.UtcNow;
        chatToasts.RemoveAll(t => t.ExpiresAtUtc <= now);
        toasts = [.. chatToasts];
    }

    if (toasts.Count == 0)
    {
        return;
    }

    const float pad = 10f;
    var y = h - pad;
    for (var i = toasts.Count - 1; i >= 0; i--)
    {
        var (line, _) = toasts[i];
        var text = $"{ChatRankTag(line.Rank)}{line.SenderName} : {line.Message}";
        var width = TextRenderer.MeasureWidth(text, 1.5f);
        var height = TextRenderer.LineHeight(1.5f);
        var topLeft = new Vector2(w - pad - width - 12f, y - height);
        DrawPanel(topLeft, new Vector2(width + 12f, height + 6f), new Vector4(0.08f, 0.08f, 0.12f, 0.85f));
        TextRenderer.Draw(spriteBatch, whiteTexture, text, topLeft + new Vector2(6f, 3f), 1.5f, ChatRankColor(line.Rank));
        y -= height + 8f;
    }
}

/// <summary>Notifications génériques en haut de l'écran (voir <see cref="PushSystemToast"/>) — utilisées pour les montées de niveau de métier.</summary>
void DrawSystemToasts(int w, int h)
{
    List<(string Text, Vector4 Color, DateTime ExpiresAtUtc)> toasts;
    lock (stateLock)
    {
        var now = DateTime.UtcNow;
        systemToasts.RemoveAll(t => t.ExpiresAtUtc <= now);
        toasts = [.. systemToasts];
    }

    if (toasts.Count == 0)
    {
        return;
    }

    var y = 90f;
    foreach (var (text, color, _) in toasts)
    {
        var width = TextRenderer.MeasureWidth(text, 2f);
        var topLeft = new Vector2(w / 2f - width / 2f - 14f, y);
        DrawPanel(topLeft, new Vector2(width + 28f, 34f), new Vector4(0.08f, 0.08f, 0.12f, 0.9f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, text, new Vector2(w / 2f, y + 17f), 2f, color);
        y += 42f;
    }
}

/// <summary>
/// Panneau Tchat (touche T, voir GDD/demande utilisateur — "un tchat global, un tchat de guilde,
/// une liste des joueurs en ligne avec leur grade") : deux onglets cliquables (aussi
/// basculables avec Tab, voir <see cref="UpdateChatPanel"/>) partageant le même historique en
/// mémoire filtré par canal, plus la liste des joueurs actuellement connectés
/// (<see cref="remotePlayers"/>, déjà utilisé pour la visibilité globale sur la carte) avec leur
/// grade affiché en préfixe.
/// </summary>
void DrawChatPanel(int w, int h)
{
    const float boxWidth = 640f;
    const float boxHeight = 420f;
    const float listWidth = 190f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);
    var chatWidth = boxWidth - listWidth - 20f;

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.5f, 0.8f, 0.6f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "TCHAT", new Vector2(topLeft.X + chatWidth / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.6f, 0.9f, 0.7f, 1f));

    var globalColor = chatChannel == ChatChannel.Global ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : new Vector4(0.6f, 0.6f, 0.65f, 1f);
    var guildColor = chatChannel == ChatChannel.Guild ? new Vector4(0.95f, 0.8f, 0.4f, 1f) : new Vector4(0.6f, 0.6f, 0.65f, 1f);

    if (DrawClickableCentered("GLOBAL", new Vector2(topLeft.X + chatWidth / 2f - 60f, topLeft.Y + 54f), 1.9f, globalColor))
    {
        chatChannel = ChatChannel.Global;
    }

    if (DrawClickableCentered("GUILDE", new Vector2(topLeft.X + chatWidth / 2f + 60f, topLeft.Y + 54f), 1.9f, guildColor))
    {
        chatChannel = ChatChannel.Guild;
    }

    // Voir GDD/demande utilisateur — "discussion privée" avec un ami (voir DrawFriendsPanel).
    if (chatWhisperTarget is { } whisperTarget)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"MESSAGE PRIVE A {whisperTarget.ToUpperInvariant()} (TAB POUR ANNULER)",
            new Vector2(topLeft.X + chatWidth / 2f, topLeft.Y + 68f), 1.5f, new Vector4(0.9f, 0.6f, 0.95f, 1f));
    }

    var messagesTop = topLeft.Y + 80f;
    var messagesBottom = topLeft.Y + boxHeight - 60f;
    List<ChatLine> visible;
    lock (stateLock)
    {
        visible = chatMessages.Where(m => m.Channel == chatChannel).TakeLast(12).ToList();
    }

    // Voir GDD/demande utilisateur — "dans le tchat ça dépasse de l'UI quand un message est trop
    // long" : les messages sont désormais renvoyés à la ligne (voir WrapTextToLines) au lieu de
    // déborder du panneau — le pseudo/tag reste sur sa propre ligne (cliquable pour un créateur,
    // voir CreatorCredits), le message est réparti sur autant de lignes que nécessaire en dessous.
    // Dessiné du bas vers le haut (messages récents en bas) : pour chaque message, ses lignes de
    // texte d'abord (de la dernière à la première), puis sa ligne d'expéditeur juste au-dessus.
    const float chatLineHeight = 20f;
    var chatWrapWidth = chatWidth - 24f;
    var y = messagesBottom - chatLineHeight;
    for (var i = visible.Count - 1; i >= 0; i--)
    {
        var line = visible[i];
        var senderTag = $"{ChatRankTag(line.Rank)}{GradeBadgeTag(line.Rank, line.SenderGradeTier)}{line.SenderName}";
        var senderColor = line.Rank == UserRank.Joueur && line.SenderGradeTier > 0
            ? GradeBadgeColor(line.SenderGradeTier, animationClock)
            : ChatRankColor(line.Rank);
        var messageLines = WrapTextToLines(line.Message, chatWrapWidth, 1.6f);

        for (var lineIndex = messageLines.Count - 1; lineIndex >= 0; lineIndex--)
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, messageLines[lineIndex], new Vector2(topLeft.X + 32f, y), 1.6f, senderColor);
            y -= chatLineHeight;
        }

        if (CreatorCredits.Find(line.SenderName) is not null)
        {
            var senderWidth = TextRenderer.MeasureWidth(senderTag, 1.6f);
            if (DrawClickableRow(senderTag, new Vector2(topLeft.X + 20f, y), senderWidth, 1.6f, senderColor))
            {
                creatorCardTarget = line.SenderName;
            }
        }
        else
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, senderTag, new Vector2(topLeft.X + 20f, y), 1.6f, senderColor);
        }

        y -= chatLineHeight;
        if (y < messagesTop)
        {
            break;
        }
    }

    DrawPanel(new Vector2(topLeft.X + 16f, messagesBottom + 4f), new Vector2(chatWidth - 16f, 30f), new Vector4(0.12f, 0.12f, 0.16f, 1f));
    TextRenderer.Draw(spriteBatch, whiteTexture, chatTextInput + "_", new Vector2(topLeft.X + 24f, messagesBottom + 11f), 1.7f, Vector4.One);

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "TAB : CANAL - ENTREE : ENVOYER - ECHAP : FERMER",
        new Vector2(topLeft.X + chatWidth / 2f, topLeft.Y + boxHeight - 18f), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));

    var listLeft = topLeft.X + chatWidth + 20f;
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "EN LIGNE", new Vector2(listLeft + (listWidth - 10f) / 2f, topLeft.Y + 62f), 1.8f, new Vector4(0.85f, 0.85f, 0.9f, 1f));

    List<KeyValuePair<Guid, RemotePlayer>> others;
    lock (stateLock)
    {
        others = remotePlayers.ToList();
    }

    var listY = topLeft.Y + 90f;
    TextRenderer.Draw(spriteBatch, whiteTexture, $"{ChatRankTag(myRank)}Vous", new Vector2(listLeft, listY), 1.5f, ChatRankColor(myRank));
    listY += 20f;

    foreach (var (_, remote) in others.OrderBy(kv => kv.Value.Name))
    {
        if (listY > topLeft.Y + boxHeight - 30f)
        {
            break;
        }

        var remoteTag = $"{ChatRankTag(remote.Rank)}{remote.Name}";
        if (CreatorCredits.Find(remote.Name) is not null)
        {
            if (DrawClickableRow(remoteTag, new Vector2(listLeft, listY), listWidth - 10f, 1.5f, ChatRankColor(remote.Rank)))
            {
                creatorCardTarget = remote.Name;
            }
        }
        else
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, remoteTag, new Vector2(listLeft, listY), 1.5f, ChatRankColor(remote.Rank));
        }

        listY += 20f;
    }

    if (creatorCardTarget is { } targetName && CreatorCredits.Find(targetName) is { } profile)
    {
        DrawCreatorCardPopup(w, h, profile);
    }
}

/// <summary>Voir GDD/demande utilisateur — "quand on clique sur un pseudo on a ces informations (feelsman | Discord : ... twitch : ... youtube : ...)".</summary>
void DrawCreatorCardPopup(int w, int h, CreatorProfile profile)
{
    const float boxWidth = 440f;
    const float boxHeight = 200f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.08f, 0.06f, 0.1f, 0.97f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.95f, 0.7f, 0.35f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, profile.DisplayName, new Vector2(w / 2f, topLeft.Y + 28f), 2.4f, new Vector4(0.95f, 0.85f, 0.6f, 1f));

    var y = topLeft.Y + 70f;
    if (profile.Discord is { } discord)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, $"Discord : {discord}", new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.6f, 0.65f, 0.95f, 1f));
        y += 26f;
    }

    if (profile.Twitch is { } twitch)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, $"Twitch : {twitch}", new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.7f, 0.55f, 0.95f, 1f));
        y += 26f;
    }

    if (profile.YouTube is { } youtube)
    {
        TextRenderer.Draw(spriteBatch, whiteTexture, $"YouTube : {youtube}", new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.9f, 0.5f, 0.5f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP : FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>Préfixe affiché devant le pseudo (voir GDD/demande utilisateur — "il est affiché [FONDATEUR] pseudo") : rien pour le grade de base.</summary>
static string ChatRankTag(UserRank rank) => rank switch
{
    UserRank.VIP => "[VIP] ",
    UserRank.Ami => "[AMI] ",
    UserRank.Testeur => "[TESTEUR] ",
    UserRank.Moderateur => "[MODERATEUR] ",
    UserRank.Fondateur => "[FONDATEUR] ",
    _ => "",
};

/// <summary>Couleur associée au grade (voir GDD/demande utilisateur — "avec une couleur"), utilisée pour le préfixe ET le pseudo dans le tchat/la liste des joueurs en ligne.</summary>
static Vector4 ChatRankColor(UserRank rank) => rank switch
{
    UserRank.VIP => new Vector4(0.95f, 0.8f, 0.35f, 1f),
    UserRank.Ami => new Vector4(0.5f, 0.85f, 0.55f, 1f),
    UserRank.Testeur => new Vector4(0.4f, 0.75f, 0.9f, 1f),
    UserRank.Moderateur => new Vector4(0.55f, 0.6f, 0.95f, 1f),
    UserRank.Fondateur => new Vector4(0.95f, 0.4f, 0.35f, 1f),
    _ => Vector4.One,
};

/// <summary>Voir GDD/demande utilisateur — "les grades coûtent [...] badge/couleur de pseudo" : badge de grade payant, affiché après le grade de modération (voir ChatRankTag) — masqué pour le Fondateur (déjà au palier maximum, redondant avec son propre grade).</summary>
static string GradeBadgeTag(UserRank rank, int gradeTier) => rank == UserRank.Fondateur ? "" : gradeTier switch
{
    1 => "[AVENTURIER] ",
    2 => "[HEROS] ",
    3 => "[LEGENDE] ",
    _ => "",
};

/// <summary>Couleur du badge de grade — palier 3 ("Légende") a un effet de couleur changeante plutôt qu'une teinte fixe, seule approximation raisonnable d'un "effet spécial" avec un simple rendu de texte.</summary>
static Vector4 GradeBadgeColor(int gradeTier, float clock) => gradeTier switch
{
    1 => new Vector4(0.4f, 0.85f, 0.5f, 1f),
    2 => new Vector4(0.95f, 0.6f, 0.2f, 1f),
    3 => Vector4.Lerp(new Vector4(0.95f, 0.8f, 0.35f, 1f), new Vector4(0.75f, 0.4f, 0.95f, 1f), 0.5f + 0.5f * MathF.Sin(clock * 1.5f)),
    _ => Vector4.One,
};

static string ArenaFormatLabel(ArenaFormat format) => format switch
{
    ArenaFormat.OneVOne => "1V1 (4 CREATURES)",
    ArenaFormat.TwoVTwo => "2V2 (2 CREATURES/JOUEUR)",
    ArenaFormat.ThreeVThree => "3V3 (ASYMETRIQUE : 2/1/1)",
    ArenaFormat.FourVFour => "4V4 (1 CREATURE/JOUEUR)",
    _ => format.ToString().ToUpperInvariant(),
};

void DrawShopPanel(int w, int h)
{
    const float boxWidth = 520f;
    const float boxHeight = 400f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.85f, 0.7f, 0.35f, 1f));

    var modeLabel = shopSellMode ? "BOUTIQUE - VENTE" : "BOUTIQUE - ACHAT";
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, modeLabel, new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    // Voir GDD/demande utilisateur — "un UI pour l'achat/vente d'objet mais tu gagnes un peu
    // moins que si tu les mets à l'HDV" : Tab bascule entre les deux modes.
    if (shopSellMode)
    {
        if (inventoryItems.Count == 0)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "INVENTAIRE VIDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        }
        else
        {
            var y = topLeft.Y + 56f;
            for (var i = 0; i < inventoryItems.Count; i++)
            {
                var entry = inventoryItems[i];
                var selected = i == shopSellCursor;
                var color = selected ? new Vector4(0.9f, 0.75f, 0.35f, 1f) : Vector4.One;
                var prefix = selected ? "> " : "  ";
                var catalogEntry = shopCatalog.FirstOrDefault(c => c.ItemId == entry.ItemId);
                var sellPrice = catalogEntry is not null ? $"{(int)(catalogEntry.Price * 0.4)} OR" : "?";
                var text = $"{prefix}{entry.Name.ToUpperInvariant()} x{entry.Quantity} - {sellPrice}/u";
                // Voir GDD/demande utilisateur — "tout puisse se faire au clic" : un clic sélectionne
                // ET valide directement, comme Entrée, plutôt que d'exiger deux étapes séparées.
                if (DrawClickableRow(text, new Vector2(topLeft.X + 20f, y), boxWidth - 40f, 2f, color) && shopBuyTask is null)
                {
                    shopSellCursor = i;
                    shopMessage = null;
                    shopBuyTask = gameDataApi!.SellItemAsync(options.SessionToken!, chosenCharacterId!.Value, entry.ItemId, 1);
                }

                y += 28f;
            }
        }
    }
    else if (shopCatalog.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var y = topLeft.Y + 56f;
        for (var i = 0; i < shopCatalog.Count; i++)
        {
            var item = shopCatalog[i];
            var selected = i == shopCursor;
            var color = selected ? new Vector4(0.9f, 0.75f, 0.35f, 1f) : Vector4.One;
            var prefix = selected ? "> " : "  ";
            var text = $"{prefix}{item.Name.ToUpperInvariant()} - {item.Price} OR";
            if (DrawClickableRow(text, new Vector2(topLeft.X + 20f, y), boxWidth - 40f, 2f, color) && shopBuyTask is null)
            {
                shopCursor = i;
                shopMessage = null;
                shopBuyTask = gameDataApi!.BuyItemAsync(options.SessionToken!, chosenCharacterId!.Value, item.ItemId);
            }

            y += 28f;
        }
    }

    if (shopMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, shopMessage, new Vector2(w / 2f, topLeft.Y + boxHeight - 50f), 1.8f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CLIC OU ENTREE : VALIDER - TAB : ACHAT/VENTE - ECHAP : FERMER",
        new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

/// <summary>Hôtel des ventes entre joueurs (voir GDD/demande utilisateur — panneau ouvert directement en entrant dans le bâtiment).</summary>
void DrawAuctionPanel(int w, int h)
{
    const float boxWidth = 560f;
    const float boxHeight = 420f;
    var topLeft = new Vector2(w / 2f - boxWidth / 2f, h / 2f - boxHeight / 2f);

    DrawPanel(topLeft, new Vector2(boxWidth, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.95f));
    DrawPanel(topLeft, new Vector2(boxWidth, 4f), new Vector4(0.4f, 0.55f, 0.68f, 1f));

    var modeLabel = auctionSellMode ? "HOTEL DES VENTES - DEPOSER" : "HOTEL DES VENTES";
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, modeLabel, new Vector2(w / 2f, topLeft.Y + 24f), 2.6f, new Vector4(0.6f, 0.8f, 0.95f, 1f));

    if (auctionSellMode)
    {
        if (inventoryItems.Count == 0)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "INVENTAIRE VIDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        }
        else
        {
            var y = topLeft.Y + 56f;
            for (var i = 0; i < inventoryItems.Count; i++)
            {
                var entry = inventoryItems[i];
                var selected = i == auctionSellCursor;
                var color = selected ? new Vector4(0.6f, 0.85f, 0.95f, 1f) : Vector4.One;
                var prefix = selected ? "> " : "  ";
                var text = $"{prefix}{entry.Name.ToUpperInvariant()} x{entry.Quantity}";
                // Clic = sélection seule ici (le prix se règle ensuite avec Gauche/Droite avant Entrée).
                if (DrawClickableRow(text, new Vector2(topLeft.X + 20f, y), boxWidth - 40f, 2f, color))
                {
                    auctionSellCursor = i;
                }

                y += 28f;
            }

            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"PRIX PAR UNITE : {auctionSellPrice} OR (GAUCHE/DROITE POUR AJUSTER)",
                new Vector2(w / 2f, topLeft.Y + boxHeight - 76f), 1.7f, new Vector4(0.9f, 0.8f, 0.4f, 1f));
        }
    }
    else if (auctionListings.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE ANNONCE POUR L'INSTANT", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f), 2.1f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        var y = topLeft.Y + 56f;
        for (var i = 0; i < auctionListings.Count; i++)
        {
            var listing = auctionListings[i];
            var selected = i == auctionCursor;
            var color = selected ? new Vector4(0.6f, 0.85f, 0.95f, 1f) : Vector4.One;
            var prefix = selected ? "> " : "  ";
            var suffix = listing.IsMine ? " (VOTRE ANNONCE)" : $" - {listing.SellerName}";
            var text = $"{prefix}{listing.ItemName.ToUpperInvariant()} x{listing.Quantity} - {listing.PricePerUnit} OR/u{suffix}";
            if (DrawClickableRow(text, new Vector2(topLeft.X + 20f, y), boxWidth - 40f, 1.9f, color) && auctionActionTask is null)
            {
                auctionCursor = i;
                auctionMessage = null;
                auctionActionTask = listing.IsMine
                    ? gameDataApi!.CancelAuctionListingAsync(options.SessionToken!, chosenCharacterId!.Value, listing.ListingId)
                    : gameDataApi!.BuyAuctionListingAsync(options.SessionToken!, chosenCharacterId!.Value, listing.ListingId);
            }

            y += 28f;
        }
    }

    if (auctionMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, auctionMessage, new Vector2(w / 2f, topLeft.Y + boxHeight - 50f), 1.8f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
    }

    var footer = auctionSellMode
        ? "TAB : PARCOURIR - HAUT/BAS : OBJET - ENTREE : DEPOSER TOUT LE STOCK - ECHAP : FERMER"
        : "TAB : DEPOSER UN OBJET - ENTREE : ACHETER (OU ANNULER SI C'EST LA VOTRE) - ECHAP : FERMER";
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, footer, new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.4f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void DrawDialogueBox(int w, int h)
{
    const float boxHeight = 130f;
    var boxTop = h - boxHeight - 24f;

    DrawPanel(new Vector2(40, boxTop), new Vector2(w - 80, boxHeight), new Vector4(0.06f, 0.06f, 0.09f, 0.92f));
    DrawPanel(new Vector2(40, boxTop), new Vector2(w - 80, 4f), new Vector4(0.85f, 0.7f, 0.35f, 1f));

    TextRenderer.Draw(spriteBatch, whiteTexture, activeDialogueNpc!.Name.ToUpperInvariant(),
        new Vector2(60, boxTop + 14f), 3.2f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    var lines = NpcDialogues.Lines.GetValueOrDefault(activeDialogueNpc.Name, ["..."]);
    var line = lines[Math.Clamp(dialogueLineIndex, 0, lines.Length - 1)];
    TextRenderer.Draw(spriteBatch, whiteTexture, line, new Vector2(60, boxTop + 48f), 2.6f, Vector4.One);

    var footer = dialogueLineIndex < lines.Length - 1 ? "APPUYEZ SUR E POUR CONTINUER" : "APPUYEZ SUR E POUR FERMER";
    TextRenderer.Draw(spriteBatch, whiteTexture, footer, new Vector2(60, boxTop + boxHeight - 22f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

void DrawInteriorScene()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.05f, 0.05f, 0.07f, 1f));
    DrawPanel(Vector2.Zero, new Vector2(w, h * 0.55f), Vector4.Lerp(new Vector4(0.05f, 0.05f, 0.07f, 1f), interiorAccent, 0.22f));
    DrawPanel(new Vector2(0, h * 0.55f), new Vector2(w, h * 0.45f), Vector4.Lerp(new Vector4(0.05f, 0.05f, 0.07f, 1f), interiorAccent, 0.38f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, interiorTitle.ToUpperInvariant(), new Vector2(w / 2f, h * 0.16f), 5f, Vector4.One);

    if (interiorIsDungeon && worldMap.DungeonId >= 0 && gameDataApi is not null)
    {
        DrawDungeonCorridor(w, h);
        return;
    }

    var lineY = h * 0.34f;
    foreach (var line in interiorBodyLines)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, line, new Vector2(w / 2f, lineY), 2.6f, new Vector4(0.92f, 0.92f, 0.95f, 1f));
        lineY += TextRenderer.LineHeight(2.6f) + 6f;
    }

    // Meubles (voir GDD — intérieurs enrichis) : rectangles positionnés en repère écran relatif
    // (voir BuildingInteriors), dessinés avant le PNJ pour rester visuellement "derrière" lui.
    foreach (var item in interiorFurniture)
    {
        var topLeft = new Vector2(item.RelativeX * w, item.RelativeY * h);
        var size = new Vector2(item.RelativeWidth * w, item.RelativeHeight * h);
        DrawPanel(topLeft, size, item.Color);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, item.Label.ToUpperInvariant(), topLeft + new Vector2(size.X / 2f, -14f), 1.4f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }

    if (activeDialogueNpc is not null)
    {
        DrawDialogueBox(w, h);
    }
    else
    {
        if (interiorNpcs.Count > 0)
        {
            var npc = interiorNpcs[0];
            var npcCenter = new Vector2(w * 0.5f, h * 0.72f);
            DrawStarterPortrait(npcCenter, 46f, npc.BodyColor);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, npc.Name.ToUpperInvariant(), npcCenter + new Vector2(0, 60f), 1.8f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "APPUYEZ SUR E POUR PARLER", npcCenter + new Vector2(0, 84f), 1.7f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        }

        if (interiorIsDungeon)
        {
            var prompt = combatStartTask is not null ? "..." : "APPUYEZ SUR ENTREE POUR AFFRONTER UN MONSTRE SAUVAGE";
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, prompt, new Vector2(w / 2f, h * 0.80f), 2.2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));

            if (combatMessage is not null)
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatMessage, new Vector2(w / 2f, h * 0.85f), 2f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
            }
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "APPUYEZ SUR ECHAP POUR SORTIR", new Vector2(w / 2f, h * 0.90f), 2.6f, new Vector4(0.85f, 0.80f, 0.5f, 1f));
    }
}

/// <summary>
/// Voir GDD/demande utilisateur — donjon façon Binding of Isaac : la salle courante rendue comme
/// une pièce (murs avec ouvertures aux portes, voir <see cref="DungeonRoom"/>), le joueur déplacé
/// dedans (voir <see cref="MoveWithinDungeonRoom"/>) plutôt qu'une rangée de cases abstraites.
/// </summary>
/// <summary>Géométrie écran de la salle de donjon courante — factorisé pour que <see cref="DrawDungeonCorridor"/> (rendu) et le déplacement au clic (voir <see cref="UpdateDungeonCorridor"/>) utilisent exactement le même rectangle.</summary>
(Vector2 TopLeft, Vector2 Size) DungeonRoomScreenRect(int w, int h) =>
    (new Vector2(w * 0.22f, h * 0.32f), new Vector2(w * 0.56f, h * 0.5f));

void DrawDungeonCorridor(int w, int h)
{
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"ETAGE {dungeonFloorNumber}", new Vector2(w / 2f, h * 0.20f), 2.4f, new Vector4(0.85f, 0.7f, 0.95f, 1f));

    if (dungeonFloorTask is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT...", new Vector2(w / 2f, h * 0.5f), 2.4f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    if (dungeonFloor is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, dungeonRoomMessage ?? "ETAGE INDISPONIBLE", new Vector2(w / 2f, h * 0.5f), 2.2f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "APPUYEZ SUR ECHAP POUR SORTIR", new Vector2(w / 2f, h * 0.90f), 2.6f, new Vector4(0.85f, 0.80f, 0.5f, 1f));
        return;
    }

    var room = dungeonFloor.Rooms.First(r => r.Index == dungeonRoomIndex);
    var isCleared = dungeonClearedRooms.Contains(dungeonRoomIndex);
    var allCleared = dungeonClearedRooms.Count >= dungeonFloor.Rooms.Count;

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"SALLES NETTOYEES : {dungeonClearedRooms.Count}/{dungeonFloor.Rooms.Count}",
        new Vector2(w / 2f, h * 0.26f), 1.5f, new Vector4(0.7f, 0.7f, 0.75f, 1f));

    // La pièce elle-même : un rectangle avec des ouvertures (portes) là où une salle voisine
    // existe sur la grille (voir DungeonRoom.North/South/East/West).
    const float doorGap = 0.16f;
    var (roomTopLeft, roomSize) = DungeonRoomScreenRect(w, h);
    var wallColor = isCleared ? new Vector4(0.35f, 0.35f, 0.4f, 1f) : DungeonRoomColor(room.EncounterType);
    const float wallThickness = 10f;

    DrawPanel(roomTopLeft, roomSize, Vector4.Lerp(wallColor, new Vector4(0.08f, 0.08f, 0.1f, 1f), 0.7f));

    // Murs (un rectangle fin par bord), avec une ouverture centrée là où une porte existe.
    void DrawWallSegment(bool hasDoor, Vector2 segTopLeft, Vector2 segSize, bool horizontal)
    {
        if (!hasDoor)
        {
            DrawPanel(segTopLeft, segSize, wallColor);
            return;
        }

        if (horizontal)
        {
            var gapWidth = segSize.X * doorGap;
            DrawPanel(segTopLeft, new Vector2(segSize.X / 2f - gapWidth / 2f, segSize.Y), wallColor);
            DrawPanel(segTopLeft + new Vector2(segSize.X / 2f + gapWidth / 2f, 0), new Vector2(segSize.X / 2f - gapWidth / 2f, segSize.Y), wallColor);
        }
        else
        {
            var gapHeight = segSize.Y * doorGap;
            DrawPanel(segTopLeft, new Vector2(segSize.X, segSize.Y / 2f - gapHeight / 2f), wallColor);
            DrawPanel(segTopLeft + new Vector2(0, segSize.Y / 2f + gapHeight / 2f), new Vector2(segSize.X, segSize.Y / 2f - gapHeight / 2f), wallColor);
        }
    }

    DrawWallSegment(room.North, roomTopLeft, new Vector2(roomSize.X, wallThickness), true);
    DrawWallSegment(room.South, roomTopLeft + new Vector2(0, roomSize.Y - wallThickness), new Vector2(roomSize.X, wallThickness), true);
    DrawWallSegment(room.West, roomTopLeft, new Vector2(wallThickness, roomSize.Y), false);
    DrawWallSegment(room.East, roomTopLeft + new Vector2(roomSize.X - wallThickness, 0), new Vector2(wallThickness, roomSize.Y), false);

    // Joueur, positionné dans la salle selon dungeonPlayerPos (0..1).
    var playerScreenPos = roomTopLeft + dungeonPlayerPos * roomSize;
    DrawStarterPortrait(playerScreenPos, 20f, new Vector4(0.92f, 0.78f, 0.31f, 1f));

    if (!isCleared)
    {
        // Voir GDD/demande utilisateur — "voir les ennemis avant de les combattre, comme Pokémon
        // Épée" : portrait + nom + élément affichés avant même d'engager le combat, dès que
        // l'aperçu (même tirage exact que le combat réel, voir GetDungeonEncounterPreviewAsync)
        // est chargé pour CETTE salle précise.
        if (dungeonEncounterPreview is { } preview && dungeonEncounterPreviewRoomIndex == dungeonRoomIndex)
        {
            var previewCenter = roomTopLeft + new Vector2(roomSize.X / 2f, roomSize.Y * 0.35f);
            DrawStarterPortrait(previewCenter, 34f, new Vector4(0.95f, 0.25f, 0.25f, 1f));
            DrawStarterPortrait(previewCenter, 30f, CombatTypeColor(preview.Type));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{preview.Name.ToUpperInvariant()} ({preview.Element})",
                previewCenter + new Vector2(0, 46f), 1.6f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        }

        if (combatStartTask is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "...", new Vector2(w / 2f, h * 0.86f), 2.1f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        }
        else if (room.EncounterType == DungeonEncounterType.Coffre)
        {
            DrawPromptBanner("APPUYEZ SUR E POUR OUVRIR LE COFFRE", new Vector2(w / 2f, h * 0.86f));
        }
    }

    if (allCleared)
    {
        DrawPromptBanner("ETAGE NETTOYE - APPUYEZ SUR E POUR DESCENDRE", new Vector2(w / 2f, h * 0.90f));
    }

    // Voir GDD/demande utilisateur — "ajoute une touche pour quitter le donjon hors des combats"
    // : Échap le fait déjà (voir UpdateDungeonCorridor), rappelé ici en permanence.
    TextRenderer.Draw(spriteBatch, whiteTexture, "ECHAP : QUITTER LE DONJON - ZQSD/FLECHES : SE DEPLACER", new Vector2(16f, h - 30f), 1.5f, new Vector4(0.65f, 0.65f, 0.7f, 1f));

    if (dungeonRoomMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, dungeonRoomMessage, new Vector2(w / 2f, h * 0.14f), 2f, new Vector4(0.9f, 0.8f, 0.4f, 1f));
    }

    if (combatMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatMessage, new Vector2(w / 2f, h * 0.14f), 2f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
    }

    // Voir GDD/demande utilisateur — "avant de quitter le donjon ajoute un texte pour demander
    // si il est sûr" : superposé à tout le reste, dessiné en dernier pour rester au premier plan.
    if (dungeonExitConfirmOpen)
    {
        DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0f, 0f, 0f, 0.6f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "QUITTER LE DONJON ?", new Vector2(w / 2f, h * 0.44f), 3f, Vector4.One);
        DrawPromptBanner("ENTREE : CONFIRMER - ECHAP : ANNULER", new Vector2(w / 2f, h * 0.56f));
    }
}

/// <summary>
/// Bandeau d'action mis en évidence (fond sombre + bordure pulsante + texte agrandi) — voir
/// retour utilisateur ("le fait d'appuyer sur Entrée doit être mieux affiché pour que les gens
/// comprennent"). Utilisé pour les invites d'action importantes (couloir de donjon) plutôt que du
/// simple texte centré qui se perdait visuellement.
/// </summary>
void DrawPromptBanner(string text, Vector2 center)
{
    var pulse = 0.6f + 0.4f * MathF.Sin(animationClock * 4f);
    const float pixelSize = 2.3f;
    var textWidth = TextRenderer.MeasureWidth(text, pixelSize);
    var boxSize = new Vector2(textWidth + 48f, 44f);
    var topLeft = center - boxSize / 2f;

    DrawPanel(topLeft, boxSize, new Vector4(0.12f, 0.10f, 0.04f, 0.92f));
    DrawPanel(topLeft, new Vector2(boxSize.X, 4f), new Vector4(0.95f, 0.55f + 0.35f * pulse, 0.25f, 1f));
    DrawPanel(new Vector2(topLeft.X, topLeft.Y + boxSize.Y - 4f), new Vector2(boxSize.X, 4f), new Vector4(0.95f, 0.55f + 0.35f * pulse, 0.25f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, text,
        center - new Vector2(0, TextRenderer.LineHeight(pixelSize) / 2f - 4f),
        pixelSize, new Vector4(1f, 0.75f + 0.25f * pulse, 0.35f, 1f));
}

static Vector4 DungeonRoomColor(DungeonEncounterType type) => type switch
{
    DungeonEncounterType.Monstre => new Vector4(0.6f, 0.25f, 0.25f, 1f),
    DungeonEncounterType.MiniBoss => new Vector4(0.75f, 0.35f, 0.15f, 1f),
    DungeonEncounterType.Boss => new Vector4(0.8f, 0.2f, 0.2f, 1f),
    DungeonEncounterType.BossLegendaire => new Vector4(0.85f, 0.65f, 0.15f, 1f),
    DungeonEncounterType.Coffre => new Vector4(0.75f, 0.62f, 0.20f, 1f),
    DungeonEncounterType.Marchand => new Vector4(0.25f, 0.55f, 0.45f, 1f),
    DungeonEncounterType.Piege => new Vector4(0.45f, 0.25f, 0.50f, 1f),
    DungeonEncounterType.Enigme => new Vector4(0.30f, 0.45f, 0.65f, 1f),
    DungeonEncounterType.Autel => new Vector4(0.55f, 0.45f, 0.70f, 1f),
    DungeonEncounterType.SalleSecrete => new Vector4(0.35f, 0.35f, 0.38f, 1f),
    _ => new Vector4(0.4f, 0.4f, 0.45f, 1f),
};

static string DungeonRoomFlavor(DungeonEncounterType type, bool chestOpened) => type switch
{
    DungeonEncounterType.Monstre => "Un monstre sauvage rode dans cette salle.",
    DungeonEncounterType.MiniBoss => "Un mini-boss redoutable garde le passage.",
    DungeonEncounterType.Boss => "Un boss puissant bloque la sortie de l'etage.",
    DungeonEncounterType.BossLegendaire => "Une presence legendaire emane de cette salle.",
    DungeonEncounterType.Coffre when chestOpened => "Le coffre est deja ouvert.",
    DungeonEncounterType.Coffre => "Un coffre poussiereux attend d'etre ouvert.",
    DungeonEncounterType.Marchand => "Un marchand ambulant propose ses services.",
    DungeonEncounterType.Piege => "Le sol de cette salle semble instable...",
    DungeonEncounterType.Enigme => "Une inscription enigmatique orne le mur.",
    DungeonEncounterType.Autel => "Un autel oublie repose au centre de la piece.",
    DungeonEncounterType.SalleSecrete => "Vous decouvrez une salle secrete.",
    _ => "Quelque chose s'est produit ici autrefois.",
};

/// <summary>Géométrie de la grille de combat à l'écran — factorisé pour rester identique entre <see cref="DrawCombat"/> (rendu) et <see cref="UpdateCombat"/> (détection de clic).</summary>
(float OriginX, float OriginY, float CellSize) CombatGridGeometry()
{
    const float cellSize = 56f;
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;
    var gridWidth = combatState!.GridWidth * cellSize;
    var gridHeight = combatState.GridHeight * cellSize;
    return (w / 2f - gridWidth / 2f, h / 2f - gridHeight / 2f - 30f, cellSize);
}

/// <summary>Cases atteignables par l'action en cours (Déplacement/Attaque) — voir retour utilisateur ("on doit pouvoir voir jusqu'où on peut se déplacer/attaquer").</summary>
IEnumerable<(int X, int Y)> CombatReachableCells(CombatantState actor, CombatActionType action)
{
    var targetsEnemy = action is CombatActionType.Attack or CombatActionType.Capture or CombatActionType.SpecialAbility;
    var range = targetsEnemy
        ? (action == CombatActionType.SpecialAbility && actor.Type == MonsterType.Archer ? actor.AttackRange + 1 : actor.AttackRange)
        : actor.MovementRange;

    for (var y = 0; y < combatState!.GridHeight; y++)
    {
        for (var x = 0; x < combatState.GridWidth; x++)
        {
            if (Math.Abs(x - actor.PositionX) + Math.Abs(y - actor.PositionY) > range)
            {
                continue;
            }

            if (targetsEnemy)
            {
                var target = combatState.Combatants.FirstOrDefault(c => c.IsAlive && c.PositionX == x && c.PositionY == y);
                if (target is null || target.Team == actor.Team)
                {
                    continue;
                }
            }
            else if (combatState.Combatants.Any(c => c.IsAlive && c.PositionX == x && c.PositionY == y))
            {
                continue;
            }

            yield return (x, y);
        }
    }
}

void DrawCombat()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.05f, 0.05f, 0.08f, 1f));

    if (combatState is null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHARGEMENT DU COMBAT...", new Vector2(w / 2f, h / 2f), 3f, Vector4.One);
        return;
    }

    var (originX, originY, cellSize) = CombatGridGeometry();

    for (var y = 0; y < combatState.GridHeight; y++)
    {
        for (var x = 0; x < combatState.GridWidth; x++)
        {
            var cellColor = (x + y) % 2 == 0 ? new Vector4(0.14f, 0.15f, 0.19f, 1f) : new Vector4(0.11f, 0.12f, 0.16f, 1f);
            DrawPanel(new Vector2(originX + x * cellSize + 1, originY + y * cellSize + 1), new Vector2(cellSize - 2, cellSize - 2), cellColor);
        }
    }

    if (combatSelectedAction is { } selectedAction && combatState.CurrentTurnCombatantId is { } actingId
        && combatState.Combatants.FirstOrDefault(c => c.Id == actingId) is { } actingCombatant)
    {
        var highlightColor = selectedAction is CombatActionType.Attack or CombatActionType.Capture
            ? new Vector4(0.9f, 0.3f, 0.25f, 0.35f)
            : new Vector4(0.3f, 0.75f, 0.9f, 0.28f);

        foreach (var (x, y) in CombatReachableCells(actingCombatant, selectedAction))
        {
            DrawPanel(new Vector2(originX + x * cellSize + 1, originY + y * cellSize + 1), new Vector2(cellSize - 2, cellSize - 2), highlightColor);
        }

        DrawPanel(new Vector2(originX + combatCursorX * cellSize + 1, originY + combatCursorY * cellSize + 1),
            new Vector2(cellSize - 2, cellSize - 2), new Vector4(0.9f, 0.75f, 0.35f, 0.4f));
    }

    foreach (var combatant in combatState.Combatants)
    {
        if (!combatant.IsAlive)
        {
            continue;
        }

        var center = new Vector2(originX + combatant.PositionX * cellSize + cellSize / 2f, originY + combatant.PositionY * cellSize + cellSize / 2f);

        if (combatant.Id == combatState.CurrentTurnCombatantId)
        {
            DrawPanel(center - new Vector2(cellSize / 2f - 2, cellSize / 2f - 2), new Vector2(cellSize - 4, cellSize - 4), new Vector4(1f, 1f, 1f, 0.15f));
        }

        // Couleur selon le type (voir GDD/demande utilisateur — "les couleurs des personnages
        // doivent être en fonction de leur type") avec un contour bleu (allié) / rouge (ennemi) —
        // simulé par un losange légèrement plus grand dessiné juste derrière, DrawStarterPortrait
        // n'ayant pas de paramètre de contour propre (réutilisé tel quel ailleurs, ex. sélection
        // du starter, où aucun contour n'est voulu).
        var typeColor = CombatTypeColor(combatant.Type);
        var outlineColor = combatant.Team == 0 ? new Vector4(0.3f, 0.55f, 0.95f, 1f) : new Vector4(0.95f, 0.25f, 0.25f, 1f);
        DrawStarterPortrait(center, cellSize * 0.32f + 4f, outlineColor);
        DrawStarterPortrait(center, cellSize * 0.32f, typeColor);

        var hpRatio = Math.Clamp((float)combatant.CurrentHealth / combatant.MaxHealth, 0f, 1f);
        var barWidth = cellSize * 0.8f;
        var barTop = center - new Vector2(barWidth / 2f, cellSize * 0.5f);
        DrawPanel(barTop, new Vector2(barWidth, 6f), new Vector4(0.2f, 0.05f, 0.05f, 1f));
        DrawPanel(barTop, new Vector2(barWidth * hpRatio, 6f), new Vector4(0.3f, 0.8f, 0.3f, 1f));

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatant.Name.ToUpperInvariant(), center + new Vector2(0, cellSize * 0.42f), 1.1f, Vector4.One);
        // Voir GDD/demande utilisateur — "chaque monstre a un type affiché" : en plus de la
        // couleur du portrait (déjà par type), le nom du type en toutes lettres sous le nom.
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatant.Type.ToString().ToUpperInvariant(), center + new Vector2(0, cellSize * 0.42f + 14f), 0.9f, typeColor);
    }

    if (combatState.IsFinished)
    {
        var resultText = combatState.WinningTeam == 0 ? "VICTOIRE !" : "DEFAITE...";
        var resultColor = combatState.WinningTeam == 0 ? new Vector4(0.4f, 0.9f, 0.4f, 1f) : new Vector4(0.9f, 0.4f, 0.4f, 1f);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, resultText, new Vector2(w / 2f, h - 120f), 4f, resultColor);

        if (activeLoot is { IsResolved: false })
        {
            DrawLootClaim(w, h);
        }
        else
        {
            if (combatState.LastMessage is not null)
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatState.LastMessage, new Vector2(w / 2f, h - 80f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
            }

            if (activeLoot is { IsResolved: true } resolved)
            {
                DrawLootResult(resolved, w, h);
            }

            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE POUR CONTINUER", new Vector2(w / 2f, h - 40f), 2.2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        }
    }
    else
    {
        if (combatState.LastMessage is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatState.LastMessage, new Vector2(w / 2f, h - 150f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        }

        // Voir GDD/demande utilisateur — "un petit texte pour dire à qui est le tour".
        if (combatState.Combatants.FirstOrDefault(c => c.Id == combatState.CurrentTurnCombatantId) is { } turnOwner)
        {
            var turnLabel = turnOwner.Team == 0 ? $"Tour de {turnOwner.Name} (vous)" : $"Tour de {turnOwner.Name}";
            var turnColor = turnOwner.Team == 0 ? new Vector4(0.55f, 0.85f, 0.6f, 1f) : new Vector4(0.9f, 0.6f, 0.55f, 1f);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, turnLabel, new Vector2(w / 2f, h - 195f), 1.8f, turnColor);
        }

        // Compte à rebours du tour (voir GDD/demande utilisateur — "timer de 10 secondes entre
        // chaque tour") : approximatif côté client (horloges non synchronisées avec le serveur,
        // qui fait foi et passe réellement le tour au-delà du délai), mais suffisant pour donner
        // une idée claire du temps restant.
        var turnSecondsLeft = Math.Max(0, GameInfo.CombatTurnTimeoutSeconds - (DateTime.UtcNow - combatState.TurnStartedAtUtc).TotalSeconds);
        var timerColor = turnSecondsLeft <= 3 ? new Vector4(0.95f, 0.4f, 0.35f, 1f) : new Vector4(0.75f, 0.75f, 0.8f, 1f);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{turnSecondsLeft:0}s", new Vector2(w / 2f, h - 175f), 2.2f, timerColor);

        var myTurn = combatState.CurrentTurnCombatantId is { } currentId
            && combatState.Combatants.FirstOrDefault(c => c.Id == currentId) is { Team: 0 };

        if (myTurn)
        {
            if (combatSelectedAction is null)
            {
                var current = combatState.Combatants.First(c => c.Id == combatState.CurrentTurnCombatantId);
                var isImmediateAbility = current.Type == MonsterType.Soigneur;

                // Voir GDD/demande utilisateur — "cooldown pour le spécial" : affiché sur le
                // bouton lui-même plutôt qu'à part, le serveur reste seul juge (rejette l'action
                // si on clique quand même, message affiché normalement via combatMessage).
                var abilityLabel = current.SpecialAbilityCooldownRemaining > 0
                    ? $"4:CAPACITE ({current.SpecialAbilityCooldownRemaining})"
                    : "4:CAPACITE";

                // Boutons cliquables (voir retour utilisateur — "on doit pouvoir cliquer pour
                // faire les actions") en plus des raccourcis clavier 1-6, toujours actifs.
                List<(string Label, CombatActionType Action)> actionButtons =
                [
                    ("1:DEPLACER", CombatActionType.Move),
                    ("2:ATTAQUER", CombatActionType.Attack),
                    ("3:PASSER", CombatActionType.Pass),
                    (abilityLabel, CombatActionType.SpecialAbility),
                ];

                if (captureSphereItemId is not null)
                {
                    actionButtons.Add(("5:CAPTURER", CombatActionType.Capture));
                }

                if (!combatState.IsDungeonCombat)
                {
                    // Voir GDD/demande utilisateur — "ajoute un bouton pour fuir les combats, on
                    // ne peut pas en donjon mais en dehors on peut" : absent plutôt que désactivé.
                    // Lu depuis combatState.IsDungeonCombat (autoritaire), pas interiorIsDungeon
                    // (état de scène local pouvant rester périmé — voir bug corrigé).
                    actionButtons.Add(("6:FUIR", CombatActionType.Flee));
                }

                const float buttonPixelSize = 2f;
                const float buttonGap = 24f;
                var widths = actionButtons.Select(b => TextRenderer.MeasureWidth(b.Label, buttonPixelSize)).ToList();
                var totalWidth = widths.Sum() + buttonGap * (actionButtons.Count - 1);
                var buttonX = w / 2f - totalWidth / 2f;

                for (var i = 0; i < actionButtons.Count; i++)
                {
                    var center = new Vector2(buttonX + widths[i] / 2f, h - 70f);
                    if (DrawClickableCentered(actionButtons[i].Label, center, buttonPixelSize, new Vector4(0.9f, 0.75f, 0.35f, 1f)))
                    {
                        var isImmediateAction = actionButtons[i].Action == CombatActionType.Pass
                            || actionButtons[i].Action == CombatActionType.Flee
                            || (actionButtons[i].Action == CombatActionType.SpecialAbility && isImmediateAbility);

                        if (isImmediateAction)
                        {
                            SendCombatAction(actionButtons[i].Action, 0, 0);
                        }
                        else
                        {
                            combatSelectedAction = actionButtons[i].Action;
                            combatCursorX = current.PositionX;
                            combatCursorY = current.PositionY;
                        }
                    }

                    buttonX += widths[i] + buttonGap;
                }
            }
            else
            {
                var label = $"{combatSelectedAction.ToString()!.ToUpperInvariant()} - CLIQUEZ/FLECHES+ENTREE POUR AGIR - ECHAP : ANNULER";
                if (DrawClickableCentered(label, new Vector2(w / 2f, h - 70f), 1.7f, new Vector4(0.9f, 0.75f, 0.35f, 1f)))
                {
                    combatSelectedAction = null;
                }
            }
        }
        else
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "TOUR ADVERSE...", new Vector2(w / 2f, h - 70f), 2.2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        }
    }

    if (combatMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatMessage, new Vector2(w / 2f, h - 20f), 1.8f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
    }
}

/// <summary>Liste des 4 objets du butin (voir GDD), navigable au clavier — voir <see cref="UpdateLoot"/>.</summary>
/// <summary>
/// Voir retour utilisateur — "le choix de l'objet ne se voit pas bien après un combat" : chaque
/// objet a désormais sa propre rangée avec fond et bordure de sélection (au lieu d'un simple
/// préfixe "&gt; " sur du texte), cliquable directement (voir <see cref="DrawClickableCentered"/>
/// ailleurs dans le fichier pour le même principe), et un badge affiche le nombre de joueurs
/// l'ayant actuellement choisi (voir GDD/demande utilisateur — "afficher une petite icône pour
/// dire choisi par un joueur, ajouter en 2 si ils sont 2 ainsi de suite").
/// </summary>
void DrawLootClaim(int w, int h)
{
    if (activeLoot is not { } loot)
    {
        return;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "BUTIN - CHOISISSEZ UN OBJET", new Vector2(w / 2f, h - 210f), 2.4f, new Vector4(0.9f, 0.8f, 0.4f, 1f));

    // Compte à rebours du choix (voir GDD/demande utilisateur — "timer de 10 secondes pour le
    // choix des gains") : approximatif côté client, le serveur fait foi et résout réellement le
    // butin au-delà du délai (voir GameInfo.LootChoiceTimeoutSeconds).
    var lootSecondsLeft = Math.Max(0, GameInfo.LootChoiceTimeoutSeconds - (DateTime.UtcNow - loot.CreatedAtUtc).TotalSeconds);
    var lootTimerColor = lootSecondsLeft <= 3 ? new Vector4(0.95f, 0.4f, 0.35f, 1f) : new Vector4(0.75f, 0.75f, 0.8f, 1f);
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{lootSecondsLeft:0}s", new Vector2(w / 2f, h - 185f), 2f, lootTimerColor);

    const float rowWidth = 440f;
    const float rowHeight = 32f;
    const float rowGap = 8f;
    var top = h - 180f;

    for (var i = 0; i < loot.Items.Count; i++)
    {
        var isSelected = i == lootCursor;
        var rowTopLeft = new Vector2(w / 2f - rowWidth / 2f, top + i * (rowHeight + rowGap));

        DrawPanel(rowTopLeft, new Vector2(rowWidth, rowHeight), isSelected ? new Vector4(0.95f, 0.8f, 0.3f, 0.3f) : new Vector4(0.08f, 0.08f, 0.11f, 0.85f));
        if (isSelected)
        {
            DrawPanel(rowTopLeft, new Vector2(4f, rowHeight), new Vector4(0.95f, 0.8f, 0.3f, 1f));
        }

        // Voir GDD/demande utilisateur — "% de drop plus ou moins rare selon la rareté, affichée
        // d'une couleur différente, rareté ajoutée à la fin du nom" : couleur toujours celle de la
        // rareté (pas écrasée par la sélection), pour rester visible d'un coup d'œil.
        var textColor = RarityColor(loot.Items[i].Rarity);
        var label = $"{loot.Items[i].Name.ToUpperInvariant()} ({RarityLabel(loot.Items[i].Rarity).ToUpperInvariant()})";
        TextRenderer.Draw(spriteBatch, whiteTexture, label, rowTopLeft + new Vector2(16f, 8f), 1.9f, textColor);

        if (loot.ClaimCountsByItemIndex.TryGetValue(i, out var claimCount) && claimCount > 0)
        {
            var badgeSize = new Vector2(28f, 24f);
            var badgeTopLeft = rowTopLeft + new Vector2(rowWidth - badgeSize.X - 8f, (rowHeight - badgeSize.Y) / 2f);
            DrawPanel(badgeTopLeft, badgeSize, new Vector4(0.25f, 0.6f, 0.9f, 0.95f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, claimCount.ToString(), badgeTopLeft + badgeSize / 2f, 1.8f, Vector4.One);
        }

        if (mouse.WasButtonJustPressed(MouseButton.Left)
            && mouse.Position.X >= rowTopLeft.X && mouse.Position.X <= rowTopLeft.X + rowWidth
            && mouse.Position.Y >= rowTopLeft.Y && mouse.Position.Y <= rowTopLeft.Y + rowHeight)
        {
            lootCursor = i;
            lootMessage = null;
            lootTask = combatApi!.ClaimLootAsync(options.SessionToken!, loot.LootId, chosenCharacterId!.Value, lootCursor);
        }
    }

    var messageY = top + loot.Items.Count * (rowHeight + rowGap) + 14f;
    if (lootMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, lootMessage, new Vector2(w / 2f, messageY), 1.8f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
        messageY += 24f;
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "FLECHES OU CLIC : CHOISIR - ENTREE : RECLAMER", new Vector2(w / 2f, messageY + 6f), 2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
}

/// <summary>Résultat du tirage de butin, une fois tous les joueurs éligibles passés (voir <see cref="LootRoll"/> côté serveur).</summary>
void DrawLootResult(LootSessionState resolved, int w, int h)
{
    if (resolved.Winners is not { } winners || winners.Count == 0)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUN OBJET RECLAME", new Vector2(w / 2f, h - 100f), 2f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
        return;
    }

    var mine = chosenCharacterId;
    var y = h - 110f;
    foreach (var (itemIndex, winnerCharacterId) in winners)
    {
        var item = resolved.Items[itemIndex];
        var wonByMe = mine == winnerCharacterId;
        var itemLabel = $"{item.Name.ToUpperInvariant()} ({RarityLabel(item.Rarity).ToUpperInvariant()})";
        var label = wonByMe ? $"VOUS REMPORTEZ : {itemLabel}" : $"{itemLabel} : ATTRIBUE";
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, label, new Vector2(w / 2f, y), 1.9f, RarityColor(item.Rarity));
        y += 24f;
    }
}

/// <summary>
/// Aperçu du personnage pour la création/sélection (voir GDD — "scène 3D animée"). Le moteur est
/// isométrique 2D (pas de vraie caméra 3D) : l'effet de caméra dynamique est approximé par un
/// balancement horizontal et un ombrage qui bascule d'un côté à l'autre façon "tourne-disque",
/// combiné à un léger bob vertical — voir Docs/README.md pour cette limite assumée.
/// </summary>
void DrawCharacterPreview(Vector2 center, float scale, int skinIndex, int hairStyleIndex, int hairColorIndex, int clothesColorIndex, int accessoryIndex, float clock)
{
    var turn = MathF.Sin(clock * 0.7f);
    var bob = MathF.Sin(clock * 2f) * 3f * scale;
    var sway = turn * 8f * scale;

    var skin = CharacterAppearancePalette.SkinColors[skinIndex].Color;
    var hairColor = CharacterAppearancePalette.HairColors[hairColorIndex].Color;
    var clothesColor = CharacterAppearancePalette.ClothesColors[clothesColorIndex].Color;

    var leftShade = 1f - MathF.Max(0f, turn) * 0.25f;
    var rightShade = 1f - MathF.Max(0f, -turn) * 0.25f;

    var footCenter = center + new Vector2(sway, bob);
    var bodyHeight = 90f * scale;
    var halfWidth = 34f * scale;

    var bodyTopCenter = footCenter - new Vector2(0, bodyHeight);
    var bodyTop = bodyTopCenter + new Vector2(0, -halfWidth * 0.5f);
    var bodyRight = bodyTopCenter + new Vector2(halfWidth, 0);
    var bodyBottom = bodyTopCenter + new Vector2(0, halfWidth * 0.5f);
    var bodyLeft = bodyTopCenter + new Vector2(-halfWidth, 0);

    var groundLeft = footCenter + new Vector2(-halfWidth, 0);
    var groundBottom = footCenter + new Vector2(0, halfWidth * 0.5f);
    var groundRight = footCenter + new Vector2(halfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, bodyLeft, bodyBottom, groundBottom, groundLeft, clothesColor * leftShade);
    spriteBatch.DrawQuad(whiteTexture, bodyBottom, bodyRight, groundRight, groundBottom, clothesColor * rightShade);
    spriteBatch.DrawQuad(whiteTexture, bodyTop, bodyRight, bodyBottom, bodyLeft, clothesColor);

    var headHalfWidth = halfWidth * 0.62f;
    var headCenter = bodyTopCenter - new Vector2(0, headHalfWidth * 1.3f);
    var headTop = headCenter + new Vector2(0, -headHalfWidth);
    var headRight = headCenter + new Vector2(headHalfWidth, 0);
    var headBottom = headCenter + new Vector2(0, headHalfWidth);
    var headLeft = headCenter + new Vector2(-headHalfWidth, 0);
    spriteBatch.DrawQuad(whiteTexture, headTop, headRight, headBottom, headLeft, skin);

    var eyeSize = new Vector2(headHalfWidth * 0.22f, headHalfWidth * 0.22f);
    var eyeY = headCenter.Y - eyeSize.Y * 0.2f;
    var eyeColor = new Vector4(0.15f, 0.12f, 0.10f, 1f);
    spriteBatch.Draw(whiteTexture, new Vector2(headCenter.X - eyeSize.X * 1.6f, eyeY), eyeSize, eyeColor);
    spriteBatch.Draw(whiteTexture, new Vector2(headCenter.X + eyeSize.X * 0.6f, eyeY), eyeSize, eyeColor);

    switch (hairStyleIndex)
    {
        case 0: // Court
            spriteBatch.Draw(whiteTexture, headTop - new Vector2(headHalfWidth, 6f * scale), new Vector2(headHalfWidth * 2f, 10f * scale), hairColor);
            break;
        case 1: // Long
            spriteBatch.Draw(whiteTexture, headTop - new Vector2(headHalfWidth, 6f * scale), new Vector2(headHalfWidth * 2f, 10f * scale), hairColor);
            spriteBatch.Draw(whiteTexture, headLeft - new Vector2(5f * scale, 0), new Vector2(6f * scale, halfWidth * 1.6f), hairColor);
            spriteBatch.Draw(whiteTexture, headRight, new Vector2(6f * scale, halfWidth * 1.6f), hairColor);
            break;
        case 2: // Crête
            var spikeTop = headTop - new Vector2(3f * scale, 16f * scale);
            spriteBatch.DrawQuad(whiteTexture, spikeTop, headTop + new Vector2(9f * scale, 0), headTop + new Vector2(9f * scale, 4f * scale), spikeTop + new Vector2(0, 4f * scale), hairColor);
            break;
        // case 3 (Chauve) : aucun trait dessiné, intentionnel.
        case 4: // Tresses
            spriteBatch.Draw(whiteTexture, headTop - new Vector2(headHalfWidth, 6f * scale), new Vector2(headHalfWidth * 2f, 10f * scale), hairColor);
            spriteBatch.Draw(whiteTexture, headLeft - new Vector2(4f * scale, -4f * scale), new Vector2(5f * scale, halfWidth * 1.8f), hairColor);
            spriteBatch.Draw(whiteTexture, headRight + new Vector2(0, 4f * scale), new Vector2(5f * scale, halfWidth * 1.8f), hairColor);
            break;
    }

    switch (accessoryIndex)
    {
        case 1: // Chapeau
            spriteBatch.Draw(whiteTexture, headTop - new Vector2(headHalfWidth * 1.3f, 14f * scale), new Vector2(headHalfWidth * 2.6f, 8f * scale), new Vector4(0.25f, 0.2f, 0.15f, 1f));
            spriteBatch.Draw(whiteTexture, headTop - new Vector2(headHalfWidth * 0.7f, 22f * scale), new Vector2(headHalfWidth * 1.4f, 10f * scale), new Vector4(0.3f, 0.24f, 0.18f, 1f));
            break;
        case 2: // Bandeau
            spriteBatch.Draw(whiteTexture, headLeft + new Vector2(0, -3f * scale), new Vector2(headHalfWidth * 2f, 6f * scale), new Vector4(0.7f, 0.2f, 0.2f, 1f));
            break;
        case 3: // Lunettes
            var glassesY = eyeY - eyeSize.Y * 0.3f;
            spriteBatch.Draw(whiteTexture, new Vector2(headCenter.X - eyeSize.X * 1.9f, glassesY), new Vector2(eyeSize.X * 1.4f, eyeSize.Y * 1.4f), new Vector4(0.1f, 0.1f, 0.1f, 0.85f));
            spriteBatch.Draw(whiteTexture, new Vector2(headCenter.X + eyeSize.X * 0.3f, glassesY), new Vector2(eyeSize.X * 1.4f, eyeSize.Y * 1.4f), new Vector4(0.1f, 0.1f, 0.1f, 0.85f));
            break;
        case 4: // Couronne
            spriteBatch.Draw(whiteTexture, headTop - new Vector2(headHalfWidth, 10f * scale), new Vector2(headHalfWidth * 2f, 6f * scale), new Vector4(0.9f, 0.75f, 0.25f, 1f));
            spriteBatch.DrawQuad(whiteTexture,
                headTop - new Vector2(1.5f * scale, 18f * scale), headTop + new Vector2(1.5f * scale, -10f * scale),
                headTop + new Vector2(1.5f * scale, -4f * scale), headTop - new Vector2(1.5f * scale, -4f * scale),
                new Vector4(0.9f, 0.75f, 0.25f, 1f));
            break;
    }
}

void DrawLoading()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.04f, 0.05f, 0.09f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CONNEXION EN COURS...", new Vector2(w / 2f, h / 2f), 3.2f, Vector4.One);
}

void DrawCharacterSelect()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.04f, 0.05f, 0.09f, 1f));
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHOISIS TON PERSONNAGE", new Vector2(w / 2f, 60f), 3.6f, Vector4.One);

    const float rowHeight = 70f;
    var totalRows = myCharacters.Count + 1;
    var originY = h / 2f - (totalRows * rowHeight) / 2f;

    for (var i = 0; i < myCharacters.Count; i++)
    {
        var character = myCharacters[i];
        var y = originY + i * rowHeight;
        var selected = i == characterCursor;

        if (selected)
        {
            DrawPanel(new Vector2(w / 2f - 220f, y), new Vector2(440f, rowHeight - 8f), new Vector4(1f, 1f, 1f, 0.10f));
        }

        DrawCharacterPreview(new Vector2(w / 2f - 160f, y + rowHeight / 2f + 24f), 0.34f,
            character.SkinColorIndex, character.HairStyleIndex, character.HairColorIndex, character.ClothesColorIndex, character.AccessoryIndex, animationClock);
        TextRenderer.Draw(spriteBatch, whiteTexture, $"{character.Name.ToUpperInvariant()} - NIV. {character.Level}",
            new Vector2(w / 2f - 50f, y + rowHeight / 2f - 8f), 2.2f, Vector4.One);
    }

    var newY = originY + myCharacters.Count * rowHeight;
    if (characterCursor == myCharacters.Count)
    {
        DrawPanel(new Vector2(w / 2f - 220f, newY), new Vector2(440f, rowHeight - 8f), new Vector4(1f, 1f, 1f, 0.10f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "+ NOUVEAU PERSONNAGE", new Vector2(w / 2f, newY + rowHeight / 2f), 2.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "FLECHES POUR CHOISIR - ENTREE POUR VALIDER", new Vector2(w / 2f, h - 40f), 2.2f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
}

void DrawCharacterCreate()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.04f, 0.05f, 0.09f, 1f));
    DrawCharacterPreview(new Vector2(w / 2f, h * 0.42f), 0.75f, createSkinIndex, createHairStyleIndex, createHairColorIndex, createClothesColorIndex, createAccessoryIndex, createPreviewClock);

    switch (createStage)
    {
        case CreateStage.Name:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "QUEL EST TON NOM ?", new Vector2(w / 2f, h * 0.62f), 3.2f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, createName.Length > 0 ? createName.ToUpperInvariant() : "_",
                new Vector2(w / 2f, h * 0.70f), 3.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "TAPEZ VOTRE NOM (3 LETTRES MIN.) - ENTREE POUR CONTINUER",
                new Vector2(w / 2f, h * 0.90f), 2f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
            break;

        case CreateStage.Appearance:
            Span<string> fieldNames = ["TEINTE DE PEAU", "STYLE DE CHEVEUX", "COULEUR DE CHEVEUX", "COULEUR DE VETEMENTS", "ACCESSOIRE"];
            Span<string> fieldValues =
            [
                CharacterAppearancePalette.SkinColors[createSkinIndex].Name,
                CharacterAppearancePalette.HairStyleNames[createHairStyleIndex],
                CharacterAppearancePalette.HairColors[createHairColorIndex].Name,
                CharacterAppearancePalette.ClothesColors[createClothesColorIndex].Name,
                CharacterAppearancePalette.AccessoryNames[createAccessoryIndex],
            ];

            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "PERSONNALISE TON APPARENCE", new Vector2(w / 2f, h * 0.60f), 3f, Vector4.One);
            for (var i = 0; i < fieldNames.Length; i++)
            {
                var y = h * 0.68f + i * 28f;
                var active = i == createAppearanceField;
                var color = active ? new Vector4(0.9f, 0.75f, 0.35f, 1f) : new Vector4(0.8f, 0.8f, 0.85f, 1f);
                var prefix = active ? "< " : "  ";
                var suffix = active ? " >" : "  ";
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{prefix}{fieldNames[i]} : {fieldValues[i]}{suffix}", new Vector2(w / 2f, y), 2f, color);
            }

            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "HAUT/BAS : CHAMP - GAUCHE/DROITE : VALEUR - ENTREE : CONTINUER",
                new Vector2(w / 2f, h * 0.95f), 1.8f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
            break;

        case CreateStage.ClassKingdom:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CLASSE ET ROYAUME", new Vector2(w / 2f, h * 0.62f), 3f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"CLASSE : {classValues[createClassIndex]}".ToUpperInvariant(),
                new Vector2(w / 2f, h * 0.70f), 2.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"ROYAUME : {kingdomValues[createKingdomIndex]}".ToUpperInvariant(),
                new Vector2(w / 2f, h * 0.77f), 2.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "GAUCHE/DROITE : CLASSE - HAUT/BAS : ROYAUME - ENTREE : CONTINUER",
                new Vector2(w / 2f, h * 0.92f), 1.8f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
            break;

        case CreateStage.Confirm:
        case CreateStage.Sending:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, createName.ToUpperInvariant(), new Vector2(w / 2f, h * 0.60f), 3.4f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{classValues[createClassIndex]} - {kingdomValues[createKingdomIndex]}".ToUpperInvariant(),
                new Vector2(w / 2f, h * 0.67f), 2.4f, new Vector4(0.85f, 0.85f, 0.9f, 1f));

            if (createStage == CreateStage.Sending)
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, "...", new Vector2(w / 2f, h * 0.88f), 2.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
            }
            else
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : CONFIRMER - ECHAP : RETOUR", new Vector2(w / 2f, h * 0.88f), 2.2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            }

            if (createErrorMessage is not null)
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, createErrorMessage, new Vector2(w / 2f, h * 0.94f), 2f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
            }

            break;
    }
}

/// <summary>Couleur de remplissage en combat selon le type de monstre (voir GDD/demande utilisateur).</summary>
static Vector4 CombatTypeColor(MonsterType type) => type switch
{
    MonsterType.Guerrier => new Vector4(0.82f, 0.4f, 0.22f, 1f),
    MonsterType.Archer => new Vector4(0.38f, 0.72f, 0.36f, 1f),
    MonsterType.Soigneur => new Vector4(0.92f, 0.84f, 0.4f, 1f),
    // Voir GDD/demande utilisateur — bestiaire étendu (nouveaux "rôles").
    MonsterType.Tank => new Vector4(0.45f, 0.45f, 0.55f, 1f),
    MonsterType.Mage => new Vector4(0.45f, 0.35f, 0.85f, 1f),
    MonsterType.Assassin => new Vector4(0.3f, 0.28f, 0.34f, 1f),
    MonsterType.Support => new Vector4(0.5f, 0.85f, 0.75f, 1f),
    MonsterType.Invocateur => new Vector4(0.65f, 0.35f, 0.65f, 1f),
    MonsterType.Berserker => new Vector4(0.85f, 0.18f, 0.18f, 1f),
    MonsterType.Controleur => new Vector4(0.35f, 0.6f, 0.85f, 1f),
    _ => new Vector4(0.7f, 0.7f, 0.75f, 1f),
};

void DrawStarterPortrait(Vector2 center, float radius, Vector4 color)
{
    var top = center + new Vector2(0, -radius);
    var right = center + new Vector2(radius * 0.85f, 0);
    var bottom = center + new Vector2(0, radius);
    var left = center + new Vector2(-radius * 0.85f, 0);
    spriteBatch.DrawQuad(whiteTexture, top, right, bottom, left, color);

    var innerRadius = radius * 0.42f;
    var innerCenter = center - new Vector2(0, radius * 0.12f);
    var innerTop = innerCenter + new Vector2(0, -innerRadius);
    var innerRight = innerCenter + new Vector2(innerRadius * 0.85f, 0);
    var innerBottom = innerCenter + new Vector2(0, innerRadius);
    var innerLeft = innerCenter + new Vector2(-innerRadius * 0.85f, 0);
    spriteBatch.DrawQuad(whiteTexture, innerTop, innerRight, innerBottom, innerLeft, Vector4.Lerp(color, Vector4.One, 0.55f));
}

void DrawStarterGrid(int w, int h)
{
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "CHOISIS TON COMPAGNON", new Vector2(w / 2f, 56f), 3.6f, Vector4.One);

    const float cellWidth = 190f;
    const float cellHeight = 168f;
    var rows = (int)MathF.Ceiling(starterChoices.Count / (float)StarterColumns);
    var gridWidth = Math.Min(starterChoices.Count, StarterColumns) * cellWidth;
    var gridHeight = rows * cellHeight;
    var originX = w / 2f - gridWidth / 2f;
    var originY = h / 2f - gridHeight / 2f + 20f;

    for (var i = 0; i < starterChoices.Count; i++)
    {
        var species = starterChoices[i];
        var col = i % StarterColumns;
        var row = i / StarterColumns;
        var cellCenter = new Vector2(originX + col * cellWidth + cellWidth / 2f, originY + row * cellHeight + cellHeight / 2f);
        var selected = i == starterCursor;
        var bob = selected ? MathF.Sin(animationClock * 6f) * 4f : 0f;

        if (selected)
        {
            DrawPanel(cellCenter - new Vector2(cellWidth * 0.42f, cellHeight * 0.46f), new Vector2(cellWidth * 0.84f, cellHeight * 0.86f), new Vector4(1f, 1f, 1f, 0.10f));
        }

        DrawStarterPortrait(cellCenter + new Vector2(0, -14f + bob), 44f, ElementColor(species.Element));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, species.Name.ToUpperInvariant(), cellCenter + new Vector2(0, 56f), 2f, Vector4.One);
    }

    // Voir GDD/demande utilisateur — "pour les indications de touche, fais comme Amis/Profil".
    DrawPromptBanner("FLECHES POUR CHOISIR - ENTREE POUR VALIDER", new Vector2(w / 2f, h - 40f));
}

void DrawStarterConfirm(int w, int h)
{
    var species = starterChoices[starterConfirmIndex!.Value];
    var elementColor = ElementColor(species.Element);
    var scale = 1f + MathF.Sin(starterAnimClock * 5f) * 0.08f;

    DrawStarterPortrait(new Vector2(w / 2f, h * 0.36f), 90f * scale, elementColor);

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, species.Name.ToUpperInvariant(), new Vector2(w / 2f, h * 0.58f), 4.4f, Vector4.One);
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, species.Element.ToString().ToUpperInvariant(), new Vector2(w / 2f, h * 0.65f), 2.2f, elementColor);
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, WrapText(species.Lore, 42), new Vector2(w / 2f, h * 0.73f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));

    if (starterStage == StarterStage.Sending)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "...", new Vector2(w / 2f, h * 0.88f), 2.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
    }
    else
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : CONFIRMER - ECHAP : RETOUR", new Vector2(w / 2f, h * 0.88f), 2.2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
    }

    if (starterErrorMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, starterErrorMessage, new Vector2(w / 2f, h * 0.94f), 2f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
    }
}

void DrawStarterSelection()
{
    var w = uiCamera.ViewportWidth;
    var h = uiCamera.ViewportHeight;

    DrawPanel(Vector2.Zero, new Vector2(w, h), new Vector4(0.04f, 0.05f, 0.09f, 1f));

    switch (starterStage)
    {
        case StarterStage.Introduction:
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "UN VIEUX GARDIEN VOUS ACCUEILLE", new Vector2(w / 2f, h * 0.35f), 4.2f, Vector4.One);
            TextRenderer.DrawCentered(spriteBatch, whiteTexture,
                "\"AVANT DE PARTIR A L'AVENTURE,\nCHOISIS TON PREMIER COMPAGNON...\"",
                new Vector2(w / 2f, h * 0.48f), 2.6f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "APPUYEZ SUR ENTREE", new Vector2(w / 2f, h * 0.80f), 2.6f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            break;

        case StarterStage.Choosing:
            DrawStarterGrid(w, h);
            break;

        case StarterStage.Confirming:
        case StarterStage.Sending:
            DrawStarterConfirm(w, h);
            break;
    }
}

enum SceneMode
{
    CharacterSelect,
    CharacterCreate,
    Loading,
    Outdoor,
    Interior,
    StarterSelection,
    Combat,
}

enum CreateStage
{
    Name,
    Appearance,
    ClassKingdom,
    Confirm,
    Sending,
}

enum InteractionKind
{
    Building,
    Dungeon,
    Npc,
}

enum PanelKind
{
    None,
    Inventory,
    Guild,
    Shop,
    Party,
    Arena,
    Monsters,
    Chat,
    Auction,
    Craft,
    Friends,
    Profile,
    Leaderboard,
    QuestList,
    Duel,
    GemShop,
    Kingdom,

    /// <summary>Voir GDD/demande utilisateur — "un UI avec un bouton pour voir les métiers, les niveaux de chaque métier".</summary>
    Professions,

    /// <summary>Voir GDD/demande utilisateur — "un pass de niveaux de joueur".</summary>
    BattlePass,

    /// <summary>Voir GDD/demande utilisateur — "un boss monde".</summary>
    WorldBoss,
}

/// <summary>Sous-état du panneau Guilde (voir GDD — rejoindre/rechercher/créer).</summary>
enum GuildPanelMode
{
    None,
    Create,
    Search,
}

/// <summary>Autre joueur visible sur la carte (voir GDD — visibilité globale, même hors groupe). Porte son grade pour la liste des joueurs en ligne.</summary>
record RemotePlayer(string Name, Vector2 Position, UserRank Rank = UserRank.Joueur);

/// <summary>Un message affiché dans le panneau Tchat (voir GDD — tchat global/tchat de guilde).</summary>
record ChatLine(ChatChannel Channel, string SenderName, UserRank Rank, string Message, int SenderGradeTier = 0);

enum StarterStage
{
    Introduction,
    Choosing,
    Confirming,
    Sending,
}

sealed record NearbyInteraction(InteractionKind Kind, string Label, Building? Building, Npc? Npc);
