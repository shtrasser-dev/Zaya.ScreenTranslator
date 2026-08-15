using Zaya.OCR.Services;
using Zaya.Screenshot.Models;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Mutable engine/session bundle for the translation loop; modules can be swapped between frames.
/// </summary>
public sealed class TranslationLoopRuntime
{
    public required ICaptureService Capture { get; set; }
    public required IOCRService Ocr { get; set; }
    public required ITextLayoutService TextLayout { get; set; }
    public required ITranslatorService Translator { get; set; }
    public required ITranslatorCacheService TranslatorCache { get; set; }
    public required ICaptureRegion Region { get; init; }
    public required ICaptureSession CaptureSession { get; set; }
    public required IOCRSession OcrSession { get; set; }
    public required ITextLayoutSession LayoutSession { get; set; }
    public required ITranslatorSession TranslatorSession { get; set; }
    public IOverlayLayoutSession? OverlaySession { get; set; }
    public nint WindowHandle { get; init; }
}

/// <summary>Applies pending module recreations after a frame finishes.</summary>
public interface ITranslationModuleRefresh
{
    Task ApplyPendingAsync(TranslationLoopRuntime runtime, CancellationToken cancellationToken);
}
