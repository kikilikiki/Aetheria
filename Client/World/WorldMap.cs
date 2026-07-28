using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Génère une carte de démonstration : terrain varié (herbe, chemins, étang), une capitale et
/// ses bâtiments (voir <c>Docs/GameDesign.md</c> — exemple du Royaume du Nord), et l'entrée
/// d'un donjon de test. Calculé une seule fois au chargement, pas à chaque frame.
/// </summary>
public sealed class WorldMap
{
    public int Size { get; }
    public Vector4[,] TileColors { get; }
    public IReadOnlyList<Building> Buildings { get; }
    public (int X, int Y) SpawnPosition { get; }
    public (int X, int Y) DungeonEntrance { get; }
    public string DungeonName { get; } = "Donjon des Araignées";

    private static readonly Vector4 GrassLight = new(0.35f, 0.55f, 0.28f, 1f);
    private static readonly Vector4 GrassMid = new(0.30f, 0.48f, 0.24f, 1f);
    private static readonly Vector4 GrassDark = new(0.25f, 0.42f, 0.20f, 1f);
    private static readonly Vector4 DirtPath = new(0.55f, 0.44f, 0.30f, 1f);
    private static readonly Vector4 WaterBlue = new(0.20f, 0.40f, 0.65f, 1f);
    private static readonly Vector4 DungeonMaw = new(0.18f, 0.08f, 0.22f, 1f);

    private static readonly Vector4 Gold = new(0.85f, 0.70f, 0.25f, 1f);
    private static readonly Vector4 DarkGold = new(0.55f, 0.44f, 0.14f, 1f);
    private static readonly Vector4 Tan = new(0.72f, 0.58f, 0.40f, 1f);
    private static readonly Vector4 Brown = new(0.45f, 0.34f, 0.22f, 1f);
    private static readonly Vector4 SteelBlue = new(0.40f, 0.55f, 0.68f, 1f);
    private static readonly Vector4 DarkBlue = new(0.22f, 0.32f, 0.45f, 1f);
    private static readonly Vector4 Ember = new(0.75f, 0.35f, 0.20f, 1f);
    private static readonly Vector4 DarkEmber = new(0.45f, 0.20f, 0.12f, 1f);
    private static readonly Vector4 Purple = new(0.55f, 0.35f, 0.65f, 1f);
    private static readonly Vector4 DarkPurple = new(0.32f, 0.20f, 0.40f, 1f);

    public WorldMap(int size)
    {
        Size = size;
        TileColors = new Vector4[size, size];

        var capital = (X: size / 2, Y: size / 2);
        var village = (X: size / 2 - 10, Y: size / 2 - 8);
        var auctionHouse = (X: size / 2 + 8, Y: size / 2 - 6);
        var forge = (X: size / 2 - 6, Y: size / 2 + 9);
        var guild = (X: size / 2 + 9, Y: size / 2 + 8);

        SpawnPosition = (capital.X, capital.Y + 2);
        DungeonEntrance = (size - 4, size - 4);

        var pathTiles = new HashSet<(int X, int Y)>();
        foreach (var target in new[] { village, auctionHouse, forge, guild, DungeonEntrance })
        {
            MarkPath(pathTiles, capital, target);
        }

        var pond = (X: 6, Y: size - 8);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                TileColors[x, y] = ComputeTileColor(x, y, pathTiles, pond);
            }
        }

        Buildings =
        [
            new Building("Capitale", capital.X, capital.Y, 2.6f, Gold, DarkGold, Gold * 0.8f),
            new Building("Village", village.X, village.Y, 1.4f, Tan, Brown, Tan * 0.8f),
            new Building("Hôtel des ventes", auctionHouse.X, auctionHouse.Y, 1.6f, SteelBlue, DarkBlue, SteelBlue * 0.8f),
            new Building("Forge", forge.X, forge.Y, 1.5f, Ember, DarkEmber, Ember * 0.8f),
            new Building("Guilde", guild.X, guild.Y, 1.8f, Purple, DarkPurple, Purple * 0.8f),
        ];
    }

    public bool IsWithinBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

    public Vector4 GetDungeonMarkerColor() => DungeonMaw;

    private static void MarkPath(HashSet<(int X, int Y)> pathTiles, (int X, int Y) from, (int X, int Y) to)
    {
        var x = from.X;
        var y = from.Y;

        while (x != to.X)
        {
            pathTiles.Add((x, y));
            x += Math.Sign(to.X - x);
        }

        while (y != to.Y)
        {
            pathTiles.Add((x, y));
            y += Math.Sign(to.Y - y);
        }

        pathTiles.Add((to.X, to.Y));
    }

    private static Vector4 ComputeTileColor(int x, int y, HashSet<(int X, int Y)> pathTiles, (int X, int Y) pond)
    {
        if (pathTiles.Contains((x, y)))
        {
            return DirtPath;
        }

        var distanceToPond = MathF.Sqrt(MathF.Pow(x - pond.X, 2) + MathF.Pow(y - pond.Y, 2));
        if (distanceToPond < 4.5f)
        {
            return WaterBlue;
        }

        return Hash(x, y) switch
        {
            < 0.33f => GrassLight,
            < 0.66f => GrassMid,
            _ => GrassDark,
        };
    }

    /// <summary>Hash déterministe [0,1) par case, pour une variation de terrain reproductible sans dépendance externe.</summary>
    private static float Hash(int x, int y)
    {
        unchecked
        {
            var h = (x * 374761393) + (y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }
}
