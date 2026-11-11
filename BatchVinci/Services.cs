using BatchVinci.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BatchVinci;

/// <summary>Carica i testi di un drp e li associa ai parametri di una configurazione.</summary>
public sealed class ProjectSession
{
    public List<TextItem> Items { get; } = new();
    /// <summary>Parametri della configurazione non più trovati nel drp.</summary>
    public List<ParameterConfig> Orphans { get; } = new();

    public static async Task<ProjectSession> LoadAsync(string drpPath, ProjectConfig? config)
    {
        var occurrences = await Task.Run(() => DrpProject.Scan(drpPath));
        var s = new ProjectSession();
        s.Items.AddRange(occurrences.Select(o => new TextItem(o)));
        if (config == null) return s;

        var matched = new HashSet<TextItem>();
        var unused = new List<ParameterConfig>(config.Parameters);

        // 1) per chiave; 2) fallback sul testo originale (drp risalvato da Resolve)
        foreach (var cfg in config.Parameters)
        {
            var it = s.Items.FirstOrDefault(i => !matched.Contains(i) && i.Occurrence.Key == cfg.Key);
            if (it == null) continue;
            Apply(it, cfg); matched.Add(it); unused.Remove(cfg);
        }
        foreach (var cfg in unused.ToList())
        {
            var it = s.Items.FirstOrDefault(i => !matched.Contains(i) && i.Original == FusionText.NormalizeNewLines(cfg.OriginalText));
            if (it == null) continue;
            Apply(it, cfg); matched.Add(it); unused.Remove(cfg);
        }
        s.Orphans.AddRange(unused);
        return s;
    }

    static void Apply(TextItem it, ParameterConfig cfg)
    {
        it.IsParameter = true;
        it.Name = cfg.Name;
        it.Value = cfg.Value;
    }

    public static ParameterConfig ToConfig(TextItem i) => new()
    {
        Key = i.Occurrence.Key,
        Name = i.Name,
        OriginalText = i.Original,
        Value = i.Value,
    };
}

public static class Ui
{
    static nint Hwnd => WindowNative.GetWindowHandle(App.MainWindow);

    public static async Task<string?> PickDrpAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.FileTypeFilter.Add(".drp");
        var f = await picker.PickSingleFileAsync();
        return f?.Path;
    }

    public static async Task<string?> PickSaveDrpAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, Hwnd);
        picker.FileTypeChoices.Add("Progetto DaVinci Resolve", new List<string> { ".drp" });
        picker.SuggestedFileName = suggestedName;
        var f = await picker.PickSaveFileAsync();
        return f?.Path;
    }

    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string message, string primary)
    {
        var d = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = message,
            PrimaryButtonText = primary,
            CloseButtonText = "Annulla",
            DefaultButton = ContentDialogButton.Close,
        };
        return await d.ShowAsync() == ContentDialogResult.Primary;
    }

    public static void Show(InfoBar bar, InfoBarSeverity severity, string message)
    {
        bar.Severity = severity;
        bar.Message = message;
        bar.IsOpen = true;
    }
}
