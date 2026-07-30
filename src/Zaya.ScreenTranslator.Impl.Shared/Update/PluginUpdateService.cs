using System.IO.Compression;
using System.Text.Json;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class PluginUpdateService
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly GitHubReleasesClient _client;

    public PluginUpdateService(GitHubReleasesClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Purge wrong-channel / empty bootstrap, then download required (and optional updates when channel ok).
    /// Call before <see cref="PluginLoader.LoadPlugins"/>.
    /// </summary>
    /// <param name="checkForUpdates">
    /// When false, only ensure required plugins are present (bootstrap / missing files);
    /// skip host-style version comparisons for already installed plugins.
    /// </param>
    public async Task<PluginUpdateResult> EnsurePluginsAsync(
        string pluginsDirectory,
        string channel,
        bool updateOptional = true,
        bool checkForUpdates = true,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(pluginsDirectory);
        var catalog = BuiltinPluginCatalog.Entries;
        var downloaded = new List<string>();

        try
        {
            var localState = ScanLocalPlugins(pluginsDirectory);
            var missingRequired = catalog.Any(e => e.Required
                && !File.Exists(Path.Combine(pluginsDirectory, e.Asset)));
            var needsBootstrap = localState.Count == 0
                || missingRequired
                || localState.Values.Any(m => !string.Equals(m.PrimitivesChannel, channel, StringComparison.Ordinal));

            if (!checkForUpdates && !needsBootstrap)
            {
                return new PluginUpdateResult
                {
                    Success = true,
                    DownloadedAssets = downloaded,
                };
            }

            if (needsBootstrap)
            {
                status?.Report("Removing incompatible plugins…");
                PurgeWrongChannel(pluginsDirectory, channel, localState);
                localState = ScanLocalPlugins(pluginsDirectory);

                foreach (var entry in catalog.Where(e => e.Required))
                {
                    status?.Report($"Downloading {entry.Asset}…");
                    await DownloadCatalogEntryAsync(entry, pluginsDirectory, channel, downloaded, cancellationToken)
                        .ConfigureAwait(false);
                }

                localState = ScanLocalPlugins(pluginsDirectory);
                var stillMissing = catalog.Where(e => e.Required
                    && !File.Exists(Path.Combine(pluginsDirectory, e.Asset))).ToList();

                if (stillMissing.Count > 0)
                {
                    return new PluginUpdateResult
                    {
                        Success = false,
                        ErrorMessage =
                            "Required plugins could not be installed. Check your network connection and try again.\n"
                            + string.Join("\n", stillMissing.Select(e => e.Asset)),
                        DownloadedAssets = downloaded,
                    };
                }

                // Optional plugins (Google/Yandex, …): best-effort — failure must not block startup
                if (updateOptional)
                {
                    foreach (var entry in catalog.Where(e => !e.Required))
                    {
                        try
                        {
                            status?.Report($"Downloading {entry.Asset}…");
                            await DownloadCatalogEntryAsync(entry, pluginsDirectory, channel, downloaded, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            status?.Report($"Optional plugin skipped ({entry.Asset}): {ex.Message}");
                        }
                    }
                }

                return new PluginUpdateResult
                {
                    Success = true,
                    DownloadedAssets = downloaded,
                    RequiresRestart = false,
                };
            }

            if (!checkForUpdates)
            {
                return new PluginUpdateResult
                {
                    Success = true,
                    DownloadedAssets = downloaded,
                };
            }

            // Channel OK — update when remote newer or asset missing
            var byRepo = catalog.GroupBy(e => e.Repo, StringComparer.OrdinalIgnoreCase);
            foreach (var group in byRepo)
            {
                GitHubReleaseInfo? release;
                try
                {
                    release = await _client.GetChannelLatestAsync(group.Key, channel, "plugin-v", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Offline with existing plugins: continue
                    status?.Report($"Update check failed for {group.Key}: {ex.Message}");
                    continue;
                }

                if (release is null)
                    continue;

                foreach (var entry in group)
                {
                    if (!updateOptional && !entry.Required)
                        continue;

                    var zipPath = Path.Combine(pluginsDirectory, entry.Asset);
                    var localManifest = ReadManifestFromZip(zipPath);
                    var localVersion = Version.TryParse(localManifest?.PluginVersion, out var lv) ? lv : null;
                    var remoteVersion = ReleaseVersionParser.ResolveAssetVersion(release, entry.Asset);

                    var assetMissing = !File.Exists(zipPath);
                    var remoteNewer = remoteVersion is not null
                        && (localVersion is null || remoteVersion > localVersion);

                    if (!assetMissing && !remoteNewer)
                        continue;

                    var asset = release.Assets.FirstOrDefault(a =>
                        string.Equals(a.Name, entry.Asset, StringComparison.OrdinalIgnoreCase));
                    if (asset is null || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                    {
                        if (entry.Required && assetMissing)
                        {
                            return new PluginUpdateResult
                            {
                                Success = false,
                                ErrorMessage = $"Required asset '{entry.Asset}' not found in {release.TagName}.",
                                DownloadedAssets = downloaded,
                            };
                        }

                        continue;
                    }

                    status?.Report($"Updating {entry.Asset}…");
                    await _client.DownloadAssetAsync(asset.BrowserDownloadUrl, zipPath, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    downloaded.Add(entry.Asset);
                }
            }

            // Final required check
            foreach (var entry in catalog.Where(e => e.Required))
            {
                if (!File.Exists(Path.Combine(pluginsDirectory, entry.Asset)))
                {
                    return new PluginUpdateResult
                    {
                        Success = false,
                        ErrorMessage = $"Required plugin missing: {entry.Asset}",
                        DownloadedAssets = downloaded,
                    };
                }
            }

            return new PluginUpdateResult
            {
                Success = true,
                DownloadedAssets = downloaded,
                RequiresRestart = downloaded.Count > 0,
            };
        }
        catch (HttpRequestException ex)
        {
            var hasRequired = catalog.Where(e => e.Required)
                .All(e => File.Exists(Path.Combine(pluginsDirectory, e.Asset)));
            if (hasRequired)
            {
                return new PluginUpdateResult
                {
                    Success = true,
                    ErrorMessage = null,
                    DownloadedAssets = downloaded,
                };
            }

            return new PluginUpdateResult
            {
                Success = false,
                ErrorMessage = "No network and required plugins are not installed.\n" + ex.Message,
                DownloadedAssets = downloaded,
            };
        }
        catch (Exception ex)
        {
            return new PluginUpdateResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                DownloadedAssets = downloaded,
            };
        }
    }

    private async Task DownloadCatalogEntryAsync(
        BuiltinPluginEntry entry,
        string pluginsDirectory,
        string channel,
        List<string> downloaded,
        CancellationToken cancellationToken)
    {
        var release = await _client.GetChannelLatestAsync(entry.Repo, channel, "plugin-v", cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No release for channel {channel} in {entry.Repo}.");

        var asset = release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, entry.Asset, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Asset '{entry.Asset}' missing from {release.TagName}.");

        var zipPath = Path.Combine(pluginsDirectory, entry.Asset);
        await _client.DownloadAssetAsync(asset.BrowserDownloadUrl, zipPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        downloaded.Add(entry.Asset);
    }

    private static Dictionary<string, PluginManifest> ScanLocalPlugins(string pluginsDirectory)
    {
        var result = new Dictionary<string, PluginManifest>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(pluginsDirectory))
            return result;

        foreach (var zip in Directory.EnumerateFiles(pluginsDirectory, "*.zip"))
        {
            var manifest = ReadManifestFromZip(zip);
            if (manifest is null)
                continue;
            result[Path.GetFileName(zip)] = manifest;
        }

        return result;
    }

    private static void PurgeWrongChannel(
        string pluginsDirectory,
        string channel,
        Dictionary<string, PluginManifest> localState)
    {
        foreach (var zip in Directory.EnumerateFiles(pluginsDirectory, "*.zip"))
        {
            var fileName = Path.GetFileName(zip);
            localState.TryGetValue(fileName, out var manifest);
            var channelOk = manifest is not null
                && !string.IsNullOrEmpty(manifest.PrimitivesChannel)
                && string.Equals(manifest.PrimitivesChannel, channel, StringComparison.Ordinal);

            if (channelOk)
                continue;

            try { File.Delete(zip); }
            catch { /* ignore locked */ }
        }
    }

    public static PluginManifest? ReadManifestFromZip(string zipPath)
    {
        if (!File.Exists(zipPath))
            return null;

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry("plugin.json")
                ?? archive.Entries.FirstOrDefault(e =>
                    string.Equals(Path.GetFileName(e.FullName), "plugin.json", StringComparison.OrdinalIgnoreCase));
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
