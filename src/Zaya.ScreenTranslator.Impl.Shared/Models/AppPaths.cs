namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Well-known filesystem locations for ScreenTranslator under %AppData%.
/// </summary>
public static class AppPaths
{
    public static string BaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Zaya", "ScreenTranslator");

    /// <summary>
    /// Per-plugin cache directory: <c>%AppData%\Zaya\ScreenTranslator\temp\cache\{engineId}</c>.
    /// </summary>
    public static string GetPluginCacheDirectory(string engineId)
        => Path.Combine(BaseDirectory, "temp", "cache", engineId);
}
