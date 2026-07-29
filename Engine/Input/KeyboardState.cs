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
    private readonly IGamepad? _gamepad;
    private readonly HashSet<Key> _previousDown = [];
    private readonly HashSet<Key> _currentDown = [];
    private readonly Queue<char> _typedChars = new();

    /// <summary>
    /// Voir GDD/demande utilisateur — "ajoute un support manette" : plutôt qu'un état séparé à
    /// vérifier en plus du clavier partout dans Client/Program.cs (des centaines d'appels à
    /// <see cref="WasJustPressed"/>/<see cref="IsDown"/>), les boutons de la manette sont fondus
    /// dans le même <see cref="_currentDown"/> — chaque appelant existant obtient le support
    /// manette gratuitement, sans modification. Un seul stick/pad analogique n'a pas de sens pour
    /// une saisie de texte libre : pas d'équivalent manette pour <see cref="DrainTypedChars"/>.
    /// </summary>
    private static readonly Dictionary<ButtonName, Key[]> GamepadButtonMap = new()
    {
        [ButtonName.A] = [Key.Enter, Key.E],
        [ButtonName.B] = [Key.Escape],
        [ButtonName.X] = [Key.E],
        [ButtonName.DPadUp] = [Key.Up],
        [ButtonName.DPadDown] = [Key.Down],
        [ButtonName.DPadLeft] = [Key.Left],
        [ButtonName.DPadRight] = [Key.Right],
        [ButtonName.LeftBumper] = [Key.M],
        [ButtonName.RightBumper] = [Key.F1],
        [ButtonName.Start] = [Key.Escape],
    };

    private const float StickDeadzone = 0.5f;

    public KeyboardState(IInputContext input)
    {
        _keyboard = input.Keyboards.Count > 0 ? input.Keyboards[0] : null;
        _gamepad = input.Gamepads.Count > 0 ? input.Gamepads[0] : null;

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

        if (_keyboard is not null)
        {
            foreach (var key in Enum.GetValues<Key>())
            {
                if (key != Key.Unknown && _keyboard.IsKeyPressed(key))
                {
                    _currentDown.Add(key);
                }
            }
        }

        if (_gamepad is not null)
        {
            foreach (var button in _gamepad.Buttons)
            {
                if (button.Pressed && GamepadButtonMap.TryGetValue(button.Name, out var keys))
                {
                    _currentDown.UnionWith(keys);
                }
            }

            // Stick gauche = déplacement/navigation (voir GDD/demande utilisateur — "manette pour
            // pouvoir y jouer") : mêmes touches Haut/Bas/Gauche/Droite que le D-pad, avec une zone
            // morte pour ignorer le bruit du capteur au repos.
            if (_gamepad.Thumbsticks.Count > 0)
            {
                var stick = _gamepad.Thumbsticks[0];
                if (stick.Y < -StickDeadzone) _currentDown.Add(Key.Up);
                if (stick.Y > StickDeadzone) _currentDown.Add(Key.Down);
                if (stick.X < -StickDeadzone) _currentDown.Add(Key.Left);
                if (stick.X > StickDeadzone) _currentDown.Add(Key.Right);
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

    /// <summary>
    /// Vide la file de caractères tapés sans les consommer (voir GDD/demande utilisateur — les
    /// touches de déplacement produisent aussi des évènements <c>KeyChar</c> ; sans purge
    /// systématique en fin de frame quand aucun champ de saisie n'est actif, elles s'accumulaient
    /// indéfiniment et se déversaient d'un coup dans le tchat à sa prochaine ouverture).
    /// </summary>
    public void DiscardTypedChars() => _typedChars.Clear();

    /// <summary>Copie du texte dans le presse-papiers système (voir GDD/demande utilisateur — bouton pour copier le code de groupe), via GLFW/Silk.NET plutôt qu'une dépendance WinForms/WPF.</summary>
    public void SetClipboardText(string text)
    {
        if (_keyboard is not null)
        {
            _keyboard.ClipboardText = text;
        }
    }
}
