namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// AppData-rooted paths for ScreenTranslator. Swap root here for standalone mode.
/// </summary>
public sealed class ConfigurationPathService : IConfigurationPathService
{
    private readonly string _root;

    public ConfigurationPathService()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zaya",
            "ScreenTranslator");
    }

    public string GetRootAppDirectory() => _root;

    public string GetPluginsDirectory() => Path.Combine(_root, "plugins");

    public string GetExtractedPluginsDirectory() => Path.Combine(_root, "temp", "plugins");

    public string GetCachePluginsDirectory() => Path.Combine(_root, "temp", "cache");

    public string GetProfilesDirectory() => Path.Combine(_root, "profiles");

    public string GetSettingsFilePath() => Path.Combine(_root, "settings.json");

    public string GetLogOptionsFilePath() => Path.Combine(_root, "log.json");

    public string GetLogsDirectory() => Path.Combine(_root, "logs");

    public string GetLibDirectory() => Path.Combine(_root, "lib");
}
