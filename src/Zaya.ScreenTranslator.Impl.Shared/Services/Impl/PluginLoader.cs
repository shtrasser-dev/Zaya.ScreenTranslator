using System.Reflection;
using Zaya.Logging.Models;
using Zaya.ScreenTranslator.Layout.Impl;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Orchestrates zip extract, plugin directory load, and host-bundled registration.
/// </summary>
public sealed class PluginLoader : IPluginLoader
{
    private readonly IPluginCatalog _pluginCatalog;
    private readonly IPluginZipDirectoryScanner _pluginZipDirectoryScanner;
    private readonly IPluginDirectoryScanner _pluginDirectoryScanner;
    private readonly IPluginAssemblyLoader _pluginAssemblyLoader;
    private readonly IConfigurationPathService _configurationPathService;

    public PluginLoader(
        IPluginCatalog pluginCatalog,
        IPluginZipDirectoryScanner pluginZipDirectoryScanner,
        IPluginDirectoryScanner pluginDirectoryScanner,
        IPluginAssemblyLoader pluginAssemblyLoader,
        IConfigurationPathService configurationPathService)
    {
        _pluginCatalog = pluginCatalog;
        _pluginZipDirectoryScanner = pluginZipDirectoryScanner;
        _pluginDirectoryScanner = pluginDirectoryScanner;
        _pluginAssemblyLoader = pluginAssemblyLoader;
        _configurationPathService = configurationPathService;
    }

    public IReadOnlyList<Assembly> LoadedAssemblies => _pluginAssemblyLoader.LoadedAssemblies;

    [Log(LogLevel.Debug)]
    public void LoadPlugins()
    {
        var plugins = _configurationPathService.GetPluginsDirectory();
        if (!Directory.Exists(plugins))
            return;

        _pluginCatalog.Clear();
        Directory.CreateDirectory(_configurationPathService.GetExtractedPluginsDirectory());
        _pluginAssemblyLoader.Process();
        _pluginZipDirectoryScanner.Process();
        _pluginDirectoryScanner.Process();

        foreach (var dll in Directory.EnumerateFiles(plugins, "*.dll"))
            _pluginAssemblyLoader.TryLoad(dll);
    }

    [Log(LogLevel.Debug)]
    public void RegisterHostBundledPlugins()
    {
        var entryType = typeof(ScreenOverlayLayoutService);
        var ifaceVer = typeof(IOverlayLayoutService).Assembly.GetName().Version;
        var pluginVer = entryType.Assembly.GetName().Version;
        var ifaceThree = FormatThreePart(ifaceVer);
        var pluginThree = FormatThreePart(pluginVer) ?? ifaceThree;

        _pluginCatalog.Register(
            new PluginManifest
            {
                Id = "ScreenOverlay",
                Type = "overlaylayout",
                Interface = "Zaya.ScreenTranslator.Layout",
                InterfaceVersion = ifaceThree ?? string.Empty,
                PluginVersion = pluginThree ?? string.Empty,
                EntryPoint = entryType.FullName!,
            },
            [entryType.Assembly]);
    }

    private static string? FormatThreePart(Version? ver)
        => ver is null ? null : $"{ver.Major}.{ver.Minor}.{Math.Max(ver.Build, 0)}";
}
