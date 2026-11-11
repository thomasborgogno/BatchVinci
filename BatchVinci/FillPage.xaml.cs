using System.Collections.ObjectModel;
using BatchVinci.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace BatchVinci;

/// <summary>Campi di testo modificabili di una configurazione + esportazione del nuovo DRP.</summary>
public sealed partial class FillPage : Page
{
    public ObservableCollection<TextItem> Parameters { get; } = new();

    ProjectConfig _config = null!;
    List<ParameterConfig> _orphans = new();
    readonly DispatcherQueueTimer _saveTimer;
    bool _loading;

    public FillPage()
    {
        InitializeComponent();
        _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => SaveConfig();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        _config = (ProjectConfig)e.Parameter;
        TitleText.Text = _config.Name;
        DrpText.Text = _config.DrpPath;
        await LoadAsync();
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (_saveTimer.IsRunning) { _saveTimer.Stop(); SaveConfig(); }
    }

    async Task LoadAsync()
    {
        Parameters.Clear();
        ExportButton.IsEnabled = false;
        if (!File.Exists(_config.DrpPath))
        {
            RelocateButton.Visibility = Visibility.Visible;
            EditButton.IsEnabled = false;
            Ui.Show(Status, InfoBarSeverity.Error, "Il file DRP non è stato trovato nella posizione salvata.");
            return;
        }
        RelocateButton.Visibility = Visibility.Collapsed;
        EditButton.IsEnabled = true;

        try
        {
            _loading = true;
            Ui.Show(Status, InfoBarSeverity.Informational, "Lettura del progetto in corso…");
            var session = await ProjectSession.LoadAsync(_config.DrpPath, _config);
            _orphans = session.Orphans;
            foreach (var it in session.Items.Where(i => i.IsParameter))
            {
                it.Edited += OnItemEdited;
                Parameters.Add(it);
            }
            ExportButton.IsEnabled = Parameters.Count > 0;
            if (_orphans.Count > 0)
                Ui.Show(Status, InfoBarSeverity.Warning,
                    $"{_orphans.Count} parametri non sono più presenti nel progetto: usa «Modifica label» per sistemarli.");
            else if (Parameters.Count == 0)
                Ui.Show(Status, InfoBarSeverity.Warning, "Nessun parametro: usa «Modifica label» per sceglierne.");
            else Status.IsOpen = false;
        }
        catch (Exception ex)
        {
            Ui.Show(Status, InfoBarSeverity.Error, "Impossibile leggere il progetto: " + ex.Message);
        }
        finally { _loading = false; }
    }

    void OnItemEdited(TextItem _)
    {
        if (_loading) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    void SaveConfig()
    {
        try
        {
            _config.Parameters = Parameters.Select(ProjectSession.ToConfig).Concat(_orphans).ToList();
            App.Store.Save(_config);
        }
        catch (Exception ex)
        {
            Ui.Show(Status, InfoBarSeverity.Error, "Salvataggio configurazione non riuscito: " + ex.Message);
        }
    }

    void ResetItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TextItem it) it.Reset();
    }

    void EditLabels_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(SelectPage), _config);

    async void Relocate_Click(object sender, RoutedEventArgs e)
    {
        string? path = await Ui.PickDrpAsync();
        if (path == null) return;
        _config.DrpPath = path;
        App.Store.Save(_config);
        DrpText.Text = path;
        await LoadAsync();
    }

    async void Export_Click(object sender, RoutedEventArgs e)
    {
        SaveConfig();
        string src = _config.DrpPath;
        string? dest = await Ui.PickSaveDrpAsync(Path.GetFileNameWithoutExtension(src) + "_nuovo");
        if (dest == null) return;

        var changes = Parameters.Where(p => p.IsModified).ToDictionary(p => p.Occurrence.Key, p => p.Value);
        try
        {
            ExportButton.IsEnabled = false;
            int n = await Task.Run(() => DrpProject.Export(src, dest, changes));
            Ui.Show(Status, InfoBarSeverity.Success,
                $"Esportato {Path.GetFileName(dest)}: {n} label aggiornate. Il progetto originale non è stato modificato.");
        }
        catch (Exception ex)
        {
            Ui.Show(Status, InfoBarSeverity.Error, "Esportazione non riuscita: " + ex.Message);
        }
        finally { ExportButton.IsEnabled = true; }
    }
}
