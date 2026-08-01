using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>Scans and maintains plugin zips on disk.</summary>
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

    /// <summary>
    /// Removes zips whose <c>interfaceVersion</c> does not match the host-shipped interface NuGet.
    /// Local builds that match the host interface (e.g. Translator 1.1.x) are kept even when
    /// their update channel differs from the host app channel.
    /// </summary>
    public static void PurgeIncompatibleInterfaces(
        string pluginsDirectory,
        Dictionary<string, PluginManifest> localState)
    {
        foreach (var zip in Directory.EnumerateFiles(pluginsDirectory, "*.zip"))
        {
            var fileName = Path.GetFileName(zip);
            localState.TryGetValue(fileName, out var manifest);
            if (manifest is null)
                continue;
            if (PluginHostCompatibility.IsInterfaceCompatible(manifest))
                continue;

            try { File.Delete(zip); }
            catch { /* ignore locked */ }
        }
    }

    /// <summary>
    /// True when the plugin declares an interface version that cannot load against the host NuGet.
    /// </summary>
    public static bool IsIncompatibleWithHost(PluginManifest manifest)
        => !PluginHostCompatibility.IsInterfaceCompatible(manifest);
}
