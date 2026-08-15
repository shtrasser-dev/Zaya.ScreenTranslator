using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Aligns a capture frame to the window client area and crops to capture regions.
/// </summary>
public interface IOcrFramePreparer
{
    PreparedOcrFrame Prepare(IRawImage frame, nint windowHandle, CaptureRegionsConfig regions);
}
