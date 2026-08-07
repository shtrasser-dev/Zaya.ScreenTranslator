using Zaya.OCR.Models;

namespace Zaya.ScreenTranslator.Layout.Models;

/// <summary>
/// Layout line drawn in overlay debug mode when it matched the previous frame.
/// </summary>
public sealed class OverlayDebugLine
{
    public required string Text { get; init; }
    public required BoundingBox Bounds { get; init; }
}
