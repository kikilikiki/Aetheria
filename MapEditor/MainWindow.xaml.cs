using System.Windows;
using Aetheria.MapEditor.ViewModels;

namespace Aetheria.MapEditor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>Voir Docs/Idees.md — écran de connexion admin : un PasswordBox ne supporte pas le data binding de son mot de passe pour des raisons de sécurité (même pattern que AdminPanel).</summary>
    private void OnAdminPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.AdminPassword = AdminPasswordBox.Password;
}
