using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BatchVinci;

public sealed partial class MainWindow : Window
{
    public Frame Nav => RootFrame;

    public MainWindow()
    {
        InitializeComponent();
        Title = "BatchVinci";
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")); } catch { }
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        RootFrame.Navigated += (_, _) => BackButton.Visibility = RootFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;
        RootFrame.Navigate(typeof(HomePage));
    }

    void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.CanGoBack) RootFrame.GoBack();
    }

    public static Frame Frame => ((MainWindow)App.MainWindow).RootFrame;
}
