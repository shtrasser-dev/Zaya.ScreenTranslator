using System.Reflection;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IPluginHostCompatibility
{
    Assembly? ResolveHostInterfaceAssembly(string interfaceName);

    string? ResolveUpdateChannel(string interfaceName);

    string? ResolveUpdateChannel(BuiltinPluginEntry entry);

    bool IsInterfaceCompatible(PluginManifest manifest);
}
