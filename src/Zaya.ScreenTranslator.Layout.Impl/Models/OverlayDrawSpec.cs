using System.Drawing;

namespace Zaya.ScreenTranslator.Layout.Impl.Models;

/// <summary>
/// Vertical alignment of text inside the draw box.
/// </summary>
public enum OverlayVAlign
{
    Top,
    Center,
    Bottom,
}

/// <summary>
/// Computed draw instructions for one overlay item (capture-pixel space).
/// <see cref="DrawBounds"/> is the unrotated local box; <see cref="AngleDegrees"/> rotates it around the box center.
/// When <see cref="IsMarker"/> is true, the spec is an on-demand hit area (1px outline, no text).
/// Always-mode text grows width to the full line (no wrap). On-demand fill (<see cref="WrapWords"/>)
/// expands to the longest word and wraps within that width.
/// </summary>
public readonly record struct OverlayDrawSpec(
    string Text,
    Rectangle DrawBounds,
    double FontSize,
    string Background,
    int BackgroundOpacity,
    string BackgroundColor,
    string TextColor,
    bool Outline,
    OverlayVAlign VAlign,
    float AngleDegrees,
    bool IsMarker = false,
    string? SourceKey = null,
    bool WrapWords = false);
