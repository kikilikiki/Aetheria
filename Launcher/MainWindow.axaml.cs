using Aetheria.Launcher.Services;
using Aetheria.Launcher.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace Aetheria.Launcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // Voir ClipboardService : le ViewModel n'a pas de référence à la vue (MVVM), la fenêtre
        // s'enregistre elle-même pour lui donner accès au presse-papiers Avalonia.
        ClipboardService.MainWindow = this;

        // Voir Docs/Idees.md — vraie image de profil : même pattern pour le sélecteur de fichiers.
        FilePickerService.MainWindow = this;
    }

    /// <summary>
    /// Voir retour utilisateur — "au launcher pouvoir voir le mot de passe que l'on tape" : bascule
    /// le champ mot de passe unique entre masqué et en clair (voir BoolToPasswordCharConverter,
    /// MainWindow.axaml) — plus besoin de synchronisation manuelle comme sous WPF puisque
    /// TextBox.Text (contrairement à PasswordBox.Password) est directement bindable.
    /// </summary>
    private void OnTogglePasswordVisibility(object? sender, RoutedEventArgs e) =>
        _viewModel.IsPasswordVisible = !_viewModel.IsPasswordVisible;

    /// <summary>
    /// Entrée en scène : cascade fondu + glissement sur la barre latérale, la barre du haut, le
    /// corps puis la barre du bas — portage de Window.Triggers/EventTrigger[Loaded] (WPF), qui
    /// n'a pas d'équivalent direct côté Avalonia (voir MainWindow.axaml pour le détail). Ne
    /// concerne que le premier affichage de la fenêtre — les panneaux superposés utilisent
    /// FadeInBehavior pour rejouer leur propre fondu à chaque ouverture.
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = PlayEntranceAnimationAsync();
    }

    private async Task PlayEntranceAnimationAsync()
    {
        await Task.WhenAll(
            FadeSlideAsync(SidebarBorder, -24, 0, 0, 400),
            FadeSlideAsync(TopBarBorder, 0, -16, 80, 400),
            FadeSlideAsync(BodyGrid, 0, 18, 160, 450),
            FadeSlideAsync(BottomBarBorder, 0, 16, 240, 400));
    }

    private static async Task FadeSlideAsync(Control target, double fromX, double fromY, int delayMs, int durationMs)
    {
        try
        {
            await Task.Delay(delayMs);

            // On anime UNIQUEMENT le contrôle (un Visual) : opacité + RenderTransform via une
            // valeur TransformOperations (le seul type de transform pour lequel Avalonia a un
            // animateur enregistré). Animer un TranslateTransform nu via Animation.RunAsync lève
            // "Unable to cast ... TranslateTransform to ... Visual" sous Avalonia 11.2 (crash
            // remonté par le filet TaskScheduler.UnobservedTaskException, voir App.axaml.cs) —
            // même famille de bug que les précédents portages d'animation du Launcher.
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Easing = new QuadraticEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0d),
                            new Setter(Visual.RenderTransformProperty, TransformOperations.Parse($"translate({fromX}px, {fromY}px)")),
                        },
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1d),
                            new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("translate(0px, 0px)")),
                        },
                    },
                },
            };

            await animation.RunAsync(target);
        }
        catch
        {
            // Animation purement cosmétique : un échec ne doit jamais faire remonter d'erreur au
            // lancement (l'élément reste simplement affiché sans transition).
            target.Opacity = 1d;
        }
    }
}
