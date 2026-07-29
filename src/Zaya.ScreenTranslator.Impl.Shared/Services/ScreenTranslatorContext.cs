using CommunityToolkit.Mvvm.ComponentModel;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class ScreenTranslatorContext : ObservableObject, IScreenTranslatorContext
{
    private nint _activeWindowHandle;
    private string? _activeWindowTitle;

    public nint ActiveWindowHandle
    {
        get => _activeWindowHandle;
        set => SetProperty(ref _activeWindowHandle, value);
    }

    public string? ActiveWindowTitle
    {
        get => _activeWindowTitle;
        set => SetProperty(ref _activeWindowTitle, value);
    }
}
