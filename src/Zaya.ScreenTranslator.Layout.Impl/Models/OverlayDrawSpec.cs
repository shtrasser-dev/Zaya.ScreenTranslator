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
/// </summary>
public readonly record struct OverlayDrawSpec(
    string Text,
    Rectangle DrawBounds,
    double FontSize,
    string Background,
    int BackgroundOpacity,
    string TextColor,
    bool Outline,
    string FitMode,
    OverlayVAlign VAlign);
