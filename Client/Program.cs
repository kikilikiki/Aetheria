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

var stateLock = new object();
var gridPosition = new Vector2(worldMap.SpawnPosition.X, worldMap.SpawnPosition.Y);
var statusMessage = string.Empty;

var moveQueue = new Queue<(int X, int Y)>();
var isAwaitingServerStep = false;
var animationClock = 0f;
var isPlayerMoving = false;

// Scène active : le monde extérieur, une scène d'intérieur plein écran (bâtiment/donjon), ou la
// sélection du premier compagnon. Voir Docs/README.md pour les limites assumées de chacune.
var sceneMode = SceneMode.Outdoor;
NearbyInteraction? nearbyInteraction = null;

// Intérieur (bâtiment ou donjon) affiché quand sceneMode == Interior — pas de vraie scène 3D/2D
// détaillée, un fond stylisé + un texte, voir DrawInteriorScene.
var interiorTitle = string.Empty;
var interiorBodyLines = Array.Empty<string>();
var interiorAccent = new Vector4(0.5f, 0.5f, 0.5f, 1f);
var interiorIsDungeon = false;

// Combat tactique (voir Server/World/CombatService.cs) : déclenché depuis l'intérieur du
// donjon (Entrée pour affronter un monstre sauvage) — voir GDD section Combats. Grille 7x7,
// actions Move/Attack/Pass/Capture, IA gérée côté serveur entre deux tours joueur.
CombatApiClient? combatApi = null;
CombatSessionState? combatState = null;
var combatCursorX = 0;
var combatCursorY = 0;
CombatActionType? combatSelectedAction = null;
string? combatMessage = null;
int? captureSphereItemId = null;
Task<CombatResult>? combatStartTask = null;
Task<CombatResult>? combatActionTask = null;

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

// Panneaux en jeu (voir GDD — boutons Inventaire/Guilde/Boutique) : superposés au monde
// extérieur comme le dialogue PNJ, ouverts via I/G/B, fermés via Échap.
var activePanel = PanelKind.None;
List<InventoryItemSummary> inventoryItems = [];
GuildSummary? myGuild = null;
var guildLoaded = false;
List<ShopItem> shopCatalog = [];
var shopCursor = 0;
string? shopMessage = null;
Task<ShopPurchaseResponse>? shopBuyTask = null;

// Sélection/création de personnage (voir GDD) : ne se fait plus dans le Launcher, mais en jeu,
// avant la connexion TCP proprement dite. `--characterId` reste accepté pour compatibilité
// (anciens raccourcis) : dans ce cas on saute directement l'écran de sélection.
Guid? chosenCharacterId = options.CharacterId;
List<CharacterSummary> myCharacters = [];
var characterCursor = 0;

var isConnectedMode = options.SessionToken is not null;

if (isConnectedMode)
{
    starterApi = new StarterApiClient(options.Host);
    characterApi = new CharacterApiClient(options.Host);
    gameDataApi = new GameDataApiClient(options.Host);
    combatApi = new CombatApiClient(options.Host);

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

    if (sceneMode == SceneMode.Interior)
    {
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
        else if (keyboard.WasJustPressed(Key.Escape))
        {
            sceneMode = SceneMode.Outdoor;
        }
        else if (interiorIsDungeon && keyboard.WasJustPressed(Key.Enter) && combatStartTask is null)
        {
            combatMessage = null;
            combatStartTask = StartWildCombatAsync();
        }

        return;
    }

    if (sceneMode == SceneMode.Combat)
    {
        UpdateCombat();
        return;
    }

    // À partir d'ici, sceneMode == SceneMode.Outdoor.
    if (activeDialogueNpc is not null)
    {
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

        return; // Le monde se fige pendant un dialogue, comme dans un RPG classique.
    }

    if (activePanel != PanelKind.None)
    {
        UpdatePanel();
        return;
    }

    if (keyboard.WasJustPressed(Key.I))
    {
        activePanel = PanelKind.Inventory;
        _ = LoadInventoryAsync();
    }
    else if (keyboard.WasJustPressed(Key.G))
    {
        activePanel = PanelKind.Guild;
        guildLoaded = false;
        _ = LoadGuildAsync();
    }
    else if (keyboard.WasJustPressed(Key.B))
    {
        activePanel = PanelKind.Shop;
        shopCursor = 0;
        shopMessage = null;
        _ = LoadShopCatalogAsync();
    }

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
                break;
            case InteractionKind.Dungeon:
                sceneMode = SceneMode.Interior;
                interiorTitle = worldMap.DungeonName;
                interiorBodyLines = DungeonFlavor();
                interiorAccent = WorldMap.PortalMidColorBright;
                interiorIsDungeon = true;
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

void ConnectAndEnterWorld(Guid characterId)
{
    Console.WriteLine($"Mode connecté : {options.Host}:{options.Port}, personnage {characterId}.");

    connection = new GameConnection();
    connection.EnterWorldAccepted += packet =>
    {
        lock (stateLock)
        {
            gridPosition = new Vector2(packet.PositionX, packet.PositionY);
            statusMessage = "Connecté au monde.";
        }

        Console.WriteLine($"[Réseau] Entrée dans le monde acceptée en ({packet.PositionX}, {packet.PositionY}).");
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
        lock (stateLock)
        {
            gridPosition = new Vector2(packet.PositionX, packet.PositionY);
        }

        // Marche automatique : si un chemin cliqué est en cours, on enchaîne la prochaine case.
        if (moveQueue.Count > 0)
        {
            var next = moveQueue.Dequeue();
            connection.SendMove(next.X, next.Y);
        }
        else
        {
            isAwaitingServerStep = false;
        }
    };
    connection.Disconnected += () =>
    {
        Console.WriteLine("[Réseau] Déconnecté du serveur.");
        lock (stateLock)
        {
            statusMessage = "Déconnecté du serveur.";
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

void UpdatePanel()
{
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
        if (keyboard.WasJustPressed(Key.Enter) || keyboard.WasJustPressed(Key.Escape))
        {
            sceneMode = SceneMode.Interior;
            combatState = null;
            combatSelectedAction = null;
        }

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
        else if (keyboard.WasJustPressed(Key.Number4) && captureSphereItemId is not null)
        {
            combatSelectedAction = CombatActionType.Capture;
            combatCursorX = current.PositionX;
            combatCursorY = current.PositionY;
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
    TextRenderer.Draw(spriteBatch, whiteTexture, $"{moveKeysLabel} : SE DEPLACER - F9 : CLAVIER - I : INVENTAIRE - G : GUILDE - B : BOUTIQUE",
        new Vector2(12, h - 26f), 1.6f, new Vector4(0.7f, 0.7f, 0.75f, 0.85f));
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
    const float boxHeight = 320f;
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
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "VOUS N'APPARTENEZ A", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f - 20f), 2.2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "AUCUNE GUILDE", new Vector2(w / 2f, topLeft.Y + boxHeight / 2f + 10f), 2.2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
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

    TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ECHAP POUR FERMER", new Vector2(w / 2f, topLeft.Y + boxHeight - 20f), 1.8f, new Vector4(0.7f, 0.7f, 0.75f, 1f));
}

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

    var lineY = h * 0.34f;
    foreach (var line in interiorBodyLines)
    {
        TextRenderer.DrawCentered(spriteBatch, whiteTexture, line, new Vector2(w / 2f, lineY), 2.6f, new Vector4(0.92f, 0.92f, 0.95f, 1f));
        lineY += TextRenderer.LineHeight(2.6f) + 6f;
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

    const float cellSize = 56f;
    var gridWidth = combatState.GridWidth * cellSize;
    var gridHeight = combatState.GridHeight * cellSize;
    var originX = w / 2f - gridWidth / 2f;
    var originY = h / 2f - gridHeight / 2f - 30f;

    for (var y = 0; y < combatState.GridHeight; y++)
    {
        for (var x = 0; x < combatState.GridWidth; x++)
        {
            var cellColor = (x + y) % 2 == 0 ? new Vector4(0.14f, 0.15f, 0.19f, 1f) : new Vector4(0.11f, 0.12f, 0.16f, 1f);
            DrawPanel(new Vector2(originX + x * cellSize + 1, originY + y * cellSize + 1), new Vector2(cellSize - 2, cellSize - 2), cellColor);
        }
    }

    if (combatSelectedAction is not null)
    {
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
        var color = combatant.Team == 0 ? new Vector4(0.35f, 0.55f, 0.85f, 1f) : new Vector4(0.8f, 0.3f, 0.3f, 1f);

        if (combatant.Id == combatState.CurrentTurnCombatantId)
        {
            DrawPanel(center - new Vector2(cellSize / 2f - 2, cellSize / 2f - 2), new Vector2(cellSize - 4, cellSize - 4), new Vector4(1f, 1f, 1f, 0.15f));
        }

        DrawStarterPortrait(center, cellSize * 0.32f, color);

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

        if (combatState.LastMessage is not null)
        {
            TextRenderer.DrawCentered(spriteBatch, whiteTexture, combatState.LastMessage, new Vector2(w / 2f, h - 80f), 2f, new Vector4(0.85f, 0.85f, 0.9f, 1f));
        }

        TextRenderer.DrawCentered(spriteBatch, whiteTexture, "ENTREE POUR CONTINUER", new Vector2(w / 2f, h - 40f), 2.2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
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
                var options4 = captureSphereItemId is not null ? "  4:CAPTURER" : "";
                TextRenderer.DrawCentered(spriteBatch, whiteTexture, $"1:DEPLACER  2:ATTAQUER  3:PASSER{options4}", new Vector2(w / 2f, h - 70f), 2f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
            }
            else
            {
                TextRenderer.DrawCentered(spriteBatch, whiteTexture,
                    $"{combatSelectedAction.ToString()!.ToUpperInvariant()} - FLECHES : VISER - ENTREE : VALIDER - ECHAP : ANNULER",
                    new Vector2(w / 2f, h - 70f), 1.9f, new Vector4(0.9f, 0.75f, 0.35f, 1f));
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
}

enum StarterStage
{
    Introduction,
    Choosing,
    Confirming,
    Sending,
}

sealed record NearbyInteraction(InteractionKind Kind, string Label, Building? Building, Npc? Npc);
