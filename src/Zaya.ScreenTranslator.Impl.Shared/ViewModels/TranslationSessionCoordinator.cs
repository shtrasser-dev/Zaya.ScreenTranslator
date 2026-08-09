using Avalonia.Threading;
using Zaya.OCR.Services;
using Zaya.Primitives;
using Zaya.Screenshot.Models;
using Zaya.Screenshot.Services;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Layout.Impl.Services;
using Zaya.ScreenTranslator.Layout.Services;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

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

internal sealed class TranslationSessionCoordinator : ITranslationModuleRefresh
{
    private readonly IApplicationProfileService _profileService;
    private readonly TranslationLoopService _loopService;
    private readonly TranslationHistoryService _history;
    private readonly TextOutputPresenter _textOutput;
    private readonly ITranslationSessionHost _host;
    private readonly object _refreshLock = new();

    private Task? _loopTask;
    private IDisposable? _ocrEngine;
    private IDisposable? _captureEngine;
    private IDisposable? _textLayoutEngine;
    private IDisposable? _translatorEngine;
    private IDisposable? _translatorCacheEngine;
    private IOverlayLayoutService? _overlayLayoutEngine;
    private IOverlayLayoutSession? _overlaySession;
    private ITranslatorSession? _liveTranslatorSession;
    private TranslationModuleKind _pendingRefresh;
    private nint _sessionWindowHandle;

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

    public void RequestModuleRefresh(TranslationModuleKind modules)
    {
        if (modules == TranslationModuleKind.None)
            return;

        lock (_refreshLock)
            _pendingRefresh |= modules;
    }

    public void CancelLoop() => _ = StopLoopAsync();

    public async Task StopLoopAsync()
    {
        lock (_refreshLock)
            _pendingRefresh = TranslationModuleKind.None;

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

    public async Task ApplyPendingAsync(TranslationLoopRuntime runtime, CancellationToken cancellationToken)
    {
        TranslationModuleKind pending;
        lock (_refreshLock)
        {
            pending = _pendingRefresh;
            _pendingRefresh = TranslationModuleKind.None;
        }

        if (pending == TranslationModuleKind.None)
            return;

        // FullRestart is handled by MainViewModel (stop/start); ignore if somehow queued.
        pending &= ~TranslationModuleKind.FullRestart;
        if (pending == TranslationModuleKind.None)
            return;

        var profile = _profileService.ActiveProfile;
        if (profile is null)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        if (pending.HasFlag(TranslationModuleKind.Capture))
            await RecreateCaptureAsync(runtime, profile, cancellationToken).ConfigureAwait(false);

        if (pending.HasFlag(TranslationModuleKind.Ocr))
            await RecreateOcrAsync(runtime, profile, cancellationToken).ConfigureAwait(false);

        if (pending.HasFlag(TranslationModuleKind.TextLayout))
            await RecreateTextLayoutAsync(runtime, profile, cancellationToken).ConfigureAwait(false);

        if (pending.HasFlag(TranslationModuleKind.Translator))
            await RecreateTranslatorAsync(runtime, profile, cancellationToken).ConfigureAwait(false);

        if (pending.HasFlag(TranslationModuleKind.Overlay))
            await RecreateOverlayAsync(runtime, profile, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartSessionAsync(
        IApplicationProfile profile,
        nint handle,
        string statusKeyRunning)
    {
        lock (_refreshLock)
            _pendingRefresh = TranslationModuleKind.None;

        _sessionWindowHandle = handle;
        _host.SetStatus(_host.Loc[LocalizationConstants.Status.CreatingSessions], isError: false);

        IOCRService? ocr = null;
        ICaptureService? capture = null;
        ITextLayoutService? textLayout = null;
        ITranslatorService? translator = null;
        ITranslatorCacheService? translatorCache = null;
        IOverlayLayoutService? overlayLayout = null;
        IOverlayLayoutSession? overlaySession = null;
        ICaptureSession? captureSession = null;
        IOCRSession? ocrSession = null;
        ITextLayoutSession? layoutSession = null;
        ITranslatorSession? translatorSession = null;
        ICaptureRegion? captureRegion = null;

        try
        {
            ocr = EngineFactory.CreateOcr(
                profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
            capture = EngineFactory.CreateCapture(
                profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));

            if (ocr is null || capture is null)
            {
                AbortPendingStart(ocr, capture, null, null, null, null);
                _host.SetStatus(_host.Loc[LocalizationConstants.Status.EngineNotFound], isError: true);
                return;
            }

            textLayout = EngineFactory.CreateTextLayout(
                profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
            if (textLayout is null)
            {
                AbortPendingStart(ocr, capture, null, null, null, null);
                _host.SetStatus(_host.Loc[LocalizationConstants.Status.TextLayoutNotFound], isError: true);
                return;
            }

            translator = EngineFactory.CreateTranslator(
                profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
            if (translator is null)
            {
                AbortPendingStart(ocr, capture, textLayout, null, null, null);
                _host.SetStatus(_host.Loc[LocalizationConstants.Status.TranslatorNotFound], isError: true);
                return;
            }

            var translatorCacheId = ResolveTranslatorCacheId(profile);
            translatorCache = EngineFactory.CreateTranslatorCache(translatorCacheId);
            if (translatorCache is null
                && !string.Equals(translatorCacheId, NoTranslatorCacheService.EngineIdValue, StringComparison.OrdinalIgnoreCase))
            {
                translatorCache = new NoTranslatorCacheService();
                translatorCacheId = NoTranslatorCacheService.EngineIdValue;
            }

            if (translatorCache is null)
            {
                AbortPendingStart(ocr, capture, textLayout, translator, null, null);
                _host.SetStatus(_host.Loc[LocalizationConstants.Status.TranslatorCacheNotFound], isError: true);
                return;
            }

            profile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TranslatorCache]
                = translatorCacheId;

            if (_host.IsOverlayMode)
            {
                var created = await TryCreateOverlayAsync(profile, handle).ConfigureAwait(true);
                if (created is null)
                {
                    AbortPendingStart(ocr, capture, textLayout, translator, translatorCache, null);
                    return;
                }

                overlayLayout = created.Value.Service;
                overlaySession = created.Value.Session;
            }

            captureRegion = new FullScreenWindowRegion
            {
                WindowHandle = handle,
                CaptureClientArea = true,
            };
            var targetLanguage = _profileService.LoadScreenTranslatorProfile().TargetLanguage;
            var ctProbe = CancellationToken.None;

            captureSession = await CreateCaptureSessionAsync(capture, captureRegion, profile, ctProbe).ConfigureAwait(true);
            ocrSession = await CreateOcrSessionAsync(ocr, profile, ctProbe).ConfigureAwait(true);
            layoutSession = await CreateTextLayoutSessionAsync(textLayout, profile, ctProbe).ConfigureAwait(true);
            translatorSession = await CreateTranslatorSessionAsync(
                translator, translatorCache, profile, targetLanguage, ctProbe).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            captureSession?.Dispose();
            ocrSession?.Dispose();
            layoutSession?.Dispose();
            translatorSession?.Dispose();
            try { overlaySession?.Dispose(); } catch { /* ignore */ }
            AbortPendingStart(ocr, capture, textLayout, translator, translatorCache, overlayLayout);
            _host.SetStatus(LocalizationService.Instance.FormatStoppedWithError(ex), isError: true);
            return;
        }

        _ocrEngine = ocr;
        _captureEngine = capture;
        _textLayoutEngine = textLayout;
        _translatorEngine = translator;
        _translatorCacheEngine = translatorCache;
        _overlayLayoutEngine = overlayLayout;
        _overlaySession = overlaySession;
        _liveTranslatorSession = translatorSession;

        var runtime = new TranslationLoopRuntime
        {
            Capture = capture!,
            Ocr = ocr!,
            TextLayout = textLayout!,
            Translator = translator!,
            TranslatorCache = translatorCache!,
            Region = captureRegion!,
            CaptureSession = captureSession!,
            OcrSession = ocrSession!,
            LayoutSession = layoutSession!,
            TranslatorSession = translatorSession!,
            OverlaySession = overlaySession,
            WindowHandle = handle,
        };

        _host.LoopCts ??= new CancellationTokenSource();
        var ct = _host.LoopCts.Token;
        _host.IsRunning = true;
        _host.SetStatus(_host.Loc[LocalizationConstants.Status.Starting]);
        _host.SetTextOutputVisible(true);

        _loopTask = Task.Run(async () =>
        {
            await _loopService.RunAsync(
                runtime,
                () => _profileService.ActiveProfile,
                this,
                ct,
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
                pairs => _history.AddRange(pairs));
        });

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _host.SetStatus(LocalizationService.Instance.FormatStoppedWithError(ex), isError: true);
        }
        finally
        {
            DisposeEngines();
            _host.IsRunning = false;
            if (_host.LoopCts is not null)
            {
                _host.LoopCts.Dispose();
                _host.LoopCts = null;
            }
            _loopTask = null;
            await _host.ClearWindowSelectionIfProcessGoneAsync().ConfigureAwait(true);
        }
    }

    private async Task RecreateCaptureAsync(
        TranslationLoopRuntime runtime, IApplicationProfile profile, CancellationToken ct)
    {
        var engineId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture);
        if (string.IsNullOrWhiteSpace(engineId))
            engineId = SettingsConstants.EngineDefaults.Capture;
        var capture = EngineFactory.CreateCapture(engineId)
            ?? throw new InvalidOperationException("Capture engine not found.");
        var session = await CreateCaptureSessionAsync(capture, runtime.Region, profile, ct).ConfigureAwait(false);

        DisposeQuietly(runtime.CaptureSession);
        DisposeQuietly(_captureEngine);
        _captureEngine = capture;
        runtime.Capture = capture;
        runtime.CaptureSession = session;
    }

    private async Task RecreateOcrAsync(
        TranslationLoopRuntime runtime, IApplicationProfile profile, CancellationToken ct)
    {
        var engineId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr);
        if (string.IsNullOrWhiteSpace(engineId))
            engineId = SettingsConstants.EngineDefaults.Ocr;
        var ocr = EngineFactory.CreateOcr(engineId)
            ?? throw new InvalidOperationException("OCR engine not found.");
        var session = await CreateOcrSessionAsync(ocr, profile, ct).ConfigureAwait(false);

        DisposeQuietly(runtime.OcrSession);
        DisposeQuietly(_ocrEngine);
        _ocrEngine = ocr;
        runtime.Ocr = ocr;
        runtime.OcrSession = session;
    }

    private async Task RecreateTextLayoutAsync(
        TranslationLoopRuntime runtime, IApplicationProfile profile, CancellationToken ct)
    {
        var engineId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout);
        if (string.IsNullOrWhiteSpace(engineId))
            engineId = SettingsConstants.EngineDefaults.TextLayout;
        var textLayout = EngineFactory.CreateTextLayout(engineId)
            ?? throw new InvalidOperationException("Text layout engine not found.");
        var session = await CreateTextLayoutSessionAsync(textLayout, profile, ct).ConfigureAwait(false);

        DisposeQuietly(runtime.LayoutSession);
        DisposeQuietly(_textLayoutEngine);
        _textLayoutEngine = textLayout;
        runtime.TextLayout = textLayout;
        runtime.LayoutSession = session;
    }

    private async Task RecreateTranslatorAsync(
        TranslationLoopRuntime runtime, IApplicationProfile profile, CancellationToken ct)
    {
        var translatorId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator);
        if (string.IsNullOrWhiteSpace(translatorId))
            translatorId = SettingsConstants.EngineDefaults.Translator;
        var translator = EngineFactory.CreateTranslator(translatorId)
            ?? throw new InvalidOperationException("Translator engine not found.");

        var cacheId = ResolveTranslatorCacheId(profile);
        var translatorCache = EngineFactory.CreateTranslatorCache(cacheId);
        if (translatorCache is null
            && !string.Equals(cacheId, NoTranslatorCacheService.EngineIdValue, StringComparison.OrdinalIgnoreCase))
            translatorCache = new NoTranslatorCacheService();
        if (translatorCache is null)
        {
            translator.Dispose();
            throw new InvalidOperationException("Translator cache engine not found.");
        }

        profile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TranslatorCache]
            = cacheId;
        var targetLanguage = _profileService.LoadScreenTranslatorProfile().TargetLanguage;
        var session = await CreateTranslatorSessionAsync(
            translator, translatorCache, profile, targetLanguage, ct).ConfigureAwait(false);

        DisposeQuietly(runtime.TranslatorSession);
        DisposeQuietly(_translatorEngine);
        DisposeQuietly(_translatorCacheEngine);
        _translatorEngine = translator;
        _translatorCacheEngine = translatorCache;
        runtime.Translator = translator;
        runtime.TranslatorCache = translatorCache;
        runtime.TranslatorSession = session;
        _liveTranslatorSession = session;
    }

    private async Task RecreateOverlayAsync(
        TranslationLoopRuntime runtime, IApplicationProfile profile, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_host.IsOverlayMode)
        {
            var old = runtime.OverlaySession;
            runtime.OverlaySession = null;
            _overlaySession = null;
            DisposeQuietly(old);
            DisposeQuietly(_overlayLayoutEngine);
            _overlayLayoutEngine = null;
            return;
        }

        var created = await TryCreateOverlayAsync(profile, _sessionWindowHandle).ConfigureAwait(false);
        if (created is null)
            throw new InvalidOperationException("Overlay layout unavailable.");

        var oldSession = runtime.OverlaySession;
        var oldEngine = _overlayLayoutEngine;
        _overlayLayoutEngine = created.Value.Service;
        _overlaySession = created.Value.Session;
        runtime.OverlaySession = created.Value.Session;

        DisposeQuietly(oldSession);
        DisposeQuietly(oldEngine);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _host.SetTextOutputVisible(true);
            created.Value.Session.SetVisible(true);
        }).GetTask().ConfigureAwait(false);
    }

    private async Task<(IOverlayLayoutService Service, IOverlayLayoutSession Session)?> TryCreateOverlayAsync(
        IApplicationProfile profile,
        nint handle)
    {
        var overlayId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout);
        if (string.IsNullOrWhiteSpace(overlayId))
            overlayId = ScreenOverlayLayoutService.EngineIdValue;

        var overlayLayout = EngineFactory.CreateOverlayLayout(overlayId);
        if (overlayLayout is null || !overlayLayout.IsAvailable)
        {
            overlayLayout?.Dispose();
            _host.SetStatus(_host.Loc[LocalizationConstants.Status.OverlayUnavailable], isError: true);
            return null;
        }

        var overlaySettings = new Dictionary<string, object>(GetOrCreatePluginSettings(profile, overlayId));
        overlaySettings[ManagedSettingKeys.TargetWindowHandle] = handle.ToInt64();
        GetOrCreatePluginSettings(profile, overlayId).Remove(ManagedSettingKeys.TargetWindowHandle);

        try
        {
            var overlaySession = await overlayLayout
                .CreateSessionAsync(overlaySettings, CreateOverlayTranslateCallback())
                .ConfigureAwait(false);
            return (overlayLayout, overlaySession);
        }
        catch (Exception ex)
        {
            overlayLayout.Dispose();
            _host.SetStatus(string.Format(
                _host.Loc[LocalizationConstants.Status.OverlayFailed],
                LocalizationService.Instance.FormatExceptionMessage(ex)), isError: true);
            return null;
        }
    }

    private OverlayTranslateCallback CreateOverlayTranslateCallback()
        => async (texts, token) =>
        {
            try
            {
                var session = _liveTranslatorSession
                    ?? throw new InvalidOperationException("Translator session is not ready.");
                var translated = await session.TranslateAsync(texts, token).ConfigureAwait(false);
                var pairs = new List<(string Source, string Translation)>(texts.Count);
                for (var i = 0; i < texts.Count; i++)
                {
                    var t = i < translated.Count ? translated[i] : texts[i];
                    pairs.Add((texts[i], t));
                }

                if (pairs.Count > 0)
                    _history.AddRange(pairs);
                return translated;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Transient translator failures must not stop the capture loop
                // (translate now runs inside overlay PresentAsync).
                var msg = string.Format(
                    LocalizationService.Instance.CurrentCulture,
                    LocalizationService.Instance[LocalizationConstants.Status.Error],
                    LocalizationService.Instance.FormatExceptionMessage(ex));
                Dispatcher.UIThread.Post(() => _host.SetStatus(msg, isError: true));
                await Task.Delay(1000, token).ConfigureAwait(false);
                return texts;
            }
        };

    private static string ResolveTranslatorCacheId(IApplicationProfile profile)
    {
        var translatorCacheId = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache);
        if (string.IsNullOrWhiteSpace(translatorCacheId)
            || string.Equals(translatorCacheId, "none", StringComparison.OrdinalIgnoreCase))
            translatorCacheId = SettingsConstants.EngineDefaults.TranslatorCache;
        return translatorCacheId;
    }

    private static async Task<ICaptureSession> CreateCaptureSessionAsync(
        ICaptureService capture,
        ICaptureRegion region,
        IApplicationProfile profile,
        CancellationToken ct)
    {
        var settings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
        return await capture.CreateSessionAsync(region,
            ManagedSettingKeys.PrepareForEngine(capture.EngineId, capture.Settings, settings), ct);
    }

    private static async Task<IOCRSession> CreateOcrSessionAsync(
        IOCRService ocr,
        IApplicationProfile profile,
        CancellationToken ct)
    {
        var settings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        return await ocr.CreateSessionAsync(
            ManagedSettingKeys.PrepareForEngine(ocr.EngineId, ocr.Settings, settings), ct);
    }

    private static async Task<ITextLayoutSession> CreateTextLayoutSessionAsync(
        ITextLayoutService textLayout,
        IApplicationProfile profile,
        CancellationToken ct)
    {
        var settings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        return await textLayout.CreateSessionAsync(
            ManagedSettingKeys.PrepareForEngine(textLayout.EngineId, textLayout.Settings, settings), ct);
    }

    private static async Task<ITranslatorSession> CreateTranslatorSessionAsync(
        ITranslatorService translator,
        ITranslatorCacheService translatorCache,
        IApplicationProfile profile,
        string? targetLanguage,
        CancellationToken ct)
    {
        var trSettings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
        var cacheSettings = GetOrCreatePluginSettings(profile,
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache));

        var raw = await translator.CreateSessionAsync(
            ManagedSettingKeys.PrepareForEngine(translator.EngineId, translator.Settings, trSettings, targetLanguage), ct);
        return await translatorCache.WrapSessionAsync(
            raw,
            ManagedSettingKeys.PrepareForEngine(translatorCache.EngineId, translatorCache.Settings, cacheSettings), ct);
    }

    private void AbortPendingStart(
        IDisposable? ocr,
        IDisposable? capture,
        IDisposable? textLayout,
        IDisposable? translator,
        IDisposable? translatorCache,
        IDisposable? overlayLayout)
    {
        DisposeQuietly(ocr);
        DisposeQuietly(capture);
        DisposeQuietly(textLayout);
        DisposeQuietly(translator);
        DisposeQuietly(translatorCache);
        DisposeQuietly(overlayLayout);
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
        _liveTranslatorSession = null;

        DisposeQuietly(_ocrEngine);
        DisposeQuietly(_captureEngine);
        DisposeQuietly(_textLayoutEngine);
        DisposeQuietly(_translatorEngine);
        DisposeQuietly(_translatorCacheEngine);
        DisposeQuietly(_overlayLayoutEngine);
        _ocrEngine = null;
        _captureEngine = null;
        _textLayoutEngine = null;
        _translatorEngine = null;
        _translatorCacheEngine = null;
        _overlayLayoutEngine = null;
    }

    private static void DisposeQuietly(IDisposable? disposable)
    {
        try { disposable?.Dispose(); } catch { /* ignore */ }
    }

    private static Dictionary<string, object> GetOrCreatePluginSettings(IApplicationProfile profile, string pluginId)
    {
        if (!profile.Settings.TryGetValue(pluginId, out var settings))
            profile.Settings[pluginId] = settings = new();
        return settings;
    }
}
