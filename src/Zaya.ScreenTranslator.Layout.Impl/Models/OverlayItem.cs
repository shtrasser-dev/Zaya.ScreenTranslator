using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Layout.Impl.Models;

/// <summary>
/// Internal draw unit: one line box after join/translate/wrap.
/// </summary>
public sealed class OverlayItem
{
    public required Guid Id { get; init; }
    public required string Text { get; init; }
    public required BoundingBox Bounds { get; init; }
}
