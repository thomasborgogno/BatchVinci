using System.Collections.ObjectModel;
using BatchVinci.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BatchVinci;

public sealed class ConfigItem
{
    public ConfigItem(ProjectConfig config) => Config = config;
    public ProjectConfig Config { get; }
    public bool Missing => !File.Exists(Config.DrpPath);
    public Visibility MissingVisibility => Missing ? Visibility.Visible : Visibility.Collapsed;
    public string Subtitle =>
        $"{Config.Parameters.Count} parametri  ·  {Path.GetFileName(Config.DrpPath)}  ·  aggiornata {Config.UpdatedUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
}

public sealed partial class ConfigsPage : Page
{
    public ObservableCollection<ConfigItem> Configs { get; } = new();

    public ConfigsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Configs.Clear();
        foreach (var c in App.Store.LoadAll()) Configs.Add(new ConfigItem(c));
        EmptyText.Visibility = Configs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = Configs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    void List_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ConfigItem ci) Frame.Navigate(typeof(FillPage), ci.Config);
    }

    void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ConfigItem ci) return;
        if (ci.Missing)
        {
            Ui.Show(Status, InfoBarSeverity.Warning, "Il file DRP non esiste più: aprila e usa «Cerca DRP…» per indicarne la nuova posizione.");
            return;
        }
        Frame.Navigate(typeof(SelectPage), ci.Config);
    }

    async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ConfigItem ci) return;
        bool ok = await Ui.ConfirmAsync(XamlRoot, "Elimina configurazione",
            $"Eliminare la configurazione «{ci.Config.Name}»? Il file DRP non verrà toccato.", "Elimina");
        if (!ok) return;
        try
        {
            App.Store.Delete(ci.Config);
            Configs.Remove(ci);
            EmptyText.Visibility = Configs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            List.Visibility = Configs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            Ui.Show(Status, InfoBarSeverity.Error, "Eliminazione non riuscita: " + ex.Message);
        }
    }
}
