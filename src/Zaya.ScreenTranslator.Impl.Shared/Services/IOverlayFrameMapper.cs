using Zaya.OCR.Models;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Maps OCR / layout results to overlay items in client coordinates.
/// </summary>
public interface IOverlayFrameMapper
{
    OverlayFrameView Map(
        IOCRResult ocr,
        ITextResult layout,
        TranslationBatch batch,
        int originX,
        int originY);
}
