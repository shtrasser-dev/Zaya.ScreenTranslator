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
    private readonly IApplicationProfileService _profileService;
    private readonly LocalizationService _loc;
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
        IApplicationProfileService profileService,
        LocalizationService loc,
        PluginUpdateService pluginUpdateService,
        HostVersionChecker hostVersionChecker)
    {
        _settingsService = settingsService;
        _profileService = profileService;
        _loc = loc;
        _updateChecker = new SettingsUpdateChecker(pluginUpdateService, hostVersionChecker, loc);
        _descriptorLoader = new SettingsEngineDescriptorLoader(settingsService);

        Loc = new LocalizedStrings(loc);

        _originalProfile = _settingsService.BeginEdit();
        _originalScreenProfile = ScreenTranslatorProfileCloner.Clone(_profileService.LoadScreenTranslatorProfile());

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
        var cacheId = _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache);
        if (string.IsNullOrWhiteSpace(cacheId)
            || string.Equals(cacheId, "none", StringComparison.OrdinalIgnoreCase))
            cacheId = SettingsConstants.EngineDefaults.TranslatorCache;
        _selectedTranslatorCacheEngine = _availableTranslatorCacheEngines.FirstOrDefault(e => e.Id == cacheId)
            ?? _availableTranslatorCacheEngines.FirstOrDefault(e =>
                e.Id == SettingsConstants.EngineDefaults.TranslatorCache)
            ?? _availableTranslatorCacheEngines.FirstOrDefault(e =>
                e.Id != NoTranslatorCacheService.EngineIdValue)
            ?? _availableTranslatorCacheEngines.FirstOrDefault();
        _selectedOverlayLayoutEngine = _availableOverlayLayoutEngines.FirstOrDefault(e => e.Id ==
            _originalProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout))
            ?? _availableOverlayLayoutEngines.FirstOrDefault();
        _framePauseMs = _originalProfile.ScreenTranslatorSettings.GetValueAsInt(ScreenTranslatorSettingDescriptors.FramePauseMs);
        _framePauseMsText = _framePauseMs.ToString(_loc.CurrentCulture);
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

    public LocalizationService Localization => _loc;
    public LocalizedStrings Loc { get; private set; }
    public Action? UiCultureChanged { get; set; }
    public Action<TranslationModuleKind>? TranslationModulesChanged { get; set; }
    public Window? OwnerWindow { get; set; }
    public IAsyncRelayCommand? DeleteProfileCommand { get; set; }
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
        EditingScreenProfile.TargetLanguage = _profileService.LoadScreenTranslatorProfile().TargetLanguage;
        EditingScreenProfile.LastActiveProfileName = SelectedProfileName;
        _profileService.SaveScreenTranslatorProfile(EditingScreenProfile);

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
        Loc = new LocalizedStrings(_loc);
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
        var dir = App.DataDirectory;
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
        UpdateStatusMessage = _loc[LocalizationConstants.Update.Checking];
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
        => LocalizationService.SupportedUiCultures
            .Select(c => Languages.Find(c))
            .Where(o => o is not null)
            .Select(o => new LanguageItem(o!.Value, o.DisplayName.GetValue(_loc.CurrentCulture)))
            .ToList();

    private IReadOnlyList<LanguageItem> BuildTargetLanguages()
        => Languages.All
            .Select(o => new LanguageItem(o.Value, o.DisplayName.GetValue(_loc.CurrentCulture)))
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
        string.Equals(_loc.CurrentCulture.TwoLetterISOLanguageName, code, StringComparison.OrdinalIgnoreCase)
        || string.Equals(_loc.CurrentCulture.Name, code, StringComparison.OrdinalIgnoreCase);
}
