using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ITranslationHistoryService _translationHistoryService;
    private readonly ILocalizationService _localizationService;

    public HistoryViewModel(ITranslationHistoryService translationHistoryService, ILocalizationService localizationService)
    {
        _translationHistoryService = translationHistoryService;
        _localizationService = localizationService;
        Loc = new LocalizedStrings(localizationService);
        Refresh();
        _translationHistoryService.Changed += OnHistoryChanged;
    }

    public LocalizedStrings Loc { get; private set; }

    [ObservableProperty]
    private string _historyText = string.Empty;

    public void RefreshLocalization()
    {
        Loc = new LocalizedStrings(_localizationService);
        OnPropertyChanged(nameof(Loc));
    }

    public void Detach() => _translationHistoryService.Changed -= OnHistoryChanged;

    private void OnHistoryChanged() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    public void Refresh() => HistoryText = _translationHistoryService.FormatDisplayText();

    [RelayCommand]
    private void ClearHistory() => _translationHistoryService.Clear();
}
