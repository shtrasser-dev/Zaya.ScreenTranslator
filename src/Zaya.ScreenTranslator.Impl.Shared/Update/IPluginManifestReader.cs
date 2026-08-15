using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IPluginManifestReader
{
    PluginManifest? ReadFromZip(string zipPath);
}
