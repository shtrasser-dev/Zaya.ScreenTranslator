using System.Diagnostics;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Logging;

/// <summary>
/// Loads and seeds <see cref="LogOptions"/> from <c>log.json</c> under the host data root.
/// </summary>
public sealed class LogOptionsStore : ILogOptionsStore
{
    private readonly IJsonConfigurationService _jsonConfigurationService;
    private readonly IConfigurationPathService _configurationPathService;

    public LogOptionsStore(IJsonConfigurationService jsonConfigurationService, IConfigurationPathService configurationPathService)
    {
        _jsonConfigurationService = jsonConfigurationService;
        _configurationPathService = configurationPathService;
    }

    public string GetConfigPath() => _configurationPathService.GetLogOptionsFilePath();

    public string GetLogsDirectory() => _configurationPathService.GetLogsDirectory();

    public LogOptions LoadOrCreate()
    {
        Directory.CreateDirectory(_configurationPathService.GetRootAppDirectory());
        var path = GetConfigPath();

        if (File.Exists(path) && _jsonConfigurationService.TryRead<LogOptions>(path, out var loaded))
            return Normalize(loaded);

        var defaults = Normalize(LogOptions.CreateDefault());
        TryWrite(path, defaults);
        return defaults;
    }

    private static LogOptions Normalize(LogOptions options)
    {
        options.Level = options.ResolveLevel().ToString();
        options.MaxFileSizeBytes = options.ResolveMaxFileSizeBytes();
        options.MaxFileCount = options.ResolveMaxFileCount();
        return options;
    }

    private void TryWrite(string path, LogOptions options)
    {
        try
        {
            _jsonConfigurationService.Write(path, options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LogOptions] Failed to write {path}: {ex.Message}");
        }
    }
}
