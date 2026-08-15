using System.Buffers;
using System.Drawing;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Native;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Crops to the capture-region union and blacks out non-capture / ignore areas.
/// Coordinates on <see cref="ProcessedFrame.OriginX"/> / <see cref="ProcessedFrame.OriginY"/>
/// are client-space offsets so overlay bounds can be remapped.
/// </summary>
public sealed class CaptureFrameProcessor : ICaptureFrameProcessor
{
    private const int SizeTolerancePx = 2;

    public sealed class ProcessedFrame : IRawImage
    {
        private readonly byte[] _data;
        private readonly bool _pooled;
        private bool _disposed;

        public ProcessedFrame(byte[] data, int width, int height, int stride, PixelFormat format, int originX, int originY, bool pooled)
        {
            _data = data;
            _pooled = pooled;
            Width = width;
            Height = height;
            Stride = stride;
            Format = format;
            OriginX = originX;
            OriginY = originY;
        }

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public PixelFormat Format { get; }
        public int OriginX { get; }
        public int OriginY { get; }

        public ReadOnlySpan<byte> GetPixelData()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _data.AsSpan(0, Stride * Height);
        }

        public byte[] ToByteArray()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _data.AsSpan(0, Stride * Height).ToArray();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_pooled)
                ArrayPool<byte>.Shared.Return(_data);
            _disposed = true;
        }
    }

    /// <summary>
    /// Crops WGC chrome (title/menu) so the frame matches the window client area used by the overlay.
    /// Returns null when the frame already matches the client (typical for browsers with in-client chrome).
    /// Origin is 0 — the cropped image is already in client space.
    /// </summary>
    public ProcessedFrame? TryAlignToClientArea(IRawImage source, nint windowHandle)
    {
        if (windowHandle == 0
            || !Win32WindowBounds.TryGetClientAlignMetrics(
                windowHandle,
                out var outerW, out var outerH,
                out var insetX, out var insetY,
                out var clientW, out var clientH))
            return null;

        var srcW = source.Width;
        var srcH = source.Height;
        if (srcW <= 0 || srcH <= 0)
            return null;

        // Already client-sized — nothing to do (Edge/Chrome-style windows).
        if (WithinTolerance(srcW, clientW) && WithinTolerance(srcH, clientH))
            return null;

        // No non-client inset and frame isn't larger than client — cannot map.
        if (insetX <= 0 && insetY <= 0)
            return null;

        if (insetX < 0 || insetY < 0 || outerW <= 0 || outerH <= 0)
            return null;

        var scaleX = srcW / (double)outerW;
        var scaleY = srcH / (double)outerH;
        var cropX = (int)Math.Round(insetX * scaleX);
        var cropY = (int)Math.Round(insetY * scaleY);
        var cropW = (int)Math.Round(clientW * scaleX);
        var cropH = (int)Math.Round(clientH * scaleY);

        cropX = Math.Clamp(cropX, 0, srcW - 1);
        cropY = Math.Clamp(cropY, 0, srcH - 1);
        cropW = Math.Clamp(cropW, 1, srcW - cropX);
        cropH = Math.Clamp(cropH, 1, srcH - cropY);

        // Crop must be a no-op or meaningfully closer to the client size.
        if (cropX == 0 && cropY == 0 && cropW == srcW && cropH == srcH)
            return null;
        if (!WithinTolerance(cropW, clientW) && Math.Abs(cropW - clientW) >= Math.Abs(srcW - clientW))
            return null;
        if (!WithinTolerance(cropH, clientH) && Math.Abs(cropH - clientH) >= Math.Abs(srcH - clientH))
            return null;

        return Crop(source, cropX, cropY, cropW, cropH, originX: 0, originY: 0);
    }

    /// <summary>
    /// Returns null when no processing is needed (caller should keep the original frame).
    /// Otherwise returns a new frame; the caller must dispose it (and may dispose the source).
    /// </summary>
    public ProcessedFrame? TryProcess(IRawImage source, CaptureRegionsConfig config)
    {
        if (config.IsEmpty)
            return null;

        var bpp = source.Format.BytesPerPixel;
        if (bpp <= 0)
            return null;

        var srcW = source.Width;
        var srcH = source.Height;
        if (srcW <= 0 || srcH <= 0)
            return null;

        var capturePx = config.CaptureRegions
            .Select(r => ToPixels(r, srcW, srcH))
            .Where(r => r.Width > 0 && r.Height > 0)
            .ToList();
        var ignorePx = config.IgnoreRegions
            .Select(r => ToPixels(r, srcW, srcH))
            .Where(r => r.Width > 0 && r.Height > 0)
            .ToList();

        int cropX = 0, cropY = 0, cropW = srcW, cropH = srcH;
        if (capturePx.Count > 0)
        {
            cropX = capturePx.Min(r => r.X);
            cropY = capturePx.Min(r => r.Y);
            var right = capturePx.Max(r => r.Right);
            var bottom = capturePx.Max(r => r.Bottom);
            cropW = Math.Max(1, right - cropX);
            cropH = Math.Max(1, bottom - cropY);
        }

        cropX = Math.Clamp(cropX, 0, srcW - 1);
        cropY = Math.Clamp(cropY, 0, srcH - 1);
        cropW = Math.Clamp(cropW, 1, srcW - cropX);
        cropH = Math.Clamp(cropH, 1, srcH - cropY);

        var src = source.GetPixelData();
        var srcStride = source.Stride;
        var dstStride = cropW * bpp;
        var dst = ArrayPool<byte>.Shared.Rent(dstStride * cropH);

        try
        {
            if (capturePx.Count == 0)
            {
                // Full (or cropped-to-full) copy, then black ignore regions.
                CopyRect(src, srcStride, bpp, srcW, srcH, cropX, cropY, cropW, cropH, dst, dstStride, 0, 0);
            }
            else
            {
                // Black canvas, then copy capture islands into crop space.
                dst.AsSpan(0, dstStride * cropH).Clear();
                foreach (var cap in capturePx)
                {
                    var inter = Rectangle.Intersect(cap, new Rectangle(cropX, cropY, cropW, cropH));
                    if (inter.Width <= 0 || inter.Height <= 0)
                        continue;
                    CopyRect(
                        src, srcStride, bpp, srcW, srcH,
                        inter.X, inter.Y, inter.Width, inter.Height,
                        dst, dstStride,
                        inter.X - cropX, inter.Y - cropY);
                }
            }

            foreach (var ign in ignorePx)
            {
                var inter = Rectangle.Intersect(ign, new Rectangle(cropX, cropY, cropW, cropH));
                if (inter.Width <= 0 || inter.Height <= 0)
                    continue;
                FillRectBlack(dst, dstStride, bpp, inter.X - cropX, inter.Y - cropY, inter.Width, inter.Height, cropW, cropH);
            }

            return new ProcessedFrame(dst, cropW, cropH, dstStride, source.Format, cropX, cropY, pooled: true);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(dst);
            throw;
        }
    }

    public Rectangle ToPixels(PercentRect r, int windowW, int windowH)
    {
        var c = r.Clamp();
        var x = (int)Math.Floor(c.X / 100.0 * windowW);
        var y = (int)Math.Floor(c.Y / 100.0 * windowH);
        var right = (int)Math.Ceiling((c.X + c.Width) / 100.0 * windowW);
        var bottom = (int)Math.Ceiling((c.Y + c.Height) / 100.0 * windowH);
        x = Math.Clamp(x, 0, Math.Max(0, windowW - 1));
        y = Math.Clamp(y, 0, Math.Max(0, windowH - 1));
        right = Math.Clamp(right, x, windowW);
        bottom = Math.Clamp(bottom, y, windowH);
        return Rectangle.FromLTRB(x, y, right, bottom);
    }

    public PercentRect FromPixels(Rectangle px, int windowW, int windowH)
    {
        if (windowW <= 0 || windowH <= 0 || px.Width <= 0 || px.Height <= 0)
            return default;

        return new PercentRect(
            100.0 * px.X / windowW,
            100.0 * px.Y / windowH,
            100.0 * px.Width / windowW,
            100.0 * px.Height / windowH).Clamp();
    }

    private static ProcessedFrame? Crop(IRawImage source, int cropX, int cropY, int cropW, int cropH, int originX, int originY)
    {
        var bpp = source.Format.BytesPerPixel;
        if (bpp <= 0)
            return null;

        var srcW = source.Width;
        var srcH = source.Height;
        var src = source.GetPixelData();
        var srcStride = source.Stride;
        var dstStride = cropW * bpp;
        var dst = ArrayPool<byte>.Shared.Rent(dstStride * cropH);
        try
        {
            CopyRect(src, srcStride, bpp, srcW, srcH, cropX, cropY, cropW, cropH, dst, dstStride, 0, 0);
            return new ProcessedFrame(dst, cropW, cropH, dstStride, source.Format, originX, originY, pooled: true);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(dst);
            throw;
        }
    }

    private static bool WithinTolerance(int a, int b)
        => Math.Abs(a - b) <= SizeTolerancePx;

    private static void CopyRect(
        ReadOnlySpan<byte> src, int srcStride, int bpp, int srcW, int srcH,
        int srcX, int srcY, int width, int height,
        Span<byte> dst, int dstStride,
        int dstX, int dstY)
    {
        var rowBytes = width * bpp;
        for (var row = 0; row < height; row++)
        {
            var sy = srcY + row;
            var dy = dstY + row;
            if ((uint)sy >= (uint)srcH || (uint)dy >= (uint)(dst.Length / dstStride))
                continue;
            var srcOffset = sy * srcStride + srcX * bpp;
            var dstOffset = dy * dstStride + dstX * bpp;
            if (srcOffset + rowBytes > src.Length || dstOffset + rowBytes > dst.Length)
                continue;
            src.Slice(srcOffset, rowBytes).CopyTo(dst.Slice(dstOffset, rowBytes));
        }
    }

    private static void FillRectBlack(
        Span<byte> dst, int dstStride, int bpp,
        int x, int y, int width, int height,
        int dstW, int dstH)
    {
        var rowBytes = width * bpp;
        for (var row = 0; row < height; row++)
        {
            var dy = y + row;
            if ((uint)dy >= (uint)dstH)
                continue;
            var offset = dy * dstStride + x * bpp;
            if (offset < 0 || offset + rowBytes > dst.Length)
                continue;
            dst.Slice(offset, rowBytes).Clear();
        }
    }
}
