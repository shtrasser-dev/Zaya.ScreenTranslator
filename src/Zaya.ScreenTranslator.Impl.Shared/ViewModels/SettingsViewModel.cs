using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Update;
using Zaya.ScreenTranslator.Impl.Shared.Views;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly ILocalizationService _localizationService;
    private readonly IConfigurationPathService _configurationPathService;
    private readonly SettingsUpdateChecker _updateChecker;
    private readonly SettingsEngineDescriptorLoader _descriptorLoader;

    private IApplicationProfile _originalProfile;
    private ScreenTranslatorProfile _originalScreenProfile;
    private bool _suppressLanguageChange;
    private Dictionary<string, Dictionary<string, object>> _committedSettingsSnapshot = new();
    private string _committedTargetLanguage = string.Empty;

    public sealed record LanguageItem(string Code, string Name);

    public SettingsViewModel(
        ISettingsService settingsService,
        IApplicationProfileService applicationProfileService,
        ILocalizationService localizationService,
        IPluginUpdateService pluginUpdateService,
        IHostVersionChecker hostVersionChecker,
        IConfigurationPathService configurationPathService)
    {
        _settingsService = settingsService;
        _applicationProfileService = applicationProfileService;
        _localizationService = localizationService;
        _configurationPathService = configurationPathService;
        _updateChecker = new SettingsUpdateChecker(pluginUpdateService, hostVersionChecker, localizationService);
        _descriptorLoader = new SettingsEngineDescriptorLoader(settingsService);

        Loc = new LocalizedStrings(localizationService);

        _originalProfile = _settingsService.BeginEdit();
        _originalScreenProfile = ScreenTranslatorProfileCloner.Clone(_applicationProfileService.LoadScreenTranslatorProfile());

        UiLanguages = BuildUiLanguages();
        TargetLanguages = BuildTargetLanguages();

        _editingProfile = _originalProfile;
        _editingScreenProfile = _originalScreenProfile;
        _availableOcrEngines = _settingsService.GetAvailableOcrEngines();
        _availableCaptureEngines = _settingsService.GetAvailableCaptureEngines();
        _availableTextLayoutEngines = _settingsService.GetAvailableTextLayoutEngines();
        _availableTranslatorEngines = _settingsService.GetAvailableTranslatorEngines();
        _availableTranslatorCacheEngines = _settingsService.GetAvailableTranslatorCacheEngines();
        _availableOverlayLayoutEngines = _settingsService.GetAvailableOverlayLayoutEngines();
        _profileNames = _applicationProfileService.ListProfileNames();
        _selectedProfileName = _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName);

        var st = _editingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey];
        var stSettings = _editingProfile.ScreenTranslatorSettings;
        _selectedOcrEngine = EngineSelection.Pick(
            _availableOcrEngines,
            stSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        _selectedCaptureEngine = EngineSelection.Pick(
            _availableCaptureEngines,
            stSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
        _selectedTextLayoutEngine = EngineSelection.Pick(
            _availableTextLayoutEngines,
            stSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        _selectedTranslatorEngine = EngineSelection.Pick(
            _availableTranslatorEngines,
            stSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));

        var cacheId = stSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache);
        if (string.IsNullOrWhiteSpace(cacheId)
            || string.Equals(cacheId, "none", StringComparison.OrdinalIgnoreCase))
            cacheId = SettingsConstants.EngineDefaults.TranslatorCache;
        _selectedTranslatorCacheEngine = EngineSelection.Pick(_availableTranslatorCacheEngines, cacheId);

        _selectedOverlayLayoutEngine = EngineSelection.Pick(
            _availableOverlayLayoutEngines,
            stSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));

        // Keep the editing profile in sync with engines that actually exist.
        if (_selectedOcrEngine is not null)
            st[ScreenTranslatorSettingDescriptors.Ocr] = _selectedOcrEngine.Id;
        if (_selectedCaptureEngine is not null)
            st[ScreenTranslatorSettingDescriptors.Capture] = _selectedCaptureEngine.Id;
        if (_selectedTextLayoutEngine is not null)
            st[ScreenTranslatorSettingDescriptors.TextLayout] = _selectedTextLayoutEngine.Id;
        if (_selectedTranslatorEngine is not null)
            st[ScreenTranslatorSettingDescriptors.Translator] = _selectedTranslatorEngine.Id;
        if (_selectedTranslatorCacheEngine is not null)
            st[ScreenTranslatorSettingDescriptors.TranslatorCache] = _selectedTranslatorCacheEngine.Id;
        if (_selectedOverlayLayoutEngine is not null)
            st[ScreenTranslatorSettingDescriptors.OverlayLayout] = _selectedOverlayLayoutEngine.Id;

        _framePauseMs = _originalProfile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.FramePauseMs);
        _framePauseMsText = _framePauseMs.ToString(_localizationService.CurrentCulture);
        _targetProcess = _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TargetProcess);
        _selectedUiLanguage = UiLanguages.FirstOrDefault(
            l => string.Equals(l.Code, _originalScreenProfile.UiCulture ?? "en", StringComparison.OrdinalIgnoreCase));
        _selectedTargetLanguage = TargetLanguages.FirstOrDefault(
            l => string.Equals(l.Code, _originalScreenProfile.TargetLanguage ?? "en", StringComparison.OrdinalIgnoreCase));
        _selectedTheme = _originalScreenProfile.Theme is AppConstants.Theme.Light or AppConstants.Theme.Dark
            ? _originalScreenProfile.Theme : AppConstants.Theme.Light;
        _checkUpdatesOnStartup = _originalScreenProfile.CheckUpdatesOnStartup;

        ReloadAllDescriptors();
        _committedSettingsSnapshot = SettingsSnapshot.Clone(EditingProfile.Settings);
        _committedTargetLanguage = EditingScreenProfile.TargetLanguage ?? string.Empty;
    }

    public ILocalizationService Localization => _localizationService;
    public LocalizedStrings Loc { get; private set; }
    public Action? UiCultureChanged { get; set; }
    public Action<TranslationModuleKind>? TranslationModulesChanged { get; set; }
    public Window? OwnerWindow { get; set; }
    public IAsyncRelayCommand? DeleteProfileCommand { get; set; }
    public IAsyncRelayCommand? ExportProfileCommand { get; set; }
    public IRelayCommand? SetCurrentProcessCommand { get; set; }

    public IReadOnlyList<LanguageItem> UiLanguages { get; private set; }
    public IReadOnlyList<LanguageItem> TargetLanguages { get; private set; }
    public IReadOnlyList<string> ThemeOptions { get; } = [AppConstants.Theme.Light, AppConstants.Theme.Dark];

    public void ApplyChanges(bool affectsTranslation = true, TranslationModuleKind moduleHint = TranslationModuleKind.None)
    {
        var st = EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey];
        st[ScreenTranslatorSettingDescriptors.ProfileName] = SelectedProfileName;
        st[ScreenTranslatorSettingDescriptors.FramePauseMs] = FramePauseMs;
        st[ScreenTranslatorSettingDescriptors.TargetProcess] = TargetProcess?.Trim() ?? string.Empty;
        if (SelectedOcrEngine is not null)
            st[ScreenTranslatorSettingDescriptors.Ocr] = SelectedOcrEngine.Id;
        if (SelectedCaptureEngine is not null)
            st[ScreenTranslatorSettingDescriptors.Capture] = SelectedCaptureEngine.Id;
        if (SelectedTextLayoutEngine is not null)
            st[ScreenTranslatorSettingDescriptors.TextLayout] = SelectedTextLayoutEngine.Id;
        if (SelectedTranslatorEngine is not null)
            st[ScreenTranslatorSettingDescriptors.Translator] = SelectedTranslatorEngine.Id;
        if (SelectedTranslatorCacheEngine is not null)
            st[ScreenTranslatorSettingDescriptors.TranslatorCache] = SelectedTranslatorCacheEngine.Id;
        if (SelectedOverlayLayoutEngine is not null)
            st[ScreenTranslatorSettingDescriptors.OverlayLayout] = SelectedOverlayLayoutEngine.Id;

        _settingsService.CommitEdit(EditingProfile);
        // Keep window geometry from disk: this clone may still have default 0,0 and would wipe
        // coordinates that App persists on exit / PositionChanged.
        var existingScreen = _applicationProfileService.LoadScreenTranslatorProfile();
        EditingScreenProfile.MainWindow = ScreenTranslatorProfileCloner.CloneWindowSettings(existingScreen.MainWindow);
        EditingScreenProfile.SettingsWindow = ScreenTranslatorProfileCloner.CloneWindowSettings(existingScreen.SettingsWindow);
        EditingScreenProfile.TextWindow = ScreenTranslatorProfileCloner.CloneWindowSettings(existingScreen.TextWindow);
        EditingScreenProfile.TargetLanguage = existingScreen.TargetLanguage;
        EditingScreenProfile.LastActiveProfileName = SelectedProfileName;
        _applicationProfileService.SaveScreenTranslatorProfile(EditingScreenProfile);

        var modules = TranslationSettingsDiff.Detect(
            _committedSettingsSnapshot,
            EditingProfile.Settings,
            _committedTargetLanguage,
            EditingScreenProfile.TargetLanguage) | moduleHint;
        _committedSettingsSnapshot = SettingsSnapshot.Clone(EditingProfile.Settings);
        _committedTargetLanguage = EditingScreenProfile.TargetLanguage ?? string.Empty;

        if (affectsTranslation && modules != TranslationModuleKind.None)
            TranslationModulesChanged?.Invoke(modules);
    }

    public void RefreshLocalizedLists()
    {
        var uiCode = SelectedUiLanguage?.Code ?? EditingScreenProfile.UiCulture ?? "en";
        var targetCode = SelectedTargetLanguage?.Code ?? EditingScreenProfile.TargetLanguage ?? "en";

        UiLanguages = BuildUiLanguages();
        TargetLanguages = BuildTargetLanguages();
        Loc = new LocalizedStrings(_localizationService);
        OnPropertyChanged(nameof(UiLanguages));
        OnPropertyChanged(nameof(TargetLanguages));
        OnPropertyChanged(nameof(Loc));

        _suppressLanguageChange = true;
        try
        {
            SelectedUiLanguage = UiLanguages.FirstOrDefault(
                l => string.Equals(l.Code, uiCode, StringComparison.OrdinalIgnoreCase));
            SelectedTargetLanguage = TargetLanguages.FirstOrDefault(
                l => string.Equals(l.Code, targetCode, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _suppressLanguageChange = false;
        }
    }

    [RelayCommand]
    private void OpenAppDataFolder()
    {
        var dir = _configurationPathService.GetRootAppDirectory();
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true,
        });
    }

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdates()
    {
        IsCheckingUpdates = true;
        UpdateStatusMessage = _localizationService[LocalizationConstants.Update.Checking];
        try
        {
            UpdateStatusMessage = await _updateChecker.CheckAsync(
                OwnerWindow, EditingScreenProfile, () => ApplyChanges(affectsTranslation: false)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = ex.Message;
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    private bool CanCheckUpdates() => !IsCheckingUpdates;

    partial void OnIsCheckingUpdatesChanged(bool value) => CheckUpdatesCommand.NotifyCanExecuteChanged();

    private IReadOnlyList<LanguageItem> BuildUiLanguages()
        => _localizationService.SupportedUiCultures
            .Select(c => Languages.Find(c))
            .Where(o => o is not null)
            .Select(o => new LanguageItem(o!.Value, o.DisplayName.GetValue(_localizationService.CurrentCulture)))
            .ToList();

    private IReadOnlyList<LanguageItem> BuildTargetLanguages()
        => Languages.All
            .Select(o => new LanguageItem(o.Value, o.DisplayName.GetValue(_localizationService.CurrentCulture)))
            .ToList();

    private void ReloadAllDescriptors()
    {
        OcrDescriptors = _descriptorLoader.LoadOcr(EditingProfile);
        CaptureDescriptors = _descriptorLoader.LoadCapture(EditingProfile);
        TextLayoutDescriptors = _descriptorLoader.LoadTextLayout(EditingProfile);
        TranslatorDescriptors = _descriptorLoader.LoadTranslator(EditingProfile);
        TranslatorCacheDescriptors = _descriptorLoader.LoadTranslatorCache(EditingProfile);
        OverlayLayoutDescriptors = _descriptorLoader.LoadOverlayLayout(EditingProfile);
    }

    private static void ApplyTheme(string theme)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = theme switch
        {
            AppConstants.Theme.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Light,
        };
    }

    private bool IsCurrentCulture(string code) =>
        string.Equals(_localizationService.CurrentCulture.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase)
        || string.Equals(_localizationService.CurrentCulture.Name, code, StringComparison.OrdinalIgnoreCase);
}
