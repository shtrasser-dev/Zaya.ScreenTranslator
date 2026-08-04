using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Text.Json;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Converters;
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
    private string _translationSettingsFingerprint = string.Empty;

    private static readonly JsonSerializerOptions TranslationFingerprintJsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new SettingsJsonConverter() },
    };

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
        _translationSettingsFingerprint = ComputeTranslationSettingsFingerprint();
    }

    public LocalizationService Localization => _loc;
    public LocalizedStrings Loc { get; private set; }
    public Action? UiCultureChanged { get; set; }
    public Action? TranslationSettingsChanged { get; set; }
    public Window? OwnerWindow { get; set; }
    public IAsyncRelayCommand? DeleteProfileCommand { get; set; }
    public IRelayCommand? SetCurrentProcessCommand { get; set; }

    public IReadOnlyList<LanguageItem> UiLanguages { get; private set; }
    public IReadOnlyList<LanguageItem> TargetLanguages { get; private set; }
    public IReadOnlyList<string> ThemeOptions { get; } = [AppConstants.Theme.Light, AppConstants.Theme.Dark];

    public void ApplyChanges(bool affectsTranslation = true)
    {
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.ProfileName] = SelectedProfileName;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.FramePauseMs] = FramePauseMs;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TargetProcess] = TargetProcess?.Trim() ?? string.Empty;
        _settingsService.CommitEdit(EditingProfile);
        EditingScreenProfile.TargetLanguage = _profileService.LoadScreenTranslatorProfile().TargetLanguage;
        EditingScreenProfile.LastActiveProfileName = SelectedProfileName;
        _profileService.SaveScreenTranslatorProfile(EditingScreenProfile);

        var fingerprint = ComputeTranslationSettingsFingerprint();
        var translationChanged = !string.Equals(fingerprint, _translationSettingsFingerprint, StringComparison.Ordinal);
        _translationSettingsFingerprint = fingerprint;

        if (affectsTranslation && translationChanged)
            TranslationSettingsChanged?.Invoke();
    }

    private string ComputeTranslationSettingsFingerprint()
    {
        var settingsJson = JsonSerializer.Serialize(EditingProfile.Settings, TranslationFingerprintJsonOptions);
        var targetLanguage = EditingScreenProfile.TargetLanguage ?? string.Empty;
        return settingsJson + "\n" + targetLanguage;
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
