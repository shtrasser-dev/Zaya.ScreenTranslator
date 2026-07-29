using System.ComponentModel;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Runtime context holding the current capture target window.
/// NOT persisted — WindowHandle is transient per session.
/// </summary>
public interface IScreenTranslatorContext : INotifyPropertyChanged
{
    nint ActiveWindowHandle { get; set; }
    string? ActiveWindowTitle { get; set; }
}
