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
}
