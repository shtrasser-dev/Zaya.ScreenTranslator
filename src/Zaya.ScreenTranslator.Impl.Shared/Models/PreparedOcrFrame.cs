using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Frame ready for OCR: optional client-align and region crop.
/// Disposes owned intermediates; does not dispose the original capture frame.
/// </summary>
public sealed class PreparedOcrFrame : IDisposable
{
    private readonly IDisposable? _aligned;
    private readonly IDisposable? _processed;
    private bool _disposed;

    public PreparedOcrFrame(
        IRawImage image,
        int originX,
        int originY,
        IDisposable? aligned,
        IDisposable? processed)
    {
        Image = image;
        OriginX = originX;
        OriginY = originY;
        _aligned = aligned;
        _processed = processed;
    }

    public IRawImage Image { get; }
    public int OriginX { get; }
    public int OriginY { get; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _processed?.Dispose();
        _aligned?.Dispose();
    }
}
