using System.Diagnostics;
using Avalonia.Threading;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class TranslationLoopService : ITranslationLoopService
{
    private readonly ICaptureRegionsStore _captureRegionsStore;
    private readonly IOcrFramePreparer _ocrFramePreparer;
    private readonly ITranslationBatchBuilder _translationBatchBuilder;
    private readonly IOverlayFrameMapper _overlayFrameMapper;
    private readonly ILocalizationService _localizationService;

    public TranslationLoopService(
        ICaptureRegionsStore captureRegionsStore,
        IOcrFramePreparer ocrFramePreparer,
        ITranslationBatchBuilder translationBatchBuilder,
        IOverlayFrameMapper overlayFrameMapper,
        ILocalizationService localizationService)
    {
        _captureRegionsStore = captureRegionsStore;
        _ocrFramePreparer = ocrFramePreparer;
        _translationBatchBuilder = translationBatchBuilder;
        _overlayFrameMapper = overlayFrameMapper;
        _localizationService = localizationService;
    }

    public async Task RunAsync(
        TranslationLoopRuntime runtime,
        Func<IApplicationProfile?> getProfile,
        ITranslationModuleRefresh? moduleRefresh,
        CancellationToken ct,
        Action<string> onTextUpdated,
        Action<(string Text, bool IsError)> onStatus,
        Action<double, double, double>? onTimings = null,
        Action<IReadOnlyList<(string Source, string Translation)>>? onTranslatedPairs = null)
    {
        try
        {
            await RunLoopAsync(
                runtime,
                getProfile,
                moduleRefresh,
                ct,
                onTextUpdated,
                onStatus,
                onTimings,
                onTranslatedPairs);
        }
        finally
        {
            DisposeQuietly(runtime.CaptureSession);
            DisposeQuietly(runtime.OcrSession);
            DisposeQuietly(runtime.LayoutSession);
            DisposeQuietly(runtime.TranslatorSession);
            // Overlay session is owned by the coordinator (may be swapped mid-run).
        }
    }

    private static void DisposeQuietly(IDisposable? disposable)
    {
        try { disposable?.Dispose(); } catch { /* ignore */ }
    }

    private async Task RunLoopAsync(
        TranslationLoopRuntime runtime,
        Func<IApplicationProfile?> getProfile,
        ITranslationModuleRefresh? moduleRefresh,
        CancellationToken ct,
        Action<string> onTextUpdated,
        Action<(string Text, bool IsError)> onStatus,
        Action<double, double, double>? onTimings,
        Action<IReadOnlyList<(string Source, string Translation)>>? onTranslatedPairs)
    {
        var captureTimes = new Queue<double>();
        var ocrTimes = new Queue<double>();
        var translatorTimes = new Queue<double>();
        const int windowSize = 10;

        onStatus((AppConstants.LoopStatus.Running, false));

        string? fatalError = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (moduleRefresh is not null)
                    await moduleRefresh.ApplyPendingAsync(runtime, ct).ConfigureAwait(false);

                var profile = getProfile();
                if (profile is null)
                {
                    await Task.Delay(200, ct).ConfigureAwait(false);
                    continue;
                }

                var captureRegions = _captureRegionsStore.Load(profile);
                var pauseMs = Math.Clamp(
                    profile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.FramePauseMs),
                    0,
                    10000);
                var frameDelay = TimeSpan.FromMilliseconds(pauseMs);

                var capSw = Stopwatch.StartNew();
                var frame = await runtime.CaptureSession.CaptureAsync(ct);
                capSw.Stop();

                if (frame is null)
                {
                    await Task.Delay(frameDelay, ct);
                    continue;
                }

                using (frame)
                {
                    using var prepared = _ocrFramePreparer.Prepare(frame, runtime.WindowHandle, captureRegions);

                    var ocrSw = Stopwatch.StartNew();
                    var ocr = await runtime.OcrSession.RecognizeAsync(prepared.Image, ct);
                    ocrSw.Stop();

                    captureTimes.Enqueue(capSw.Elapsed.TotalMilliseconds);
                    ocrTimes.Enqueue(ocrSw.Elapsed.TotalMilliseconds);
                    if (captureTimes.Count > windowSize) captureTimes.Dequeue();
                    if (ocrTimes.Count > windowSize) ocrTimes.Dequeue();

                    var avgCaptureMs = captureTimes.Average();
                    var avgOcrMs = ocrTimes.Average();

                    var layout = await runtime.LayoutSession.ProcessAsync(ocr, ct);
                    var batch = _translationBatchBuilder.Build(layout.Paragraphs);
                    var overlaySession = runtime.OverlaySession;
                    var useOverlayTranslate = overlaySession is not null;

                    IReadOnlyList<string> translatedTexts = Array.Empty<string>();
                    double avgTranslateMs = translatorTimes.Count > 0 ? translatorTimes.Average() : 0;

                    if (!useOverlayTranslate)
                    {
                        try
                        {
                            var trSw = Stopwatch.StartNew();
                            translatedTexts = batch.Texts.Count == 0
                                ? Array.Empty<string>()
                                : await runtime.TranslatorSession.TranslateAsync(batch.Texts, ct);
                            trSw.Stop();

                            translatorTimes.Enqueue(trSw.Elapsed.TotalMilliseconds);
                            if (translatorTimes.Count > windowSize) translatorTimes.Dequeue();
                            avgTranslateMs = translatorTimes.Average();
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            onStatus((FormatStatusError(ex), true));
                            await Task.Delay(1000, ct);
                            continue;
                        }
                    }

                    if (overlaySession is not null)
                    {
                        var view = _overlayFrameMapper.Map(
                            ocr, layout, batch, prepared.OriginX, prepared.OriginY);
                        await overlaySession.PresentAsync(
                            view.Items, view.DebugWords, view.DebugLines, ct);
                    }

                    var pairs = new List<(string Source, string Translation)>(batch.Texts.Count);
                    if (!useOverlayTranslate)
                    {
                        for (var i = 0; i < batch.Texts.Count; i++)
                        {
                            var translated = i < translatedTexts.Count ? translatedTexts[i] : batch.Texts[i];
                            pairs.Add((batch.Texts[i], translated));
                        }
                    }

                    var confLine = string.Format(
                        _localizationService.CurrentCulture,
                        _localizationService[LocalizationConstants.Text.AvgConfidence],
                        ocr.Confidence);
                    var textOut = useOverlayTranslate
                        ? confLine
                        : string.Join("\n\n", translatedTexts) + "\n" + confLine;

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (pairs.Count > 0)
                            onTranslatedPairs?.Invoke(pairs);
                        onTextUpdated(textOut);
                        onTimings?.Invoke(avgCaptureMs, avgOcrMs, avgTranslateMs);
                    });
                }

                await Task.Delay(frameDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                fatalError = _localizationService.FormatExceptionMessage(ex);
                break;
            }
        }

        if (fatalError is not null)
        {
            onStatus((_localizationService.FormatStoppedWithError(fatalError), true));
        }
        else
        {
            onStatus((_localizationService[LocalizationConstants.Status.Stopped], false));
        }
    }

    private string FormatStatusError(Exception ex)
        => string.Format(
            _localizationService.CurrentCulture,
            _localizationService[LocalizationConstants.Status.Error],
            _localizationService.FormatExceptionMessage(ex));
}
