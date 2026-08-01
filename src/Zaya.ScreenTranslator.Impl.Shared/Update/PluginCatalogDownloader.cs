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
        string fallbackChannel,
        List<string> downloaded,
        CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(pluginsDirectory, entry.Asset);

        var release = await ResolveReleaseForAssetAsync(entry, fallbackChannel, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No release containing '{entry.Asset}' in {entry.Repo}.");

        var asset = FindAsset(release, entry.Asset)
            ?? throw new InvalidOperationException($"Asset '{entry.Asset}' missing from {release.TagName}.");

        if (ShouldKeepLocal(zipPath, release, entry.Asset))
            return;

        await _client.DownloadAssetAsync(asset.BrowserDownloadUrl, zipPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var downloadedManifest = PluginManifestReader.ReadFromZip(zipPath);
        if (downloadedManifest is not null
            && LocalPluginStore.IsIncompatibleWithHost(downloadedManifest))
        {
            try { File.Delete(zipPath); }
            catch { /* ignore */ }
            throw new InvalidOperationException(
                $"Downloaded '{entry.Asset}' interface {downloadedManifest.InterfaceVersion} is incompatible with the host.");
        }

        downloaded.Add(entry.Asset);
    }

    public async Task UpdateFromReleasesAsync(
        IReadOnlyList<BuiltinPluginEntry> catalog,
        string pluginsDirectory,
        string fallbackChannel,
        bool updateOptional,
        List<string> downloaded,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        foreach (var entry in catalog)
        {
            if (!updateOptional && !entry.Required)
                continue;

            var zipPath = Path.Combine(pluginsDirectory, entry.Asset);
            var assetMissing = !File.Exists(zipPath);

            GitHubReleaseInfo? release;
            try
            {
                release = await ResolveReleaseForAssetAsync(entry, fallbackChannel, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                status?.Report(string.Format(
                    Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.UpdateCheckFailed], entry.Repo, ex.Message));
                continue;
            }

            if (release is null)
            {
                if (entry.Required && assetMissing)
                {
                    throw new RequiredPluginMissingException(string.Format(
                        Loc.CurrentCulture,
                        Loc[LocalizationConstants.Plugin.RequiredAssetMissing],
                        entry.Asset,
                        "(no matching release)"));
                }

                continue;
            }

            if (!assetMissing && ShouldKeepLocal(zipPath, release, entry.Asset))
                continue;

            var remoteVersion = ReleaseVersionParser.ResolveAssetVersion(release, entry.Asset);
            var localManifest = PluginManifestReader.ReadFromZip(zipPath);
            var localVersion = Version.TryParse(localManifest?.PluginVersion, out var lv) ? lv : null;
            var remoteNewer = remoteVersion is not null
                && (localVersion is null || remoteVersion > localVersion);

            if (!assetMissing && !remoteNewer)
                continue;

            var asset = FindAsset(release, entry.Asset);
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

            var downloadedManifest = PluginManifestReader.ReadFromZip(zipPath);
            if (downloadedManifest is not null
                && LocalPluginStore.IsIncompatibleWithHost(downloadedManifest))
            {
                try { File.Delete(zipPath); }
                catch { /* ignore */ }
                status?.Report(string.Format(
                    Loc.CurrentCulture,
                    Loc[LocalizationConstants.Plugin.UpdateCheckFailed],
                    entry.Asset,
                    $"interface {downloadedManifest.InterfaceVersion} incompatible with host"));
                continue;
            }

            downloaded.Add(entry.Asset);
        }
    }

    /// <summary>
    /// Resolve the GitHub release for a catalog entry via
    /// <c>plugin-{interface}-v{channel}-latest</c> (or the newest immutable
    /// <c>plugin-{interface}-v{channel}.*</c> tag).
    /// </summary>
    private async Task<GitHubReleaseInfo?> ResolveReleaseForAssetAsync(
        BuiltinPluginEntry entry,
        string fallbackChannel,
        CancellationToken cancellationToken)
    {
        var channel = PluginHostCompatibility.ResolveUpdateChannel(entry) ?? fallbackChannel;
        var interfaceName = entry.Interface?.Trim();
        if (string.IsNullOrEmpty(interfaceName))
            return null;

        var release = await _client
            .GetPluginInterfaceChannelLatestAsync(entry.Repo, interfaceName, channel, cancellationToken)
            .ConfigureAwait(false);

        if (release is not null && FindAsset(release, entry.Asset) is not null)
            return release;

        return null;
    }

    private static GitHubReleaseAsset? FindAsset(GitHubReleaseInfo release, string assetName) =>
        release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Keep an existing local zip when its pluginVersion is greater than or equal to the remote asset version.
    /// </summary>
    private static bool ShouldKeepLocal(string zipPath, GitHubReleaseInfo release, string assetName)
    {
        if (!File.Exists(zipPath))
            return false;

        var localManifest = PluginManifestReader.ReadFromZip(zipPath);
        if (!Version.TryParse(localManifest?.PluginVersion, out var localVersion))
            return false;

        var remoteVersion = ReleaseVersionParser.ResolveAssetVersion(release, assetName);
        if (remoteVersion is null)
            return true; // unknown remote version — do not downgrade a versioned local build

        return localVersion >= remoteVersion;
    }
}

/// <summary>Signals a required plugin asset could not be resolved from a release.</summary>
internal sealed class RequiredPluginMissingException : Exception
{
    public RequiredPluginMissingException(string message) : base(message) { }
}
