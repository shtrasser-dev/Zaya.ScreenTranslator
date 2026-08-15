using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Continuous capture → OCR → layout → translate loop.
/// </summary>
public interface ITranslationLoopService
{
    Task RunAsync(
        TranslationLoopRuntime runtime,
        Func<IApplicationProfile?> getProfile,
        ITranslationModuleRefresh? moduleRefresh,
        CancellationToken ct,
        Action<string> onTextUpdated,
        Action<(string Text, bool IsError)> onStatus,
        Action<double, double, double>? onTimings = null,
        Action<IReadOnlyList<(string Source, string Translation)>>? onTranslatedPairs = null);
}
