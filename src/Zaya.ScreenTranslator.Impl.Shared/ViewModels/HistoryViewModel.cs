using CommunityToolkit.Mvvm.ComponentModel;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly TranslationHistoryService _history;
    private readonly LocalizationService _loc;

    public HistoryViewModel(TranslationHistoryService history, LocalizationService loc)
    {
        _history = history;
        _loc = loc;
        Loc = new LocalizedStrings(loc);
        Refresh();
        _history.Changed += OnHistoryChanged;
    }

    public LocalizedStrings Loc { get; private set; }

    [ObservableProperty]
    private string _historyText = string.Empty;

    public void RefreshLocalization()
    {
        Loc = new LocalizedStrings(_loc);
        OnPropertyChanged(nameof(Loc));
    }

    public void Detach() => _history.Changed -= OnHistoryChanged;

    private void OnHistoryChanged() =>
        Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    public void Refresh() => HistoryText = _history.FormatDisplayText();
}
