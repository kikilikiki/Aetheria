using Avalonia.Controls;

namespace Aetheria.Launcher.Services;

/// <summary>
/// Avalonia n'expose pas de presse-papiers statique global comme <c>System.Windows.Clipboard</c>
/// — l'accès passe par le <c>TopLevel</c> (fenêtre) qui l'héberge. MainWindow enregistre la
/// référence à sa construction pour que le ViewModel (qui n'a pas de référence à la vue,
/// conformément au pattern MVVM déjà en place) puisse y accéder.
/// </summary>
public static class ClipboardService
{
    public static Window? MainWindow { get; set; }

    public static Task SetTextAsync(string text) =>
        MainWindow?.Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;
}
