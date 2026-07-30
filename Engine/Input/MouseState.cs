using System.Numerics;
using Silk.NET.Input;

namespace Aetheria.Engine.Input;

/// <summary>
/// Position et boutons de la souris, avec détection de "vient d'être cliqué" (comme
/// <see cref="KeyboardState"/>) — utilisé pour sélectionner une case de la grille de combat
/// ou déplacer le personnage au clic. À mettre à jour une fois par frame via <see cref="Update"/>.
/// </summary>
public sealed class MouseState
{
    private readonly IMouse? _mouse;
    private readonly HashSet<MouseButton> _previousDown = [];
    private readonly HashSet<MouseButton> _currentDown = [];
    private float _pendingScrollDelta;
    private float _scrollDelta;

    public MouseState(IInputContext input)
    {
        _mouse = input.Mice.Count > 0 ? input.Mice[0] : null;
        if (_mouse is not null)
        {
            // Voir retour utilisateur — "faire en sorte que l'on puisse aussi scroll a la
            // molette" : évènement Silk.NET (pas de polling direct pour la molette), accumulé
            // ici puis exposé par frame via ScrollDelta (voir Update).
            _mouse.Scroll += (_, wheel) => _pendingScrollDelta += wheel.Y;
        }
    }

    public Vector2 Position => _mouse?.Position ?? Vector2.Zero;

    /// <summary>Déplacement de la molette depuis la dernière frame (positif = vers le haut). Voir <see cref="Update"/> pour la réinitialisation par frame.</summary>
    public float ScrollDelta => _scrollDelta;

    public void Update()
    {
        _previousDown.Clear();
        _previousDown.UnionWith(_currentDown);
        _currentDown.Clear();

        _scrollDelta = _pendingScrollDelta;
        _pendingScrollDelta = 0f;

        if (_mouse is null)
        {
            return;
        }

        foreach (var button in Enum.GetValues<MouseButton>())
        {
            if (button != MouseButton.Unknown && _mouse.IsButtonPressed(button))
            {
                _currentDown.Add(button);
            }
        }
    }

    public bool IsButtonDown(MouseButton button) => _currentDown.Contains(button);

    public bool WasButtonJustPressed(MouseButton button) => _currentDown.Contains(button) && !_previousDown.Contains(button);
}
