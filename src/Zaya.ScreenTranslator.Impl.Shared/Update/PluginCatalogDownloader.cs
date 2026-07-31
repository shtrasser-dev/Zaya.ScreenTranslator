using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>Downloads plugin assets from GitHub releases for catalog entries.</summary>
internal sealed class PluginCatalogDownloader
{
    private readonly GitHubReleasesClient _client;

    private static LocalizationService Loc => LocalizationService.Instance;

    public PluginCatalogDownloader(GitHubReleasesClient client)
    {
        _client = client;
    }

    public async Task DownloadCatalogEntryAsync(
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

    public async Task UpdateFromReleasesAsync(
        IReadOnlyList<BuiltinPluginEntry> catalog,
        string pluginsDirectory,
        string channel,
        bool updateOptional,
        List<string> downloaded,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
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
                status?.Report(string.Format(
                    Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.UpdateCheckFailed], group.Key, ex.Message));
                continue;
            }

            if (release is null)
                continue;

            foreach (var entry in group)
            {
                if (!updateOptional && !entry.Required)
                    continue;

                var zipPath = Path.Combine(pluginsDirectory, entry.Asset);
                var localManifest = PluginManifestReader.ReadFromZip(zipPath);
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
                        throw new RequiredPluginMissingException(string.Format(
                            Loc.CurrentCulture,
                            Loc[LocalizationConstants.Plugin.RequiredAssetMissing],
                            entry.Asset,
                            release.TagName));
                    }

                    continue;
                }

                status?.Report(string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.Updating], entry.Asset));
                await _client.DownloadAssetAsync(asset.BrowserDownloadUrl, zipPath, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                downloaded.Add(entry.Asset);
            }
        }
    }
}

/// <summary>Signals a required plugin asset could not be resolved from a release.</summary>
internal sealed class RequiredPluginMissingException : Exception
{
    public RequiredPluginMissingException(string message) : base(message) { }
}
