using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

internal interface IStatusHost
{
    void SetStatus(string text, string? key = null, bool isError = false);
    void SetWindowError(string? message);
    LocalizedStrings Loc { get; }
}

internal interface ICaptureHostState : IStatusHost
{
    WindowInfo? SelectedWindow { get; }
    bool IsRunning { get; set; }
    CancellationTokenSource? LoopCts { get; set; }
    IScreenTranslatorContext Context { get; }
    IReadOnlyList<WindowInfo> Windows { get; set; }
    WindowInfo? LastSelectedWindow { get; set; }
    void SetSelectedWindow(WindowInfo? window);
    void NotifySetCurrentProcessCanExecuteChanged();
}
