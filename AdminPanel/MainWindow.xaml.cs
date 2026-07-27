using System.Windows;
using Aetheria.AdminPanel.ViewModels;

namespace Aetheria.AdminPanel;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
