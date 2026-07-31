using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>Scans and maintains plugin zips on disk for the active update channel.</summary>
internal static class LocalPluginStore
{
    public static Dictionary<string, PluginManifest> Scan(string pluginsDirectory)
    {
        var result = new Dictionary<string, PluginManifest>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(pluginsDirectory))
            return result;

        foreach (var zip in Directory.EnumerateFiles(pluginsDirectory, "*.zip"))
        {
            var manifest = PluginManifestReader.ReadFromZip(zip);
            if (manifest is null)
                continue;
            result[Path.GetFileName(zip)] = manifest;
        }

        return result;
    }

    public static void PurgeWrongChannel(
        string pluginsDirectory,
        string channel,
        Dictionary<string, PluginManifest> localState)
    {
        foreach (var zip in Directory.EnumerateFiles(pluginsDirectory, "*.zip"))
        {
            var fileName = Path.GetFileName(zip);
            localState.TryGetValue(fileName, out var manifest);
            if (manifest is null || !IsIncompatibleChannel(manifest, channel))
                continue;

            try { File.Delete(zip); }
            catch { /* ignore locked */ }
        }
    }

    /// <summary>
    /// True only when the plugin declares (or implies) a channel and it differs from the host channel.
    /// Missing channel is not treated as incompatible — avoids re-downloading every startup.
    /// </summary>
    public static bool IsIncompatibleChannel(PluginManifest manifest, string hostChannel)
    {
        var pluginChannel = manifest.ResolveUpdateChannel();
        if (string.IsNullOrEmpty(pluginChannel))
            return false;
        return !string.Equals(pluginChannel, hostChannel, StringComparison.Ordinal);
    }
}
