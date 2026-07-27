using System.Numerics;
using System.Text;
using Aetheria.Engine.Core;
using Aetheria.Engine.Input;
using Aetheria.Engine.Rendering;
using Aetheria.Shared;
using Silk.NET.Input;
using Silk.NET.OpenGL;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"{GameInfo.Name} Client v{GameInfo.Version}");

using var host = new GameHost($"{GameInfo.Name} — v{GameInfo.Version}", 1280, 720);

SpriteBatch spriteBatch = null!;
Texture2D playerTexture = null!;
Texture2D tileTexture = null!;
KeyboardState keyboard = null!;
var camera = new Camera2D { ViewportWidth = 1280, ViewportHeight = 720 };

const float TileSize = 48f;
const int GridSize = 8;
var playerPosition = new Vector2(GridSize / 2 * TileSize, GridSize / 2 * TileSize);
const float MoveSpeed = 220f;

host.Load += () =>
{
    spriteBatch = new SpriteBatch(host.Gl);
    playerTexture = Texture2D.CreateSolidColor(host.Gl, 235, 200, 80);
    tileTexture = Texture2D.CreateSolidColor(host.Gl, 40, 42, 54);
    keyboard = new KeyboardState(host.Input);

    host.Gl.Enable(EnableCap.Blend);
    host.Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

    Console.WriteLine("Moteur initialisé : fenêtre, OpenGL, sprite batch et input prêts.");
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

    var direction = Vector2.Zero;
    if (keyboard.IsDown(Key.W) || keyboard.IsDown(Key.Up)) direction.Y -= 1;
    if (keyboard.IsDown(Key.S) || keyboard.IsDown(Key.Down)) direction.Y += 1;
    if (keyboard.IsDown(Key.A) || keyboard.IsDown(Key.Left)) direction.X -= 1;
    if (keyboard.IsDown(Key.D) || keyboard.IsDown(Key.Right)) direction.X += 1;

    if (direction != Vector2.Zero)
    {
        playerPosition += Vector2.Normalize(direction) * MoveSpeed * deltaTime;
    }

    camera.Position = playerPosition;
};

host.Render += _ =>
{
    host.Gl.ClearColor(0.06f, 0.06f, 0.09f, 1.0f);
    host.Gl.Clear(ClearBufferMask.ColorBufferBit);

    spriteBatch.Begin(camera);

    // Grille de tuiles (aperçu du futur combat tactique — voir Docs/GameDesign.md).
    for (var y = 0; y < GridSize; y++)
    {
        for (var x = 0; x < GridSize; x++)
        {
            var color = (x + y) % 2 == 0 ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.85f, 0.85f, 0.85f, 1f);
            spriteBatch.Draw(tileTexture, new Vector2(x * TileSize, y * TileSize), new Vector2(TileSize - 2, TileSize - 2), color);
        }
    }

    spriteBatch.Draw(playerTexture, playerPosition - new Vector2(TileSize / 4, TileSize / 4),
        new Vector2(TileSize / 2, TileSize / 2), Vector4.One);

    spriteBatch.End();
};

host.Run();
