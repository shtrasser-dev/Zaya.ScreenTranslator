using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class OcrFramePreparer : IOcrFramePreparer
{
    private readonly ICaptureFrameProcessor _captureFrameProcessor;

    public OcrFramePreparer(ICaptureFrameProcessor captureFrameProcessor)
    {
        _captureFrameProcessor = captureFrameProcessor;
    }

    public PreparedOcrFrame Prepare(IRawImage frame, nint windowHandle, CaptureRegionsConfig regions)
    {
        var aligned = windowHandle != 0
            ? _captureFrameProcessor.TryAlignToClientArea(frame, windowHandle)
            : null;
        var clientFrame = (IRawImage)(aligned ?? frame);
        var processed = _captureFrameProcessor.TryProcess(clientFrame, regions);
        var image = (IRawImage)(processed ?? clientFrame);
        return new PreparedOcrFrame(
            image,
            processed?.OriginX ?? 0,
            processed?.OriginY ?? 0,
            aligned,
            processed);
    }
}
