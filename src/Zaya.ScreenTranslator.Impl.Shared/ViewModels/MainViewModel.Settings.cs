using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class MainViewModel
{
    private SettingsViewModel CreateSettingsViewModel()
    {
        var vm = new SettingsViewModel(_settingsService, _profileService, Localization, _pluginUpdateService, _hostVersionChecker);
        vm.UiCultureChanged = RefreshUiForCulture;
        vm.DeleteProfileCommand = DeleteProfileCommand;
        vm.SetCurrentProcessCommand = SetCurrentProcessCommand;
        return vm;
    }

    private void ReloadSettingsIfOpen()
    {
        if (!IsSettingsOpen) return;
        Settings = CreateSettingsViewModel();
    }

    private void RefreshUiForCulture()
    {
        if (_isRefreshingUiCulture) return;
        _isRefreshingUiCulture = true;
        try
        {
            Loc = new LocalizedStrings(Localization);
            OnPropertyChanged(nameof(Loc));
            OnPropertyChanged(nameof(StartStopButtonText));
            OnPropertyChanged(nameof(ShowHideTextButtonText));
            OnPropertyChanged(nameof(SettingsToggleText));

            var targetCode = SelectedTargetLanguage?.Code ?? "en";
            TargetLanguages = BuildTargetLanguages();
            SelectedTargetLanguage = TargetLanguages.FirstOrDefault(
                l => string.Equals(l.Code, targetCode, StringComparison.OrdinalIgnoreCase))
                ?? TargetLanguages.FirstOrDefault();

            if (_statusKey == AppConstants.StatusState.Idle)
                SetStatus(Loc[LocalizationConstants.Status.Idle], AppConstants.StatusState.Idle);
            else if (_statusKey == AppConstants.StatusState.Running)
                SetStatus(Loc[LocalizationConstants.Status.Running], AppConstants.StatusState.Running);
            OnPropertyChanged(nameof(StatusLine));
            OnPropertyChanged(nameof(StatusLabelText));
            RebuildDisplayModeOptions();
            _profilePicker.RefreshProfilePicker();
            _profilePicker.SetSelectedProfileSilent(_committedProfileName);

            if (IsSettingsOpen)
            {
                if (Settings is not null) Settings.UiCultureChanged = null;
                Settings = CreateSettingsViewModel();
            }

            _textOutput.RefreshLocalization();
            ForceMainWindowRebind();
            _textOutput.ForceWindowRebind();
        }
        finally { _isRefreshingUiCulture = false; }
    }

    private static void ForceMainWindowRebind()
    {
        var owner = Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        if (owner is null) return;
        var dc = owner.DataContext;
        owner.DataContext = null;
        owner.DataContext = dc;
    }

    [RelayCommand]
    private async Task ToggleSettings()
    {
        if (IsSettingsOpen) { await CloseSettingsPanelAsync(); return; }
        Settings = CreateSettingsViewModel();
        IsSettingsOpen = true;
    }

    private async Task CloseSettingsPanelAsync()
    {
        _profilePicker.RefreshProfilePicker();
        var activeName = _profileService.ActiveProfile?.ScreenTranslatorSettings
            .GetValueAsString(ScreenTranslatorSettingDescriptors.ProfileName)
            ?? ProfileNames.FirstOrDefault();
        _committedProfileName = activeName;
        _profilePicker.SetSelectedProfileSilent(activeName);

        var screen = _profileService.LoadScreenTranslatorProfile();
        RebuildDisplayModeOptions(screen.DisplayMode);

        if (Settings is not null) Settings.UiCultureChanged = null;
        Settings = null;
        IsSettingsOpen = false;
        ForceMainWindowRebind();
        await Task.CompletedTask;
    }
}
