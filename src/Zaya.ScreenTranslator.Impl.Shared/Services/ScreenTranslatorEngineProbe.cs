using System.Globalization;
using Zaya.OCR.Services;
using Zaya.PluginManager.Impl.Services;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class ScreenTranslatorEngineProbe : IPluginEngineProbe
{
    public bool TryDescribe(object instance, CultureInfo culture, out PluginEngineInfo info)
    {
        switch (instance)
        {
            case IOCRService ocr:
                info = new PluginEngineInfo
                {
                    ServiceKind = ScreenTranslatorPluginKinds.Ocr,
                    EngineId = ocr.EngineId,
                    DisplayName = ocr.DisplayName.GetValue(culture),
                };
                return !string.IsNullOrWhiteSpace(info.EngineId);
            case ITextLayoutService layout:
                info = new PluginEngineInfo
                {
                    ServiceKind = ScreenTranslatorPluginKinds.TextLayout,
                    EngineId = layout.EngineId,
                    DisplayName = layout.DisplayName.GetValue(culture),
                };
                return !string.IsNullOrWhiteSpace(info.EngineId);
            case ICaptureService capture:
                info = new PluginEngineInfo
                {
                    ServiceKind = ScreenTranslatorPluginKinds.Capture,
                    EngineId = capture.EngineId,
                    DisplayName = capture.DisplayName.GetValue(culture),
                };
                return !string.IsNullOrWhiteSpace(info.EngineId);
            case ITranslatorService translator:
                info = new PluginEngineInfo
                {
                    ServiceKind = ScreenTranslatorPluginKinds.Translator,
                    EngineId = translator.EngineId,
                    DisplayName = translator.DisplayName.GetValue(culture),
                };
                return !string.IsNullOrWhiteSpace(info.EngineId);
            case ITranslatorCacheService cache:
                info = new PluginEngineInfo
                {
                    ServiceKind = ScreenTranslatorPluginKinds.TranslatorCache,
                    EngineId = cache.EngineId,
                    DisplayName = cache.DisplayName.GetValue(culture),
                };
                return !string.IsNullOrWhiteSpace(info.EngineId);
            case IOverlayLayoutService overlay:
                info = new PluginEngineInfo
                {
                    ServiceKind = ScreenTranslatorPluginKinds.OverlayLayout,
                    EngineId = overlay.EngineId,
                    DisplayName = overlay.DisplayName.GetValue(culture),
                };
                return !string.IsNullOrWhiteSpace(info.EngineId);
            default:
                info = null!;
                return false;
        }
    }
}
