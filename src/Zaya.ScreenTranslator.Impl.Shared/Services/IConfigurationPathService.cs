using Zaya.PluginManager.Impl.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Well-known host filesystem locations. Relative layout is fixed; only the root changes
/// (AppData today, exe directory for a future standalone mode).
/// </summary>
public interface IConfigurationPathService : IPluginPaths
{
    string GetRootAppDirectory();

    string GetCachePluginsDirectory();

    string GetProfilesDirectory();

    string GetSettingsFilePath();

    string GetLogConfigFilePath();

    string GetLogsDirectory();

    string GetLibDirectory();
}
