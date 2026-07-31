using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class TextWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _recognizedText = string.Empty;

    [ObservableProperty]
    private bool _isTopmost = true;

    public LocalizedStrings Loc { get; private set; } = new(LocalizationService.Instance);

    public void RefreshLocalization()
    {
        Loc = new LocalizedStrings(LocalizationService.Instance);
        OnPropertyChanged(nameof(Loc));
    }
}
