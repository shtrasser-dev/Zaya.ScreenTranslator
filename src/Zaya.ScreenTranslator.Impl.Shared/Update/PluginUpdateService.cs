using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class PluginUpdateService : IPluginUpdateService
{
    private readonly IPluginCatalogDownloader _pluginCatalogDownloader;
    private readonly IBuiltinPluginCatalog _builtinPluginCatalog;
    private readonly ILocalPluginStore _localPluginStore;
    private readonly ILocalizationService _localizationService;
    private readonly IPluginManifestReader _pluginManifestReader;
    private readonly IConfigurationPathService _configurationPathService;

    public PluginUpdateService(
        IPluginCatalogDownloader pluginCatalogDownloader,
        IBuiltinPluginCatalog builtinPluginCatalog,
        ILocalPluginStore localPluginStore,
        ILocalizationService localizationService,
        IPluginManifestReader pluginManifestReader,
        IConfigurationPathService configurationPathService)
    {
        _pluginCatalogDownloader = pluginCatalogDownloader;
        _builtinPluginCatalog = builtinPluginCatalog;
        _localPluginStore = localPluginStore;
        _localizationService = localizationService;
        _pluginManifestReader = pluginManifestReader;
        _configurationPathService = configurationPathService;
    }

    /// <inheritdoc />
    public async Task<PluginUpdateResult> EnsurePluginsAsync(
        string channel,
        bool updateOptional = true,
        bool checkForUpdates = true,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        var pluginsDirectory = _configurationPathService.GetPluginsDirectory();
        Directory.CreateDirectory(pluginsDirectory);
        var catalog = _builtinPluginCatalog.Entries;
        var downloaded = new List<string>();

        try
        {
            var localState = _localPluginStore.Scan();
            var missingRequired = catalog.Any(e => e.Required
                && !File.Exists(Path.Combine(pluginsDirectory, e.Asset)));
            var needsBootstrap = localState.Count == 0
                || missingRequired
                || localState.Values.Any(_localPluginStore.IsIncompatibleWithHost);

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
                    catalog, channel, updateOptional, downloaded, status, cancellationToken)
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
                await _pluginCatalogDownloader.UpdateFromReleasesAsync(
                        catalog, channel, updateOptional, downloaded, status, cancellationToken)
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
                        ErrorMessage = string.Format(_localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.RequiredMissing], entry.Asset),
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
                ErrorMessage = string.Format(_localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.NoNetwork], ex.Message),
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
        string channel,
        bool updateOptional,
        List<string> downloaded,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        _ = updateOptional;
        var pluginsDirectory = _configurationPathService.GetPluginsDirectory();
        status?.Report(_localizationService[LocalizationConstants.Plugin.RemovingIncompatible]);
        var localState = _localPluginStore.Scan();
        _localPluginStore.PurgeIncompatibleInterfaces(localState);

        foreach (var entry in catalog.Where(e => e.Required))
        {
            status?.Report(string.Format(_localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.Downloading], entry.Asset));
            await _pluginCatalogDownloader.DownloadCatalogEntryAsync(entry, channel, downloaded, cancellationToken)
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
                    _localizationService.CurrentCulture,
                    _localizationService[LocalizationConstants.Plugin.RequiredInstallFailed],
                    string.Join("\n", stillMissing.Select(e => e.Asset))),
                DownloadedAssets = downloaded,
            };
        }

        foreach (var entry in catalog.Where(e => !e.Required))
        {
            if (File.Exists(Path.Combine(pluginsDirectory, entry.Asset)))
                continue;

            try
            {
                status?.Report(string.Format(_localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.Downloading], entry.Asset));
                await _pluginCatalogDownloader.DownloadCatalogEntryAsync(
                        entry, channel, downloaded, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                status?.Report(string.Format(
                    _localizationService.CurrentCulture, _localizationService[LocalizationConstants.Plugin.OptionalSkipped], entry.Asset, ex.Message));
            }
        }

        return new PluginUpdateResult
        {
            Success = true,
            DownloadedAssets = downloaded,
            RequiresRestart = false,
        };
    }

    public PluginManifest? ReadManifestFromZip(string zipPath)
        => _pluginManifestReader.ReadFromZip(zipPath);
}
