using System.Windows;
using Aetheria.Launcher.ViewModels;

namespace Aetheria.Launcher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>
    /// WPF n'autorise pas le binding direct de <see cref="System.Windows.Controls.PasswordBox.Password"/>
    /// (protection contre l'exposition du mot de passe en mémoire via le binding) : on relaie
    /// manuellement la valeur au ViewModel.
    /// </summary>
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.Password = PasswordBox.Password;
}
