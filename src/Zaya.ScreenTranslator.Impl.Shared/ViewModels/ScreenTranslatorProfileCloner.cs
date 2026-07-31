using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

/// <summary>Deep-copies screen translator UI profile settings.</summary>
internal static class ScreenTranslatorProfileCloner
{
    public static ScreenTranslatorProfile Clone(ScreenTranslatorProfile source)
    {
        return new ScreenTranslatorProfile
        {
            MainWindow = CloneWindowSettings(source.MainWindow),
            SettingsWindow = CloneWindowSettings(source.SettingsWindow),
            TextWindow = CloneWindowSettings(source.TextWindow),
            UiCulture = source.UiCulture,
            Theme = source.Theme,
            TargetLanguage = source.TargetLanguage,
            LastActiveProfileName = source.LastActiveProfileName,
            DisplayMode = source.DisplayMode,
            CheckUpdatesOnStartup = source.CheckUpdatesOnStartup,
            LastUpdateCheckUtc = source.LastUpdateCheckUtc,
        };
    }

    public static WindowSettings CloneWindowSettings(WindowSettings ws)
    {
        return new WindowSettings
        {
            X = ws.X,
            Y = ws.Y,
            Width = ws.Width,
            Height = ws.Height,
            Topmost = ws.Topmost,
        };
    }
}
