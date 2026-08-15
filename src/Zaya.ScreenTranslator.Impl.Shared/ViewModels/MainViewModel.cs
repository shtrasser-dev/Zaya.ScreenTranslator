using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.Views;
using Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class MainViewModel :
    ObservableObject,
    ICaptureHostState,
    IProfilePickerHost,
    ITextOutputHost,
    ITranslationSessionHost
{
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly IScreenTranslatorContext _screenTranslatorContext;
    private readonly ISettingsService _settingsService;
    private readonly IPluginUpdateService _pluginUpdateService;
    private readonly IHostVersionChecker _hostVersionChecker;
    private readonly ICaptureRegionsStore _captureRegionsStore;
    private readonly ICaptureRegionsSnapshotService _captureRegionsSnapshotService;
    private readonly IProcessIconLoader _processIconLoader;

    private readonly CaptureWindowResolver _captureResolver;
    private readonly TranslationSessionCoordinator _sessionCoordinator;
    private readonly ProfilePickerService _profilePicker;
    private readonly TextOutputPresenter _textOutput;
    private readonly ILocalizationService _localizationService;
    private readonly IConfigurationPathService _configurationPathService;

    private WindowInfo? _lastSelectedWindow;
    private string? _committedProfileName;
    private bool _suppressProfileChange;
    private string? _statusKey = AppConstants.StatusState.Idle;
    private bool _isRefreshingUiCulture;
    private int _settingsRestartToken;
    private CancellationTokenSource? _settingsRestartDebounceCts;
    private TranslationModuleKind _pendingModuleRefresh;

    public MainViewModel(
        IApplicationProfileService applicationProfileService,
        IScreenTranslatorContext screenTranslatorContext,
        ISettingsService settingsService,
        ITranslationLoopService translationLoopService,
        IPluginUpdateService pluginUpdateService,
        IHostVersionChecker hostVersionChecker,
        ICaptureRegionsStore captureRegionsStore,
        ICaptureRegionsSnapshotService captureRegionsSnapshotService,
        IProcessIconLoader processIconLoader,
        IEngineFactory engineFactory,
        ITranslationHistoryService translationHistoryService,
        ILocalizationService localizationService,
        IConfigurationPathService configurationPathService)
    {
        _applicationProfileService = applicationProfileService;
        _screenTranslatorContext = screenTranslatorContext;
        _settingsService = settingsService;
        _pluginUpdateService = pluginUpdateService;
        _hostVersionChecker = hostVersionChecker;
        _captureRegionsStore = captureRegionsStore;
        _captureRegionsSnapshotService = captureRegionsSnapshotService;
        _processIconLoader = processIconLoader;
        _localizationService = localizationService;
        _configurationPathService = configurationPathService;
        Loc = new LocalizedStrings(localizationService);

        TranslationSessionCoordinator? session = null;
        _textOutput = new TextOutputPresenter(
            applicationProfileService, translationHistoryService, localizationService, this, () => session!.OverlaySession);
        _sessionCoordinator = new TranslationSessionCoordinator(
            applicationProfileService, translationLoopService, translationHistoryService, engineFactory, localizationService, configurationPathService, _textOutput, this);
        session = _sessionCoordinator;
        _captureResolver = new CaptureWindowResolver(this, processIconLoader);
        _profilePicker = new ProfilePickerService(applicationProfileService, settingsService, this);

        _profileNames = SortedProfileNames(applicationProfileService.ListProfileNames());
        _selectedProfileName = applicationProfileService.ActiveProfile?.ScreenTranslatorSettings
            .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName)
                               ?? _profileNames.FirstOrDefault();
        _committedProfileName = _selectedProfileName;
        _windows = [];

        var screen = applicationProfileService.LoadScreenTranslatorProfile();
        var displayModeId = screen.DisplayMode is AppConstants.DisplayMode.Overlay or AppConstants.DisplayMode.TextWindow
            ? screen.DisplayMode
            : AppConstants.DisplayMode.Overlay;
        RebuildDisplayModeOptions(displayModeId);

        _targetLanguages = BuildTargetLanguages();
        _selectedTargetLanguage = _targetLanguages.FirstOrDefault(
            l => string.Equals(l.Code, screen.TargetLanguage ?? "en", StringComparison.OrdinalIgnoreCase))
            ?? _targetLanguages.FirstOrDefault();

        _statusText = Loc[LocalizationConstants.Status.Idle];
        _statusKey = AppConstants.StatusState.Idle;
        RefreshCaptureRegionsIndicator();

        if (Application.Current is { } app)
            app.ActualThemeVariantChanged += OnApplicationThemeVariantChanged;
    }

    public sealed record TargetLanguageItem(string Code, string Name);

    private IReadOnlyList<TargetLanguageItem> BuildTargetLanguages() =>
        Languages.All
            .Select(o => new TargetLanguageItem(o.Value, o.DisplayName.GetValue(_localizationService.CurrentCulture)))
            .ToList();

    [ObservableProperty] private IReadOnlyList<TargetLanguageItem> _targetLanguages = [];
    [ObservableProperty] private TargetLanguageItem? _selectedTargetLanguage;

    partial void OnSelectedTargetLanguageChanged(TargetLanguageItem? value)
    {
        if (value is null) return;
        var screen = _applicationProfileService.LoadScreenTranslatorProfile();
        if (string.Equals(screen.TargetLanguage, value.Code, StringComparison.OrdinalIgnoreCase))
            return;
        screen.TargetLanguage = value.Code;
        _applicationProfileService.SaveScreenTranslatorProfile(screen);
        ScheduleModulesRefreshIfRunning(TranslationModuleKind.Translator);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopButtonText))]
    [NotifyPropertyChangedFor(nameof(IsStartButton))]
    private bool _isRunning;

    public string StartStopButtonText => IsRunning ? Loc[LocalizationConstants.Buttons.Stop] : Loc[LocalizationConstants.Buttons.Start];
    public bool IsStartButton => !IsRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLine))]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWindowError))]
    private string _windowErrorMessage = string.Empty;

    public string StatusLabelText => $"{Loc[LocalizationConstants.Status.Label]}: ";
    public string StatusLine => $"{StatusLabelText}{StatusText}";
    public bool HasWindowError => !string.IsNullOrWhiteSpace(WindowErrorMessage);

    void IStatusHost.SetStatus(string text, string? key, bool isError) => SetStatus(text, key, isError);
    void IStatusHost.SetWindowError(string? message) => SetWindowError(message);

    internal void SetStatus(string text, string? key = null, bool isError = false)
    {
        _statusKey = key;
        IsStatusError = isError;
        StatusText = text;
    }

    internal void SetWindowError(string? message)
    {
        WindowErrorMessage = message?.Trim() ?? string.Empty;
    }

    void ITranslationSessionHost.SetLocalizedStatus(string resourceKey, string statusKey)
        => SetStatus(Loc[resourceKey], statusKey);

    [ObservableProperty] private IReadOnlyList<WindowInfo> _windows;
    [ObservableProperty] private WindowInfo? _selectedWindow;

    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        if (value is { IsLoadingPlaceholder: true })
        {
            if (!ReferenceEquals(SelectedWindow, _lastSelectedWindow))
                SelectedWindow = _lastSelectedWindow;
            return;
        }
        if (value is null && Windows.Count == 1 && Windows[0].IsLoadingPlaceholder)
            return;
        _lastSelectedWindow = value;
        if (value is { IsLoadingPlaceholder: false })
            SetWindowError(null);
        SetCurrentProcessCommand.NotifyCanExecuteChanged();
    }

    private bool CanSetCurrentProcess() =>
        SelectedWindow is { IsLoadingPlaceholder: false }
        && !string.IsNullOrWhiteSpace(SelectedWindow.ProcessName);

    [RelayCommand(CanExecute = nameof(CanSetCurrentProcess))]
    private void SetCurrentProcess()
    {
        if (Settings is null || SelectedWindow is not { IsLoadingPlaceholder: false } window)
            return;
        if (string.IsNullOrWhiteSpace(window.ProcessName)) return;
        Settings.TargetProcess = window.ProcessName;
    }

    [ObservableProperty] private IReadOnlyList<string> _profileNames;
    [ObservableProperty] private string? _selectedProfileName;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasProfileError))] private string _profileErrorMessage = string.Empty;

    public bool HasProfileError => !string.IsNullOrEmpty(ProfileErrorMessage);
    public string CreateNewProfileLabel => Loc[LocalizationConstants.Profile.CreateNew];
    public string CopyCurrentProfileLabel => Loc[LocalizationConstants.Profile.CopyCurrent];
    public string ImportProfileLabel => Loc[LocalizationConstants.Profile.Import];

    public IReadOnlyList<ActionEditableComboBoxAction> ProfileActions =>
    [
        new(ProfilePickerActionIds.Create, CreateNewProfileLabel),
        new(ProfilePickerActionIds.Copy, CopyCurrentProfileLabel),
        new(ProfilePickerActionIds.Import, ImportProfileLabel),
    ];

    public async Task OnProfileActionAsync(string actionId)
    {
        var previous = _committedProfileName;
        await (actionId switch
        {
            ProfilePickerActionIds.Create => _profilePicker.CreateNewProfileAsync(),
            ProfilePickerActionIds.Copy => _profilePicker.CopyCurrentProfileAsync(),
            ProfilePickerActionIds.Import => _profilePicker.ImportProfileAsync(),
            _ => Task.CompletedTask,
        }).ConfigureAwait(true);

        if (!string.Equals(previous, _committedProfileName, StringComparison.Ordinal))
            ScheduleRestartTranslationIfRunning();
    }

    public Task OnProfileItemSelectedAsync(string? name) => OnProfilePickedFromListCoreAsync(name);

    private async Task OnProfilePickedFromListCoreAsync(string? name)
    {
        var previous = _committedProfileName;
        await _profilePicker.SelectProfileAsync(name).ConfigureAwait(true);
        if (!string.Equals(previous, _committedProfileName, StringComparison.Ordinal))
            ScheduleRestartTranslationIfRunning();
    }

    public bool CommitProfileRename(string? editedText) => _profilePicker.CommitProfileRename(editedText);

    private static IReadOnlyList<string> SortedProfileNames(IEnumerable<string> names) =>
        names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private Task DeleteProfile() => _profilePicker.DeleteProfileAsync(CanDeleteProfile, () => DeleteProfileCommand.NotifyCanExecuteChanged());

    [RelayCommand]
    private Task ExportProfile() => _profilePicker.ExportProfileAsync(Settings?.OwnerWindow);

    private bool CanDeleteProfile() => ProfileNames.Count > 1;
    partial void OnProfileNamesChanged(IReadOnlyList<string> value) => DeleteProfileCommand.NotifyCanExecuteChanged();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowHideTextButtonText))]
    private bool _isTextWindowVisible;

    public string ShowHideTextButtonText => IsTextWindowVisible ? Loc[LocalizationConstants.Buttons.HideText] : Loc[LocalizationConstants.Buttons.ShowText];

    [ObservableProperty] private string _timingInfo = string.Empty;

    public sealed record DisplayModeOption(string Id, string Name);

    public IReadOnlyList<DisplayModeOption> DisplayModeOptions { get; private set; } = [];

    [ObservableProperty] private DisplayModeOption? _selectedDisplayModeOption;

    /// <summary>Persisted display mode id (<c>textWindow</c> / <c>overlay</c>).</summary>
    public string SelectedDisplayMode =>
        SelectedDisplayModeOption?.Id ?? ScreenTranslatorSettingDescriptors.DisplayModeOverlay;

    partial void OnSelectedDisplayModeOptionChanged(DisplayModeOption? oldValue, DisplayModeOption? newValue)
    {
        if (newValue is null) return;
        var screen = _applicationProfileService.LoadScreenTranslatorProfile();
        screen.DisplayMode = newValue.Id is AppConstants.DisplayMode.Overlay or AppConstants.DisplayMode.TextWindow
            ? newValue.Id
            : AppConstants.DisplayMode.Overlay;
        _applicationProfileService.SaveScreenTranslatorProfile(screen);
        _textOutput.OnDisplayModeChanged(newValue.Id);
        if (oldValue is not null
            && !string.Equals(oldValue.Id, newValue.Id, StringComparison.OrdinalIgnoreCase))
            ScheduleRestartTranslationIfRunning();
    }

    internal void RebuildDisplayModeOptions(string? selectedId = null)
    {
        var id = selectedId
            ?? SelectedDisplayModeOption?.Id
            ?? ScreenTranslatorSettingDescriptors.DisplayModeOverlay;
        if (id is not (AppConstants.DisplayMode.Overlay or AppConstants.DisplayMode.TextWindow))
            id = ScreenTranslatorSettingDescriptors.DisplayModeOverlay;

        DisplayModeOptions =
        [
            new DisplayModeOption(ScreenTranslatorSettingDescriptors.DisplayModeOverlay, Loc[LocalizationConstants.DisplayMode.Overlay]),
            new DisplayModeOption(ScreenTranslatorSettingDescriptors.DisplayModeTextWindow, Loc[LocalizationConstants.DisplayMode.TextWindow]),
        ];
        OnPropertyChanged(nameof(DisplayModeOptions));
        SelectedDisplayModeOption = DisplayModeOptions.FirstOrDefault(o => o.Id == id)
            ?? DisplayModeOptions[0];
    }

    internal bool IsOverlayMode =>
        string.Equals(SelectedDisplayMode, ScreenTranslatorSettingDescriptors.DisplayModeOverlay, StringComparison.OrdinalIgnoreCase);

    internal CancellationTokenSource? LoopCts { get; set; }

    public ILocalizationService Localization => _localizationService;
    public LocalizedStrings Loc { get; private set; }
    public IApplicationProfileService ProfileService => _applicationProfileService;
    public IScreenTranslatorContext Context => _screenTranslatorContext;

    public void CancelLoop() => _sessionCoordinator.CancelLoop();
    public Task StopLoopAsync() => _sessionCoordinator.StopLoopAsync();

    private void CancelPendingSettingsRestart()
    {
        _settingsRestartToken++;
        _pendingModuleRefresh = TranslationModuleKind.None;
        _settingsRestartDebounceCts?.Cancel();
        _settingsRestartDebounceCts?.Dispose();
        _settingsRestartDebounceCts = null;
    }

    private void ScheduleModulesRefreshIfRunning(TranslationModuleKind modules)
    {
        if (!IsRunning || modules == TranslationModuleKind.None)
            return;

        if (modules.HasFlag(TranslationModuleKind.FullRestart))
        {
            _pendingModuleRefresh = TranslationModuleKind.None;
            ScheduleRestartTranslationIfRunning();
            return;
        }

        _pendingModuleRefresh |= modules;
        var token = ++_settingsRestartToken;
        _settingsRestartDebounceCts?.Cancel();
        _settingsRestartDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _settingsRestartDebounceCts = cts;
        _ = RefreshModulesAfterSettingsDebounceAsync(token, cts.Token);
    }

    private async Task RefreshModulesAfterSettingsDebounceAsync(int token, CancellationToken ct)
    {
        try
        {
            await Task.Delay(400, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token != _settingsRestartToken || !IsRunning)
            return;

        var modules = _pendingModuleRefresh;
        _pendingModuleRefresh = TranslationModuleKind.None;
        if (modules == TranslationModuleKind.None)
            return;

        if (modules.HasFlag(TranslationModuleKind.FullRestart))
        {
            await RestartTranslationAfterSettingsDebounceAsync(token, CancellationToken.None).ConfigureAwait(true);
            return;
        }

        _sessionCoordinator.RequestModuleRefresh(modules);
    }

    private void ScheduleRestartTranslationIfRunning()
    {
        if (!IsRunning)
            return;

        _pendingModuleRefresh = TranslationModuleKind.None;
        var token = ++_settingsRestartToken;
        _settingsRestartDebounceCts?.Cancel();
        _settingsRestartDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _settingsRestartDebounceCts = cts;
        _ = RestartTranslationAfterSettingsDebounceAsync(token, cts.Token);
    }

    private async Task RestartTranslationAfterSettingsDebounceAsync(int token, CancellationToken ct)
    {
        try
        {
            if (ct.CanBeCanceled)
                await Task.Delay(400, ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token != _settingsRestartToken || !IsRunning)
            return;

        SetStatus(Loc[LocalizationConstants.Status.Stopping]);
        await StopLoopAsync().ConfigureAwait(true);

        if (token != _settingsRestartToken)
            return;

        await StartTranslationSessionAsync().ConfigureAwait(true);
    }

    private async Task StartTranslationSessionAsync()
    {
        var profile = _applicationProfileService.ActiveProfile;
        if (profile is null)
        {
            SetStatus(Loc[LocalizationConstants.Status.NoActiveProfile], isError: true);
            return;
        }

        var target = await _captureResolver.ResolveCaptureTargetAsync(profile).ConfigureAwait(true);
        if (target is null)
            return;

        await _captureResolver.SyncWindowPickerAsync(target).ConfigureAwait(true);
        await _sessionCoordinator.StartSessionAsync(profile, target.Handle, AppConstants.StatusState.Running)
            .ConfigureAwait(true);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task StartStop()
    {
        CancelPendingSettingsRestart();

        if (IsRunning)
        {
            SetStatus(Loc[LocalizationConstants.Status.Stopping]);
            await StopLoopAsync();
            SetStatus(Loc[LocalizationConstants.Status.Stopped]);
            return;
        }

        await StartTranslationSessionAsync().ConfigureAwait(true);
    }

    [RelayCommand] private void ShowHideText() => _textOutput.ToggleTextOutput();
    [RelayCommand] private void OpenHistory() => _textOutput.OpenHistory();

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenCaptureRegions()
    {
        var profile = _applicationProfileService.ActiveProfile;
        if (profile is null)
        {
            SetStatus(Loc[LocalizationConstants.Status.NoActiveProfile], isError: true);
            return;
        }

        var initial = _captureRegionsStore.Load(profile);
        nint? targetHwnd = SelectedWindow is { IsLoadingPlaceholder: false, Handle: not 0 } window
            ? window.Handle
            : null;

        if (targetHwnd is null && initial.IsEmpty)
        {
            SetWindowError(Loc[LocalizationConstants.Status.SelectTargetWindow]);
            return;
        }

        SetWindowError(null);

        var wasRunning = IsRunning;
        CancelPendingSettingsRestart();
        if (wasRunning)
        {
            SetStatus(Loc[LocalizationConstants.Status.Stopping]);
            await StopLoopAsync();
            SetStatus(Loc[LocalizationConstants.Status.Stopped]);
        }

        var owner = GetMainWindow();
        if (owner is null)
            return;

        var result = await CaptureRegionsDialog.ShowAsync(
            owner, profile, targetHwnd, initial, _captureRegionsSnapshotService, _localizationService);
        if (result is null)
            return;

        _captureRegionsStore.Save(profile, result);
        _applicationProfileService.Save(profile);
        RefreshCaptureRegionsIndicator();

        if (wasRunning)
            await StartTranslationSessionAsync().ConfigureAwait(true);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureRegionsTooltip))]
    private bool _hasCaptureRegionsConfigured;

    public string CaptureRegionsTooltip => HasCaptureRegionsConfigured
        ? Loc[LocalizationConstants.CaptureRegions.ConfiguredTooltip]
        : Loc[LocalizationConstants.CaptureRegions.NotConfiguredTooltip];

    internal void RefreshCaptureRegionsIndicator()
    {
        var profile = _applicationProfileService.ActiveProfile;
        HasCaptureRegionsConfigured = profile is not null && !_captureRegionsStore.Load(profile).IsEmpty;
        OnPropertyChanged(nameof(CaptureRegionsTooltip));
    }

    private static Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    public void CloseAuxiliaryWindows() => _textOutput.CloseAuxiliaryWindows();
    public void CaptureTextWindowSettings(WindowSettings settings) => _textOutput.CaptureTextWindowSettings(settings);
    public Task LoadWindowsAsync() => _captureResolver.LoadWindowsAsync();

    [ObservableProperty] private bool _isSettingsOpen;
    [ObservableProperty] private SettingsViewModel? _settings;

    public string SettingsToggleText => $"{(IsSettingsOpen ? "▲" : "▼")} {Loc[LocalizationConstants.Buttons.Settings]}";
    partial void OnIsSettingsOpenChanged(bool value) => OnPropertyChanged(nameof(SettingsToggleText));

    [RelayCommand]
    private void SwitchProfile(string? name)
    {
        if (name is not null) _ = OnProfileItemSelectedAsync(name);
    }
}
