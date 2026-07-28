using System.Numerics;

namespace Aetheria.Client.World;

/// <summary>
/// Conversion grille logique -&gt; écran pour une vue isométrique "2:1" classique (façon
/// Dofus/Diablo) : chaque tuile carrée de la grille de jeu est projetée en losange à l'écran,
/// ce qui donne l'impression de perspective sans nécessiter de vrai rendu 3D.
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
}
