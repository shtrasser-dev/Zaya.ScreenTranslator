using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Zaya.ScreenTranslator.Layout.Impl.Constants;
using Zaya.ScreenTranslator.Layout.Impl.Models;
using Zaya.ScreenTranslator.Layout.Impl.Native;
using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Layout.Impl.Views;

/// <summary>
/// Transparent topmost overlay surface owned by the overlay-layout session.
/// Stays Win32 click-through so Avalonia transparent composition keeps working;
/// on-demand hover is detected by polling cursor vs paragraph hit boxes.
/// </summary>
public sealed class OverlayWindow : Window
{
    private static readonly IBrush ParagraphOutlineBrush =
        new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

    private readonly Canvas _canvas = new();
    private readonly DispatcherTimer _inputPollTimer;
    private readonly List<(int X, int Y, int W, int H, string Key)> _hitTargets = [];
    private bool _hasSyncedGeometry;
    private int _lastX;
    private int _lastY;
    private int _lastW;
    private int _lastH;
    private string? _lastRenderKey;
    private bool _interactive;
    private string? _hoveredKey;

    /// <summary>
    /// Raised when the hovered paragraph changes. <c>null</c> when the cursor leaves all hit areas.
    /// </summary>
    public event Action<string?>? HoverTargetChanged;

    public OverlayWindow()
    {
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Focusable = false;
        // Always false: keep HWND click-through for stable transparent rendering.
        IsHitTestVisible = false;
        ShowActivated = false;
        Content = _canvas;

        _inputPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
        _inputPollTimer.Tick += (_, _) => PollHover();

        Opened += (_, _) => EnsureClickThrough();
    }

    public void SetInteractive(bool interactive)
    {
        if (_interactive == interactive)
            return;

        _interactive = interactive;
        // Force redraw so hit targets refresh.
        _lastRenderKey = null;

        if (interactive)
            _inputPollTimer.Start();
        else
        {
            _inputPollTimer.Stop();
            _hitTargets.Clear();
            SetHoveredKey(null);
        }

        // Always click-through at Win32 level (removing WS_EX_TRANSPARENT breaks Avalonia
        // transparent composition and makes the overlay draw nothing).
        EnsureClickThrough();
    }

    private void EnsureClickThrough()
    {
        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (!OperatingSystem.IsWindows() || handle == IntPtr.Zero)
            return;
        NativeWindowMethods.EnableClickThrough(handle);
    }

    public void SyncToTarget(IntPtr targetHwnd)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!NativeWindowMethods.TryGetClientScreenRect(targetHwnd, out var x, out var y, out var w, out var h))
            return;

        // Avoid layout thrash from 1px HWND / DPI noise.
        if (_hasSyncedGeometry &&
            WithinOne(x, _lastX) &&
            WithinOne(y, _lastY) &&
            WithinOne(w, _lastW) &&
            WithinOne(h, _lastH))
        {
            return;
        }

        _hasSyncedGeometry = true;
        _lastX = x;
        _lastY = y;
        _lastW = w;
        _lastH = h;

        var scaling = Math.Max(0.1, RenderScaling);
        Position = new PixelPoint(x, y);
        Width = w / scaling;
        Height = h / scaling;
    }

    private static bool WithinOne(int a, int b)
    {
        var d = (long)a - b;
        return d >= -1 && d <= 1;
    }

    public void RenderItems(
        IReadOnlyList<OverlayDrawSpec> specs,
        IReadOnlyList<OverlayDebugWord>? debugWords = null,
        IReadOnlyList<OverlayDebugLine>? debugMatchedLines = null)
    {
        var key = BuildRenderKey(specs, debugWords, debugMatchedLines);
        if (string.Equals(key, _lastRenderKey, StringComparison.Ordinal))
            return;
        _lastRenderKey = key;

        _canvas.Children.Clear();
        _hitTargets.Clear();
        var scaling = Math.Max(0.1, RenderScaling);
        const double borderPad = 2; // Border.Padding on each side
        var originX = Position.X;
        var originY = Position.Y;

        // On-demand fill: grow width to the longest whole word so wrap never clips a word.
        // Outline (same SourceKey) uses the same expanded width.
        var expandedWidthByKey = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var spec in specs)
        {
            if (spec.IsMarker || !spec.WrapWords || string.IsNullOrEmpty(spec.SourceKey))
                continue;

            var boxW = Math.Max(1, Math.Round(spec.DrawBounds.Width / scaling));
            var fontSizeDip = Math.Max(8, spec.FontSize / scaling);
            var contentW = Math.Max(1, boxW - borderPad * 2);
            var needContent = Math.Max(contentW, MeasureMaxWordWidth(spec.Text, fontSizeDip));
            var needBox = Math.Ceiling(needContent + borderPad * 2);
            if (needBox > boxW)
                expandedWidthByKey[spec.SourceKey] = needBox;
        }

        foreach (var spec in specs)
        {
            // Snap to whole DIPs to reduce sub-pixel shimmer.
            var left = Math.Round(spec.DrawBounds.X / scaling);
            var top = Math.Round(spec.DrawBounds.Y / scaling);
            var boxW = Math.Max(1, Math.Round(spec.DrawBounds.Width / scaling));
            var boxH = Math.Max(1, Math.Round(spec.DrawBounds.Height / scaling));
            if (!string.IsNullOrEmpty(spec.SourceKey)
                && expandedWidthByKey.TryGetValue(spec.SourceKey, out var expandedW))
                boxW = expandedW;

            if (spec.IsMarker)
            {
                // 1 physical pixel outline around the paragraph AABB.
                var strokeDip = 1.0 / scaling;
                var outline = new Border
                {
                    Background = Brushes.Transparent,
                    BorderBrush = ParagraphOutlineBrush,
                    BorderThickness = new Thickness(strokeDip),
                    Width = boxW,
                    Height = boxH,
                };
                Canvas.SetLeft(outline, left);
                Canvas.SetTop(outline, top);
                _canvas.Children.Add(outline);

                if (_interactive && !string.IsNullOrEmpty(spec.SourceKey))
                {
                    _hitTargets.Add((
                        (int)Math.Round(originX + left * scaling),
                        (int)Math.Round(originY + top * scaling),
                        (int)Math.Max(1, Math.Round(boxW * scaling)),
                        (int)Math.Max(1, Math.Round(boxH * scaling)),
                        spec.SourceKey));
                }

                continue;
            }

            var fontSizeDip = Math.Max(8, spec.FontSize / scaling);
            var displayText = spec.WrapWords
                ? WrapWholeWords(spec.Text, fontSizeDip, Math.Max(1, boxW - borderPad * 2))
                : spec.Text;

            var textBlock = new TextBlock
            {
                Text = displayText,
                FontSize = fontSizeDip,
                Foreground = ResolveForeground(spec),
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = ToAvaloniaVAlign(spec.VAlign),
                TextAlignment = TextAlignment.Left,
                Effect = spec.Outline
                    ? new DropShadowEffect
                    {
                        BlurRadius = 5,
                        OffsetX = 0,
                        OffsetY = 0,
                        Color = Colors.Black,
                        Opacity = 1,
                    }
                    : null,
            };

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (spec.WrapWords)
            {
                // Width already sized for longest word; grow height for wrapped lines.
                var neededH = Math.Ceiling(textBlock.DesiredSize.Height) + borderPad * 2;
                if (neededH > boxH)
                    boxH = neededH;
            }
            else
            {
                // Always mode: one line, grow width (and height if needed) to fit full text.
                var neededW = Math.Ceiling(textBlock.DesiredSize.Width) + borderPad * 2;
                var neededH = Math.Ceiling(textBlock.DesiredSize.Height) + borderPad * 2;
                if (neededW > boxW)
                    boxW = neededW;
                if (neededH > boxH)
                    boxH = neededH;
            }

            var panel = new Border
            {
                Padding = new Thickness(borderPad),
                ClipToBounds = false,
                Background = CreateBackground(spec),
                Child = textBlock,
                Width = boxW,
                Height = boxH,
            };

            Canvas.SetLeft(panel, left);
            Canvas.SetTop(panel, top);

            if (Math.Abs(spec.AngleDegrees) >= 0.5f)
            {
                panel.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                panel.RenderTransform = new RotateTransform(spec.AngleDegrees);
            }

            _canvas.Children.Add(panel);

            if (_interactive && !string.IsNullOrEmpty(spec.SourceKey))
            {
                _hitTargets.Add((
                    (int)Math.Round(originX + left * scaling),
                    (int)Math.Round(originY + top * scaling),
                    (int)Math.Max(1, Math.Round(boxW * scaling)),
                    (int)Math.Max(1, Math.Round(boxH * scaling)),
                    spec.SourceKey));
            }
        }

        if (debugMatchedLines is { Count: > 0 })
            RenderDebugMatchedLines(debugMatchedLines, scaling);

        if (debugWords is { Count: > 0 })
            RenderDebugWords(debugWords, scaling);
    }

    /// <summary>
    /// Widest whole-word width in DIP (never splits words).
    /// </summary>
    private static double MeasureMaxWordWidth(string text, double fontSizeDip)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var words = text.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return 0;

        var measure = new TextBlock
        {
            FontSize = fontSizeDip,
            TextWrapping = TextWrapping.NoWrap,
        };

        var max = 0.0;
        foreach (var word in words)
        {
            measure.Text = word;
            measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (measure.DesiredSize.Width > max)
                max = measure.DesiredSize.Width;
        }

        return max;
    }

    /// <summary>
    /// Inserts newlines so each line fits <paramref name="maxWidthDip"/> when possible.
    /// Never splits a word; caller must ensure <paramref name="maxWidthDip"/> ≥ longest word.
    /// </summary>
    private static string WrapWholeWords(string text, double fontSizeDip, double maxWidthDip)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var words = text.Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return text;

        var measure = new TextBlock
        {
            FontSize = fontSizeDip,
            TextWrapping = TextWrapping.NoWrap,
        };

        double Measure(string s)
        {
            measure.Text = s;
            measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return measure.DesiredSize.Width;
        }

        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in words)
        {
            var trial = current.Length == 0 ? word : current + " " + word;
            if (current.Length == 0 || Measure(trial) <= maxWidthDip)
            {
                current = trial;
                continue;
            }

            lines.Add(current);
            current = word;
        }

        if (current.Length > 0)
            lines.Add(current);

        return string.Join('\n', lines);
    }

    private void PollHover()
    {
        if (!_interactive || !OperatingSystem.IsWindows())
            return;

        string? hitKey = null;
        if (_hitTargets.Count > 0 && NativeWindowMethods.GetCursorPos(out var pt))
        {
            // Last drawn wins (expanded fill over outline if both overlap).
            for (var i = _hitTargets.Count - 1; i >= 0; i--)
            {
                var h = _hitTargets[i];
                if (pt.X >= h.X && pt.X < h.X + h.W && pt.Y >= h.Y && pt.Y < h.Y + h.H)
                {
                    hitKey = h.Key;
                    break;
                }
            }
        }

        SetHoveredKey(hitKey);
    }

    private void SetHoveredKey(string? key)
    {
        if (string.Equals(_hoveredKey, key, StringComparison.Ordinal))
            return;
        _hoveredKey = key;
        HoverTargetChanged?.Invoke(key);
    }

    private void RenderDebugMatchedLines(IReadOnlyList<OverlayDebugLine> lines, double scaling)
    {
        var fill = new SolidColorBrush(Color.FromArgb(160, 0, 180, 0));
        var stroke = new SolidColorBrush(Colors.Lime);
        foreach (var line in lines)
        {
            if (line.Bounds.IsEmpty)
                continue;

            var b = line.Bounds;
            var poly = new Polygon
            {
                Stroke = stroke,
                StrokeThickness = 1.5,
                Fill = fill,
                Points =
                [
                    new Point(b.P1.X / scaling, b.P1.Y / scaling),
                    new Point(b.P2.X / scaling, b.P2.Y / scaling),
                    new Point(b.P3.X / scaling, b.P3.Y / scaling),
                    new Point(b.P4.X / scaling, b.P4.Y / scaling),
                ],
            };
            _canvas.Children.Add(poly);

            if (string.IsNullOrWhiteSpace(line.Text))
                continue;

            var centerX = (b.P5.X + b.P6.X) * 0.5 / scaling;
            var centerY = (b.P5.Y + b.P6.Y) * 0.5 / scaling;
            var label = new TextBlock
            {
                Text = line.Text,
                FontSize = 8,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.NoWrap,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var w = label.DesiredSize.Width;
            var h = label.DesiredSize.Height;
            Canvas.SetLeft(label, Math.Round(centerX - w * 0.5));
            Canvas.SetTop(label, Math.Round(centerY - h * 0.5));
            if (Math.Abs(b.AngleDegrees) >= 0.5f)
            {
                label.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                label.RenderTransform = new RotateTransform(b.AngleDegrees);
            }

            _canvas.Children.Add(label);
        }
    }

    private void RenderDebugWords(IReadOnlyList<OverlayDebugWord> debugWords, double scaling)
    {
        var stroke = new SolidColorBrush(Colors.Red);
        foreach (var word in debugWords)
        {
            if (string.IsNullOrWhiteSpace(word.Text) && word.Bounds.IsEmpty)
                continue;

            var b = word.Bounds;
            var poly = new Polygon
            {
                Stroke = stroke,
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent,
                Points =
                [
                    new Point(b.P1.X / scaling, b.P1.Y / scaling),
                    new Point(b.P2.X / scaling, b.P2.Y / scaling),
                    new Point(b.P3.X / scaling, b.P3.Y / scaling),
                    new Point(b.P4.X / scaling, b.P4.Y / scaling),
                ],
            };
            _canvas.Children.Add(poly);

            if (string.IsNullOrWhiteSpace(word.Text))
                continue;

            var fontSizeDip = 8;
            var centerX = (b.P5.X + b.P6.X) * 0.5 / scaling;
            var centerY = (b.P5.Y + b.P6.Y) * 0.5 / scaling;
            var label = new TextBlock
            {
                Text = word.Text,
                FontSize = fontSizeDip,
                Foreground = stroke,
                TextWrapping = TextWrapping.NoWrap,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var w = label.DesiredSize.Width;
            var h = label.DesiredSize.Height;
            Canvas.SetLeft(label, Math.Round(centerX - w * 0.5));
            Canvas.SetTop(label, Math.Round(centerY - h * 0.5));
            if (Math.Abs(b.AngleDegrees) >= 0.5f)
            {
                label.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                label.RenderTransform = new RotateTransform(b.AngleDegrees);
            }

            _canvas.Children.Add(label);
        }
    }

    public void ClearItems()
    {
        _lastRenderKey = null;
        _hitTargets.Clear();
        SetHoveredKey(null);
        _canvas.Children.Clear();
    }

    private static string BuildRenderKey(
        IReadOnlyList<OverlayDrawSpec> specs,
        IReadOnlyList<OverlayDebugWord>? debugWords,
        IReadOnlyList<OverlayDebugLine>? debugMatchedLines)
    {
        // Integer geometry + rounded font — ignore sub-pixel noise.
        var extra = (debugWords?.Count ?? 0) + (debugMatchedLines?.Count ?? 0);
        var parts = new string[specs.Count + extra];
        for (var i = 0; i < specs.Count; i++)
        {
            var s = specs[i];
            parts[i] =
                $"{s.Text}|{s.DrawBounds.X},{s.DrawBounds.Y},{s.DrawBounds.Width},{s.DrawBounds.Height}|{s.AngleDegrees:F1}|{s.FontSize:F0}|{s.Background}|{s.BackgroundOpacity}|{s.BackgroundColor}|{s.TextColor}|{s.Outline}|{s.VAlign}|{s.IsMarker}|{s.SourceKey}|{s.WrapWords}";
        }

        var idx = specs.Count;
        if (debugMatchedLines is not null)
        {
            foreach (var line in debugMatchedLines)
            {
                var b = line.Bounds;
                parts[idx++] =
                    $"dbgL|{line.Text}|{b.P1.X:F0},{b.P1.Y:F0},{b.P2.X:F0},{b.P2.Y:F0},{b.P3.X:F0},{b.P3.Y:F0},{b.P4.X:F0},{b.P4.Y:F0}";
            }
        }

        if (debugWords is not null)
        {
            foreach (var w in debugWords)
            {
                var b = w.Bounds;
                parts[idx++] =
                    $"dbg|{w.Text}|{b.P1.X:F0},{b.P1.Y:F0},{b.P2.X:F0},{b.P2.Y:F0},{b.P3.X:F0},{b.P3.Y:F0},{b.P4.X:F0},{b.P4.Y:F0}";
            }
        }

        return string.Join('\n', parts);
    }

    private static Avalonia.Layout.VerticalAlignment ToAvaloniaVAlign(OverlayVAlign align) =>
        align switch
        {
            OverlayVAlign.Bottom => Avalonia.Layout.VerticalAlignment.Bottom,
            OverlayVAlign.Center => Avalonia.Layout.VerticalAlignment.Center,
            _ => Avalonia.Layout.VerticalAlignment.Top,
        };

    private static IBrush? CreateBackground(OverlayDrawSpec spec)
    {
        if (spec.Background is OverlayLayoutSettingKeys.BackgroundNone)
            return null;

        var alpha = spec.Background == OverlayLayoutSettingKeys.BackgroundOpaque
            ? (byte)255
            : (byte)Math.Clamp((int)(spec.BackgroundOpacity / 100.0 * 255), 0, 255);

        var (r, g, b) = spec.BackgroundColor is OverlayLayoutSettingKeys.BackgroundColorLight
            ? ((byte)245, (byte)245, (byte)245)
            : ((byte)20, (byte)20, (byte)20);

        return new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
    }

    private static IBrush ResolveForeground(OverlayDrawSpec spec)
    {
        var color = spec.TextColor switch
        {
            OverlayLayoutSettingKeys.TextColorDark => Colors.Black,
            OverlayLayoutSettingKeys.TextColorCream => Color.FromRgb(0xFF, 0xF8, 0xE1),
            OverlayLayoutSettingKeys.TextColorYellow => Color.FromRgb(0xFF, 0xEB, 0x3B),
            OverlayLayoutSettingKeys.TextColorCyan => Color.FromRgb(0x00, 0xE5, 0xFF),
            OverlayLayoutSettingKeys.TextColorLime => Color.FromRgb(0xB2, 0xFF, 0x59),
            OverlayLayoutSettingKeys.TextColorOrange => Color.FromRgb(0xFF, 0xAB, 0x40),
            // light, legacy "auto", or unknown
            _ => Colors.White,
        };
        return new SolidColorBrush(color);
    }

    public static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
