using System.Text.Json;

namespace BatchVinci.Core;

public sealed class ParameterConfig
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string OriginalText { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Configurazione di un progetto DRP: quali label sono parametri e i loro valori.</summary>
public sealed class ProjectConfig
{
    public int Version { get; set; } = 2;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string DrpPath { get; set; } = "";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<ParameterConfig> Parameters { get; set; } = new();
}

/// <summary>Archivio locale delle configurazioni: %LOCALAPPDATA%\BatchVinci\configs\*.json</summary>
public sealed class ConfigStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string Folder { get; }

    public ConfigStore(string? folder = null)
    {
        Folder = folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatchVinci", "configs");
        Directory.CreateDirectory(Folder);
    }

    string PathOf(string id) => Path.Combine(Folder, id + ".json");

    public List<ProjectConfig> LoadAll()
    {
        var list = new List<ProjectConfig>();
        foreach (var f in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var c = JsonSerializer.Deserialize<ProjectConfig>(File.ReadAllText(f), Json);
                if (c != null && c.Id.Length > 0) list.Add(c);
            }
            catch (JsonException) { /* file non valido: ignorato */ }
        }
        return list.OrderByDescending(c => c.UpdatedUtc).ToList();
    }

    public void Save(ProjectConfig config)
    {
        config.UpdatedUtc = DateTime.UtcNow;
        string p = PathOf(config.Id);
        File.WriteAllText(p + ".tmp", JsonSerializer.Serialize(config, Json));
        File.Move(p + ".tmp", p, overwrite: true);
    }

    public void Delete(ProjectConfig config)
    {
        string p = PathOf(config.Id);
        if (File.Exists(p)) File.Delete(p);
    }
}
