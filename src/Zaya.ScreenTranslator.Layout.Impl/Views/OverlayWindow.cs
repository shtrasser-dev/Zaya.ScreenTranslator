using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Zaya.ScreenTranslator.Layout.Impl.Constants;
using Zaya.ScreenTranslator.Layout.Impl.Models;
using Zaya.ScreenTranslator.Layout.Impl.Native;

namespace Zaya.ScreenTranslator.Layout.Impl.Views;

/// <summary>
/// Transparent topmost overlay surface owned by the overlay-layout session.
/// </summary>
public sealed class OverlayWindow : Window
{
    private readonly Canvas _canvas = new();
    private bool _hasSyncedGeometry;
    private int _lastX;
    private int _lastY;
    private int _lastW;
    private int _lastH;
    private string? _lastRenderKey;

    public OverlayWindow()
    {
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Focusable = false;
        IsHitTestVisible = false;
        ShowActivated = false;
        Content = _canvas;

        Opened += (_, _) =>
        {
            var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (OperatingSystem.IsWindows())
                NativeWindowMethods.EnableClickThrough(handle);
        };
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

    public void RenderItems(IReadOnlyList<OverlayDrawSpec> specs)
    {
        var key = BuildRenderKey(specs);
        if (string.Equals(key, _lastRenderKey, StringComparison.Ordinal))
            return;
        _lastRenderKey = key;

        _canvas.Children.Clear();
        var scaling = Math.Max(0.1, RenderScaling);

        foreach (var spec in specs)
        {
            var panel = new Border
            {
                Padding = new Thickness(2),
                ClipToBounds = false,
                Background = CreateBackground(spec),
                Child = new TextBlock
                {
                    Text = spec.Text,
                    FontSize = Math.Max(8, spec.FontSize / scaling),
                    Foreground = ResolveForeground(spec),
                    TextWrapping = spec.FitMode == OverlayLayoutSettingKeys.FitWrap
                        ? TextWrapping.Wrap
                        : TextWrapping.NoWrap,
                    VerticalAlignment = ToAvaloniaVAlign(spec.VAlign),
                    Effect = spec.Outline
                        ? new DropShadowEffect
                        {
                            BlurRadius = 2,
                            OffsetX = 0,
                            OffsetY = 0,
                            Color = Colors.Black,
                            Opacity = 0.85,
                        }
                        : null,
                },
            };

            // Snap to whole DIPs to reduce sub-pixel shimmer.
            var left = Math.Round(spec.DrawBounds.X / scaling);
            var top = Math.Round(spec.DrawBounds.Y / scaling);
            Canvas.SetLeft(panel, left);
            Canvas.SetTop(panel, top);
            panel.Width = Math.Max(1, Math.Round(spec.DrawBounds.Width / scaling));
            var boxH = Math.Max(1, Math.Round(spec.DrawBounds.Height / scaling));
            if (spec.FitMode == OverlayLayoutSettingKeys.FitClip)
            {
                panel.Height = boxH;
                panel.ClipToBounds = true;
            }
            else
            {
                // Fixed box so above/below can bottom/top-align toward the source line.
                panel.Height = boxH;
            }

            _canvas.Children.Add(panel);
        }
    }

    public void ClearItems()
    {
        _lastRenderKey = null;
        _canvas.Children.Clear();
    }

    private static string BuildRenderKey(IReadOnlyList<OverlayDrawSpec> specs)
    {
        // Integer geometry + rounded font — ignore sub-pixel noise.
        var parts = new string[specs.Count];
        for (var i = 0; i < specs.Count; i++)
        {
            var s = specs[i];
            parts[i] =
                $"{s.Text}|{s.DrawBounds.X},{s.DrawBounds.Y},{s.DrawBounds.Width},{s.DrawBounds.Height}|{s.FontSize:F0}|{s.Background}|{s.BackgroundOpacity}|{s.TextColor}|{s.Outline}|{s.FitMode}|{s.VAlign}";
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

        return new SolidColorBrush(Color.FromArgb(alpha, 20, 20, 20));
    }

    private static IBrush ResolveForeground(OverlayDrawSpec spec)
    {
        var light = spec.TextColor is OverlayLayoutSettingKeys.TextColorLight
            or OverlayLayoutSettingKeys.TextColorAuto;
        return new SolidColorBrush(light ? Colors.White : Colors.Black);
    }

    public static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
