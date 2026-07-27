using System.Windows;
using Aetheria.MapEditor.ViewModels;

namespace Aetheria.MapEditor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
