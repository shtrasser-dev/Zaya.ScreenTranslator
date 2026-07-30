using System.Drawing;

namespace Zaya.ScreenTranslator.Layout.Models;

/// <summary>
/// A single text block to draw: content and its source rectangle (capture/client pixels).
/// </summary>
public sealed class OverlayItem
{
    public required string Text { get; init; }
    public required Rectangle Bounds { get; init; }
}
