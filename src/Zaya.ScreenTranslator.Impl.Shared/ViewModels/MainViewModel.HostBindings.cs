using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class MainViewModel
{
    WindowInfo? ICaptureHostState.SelectedWindow => SelectedWindow;
    IReadOnlyList<WindowInfo> ICaptureHostState.Windows { get => Windows; set => Windows = value; }
    WindowInfo? ICaptureHostState.LastSelectedWindow { get => _lastSelectedWindow; set => _lastSelectedWindow = value; }
    void ICaptureHostState.SetSelectedWindow(WindowInfo? window) => SelectedWindow = window;
    void ICaptureHostState.NotifySetCurrentProcessCanExecuteChanged() => SetCurrentProcessCommand.NotifyCanExecuteChanged();
    IScreenTranslatorContext ICaptureHostState.Context => _context;
    LocalizedStrings IStatusHost.Loc => Loc;
    CancellationTokenSource? ICaptureHostState.LoopCts { get => LoopCts; set => LoopCts = value; }

    IReadOnlyList<string> IProfilePickerHost.ProfileNames { get => ProfileNames; set => ProfileNames = value; }
    IReadOnlyList<string> IProfilePickerHost.ProfilePickerItems { get => ProfilePickerItems; set => ProfilePickerItems = value; }
    string? IProfilePickerHost.SelectedProfileName { get => SelectedProfileName; set => SelectedProfileName = value; }
    string IProfilePickerHost.ProfileErrorMessage { get => ProfileErrorMessage; set => ProfileErrorMessage = value; }
    string? IProfilePickerHost.CommittedProfileName { get => _committedProfileName; set => _committedProfileName = value; }
    bool IProfilePickerHost.SuppressProfileChange { get => _suppressProfileChange; set => _suppressProfileChange = value; }
    string IProfilePickerHost.CreateNewProfileLabel => CreateNewProfileLabel;
    LocalizedStrings IProfilePickerHost.Loc => Loc;
    void IProfilePickerHost.ReloadSettingsIfOpen() => ReloadSettingsIfOpen();
    void IProfilePickerHost.NotifyDeleteProfileCanExecuteChanged() => DeleteProfileCommand.NotifyCanExecuteChanged();

    bool ITextOutputHost.IsTextWindowVisible { get => IsTextWindowVisible; set => IsTextWindowVisible = value; }
    bool ITextOutputHost.IsOverlayMode => IsOverlayMode;

    string ITranslationSessionHost.TimingInfo { get => TimingInfo; set => TimingInfo = value; }
    bool ITranslationSessionHost.IsOverlayMode => IsOverlayMode;
    bool ITranslationSessionHost.IsRunning { get => IsRunning; set => IsRunning = value; }
    void ITranslationSessionHost.SetTextOutputVisible(bool visible) => _textOutput.SetTextOutputVisible(visible);
    CancellationTokenSource? ITranslationSessionHost.LoopCts { get => LoopCts; set => LoopCts = value; }
}
