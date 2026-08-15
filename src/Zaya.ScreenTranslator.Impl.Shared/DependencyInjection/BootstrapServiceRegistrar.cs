using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zaya.Logging.Impl.Microsoft.Services;
using Zaya.Logging.Impl.Services;
using Zaya.Logging.Services;
using Zaya.ScreenTranslator.Impl.Shared.Logging;
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
        var paths = new ConfigurationPathService();
        var jsonStore = new JsonConfigurationService();
        var logOptionsStore = new LogOptionsStore(jsonStore, paths);
        var logOptions = logOptionsStore.LoadOrCreate();
        var melLevel = logOptions.ResolveLevel();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(melLevel);
            if (logOptions.WriteToDebug)
                builder.AddDebug();
            if (logOptions.WriteToFile)
            {
                builder.AddProvider(new RollingFileLoggerProvider(
                    logOptionsStore.GetLogsDirectory(),
                    logOptions.ResolveMaxFileSizeBytes(),
                    logOptions.ResolveMaxFileCount()));
            }
        });

        services.AddSingleton<ILoggingWrapper>(sp =>
        {
            var mel = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Zaya");
            return new LoggingWrapper(new MicrosoftExtensionsLogger(mel));
        });

        services.AddSingleton<IConfigurationPathService>(paths).WrapLogging<IConfigurationPathService>();
        services.AddSingleton<IJsonConfigurationService>(jsonStore).WrapLogging<IJsonConfigurationService>();
        services.AddSingleton<ILogOptionsStore>(logOptionsStore).WrapLogging<ILogOptionsStore>();
        services.AddSingleton<IEmbeddedResourceService, EmbeddedResourceService>().WrapLogging<IEmbeddedResourceService>();

        services.AddSingleton<HttpClient>();
        services.AddSingleton<IGitHubReleasesClient, GitHubReleasesClient>().WrapLogging<IGitHubReleasesClient>();

        services.AddSingleton<ILocalizationService, LocalizationService>().WrapLogging<ILocalizationService>();
        services.AddSingleton<IApplicationProfileService, ApplicationProfileService>().WrapLogging<IApplicationProfileService>();
        services.AddSingleton<IBuiltinPluginCatalog, BuiltinPluginCatalog>().WrapLogging<IBuiltinPluginCatalog>();
        services.AddSingleton<IPluginHostCompatibility, PluginHostCompatibility>().WrapLogging<IPluginHostCompatibility>();
        services.AddSingleton<IPluginManifestReader, PluginManifestReader>().WrapLogging<IPluginManifestReader>();
        services.AddSingleton<ILocalPluginStore, LocalPluginStore>().WrapLogging<ILocalPluginStore>();
        services.AddSingleton<IPluginCatalogDownloader, PluginCatalogDownloader>().WrapLogging<IPluginCatalogDownloader>();
        services.AddSingleton<IPluginUpdateService, PluginUpdateService>().WrapLogging<IPluginUpdateService>();
        services.AddSingleton<IHostVersionChecker, HostVersionChecker>().WrapLogging<IHostVersionChecker>();

        services.AddSingleton<IPluginExtractCache, PluginExtractCache>().WrapLogging<IPluginExtractCache>();
        services.AddSingleton<IPluginZipProcessor, PluginZipProcessor>().WrapLogging<IPluginZipProcessor>();
        services.AddSingleton<IPluginZipDirectoryScanner, PluginZipDirectoryScanner>().WrapLogging<IPluginZipDirectoryScanner>();
        services.AddSingleton<IPluginAssemblyLoader, PluginAssemblyLoader>().WrapLogging<IPluginAssemblyLoader>();
        services.AddSingleton<IPluginDirectoryProcessor, PluginDirectoryProcessor>().WrapLogging<IPluginDirectoryProcessor>();
        services.AddSingleton<IPluginDirectoryScanner, PluginDirectoryScanner>().WrapLogging<IPluginDirectoryScanner>();
        services.AddSingleton<IPluginCatalog, PluginCatalog>().WrapLogging<IPluginCatalog>();
        services.AddSingleton<IEngineFactoryCatalogService, EngineFactoryCatalogService>().WrapLogging<IEngineFactoryCatalogService>();
        services.AddSingleton<IPluginLoader, PluginLoader>().WrapLogging<IPluginLoader>();
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
