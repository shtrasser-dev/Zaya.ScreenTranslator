using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface ICaptureRegionsSnapshotService
{
    int PlaceholderSize { get; }

    CaptureRegionsSnapshotService.Snapshot CreatePlaceholderSnapshot(int size = 800);

    Task<CaptureRegionsSnapshotService.Snapshot?> CaptureUntilStableAsync(
        IApplicationProfile profile,
        nint windowHandle,
        CancellationToken cancellationToken = default);

    bool IsUsableFrameSize(Zaya.Primitives.IRawImage frame, nint windowHandle);

    bool IsFullyBlack(Zaya.Primitives.IRawImage image);
}
