using Microsoft.Extensions.DependencyInjection;
using Zaya.Logging.Impl.Microsoft.Services;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;

namespace Zaya.ScreenTranslator.Impl.Shared.DependencyInjection;

public static class AppServiceRegistrar
{
    public static void Register(IServiceCollection services, BootstrapTransferredServices transferred)
    {
        services.AddSingleton(transferred.Paths);
        services.AddSingleton(transferred.JsonFileStore);
        services.AddSingleton(transferred.PluginCatalog);
        services.AddSingleton(transferred.PluginLoader);
        services.AddSingleton(transferred.EngineFactory);
        services.AddSingleton(transferred.Localization);
        services.AddSingleton(transferred.Logging);
        services.AddSingleton(transferred.ProfileService);
        services.AddSingleton(transferred.CaptureRegionsStore);
        services.AddSingleton(transferred.CaptureFrameProcessor);
        services.AddSingleton(transferred.CaptureRegionsSnapshotService);
        services.AddSingleton(transferred.ProcessIconLoader);
        services.AddSingleton(transferred.PluginUpdateService);
        services.AddSingleton(transferred.HostVersionChecker);
        services.AddSingleton(transferred.GitHubReleasesClient);

        services.AddSingleton<IScreenTranslatorContext, ScreenTranslatorContext>().WrapLogging<IScreenTranslatorContext>();
        services.AddSingleton<ISettingsService, SettingsService>().WrapLogging<ISettingsService>();
        services.AddSingleton<IOcrFramePreparer, OcrFramePreparer>().WrapLogging<IOcrFramePreparer>();
        services.AddSingleton<ITranslationBatchBuilder, TranslationBatchBuilder>().WrapLogging<ITranslationBatchBuilder>();
        services.AddSingleton<IOverlayFrameMapper, OverlayFrameMapper>().WrapLogging<IOverlayFrameMapper>();
        services.AddSingleton<ITranslationLoopService, TranslationLoopService>().WrapLogging<ITranslationLoopService>();
        services.AddSingleton<ITranslationHistoryService, TranslationHistoryService>().WrapLogging<ITranslationHistoryService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<TextWindowViewModel>();
    }
}
