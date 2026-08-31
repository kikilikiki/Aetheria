using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace Aetheria.Launcher.Behaviors;

/// <summary>
/// Rejoue un fondu + léger glissement vers le haut à chaque passage en visible d'un élément — pas
/// seulement au premier affichage de la fenêtre (Avalonia n'a pas d'événement "Loaded" qui se
/// redéclenche quand IsVisible repasse de false à true, un panneau superposé resterait donc figé/
/// instantané sans ce comportement). Purement visuel : n'ajoute ni ne renomme aucun Binding/
/// Command existant.
/// </summary>
public static class FadeInBehavior
{
    public static readonly AttachedProperty<bool> EnableProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Enable", typeof(FadeInBehavior));

    public static bool GetEnable(Control element) => element.GetValue(EnableProperty);

    public static void SetEnable(Control element, bool value) => element.SetValue(EnableProperty, value);

    static FadeInBehavior()
    {
        EnableProperty.Changed.AddClassHandler<Control>(OnEnableChanged);
    }

    private static void OnEnableChanged(Control element, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        element.PropertyChanged += (sender, args) =>
        {
            if (sender is Control control && args.Property == Visual.IsVisibleProperty && args.NewValue is true)
            {
                _ = Animate(control);
            }
        };
    }

    private static async Task Animate(Control element)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(240),
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
                        new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("translateY(16px)")),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1d),
                        new Setter(Visual.RenderTransformProperty, TransformOperations.Parse("translateY(0px)")),
                    },
                },
            },
        };

        try
        {
            await animation.RunAsync(element);
        }
        catch
        {
            // Animation cosmétique : un échec ne doit jamais remonter en erreur fatale (voir
            // App.axaml.cs, filet TaskScheduler.UnobservedTaskException).
            element.Opacity = 1d;
        }
    }
}
