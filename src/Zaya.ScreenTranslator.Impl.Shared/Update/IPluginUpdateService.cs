using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IPluginUpdateService
{
    Task<PluginUpdateResult> EnsurePluginsAsync(
        string channel,
        bool updateOptional = true,
        bool checkForUpdates = true,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default);

    PluginManifest? ReadManifestFromZip(string zipPath);
}
