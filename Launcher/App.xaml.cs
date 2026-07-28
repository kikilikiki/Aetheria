using System.Windows;
using System.Windows.Threading;

namespace Aetheria.Launcher;

public partial class App : Application
{
    public App()
    {
        // Filet de sécurité : sans ceci, toute exception non gérée sur le thread UI (ex. le bug
        // de crash sur "Créer un compte" avec un email vide, voir MainViewModel.IsValidEmail)
        // termine tout le processus WPF sans message compréhensible pour l'utilisateur.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Une erreur inattendue est survenue :\n\n{e.Exception.Message}",
            "Aetheria Launcher — Erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
