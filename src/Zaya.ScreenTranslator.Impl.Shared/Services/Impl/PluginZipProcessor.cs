using Zaya.Logging.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class PluginZipProcessor : IPluginZipProcessor
{
    private readonly IPluginExtractCache _pluginExtractCache;
    private readonly IConfigurationPathService _configurationPathService;

    public PluginZipProcessor(IPluginExtractCache pluginExtractCache, IConfigurationPathService configurationPathService)
    {
        _pluginExtractCache = pluginExtractCache;
        _configurationPathService = configurationPathService;
    }

    [Log(LogLevel.Debug, LogParameters = true)]
    public void Process(string zipPath)
        => _pluginExtractCache.ExtractIfNeeded(zipPath, _configurationPathService.GetExtractedPluginsDirectory());
}
