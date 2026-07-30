using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Aetheria.Launcher;

/// <summary>
/// "Avatar" simplifié en pastille de couleur + initiale (voir GDD/demande utilisateur — panneau
/// Communauté du Launcher, "avec leur avatar d'affiché") : aucune image de profil n'existe dans
/// ce projet (pas de pipeline d'upload/stockage d'images), donc une couleur dérivée du pseudo
/// (déterministe, stable d'un lancement à l'autre) sert de repère visuel simple plutôt que de
/// laisser la case vide — voir Docs/README.md pour cette limite assumée.
/// </summary>
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
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Première lettre du pseudo en majuscule, affichée sur la pastille "avatar" (voir <see cref="UsernameToAvatarColorConverter"/>).</summary>
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

/// <summary>
/// Voir GDD/demande utilisateur — "dans le launcher la couleur a gauche du pseudo correspond a si
/// la personne est en ligne ou pas" : remplace la pastille colorée par pseudo
/// (<see cref="UsernameToAvatarColorConverter"/>) par un indicateur vert/rouge selon
/// <c>AdminUserSummary.IsOnline</c> dans la liste Communauté du panel admin.
/// </summary>
public sealed class OnlineStatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush OnlineBrush = new(Color.FromRgb(0x5A, 0xD9, 0x7E));
    private static readonly SolidColorBrush OfflineBrush = new(Color.FromRgb(0xD9, 0x5A, 0x5A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? OnlineBrush : OfflineBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
