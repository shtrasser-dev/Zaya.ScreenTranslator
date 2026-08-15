using Zaya.Logging.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class PluginZipDirectoryScanner : IPluginZipDirectoryScanner
{
    private readonly IPluginZipProcessor _pluginZipProcessor;
    private readonly IPluginExtractCache _pluginExtractCache;
    private readonly IConfigurationPathService _configurationPathService;

    public PluginZipDirectoryScanner(
        IPluginZipProcessor pluginZipProcessor,
        IPluginExtractCache pluginExtractCache,
        IConfigurationPathService configurationPathService)
    {
        _pluginZipProcessor = pluginZipProcessor;
        _pluginExtractCache = pluginExtractCache;
        _configurationPathService = configurationPathService;
    }

    [Log(LogLevel.Debug, LogParameters = true)]
    public void Process()
    {
        var pluginsPath = _configurationPathService.GetPluginsDirectory();
        var extractRoot = _configurationPathService.GetExtractedPluginsDirectory();

        if (!Directory.Exists(pluginsPath))
            return;

        Directory.CreateDirectory(extractRoot);

        foreach (var zip in Directory.EnumerateFiles(pluginsPath, "*.zip"))
        {
            try
            {
                _pluginZipProcessor.Process(zip);
            }
            catch (Exception)
            {
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(extractRoot))
        {
            var dirName = Path.GetFileName(dir);
            var expectedZip = Path.Combine(pluginsPath, dirName + ".zip");
            if (File.Exists(expectedZip))
                continue;

            try
            {
                _pluginExtractCache.DeleteDirectory(dir);
            }
            catch (Exception)
            {
            }
        }
    }
}
