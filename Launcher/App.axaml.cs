using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace Aetheria.Launcher;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Filet de sécurité : sans ceci, toute exception non gérée sur le thread UI (ex. le bug
        // de crash sur "Créer un compte" avec un email vide, voir MainViewModel.IsValidEmail)
        // termine tout le processus sans message compréhensible pour l'utilisateur. Avalonia n'a
        // pas d'équivalent direct à Dispatcher.UnhandledException : on passe par
        // AppDomain/TaskScheduler, qui couvrent aussi bien le thread UI que les Task async
        // (RelayCommand asynchrones du ViewModel).
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowFatalError(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ShowFatalError(e.Exception);
            e.SetObserved();
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ShowFatalError(Exception? ex)
    {
        if (ex is null)
        {
            return;
        }

        // TaskScheduler.UnobservedTaskException se déclenche sur le thread du finaliseur GC (pas
        // le thread UI) : construire des contrôles Avalonia (Button/Window ci-dessous) depuis ce
        // thread lève "Call from invalid thread" et fait planter le processus — cette fenêtre
        // d'erreur censée être un filet de sécurité devenait elle-même la cause du crash. Il faut
        // donc systématiquement rebasculer sur le thread UI avant de construire quoi que ce soit.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowFatalError(ex));
            return;
        }

        var closeButton = new Button { Content = "Fermer", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) };
        var window = new Window
        {
            Title = "Aetheria Launcher — Erreur",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.Parse("#18181B")),
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Une erreur inattendue est survenue :",
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(0, 0, 0, 10),
                    },
                    new TextBlock { Text = ex.Message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap },
                    closeButton,
                },
            },
        };
        closeButton.Click += (_, _) => window.Close();

        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } main })
        {
            window.ShowDialog(main);
        }
        else
        {
            window.Show();
        }
    }
}
