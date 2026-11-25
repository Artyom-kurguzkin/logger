using System.Text.Json;

namespace AkLogger.AppState;

public sealed record AppSettings(
    string Theme = "light",         // light (default) | dark | system
    bool MenuExpanded = false);     // the side menu shows labels

/// <summary>Per-user settings in one JSON file (%APPDATA%/AkLogger/settings.json). A file that is missing or unreadable means the defaults.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AkLogger", "settings.json");

    private readonly string path;

    public SettingsStore(string path)
    {
        this.path = path;
        Current = Load(path);
    }

    public AppSettings Current { get; private set; }

    public event Action? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(Current, Json));
        Changed?.Invoke();
    }

    private static AppSettings Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new() : new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
}
