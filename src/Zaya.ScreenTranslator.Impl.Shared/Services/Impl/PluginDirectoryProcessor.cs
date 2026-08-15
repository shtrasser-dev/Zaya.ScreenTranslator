using Zaya.Logging.Models;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Exceptions;
using Zaya.ScreenTranslator.Impl.Shared.Update;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class PluginDirectoryProcessor : IPluginDirectoryProcessor
{
    private readonly IPluginCatalog _pluginCatalog;
    private readonly IPluginHostCompatibility _pluginHostCompatibility;
    private readonly IJsonConfigurationService _jsonConfigurationService;
    private readonly IPluginAssemblyLoader _pluginAssemblyLoader;

    public PluginDirectoryProcessor(
        IPluginCatalog pluginCatalog,
        IPluginHostCompatibility pluginHostCompatibility,
        IJsonConfigurationService jsonConfigurationService,
        IPluginAssemblyLoader pluginAssemblyLoader)
    {
        _pluginCatalog = pluginCatalog;
        _pluginHostCompatibility = pluginHostCompatibility;
        _jsonConfigurationService = jsonConfigurationService;
        _pluginAssemblyLoader = pluginAssemblyLoader;
    }

    [Log(LogLevel.Debug, LogParameters = true)]
    public void Process(string pluginDirectory)
    {
        var manifestPath = Path.Combine(pluginDirectory, PluginConstants.ManifestFileName);
        if (!_jsonConfigurationService.TryRead<PluginManifest>(manifestPath, out var manifest))
            throw new InvalidPluginManifestException(InvalidPluginManifestReason.InvalidManifest);

        if (!_pluginHostCompatibility.IsInterfaceCompatible(manifest))
            throw new InvalidPluginManifestException(InvalidPluginManifestReason.IncompatibleInterface);

        var loadedBefore = _pluginAssemblyLoader.LoadedAssemblies.Count;

        foreach (var dll in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            _pluginAssemblyLoader.TryLoad(dll);

        foreach (var dll in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories))
        {
            if (Path.GetDirectoryName(dll) == pluginDirectory)
                continue;

            _pluginAssemblyLoader.TryLoad(dll);
        }

        var zipAssemblies = _pluginAssemblyLoader.LoadedAssemblies.Skip(loadedBefore).ToList();
        var search = zipAssemblies.Count > 0
            ? (IReadOnlyList<System.Reflection.Assembly>)zipAssemblies
            : _pluginAssemblyLoader.LoadedAssemblies;
        _pluginCatalog.Register(manifest, search);
    }
}
