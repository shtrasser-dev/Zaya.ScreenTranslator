using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>Downloads plugin assets from GitHub releases for catalog entries.</summary>
public sealed class PluginCatalogDownloader : IPluginCatalogDownloader
{
    private readonly IGitHubReleasesClient _gitHubReleasesClient;
    private readonly ILocalizationService _localizationService;
    private readonly ILocalPluginStore _localPluginStore;
    private readonly IPluginHostCompatibility _pluginHostCompatibility;
    private readonly IPluginManifestReader _pluginManifestReader;
    private readonly IConfigurationPathService _configurationPathService;

    public PluginCatalogDownloader(
        IGitHubReleasesClient gitHubReleasesClient,
        ILocalizationService localizationService,
        ILocalPluginStore localPluginStore,
        IPluginHostCompatibility pluginHostCompatibility,
        IPluginManifestReader pluginManifestReader,
        IConfigurationPathService configurationPathService)
    {
        _gitHubReleasesClient = gitHubReleasesClient;
        _localizationService = localizationService;
        _localPluginStore = localPluginStore;
        _pluginHostCompatibility = pluginHostCompatibility;
        _pluginManifestReader = pluginManifestReader;
        _configurationPathService = configurationPathService;
    }

    public async Task DownloadCatalogEntryAsync(
        BuiltinPluginEntry entry,
        string fallbackChannel,
        List<string> downloaded,
        CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(_configurationPathService.GetPluginsDirectory(), entry.Asset);

        var release = await ResolveReleaseForAssetAsync(entry, fallbackChannel, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No release containing '{entry.Asset}' in {entry.Repo}.");

        var asset = FindAsset(release, entry.Asset)
            ?? throw new InvalidOperationException($"Asset '{entry.Asset}' missing from {release.TagName}.");

        if (ShouldKeepLocal(zipPath, release, entry.Asset))
            return;

        await _gitHubReleasesClient.DownloadAssetAsync(asset.BrowserDownloadUrl, zipPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var downloadedManifest = _pluginManifestReader.ReadFromZip(zipPath);
        if (downloadedManifest is not null
            && _localPluginStore.IsIncompatibleWithHost(downloadedManifest))
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
        string fallbackChannel,
        bool updateOptional,
        List<string> downloaded,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        var pluginsDirectory = _configurationPathService.GetPluginsDirectory();
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
                    _localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.UpdateCheckFailed], entry.Repo, ex.Message));
                continue;
            }

            if (release is null)
            {
                if (entry.Required && assetMissing)
                {
                    throw new RequiredPluginMissingException(string.Format(
                        _localizationService.CurrentCulture,
                        _localizationService[LocalizationConstants.Plugin.RequiredAssetMissing],
                        entry.Asset,
                        "(no matching release)"));
                }

                continue;
            }

            if (!assetMissing && ShouldKeepLocal(zipPath, release, entry.Asset))
                continue;

            var remoteVersion = ReleaseVersionParser.ResolveAssetVersion(release, entry.Asset);
            var localManifest = _pluginManifestReader.ReadFromZip(zipPath);
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
                        _localizationService.CurrentCulture,
                        _localizationService[LocalizationConstants.Plugin.RequiredAssetMissing],
                        entry.Asset,
                        release.TagName));
                }

                continue;
            }

            status?.Report(string.Format(_localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.Updating], entry.Asset));
            await _gitHubReleasesClient.DownloadAssetAsync(asset.BrowserDownloadUrl, zipPath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var downloadedManifest = _pluginManifestReader.ReadFromZip(zipPath);
            if (downloadedManifest is not null
                && _localPluginStore.IsIncompatibleWithHost(downloadedManifest))
            {
                try { File.Delete(zipPath); }
                catch { /* ignore */ }
                status?.Report(string.Format(
                    _localizationService.CurrentCulture,
                    _localizationService[LocalizationConstants.Plugin.UpdateCheckFailed],
                    entry.Asset,
                    $"interface {downloadedManifest.InterfaceVersion} incompatible with host"));
                continue;
            }

            downloaded.Add(entry.Asset);
        }
    }

    private async Task<GitHubReleaseInfo?> ResolveReleaseForAssetAsync(
        BuiltinPluginEntry entry,
        string fallbackChannel,
        CancellationToken cancellationToken)
    {
        var channel = _pluginHostCompatibility.ResolveUpdateChannel(entry) ?? fallbackChannel;
        var interfaceName = entry.Interface?.Trim();
        if (string.IsNullOrEmpty(interfaceName))
            return null;

        var release = await _gitHubReleasesClient
            .GetPluginInterfaceChannelLatestAsync(entry.Repo, interfaceName, channel, cancellationToken)
            .ConfigureAwait(false);

        if (release is not null && FindAsset(release, entry.Asset) is not null)
            return release;

        return null;
    }

    private static GitHubReleaseAsset? FindAsset(GitHubReleaseInfo release, string assetName) =>
        release.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));

    private bool ShouldKeepLocal(string zipPath, GitHubReleaseInfo release, string assetName)
    {
        if (!File.Exists(zipPath))
            return false;

        var localManifest = _pluginManifestReader.ReadFromZip(zipPath);
        if (!Version.TryParse(localManifest?.PluginVersion, out var localVersion))
            return false;

        var remoteVersion = ReleaseVersionParser.ResolveAssetVersion(release, assetName);
        if (remoteVersion is null)
            return true;

        return localVersion >= remoteVersion;
    }
}

/// <summary>Signals a required plugin asset could not be resolved from a release.</summary>
internal sealed class RequiredPluginMissingException : Exception
{
    public RequiredPluginMissingException(string message) : base(message) { }
}
