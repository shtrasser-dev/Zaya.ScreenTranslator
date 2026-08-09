using Zaya.OCR.Models;

namespace Zaya.ScreenTranslator.Layout.Models;

/// <summary>
/// A single text block to draw: content and its source oriented bounds (capture/client pixels).
/// <see cref="Id"/> is stable across frames when text-layout reports a previous-frame match.
/// </summary>
public sealed class OverlayItem
{
    /// <summary>Stable identity for on-demand expand state (new Guid when the block is new).</summary>
    public required Guid Id { get; init; }

    public required string Text { get; init; }
    public required BoundingBox Bounds { get; init; }
}
