using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BatchVinci;

public sealed partial class HomePage : Page
{
    public HomePage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        int n = App.Store.LoadAll().Count;
        CountText.Text = n switch { 0 => "Nessuna configurazione salvata", 1 => "1 configurazione salvata", _ => $"{n} configurazioni salvate" };
    }

    void Import_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SelectPage), null);
    void Existing_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(ConfigsPage));
}
