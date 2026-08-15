using Zaya.Logging.Models;
using Zaya.Logging.Services;
using Zaya.OCR.Services;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Exceptions;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

[Log(LogLevel.Debug)]
public sealed class EngineFactory : IEngineFactory
{
    private readonly ILoggingWrapper _loggingWrapper;
    private readonly IPluginCatalog _pluginCatalog;
    private readonly IEngineFactoryCatalogService _engineFactoryCatalogService;

    public EngineFactory(
        ILoggingWrapper loggingWrapper,
        IPluginCatalog pluginCatalog,
        IEngineFactoryCatalogService engineFactoryCatalogService)
    {
        _loggingWrapper = loggingWrapper;
        _pluginCatalog = pluginCatalog;
        _engineFactoryCatalogService = engineFactoryCatalogService;
    }

    public IOCRService? CreateOcr(string? engineId)
        => CreateFromCatalog<IOCRService>(PluginServiceKind.Ocr, engineId);

    public ITextLayoutService? CreateTextLayout(string? engineId)
        => CreateFromCatalog<ITextLayoutService>(PluginServiceKind.TextLayout, engineId);

    public ICaptureService? CreateCapture(string? engineId)
        => CreateFromCatalog<ICaptureService>(PluginServiceKind.Capture, engineId);

    public ITranslatorService? CreateTranslator(string? engineId)
    {
        if (string.Equals(engineId, NoTranslationTranslatorService.EngineIdValue, StringComparison.OrdinalIgnoreCase))
            return _loggingWrapper.Wrap<ITranslatorService>(new NoTranslationTranslatorService(_loggingWrapper));

        var created = CreateFromCatalog<ITranslatorService>(PluginServiceKind.Translator, engineId);
        if (created is not null)
            return created;

        if (_pluginCatalog.List(PluginServiceKind.Translator).Count == 0)
            return _loggingWrapper.Wrap<ITranslatorService>(new NoTranslationTranslatorService(_loggingWrapper));

        return null;
    }

    public ITranslatorCacheService? CreateTranslatorCache(string? engineId)
    {
        if (string.Equals(engineId, NoTranslatorCacheService.EngineIdValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(engineId, "none", StringComparison.OrdinalIgnoreCase))
            return _loggingWrapper.Wrap<ITranslatorCacheService>(new NoTranslatorCacheService(_loggingWrapper));

        var created = CreateFromCatalog<ITranslatorCacheService>(PluginServiceKind.TranslatorCache, engineId);
        if (created is not null)
            return created;

        if (_pluginCatalog.List(PluginServiceKind.TranslatorCache).Count == 0)
            return _loggingWrapper.Wrap<ITranslatorCacheService>(new NoTranslatorCacheService(_loggingWrapper));

        return null;
    }

    public IOverlayLayoutService? CreateOverlayLayout(string? engineId)
        => CreateFromCatalog<IOverlayLayoutService>(PluginServiceKind.OverlayLayout, engineId);

    private TService? CreateFromCatalog<TService>(PluginServiceKind kind, string? engineId)
        where TService : class
    {
        var entries = _pluginCatalog.List(kind);
        if (entries.Count == 0)
            return null;

        PluginEngineRegistration? reg = null;
        if (!string.IsNullOrWhiteSpace(engineId))
        {
            try
            {
                reg = _engineFactoryCatalogService.Find(kind, engineId);
            }
            catch (EngineNotFoundException)
            {
            }
        }

        reg ??= entries[0];

        var created = _pluginCatalog.Create(reg.EntryType, _loggingWrapper) as TService;
        return created is null ? null : _loggingWrapper.Wrap<TService>(created);
    }
}
