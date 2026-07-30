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

    /// <summary>
    /// Voir retour utilisateur — "au launcher pouvoir voir le mot de passe que l'on tape" : bascule
    /// entre le PasswordBox masqué et un TextBox en clair (les deux liés au même
    /// <see cref="MainViewModel.Password"/>, un seul visible à la fois — voir MainWindow.xaml).
    /// PasswordBox.Password n'étant pas bindable, il faut le resynchroniser manuellement en
    /// repassant en mode masqué : il ne reçoit aucune mise à jour tant que le TextBox est actif.
    /// </summary>
    private void OnTogglePasswordVisibility(object sender, RoutedEventArgs e)
    {
        _viewModel.IsPasswordVisible = !_viewModel.IsPasswordVisible;
        if (!_viewModel.IsPasswordVisible)
        {
            PasswordBox.Password = _viewModel.Password;
        }
    }
}
