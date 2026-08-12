using Aetheria.Launcher.Services;
using Aetheria.Launcher.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
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
        await Task.Delay(delayMs);

        // RenderTransform est de type ITransform côté propriété Avalonia : aucun animateur n'est
        // enregistré pour ce type (même si TransformOperations.Parse produit une valeur valide en
        // usage statique) — animer Visual.RenderTransformProperty directement lève
        // "No animator registered for the property RenderTransform" (crash silencieux au
        // démarrage, cette Task étant fire-and-forget, voir retour utilisateur - "erreur sur le
        // launcher" après la 0.3.1). On anime donc un TranslateTransform dédié (X/Y, double,
        // animés nativement) assigné au RenderTransform plutôt que la propriété elle-même.
        var transform = new TranslateTransform();
        target.RenderTransform = transform;

        var opacityAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = new QuadraticEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 1d) } },
            },
        };

        var translateAnimation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = new QuadraticEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.XProperty, fromX), new Setter(TranslateTransform.YProperty, fromY) },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.XProperty, 0d), new Setter(TranslateTransform.YProperty, 0d) },
                },
            },
        };

        await Task.WhenAll(opacityAnimation.RunAsync(target), translateAnimation.RunAsync(transform));
    }
}
