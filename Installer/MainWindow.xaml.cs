using System.Windows;
using Aetheria.Installer.ViewModels;

namespace Aetheria.Installer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
