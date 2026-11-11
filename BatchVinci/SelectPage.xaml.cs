using System.Collections.ObjectModel;
using BatchVinci.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BatchVinci;

/// <summary>Scelta dei testi da parametrizzare: nuova importazione (parametro null) o modifica di una configurazione.</summary>
public sealed partial class SelectPage : Page
{
    public ObservableCollection<TextItem> Items { get; } = new();

    ProjectConfig? _editing;
    string? _drpPath;

    public SelectPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _editing = e.Parameter as ProjectConfig;
        if (_editing == null)
        {
            Title.Text = "Importa progetto";
            return;
        }

        Title.Text = "Modifica label della configurazione";
        PickButton.Visibility = Visibility.Collapsed;
        NameBox.Text = _editing.Name;
        await LoadAsync(_editing.DrpPath);
    }

    async void Pick_Click(object sender, RoutedEventArgs e)
    {
        string? path = await Ui.PickDrpAsync();
        if (path == null) return;
        if (NameBox.Text.Length == 0) NameBox.Text = Path.GetFileNameWithoutExtension(path);
        await LoadAsync(path);
    }

    async Task LoadAsync(string path)
    {
        try
        {
            PickButton.IsEnabled = false;
            Ui.Show(Status, InfoBarSeverity.Informational, "Lettura del progetto in corso…");
            var session = await ProjectSession.LoadAsync(path, _editing);
            _drpPath = path;
            Items.Clear();
            foreach (var it in session.Items)
            {
                it.Edited += _ => UpdateState();
                Items.Add(it);
            }
            DrpPathText.Text = path;
            FoundHeader.Text = $"Spunta le label da gestire come parametri ({Items.Count} trovate)";
            if (Items.Count == 0)
                Ui.Show(Status, InfoBarSeverity.Warning, "Nessun testo Text+/Fusion Title trovato nelle timeline di questo progetto.");
            else if (session.Orphans.Count > 0)
                Ui.Show(Status, InfoBarSeverity.Warning, $"{session.Orphans.Count} parametri salvati non sono più presenti nel progetto e verranno rimossi al salvataggio.");
            else
                Status.IsOpen = false;
            UpdateState();
        }
        catch (Exception ex)
        {
            Ui.Show(Status, InfoBarSeverity.Error, "Impossibile leggere il progetto: " + ex.Message);
        }
        finally { PickButton.IsEnabled = true; }
    }

    void NameBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateState();

    void UpdateState() =>
        SaveButton.IsEnabled = _drpPath != null && NameBox.Text.Trim().Length > 0 && Items.Any(i => i.IsParameter);

    void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_drpPath == null) return;
        try
        {
            var cfg = _editing ?? new ProjectConfig();
            cfg.Name = NameBox.Text.Trim();
            cfg.DrpPath = _drpPath;
            cfg.Parameters = Items.Where(i => i.IsParameter).Select(ProjectSession.ToConfig).ToList();
            App.Store.Save(cfg);

            if (_editing != null) Frame.GoBack();           // torna alla pagina di compilazione (ricaricata)
            else
            {
                Frame.Navigate(typeof(FillPage), cfg);
                Frame.BackStack.RemoveAt(Frame.BackStack.Count - 1);   // niente ritorno alla selezione
            }
        }
        catch (Exception ex)
        {
            Ui.Show(Status, InfoBarSeverity.Error, "Salvataggio non riuscito: " + ex.Message);
        }
    }
}
