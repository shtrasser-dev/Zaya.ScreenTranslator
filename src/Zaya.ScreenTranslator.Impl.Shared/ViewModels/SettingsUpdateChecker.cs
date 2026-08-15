using Avalonia.Controls;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

/// <summary>Runs host + plugin update checks for the settings UI.</summary>
internal sealed class SettingsUpdateChecker
{
    private readonly IPluginUpdateService _pluginUpdateService;
    private readonly IHostVersionChecker _hostVersionChecker;
    private readonly ILocalizationService _localizationService;

    public SettingsUpdateChecker(
        IPluginUpdateService pluginUpdateService,
        IHostVersionChecker hostVersionChecker,
        ILocalizationService localizationService)
    {
        _pluginUpdateService = pluginUpdateService;
        _hostVersionChecker = hostVersionChecker;
        _localizationService = localizationService;
    }

    public async Task<string> CheckAsync(
        Window? ownerWindow,
        ScreenTranslatorProfile editingScreenProfile,
        Action applyChanges,
        CancellationToken cancellationToken = default)
    {
        var channel = HostChannel.Current;
        var hostUpdate = await _hostVersionChecker.CheckAsync(cancellationToken).ConfigureAwait(true);
        if (hostUpdate.UpdateAvailable && !string.IsNullOrEmpty(hostUpdate.ReleaseHtmlUrl))
        {
            var open = await UpdateDialogs.ShowHostUpdateAsync(
                ownerWindow,
                _localizationService,
                hostUpdate.RemoteVersion?.ToString() ?? "?",
                hostUpdate.ReleaseName).ConfigureAwait(true);
            if (open)
                _hostVersionChecker.OpenReleasePage(hostUpdate.ReleaseHtmlUrl);
        }

        var result = await _pluginUpdateService.EnsurePluginsAsync(
            channel,
            updateOptional: true,
            checkForUpdates: true,
            cancellationToken: cancellationToken).ConfigureAwait(true);

        editingScreenProfile.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
        applyChanges();

        if (!result.Success)
            return result.ErrorMessage ?? _localizationService[LocalizationConstants.Update.Failed];

        if (result.DownloadedAssets.Count > 0)
            return _localizationService[LocalizationConstants.Update.RestartRequired];

        if (!hostUpdate.UpdateAvailable)
            return _localizationService[LocalizationConstants.Update.UpToDate];

        return _localizationService[LocalizationConstants.Update.PluginsOk];
    }
}
