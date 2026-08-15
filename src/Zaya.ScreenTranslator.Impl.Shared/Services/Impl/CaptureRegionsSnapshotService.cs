using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Zaya.Primitives;
using Zaya.Screenshot.Models;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Native;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using ZayaPixelFormat = Zaya.Primitives.PixelFormat;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Window capture for the capture-regions editor.
/// Keeps capturing until two consecutive usable non-black frames share the same size.
/// </summary>
public sealed class CaptureRegionsSnapshotService : ICaptureRegionsSnapshotService
{
    private readonly IEngineFactory _engineFactory;
    private readonly ICaptureFrameProcessor _captureFrameProcessor;
    private readonly IConfigurationPathService _configurationPathService;

    public CaptureRegionsSnapshotService(
        IEngineFactory engineFactory,
        ICaptureFrameProcessor captureFrameProcessor,
        IConfigurationPathService configurationPathService)
    {
        _engineFactory = engineFactory;
        _captureFrameProcessor = captureFrameProcessor;
        _configurationPathService = configurationPathService;
    }

    public sealed record Snapshot(WriteableBitmap Bitmap, int PixelWidth, int PixelHeight);

    private static readonly TimeSpan CaptureInterval = TimeSpan.FromSeconds(1);

    /// <summary>Reject WGC thumbnail / minimized surfaces (often ~160×~30).</summary>
    private const int AbsoluteMinWidth = 200;
    private const int AbsoluteMinHeight = 100;

    public int PlaceholderSize => PlaceholderSizeValue;

    private const int PlaceholderSizeValue = 800;

    /// <summary>
    /// Fully transparent square used as the editor backdrop when no window is selected.
    /// </summary>
    public Snapshot CreatePlaceholderSnapshot(int size = PlaceholderSizeValue)
    {
        if (size < 1)
            size = PlaceholderSizeValue;

        var bmp = new WriteableBitmap(
            new PixelSize(size, size),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Unpremul);

        using var fb = bmp.Lock();
        var bytes = new byte[fb.RowBytes * size];
        Marshal.Copy(bytes, 0, fb.Address, bytes.Length);

        return new Snapshot(bmp, size, size);
    }

    public async Task<Snapshot?> CaptureUntilStableAsync(
        IApplicationProfile profile,
        nint windowHandle,
        CancellationToken cancellationToken = default)
    {
        var engineId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture);
        using var capture = _engineFactory.CreateCapture(engineId);
        if (capture is null)
            return null;

        if (!profile.Settings.TryGetValue(engineId, out var captureSettings) || captureSettings is null)
            captureSettings = new Dictionary<string, object>();

        var region = new FullScreenWindowRegion
        {
            WindowHandle = windowHandle,
            PixelFormat = ZayaPixelFormat.Bgra32,
            CaptureClientArea = true,
        };

        using var session = await capture.CreateSessionAsync(
            region,
            ManagedSettingKeys.PrepareForEngine(_configurationPathService, capture.EngineId, capture.Settings, captureSettings),
            cancellationToken).ConfigureAwait(false);

        (int Width, int Height)? lastGoodSize = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            IRawImage? frame = null;
            try
            {
                frame = await session.CaptureAsync(cancellationToken).ConfigureAwait(false);
                if (frame is not null
                    && !IsFullyBlack(frame)
                    && IsUsableFrameSize(frame, windowHandle))
                {
                    using var aligned = _captureFrameProcessor.TryAlignToClientArea(frame, windowHandle);
                    var image = (IRawImage)(aligned ?? frame);
                    var size = (image.Width, image.Height);
                    if (lastGoodSize is { } prev && prev.Width == size.Width && prev.Height == size.Height)
                    {
                        var bmp = ToWriteableBitmap(image);
                        return new Snapshot(bmp, image.Width, image.Height);
                    }

                    lastGoodSize = size;
                }
                else
                {
                    lastGoodSize = null;
                }
            }
            finally
            {
                frame?.Dispose();
            }

            await Task.Delay(CaptureInterval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    /// Drops minimized-window / thumbnail frames that WGC can emit before the window is restored.
    /// </summary>
    public bool IsUsableFrameSize(IRawImage frame, nint windowHandle)
    {
        if (frame.Width < AbsoluteMinWidth || frame.Height < AbsoluteMinHeight)
            return false;

        if (Win32WindowBounds.IsMinimized(windowHandle))
            return false;

        if (Win32WindowBounds.TryGetClientSize(windowHandle, out var clientW, out var clientH)
            && clientW >= AbsoluteMinWidth
            && clientH >= AbsoluteMinHeight)
        {
            // Capture size should be in the ballpark of the real client area.
            // Thumbnail/min frames are far smaller; DPI/chrome can make capture slightly larger.
            if (frame.Width < clientW * 0.5 || frame.Height < clientH * 0.5)
                return false;
        }

        return true;
    }

    public bool IsFullyBlack(IRawImage image)
    {
        var w = image.Width;
        var h = image.Height;
        if (w <= 0 || h <= 0)
            return true;

        var data = image.GetPixelData();
        var stride = image.Stride;
        var bpp = image.Format.BytesPerPixel;
        if (bpp <= 0 || data.Length == 0)
            return true;

        const int step = 8;
        const int threshold = 8;
        for (var y = 0; y < h; y += step)
        {
            var row = y * stride;
            for (var x = 0; x < w; x += step)
            {
                var i = row + x * bpp;
                if (i + bpp > data.Length)
                    continue;

                int lum;
                if (image.Format == ZayaPixelFormat.Gray8)
                    lum = data[i];
                else if (image.Format == ZayaPixelFormat.Bgra32 || image.Format == ZayaPixelFormat.Bgr24)
                    lum = (data[i + 2] * 76 + data[i + 1] * 150 + data[i] * 29) >> 8;
                else if (image.Format == ZayaPixelFormat.Rgb24)
                    lum = (data[i] * 76 + data[i + 1] * 150 + data[i + 2] * 29) >> 8;
                else
                    lum = data[i];

                if (lum > threshold)
                    return false;
            }
        }

        return true;
    }

    private static WriteableBitmap ToWriteableBitmap(IRawImage image)
    {
        var w = image.Width;
        var h = image.Height;
        var bmp = new WriteableBitmap(
            new PixelSize(w, h),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Unpremul);

        using var fb = bmp.Lock();
        var srcStride = image.Stride;
        var dstStride = fb.RowBytes;
        var dstPtr = fb.Address;
        var bytes = image.ToByteArray();

        if (image.Format == ZayaPixelFormat.Bgra32)
        {
            if (srcStride == dstStride)
            {
                Marshal.Copy(bytes, 0, dstPtr, Math.Min(bytes.Length, dstStride * h));
            }
            else
            {
                for (var y = 0; y < h; y++)
                {
                    var srcOffset = y * srcStride;
                    var rowBytes = Math.Min(w * 4, Math.Min(srcStride, dstStride));
                    if (srcOffset + rowBytes > bytes.Length)
                        break;
                    Marshal.Copy(bytes, srcOffset, dstPtr + y * dstStride, rowBytes);
                }
            }

            return bmp;
        }

        var rowBuf = new byte[w * 4];
        for (var y = 0; y < h; y++)
        {
            var srcRow = y * srcStride;
            for (var x = 0; x < w; x++)
            {
                var si = srcRow + x * image.Format.BytesPerPixel;
                var di = x * 4;
                if (si + image.Format.BytesPerPixel > bytes.Length)
                    continue;

                byte b, g, r, a = 255;
                if (image.Format == ZayaPixelFormat.Bgr24)
                {
                    b = bytes[si];
                    g = bytes[si + 1];
                    r = bytes[si + 2];
                }
                else if (image.Format == ZayaPixelFormat.Rgb24)
                {
                    r = bytes[si];
                    g = bytes[si + 1];
                    b = bytes[si + 2];
                }
                else
                {
                    b = g = r = bytes[si];
                }

                rowBuf[di] = b;
                rowBuf[di + 1] = g;
                rowBuf[di + 2] = r;
                rowBuf[di + 3] = a;
            }

            Marshal.Copy(rowBuf, 0, dstPtr + y * dstStride, rowBuf.Length);
        }

        return bmp;
    }
}
