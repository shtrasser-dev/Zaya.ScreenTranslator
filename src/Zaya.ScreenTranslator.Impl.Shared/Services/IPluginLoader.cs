using System.Reflection;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IPluginLoader
{
    IReadOnlyList<Assembly> LoadedAssemblies { get; }

    void LoadPlugins();

    void RegisterHostBundledPlugins();
}
