using System.Numerics;

namespace Aetheria.Engine.Rendering;

/// <summary>
/// Caméra orthographique 2D. <see cref="Position"/> est le point du monde affiché au centre
/// de l'écran ; suffisant pour un jeu en vue de dessus/grille comme Aetheria (pas besoin de
/// perspective 3D).
/// </summary>
public sealed class Camera2D
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public float Zoom { get; set; } = 1f;
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }

    public Matrix4x4 GetViewProjection()
    {
        var halfWidth = ViewportWidth / (2f * Zoom);
        var halfHeight = ViewportHeight / (2f * Zoom);

        var left = Position.X - halfWidth;
        var right = Position.X + halfWidth;
        var bottom = Position.Y + halfHeight;
        var top = Position.Y - halfHeight;

        return Matrix4x4.CreateOrthographicOffCenter(left, right, bottom, top, -1f, 1f);
    }

    /// <summary>
    /// Convertit une position écran en pixels (origine en haut-à-gauche de la fenêtre, comme
    /// <see cref="Aetheria.Engine.Input.MouseState.Position"/>) en position monde — l'inverse
    /// de <see cref="GetViewProjection"/>. Utilisé pour la sélection au clic.
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        var halfWidth = ViewportWidth / (2f * Zoom);
        var halfHeight = ViewportHeight / (2f * Zoom);

        var left = Position.X - halfWidth;
        var top = Position.Y - halfHeight;

        var worldX = left + (screenPosition.X / ViewportWidth) * (2f * halfWidth);
        var worldY = top + (screenPosition.Y / ViewportHeight) * (2f * halfHeight);

        return new Vector2(worldX, worldY);
    }
}
