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
    $"entrée de donjon « {worldMap.DungeonName} » en ({worldMap.DungeonEntrance.X}, {worldMap.DungeonEntrance.Y}).");

var stateLock = new object();
var gridPosition = new Vector2(worldMap.SpawnPosition.X, worldMap.SpawnPosition.Y);
var statusMessage = string.Empty;
var dungeonPromptShown = false;

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

        Console.WriteLine($"[Réseau] Position confirmée : ({packet.PositionX}, {packet.PositionY}).");
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
Texture2D playerTexture = null!;
KeyboardState keyboard = null!;
var camera = new Camera2D { ViewportWidth = 1280, ViewportHeight = 720, Zoom = 1.4f };

host.Load += () =>
{
    spriteBatch = new SpriteBatch(host.Gl);
    whiteTexture = Texture2D.CreateSolidColor(host.Gl, 255, 255, 255);
    playerTexture = Texture2D.CreateSolidColor(host.Gl, 235, 200, 80);
    keyboard = new KeyboardState(host.Input);

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

    if (connection is null)
    {
        // Mode démo : déplacement libre continu, exprimé en cases par seconde.
        var direction = Vector2.Zero;
        if (keyboard.IsDown(Key.W) || keyboard.IsDown(Key.Up)) direction.Y -= 1;
        if (keyboard.IsDown(Key.S) || keyboard.IsDown(Key.Down)) direction.Y += 1;
        if (keyboard.IsDown(Key.A) || keyboard.IsDown(Key.Left)) direction.X -= 1;
        if (keyboard.IsDown(Key.D) || keyboard.IsDown(Key.Right)) direction.X += 1;

        if (direction != Vector2.Zero)
        {
            lock (stateLock)
            {
                var next = gridPosition + (Vector2.Normalize(direction) * 4.5f * deltaTime);
                gridPosition = new Vector2(
                    Math.Clamp(next.X, 0, worldMap.Size - 1),
                    Math.Clamp(next.Y, 0, worldMap.Size - 1));
            }
        }
    }
    else
    {
        // Mode connecté : déplacement case par case, confirmé par le serveur (autoritaire).
        var (dx, dy) = (0, 0);
        if (keyboard.WasJustPressed(Key.W) || keyboard.WasJustPressed(Key.Up)) dy = -1;
        else if (keyboard.WasJustPressed(Key.S) || keyboard.WasJustPressed(Key.Down)) dy = 1;
        else if (keyboard.WasJustPressed(Key.A) || keyboard.WasJustPressed(Key.Left)) dx = -1;
        else if (keyboard.WasJustPressed(Key.D) || keyboard.WasJustPressed(Key.Right)) dx = 1;

        if (dx != 0 || dy != 0)
        {
            Vector2 current;
            lock (stateLock)
            {
                current = gridPosition;
            }

            var targetX = Math.Clamp((int)current.X + dx, 0, worldMap.Size - 1);
            var targetY = Math.Clamp((int)current.Y + dy, 0, worldMap.Size - 1);
            connection.SendMove(targetX, targetY);
        }
    }

    Vector2 positionForDungeonCheck;
    lock (stateLock)
    {
        positionForDungeonCheck = gridPosition;
    }

    var distanceToDungeon = Vector2.Distance(
        positionForDungeonCheck, new Vector2(worldMap.DungeonEntrance.X, worldMap.DungeonEntrance.Y));

    if (distanceToDungeon < 1.5f && !dungeonPromptShown)
    {
        dungeonPromptShown = true;
        Console.WriteLine($"[Monde] Vous approchez de l'entrée du « {worldMap.DungeonName} ». " +
            "(L'entrée réelle en donjon — génération d'étage + combat — existe déjà côté serveur, " +
            "voir POST /api/dungeons/{id}/floors/{n}/rooms/{i}/engage ; le fil visuel client->serveur " +
            "pour cette transition reste à faire.)");
    }
    else if (distanceToDungeon >= 1.5f)
    {
        dungeonPromptShown = false;
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
            DrawIsoTile(x, y, worldMap.TileColors[x, y]);
        }
    }

    DrawIsoTile(worldMap.DungeonEntrance.X, worldMap.DungeonEntrance.Y, worldMap.GetDungeonMarkerColor());

    // Bâtiments (en hauteur) et joueur : triés par profondeur pour une occlusion correcte.
    var depthJobs = new List<(float Depth, Action Draw)>(worldMap.Buildings.Count + 1)
    {
        (currentGridPosition.X + currentGridPosition.Y + 0.5f, () => DrawPlayerMarker(currentGridPosition)),
    };

    foreach (var building in worldMap.Buildings)
    {
        depthJobs.Add((building.GridX + building.GridY, () => DrawBuilding(building)));
    }

    foreach (var job in depthJobs.OrderBy(j => j.Depth))
    {
        job.Draw();
    }

    spriteBatch.End();
};

host.Run();

connection?.Dispose();

void DrawIsoTile(float gridX, float gridY, Vector4 color)
{
    var center = IsoMath.GridToIso(gridX, gridY);
    var top = center + new Vector2(0, -IsoMath.TileHeight / 2f);
    var right = center + new Vector2(IsoMath.TileWidth / 2f, 0);
    var bottom = center + new Vector2(0, IsoMath.TileHeight / 2f);
    var left = center + new Vector2(-IsoMath.TileWidth / 2f, 0);

    // Léger liseré : une case très légèrement rétractée dessinée par-dessus une case pleine
    // donnerait un effet de grille sans texture dédiée ; ici on garde simple (pas de liseré)
    // pour limiter le nombre de quads sur une carte de 2500 cases.
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
}

void DrawPlayerMarker(Vector2 gridPos)
{
    const float markerHeight = 0.5f;
    var groundCenter = IsoMath.GridToIso(gridPos.X, gridPos.Y);
    var topCenter = groundCenter - new Vector2(0, markerHeight * IsoMath.TileHeight);

    const float halfWidth = IsoMath.TileWidth * 0.28f;
    const float halfHeight = IsoMath.TileHeight * 0.28f;

    var top = topCenter + new Vector2(0, -halfHeight);
    var right = topCenter + new Vector2(halfWidth, 0);
    var bottom = topCenter + new Vector2(0, halfHeight);
    var left = topCenter + new Vector2(-halfWidth, 0);

    spriteBatch.DrawQuad(whiteTexture, top, right, bottom, left, new Vector4(0.92f, 0.78f, 0.31f, 1f));
}
