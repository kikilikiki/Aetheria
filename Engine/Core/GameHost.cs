using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Aetheria.Engine.Core;

/// <summary>
/// Encapsule la fenêtre et la boucle de jeu (Load / Update / Render / Resize) au-dessus de
/// Silk.NET, afin que Client et MapEditor n'aient jamais besoin de manipuler directement
/// l'API de fenêtrage/contexte OpenGL sous-jacente.
/// </summary>
public sealed class GameHost : IDisposable
{
    private readonly IWindow _window;

    /// <summary>Contexte OpenGL, disponible une fois <see cref="Load"/> déclenché.</summary>
    public GL Gl { get; private set; } = null!;

    /// <summary>Contexte clavier/souris, disponible une fois <see cref="Load"/> déclenché.</summary>
    public IInputContext Input { get; private set; } = null!;

    /// <summary>Déclenché une seule fois quand la fenêtre et le contexte OpenGL sont prêts.</summary>
    public event Action? Load;

    /// <summary>Déclenché à chaque tick de simulation, avant le rendu.</summary>
    public event Action<float>? Update;

    /// <summary>Déclenché à chaque frame, pour dessiner la scène.</summary>
    public event Action<float>? Render;

    /// <summary>Déclenché quand la fenêtre change de taille (nouvelle largeur/hauteur en pixels).</summary>
    public event Action<int, int>? Resize;

    public GameHost(string title, int width, int height)
    {
        var options = WindowOptions.Default with
        {
            Title = title,
            Size = new Vector2D<int>(width, height),
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += delta => Update?.Invoke((float)delta);
        _window.Render += delta => Render?.Invoke((float)delta);
        _window.Resize += size => Resize?.Invoke(size.X, size.Y);
    }

    /// <summary>Démarre la boucle de jeu. Bloque jusqu'à la fermeture de la fenêtre.</summary>
    public void Run() => _window.Run();

    /// <summary>Voir GDD/demande utilisateur — "ajoute un ecran titre avec play option etc" (bouton Quitter) : ferme la fenêtre, ce qui débloque <see cref="Run"/> proprement (même chemin que fermer la fenêtre à la croix).</summary>
    public void Close() => _window.Close();

    private void OnLoad()
    {
        Gl = GL.GetApi(_window);
        Input = _window.CreateInput();
        Load?.Invoke();
    }

    public void Dispose()
    {
        Gl?.Dispose();
        _window.Dispose();
    }
}
