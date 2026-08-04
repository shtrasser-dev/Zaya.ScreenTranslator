using System.Drawing;
using Zaya.ScreenTranslator.Layout.Impl.Constants;
using Zaya.ScreenTranslator.Layout.Impl.Models;
using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Layout.Impl.Services;

/// <summary>
/// Pure layout math for overlay items (no UI).
/// </summary>
public static class OverlayLayoutMath
{
    public static OverlayDrawSpec Compute(
        OverlayItem item,
        string placement,
        bool fixedFontSize,
        int fontScalePercent,
        int fontSizePx,
        int offsetYPx,
        int offsetYPercent,
        int padding,
        string background,
        int backgroundOpacity,
        string textColor,
        bool outline,
        string fitMode)
    {
        var bounds = item.Bounds;
        var fontSize = fixedFontSize
            ? Math.Max(8.0, fontSizePx)
            : Math.Max(8.0, bounds.Height * (fontScalePercent / 100.0));
        // Natural separation from source; offset is extra gap (positive = away, negative = toward/into).
        var baseGap = Math.Max(0, (int)(fontSize * 0.15));
        var offsetY = fixedFontSize
            ? offsetYPx
            : (int)Math.Round(fontSize * (offsetYPercent / 100.0));

        // FontSize ≈ em-size; real glyph box (ascent+descent) is a bit taller.
        const double lineHeightFactor = 1.25;
        var drawHeight = (int)Math.Ceiling(fontSize * lineHeightFactor) + padding * 2 + 4; // +4 for Border padding
        var drawWidth = Math.Max(bounds.Width, 1);
        int drawX = bounds.X;
        int drawY;
        OverlayVAlign vAlign;

        switch (placement)
        {
            case OverlayLayoutSettingKeys.PlacementOver:
                drawY = bounds.Y + offsetY;
                drawHeight = Math.Max(drawHeight, bounds.Height);
                vAlign = OverlayVAlign.Center;
                break;
            case OverlayLayoutSettingKeys.PlacementBelow:
                // Positive offsetY pushes further down; negative pulls up toward/into source.
                drawY = bounds.Bottom + baseGap + offsetY;
                vAlign = OverlayVAlign.Top;
                break;
            default: // above
                // Positive offsetY pushes further up; negative pulls down toward/into source.
                drawY = bounds.Y - baseGap - offsetY - drawHeight;
                vAlign = OverlayVAlign.Bottom;
                break;
        }

        return new OverlayDrawSpec(
            item.Text,
            new Rectangle(drawX, drawY, drawWidth, Math.Max(1, drawHeight)),
            fontSize,
            background,
            backgroundOpacity,
            textColor,
            outline,
            fitMode,
            vAlign);
    }
}
