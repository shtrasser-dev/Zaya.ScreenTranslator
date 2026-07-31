using Zaya.ScreenTranslator.Impl.Shared.Constants;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Per‑window position, size, and topmost flag.
/// </summary>
public sealed class WindowSettings
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Topmost { get; set; }
}

/// <summary>
/// App‑level settings stored in a separate settings.json.
/// NOT part of a translation profile — these are global.
/// </summary>
public sealed class ScreenTranslatorProfile
{
    public WindowSettings MainWindow { get; set; } = new() { Width = 920, Height = 340 };
    public WindowSettings SettingsWindow { get; set; } = new() { Width = 920, Height = 640 };
    public WindowSettings TextWindow { get; set; } = new() { Width = 480, Height = 320, Topmost = true };
    public string UiCulture { get; set; } = "en";
    public string Theme { get; set; } = AppConstants.Theme.Light;
    public string TargetLanguage { get; set; } = "en";
    public string LastActiveProfileName { get; set; } = "Default";
    /// <summary>Output mode: <c>textWindow</c> or <c>overlay</c>.</summary>
    public string DisplayMode { get; set; } = AppConstants.DisplayMode.TextWindow;
    /// <summary>When true, check host/plugin updates on application startup.</summary>
    public bool CheckUpdatesOnStartup { get; set; } = true;
    /// <summary>UTC time of the last host/plugin update check (startup or manual).</summary>
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
}
