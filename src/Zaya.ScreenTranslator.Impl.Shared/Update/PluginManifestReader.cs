using System.IO.Compression;
using System.Text.Json;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>Reads <c>plugin.json</c> from plugin zip archives.</summary>
internal static class PluginManifestReader
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static PluginManifest? ReadFromZip(string zipPath)
    {
        if (!File.Exists(zipPath))
            return null;

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(PluginConstants.ManifestFileName)
                ?? archive.Entries.FirstOrDefault(e =>
                    string.Equals(Path.GetFileName(e.FullName), PluginConstants.ManifestFileName, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return null;

            using var stream = entry.Open();
            return JsonSerializer.Deserialize<PluginManifest>(stream, ManifestJsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
