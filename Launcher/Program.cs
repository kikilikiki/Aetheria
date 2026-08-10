using Avalonia;

namespace Aetheria.Launcher;

public static class Program
{
    // Avalonia (contrairement à WPF, qui démarrait implicitement via App.xaml/StartupUri) exige
    // un point d'entrée explicite. [STAThread] reste nécessaire sous Windows (interop
    // COM/presse-papiers) ; les autres OS l'ignorent.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
