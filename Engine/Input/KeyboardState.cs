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
    private readonly Queue<char> _typedChars = new();

    public KeyboardState(IInputContext input)
    {
        _keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;

        if (_keyboard is not null)
        {
            // BeginInput/EndInput ne font rien sur desktop (utile seulement sur mobile où le
            // clavier est virtuel) mais on les appelle par convention — voir doc Silk.NET.
            _keyboard.BeginInput();
            _keyboard.KeyChar += (_, character) => _typedChars.Enqueue(character);
        }
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

    /// <summary>
    /// Caractères réellement tapés depuis le dernier appel, tels que produits par la disposition
    /// clavier du système (QWERTY, AZERTY, ...) — contrairement à <see cref="IsDown"/>/
    /// <see cref="WasJustPressed"/> qui portent sur la position physique de la touche (les codes
    /// <see cref="Key"/> de GLFW/Silk.NET sont indépendants de la disposition, ce qui convient
    /// pour des déplacements type WASD mais donnerait la mauvaise lettre pour une saisie de texte
    /// sur un clavier AZERTY). À utiliser pour toute saisie de nom/texte libre.
    /// </summary>
    public IReadOnlyList<char> DrainTypedChars()
    {
        if (_typedChars.Count == 0)
        {
            return [];
        }

        var chars = _typedChars.ToArray();
        _typedChars.Clear();
        return chars;
    }
}
