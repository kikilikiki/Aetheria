using System.Globalization;
using Aetheria.Shared.Localization;
using Aetheria.Shared.Settings;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Aetheria.Launcher.Localization;

/// <summary>
/// Voir GDD/demande utilisateur — "traduit le launcher en anglais aussi". Extension XAML
/// `{loc:Tr 'Texte francais'}` : réutilise telle quelle la table de traduction du jeu (voir
/// Shared/Localization/Localization.cs, déjà enrichie de ~340 entrées) plutôt qu'un second
/// dictionnaire à maintenir en double. Se relie à <see cref="LauncherLanguageService"/> via un
/// Binding pour se retraduire automatiquement dès que la langue change, sans redémarrer le
/// Launcher (contrairement à un `.resx`/culture qui exigerait de relancer l'application).
/// Avalonia (contrairement à WPF) ne demande pas d'hériter de MarkupExtension : toute classe
/// exposant une méthode ProvideValue(IServiceProvider) est reconnue comme extension XAML.
/// </summary>
public sealed class TrExtension(string text)
{
    public string Text { get; set; } = text;

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(LauncherLanguageService.Current))
        {
            Source = LauncherLanguageService.Instance,
            Converter = new TrConverter(Text),
            Mode = BindingMode.OneWay,
        };

        // Avalonia.Data.Binding, contrairement à System.Windows.Data.Binding, n'a pas de méthode
        // ProvideValue : c'est le Binding lui-même (IBinding) que le runtime XAML applique
        // comme valeur de la propriété cible quand une extension personnalisée le retourne.
        return binding;
    }
}

internal sealed class TrConverter(string text) : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Aetheria.Shared.Localization.Localization.Translate(text, value is Language language ? language : Language.Francais);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
