using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace MonitorDimmer;

internal enum AppTheme
{
    System,
    Light,
    Dark
}

internal sealed class DisplayPrefs
{
    public string? Name { get; set; }
    public int ExtraDim { get; set; }
    public bool Linked { get; set; } = true;
}

internal sealed class Preset
{
    public string Name { get; set; } = string.Empty;
    public int Brightness { get; set; }
    public int Dim { get; set; }

    public static List<Preset> Defaults() =>
    [
        new() { Name = "Daylight", Brightness = 100, Dim = 0 },
        new() { Name = "Focus", Brightness = 75, Dim = 10 },
        new() { Name = "Evening", Brightness = 50, Dim = 30 },
        new() { Name = "Night", Brightness = 30, Dim = 55 }
    ];
}

internal sealed class AppSettings
{
    public const int MaxPresets = 8;
    public const int MaxNameLength = 40;

    public int Version { get; set; } = 1;
    public string Language { get; set; } = "system";
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool StartWithWindows { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool Hotkeys { get; set; } = true;
    public bool TrayHintShown { get; set; }
    public Dictionary<string, DisplayPrefs> Displays { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Preset> Presets { get; set; } = Preset.Defaults();

    public AppSettings Clone()
    {
        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, SettingsStore.JsonOptions), SettingsStore.JsonOptions)!;
        copy.Normalize();
        return copy;
    }

    public void Normalize()
    {
        if (!Language.Equals("system", StringComparison.OrdinalIgnoreCase) && !Strings.IsSupported(Language))
        {
            Language = "system";
        }

        if (!Enum.IsDefined(Theme))
        {
            Theme = AppTheme.System;
        }

        Displays = (Displays ?? []).Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair =>
            {
                var prefs = pair.Value;
                prefs.Name = Clip(prefs.Name);
                prefs.ExtraDim = Math.Clamp(prefs.ExtraDim, 0, 80);
                return prefs;
            }, StringComparer.OrdinalIgnoreCase);

        Presets = (Presets ?? []).Where(preset => preset is not null).Take(MaxPresets).Select((preset, index) => new Preset
        {
            Name = Clip(preset.Name) ?? $"Preset {index + 1}",
            Brightness = Math.Clamp(preset.Brightness, 0, 100),
            Dim = Math.Clamp(preset.Dim, 0, 80)
        }).ToList();
    }

    public static string? Clip(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(MaxNameLength, trimmed.Length)];
    }
}

internal sealed class SettingsStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorDimmer.Claude");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");
    private static readonly string LegacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorDimmer", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
                settings.Normalize();
                return settings;
            }
        }
        catch (Exception)
        {
            // A damaged file falls back to defaults rather than blocking start-up.
        }

        var fresh = ImportLegacy() ?? new AppSettings();
        fresh.Normalize();
        return fresh;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            settings.Normalize();
            Directory.CreateDirectory(Folder);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception)
        {
            // Settings are a convenience; a failed write must never crash the tray app.
        }
    }

    /// <summary>Carries presets, theme and language over from the original Monitor Dimmer.</summary>
    private static AppSettings? ImportLegacy()
    {
        try
        {
            if (!File.Exists(LegacyPath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(LegacyPath));
            var root = document.RootElement;
            var settings = new AppSettings();
            if (root.TryGetProperty("LanguageCode", out var language) && language.ValueKind == JsonValueKind.String)
            {
                settings.Language = language.GetString() ?? "system";
            }

            if (root.TryGetProperty("Theme", out var theme) && theme.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(AppTheme), theme.GetInt32()))
            {
                settings.Theme = (AppTheme)theme.GetInt32();
            }

            if (root.TryGetProperty("MinimizeOnClose", out var close) && close.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                settings.CloseToTray = close.GetBoolean();
            }

            if (root.TryGetProperty("Presets", out var presets) && presets.ValueKind == JsonValueKind.Array)
            {
                settings.Presets = presets.EnumerateArray().Select(preset => new Preset
                {
                    Name = preset.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                    Brightness = preset.TryGetProperty("HardwareBrightness", out var brightness) && brightness.TryGetInt32(out var b) ? b : 100,
                    Dim = preset.TryGetProperty("SoftwareDim", out var dim) && dim.TryGetInt32(out var d) ? d : 0
                }).ToList();
                if (settings.Presets.Count == 0)
                {
                    settings.Presets = Preset.Defaults();
                }
            }

            return settings;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal static class DevicePreferences
{
    public static bool IsDark(AppTheme preference) => preference switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => SystemUsesDarkTheme()
    };

    public static string ResolveLanguage(string code)
    {
        if (!code.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            return Strings.IsSupported(code) ? code : "en";
        }

        var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Strings.IsSupported(system) ? system : "en";
    }

    public static string SystemLanguageName => CultureInfo.CurrentUICulture.NativeName;

    private static bool SystemUsesDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The Windows accent shades used by Windows 11 itself for light and dark surfaces.</summary>
    public static (Color? ForLight, Color? ForDark) AccentColors()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 32)
            {
                Color At(int index) => Color.FromArgb(palette[index * 4], palette[index * 4 + 1], palette[index * 4 + 2]);
                return (At(4), At(1));
            }
        }
        catch (Exception)
        {
        }

        return (null, null);
    }
}

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MonitorDimmer.Claude";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <returns>null on success, otherwise the localization key of the error.</returns>
    public static string? SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return null;
            }

            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe) || Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                return "startupError";
            }

            key.SetValue(ValueName, $"\"{exe}\" --tray");
            return null;
        }
        catch (Exception)
        {
            return "startupError";
        }
    }
}
