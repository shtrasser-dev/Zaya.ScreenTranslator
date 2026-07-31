using Avalonia.Controls;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class HistoryWindow : Window
{
    private bool _forceClose;

    public HistoryWindow()
    {
        InitializeComponent();
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose)
            return;

        // Hide instead of destroy so the same window can be reopened quickly.
        e.Cancel = true;
        Hide();
    }
}
