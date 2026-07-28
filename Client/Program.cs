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
using Aetheria.Shared.Models.Combat;
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

// Exploration du donjon en couloir linéaire (voir GDD — "mobs/loot au fil du chemin") : la
// séquence de salles d'un étage (voir DungeonFloorGenerator côté serveur) traversée une par une,
// combat pour les salles Monstre/MiniBoss/Boss/BossLegendaire, coffre d'or pour les salles Coffre,
// texte d'ambiance pour les autres types (non simulés, voir Docs/README.md). Disponible
// uniquement en mode connecté (worldMap.DungeonId résolu) — le mode démo hors-ligne garde
// l'ancien stub (un seul combat aléatoire, voir StartWildCombatAsync).
var dungeonFloorNumber = 1;
DungeonFloor? dungeonFloor = null;
var dungeonRoomIndex = 0;
Task<DungeonFloor?>? dungeonFloorTask = null;
Task<int?>? dungeonChestTask = null;
var dungeonChestOpened = false;
string? dungeonRoomMessage = null;

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

// Butin de victoire (voir GDD — 4 objets à départager, tirage aléatoire en cas d'égalité) :
// affiché après un combat gagné, avant de revenir à la scène d'intérieur.
LootSessionState? activeLoot = null;
var lootCursor = 0;
Task<LootSessionState?>? lootTask = null;
string? lootMessage = null;

// Dialogue PNJ, superposé au monde extérieur (le déplacement se fige tant qu'il est ouvert).
Npc? activeDialogueNpc = null;
var dialogueLineIndex = 0;

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
GuildSummary? myGuild = null;
var guildLoaded = false;

// Tchat global/guilde et grade du joueur (voir GDD/demande utilisateur — "un tchat global, un
// tchat de guilde, une liste des joueurs en ligne avec leur grade"). Historique borné en mémoire
// uniquement (pas de persistance des messages), reçu/renseigné par le serveur via
// ChatMessagePacket (voir PlayerSession.HandleChatMessage côté serveur).
var myRank = UserRank.Joueur;
var chatChannel = ChatChannel.Global;
var chatTextInput = string.Empty;
var chatMessages = new List<ChatLine>();
const int MaxChatLines = 100;

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

// Groupe (voir GDD — bouton Groupe, XP partagée, visibilité globale même hors groupe).
PartySummary? myParty = null;
var partyLoaded = false;
var partyJoinPromptOpen = false;
var partyJoinInput = string.Empty;
string? partyMessage = null;
Task<PartySummary?>? partyActionTask = null;

// Gestion des créatures (voir GDD — UI montres : monter de niveau, objet à donner).
List<MonsterInstanceData> ownedMonsters = [];
Dictionary<int, MonsterSpeciesData> speciesById = [];
var monsterCursor = 0;
var monstersLoaded = false;
var monsterGiveItemMode = false;
var monsterGiveItemCursor = 0;
Task<MonsterInstanceData?>? monsterGiveItemTask = null;
string? monsterMessage = null;

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

// Sélection/création de personnage (voir GDD) : ne se fait plus dans le Launcher, mais en jeu,
// avant la connexion TCP proprement dite. `--characterId` reste accepté pour compatibilité
// (anciens raccourcis) : dans ce cas on saute directement l'écran de sélection.
Guid? chosenCharacterId = options.CharacterId;
List<CharacterSummary> myCharacters = [];
var characterCursor = 0;

var isConnectedMode = options.SessionToken is not null;

if (isConnectedMode)
{
    // Le tunnel ngrok éventuel (voir GDD/demande utilisateur — "utilise ngrok") ne couvre que
    // l'API HTTP de compte : la connexion TCP de jeu (ConnectAndEnterWorld) continue de cibler
    // options.Host/options.Port (redirection de ports classique côté routeur).
    var apiBaseUrl = options.ResolveApiBaseUrl(GameInfo.DefaultAccountApiPort);
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
            UpdateDungeonCorridor();
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
        UpdateCombat();
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

    if (keyboard.WasJustPressed(Key.I)) OpenPanel(PanelKind.Inventory);
    else if (keyboard.WasJustPressed(Key.G)) OpenPanel(PanelKind.Guild);
    else if (keyboard.WasJustPressed(Key.B)) OpenPanel(PanelKind.Shop);
    else if (keyboard.WasJustPressed(Key.P)) OpenPanel(PanelKind.Party);
    else if (keyboard.WasJustPressed(Key.V)) OpenPanel(PanelKind.Arena);
    else if (keyboard.WasJustPressed(Key.M)) OpenPanel(PanelKind.Monsters);
    else if (keyboard.WasJustPressed(Key.T)) OpenPanel(PanelKind.Chat);

    Vector2 positionBeforeInput;
    lock (stateLock)
    {
        positionBeforeInput = gridPosition;
    }

    // Clic gauche : calcule la case visée (transformation isométrique inverse) et y trace un chemin.
    if (mouse.WasButtonJustPressed(MouseButton.Left))
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
            case InteractionKind.Npc:
                activeDialogueNpc = interaction.Npc;
                dialogueLineIndex = 0;
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
                dungeonChestOpened = false;
                dungeonRoomMessage = null;
                dungeonEncounterPreview = null;
                dungeonEncounterPreviewTask = null;
                dungeonEncounterPreviewRoomIndex = -1;
                if (worldMap.DungeonId >= 0 && gameDataApi is not null)
                {
                    dungeonFloorTask = gameDataApi.GetDungeonFloorAsync(worldMap.DungeonId, dungeonFloorNumber);
                }
                break;
        }
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
        "I : Inventaire   M : Montres   P : Groupe",
        "G : Guilde   B : Boutique   V : Arène classée",
        "Ou cliquez les boutons en haut à droite de l'écran.",
    ]),
    ("COMBAT",
    [
        "Choisissez une action : 1 Déplacer, 2 Attaquer,",
        "3 Passer, 4 Capturer (avec une Sphère de capture).",
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

/// <summary>
/// Reconstruit la carte du monde pour le royaume du personnage choisi (voir GDD — "plusieurs
/// villes distinctes par royaume/biome", <c>KingdomBiome</c>) : appelé une fois le royaume connu
/// (sélection d'un personnage existant ou fin de création), avant la connexion au serveur.
/// Réinitialise aussi la position locale sur le nouveau point d'apparition.
/// </summary>
void RebuildWorldMapForKingdom(KingdomType kingdom)
{
    worldMap = new WorldMap(size: 50, kingdom: kingdom);
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
        }

        Console.WriteLine($"[Réseau] Entrée dans le monde acceptée en ({packet.PositionX}, {packet.PositionY}).");
        _ = RefreshDungeonPositionAsync();
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
            chatMessages.Add(new ChatLine(packet.Channel, packet.SenderName, packet.Rank, packet.Message));
            if (chatMessages.Count > MaxChatLines)
            {
                chatMessages.RemoveAt(0);
            }
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
/// Ouvre un panneau en jeu (voir GDD — boutons Inventaire/Guilde/Boutique/Groupe/Arène/Montres) :
/// partagé entre les raccourcis clavier (I/G/B/P/V/M) et les boutons cliquables du HUD (voir
/// <see cref="DrawOutdoorHudButtons"/>) pour ne pas dupliquer la logique d'ouverture/chargement.
/// </summary>
void OpenPanel(PanelKind kind)
{
    activePanel = kind;

    switch (kind)
    {
        case PanelKind.Inventory:
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
            shopMessage = null;
            _ = LoadShopCatalogAsync();
            break;
        case PanelKind.Party:
            partyLoaded = false;
            partyJoinPromptOpen = false;
            partyJoinInput = string.Empty;
            partyMessage = null;
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
        foreach (var typed in keyboard.DrainTypedChars())
        {
            if (partyJoinInput.Length < 36 && (char.IsLetterOrDigit(typed) || typed == '-'))
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
            if (Guid.TryParse(partyJoinInput, out var partyId))
            {
                partyMessage = null;
                partyActionTask = gameDataApi!.JoinPartyAsync(options.SessionToken!, chosenCharacterId!.Value, partyId)!;
            }
            else
            {
                partyMessage = "Identifiant de groupe invalide.";
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
/// Panneau Montres (touche M) : liste des créatures possédées, niveau/XP, et un mode "donner un
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
            monsterMessage = "Impossible de donner cet objet.";
        }

        monsterGiveItemTask = null;
        monsterGiveItemMode = false;
        return;
    }

    if (monsterGiveItemTask is not null)
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
/// Panneau Tchat (touche T) : Tab bascule entre canal global et canal de guilde, Entrée envoie
/// (rien ne se passe si le champ est vide — pas de message vide sur le réseau), Échap vide le
/// champ de saisie puis ferme le panneau au second appui, comme les autres panneaux avec saisie
/// (voir <see cref="UpdateGuildPanel"/>). La liste des joueurs en ligne (voir GDD — "avec leur
/// grade") est affichée à côté du tchat par <see cref="DrawChatPanel"/>, pas de touche dédiée.
/// </summary>
void UpdateChatPanel()
{
    if (keyboard.WasJustPressed(Key.Tab))
    {
        chatChannel = chatChannel == ChatChannel.Global ? ChatChannel.Guild : ChatChannel.Global;
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
            activePanel = PanelKind.None;
        }
    }
    else if (keyboard.WasJustPressed(Key.Enter) && chatTextInput.Trim().Length > 0)
    {
        connection?.SendChatMessage(chatTextInput.Trim(), chatChannel);
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
        shopBuyTask = null;
        return;
    }

    if (shopCatalog.Count == 0 || shopBuyTask is not null)
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
        var monsterIds = monsters.Select(m => m.Id).Take(4).ToList();

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
/// Exploration en couloir linéaire d'un étage de donjon (voir GDD — "mobs/loot au fil du
/// chemin") : avance salle par salle, un combat par salle Monstre/MiniBoss/Boss/BossLegendaire
/// (voir <see cref="StartDungeonRoomCombatAsync"/>), un coffre d'or par salle Coffre (voir
/// <c>DungeonRoomService.OpenChestAsync</c> côté serveur), du texte d'ambiance pour les autres
/// types de salle (Énigme/Piège/Marchand/Événement/Autel/Salle secrète — non simulés, voir
/// Docs/README.md). Une fois la dernière salle passée, Entrée descend à l'étage suivant.
/// </summary>
void UpdateDungeonCorridor()
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
        return;
    }

    if (dungeonChestTask is { IsCompleted: true } chestTask)
    {
        var gold = chestTask.IsFaulted ? null : chestTask.Result;
        dungeonChestTask = null;
        dungeonChestOpened = true;
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

    // Charge l'aperçu de la créature dès l'arrivée dans une salle à monstre (voir GDD/demande
    // utilisateur — "voir les ennemis avant de les combattre, comme Pokémon Épée"), avant même
    // que le joueur n'appuie sur Entrée pour engager le combat.
    if (dungeonRoomIndex < dungeonFloor.Rooms.Count)
    {
        var currentRoom = dungeonFloor.Rooms[dungeonRoomIndex];
        var isMonsterRoom = currentRoom.EncounterType is DungeonEncounterType.Monstre or DungeonEncounterType.MiniBoss
            or DungeonEncounterType.Boss or DungeonEncounterType.BossLegendaire;

        if (isMonsterRoom && dungeonEncounterPreviewRoomIndex != dungeonRoomIndex && dungeonEncounterPreviewTask is null)
        {
            dungeonEncounterPreviewRoomIndex = dungeonRoomIndex;
            dungeonEncounterPreviewTask = gameDataApi!.GetDungeonEncounterPreviewAsync(worldMap.DungeonId, dungeonFloorNumber, dungeonRoomIndex);
        }
    }

    if (keyboard.WasJustPressed(Key.Escape))
    {
        sceneMode = SceneMode.Outdoor;
        return;
    }

    if (dungeonRoomIndex >= dungeonFloor.Rooms.Count)
    {
        if (keyboard.WasJustPressed(Key.Enter))
        {
            dungeonFloorNumber++;
            dungeonRoomIndex = 0;
            dungeonFloor = null;
            dungeonChestOpened = false;
            dungeonRoomMessage = null;
            dungeonEncounterPreview = null;
            dungeonEncounterPreviewTask = null;
            dungeonEncounterPreviewRoomIndex = -1;
            dungeonFloorTask = gameDataApi!.GetDungeonFloorAsync(worldMap.DungeonId, dungeonFloorNumber);
        }

        return;
    }

    if (!keyboard.WasJustPressed(Key.Enter))
    {
        return;
    }

    var room = dungeonFloor.Rooms[dungeonRoomIndex];
    switch (room.EncounterType)
    {
        case DungeonEncounterType.Monstre:
        case DungeonEncounterType.MiniBoss:
        case DungeonEncounterType.Boss:
        case DungeonEncounterType.BossLegendaire:
            combatMessage = null;
            combatReturnScene = SceneMode.Interior;
            combatStartTask = StartDungeonRoomCombatAsync(dungeonFloorNumber, dungeonRoomIndex);
            break;

        case DungeonEncounterType.Coffre when !dungeonChestOpened:
            dungeonChestTask = gameDataApi!.OpenChestAsync(options.SessionToken!, chosenCharacterId!.Value, worldMap.DungeonId, dungeonFloorNumber, dungeonRoomIndex);
            break;

        default:
            AdvanceDungeonRoom();
            break;
    }
}

void AdvanceDungeonRoom()
{
    dungeonRoomIndex++;
    dungeonChestOpened = false;
    dungeonRoomMessage = null;
    dungeonEncounterPreview = null;
    dungeonEncounterPreviewTask = null;
    dungeonEncounterPreviewRoomIndex = -1;
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
        var monsterIds = monsters.Select(m => m.Id).Take(4).ToList();

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
        var monsterIds = monsters.Select(m => m.Id).Take(4).ToList();

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
    combatSelectedAction = null;
}

void UpdateCombat()
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
        }

        combatActionTask = null;
        return;
    }

    if (combatState is null || combatActionTask is not null)
    {
        return;
    }

    if (combatState.IsFinished)
    {
        UpdateLoot();
        return;
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
void UpdateLoot()
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
            // Victoire dans le couloir du donjon (voir GDD) : avance à la salle suivante plutôt
            // que de rejouer la même — uniquement sur victoire (team 0), une défaite laisse le
            // joueur retenter la même salle.
            if (combatReturnScene == SceneMode.Interior && interiorIsDungeon && combatState?.WinningTeam == 0)
            {
                AdvanceDungeonRoom();
            }

            sceneMode = combatReturnScene;
            combatState = null;
            combatSelectedAction = null;
            activeLoot = null;
            lootMessage = null;
            lootCursor = 0;
        }

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

    var plaqueSize = new Vector2(IsoMath.TileWidth * 0.46f, IsoMath.TileHeight * 0.42f);
    var plaquePosition = postTop - new Vector2(plaqueSize.X / 2f, plaqueSize.Y * 0.75f);
    spriteBatch.Draw(whiteTexture, plaquePosition, plaqueSize, WorldMap.SignboardColor);
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, building.Name.ToUpperInvariant(),
        plaquePosition + new Vector2(plaqueSize.X / 2f, plaqueSize.Y / 2f - 3f), 1.1f, new Vector4(0.25f, 0.17f, 0.09f, 1f));
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

void DrawPlayerFigure(Vector2 gridPos, float bobPixels)
{
    DrawFigure(
        gridPos, 0.55f,
        new Vector4(0.92f, 0.78f, 0.31f, 1f), new Vector4(0.60f, 0.48f, 0.15f, 1f), new Vector4(0.78f, 0.64f, 0.22f, 1f),
        new Vector4(0.92f, 0.80f, 0.68f, 1f), bobPixels, "Vous");
}

void DrawRemotePlayerFigure(RemotePlayer remote, float animClock)
{
    var bob = MathF.Sin(animClock * 2.0f) * 1.0f;
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
void DrawOutdoorHudButtons(int w, int h)
{
    (string Label, PanelKind Kind)[] buttons =
    [
        ("INVENTAIRE (I)", PanelKind.Inventory),
        ("MONTRES (M)", PanelKind.Monsters),
        ("GROUPE (P)", PanelKind.Party),
        ("GUILDE (G)", PanelKind.Guild),
        ("BOUTIQUE (B)", PanelKind.Shop),
        ("ARENE (V)", PanelKind.Arena),
        ("TCHAT (T)", PanelKind.Chat),
    ];

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
        var y = topLeft.Y + 56f;
        foreach (var item in inventoryItems)
        {
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{item.Name.ToUpperInvariant()} x{item.Quantity}", new Vector2(topLeft.X + 20f, y), 2f, Vector4.One);
            y += 28f;
        }
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
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

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "MONTRES", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.55f, 0.9f, 0.6f, 1f));

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
                TextRenderer.Draw(spriteBatch, whiteTexture, $"{prefix}{inventoryItems[i].Name.ToUpperInvariant()} x{inventoryItems[i].Quantity}", new Vector2(topLeft.X + 30f, y), 2f, color);
                y += 26f;
            }
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE : DONNER - ECHAP : ANNULER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
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
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{prefix}{name.ToUpperInvariant()} - NIV. {monster.Level}", new Vector2(textX, y), 2f, color);

            var xpForNextLevel = monster.Level * 100;
            var xpRatio = Math.Clamp((float)monster.Experience / Math.Max(1, xpForNextLevel), 0f, 1f);
            var barTop = new Vector2(textX, y + 24f);
            DrawPanel(barTop, new Vector2(190f, 6f), new Vector4(0.2f, 0.2f, 0.22f, 1f));
            DrawPanel(barTop, new Vector2(190f * xpRatio, 6f), new Vector4(0.4f, 0.85f, 0.5f, 1f));
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{monster.Experience}/{xpForNextLevel} XP", barTop + new Vector2(200f, -4f), 1.3f, new Vector4(0.7f, 0.7f, 0.75f, 1f));

            y += 48f;
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "D : DONNER UN OBJET A LA CREATURE SELECTIONNEE", new Vector2(w / 2f, topLeft.Y + boxHeight - 44f), 1.8f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
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
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "IDENTIFIANT DU GROUPE A REJOINDRE :", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 40f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
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
        TextRenderer.Draw(spriteBatch, whiteTexture, $"ID : {myParty.Id}", new Vector2(topLeft.X + 20f, y), 1.5f, new Vector4(0.55f, 0.55f, 0.6f, 1f));
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

    var messagesTop = topLeft.Y + 80f;
    var messagesBottom = topLeft.Y + boxHeight - 60f;
    List<ChatLine> visible;
    lock (stateLock)
    {
        visible = chatMessages.Where(m => m.Channel == chatChannel).TakeLast(12).ToList();
    }

    var y = messagesBottom - 20f;
    for (var i = visible.Count - 1; i >= 0; i--)
    {
        var line = visible[i];
        var text = $"{ChatRankTag(line.Rank)}{line.SenderName} : {line.Message}";
        TextRenderer.Draw(spriteBatch, whiteTexture, text, new Vector2(topLeft.X + 20f, y), 1.6f, Vector4.One);
        y -= 20f;
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
    TextRenderer.Draw(spriteBatch, whiteTexture, $"{ChatRankTag(myRank)}Vous", new Vector2(listLeft, listY), 1.5f, new Vector4(0.95f, 0.8f, 0.4f, 1f));
    listY += 20f;

    foreach (var (_, remote) in others.OrderBy(kv => kv.Value.Name))
    {
        if (listY > topLeft.Y + boxHeight - 30f)
        {
            break;
        }

        TextRenderer.Draw(spriteBatch, whiteTexture, $"{ChatRankTag(remote.Rank)}{remote.Name}", new Vector2(listLeft, listY), 1.5f, Vector4.One);
        listY += 20f;
    }
}

static string ChatRankTag(UserRank rank) => rank switch
{
    UserRank.Veteran => "[Vet] ",
    UserRank.Moderateur => "[Mod] ",
    UserRank.Administrateur => "[Admin] ",
    _ => "",
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

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "BOUTIQUE", new Vector2(w / 2f, topLeft.Y + 24f), 2.8f, new Vector4(0.95f, 0.8f, 0.4f, 1f));

    if (shopCatalog.Count == 0)
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
            TextRenderer.Draw(spriteBatch, whiteTexture, $"{prefix}{item.Name.ToUpperInvariant()} - {item.Price} OR", new Vector2(topLeft.X + 20f, y), 2f, color);
            y += 28f;
        }
    }

    if (shopMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, shopMessage, new Vector2(w / 2f, topLeft.Y + boxHeight - 50f), 1.8f, new Vector4(0.6f, 0.9f, 0.6f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "HAUT/BAS : CHOISIR - ENTREE : ACHETER - ECHAP : FERMER",
        new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
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

/// <summary>Rendu du couloir de donjon (voir <see cref="UpdateDungeonCorridor"/>) : une rangée de cases représentant les salles de l'étage, la case courante mise en évidence.</summary>
void DrawDungeonCorridor(int w, int h)
{
    TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"ETAGE {dungeonFloorNumber}", new Vector2(w / 2f, h * 0.26f), 2.4f, new Vector4(0.85f, 0.7f, 0.95f, 1f));

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

    const float cellSize = 64f;
    var totalWidth = dungeonFloor.Rooms.Count * (cellSize + 12f) - 12f;
    var originX = w / 2f - totalWidth / 2f;
    var y = h * 0.42f;

    for (var i = 0; i < dungeonFloor.Rooms.Count; i++)
    {
        var room = dungeonFloor.Rooms[i];
        var center = new Vector2(originX + i * (cellSize + 12f) + cellSize / 2f, y);
        var color = DungeonRoomColor(room.EncounterType);

        if (i == dungeonRoomIndex)
        {
            DrawPanel(center - new Vector2(cellSize / 2f + 4f, cellSize / 2f + 4f), new Vector2(cellSize + 8f, cellSize + 8f), new Vector4(1f, 0.9f, 0.5f, 0.9f));
        }
        else if (i < dungeonRoomIndex)
        {
            color *= 0.5f;
            color.W = 1f;
        }

        DrawPanel(center - new Vector2(cellSize / 2f, cellSize / 2f), new Vector2(cellSize, cellSize), color);
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, DungeonRoomLabel(room.EncounterType), center + new Vector2(0, cellSize / 2f + 16f), 1.3f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
    }

    if (dungeonRoomIndex >= dungeonFloor.Rooms.Count)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ETAGE TERMINE !", new Vector2(w / 2f, h * 0.68f), 3f, new Vector4(0.5f, 0.9f, 0.5f, 1f));
        DrawPromptBanner("APPUYEZ SUR [ENTREE] POUR DESCENDRE", new Vector2(w / 2f, h * 0.82f));
    }
    else
    {
        var room = dungeonFloor.Rooms[dungeonRoomIndex];
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, DungeonRoomFlavor(room.EncounterType, dungeonChestOpened), new Vector2(w / 2f, h * 0.62f), 2.1f, new Vector4(0.92f, 0.92f, 0.95f, 1f));

        // Voir GDD/demande utilisateur — "voir les ennemis avant de les combattre, comme Pokémon
        // Épée" : portrait + nom + élément affichés avant même d'engager le combat, dès que
        // l'aperçu (même tirage exact que le combat réel, voir GetDungeonEncounterPreviewAsync)
        // est chargé pour CETTE salle précise.
        if (dungeonEncounterPreview is { } preview && dungeonEncounterPreviewRoomIndex == dungeonRoomIndex)
        {
            var previewCenter = new Vector2(w / 2f, h * 0.5f);
            DrawStarterPortrait(previewCenter, 34f, new Vector4(0.95f, 0.25f, 0.25f, 1f));
            DrawStarterPortrait(previewCenter, 30f, CombatTypeColor(preview.Type));
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"{preview.Name.ToUpperInvariant()} ({preview.Element})",
                previewCenter + new Vector2(0, 46f), 1.6f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        }

        if (combatStartTask is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, "...", new Vector2(w / 2f, h * 0.82f), 2.1f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
        }
        else
        {
            DrawPromptBanner(DungeonRoomPrompt(room.EncounterType, dungeonChestOpened), new Vector2(w / 2f, h * 0.82f));
        }
    }

    if (combatStartTask is null)
    {
        // Voir GDD/demande utilisateur — "ajoute une touche pour quitter le donjon hors des
        // combats" : Échap le fait déjà (voir UpdateDungeonCorridor) mais ce n'était affiché
        // nulle part hors de l'écran d'erreur — juste un rappel manquant, pas une touche à ajouter.
        TextRenderer.Draw(spriteBatch, whiteTexture, "ECHAP : QUITTER LE DONJON", new Vector2(16f, h - 30f), 1.5f, new Vector4(0.65f, 0.65f, 0.7f, 1f));
    }

    if (dungeonRoomMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, dungeonRoomMessage, new Vector2(w / 2f, h * 0.85f), 2f, new Vector4(0.9f, 0.8f, 0.4f, 1f));
    }

    if (combatMessage is not null)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatMessage, new Vector2(w / 2f, h * 0.85f), 2f, new Vector4(0.9f, 0.4f, 0.4f, 1f));
    }

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR SORTIR DU DONJON", new Vector2(w / 2f, h * 0.92f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
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

static string DungeonRoomLabel(DungeonEncounterType type) => type switch
{
    DungeonEncounterType.Monstre => "MONSTRE",
    DungeonEncounterType.MiniBoss => "MINI-BOSS",
    DungeonEncounterType.Boss => "BOSS",
    DungeonEncounterType.BossLegendaire => "BOSS LEG.",
    DungeonEncounterType.Coffre => "COFFRE",
    DungeonEncounterType.Marchand => "MARCHAND",
    DungeonEncounterType.Piege => "PIEGE",
    DungeonEncounterType.Enigme => "ENIGME",
    DungeonEncounterType.Autel => "AUTEL",
    DungeonEncounterType.SalleSecrete => "SECRET",
    _ => "EVENEMENT",
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

static string DungeonRoomPrompt(DungeonEncounterType type, bool chestOpened) => type switch
{
    DungeonEncounterType.Monstre or DungeonEncounterType.MiniBoss or DungeonEncounterType.Boss or DungeonEncounterType.BossLegendaire
        => "APPUYEZ SUR ENTREE POUR AFFRONTER",
    DungeonEncounterType.Coffre when !chestOpened => "APPUYEZ SUR ENTREE POUR OUVRIR LE COFFRE",
    _ => "APPUYEZ SUR ENTREE POUR CONTINUER",
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

        var myTurn = combatState.CurrentTurnCombatantId is { } currentId
            && combatState.Combatants.FirstOrDefault(c => c.Id == currentId) is { Team: 0 };

        if (myTurn)
        {
            if (combatSelectedAction is null)
            {
                var current = combatState.Combatants.First(c => c.Id == combatState.CurrentTurnCombatantId);
                var isImmediateAbility = current.Type == MonsterType.Soigneur;

                // Boutons cliquables (voir retour utilisateur — "on doit pouvoir cliquer pour
                // faire les actions") en plus des raccourcis clavier 1-6, toujours actifs.
                List<(string Label, CombatActionType Action)> actionButtons =
                [
                    ("1:DEPLACER", CombatActionType.Move),
                    ("2:ATTAQUER", CombatActionType.Attack),
                    ("3:PASSER", CombatActionType.Pass),
                    ("4:CAPACITE", CombatActionType.SpecialAbility),
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

        var textColor = isSelected ? new Vector4(0.98f, 0.9f, 0.5f, 1f) : Vector4.One;
        TextRenderer.Draw(spriteBatch, whiteTexture, loot.Items[i].Name.ToUpperInvariant(), rowTopLeft + new Vector2(16f, 8f), 1.9f, textColor);

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
        var label = wonByMe ? $"VOUS REMPORTEZ : {item.Name.ToUpperInvariant()}" : $"{item.Name.ToUpperInvariant()} : ATTRIBUE";
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, label, new Vector2(w / 2f, y), 1.9f, wonByMe ? new Vector4(0.5f, 0.9f, 0.5f, 1f) : new Vector4(0.75f, 0.75f, 0.8f, 1f));
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

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "FLECHES POUR CHOISIR - ENTREE POUR VALIDER", new Vector2(w / 2f, h - 40f), 2.2f, new Vector4(0.75f, 0.75f, 0.8f, 1f));
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
record ChatLine(ChatChannel Channel, string SenderName, UserRank Rank, string Message);

enum StarterStage
{
    Introduction,
    Choosing,
    Confirming,
    Sending,
}

sealed record NearbyInteraction(InteractionKind Kind, string Label, Building? Building, Npc? Npc);
