using System.Text.Json;
using FreeformHelper.Infrastructure.Project;
using NLog;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Persists app-level general settings (cross-project defaults) between app runs.
/// Layer priority: Project file settings > App general settings > Built-in defaults.
/// </summary>
public sealed class AppGeneralSettingsStore
{
    public const string SettingsPathOverrideEnvironmentVariable = "FREEFORMHELPER_APP_GENERAL_SETTINGS_PATH";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppGeneralSettingsStore(string? settingsPath = null)
    {
        _settingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? BuildDefaultSettingsPath()
            : settingsPath;
    }

    public string SettingsPath => _settingsPath;

    public AppGeneralSettingsDocument? TryLoad()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return null;
            }

            var json = File.ReadAllText(_settingsPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            AppGeneralSettingsDocument? document;
            if (LooksLikeWhitelistShape(json))
            {
                document = JsonSerializer.Deserialize<AppGeneralSettingsDocument>(json, JsonOptions);
            }
            else
            {
                var legacy = JsonSerializer.Deserialize<LegacyAppGeneralSettingsDocument>(json, JsonOptions);
                if (legacy is null)
                {
                    return null;
                }

                document = new AppGeneralSettingsDocument
                {
                    SavedAtUtc = legacy.SavedAtUtc,
                    View = legacy.UiSnapshot?.View ?? new UiViewSnapshot(),
                    Import = legacy.UiSnapshot?.Import ?? new UiImportSnapshot(),
                    Behavior = new AppGeneralBehaviorSettings()
                };
            }

            if (document is null)
            {
                return null;
            }

            document.View ??= new UiViewSnapshot();
            document.Import ??= new UiImportSnapshot();
            document.Behavior ??= new AppGeneralBehaviorSettings();
            return document;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to load app general settings from '{0}'.", _settingsPath);
            return null;
        }
    }

    public void Save(AppGeneralSettingsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var dir = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        document.SchemaVersion = AppGeneralSettingsDocument.CurrentSchemaVersion;
        document.SavedAtUtc = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static string BuildDefaultSettingsPath()
    {
        var overridePath = Environment.GetEnvironmentVariable(SettingsPathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return ExpandOverridePathTemplate(overridePath.Trim());
        }

        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(appDataRoot, "FreeformHelper");
        return Path.Combine(appDir, "app-general-settings.json");
    }

    private static string ExpandOverridePathTemplate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var resolved = path
            .Replace("{pid}", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{guid}", Guid.NewGuid().ToString("N"), StringComparison.OrdinalIgnoreCase);
        return resolved;
    }

    private static bool LooksLikeWhitelistShape(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return doc.RootElement.TryGetProperty("View", out _) ||
                   doc.RootElement.TryGetProperty("Import", out _) ||
                   doc.RootElement.TryGetProperty("Behavior", out _);
        }
        catch
        {
            return false;
        }
    }
}

public sealed class AppGeneralSettingsDocument
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public UiViewSnapshot View { get; set; } = new();
    public UiImportSnapshot Import { get; set; } = new();
    public AppGeneralBehaviorSettings Behavior { get; set; } = new();
}

public sealed class AppGeneralBehaviorSettings
{
    public bool ApplyVisualPreferencesOnProjectLoad { get; set; } = true;
}

internal sealed class LegacyAppGeneralSettingsDocument
{
    public DateTimeOffset SavedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ProjectUiSnapshot UiSnapshot { get; set; } = new();
}
