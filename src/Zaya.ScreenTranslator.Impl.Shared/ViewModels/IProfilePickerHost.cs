using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal interface IProfilePickerHost
{
    IReadOnlyList<string> ProfileNames { get; set; }
    IReadOnlyList<string> ProfilePickerItems { get; set; }
    string? SelectedProfileName { get; set; }
    string ProfileErrorMessage { get; set; }
    string? CommittedProfileName { get; set; }
    bool SuppressProfileChange { get; set; }
    string CreateNewProfileLabel { get; }
    LocalizedStrings Loc { get; }
    void ReloadSettingsIfOpen();
    void NotifyDeleteProfileCanExecuteChanged();
}
