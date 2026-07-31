using System.ComponentModel;
using Aetheria.Shared.Settings;

namespace Aetheria.Launcher.Localization;

/// <summary>
/// Voir GDD/demande utilisateur — "traduit le launcher en anglais aussi" : le Launcher n'a
/// jusqu'ici jamais lu <see cref="GameSettings.Language"/> pour lui-même (seulement pour le
/// transmettre au Client, voir MainViewModel.OnLanguagePreferenceChanged) — ce singleton porte la
/// langue courante du Launcher et notifie les bindings XAML (voir TrExtension) quand elle change,
/// pour une traduction immédiate sans redémarrer l'application.
/// </summary>
public sealed class LauncherLanguageService : INotifyPropertyChanged
{
    public static LauncherLanguageService Instance { get; } = new();

    private Language _current = GameSettings.Load().Language;

    public Language Current
    {
        get => _current;
        set
        {
            if (_current == value)
            {
                return;
            }

            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
