using Zaya.OCR.Models;

namespace Zaya.ScreenTranslator.Layout.Models;

/// <summary>
/// OCR word drawn in overlay debug mode (source text + oriented bounds).
/// </summary>
public sealed class OverlayDebugWord
{
    public required string Text { get; init; }
    public required BoundingBox Bounds { get; init; }
}
