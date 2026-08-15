using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface ILocalPluginStore
{
    Dictionary<string, PluginManifest> Scan();

    void PurgeIncompatibleInterfaces(Dictionary<string, PluginManifest> localState);

    bool IsIncompatibleWithHost(PluginManifest manifest);
}
