using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Runs startup stages: plugin updates (stage 1) and plugin loading (stage 2).
/// </summary>
public sealed class ApplicationBootstrap : IApplicationBootstrap
{
    private readonly IPluginUpdateService _pluginUpdateService;
    private readonly IHostVersionChecker _hostVersionChecker;
    private readonly IPluginLoader _pluginLoader;
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly ILocalizationService _localizationService;

    public ApplicationBootstrap(
        IPluginUpdateService pluginUpdateService,
        IHostVersionChecker hostVersionChecker,
        IPluginLoader pluginLoader,
        IApplicationProfileService applicationProfileService,
        ILocalizationService localizationService)
    {
        _pluginUpdateService = pluginUpdateService;
        _hostVersionChecker = hostVersionChecker;
        _pluginLoader = pluginLoader;
        _applicationProfileService = applicationProfileService;
        _localizationService = localizationService;
    }

    public async Task<BootstrapResult> RunAsync(
        string channel,
        bool checkUpdatesOnStartup,
        IProgress<string>? status,
        CancellationToken cancellationToken = default)
    {
        HostUpdateInfo? hostUpdate = null;
        if (checkUpdatesOnStartup)
            hostUpdate = await _hostVersionChecker.CheckAsync(cancellationToken).ConfigureAwait(false);

        status?.Report(checkUpdatesOnStartup
            ? _localizationService[LocalizationConstants.Update.UpdatingPlugins]
            : _localizationService[LocalizationConstants.Update.PreparingPlugins]);

        var pluginResult = await _pluginUpdateService.EnsurePluginsAsync(
            channel,
            updateOptional: checkUpdatesOnStartup,
            checkForUpdates: checkUpdatesOnStartup,
            status: status,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (checkUpdatesOnStartup)
        {
            var profile = _applicationProfileService.LoadScreenTranslatorProfile();
            profile.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _applicationProfileService.SaveScreenTranslatorProfile(profile);
        }

        if (!pluginResult.Success)
        {
            return new BootstrapResult
            {
                Success = false,
                ErrorTitle = _localizationService[LocalizationConstants.Update.PluginsRequiredTitle],
                ErrorMessage = pluginResult.ErrorMessage ?? _localizationService[LocalizationConstants.Update.PluginsRequiredBody],
                HostUpdate = hostUpdate,
            };
        }

        _pluginLoader.LoadPlugins();
        _pluginLoader.RegisterHostBundledPlugins();

        return new BootstrapResult
        {
            Success = true,
            HostUpdate = hostUpdate,
        };
    }
}
