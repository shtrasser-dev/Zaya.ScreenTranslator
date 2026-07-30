using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using Zaya.Screenshot.Models;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Layout.Services;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IApplicationProfileService _profileService;
    private readonly IScreenTranslatorContext _context;
    private readonly ISettingsService _settingsService;
    private readonly TranslationLoopService _loopService;
    private readonly PluginUpdateService _pluginUpdateService;
    private readonly HostVersionChecker _hostVersionChecker;

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private Window? _textWindow;
    private IDisposable? _ocrEngine;
    private IDisposable? _captureEngine;
    private IDisposable? _textLayoutEngine;
    private IDisposable? _translatorEngine;
    private IOverlayLayoutService? _overlayLayoutEngine;
    private IOverlayLayoutSession? _overlaySession;

    public MainViewModel(
        IApplicationProfileService profileService,
        IScreenTranslatorContext context,
        ISettingsService settingsService,
        TranslationLoopService loopService,
        PluginUpdateService pluginUpdateService,
        HostVersionChecker hostVersionChecker)
    {
        _profileService = profileService;
        _context = context;
        _settingsService = settingsService;
        _loopService = loopService;
        _pluginUpdateService = pluginUpdateService;
        _hostVersionChecker = hostVersionChecker;

        _profileNames = profileService.ListProfileNames();
        _selectedProfileName = profileService.ActiveProfile?.ScreenTranslatorSettings
            .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName)
                               ?? _profileNames.FirstOrDefault();
        _windows = [];

        var screen = profileService.LoadScreenTranslatorProfile();
        _selectedDisplayMode = screen.DisplayMode is "overlay" or "textWindow"
            ? screen.DisplayMode
            : "textWindow";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopButtonText))]
    private bool _isRunning;

    public string StartStopButtonText => IsRunning ? Loc["Btn_Stop"] : Loc["Btn_Start"];

    [ObservableProperty]
    private string _statusText = "Idle";

    [ObservableProperty]
    private IReadOnlyList<WindowInfo> _windows;

    [ObservableProperty]
    private WindowInfo? _selectedWindow;

    [ObservableProperty]
    private IReadOnlyList<string> _profileNames;

    [ObservableProperty]
    private string? _selectedProfileName;

    partial void OnSelectedProfileNameChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _profileService.SetActiveProfile(value);
    }

    [ObservableProperty]
    private bool _isTextWindowVisible;

    [ObservableProperty]
    private string _timingInfo = string.Empty;

    public IReadOnlyList<string> DisplayModeOptions { get; } =
    [
        ScreenTranslatorSettingDescriptors.DisplayModeTextWindow,
        ScreenTranslatorSettingDescriptors.DisplayModeOverlay,
    ];

    [ObservableProperty]
    private string _selectedDisplayMode = ScreenTranslatorSettingDescriptors.DisplayModeTextWindow;

    partial void OnSelectedDisplayModeChanged(string value)
    {
        var screen = _profileService.LoadScreenTranslatorProfile();
        screen.DisplayMode = value is "overlay" or "textWindow" ? value : "textWindow";
        _profileService.SaveScreenTranslatorProfile(screen);

        if (IsOverlayMode)
        {
            if (_textWindow is not null)
            {
                _textWindow.Hide();
                IsTextWindowVisible = false;
            }

            if (_overlaySession is not null)
            {
                IsTextWindowVisible = true;
                _overlaySession.SetVisible(true);
            }
        }
        else
        {
            _overlaySession?.SetVisible(false);
        }
    }

    private bool IsOverlayMode =>
        string.Equals(SelectedDisplayMode, ScreenTranslatorSettingDescriptors.DisplayModeOverlay, StringComparison.OrdinalIgnoreCase);

    public LocalizationService Loc => LocalizationService.Instance;
    public IApplicationProfileService ProfileService => _profileService;
    public IScreenTranslatorContext Context => _context;

    public void CancelLoop()
    {
        _ = StopLoopAsync();
    }

    public async Task StopLoopAsync()
    {
        var cts = _loopCts;
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
        CloseTextWindow();
    }

    private void DisposeEngines()
    {
        try { _overlaySession?.Dispose(); } catch { }
        _overlaySession = null;

        _ocrEngine?.Dispose();
        _captureEngine?.Dispose();
        _textLayoutEngine?.Dispose();
        _translatorEngine?.Dispose();
        _overlayLayoutEngine?.Dispose();
        _ocrEngine = null;
        _captureEngine = null;
        _textLayoutEngine = null;
        _translatorEngine = null;
        _overlayLayoutEngine = null;
    }

    private void CloseTextWindow()
    {
        if (_textWindow is Views.TextWindow tw)
        {
            tw.ForceClose();
            _textWindow = null;
            IsTextWindowVisible = false;
        }
        else if (_textWindow is not null)
        {
            _textWindow.Close();
            _textWindow = null;
            IsTextWindowVisible = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task StartStop()
    {
        if (IsRunning)
        {
            StatusText = "Stopping...";
            await StopLoopAsync();
            StatusText = "Stopped";
            return;
        }

        var profile = _profileService.ActiveProfile;
        if (profile is null)
        {
            StatusText = "No active profile";
            return;
        }

        nint handle = 0;
        var targetProcess = profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TargetProcess);
        if (!string.IsNullOrEmpty(targetProcess))
        {
            var candidates = Process.GetProcessesByName(targetProcess)
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .ToList();

            if (candidates.Count == 1)
            {
                handle = candidates[0].MainWindowHandle;
                _context.ActiveWindowHandle = handle;
                _context.ActiveWindowTitle = candidates[0].MainWindowTitle;
            }
        }

        if (handle == 0 && SelectedWindow is not null)
        {
            handle = SelectedWindow.Handle;
            _context.ActiveWindowHandle = handle;
            _context.ActiveWindowTitle = SelectedWindow.Title;
        }

        if (handle == 0)
        {
            StatusText = "Select a target window";
            return;
        }

        var ocr = EngineFactory.CreateOcr(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        var capture = EngineFactory.CreateCapture(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));

        if (ocr is null || capture is null)
        {
            ocr?.Dispose();
            capture?.Dispose();
            StatusText = "Engine not found";
            return;
        }

        var textLayout = EngineFactory.CreateTextLayout(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        if (textLayout is null)
        {
            ocr.Dispose();
            capture.Dispose();
            StatusText = "TextLayout engine not found";
            return;
        }

        var translator = EngineFactory.CreateTranslator(
            profile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
        if (translator is null)
        {
            ocr.Dispose();
            capture.Dispose();
            textLayout.Dispose();
            StatusText = "Translator engine not found";
            return;
        }

        IOverlayLayoutService? overlayLayout = null;
        IOverlayLayoutSession? overlaySession = null;
        if (IsOverlayMode)
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
                overlayLayout?.Dispose();
                StatusText = "Overlay layout engine not available";
                return;
            }

            // Clone — never write host-only HWND into the persisted profile dictionary.
            var overlaySettings = new Dictionary<string, object>(GetOrCreatePluginSettings(profile, overlayId));
            overlaySettings[ManagedSettingKeys.TargetWindowHandle] = handle.ToInt64();
            GetOrCreatePluginSettings(profile, overlayId).Remove(ManagedSettingKeys.TargetWindowHandle);

            try
            {
                overlaySession = await overlayLayout.CreateSessionAsync(overlaySettings);
                IsTextWindowVisible = true;
                overlaySession.SetVisible(true);
            }
            catch (Exception ex)
            {
                ocr.Dispose();
                capture.Dispose();
                textLayout.Dispose();
                translator.Dispose();
                overlayLayout.Dispose();
                StatusText = $"Overlay failed: {ex.Message}";
                return;
            }
        }

        _ocrEngine = ocr;
        _captureEngine = capture;
        _textLayoutEngine = textLayout;
        _translatorEngine = translator;
        _overlayLayoutEngine = overlayLayout;
        _overlaySession = overlaySession;

        var region = new FullScreenWindowRegion { WindowHandle = handle };
        var targetLanguage = _profileService.LoadScreenTranslatorProfile().TargetLanguage;

        _loopCts = new CancellationTokenSource();
        var ct = _loopCts.Token;
        IsRunning = true;
        StatusText = "Starting...";

        _loopTask = Task.Run(async () =>
        {
            await _loopService.RunAsync(
                ocr, capture, textLayout, translator, region, profile, ct,
                text => UpdateText(text),
                status => Dispatcher.UIThread.Post(() => StatusText = status),
                (capMs, ocrMs, trMs) => Dispatcher.UIThread.Post(() =>
                    TimingInfo = $"Capture: {capMs:F0}ms | OCR: {ocrMs:F0}ms | Translate: {trMs:F0}ms"),
                targetLanguage,
                overlaySession);
        });

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException) { }
        finally
        {
            DisposeEngines();
            IsRunning = false;
            if (_loopCts is not null)
            {
                if (!_loopCts.IsCancellationRequested)
                    StatusText = "Stopped";
                _loopCts.Dispose();
                _loopCts = null;
            }
            _loopTask = null;
        }
    }

    [RelayCommand]
    private void ShowHideText()
    {
        IsTextWindowVisible = !IsTextWindowVisible;

        if (IsOverlayMode)
        {
            if (_overlaySession is not null)
                _overlaySession.SetVisible(IsTextWindowVisible);
            else if (IsTextWindowVisible)
                StatusText = Loc["Overlay_NeedStart"];
            return;
        }

        if (_textWindow is null)
        {
            var screenProfile = _profileService.LoadScreenTranslatorProfile();
            var vm = new TextWindowViewModel
            {
                IsTopmost = screenProfile.TextWindow.Topmost
            };
            _textWindow = new Views.TextWindow
            {
                DataContext = vm,
                Width = screenProfile.TextWindow.Width > 0 ? screenProfile.TextWindow.Width : 480,
                Height = screenProfile.TextWindow.Height > 0 ? screenProfile.TextWindow.Height : 320,
            };
            if (screenProfile.TextWindow.X != 0 || screenProfile.TextWindow.Y != 0)
                _textWindow.Position = new PixelPoint(screenProfile.TextWindow.X, screenProfile.TextWindow.Y);
        }

        if (IsTextWindowVisible)
            _textWindow.Show();
        else
            _textWindow.Hide();
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        var lifetime = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        if (owner is null)
            return;

        var vm = new SettingsViewModel(
            _settingsService,
            _profileService,
            Loc,
            _pluginUpdateService,
            _hostVersionChecker);
        var window = new Views.SettingsWindow(vm);
        await window.ShowDialog(owner);

        ProfileNames = _profileService.ListProfileNames();
        SelectedProfileName = _profileService.ActiveProfile?.ScreenTranslatorSettings
            .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName)
            ?? ProfileNames.FirstOrDefault();

        var screen = _profileService.LoadScreenTranslatorProfile();
        SelectedDisplayMode = screen.DisplayMode is "overlay" or "textWindow"
            ? screen.DisplayMode
            : "textWindow";

        var dc = owner.DataContext;
        owner.DataContext = null;
        owner.DataContext = dc;
    }

    [RelayCommand]
    private void RefreshWindows()
    {
        var list = Process.GetProcesses()
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
            .Select(p => new WindowInfo
            {
                Handle = p.MainWindowHandle,
                Title = p.MainWindowTitle,
                ProcessName = p.ProcessName
            })
            .OrderBy(w => w.Title)
            .ToList();

        Windows = list;
    }

    [RelayCommand]
    private void SwitchProfile(string? name)
    {
        if (name is not null)
        {
            _profileService.SetActiveProfile(name);
            SelectedProfileName = name;
        }
    }

    public void CaptureTextWindowSettings(WindowSettings settings)
    {
        if (_textWindow is null)
            return;

        settings.X = _textWindow.Position.X;
        settings.Y = _textWindow.Position.Y;
        settings.Width = (int)_textWindow.Width;
        settings.Height = (int)_textWindow.Height;
        if (_textWindow.DataContext is TextWindowViewModel vm)
            settings.Topmost = vm.IsTopmost;
    }

    private void UpdateText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_textWindow?.DataContext is TextWindowViewModel vm)
                vm.RecognizedText = text;
        });
    }

    private static Dictionary<string, object> GetOrCreatePluginSettings(IApplicationProfile profile, string pluginId)
    {
        if (!profile.Settings.TryGetValue(pluginId, out var settings))
            profile.Settings[pluginId] = settings = new();
        return settings;
    }
}
