using System.Drawing;
using System.Numerics;
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
        string backgroundColor,
        string textColor,
        bool outline,
        string fitMode)
    {
        var bounds = item.Bounds;
        var textHeight = Math.Max(1.0, bounds.TextHeight);
        var alongWidth = Math.Max(1.0, Vector2.Distance(bounds.P5, bounds.P6));
        var center = (bounds.P5 + bounds.P6) * 0.5f;
        var normal = bounds.Normal;

        var fontSize = fixedFontSize
            ? Math.Max(8.0, fontSizePx)
            : Math.Max(8.0, textHeight * (fontScalePercent / 100.0));
        // Natural separation from source; offset is extra gap (positive = away, negative = toward/into).
        var baseGap = Math.Max(0, (int)(fontSize * 0.15));
        var offsetY = fixedFontSize
            ? offsetYPx
            : (int)Math.Round(fontSize * (offsetYPercent / 100.0));

        // FontSize ≈ em-size; real glyph box (ascent+descent) is a bit taller.
        const double lineHeightFactor = 1.25;
        var drawHeight = (int)Math.Ceiling(fontSize * lineHeightFactor) + padding * 2 + 4; // +4 for Border padding
        var drawWidth = (int)Math.Max(1, Math.Round(alongWidth));
        OverlayVAlign vAlign;

        Vector2 drawCenter;
        switch (placement)
        {
            case OverlayLayoutSettingKeys.PlacementOver:
                drawHeight = Math.Max(drawHeight, (int)Math.Ceiling(textHeight));
                drawCenter = center + normal * offsetY;
                vAlign = OverlayVAlign.Center;
                break;
            case OverlayLayoutSettingKeys.PlacementBelow:
                // Positive offsetY pushes further down the glyph normal; negative pulls toward/into source.
                drawCenter = center + normal * (float)(textHeight * 0.5 + baseGap + offsetY + drawHeight * 0.5);
                vAlign = OverlayVAlign.Top;
                break;
            default: // above
                // Positive offsetY pushes further up (-Normal); negative pulls toward/into source.
                drawCenter = center - normal * (float)(textHeight * 0.5 + baseGap + offsetY + drawHeight * 0.5);
                vAlign = OverlayVAlign.Bottom;
                break;
        }

        var drawX = (int)Math.Round(drawCenter.X - drawWidth * 0.5);
        var drawY = (int)Math.Round(drawCenter.Y - drawHeight * 0.5);

        return new OverlayDrawSpec(
            item.Text,
            new Rectangle(drawX, drawY, drawWidth, Math.Max(1, drawHeight)),
            fontSize,
            background,
            backgroundOpacity,
            backgroundColor,
            textColor,
            outline,
            fitMode,
            vAlign,
            bounds.AngleDegrees);
    }
}
