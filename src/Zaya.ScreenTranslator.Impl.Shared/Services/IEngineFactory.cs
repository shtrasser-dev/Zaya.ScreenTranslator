using Zaya.OCR.Services;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IEngineFactory
{
    IOCRService? CreateOcr(string? engineId);
    ITextLayoutService? CreateTextLayout(string? engineId);
    ICaptureService? CreateCapture(string? engineId);
    ITranslatorService? CreateTranslator(string? engineId);
    ITranslatorCacheService? CreateTranslatorCache(string? engineId);
    IOverlayLayoutService? CreateOverlayLayout(string? engineId);
}
