using BatchVinci.Core;
using Microsoft.UI.Xaml;

namespace BatchVinci;

public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;
    public static ConfigStore Store { get; } = new();

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartMenuShortcut.EnsureCreated();
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
