using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IApplicationProfileService _profileService;
    private readonly LocalizationService _loc;
    private readonly PluginUpdateService _pluginUpdateService;
    private readonly HostVersionChecker _hostVersionChecker;

    private IApplicationProfile _originalProfile;
    private ScreenTranslatorProfile _originalScreenProfile;

    public sealed record LanguageItem(string Code, string Name);

    public SettingsViewModel(
        ISettingsService settingsService,
        IApplicationProfileService profileService,
        LocalizationService loc,
        PluginUpdateService pluginUpdateService,
        HostVersionChecker hostVersionChecker)
    {
        _settingsService = settingsService;
        _profileService = profileService;
        _loc = loc;
        _pluginUpdateService = pluginUpdateService;
        _hostVersionChecker = hostVersionChecker;

        _originalProfile = _settingsService.BeginEdit();
        _originalScreenProfile = CloneScreenProfile(_profileService.LoadScreenTranslatorProfile());

        UiLanguages = new[] { "en", "ru" }
            .Select(c => Languages.Find(c))
            .Where(o => o is not null)
            .Select(o => new LanguageItem(o!.Value, o.DisplayName.GetValue(loc.CurrentCulture)))
            .ToList();

        TargetLanguages = Languages.All
            .Select(o => new LanguageItem(o.Value, o.DisplayName.GetValue(loc.CurrentCulture)))
            .ToList();

        // Copy to observable properties
        _editingProfile = _originalProfile;
        _editingScreenProfile = _originalScreenProfile;
        _availableOcrEngines = _settingsService.GetAvailableOcrEngines();
        _availableCaptureEngines = _settingsService.GetAvailableCaptureEngines();
        _availableTextLayoutEngines = _settingsService.GetAvailableTextLayoutEngines();
        _availableTranslatorEngines = _settingsService.GetAvailableTranslatorEngines();
        _availableOverlayLayoutEngines = _settingsService.GetAvailableOverlayLayoutEngines();
        _profileNames = _profileService.ListProfileNames();
        _selectedProfileName = _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);
        _selectedOcrEngine = _availableOcrEngines.FirstOrDefault(e => e.Id ==
            _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        _selectedCaptureEngine = _availableCaptureEngines.FirstOrDefault(e => e.Id ==
            _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
        _selectedTextLayoutEngine = _availableTextLayoutEngines.FirstOrDefault(e => e.Id ==
            _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        _selectedTranslatorEngine = _availableTranslatorEngines.FirstOrDefault(e => e.Id ==
            _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
        _selectedOverlayLayoutEngine = _availableOverlayLayoutEngines.FirstOrDefault(e => e.Id ==
            _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout))
            ?? _availableOverlayLayoutEngines.FirstOrDefault();
        _targetFps = _originalProfile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.TargetFps);
        _selectedUiLanguage = UiLanguages.FirstOrDefault(
            l => string.Equals(l.Code, _originalScreenProfile.UiCulture ?? "en", StringComparison.OrdinalIgnoreCase));
        _selectedTargetLanguage = TargetLanguages.FirstOrDefault(
            l => string.Equals(l.Code, _originalScreenProfile.TargetLanguage ?? "en", StringComparison.OrdinalIgnoreCase));
        _selectedTheme = _originalScreenProfile.Theme is "light" or "dark"
            ? _originalScreenProfile.Theme : "light";
        _checkUpdatesOnStartup = _originalScreenProfile.CheckUpdatesOnStartup;

        LoadOcrDescriptors();
        LoadCaptureDescriptors();
        LoadTextLayoutDescriptors();
        LoadTranslatorDescriptors();
        LoadOverlayLayoutDescriptors();
    }

    // ── Observable properties ──

    [ObservableProperty]
    private IApplicationProfile _editingProfile;

    [ObservableProperty]
    private ScreenTranslatorProfile _editingScreenProfile;

    [ObservableProperty]
    private IReadOnlyList<EngineInfo> _availableOcrEngines;

    [ObservableProperty]
    private IReadOnlyList<EngineInfo> _availableCaptureEngines;

    [ObservableProperty]
    private IReadOnlyList<SettingDescriptor>? _ocrDescriptors;

    [ObservableProperty]
    private IReadOnlyList<SettingDescriptor>? _captureDescriptors;

    [ObservableProperty]
    private IReadOnlyList<EngineInfo> _availableTextLayoutEngines;

    [ObservableProperty]
    private IReadOnlyList<SettingDescriptor>? _textLayoutDescriptors;

    [ObservableProperty]
    private IReadOnlyList<EngineInfo> _availableTranslatorEngines;

    [ObservableProperty]
    private IReadOnlyList<SettingDescriptor>? _translatorDescriptors;

    [ObservableProperty]
    private EngineInfo? _selectedTranslatorEngine;

    [ObservableProperty]
    private IReadOnlyList<EngineInfo> _availableOverlayLayoutEngines;

    [ObservableProperty]
    private IReadOnlyList<SettingDescriptor>? _overlayLayoutDescriptors;

    [ObservableProperty]
    private EngineInfo? _selectedOverlayLayoutEngine;

    [ObservableProperty]
    private EngineInfo? _selectedTextLayoutEngine;

    [ObservableProperty]
    private IReadOnlyList<string> _profileNames;

    [ObservableProperty]
    private string _selectedProfileName = string.Empty;

    [ObservableProperty]
    private EngineInfo? _selectedOcrEngine;

    [ObservableProperty]
    private EngineInfo? _selectedCaptureEngine;

    public IReadOnlyList<LanguageItem> UiLanguages { get; }
    public IReadOnlyList<LanguageItem> TargetLanguages { get; }
    public IReadOnlyList<string> ThemeOptions { get; } = ["light", "dark"];

    [ObservableProperty]
    private LanguageItem? _selectedUiLanguage;

    [ObservableProperty]
    private LanguageItem? _selectedTargetLanguage;

    [ObservableProperty]
    private string _selectedTheme = "light";

    [ObservableProperty]
    private bool _checkUpdatesOnStartup = true;

    [ObservableProperty]
    private int _targetFps;

    partial void OnSelectedThemeChanged(string value)
    {
        EditingScreenProfile.Theme = value;
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value)
    {
        EditingScreenProfile.CheckUpdatesOnStartup = value;
    }

    partial void OnSelectedUiLanguageChanged(LanguageItem? value)
    {
        if (value is not null)
            EditingScreenProfile.UiCulture = value.Code;
    }

    partial void OnSelectedTargetLanguageChanged(LanguageItem? value)
    {
        if (value is not null)
            EditingScreenProfile.TargetLanguage = value.Code;
    }

    partial void OnSelectedOcrEngineChanged(EngineInfo? value)
    {
        if (value is not null)
        {
            EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.Ocr] = value.Id;
            LoadOcrDescriptors();
        }
    }

    partial void OnSelectedCaptureEngineChanged(EngineInfo? value)
    {
        if (value is not null)
        {
            EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.Capture] = value.Id;
            LoadCaptureDescriptors();
        }
    }

    partial void OnSelectedTextLayoutEngineChanged(EngineInfo? value)
    {
        if (value is not null)
        {
            EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TextLayout] = value.Id;
            LoadTextLayoutDescriptors();
        }
    }

    partial void OnSelectedTranslatorEngineChanged(EngineInfo? value)
    {
        if (value is not null)
        {
            EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.Translator] = value.Id;
            LoadTranslatorDescriptors();
        }
    }

    partial void OnSelectedOverlayLayoutEngineChanged(EngineInfo? value)
    {
        if (value is not null)
        {
            EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.OverlayLayout] = value.Id;
            LoadOverlayLayoutDescriptors();
        }
    }

    public LocalizationService Loc => _loc;

    /// <summary>Set by the view to show a name-input dialog. Returns the entered name, or null if cancelled.</summary>
    public Func<Task<string?>>? SaveAsNewPrompt;

    /// <summary>Set by the view for modal dialogs.</summary>
    public Window? OwnerWindow { get; set; }

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCheckingUpdates;

    // ── Commands ──

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdates()
    {
        IsCheckingUpdates = true;
        UpdateStatusMessage = _loc["Update_Checking"];
        try
        {
            var channel = HostChannel.Current;
            var hostUpdate = await _hostVersionChecker.CheckAsync(channel);
            if (hostUpdate.UpdateAvailable && !string.IsNullOrEmpty(hostUpdate.ReleaseHtmlUrl))
            {
                var open = await UpdateDialogs.ShowHostUpdateAsync(
                    OwnerWindow,
                    hostUpdate.RemoteVersion?.ToString() ?? "?",
                    hostUpdate.ReleaseName);
                if (open)
                    HostVersionChecker.OpenReleasePage(hostUpdate.ReleaseHtmlUrl);
            }

            var result = await _pluginUpdateService.EnsurePluginsAsync(
                App.PluginsDirectory,
                channel,
                updateOptional: true,
                checkForUpdates: true);

            if (!result.Success)
            {
                UpdateStatusMessage = result.ErrorMessage ?? _loc["Update_Failed"];
                await UpdateDialogs.ShowMessageAsync(OwnerWindow, _loc["Update_Title"], UpdateStatusMessage);
                return;
            }

            if (result.DownloadedAssets.Count > 0)
            {
                UpdateStatusMessage = _loc["Update_RestartRequired"];
                await UpdateDialogs.ShowMessageAsync(OwnerWindow, _loc["Update_Title"], UpdateStatusMessage);
            }
            else if (!hostUpdate.UpdateAvailable)
            {
                UpdateStatusMessage = _loc["Update_UpToDate"];
                await UpdateDialogs.ShowMessageAsync(OwnerWindow, _loc["Update_Title"], UpdateStatusMessage);
            }
            else
            {
                UpdateStatusMessage = _loc["Update_PluginsOk"];
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = ex.Message;
            await UpdateDialogs.ShowMessageAsync(OwnerWindow, _loc["Update_Title"], ex.Message);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private bool CanCheckUpdates() => !IsCheckingUpdates;

    partial void OnIsCheckingUpdatesChanged(bool value) => CheckUpdatesCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task Save()
    {
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName] = SelectedProfileName;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TargetFps] = TargetFps;
        _settingsService.CommitEdit(EditingProfile);
        EditingScreenProfile.LastActiveProfileName = SelectedProfileName;
        _profileService.SaveScreenTranslatorProfile(EditingScreenProfile);

        ApplyTheme(EditingScreenProfile.Theme);
        _loc.SetCulture(EditingScreenProfile.UiCulture);

        await CloseWindow();
    }

    [RelayCommand]
    private async Task SaveAsNew()
    {
        if (SaveAsNewPrompt is null) return;

        var name = await SaveAsNewPrompt();
        if (string.IsNullOrWhiteSpace(name)) return;

        _settingsService.CommitEditAsNew(name, EditingProfile);
        EditingScreenProfile.LastActiveProfileName = name;
        _profileService.SaveScreenTranslatorProfile(EditingScreenProfile);
        ApplyTheme(EditingScreenProfile.Theme);
        _loc.SetCulture(EditingScreenProfile.UiCulture);

        await CloseWindow();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await CloseWindow();
    }

    // ── Event for view close ──

    public event Func<Task>? CloseRequested;

    // ── Helpers ──

    private void LoadOcrDescriptors()
    {
        OcrDescriptors = _settingsService.GetOcrDescriptors(
            EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
    }

    private void LoadCaptureDescriptors()
    {
        CaptureDescriptors = _settingsService.GetCaptureDescriptors(
            EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
    }

    private void LoadTextLayoutDescriptors()
    {
        TextLayoutDescriptors = _settingsService.GetTextLayoutDescriptors(
            EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
    }

    private void LoadTranslatorDescriptors()
    {
        TranslatorDescriptors = _settingsService.GetTranslatorDescriptors(
            EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
    }

    private void LoadOverlayLayoutDescriptors()
    {
        OverlayLayoutDescriptors = _settingsService.GetOverlayLayoutDescriptors(
            EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
    }

    private static void ApplyTheme(string theme)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = theme switch
            {
                "dark" => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Light,
            };
        }
    }

    private async Task CloseWindow()
    {
        if (CloseRequested is not null)
            await CloseRequested.Invoke();
    }

    private static ScreenTranslatorProfile CloneScreenProfile(ScreenTranslatorProfile source)
    {
        return new ScreenTranslatorProfile
        {
            MainWindow = CloneWindowSettings(source.MainWindow),
            SettingsWindow = CloneWindowSettings(source.SettingsWindow),
            TextWindow = CloneWindowSettings(source.TextWindow),
            UiCulture = source.UiCulture,
            Theme = source.Theme,
            TargetLanguage = source.TargetLanguage,
            LastActiveProfileName = source.LastActiveProfileName,
            DisplayMode = source.DisplayMode,
            CheckUpdatesOnStartup = source.CheckUpdatesOnStartup,
        };
    }

    private static WindowSettings CloneWindowSettings(WindowSettings ws)
    {
        return new WindowSettings
        {
            X = ws.X,
            Y = ws.Y,
            Width = ws.Width,
            Height = ws.Height,
            Topmost = ws.Topmost,
        };
    }
}
