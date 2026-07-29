using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aetheria.Launcher.Behaviors;

/// <summary>
/// Rejoue un fondu + léger glissement vers le haut à chaque passage en visible d'un élément — pas
/// seulement au premier chargement de la fenêtre (l'événement Loaded ne se redéclenche pas quand
/// Visibility repasse de Collapsed à Visible, un panneau superposé resterait donc figé/instantané
/// sans ce comportement). Purement visuel : n'ajoute ni ne renomme aucun Binding/Command existant.
/// </summary>
public static class FadeInBehavior
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable", typeof(bool), typeof(FadeInBehavior), new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnable(DependencyObject obj) => (bool)obj.GetValue(EnableProperty);

    public static void SetEnable(DependencyObject obj, bool value) => obj.SetValue(EnableProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || e.NewValue is not true)
        {
            return;
        }

        element.IsVisibleChanged += OnIsVisibleChanged;
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not UIElement element || e.NewValue is not true)
        {
            return;
        }

        var transform = new TranslateTransform(0, 16);
        element.RenderTransform = transform;
        element.Opacity = 0;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing };
        var slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = easing };

        element.BeginAnimation(UIElement.OpacityProperty, fade);
        transform.BeginAnimation(TranslateTransform.YProperty, slide);
    }
}
