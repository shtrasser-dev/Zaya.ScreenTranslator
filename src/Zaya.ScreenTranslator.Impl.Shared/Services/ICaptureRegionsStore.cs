using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface ICaptureRegionsStore
{
    string CaptureKey { get; }
    string IgnoreKey { get; }
    string ColX { get; }
    string ColY { get; }
    string ColWidth { get; }
    string ColHeight { get; }

    CaptureRegionsConfig Load(IApplicationProfile profile);

    void Save(IApplicationProfile profile, CaptureRegionsConfig config);
}
