using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class TextWindowViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;

    public TextWindowViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        Loc = new LocalizedStrings(localizationService);
    }

    [ObservableProperty]
    private string _recognizedText = string.Empty;

    [ObservableProperty]
    private bool _isTopmost = true;

    public LocalizedStrings Loc { get; private set; }

    public void RefreshLocalization()
    {
        Loc = new LocalizedStrings(_localizationService);
        OnPropertyChanged(nameof(Loc));
    }
}
