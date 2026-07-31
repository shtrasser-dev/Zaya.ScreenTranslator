using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Svg.Skia;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>Builds Avalonia images from inline SVG setting icons.</summary>
internal static class SettingSvgIconHelper
{
    // Slightly darker than Tailwind green-500 for clearer "on" state.
    public static readonly Color CheckedColor = Color.FromRgb(0x16, 0xA3, 0x4A);

    public static Image CreateIconImage(string iconSvg, bool checkedState, double height)
    {
        var (viewW, viewH) = ResolveSvgViewBoxSize(iconSvg);
        var aspect = viewW > 0 && viewH > 0 ? viewW / viewH : 1.0;
        return new Image
        {
            Height = height,
            Width = height * aspect,
            Stretch = Stretch.Fill,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Source = CreateImageSource(iconSvg, checkedState),
        };
    }

    public static SvgImage CreateImageSource(string iconSvg, bool checkedState)
        => new()
        {
            Source = SvgSource.LoadFromSvg(iconSvg),
            CurrentColor = checkedState ? CheckedColor : GetMutedIconColor(),
            Css = checkedState ? BuildCheckedSvgCss(iconSvg) : null,
        };

    /// <summary>
    /// Stroke icons (small viewBox, outline) get a thicker stroke.
    /// Fill icons (often large traced viewBoxes) get an outline stroke scaled to the viewBox so they look heavier.
    /// </summary>
    public static string BuildCheckedSvgCss(string iconSvg)
    {
        var isStrokeIcon = iconSvg.Contains("fill=\"none\"", StringComparison.OrdinalIgnoreCase)
            || iconSvg.Contains("stroke=\"currentColor\"", StringComparison.OrdinalIgnoreCase)
            || iconSvg.Contains("stroke='currentColor'", StringComparison.OrdinalIgnoreCase);

        if (isStrokeIcon)
            return "path, line, circle, polyline, polygon { stroke-width: 2.75; }";

        var strokeWidth = ResolveFillBoldStrokeWidth(iconSvg);
        return FormattableString.Invariant(
            $"path, line, circle, polyline, polygon {{ stroke: currentColor; stroke-width: {strokeWidth:0.##}; paint-order: stroke fill; }}");
    }

    public static (double Width, double Height) ResolveSvgViewBoxSize(string iconSvg)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            iconSvg,
            """viewBox\s*=\s*["']\s*[-0-9.]+\s+[-0-9.]+\s+(?<w>[0-9.]+)\s+(?<h>[0-9.]+)["']""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success
            && double.TryParse(m.Groups["w"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            && double.TryParse(m.Groups["h"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            && w > 0 && h > 0)
        {
            return (w, h);
        }

        return (1, 1);
    }

    public static double ResolveFillBoldStrokeWidth(string iconSvg)
    {
        var (w, _) = ResolveSvgViewBoxSize(iconSvg);
        // ~5% of viewBox width — visible fattening for 16px and 600px boxes alike.
        return Math.Clamp(w * 0.05, 1.2, 80);
    }

    public static Color GetMutedIconColor()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        return isDark
            ? Color.FromRgb(0xE0, 0xE0, 0xE0)
            : Color.FromRgb(0x8A, 0x8A, 0x8A);
    }
}
