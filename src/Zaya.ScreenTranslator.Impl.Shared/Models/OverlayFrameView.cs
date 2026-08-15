using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Overlay payload for one frame: line items plus optional debug geometry.
/// </summary>
public sealed class OverlayFrameView
{
    public OverlayFrameView(
        IReadOnlyList<OverlayItem> items,
        IReadOnlyList<OverlayDebugWord> debugWords,
        IReadOnlyList<OverlayDebugLine> debugLines)
    {
        Items = items;
        DebugWords = debugWords;
        DebugLines = debugLines;
    }

    public IReadOnlyList<OverlayItem> Items { get; }
    public IReadOnlyList<OverlayDebugWord> DebugWords { get; }
    public IReadOnlyList<OverlayDebugLine> DebugLines { get; }
}
