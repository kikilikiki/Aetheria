using System.Numerics;
using System.Text;
using Aetheria.Client;
using Aetheria.Client.Networking;
using Aetheria.Client.World;
using Aetheria.Engine.Core;
using Aetheria.Engine.Input;
using Aetheria.Engine.Rendering;
using Aetheria.Shared;
using Silk.NET.Input;
using Silk.NET.OpenGL;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"{GameInfo.Name} Client v{GameInfo.Version}");

var options = LaunchOptions.Parse(args);

var worldMap = new WorldMap(size: 50);
Console.WriteLine($"Monde généré : {worldMap.Size}x{worldMap.Size} cases, {worldMap.Buildings.Count} bâtiments, " +
    $"{worldMap.Npcs.Count} PNJ, entrée de donjon « {worldMap.DungeonName} » en " +
    $"({worldMap.DungeonEntrance.X}, {worldMap.DungeonEntrance.Y}). Clic gauche pour se déplacer.");

var stateLock = new object();
var gridPosition = new Vector2(worldMap.SpawnPosition.X, worldMap.SpawnPosition.Y);
var statusMessage = string.Empty;

var moveQueue = new Queue<(int X, int Y)>();
var isAwaitingServerStep = false;
var interactionShown = new Dictionary<string, bool>();
var animationClock = 0f;
var isPlayerMoving = false;

GameConnection? connection = null;

if (options.SessionToken is not null && options.CharacterId is not null)
{
    Console.WriteLine($"Mode connecté : {options.Host}:{options.Port}, personnage {options.CharacterId}.");

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
        connection.RequestEnterWorld(options.SessionToken, options.CharacterId.Value);
    }
    catch (Exception ex) when (ex is System.Net.Sockets.SocketException or IOException)
    {
        statusMessage = $"Impossible de se connecter au serveur : {ex.Message}";
        connection = null;
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
};

host.Update += deltaTime =>
{
    keyboard.Update();
    mouse.Update();
    animationClock += deltaTime;

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

    // Interactions de proximité : bâtiments et portail de donjon (voir Docs/README.md pour les
    // limites assumées — pas de scène d'intérieur, seulement un message clair).
    foreach (var building in worldMap.Buildings)
    {
        var key = $"building:{building.Name}";
        var distance = Vector2.Distance(positionAfterInput, new Vector2(building.GridX, building.GridY));
        if (distance < 1.6f)
        {
            if (!interactionShown.GetValueOrDefault(key))
            {
                interactionShown[key] = true;
                Console.WriteLine($"[Monde] Vous entrez dans « {building.Name} ». " +
                    "(Pas encore de scène d'intérieur — voir Docs/README.md.)");
            }
        }
        else
        {
            interactionShown[key] = false;
        }
    }

    var dungeonDistance = Vector2.Distance(
        positionAfterInput, new Vector2(worldMap.DungeonEntrance.X, worldMap.DungeonEntrance.Y));

    if (dungeonDistance < 1.6f)
    {
        if (!interactionShown.GetValueOrDefault("dungeon"))
        {
            interactionShown["dungeon"] = true;
            Console.WriteLine($"[Monde] Vous franchissez le portail du « {worldMap.DungeonName} »... " +
                "(le donjon lui-même — génération d'étage + combat — existe déjà côté serveur, voir " +
                "POST /api/dungeons/{id}/floors/{n}/rooms/{i}/engage ; la scène visuelle d'intérieur reste à faire.)");
        }
    }
    else
    {
        interactionShown["dungeon"] = false;
    }
};

host.Render += _ =>
{
    host.Gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);
    host.Gl.Clear(ClearBufferMask.ColorBufferBit);

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
};

host.Run();

connection?.Dispose();

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

    // Enseigne : un poteau planté devant l'entrée (côté "sud", vers la caméra) et une plaque.
    var postWidth = 3f;
    var postHeight = IsoMath.TileHeight * 0.55f;
    var postBase = groundCenter + new Vector2(0, IsoMath.TileHeight * 0.6f);
    var postTop = postBase - new Vector2(0, postHeight);
    spriteBatch.Draw(whiteTexture, new Vector2(postBase.X - postWidth / 2f, postTop.Y), new Vector2(postWidth, postHeight), WorldMap.SignpostColor);

    var plaqueSize = new Vector2(IsoMath.TileWidth * 0.26f, IsoMath.TileHeight * 0.32f);
    var plaquePosition = postTop - new Vector2(plaqueSize.X / 2f, plaqueSize.Y * 0.7f);
    spriteBatch.Draw(whiteTexture, plaquePosition, plaqueSize, WorldMap.SignboardColor);
}

void DrawPortal(Vector2 gridPos, float animClock)
{
    var pulse = (MathF.Sin(animClock * 2f) + 1f) / 2f;

    DrawIsoDiamond(gridPos, 1.3f, WorldMap.PortalOuterColor);
    DrawIsoDiamond(gridPos, 0.88f, Vector4.Lerp(WorldMap.PortalMidColorDark, WorldMap.PortalMidColorBright, pulse));
    DrawIsoDiamond(gridPos, 0.46f, Vector4.Lerp(WorldMap.PortalMidColorBright, WorldMap.PortalCoreColor, pulse));
}

void DrawFigure(Vector2 gridPos, float bodyHeight, Vector4 roofColor, Vector4 wallLeftColor, Vector4 wallRightColor, Vector4 headColor, float bobPixels)
{
    const float footprint = 0.34f;

    var groundCenter = IsoMath.GridToIso(gridPos.X, gridPos.Y) - new Vector2(0, bobPixels);
    var halfWidth = IsoMath.TileWidth * footprint / 2f;
    var halfHeight = IsoMath.TileHeight * footprint / 2f;

    var bodyTopCenter = groundCenter - new Vector2(0, bodyHeight * IsoMath.TileHeight);

    var bodyTop = bodyTopCenter + new Vector2(0, -halfHeight);
    var bodyRight = bodyTopCenter + new Vector2(halfWidth, 0);
    var bodyBottom = bodyTopCenter + new Vector2(0, halfHeight);
    var bodyLeft = bodyTopCenter + new Vector2(-halfWidth, 0);

    var groundLeft = groundCenter + new Vector2(-halfWidth, 0);
    var groundBottom = groundCenter + new Vector2(0, halfHeight);
    var groundRight = groundCenter + new Vector2(halfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, bodyLeft, bodyBottom, groundBottom, groundLeft, wallLeftColor);
    spriteBatch.DrawQuad(whiteTexture, bodyBottom, bodyRight, groundRight, groundBottom, wallRightColor);
    spriteBatch.DrawQuad(whiteTexture, bodyTop, bodyRight, bodyBottom, bodyLeft, roofColor);

    // Tête : petit losange posé sur le corps, pour une silhouette reconnaissable sans sprite.
    var headCenter = bodyTopCenter - new Vector2(0, halfHeight * 0.85f);
    var headHalfWidth = halfWidth * 0.55f;
    var headHalfHeight = halfHeight * 0.55f;

    var headTop = headCenter + new Vector2(0, -headHalfHeight);
    var headRight = headCenter + new Vector2(headHalfWidth, 0);
    var headBottom = headCenter + new Vector2(0, headHalfHeight);
    var headLeft = headCenter + new Vector2(-headHalfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, headTop, headRight, headBottom, headLeft, headColor);
}

void DrawPlayerFigure(Vector2 gridPos, float bobPixels)
{
    DrawFigure(
        gridPos, 0.55f,
        new Vector4(0.92f, 0.78f, 0.31f, 1f), new Vector4(0.60f, 0.48f, 0.15f, 1f), new Vector4(0.78f, 0.64f, 0.22f, 1f),
        new Vector4(0.92f, 0.80f, 0.68f, 1f), bobPixels);
}

void DrawNpcFigure(Npc npc, float animClock)
{
    var bob = MathF.Sin((animClock + npc.AnimationOffset) * 2.2f) * 1.0f;
    DrawFigure(
        new Vector2(npc.GridX, npc.GridY), 0.5f,
        npc.BodyColor, npc.BodyColor * 0.65f, npc.BodyColor * 0.85f,
        npc.HeadColor, bob);
}
