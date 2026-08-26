using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Aetheria.Shared;

namespace Aetheria.AdminPanel;

/// <summary>
/// Voir Docs/Idees.md — vraie image de profil : convertit l'URL relative renvoyée par le serveur
/// (<c>AdminUserSummary.AvatarUrl</c>, ex. <c>/avatars/xxx.png</c>) en <see cref="BitmapImage"/> —
/// contrairement à l'équivalent Avalonia du Launcher, WPF sait charger directement depuis une URI
/// http (pas besoin de télécharger les octets à la main). Même limite assumée que l'AdminPanel
/// dans son ensemble : toujours contre <c>localhost</c> (voir AdminApiClient), pas de réglage de
/// serveur distant ici.
/// </summary>
public sealed class AvatarUrlToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string relativeUrl || string.IsNullOrWhiteSpace(relativeUrl))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"http://localhost:{GameInfo.DefaultAccountApiPort}{relativeUrl}");
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Voir Docs/Idees.md — bascule pastille/vraie image : Visible si une URL d'avatar non vide est présente (WPF Visibility, pas de conversion bool implicite comme côté Avalonia).</summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Voir Docs/Idees.md — pastille de repli : couleur déterministe dérivée du pseudo (même palette que le Launcher, voir <c>Launcher/AvatarConverters.cs</c>).</summary>
public sealed class UsernameToAvatarColorConverter : IValueConverter
{
    private static readonly (byte R, byte G, byte B)[] Palette =
    [
        (0xE8, 0xA9, 0x3C), (0x5A, 0xD9, 0x7E), (0x5A, 0x9C, 0xD9), (0xD9, 0x5A, 0x8C),
        (0x9C, 0x5A, 0xD9), (0xD9, 0x8C, 0x5A), (0x5A, 0xD9, 0xC7), (0xD9, 0x5A, 0x5A),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var username = value as string ?? string.Empty;
        var index = Math.Abs(username.GetHashCode()) % Palette.Length;
        var (r, g, b) = Palette[index];
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Voir Docs/Idees.md — première lettre du pseudo, affichée sur la pastille de repli.</summary>
public sealed class UsernameToInitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var username = value as string ?? string.Empty;
        return username.Length > 0 ? char.ToUpperInvariant(username[0]).ToString() : "?";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
