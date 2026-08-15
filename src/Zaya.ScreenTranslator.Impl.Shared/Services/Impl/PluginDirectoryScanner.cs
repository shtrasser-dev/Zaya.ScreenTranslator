using Zaya.Logging.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class PluginDirectoryScanner : IPluginDirectoryScanner
{
    private readonly IPluginDirectoryProcessor _pluginDirectoryProcessor;
    private readonly IConfigurationPathService _configurationPathService;

    public PluginDirectoryScanner(
        IPluginDirectoryProcessor pluginDirectoryProcessor,
        IConfigurationPathService configurationPathService)
    {
        _pluginDirectoryProcessor = pluginDirectoryProcessor;
        _configurationPathService = configurationPathService;
    }

    [Log(LogLevel.Debug, LogParameters = true)]
    public void Process()
    {
        var extractRoot = _configurationPathService.GetExtractedPluginsDirectory();
        if (!Directory.Exists(extractRoot))
            return;

        foreach (var dir in Directory.EnumerateDirectories(extractRoot))
        {
            try
            {
                _pluginDirectoryProcessor.Process(dir);
            }
            catch (Exception)
            {
            }
        }
    }
}
