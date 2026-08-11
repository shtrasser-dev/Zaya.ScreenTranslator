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
        int horizonSnapDegrees,
        string background,
        int backgroundOpacity,
        string backgroundColor,
        string textColor,
        bool outline,
        string? displayText = null,
        string? sourceKey = null)
    {
        var bounds = item.Bounds;
        var textHeight = Math.Max(1.0, bounds.TextHeight);
        var alongWidth = Math.Max(1.0, Vector2.Distance(bounds.P5, bounds.P6));
        var center = (bounds.P5 + bounds.P6) * 0.5f;
        var normal = bounds.Normal;

        displayText ??= item.Text;
        sourceKey ??= item.Text;

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
            displayText,
            new Rectangle(drawX, drawY, drawWidth, Math.Max(1, drawHeight)),
            fontSize,
            background,
            backgroundOpacity,
            backgroundColor,
            textColor,
            outline,
            vAlign,
            SnapAngleToHorizon(bounds.AngleDegrees, horizonSnapDegrees),
            IsMarker: false,
            SourceKey: sourceKey);
    }

    /// <summary>
    /// If the line tilt from the nearest horizontal orientation is within
    /// <paramref name="maxTiltDegrees"/>, snap to 0° or ±180°; otherwise keep the angle.
    /// </summary>
    public static float SnapAngleToHorizon(float angleDegrees, int maxTiltDegrees)
    {
        if (maxTiltDegrees <= 0)
            return angleDegrees;

        var a = NormalizeAngleDegrees(angleDegrees);
        var distTo0 = Math.Abs(a);
        var distTo180 = 180f - distTo0;

        if (distTo0 <= maxTiltDegrees)
            return 0f;
        if (distTo180 <= maxTiltDegrees)
            return a >= 0f ? 180f : -180f;
        return angleDegrees;
    }

    private static float NormalizeAngleDegrees(float angleDegrees)
    {
        var a = angleDegrees % 360f;
        if (a > 180f)
            a -= 360f;
        else if (a <= -180f)
            a += 360f;
        return a;
    }

    /// <summary>
    /// Axis-aligned hit/outline area covering all OCR line boxes of a paragraph
    /// (comics / on-demand mode). Drawn as a 1px outline; hover anywhere to expand.
    /// </summary>
    public static OverlayDrawSpec ComputeParagraphHitArea(
        IReadOnlyList<OverlayItem> lines,
        string textColor,
        string? sourceKey = null)
    {
        var box = UnionAabb(lines);
        return new OverlayDrawSpec(
            string.Empty,
            box,
            1,
            OverlayLayoutSettingKeys.BackgroundNone,
            0,
            OverlayLayoutSettingKeys.BackgroundColorDark,
            textColor,
            false,
            OverlayVAlign.Center,
            0,
            IsMarker: true,
            SourceKey: sourceKey ?? lines[0].Id.ToString("N"));
    }

    /// <summary>
    /// Fills the paragraph AABB with word-wrapped translation (on-demand expand).
    /// Renderer grows width to the longest word and wraps within that box.
    /// </summary>
    public static OverlayDrawSpec ComputeParagraphFill(
        IReadOnlyList<OverlayItem> lines,
        string text,
        bool fixedFontSize,
        int fontScalePercent,
        int fontSizePx,
        string background,
        int backgroundOpacity,
        string backgroundColor,
        string textColor,
        bool outline,
        string? sourceKey = null)
    {
        var box = UnionAabb(lines);
        var textHeight = 0.0;
        foreach (var line in lines)
            textHeight = Math.Max(textHeight, line.Bounds.TextHeight);
        textHeight = Math.Max(1.0, textHeight);

        var fontSize = fixedFontSize
            ? Math.Max(8.0, fontSizePx)
            : Math.Max(8.0, textHeight * (fontScalePercent / 100.0));

        return new OverlayDrawSpec(
            text,
            box,
            fontSize,
            background,
            backgroundOpacity,
            backgroundColor,
            textColor,
            outline,
            OverlayVAlign.Center,
            0,
            IsMarker: false,
            SourceKey: sourceKey ?? lines[0].Id.ToString("N"),
            WrapWords: true);
    }

    private static Rectangle UnionAabb(IReadOnlyList<OverlayItem> lines)
    {
        if (lines.Count == 0)
            throw new ArgumentException("At least one line is required.", nameof(lines));

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var line in lines)
        {
            var b = line.Bounds;
            if (b.MinX < minX) minX = b.MinX;
            if (b.MinY < minY) minY = b.MinY;
            if (b.MaxX > maxX) maxX = b.MaxX;
            if (b.MaxY > maxY) maxY = b.MaxY;
        }

        var x = (int)Math.Floor(minX);
        var y = (int)Math.Floor(minY);
        var w = Math.Max(1, (int)Math.Ceiling(maxX) - x);
        var h = Math.Max(1, (int)Math.Ceiling(maxY) - y);
        return new Rectangle(x, y, w, h);
    }
}
