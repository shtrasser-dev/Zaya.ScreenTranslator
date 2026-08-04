using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
using Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class SettingsViewModel
{
    [ObservableProperty] private IApplicationProfile _editingProfile;
    [ObservableProperty] private ScreenTranslatorProfile _editingScreenProfile;
    [ObservableProperty] private IReadOnlyList<EngineInfo> _availableOcrEngines;
    [ObservableProperty] private IReadOnlyList<EngineInfo> _availableCaptureEngines;
    [ObservableProperty] private IReadOnlyList<SettingDescriptor>? _ocrDescriptors;
    [ObservableProperty] private IReadOnlyList<SettingDescriptor>? _captureDescriptors;
    [ObservableProperty] private IReadOnlyList<EngineInfo> _availableTextLayoutEngines;
    [ObservableProperty] private IReadOnlyList<SettingDescriptor>? _textLayoutDescriptors;
    [ObservableProperty] private IReadOnlyList<EngineInfo> _availableTranslatorEngines;
    [ObservableProperty] private IReadOnlyList<SettingDescriptor>? _translatorDescriptors;
    [ObservableProperty] private EngineInfo? _selectedTranslatorEngine;
    [ObservableProperty] private IReadOnlyList<EngineInfo> _availableTranslatorCacheEngines;
    [ObservableProperty] private IReadOnlyList<SettingDescriptor>? _translatorCacheDescriptors;
    [ObservableProperty] private EngineInfo? _selectedTranslatorCacheEngine;
    [ObservableProperty] private IReadOnlyList<EngineInfo> _availableOverlayLayoutEngines;
    [ObservableProperty] private IReadOnlyList<SettingDescriptor>? _overlayLayoutDescriptors;
    [ObservableProperty] private EngineInfo? _selectedOverlayLayoutEngine;
    [ObservableProperty] private EngineInfo? _selectedTextLayoutEngine;
    [ObservableProperty] private IReadOnlyList<string> _profileNames;
    [ObservableProperty] private string _selectedProfileName = string.Empty;
    [ObservableProperty] private EngineInfo? _selectedOcrEngine;
    [ObservableProperty] private EngineInfo? _selectedCaptureEngine;
    [ObservableProperty] private LanguageItem? _selectedUiLanguage;
    [ObservableProperty] private LanguageItem? _selectedTargetLanguage;
    [ObservableProperty] private string _selectedTheme = AppConstants.Theme.Light;
    [ObservableProperty] private bool _checkUpdatesOnStartup = true;
    [ObservableProperty] private int _framePauseMs;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFramePauseMsError))]
    private string _framePauseMsText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFramePauseMsError))]
    private string _framePauseMsError = string.Empty;
    [ObservableProperty] private string _targetProcess = string.Empty;
    [ObservableProperty] private string _updateStatusMessage = string.Empty;
    [ObservableProperty] private bool _isCheckingUpdates;

    public bool HasFramePauseMsError => !string.IsNullOrWhiteSpace(FramePauseMsError);

    partial void OnSelectedThemeChanged(string value)
    {
        EditingScreenProfile.Theme = value;
        ApplyTheme(value);
        ApplyChanges(affectsTranslation: false);
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value)
    {
        EditingScreenProfile.CheckUpdatesOnStartup = value;
        ApplyChanges(affectsTranslation: false);
    }

    partial void OnSelectedUiLanguageChanged(LanguageItem? value)
    {
        if (value is null || _suppressLanguageChange) return;

        EditingScreenProfile.UiCulture = value.Code;
        if (IsCurrentCulture(value.Code))
        {
            ApplyChanges(affectsTranslation: false);
            return;
        }

        _loc.SetCulture(value.Code);
        ApplyChanges(affectsTranslation: false);
        UiCultureChanged?.Invoke();
    }

    partial void OnSelectedTargetLanguageChanged(LanguageItem? value)
    {
        if (value is null || _suppressLanguageChange) return;
        EditingScreenProfile.TargetLanguage = value.Code;
        var screen = _profileService.LoadScreenTranslatorProfile();
        screen.TargetLanguage = value.Code;
        _profileService.SaveScreenTranslatorProfile(screen);
        ApplyChanges();
    }

    partial void OnFramePauseMsTextChanged(string value)
    {
        var desc = FramePauseDescriptor;
        if (!IntegerSettingValidation.TryParse(value, desc, out var parsed, out var error, _loc.CurrentCulture))
        {
            FramePauseMsError = error ?? string.Empty;
            return;
        }

        FramePauseMsError = string.Empty;
        if (FramePauseMs != parsed)
            FramePauseMs = parsed;
        else
            ApplyChanges();
    }

    partial void OnFramePauseMsChanged(int value)
    {
        var text = value.ToString(_loc.CurrentCulture);
        if (!string.Equals(FramePauseMsText, text, StringComparison.Ordinal))
            FramePauseMsText = text;
        ApplyChanges();
    }

    partial void OnTargetProcessChanged(string value) => ApplyChanges();

    private static IntegerSettingDescriptor FramePauseDescriptor =>
        ScreenTranslatorSettingDescriptors.All
            .OfType<IntegerSettingDescriptor>()
            .First(d => d.Key == ScreenTranslatorSettingDescriptors.FramePauseMs);

    partial void OnSelectedProfileNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        ApplyChanges();
    }

    partial void OnSelectedOcrEngineChanged(EngineInfo? value)
    {
        if (value is null) return;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.Ocr] = value.Id;
        OcrDescriptors = _descriptorLoader.LoadOcr(EditingProfile);
        ApplyChanges();
    }

    partial void OnSelectedCaptureEngineChanged(EngineInfo? value)
    {
        if (value is null) return;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.Capture] = value.Id;
        CaptureDescriptors = _descriptorLoader.LoadCapture(EditingProfile);
        ApplyChanges();
    }

    partial void OnSelectedTextLayoutEngineChanged(EngineInfo? value)
    {
        if (value is null) return;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TextLayout] = value.Id;
        TextLayoutDescriptors = _descriptorLoader.LoadTextLayout(EditingProfile);
        ApplyChanges();
    }

    partial void OnSelectedTranslatorEngineChanged(EngineInfo? value)
    {
        if (value is null) return;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.Translator] = value.Id;
        TranslatorDescriptors = _descriptorLoader.LoadTranslator(EditingProfile);
        ApplyChanges();
    }

    partial void OnSelectedTranslatorCacheEngineChanged(EngineInfo? value)
    {
        if (value is null) return;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.TranslatorCache] = value.Id;
        TranslatorCacheDescriptors = _descriptorLoader.LoadTranslatorCache(EditingProfile);
        ApplyChanges();
    }

    partial void OnSelectedOverlayLayoutEngineChanged(EngineInfo? value)
    {
        if (value is null) return;
        EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey][ScreenTranslatorSettingDescriptors.OverlayLayout] = value.Id;
        OverlayLayoutDescriptors = _descriptorLoader.LoadOverlayLayout(EditingProfile);
        ApplyChanges();
    }
}
