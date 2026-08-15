using System.Drawing;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface ICaptureFrameProcessor
{
    CaptureFrameProcessor.ProcessedFrame? TryAlignToClientArea(IRawImage source, nint windowHandle);

    CaptureFrameProcessor.ProcessedFrame? TryProcess(IRawImage source, CaptureRegionsConfig config);

    Rectangle ToPixels(PercentRect r, int windowW, int windowH);

    PercentRect FromPixels(Rectangle px, int windowW, int windowH);
}
