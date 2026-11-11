using System.ComponentModel;
using System.Runtime.CompilerServices;
using BatchVinci.Core;

namespace BatchVinci;

/// <summary>Un testo trovato nel progetto; se IsParameter è true è editabile ed esportabile.</summary>
public sealed class TextItem : INotifyPropertyChanged
{
    bool _isParameter;
    string _name;
    string _value;
    bool _fromEditor;

    public TextItem(TextOccurrence occ)
    {
        Occurrence = occ;
        _value = FusionText.NormalizeNewLines(occ.Text);
        _name = DefaultName(occ);
    }

    public TextOccurrence Occurrence { get; }
    public string Original => FusionText.NormalizeNewLines(Occurrence.Text);

    public bool IsParameter { get => _isParameter; set => Set(ref _isParameter, value); }
    public string Name { get => _name; set => Set(ref _name, value); }

    /// <summary>Valore con a capo LF, lo stesso formato del drp.</summary>
    public string Value
    {
        get => _value;
        set
        {
            value = FusionText.NormalizeNewLines(value);
            if (!Set(ref _value, value)) return;
            OnChanged(nameof(IsModified));
            OnChanged(nameof(ModifiedBadge));
            if (!_fromEditor) OnChanged(nameof(EditorText));
        }
    }

    /// <summary>Testo per la TextBox di WinUI, che usa CR come ritorno a capo.</summary>
    public string EditorText
    {
        get => _value.Replace("\n", "\r");
        set { _fromEditor = true; try { Value = value; } finally { _fromEditor = false; } }
    }

    public bool IsModified => Value != Original;
    public string ModifiedBadge => IsModified ? "● modificato" : "";

    public string Where =>
        $"{Occurrence.Sequence}  ·  {Occurrence.Template}  ·  frame {Occurrence.StartFrame}";

    public string Preview => Original.Replace("\n", " ⏎ ");

    public void Reset() => Value = Original;

    static string DefaultName(TextOccurrence o)
    {
        string first = o.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        if (first.Length > 28) first = first[..28] + "…";
        return first.Length > 0 ? first : o.Template;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<TextItem>? Edited;

    bool Set<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnChanged(prop);
        Edited?.Invoke(this);
        return true;
    }

    void OnChanged(string? prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
