using System.Windows;
using Aetheria.AdminPanel.ViewModels;

namespace Aetheria.AdminPanel;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>Voir Launcher/MainWindow.xaml.cs pour l'explication : PasswordBox ne supporte pas le binding direct.</summary>
    private void OnAdminPasswordChanged(object sender, RoutedEventArgs e) => _viewModel.AdminPassword = AdminPasswordBox.Password;
}
