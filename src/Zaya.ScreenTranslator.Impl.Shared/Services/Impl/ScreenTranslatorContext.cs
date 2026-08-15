using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

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
