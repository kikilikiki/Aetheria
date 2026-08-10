using System.Globalization;
using Avalonia.Data.Converters;

namespace Aetheria.Launcher;

/// <summary>
/// Inverse booléen simple, branché sur <c>IsVisible</c> (Avalonia n'a pas de <c>Visibility</c>
/// à la WPF — la visibilité est un bool direct sur tout <c>Visual</c>) : true ⇒ false.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
