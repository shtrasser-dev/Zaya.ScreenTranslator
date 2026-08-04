using Avalonia.Threading;
using Zaya.Primitives;
using Zaya.Screenshot.Models;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Layout.Impl.Services;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal interface ITranslationSessionHost : IStatusHost
{
    bool IsRunning { get; set; }
    bool IsOverlayMode { get; }
    string TimingInfo { get; set; }
    CancellationTokenSource? LoopCts { get; set; }
    void SetLocalizedStatus(string resourceKey, string statusKey);
    void SetTextOutputVisible(bool visible);
    Task ClearWindowSelectionIfProcessGoneAsync();
}

internal sealed class TranslationSessionCoordinator
{
    private readonly IApplicationProfileService _profileService;
    private readonly TranslationLoopService _loopService;
    private readonly TranslationHistoryService _history;
    private readonly TextOutputPresenter _textOutput;
    private readonly ITranslationSessionHost _host;

    private Task? _loopTask;
    private IDisposable? _ocrEngine;
    private IDisposable? _captureEngine;
    private IDisposable? _textLayoutEngine;
    private IDisposable? _translatorEngine;
    private IDisposable? _translatorCacheEngine;
    private IOverlayLayoutService? _overlayLayoutEngine;
    private IOverlayLayoutSession? _overlaySession;

    public TranslationSessionCoordinator(
        IApplicationProfileService profileService,
        TranslationLoopService loopService,
        TranslationHistoryService history,
        TextOutputPresenter textOutput,
        ITranslationSessionHost host)
    {
        _profileService = profileService;
        _loopService = loopService;
        _history = history;
        _textOutput = textOutput;
        _host = host;
    }

    public IOverlayLayoutSession? OverlaySession => _overlaySession;

    public void CancelLoop() => _ = StopLoopAsync();

    public async Task StopLoopAsync()
    {
        var cts = _host.LoopCts;
        if (cts is not null && !cts.IsCancellationRequested)
            cts.Cancel();

        var loopTask = _loopTask;
        if (loopTask is not null)
        {
            try { await loopTask.ConfigureAwait(true); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        DisposeEngines();
        _textOutput.CloseTextWindow();
    }

    public async Task StartSessionAsync(
        IApplicationProfile profile,
        nint handle,
        string statusKeyRunning)
    {
        var ocr = EngineFactory.CreateOcr(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        var capture = EngineFactory.CreateCapture(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));

        if (ocr is null || capture is null)
        {
            ocr?.Dispose();
            capture?.Dispose();
            AbortPendingStart();
            _host.SetStatus(_host.Loc[LocalizationConstants.Status.EngineNotFound], isError: true);
            return;
        }

        var textLayout = EngineFactory.CreateTextLayout(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        if (textLayout is null)
        {
            ocr.Dispose();
            capture.Dispose();
            AbortPendingStart();
            _host.SetStatus(_host.Loc[LocalizationConstants.Status.TextLayoutNotFound], isError: true);
            return;
        }

        var translator = EngineFactory.CreateTranslator(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
        if (translator is null)
        {
            ocr.Dispose();
            capture.Dispose();
            textLayout.Dispose();
            AbortPendingStart();
            _host.SetStatus(_host.Loc[LocalizationConstants.Status.TranslatorNotFound], isError: true);
            return;
        }

        var translatorCacheId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache);
        if (string.IsNullOrWhiteSpace(translatorCacheId)
            || string.Equals(translatorCacheId, "none", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy profiles used translator "none" id for cache-off; prefer Memory by default.
            translatorCacheId = SettingsConstants.EngineDefaults.TranslatorCache;
        }

        // Keep the chosen engine id persisted so settings UI and runtime stay aligned.
        profile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TranslatorCache]
            = translatorCacheId;

        var translatorCache = EngineFactory.CreateTranslatorCache(translatorCacheId);
        if (translatorCache is null
            && !string.Equals(translatorCacheId, NoTranslatorCacheService.EngineIdValue, StringComparison.OrdinalIgnoreCase))
        {
            // Memory plugin missing → fall back to no-cache rather than aborting the session.
            translatorCache = new NoTranslatorCacheService();
            translatorCacheId = NoTranslatorCacheService.EngineIdValue;
        }
        if (translatorCache is null)
        {
            ocr.Dispose();
            capture.Dispose();
            textLayout.Dispose();
            translator.Dispose();
            AbortPendingStart();
            _host.SetStatus(_host.Loc[LocalizationConstants.Status.TranslatorCacheNotFound], isError: true);
            return;
        }

        IOverlayLayoutService? overlayLayout = null;
        IOverlayLayoutSession? overlaySession = null;
        if (_host.IsOverlayMode)
        {
            var overlayId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout);
            if (string.IsNullOrWhiteSpace(overlayId))
                overlayId = ScreenOverlayLayoutService.EngineIdValue;

            overlayLayout = EngineFactory.CreateOverlayLayout(overlayId);
            if (overlayLayout is null || !overlayLayout.IsAvailable)
            {
                ocr.Dispose();
                capture.Dispose();
                textLayout.Dispose();
                translator.Dispose();
                translatorCache.Dispose();
                overlayLayout?.Dispose();
                AbortPendingStart();
                _host.SetStatus(_host.Loc[LocalizationConstants.Status.OverlayUnavailable], isError: true);
                return;
            }

            var overlaySettings = new Dictionary<string, object>(GetOrCreatePluginSettings(profile, overlayId));
            overlaySettings[ManagedSettingKeys.TargetWindowHandle] = handle.ToInt64();
            GetOrCreatePluginSettings(profile, overlayId).Remove(ManagedSettingKeys.TargetWindowHandle);

            try
            {
                overlaySession = await overlayLayout.CreateSessionAsync(overlaySettings);
                _host.SetTextOutputVisible(true);
                overlaySession.SetVisible(true);
            }
            catch (Exception ex)
            {
                ocr.Dispose();
                capture.Dispose();
                textLayout.Dispose();
                translator.Dispose();
                translatorCache.Dispose();
                overlayLayout.Dispose();
                AbortPendingStart();
                _host.SetStatus(string.Format(
                    _host.Loc[LocalizationConstants.Status.OverlayFailed],
                    LocalizationService.Instance.FormatExceptionMessage(ex)), isError: true);
                return;
            }
        }

        _ocrEngine = ocr;
        _captureEngine = capture;
        _textLayoutEngine = textLayout;
        _translatorEngine = translator;
        _translatorCacheEngine = translatorCache;
        _overlayLayoutEngine = overlayLayout;
        _overlaySession = overlaySession;

        var region = new FullScreenWindowRegion { WindowHandle = handle };
        var targetLanguage = _profileService.LoadScreenTranslatorProfile().TargetLanguage;

        _host.LoopCts ??= new CancellationTokenSource();
        var ct = _host.LoopCts.Token;
        _host.IsRunning = true;
        _host.SetStatus(_host.Loc[LocalizationConstants.Status.Starting]);
        _host.SetTextOutputVisible(true);

        _loopTask = Task.Run(async () =>
        {
            await _loopService.RunAsync(
                ocr, capture, textLayout, translator, translatorCache, region, profile, ct,
                text => _textOutput.UpdateText(text),
                status => Dispatcher.UIThread.Post(() =>
                {
                    if (string.Equals(status.Text, AppConstants.LoopStatus.Running, StringComparison.Ordinal))
                        _host.SetLocalizedStatus(LocalizationConstants.Status.Running, statusKeyRunning);
                    else
                        _host.SetStatus(status.Text, isError: status.IsError);
                }),
                (capMs, ocrMs, trMs) => Dispatcher.UIThread.Post(() =>
                    _host.TimingInfo = string.Format(
                        LocalizationService.Instance.CurrentCulture,
                        _host.Loc[LocalizationConstants.Timing.Format],
                        capMs, ocrMs, trMs)),
                targetLanguage,
                overlaySession,
                pairs => _history.AddRange(pairs));
        });

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // Safety net if RunAsync fails before posting status (should be rare).
            _host.SetStatus(LocalizationService.Instance.FormatStoppedWithError(ex), isError: true);
        }
        finally
        {
            DisposeEngines();
            _host.IsRunning = false;
            if (_host.LoopCts is not null)
            {
                // Status is set by the loop (Stopped / Stopped + Error) or by the caller on cancel.
                _host.LoopCts.Dispose();
                _host.LoopCts = null;
            }
            _loopTask = null;
            await _host.ClearWindowSelectionIfProcessGoneAsync().ConfigureAwait(true);
        }
    }

    private void AbortPendingStart()
    {
        _host.IsRunning = false;
        if (_host.LoopCts is null)
            return;

        _host.LoopCts.Dispose();
        _host.LoopCts = null;
    }

    private void DisposeEngines()
    {
        try { _overlaySession?.Dispose(); } catch { }
        _overlaySession = null;

        _ocrEngine?.Dispose();
        _captureEngine?.Dispose();
        _textLayoutEngine?.Dispose();
        _translatorEngine?.Dispose();
        _translatorCacheEngine?.Dispose();
        _overlayLayoutEngine?.Dispose();
        _ocrEngine = null;
        _captureEngine = null;
        _textLayoutEngine = null;
        _translatorEngine = null;
        _translatorCacheEngine = null;
        _overlayLayoutEngine = null;
    }

    private static Dictionary<string, object> GetOrCreatePluginSettings(IApplicationProfile profile, string pluginId)
    {
        if (!profile.Settings.TryGetValue(pluginId, out var settings))
            profile.Settings[pluginId] = settings = new();
        return settings;
    }
}
