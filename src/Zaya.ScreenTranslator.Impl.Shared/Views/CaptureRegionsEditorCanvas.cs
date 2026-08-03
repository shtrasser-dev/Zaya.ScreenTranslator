using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using AvColor = Avalonia.Media.Color;
using AvBrushes = Avalonia.Media.Brushes;
using AvPoint = Avalonia.Point;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

/// <summary>
/// Screenshot preview with interactive capture/ignore rectangle drawing.
/// </summary>
public sealed class CaptureRegionsEditorCanvas : Panel
{
    private const double ChromeButtonSize = 18;
    private const double ChromeInset = 2;
    private const double EdgeHitThickness = 7;
    private const double MinRegionPercent = 0.5;

    // 16×16 filled "move" glyph (four arrows).
    private static readonly Geometry MoveIconGeometry = StreamGeometry.Parse(
        "M8,0 L10.5,3.5 H9 V6.5 H12.5 V5 L16,8 L12.5,11 V9.5 H9 V12.5 H10.5 L8,16 L5.5,12.5 H7 V9.5 H3.5 V11 L0,8 L3.5,5 V6.5 H7 V3.5 H5.5 Z");

    // 16×16 X glyph.
    private static readonly Geometry CloseIconGeometry = StreamGeometry.Parse(
        "M3.5,2.5 L8,7 L12.5,2.5 L13.5,3.5 L9,8 L13.5,12.5 L12.5,13.5 L8,9 L3.5,13.5 L2.5,12.5 L7,8 L2.5,3.5 Z");

    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Canvas _overlay = new();
    private readonly List<EditableCaptureRegion> _regions;

    private CaptureRegionKind? _drawKind;
    private bool _drawing;
    private AvPoint _start;
    private AvPoint _current;
    private Border? _preview;

    private InteractMode _interactMode;
    private ResizeEdge _resizeEdge;
    private EditableCaptureRegion? _activeRegion;
    private Border? _activeBox;
    private Border? _moveHandle;
    private Border? _deleteHandle;
    private PercentRect _interactStartRect;
    private AvPoint _interactOrigin;

    private enum InteractMode
    {
        None,
        Move,
        Resize,
    }

    [Flags]
    private enum ResizeEdge
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
    }

    public CaptureRegionsEditorCanvas(List<EditableCaptureRegion> regions)
    {
        _regions = regions;
        Children.Add(_image);
        Children.Add(_overlay);

        _overlay.Background = AvBrushes.Transparent;
        _overlay.PointerPressed += OnPointerPressed;
        _overlay.PointerMoved += OnPointerMoved;
        _overlay.PointerReleased += OnPointerReleased;
        _overlay.PointerCaptureLost += (_, _) =>
        {
            CancelDrawing();
            var hadInteract = _interactMode != InteractMode.None;
            EndInteract(commit: true);
            if (hadInteract)
                RebuildVisuals();
        };
    }

    public void SetBitmap(Bitmap bitmap, double displayWidth, double displayHeight)
    {
        _image.Source = bitmap;
        Width = displayWidth;
        Height = displayHeight;
        _image.Width = displayWidth;
        _image.Height = displayHeight;
        _overlay.Width = displayWidth;
        _overlay.Height = displayHeight;
        RebuildVisuals();
    }

    public void BeginDraw(CaptureRegionKind kind)
    {
        _drawKind = kind;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public void CancelDrawMode()
    {
        _drawKind = null;
        CancelDrawing();
        Cursor = Cursor.Default;
    }

    public void RebuildVisuals()
    {
        EndInteract(commit: false);
        _overlay.Children.Clear();
        _preview = null;
        var w = Math.Max(1, _overlay.Width);
        var h = Math.Max(1, _overlay.Height);

        foreach (var region in _regions)
        {
            var c = region.Rect.Clamp();
            if (c.IsEmpty)
                continue;

            var brush = region.Kind == CaptureRegionKind.Capture
                ? new SolidColorBrush(AvColor.FromArgb(100, 40, 180, 70))
                : new SolidColorBrush(AvColor.FromArgb(100, 200, 50, 50));
            var borderBrush = region.Kind == CaptureRegionKind.Capture
                ? new SolidColorBrush(AvColor.FromArgb(220, 40, 200, 80))
                : new SolidColorBrush(AvColor.FromArgb(220, 220, 60, 60));

            var left = c.X / 100.0 * w;
            var top = c.Y / 100.0 * h;
            var boxW = Math.Max(1, c.Width / 100.0 * w);
            var boxH = Math.Max(1, c.Height / 100.0 * h);

            var box = new Border
            {
                Background = brush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false,
                Width = boxW,
                Height = boxH,
            };
            Canvas.SetLeft(box, left);
            Canvas.SetTop(box, top);
            _overlay.Children.Add(box);

            var moveHandle = CreateChromeButton(MoveIconGeometry, StandardCursorType.SizeAll);
            var deleteHandle = CreateChromeButton(CloseIconGeometry, StandardCursorType.Hand);
            Canvas.SetLeft(moveHandle, left + ChromeInset);
            Canvas.SetTop(moveHandle, top + ChromeInset);
            Canvas.SetLeft(deleteHandle, left + boxW - ChromeButtonSize - ChromeInset);
            Canvas.SetTop(deleteHandle, top + ChromeInset);

            AddResizeEdges(region, box, moveHandle, deleteHandle, left, top, boxW, boxH);

            moveHandle.PointerPressed += (_, e) =>
                BeginInteract(InteractMode.Move, ResizeEdge.None, region, box, moveHandle, deleteHandle, e);
            deleteHandle.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(_overlay).Properties.IsLeftButtonPressed)
                    return;
                e.Handled = true;
                CancelDrawing();
                EndInteract(commit: false);
                _regions.Remove(region);
                RebuildVisuals();
            };

            // Chrome buttons above edge hit-targets.
            _overlay.Children.Add(moveHandle);
            _overlay.Children.Add(deleteHandle);
        }
    }

    private void AddResizeEdges(
        EditableCaptureRegion region,
        Border box,
        Border moveHandle,
        Border deleteHandle,
        double left,
        double top,
        double boxW,
        double boxH)
    {
        var t = EdgeHitThickness;

        void AddEdge(ResizeEdge edge, double x, double y, double ew, double eh, StandardCursorType cursor)
        {
            var hit = new Border
            {
                Width = Math.Max(1, ew),
                Height = Math.Max(1, eh),
                Background = AvBrushes.Transparent,
                Cursor = new Cursor(cursor),
                IsHitTestVisible = true,
            };
            Canvas.SetLeft(hit, x);
            Canvas.SetTop(hit, y);
            hit.PointerPressed += (_, e) =>
                BeginInteract(InteractMode.Resize, edge, region, box, moveHandle, deleteHandle, e);
            _overlay.Children.Add(hit);
        }

        AddEdge(ResizeEdge.Top, left + t, top - t * 0.5, boxW - 2 * t, t, StandardCursorType.SizeNorthSouth);
        AddEdge(ResizeEdge.Bottom, left + t, top + boxH - t * 0.5, boxW - 2 * t, t, StandardCursorType.SizeNorthSouth);
        AddEdge(ResizeEdge.Left, left - t * 0.5, top + t, t, boxH - 2 * t, StandardCursorType.SizeWestEast);
        AddEdge(ResizeEdge.Right, left + boxW - t * 0.5, top + t, t, boxH - 2 * t, StandardCursorType.SizeWestEast);

        AddEdge(ResizeEdge.Left | ResizeEdge.Top, left - t * 0.5, top - t * 0.5, t, t, StandardCursorType.TopLeftCorner);
        AddEdge(ResizeEdge.Right | ResizeEdge.Top, left + boxW - t * 0.5, top - t * 0.5, t, t, StandardCursorType.TopRightCorner);
        AddEdge(ResizeEdge.Left | ResizeEdge.Bottom, left - t * 0.5, top + boxH - t * 0.5, t, t, StandardCursorType.BottomLeftCorner);
        AddEdge(ResizeEdge.Right | ResizeEdge.Bottom, left + boxW - t * 0.5, top + boxH - t * 0.5, t, t, StandardCursorType.BottomRightCorner);
    }

    private static Border CreateChromeButton(Geometry icon, StandardCursorType cursor)
    {
        return new Border
        {
            Width = ChromeButtonSize,
            Height = ChromeButtonSize,
            Background = new SolidColorBrush(AvColor.FromArgb(200, 20, 20, 20)),
            CornerRadius = new CornerRadius(3),
            Child = new Viewbox
            {
                Width = 12,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Avalonia.Controls.Shapes.Path
                {
                    Data = icon,
                    Fill = AvBrushes.White,
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Uniform,
                },
            },
            Cursor = new Cursor(cursor),
            IsHitTestVisible = true,
        };
    }

    private void BeginInteract(
        InteractMode mode,
        ResizeEdge edge,
        EditableCaptureRegion region,
        Border box,
        Border? moveHandle,
        Border? deleteHandle,
        PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_overlay).Properties.IsLeftButtonPressed)
            return;

        CancelDrawing();
        _interactMode = mode;
        _resizeEdge = edge;
        _activeRegion = region;
        _activeBox = box;
        _moveHandle = moveHandle;
        _deleteHandle = deleteHandle;
        _interactStartRect = region.Rect.Clamp();
        _interactOrigin = e.GetPosition(_overlay);
        e.Pointer.Capture(_overlay);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_interactMode != InteractMode.None)
            return;
        if (_drawKind is null)
            return;
        if (!e.GetCurrentPoint(_overlay).Properties.IsLeftButtonPressed)
            return;

        _drawing = true;
        _start = e.GetPosition(_overlay);
        _current = _start;
        e.Pointer.Capture(_overlay);

        var brush = _drawKind == CaptureRegionKind.Capture
            ? new SolidColorBrush(AvColor.FromArgb(120, 40, 180, 70))
            : new SolidColorBrush(AvColor.FromArgb(120, 200, 50, 50));
        _preview = new Border
        {
            Background = brush,
            BorderBrush = AvBrushes.White,
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
        };
        _overlay.Children.Add(_preview);
        UpdatePreview();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_interactMode != InteractMode.None)
        {
            UpdateInteract(e.GetPosition(_overlay));
            return;
        }

        if (!_drawing || _preview is null)
            return;
        _current = e.GetPosition(_overlay);
        UpdatePreview();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_interactMode != InteractMode.None)
        {
            UpdateInteract(e.GetPosition(_overlay));
            e.Pointer.Capture(null);
            EndInteract(commit: true);
            // Edge hit-targets are not moved live; rebuild so resize zones match the new rect.
            RebuildVisuals();
            return;
        }

        if (!_drawing || _drawKind is null)
            return;

        _current = e.GetPosition(_overlay);
        e.Pointer.Capture(null);
        _drawing = false;

        if (_preview is not null)
        {
            _overlay.Children.Remove(_preview);
            _preview = null;
        }

        var w = Math.Max(1, _overlay.Width);
        var h = Math.Max(1, _overlay.Height);
        var left = Math.Min(_start.X, _current.X);
        var top = Math.Min(_start.Y, _current.Y);
        var right = Math.Max(_start.X, _current.X);
        var bottom = Math.Max(_start.Y, _current.Y);

        if (right - left < 2 || bottom - top < 2)
            return;

        var percent = new PercentRect(
            100.0 * left / w,
            100.0 * top / h,
            100.0 * (right - left) / w,
            100.0 * (bottom - top) / h).Clamp();

        if (!percent.IsEmpty)
        {
            _regions.Add(new EditableCaptureRegion { Kind = _drawKind.Value, Rect = percent });
            RebuildVisuals();
        }

        Cursor = new Cursor(StandardCursorType.Cross);
    }

    private void UpdateInteract(AvPoint position)
    {
        if (_activeRegion is null || _activeBox is null)
            return;

        var w = Math.Max(1, _overlay.Width);
        var h = Math.Max(1, _overlay.Height);
        var dxPct = 100.0 * (position.X - _interactOrigin.X) / w;
        var dyPct = 100.0 * (position.Y - _interactOrigin.Y) / h;
        var r = _interactStartRect;

        PercentRect next;
        if (_interactMode == InteractMode.Move)
        {
            var x = Math.Clamp(r.X + dxPct, 0, 100 - r.Width);
            var y = Math.Clamp(r.Y + dyPct, 0, 100 - r.Height);
            next = new PercentRect(x, y, r.Width, r.Height);
        }
        else
        {
            var left = r.X;
            var top = r.Y;
            var right = r.X + r.Width;
            var bottom = r.Y + r.Height;

            if (_resizeEdge.HasFlag(ResizeEdge.Left))
                left = r.X + dxPct;
            if (_resizeEdge.HasFlag(ResizeEdge.Right))
                right = r.X + r.Width + dxPct;
            if (_resizeEdge.HasFlag(ResizeEdge.Top))
                top = r.Y + dyPct;
            if (_resizeEdge.HasFlag(ResizeEdge.Bottom))
                bottom = r.Y + r.Height + dyPct;

            left = Math.Clamp(left, 0, 100);
            top = Math.Clamp(top, 0, 100);
            right = Math.Clamp(right, 0, 100);
            bottom = Math.Clamp(bottom, 0, 100);

            if (right < left)
                (left, right) = (right, left);
            if (bottom < top)
                (top, bottom) = (bottom, top);

            var width = Math.Max(MinRegionPercent, right - left);
            var height = Math.Max(MinRegionPercent, bottom - top);
            if (left + width > 100)
                left = 100 - width;
            if (top + height > 100)
                top = 100 - height;
            next = new PercentRect(left, top, width, height);
        }

        _activeRegion.Rect = next;
        ApplyChromeLayout(next);
    }

    private void ApplyChromeLayout(PercentRect rect)
    {
        if (_activeBox is null)
            return;

        var w = Math.Max(1, _overlay.Width);
        var h = Math.Max(1, _overlay.Height);
        var left = rect.X / 100.0 * w;
        var top = rect.Y / 100.0 * h;
        var boxW = Math.Max(1, rect.Width / 100.0 * w);
        var boxH = Math.Max(1, rect.Height / 100.0 * h);

        Canvas.SetLeft(_activeBox, left);
        Canvas.SetTop(_activeBox, top);
        _activeBox.Width = boxW;
        _activeBox.Height = boxH;

        if (_moveHandle is not null)
        {
            Canvas.SetLeft(_moveHandle, left + ChromeInset);
            Canvas.SetTop(_moveHandle, top + ChromeInset);
        }

        if (_deleteHandle is not null)
        {
            Canvas.SetLeft(_deleteHandle, left + boxW - ChromeButtonSize - ChromeInset);
            Canvas.SetTop(_deleteHandle, top + ChromeInset);
        }
    }

    private void EndInteract(bool commit)
    {
        if (_interactMode == InteractMode.None)
            return;

        if (commit && _activeRegion is not null)
            _activeRegion.Rect = _activeRegion.Rect.Clamp();

        _interactMode = InteractMode.None;
        _resizeEdge = ResizeEdge.None;
        _activeRegion = null;
        _activeBox = null;
        _moveHandle = null;
        _deleteHandle = null;
    }

    private void UpdatePreview()
    {
        if (_preview is null)
            return;
        var left = Math.Min(_start.X, _current.X);
        var top = Math.Min(_start.Y, _current.Y);
        Canvas.SetLeft(_preview, left);
        Canvas.SetTop(_preview, top);
        _preview.Width = Math.Max(1, Math.Abs(_current.X - _start.X));
        _preview.Height = Math.Max(1, Math.Abs(_current.Y - _start.Y));
    }

    private void CancelDrawing()
    {
        _drawing = false;
        if (_preview is not null)
        {
            _overlay.Children.Remove(_preview);
            _preview = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(Width > 0 ? Width : 0, Height > 0 ? Height : 0);
        _image.Measure(size);
        _overlay.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(0, 0, Width > 0 ? Width : finalSize.Width, Height > 0 ? Height : finalSize.Height);
        _image.Arrange(rect);
        _overlay.Arrange(rect);
        return finalSize;
    }
}
