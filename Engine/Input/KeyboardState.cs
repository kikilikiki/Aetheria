using Silk.NET.Input;

namespace Aetheria.Engine.Input;

/// <summary>
/// État clavier interrogeable par polling, avec détection de "vient d'être pressée" (utile
/// pour un déplacement case par case sur la grille tactique plutôt qu'un mouvement continu).
/// À appeler une fois par frame via <see cref="Update"/> (voir <c>GameHost.Update</c>).
/// </summary>
public sealed class KeyboardState
{
    private readonly IKeyboard? _keyboard;
    private readonly HashSet<Key> _previousDown = [];
    private readonly HashSet<Key> _currentDown = [];

    public KeyboardState(IInputContext input)
    {
        _keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
    }

    public void Update()
    {
        _previousDown.Clear();
        _previousDown.UnionWith(_currentDown);
        _currentDown.Clear();

        if (_keyboard is null)
        {
            return;
        }

        foreach (var key in Enum.GetValues<Key>())
        {
            if (key != Key.Unknown && _keyboard.IsKeyPressed(key))
            {
                _currentDown.Add(key);
            }
        }
    }

    public bool IsDown(Key key) => _currentDown.Contains(key);

    public bool WasJustPressed(Key key) => _currentDown.Contains(key) && !_previousDown.Contains(key);
}
