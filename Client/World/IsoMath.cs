using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Conversion grille logique &lt;-&gt; écran pour une vue isométrique "2:1" classique : chaque
/// tuile carrée de la grille de jeu est projetée en losange à l'écran, ce qui donne
/// l'impression de perspective sans nécessiter de vrai rendu 3D.
/// </summary>
public static class IsoMath
{
    public const float TileWidth = 64f;
    public const float TileHeight = 32f;

    public static Vector2 GridToIso(float gridX, float gridY)
    {
        var screenX = (gridX - gridY) * (TileWidth / 2f);
        var screenY = (gridX + gridY) * (TileHeight / 2f);
        return new Vector2(screenX, screenY);
    }

    /// <summary>Inverse de <see cref="GridToIso"/> : retrouve la case de grille sous une position écran (ex. un clic).</summary>
    public static Vector2 IsoToGrid(Vector2 screenPosition)
    {
        var a = screenPosition.X / (TileWidth / 2f);
        var b = screenPosition.Y / (TileHeight / 2f);
        var gridX = (a + b) / 2f;
        var gridY = (b - a) / 2f;
        return new Vector2(gridX, gridY);
    }
}
