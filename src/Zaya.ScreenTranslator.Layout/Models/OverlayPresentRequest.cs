using Zaya.Primitives.OCR;

namespace Zaya.ScreenTranslator.Layout.Models;

/// <summary>
/// One frame for overlay presentation: text-layout result in capture space plus origin offset to client.
/// </summary>
public sealed class OverlayPresentRequest
{
    public required ITextResult Layout { get; init; }

    /// <summary>Capture-region origin X in target-window client coordinates.</summary>
    public int OriginX { get; init; }

    /// <summary>Capture-region origin Y in target-window client coordinates.</summary>
    public int OriginY { get; init; }

    /// <summary>Optional OCR words for debug drawing when overlay <c>debugMode</c> is on.</summary>
    public IOCRResult? Ocr { get; init; }
}
