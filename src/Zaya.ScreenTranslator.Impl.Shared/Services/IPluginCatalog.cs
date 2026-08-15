using System.Reflection;
using Zaya.Logging.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Catalog of plugin engines discovered from <c>plugin.json</c> <c>entryPoint</c> values.
/// </summary>
public interface IPluginCatalog
{
    IReadOnlyList<PluginEngineRegistration> Entries { get; }

    void Clear();

    void Register(PluginManifest manifest, IReadOnlyList<Assembly> assemblies);

    PluginEngineRegistration? Find(PluginServiceKind kind, string engineId);

    IReadOnlyList<PluginEngineRegistration> List(PluginServiceKind kind);

    object Create(Type entryType, ILoggingWrapper loggingWrapper);
}
