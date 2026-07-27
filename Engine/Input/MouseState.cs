using System.Numerics;
using Silk.NET.Input;

namespace Aetheria.Engine.Input;

/// <summary>Position et boutons de la souris — utilisé pour sélectionner une case de la grille de combat.</summary>
public sealed class MouseState
{
    private readonly IMouse? _mouse;

    public MouseState(IInputContext input)
    {
        _mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
    }

    public Vector2 Position => _mouse?.Position ?? Vector2.Zero;

    public bool IsButtonDown(MouseButton button) => _mouse?.IsButtonPressed(button) ?? false;
}
