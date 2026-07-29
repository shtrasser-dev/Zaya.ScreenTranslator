namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Describes what to capture.
/// Kept for future use — MVP only supports window capture.
/// </summary>
public enum CaptureTargetType
{
    /// <summary>Capture a specific window by handle.</summary>
    Window,
    /// <summary>Capture an entire monitor by index.</summary>
    Monitor,
    /// <summary>Capture a rectangular sub-region of a monitor.</summary>
    Region,
}
