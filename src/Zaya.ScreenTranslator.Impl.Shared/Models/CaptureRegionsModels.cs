namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Rectangle in percent of the target window client area (0–100).
/// </summary>
public readonly record struct PercentRect(double X, double Y, double Width, double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public PercentRect Clamp()
    {
        var x = Math.Clamp(X, 0, 100);
        var y = Math.Clamp(Y, 0, 100);
        var w = Math.Clamp(Width, 0, 100 - x);
        var h = Math.Clamp(Height, 0, 100 - y);
        return new PercentRect(x, y, w, h);
    }
}

public enum CaptureRegionKind
{
    Capture,
    Ignore,
}

public sealed class EditableCaptureRegion
{
    public CaptureRegionKind Kind { get; set; }
    public PercentRect Rect { get; set; }
}

public sealed class CaptureRegionsConfig
{
    public IReadOnlyList<PercentRect> CaptureRegions { get; init; } = [];
    public IReadOnlyList<PercentRect> IgnoreRegions { get; init; } = [];

    public bool HasCaptureRegions => CaptureRegions.Count > 0;
    public bool HasIgnoreRegions => IgnoreRegions.Count > 0;
    public bool IsEmpty => !HasCaptureRegions && !HasIgnoreRegions;
}
