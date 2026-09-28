using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaya.Logging.Impl.Microsoft.Services;
using Zaya.Logging.Impl.Services;
using Zaya.Logging.Services;
using Zaya.PluginManager.Impl;
using Zaya.PluginManager.Impl.DependencyInjection;
using Zaya.PluginManager.Impl.Services;
using Zaya.PluginManager.Impl.Update;
using Zaya.ScreenTranslator.Impl.Shared.Logging;
using Zaya.ScreenTranslator.Impl.Shared.Logging.Impl;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;

namespace Zaya.ScreenTranslator.Impl.Shared.DependencyInjection;

/// <summary>
/// Holds singleton instances created during bootstrap that are re-registered in app DI.
/// The bootstrap <see cref="IServiceProvider"/> must stay alive for the app lifetime so these
/// instances (and the shared <see cref="HttpClient"/>) are not disposed prematurely.
/// </summary>
public sealed class BootstrapTransferredServices
{
    public required IConfigurationPathService Paths { get; init; }
    public required IJsonConfigurationService JsonFileStore { get; init; }
    public required IPluginCatalog PluginCatalog { get; init; }
    public required IPluginLoader PluginLoader { get; init; }
    public required IEngineFactory EngineFactory { get; init; }
    public required ILocalizationService Localization { get; init; }
    public required ILoggingWrapper Logging { get; init; }
    public required IApplicationProfileService ProfileService { get; init; }
    public required ICaptureRegionsStore CaptureRegionsStore { get; init; }
    public required ICaptureFrameProcessor CaptureFrameProcessor { get; init; }
    public required ICaptureRegionsSnapshotService CaptureRegionsSnapshotService { get; init; }
    public required IProcessIconLoader ProcessIconLoader { get; init; }
    public required IPluginUpdateService PluginUpdateService { get; init; }
    public required IHostVersionChecker HostVersionChecker { get; init; }
    public required IGitHubReleasesClient GitHubReleasesClient { get; init; }
}

public static class BootstrapServiceRegistrar
{
    public static void Register(IServiceCollection services)
    {
        // Pre-DI: paths + log.json must load before MEL / ILoggingWrapper exist.
        var configurationPathService = new ConfigurationPathService();
        var jsonConfigurationService = new JsonConfigurationService();
        var logConfigService = new LogConfigService(jsonConfigurationService, configurationPathService);
        var logConfig = logConfigService.LoadOrCreate();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(logConfig.Level);
            if (logConfig.WriteToDebug)
                builder.AddDebug();
            if (logConfig.WriteToFile)
            {
                builder.AddProvider(new RollingFileLoggerProvider(
                    configurationPathService.GetLogsDirectory(),
                    logConfig));
            }
        });

        services.AddSingleton<ILoggingWrapper>(sp =>
        {
            var mel = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Zaya");
            return new LoggingWrapper(new MicrosoftExtensionsLogger(mel));
        });

        services.AddSingleton<IConfigurationPathService>(configurationPathService).WrapLogging<IConfigurationPathService>();
        services.AddSingleton<IJsonConfigurationService>(jsonConfigurationService).WrapLogging<IJsonConfigurationService>();
        services.AddSingleton<ILogConfigService>(logConfigService).WrapLogging<ILogConfigService>();
        services.AddSingleton<IEmbeddedResourceService, EmbeddedResourceService>().WrapLogging<IEmbeddedResourceService>();

        services.AddSingleton<HttpClient>();
        services.AddSingleton<ILocalizationService, LocalizationService>().WrapLogging<ILocalizationService>();
        services.AddSingleton<IPluginUiCulture, LocalizationPluginUiCulture>().WrapLogging<IPluginUiCulture>();
        services.AddSingleton<IApplicationProfileService, ApplicationProfileService>().WrapLogging<IApplicationProfileService>();
        services.AddSingleton<IBuiltinPluginCatalog, BuiltinPluginCatalog>().WrapLogging<IBuiltinPluginCatalog>();
        services.AddSingleton<IPluginHostCompatibility, PluginHostCompatibility>().WrapLogging<IPluginHostCompatibility>();
        services.AddSingleton<IPluginEngineProbe, ScreenTranslatorEngineProbe>();
        services.AddSingleton<IPluginPaths>(configurationPathService);
        services.AddZayaPluginManager(new PluginManagerOptions
        {
            GitHubProductName = "Zaya.ScreenTranslator",
            GitHubProductVersion = HostChannel.Current,
            LibDirectory = configurationPathService.GetLibDirectory(),
        });
        services.WrapLogging<IGitHubReleasesClient>();
        services.WrapLogging<IPluginManifestReader>();
        services.WrapLogging<ILocalPluginStore>();
        services.WrapLogging<IPluginCatalogDownloader>();
        services.WrapLogging<IPluginUpdateService>();
        services.WrapLogging<IPluginExtractCache>();
        services.WrapLogging<IPluginZipProcessor>();
        services.WrapLogging<IPluginZipDirectoryScanner>();
        services.WrapLogging<IPluginAssemblyLoader>();
        services.WrapLogging<IPluginDirectoryProcessor>();
        services.WrapLogging<IPluginDirectoryScanner>();
        services.WrapLogging<IPluginCatalog>();
        services.WrapLogging<IPluginLoader>();
        services.AddSingleton<IEngineFactoryCatalogService, EngineFactoryCatalogService>().WrapLogging<IEngineFactoryCatalogService>();
        services.AddSingleton<IHostVersionChecker, HostVersionChecker>().WrapLogging<IHostVersionChecker>();
        services.AddSingleton<IEngineFactory, EngineFactory>().WrapLogging<IEngineFactory>();
        services.AddSingleton<ICaptureRegionsStore, CaptureRegionsStore>().WrapLogging<ICaptureRegionsStore>();
        services.AddSingleton<ICaptureFrameProcessor, CaptureFrameProcessor>().WrapLogging<ICaptureFrameProcessor>();
        services.AddSingleton<ICaptureRegionsSnapshotService, CaptureRegionsSnapshotService>().WrapLogging<ICaptureRegionsSnapshotService>();
        services.AddSingleton<IProcessIconLoader, ProcessIconLoader>().WrapLogging<IProcessIconLoader>();

        services.AddSingleton<IApplicationBootstrap, ApplicationBootstrap>().WrapLogging<IApplicationBootstrap>();
    }

    public static BootstrapTransferredServices ResolveTransferred(IServiceProvider bootstrapProvider)
        => new()
        {
            Paths = bootstrapProvider.GetRequiredService<IConfigurationPathService>(),
            JsonFileStore = bootstrapProvider.GetRequiredService<IJsonConfigurationService>(),
            PluginCatalog = bootstrapProvider.GetRequiredService<IPluginCatalog>(),
            PluginLoader = bootstrapProvider.GetRequiredService<IPluginLoader>(),
            EngineFactory = bootstrapProvider.GetRequiredService<IEngineFactory>(),
            Localization = bootstrapProvider.GetRequiredService<ILocalizationService>(),
            Logging = bootstrapProvider.GetRequiredService<ILoggingWrapper>(),
            ProfileService = bootstrapProvider.GetRequiredService<IApplicationProfileService>(),
            CaptureRegionsStore = bootstrapProvider.GetRequiredService<ICaptureRegionsStore>(),
            CaptureFrameProcessor = bootstrapProvider.GetRequiredService<ICaptureFrameProcessor>(),
            CaptureRegionsSnapshotService = bootstrapProvider.GetRequiredService<ICaptureRegionsSnapshotService>(),
            ProcessIconLoader = bootstrapProvider.GetRequiredService<IProcessIconLoader>(),
            PluginUpdateService = bootstrapProvider.GetRequiredService<IPluginUpdateService>(),
            HostVersionChecker = bootstrapProvider.GetRequiredService<IHostVersionChecker>(),
            GitHubReleasesClient = bootstrapProvider.GetRequiredService<IGitHubReleasesClient>(),
        };
}
