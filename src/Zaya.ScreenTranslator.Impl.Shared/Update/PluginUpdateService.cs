using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class PluginUpdateService
{
    private readonly PluginCatalogDownloader _downloader;

    private static LocalizationService Loc => LocalizationService.Instance;

    public PluginUpdateService(GitHubReleasesClient client)
    {
        _downloader = new PluginCatalogDownloader(client);
    }

    /// <summary>
    /// Purge wrong-channel / empty bootstrap, then download required (and optional updates when channel ok).
    /// Call before <see cref="Services.PluginLoader.LoadPlugins"/>.
    /// </summary>
    /// <param name="pluginsDirectory">Directory that stores plugin zip files.</param>
    /// <param name="channel">Primitives/interface channel (e.g. <c>1.0</c>) used for floating release tags.</param>
    /// <param name="updateOptional">When true, also update optional catalog entries if a newer release exists.</param>
    /// <param name="checkForUpdates">
    /// When false, only ensure required plugins are present (bootstrap / missing files);
    /// skip host-style version comparisons for already installed plugins.
    /// </param>
    /// <param name="status">Optional progress reporter for UI status text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
            var localState = LocalPluginStore.Scan(pluginsDirectory);
            var missingRequired = catalog.Any(e => e.Required
                && !File.Exists(Path.Combine(pluginsDirectory, e.Asset)));
            var needsBootstrap = localState.Count == 0
                || missingRequired
                || localState.Values.Any(m => LocalPluginStore.IsIncompatibleChannel(m, channel));

            if (!checkForUpdates && !needsBootstrap)
            {
                return new PluginUpdateResult
                {
                    Success = true,
                    DownloadedAssets = downloaded,
                };
            }

            if (needsBootstrap)
                return await BootstrapAsync(
                    catalog, pluginsDirectory, channel, updateOptional, downloaded, status, cancellationToken)
                    .ConfigureAwait(false);

            if (!checkForUpdates)
            {
                return new PluginUpdateResult
                {
                    Success = true,
                    DownloadedAssets = downloaded,
                };
            }

            try
            {
                await _downloader.UpdateFromReleasesAsync(
                        catalog, pluginsDirectory, channel, updateOptional, downloaded, status, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (RequiredPluginMissingException ex)
            {
                return new PluginUpdateResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    DownloadedAssets = downloaded,
                };
            }

            foreach (var entry in catalog.Where(e => e.Required))
            {
                if (!File.Exists(Path.Combine(pluginsDirectory, entry.Asset)))
                {
                    return new PluginUpdateResult
                    {
                        Success = false,
                        ErrorMessage = string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.RequiredMissing], entry.Asset),
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
                ErrorMessage = string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.NoNetwork], ex.Message),
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

    private async Task<PluginUpdateResult> BootstrapAsync(
        IReadOnlyList<BuiltinPluginEntry> catalog,
        string pluginsDirectory,
        string channel,
        bool updateOptional,
        List<string> downloaded,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        status?.Report(Loc[LocalizationConstants.Plugin.RemovingIncompatible]);
        var localState = LocalPluginStore.Scan(pluginsDirectory);
        LocalPluginStore.PurgeWrongChannel(pluginsDirectory, channel, localState);

        foreach (var entry in catalog.Where(e => e.Required))
        {
            status?.Report(string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.Downloading], entry.Asset));
            await _downloader.DownloadCatalogEntryAsync(entry, pluginsDirectory, channel, downloaded, cancellationToken)
                .ConfigureAwait(false);
        }

        var stillMissing = catalog.Where(e => e.Required
            && !File.Exists(Path.Combine(pluginsDirectory, e.Asset))).ToList();

        if (stillMissing.Count > 0)
        {
            return new PluginUpdateResult
            {
                Success = false,
                ErrorMessage = string.Format(
                    Loc.CurrentCulture,
                    Loc[LocalizationConstants.Plugin.RequiredInstallFailed],
                    string.Join("\n", stillMissing.Select(e => e.Asset))),
                DownloadedAssets = downloaded,
            };
        }

        if (updateOptional)
        {
            foreach (var entry in catalog.Where(e => !e.Required))
            {
                try
                {
                    status?.Report(string.Format(Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.Downloading], entry.Asset));
                    await _downloader.DownloadCatalogEntryAsync(
                            entry, pluginsDirectory, channel, downloaded, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    status?.Report(string.Format(
                        Loc.CurrentCulture, Loc[LocalizationConstants.Plugin.OptionalSkipped], entry.Asset, ex.Message));
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

    public static PluginManifest? ReadManifestFromZip(string zipPath)
        => PluginManifestReader.ReadFromZip(zipPath);
}
