using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using Aetheria.Shared;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Aetheria.Launcher;

/// <summary>
/// "Avatar" simplifié en pastille de couleur + initiale (voir GDD/demande utilisateur — panneau
/// Communauté du Launcher, "avec leur avatar d'affiché") : reste le repère visuel affiché tant
/// qu'aucune image de profil n'a été envoyée (voir Docs/Idees.md — <c>AvatarUrlToBitmapConverter</c>
/// ci-dessous pour la vraie image, une fois <c>UserEntity.AvatarUrl</c> renseigné).
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

/// <summary>
/// Voir Docs/Idees.md — vraie image de profil : convertit l'URL relative renvoyée par le serveur
/// (<c>UserEntity.AvatarUrl</c>, ex. <c>/avatars/xxx.png</c>) en <see cref="Bitmap"/> Avalonia.
/// <see cref="ServerHost"/> est mis à jour par <c>MainViewModel</c> à chaque changement de serveur
/// (évite un <c>MultiBinding</c> pour un simple préfixe d'URL). Simplification assumée :
/// téléchargement synchrone avec petit cache mémoire par URL — pas de pipeline de chargement
/// asynchrone dédié pour cet usage peu fréquent (liste Communauté, ouverte occasionnellement).
/// </summary>
public sealed class AvatarUrlToBitmapConverter : IValueConverter
{
    public static string ServerHost = "localhost";

    private static readonly Dictionary<string, Bitmap?> Cache = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string relativeUrl || string.IsNullOrWhiteSpace(relativeUrl))
        {
            return null;
        }

        var fullUrl = $"http://{ServerHost}:{GameInfo.DefaultAccountApiPort}{relativeUrl}";
        if (Cache.TryGetValue(fullUrl, out var cached))
        {
            return cached;
        }

        try
        {
            var bytes = Http.GetByteArrayAsync(fullUrl).GetAwaiter().GetResult();
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Cache[fullUrl] = bitmap;
            return bitmap;
        }
        catch
        {
            Cache[fullUrl] = null;
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Voir Docs/Idees.md — bascule pastille/vraie image : vrai si une URL d'avatar non vide est présente.</summary>
public sealed class StringNotEmptyToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Avalonia n'a pas de PasswordBox dédié comme WPF — le masquage se fait via
/// <c>TextBox.PasswordChar</c> sur un TextBox normal (dont le Text EST bindable, contrairement à
/// WPF PasswordBox.Password). Voir retour utilisateur — "au launcher pouvoir voir le mot de passe
/// que l'on tape" : ce convertisseur bascule entre masqué ('●') et en clair ('\0', pas de
/// masquage) selon <see cref="ViewModels.MainViewModel.IsPasswordVisible"/>.
/// </summary>
public sealed class BoolToPasswordCharConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? '\0' : '●';

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
