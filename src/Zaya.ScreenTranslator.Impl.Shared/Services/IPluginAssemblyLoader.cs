using System.Reflection;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IPluginAssemblyLoader
{
    IReadOnlyList<Assembly> LoadedAssemblies { get; }

    void Process();

    void TryLoad(string dllPath);
}
