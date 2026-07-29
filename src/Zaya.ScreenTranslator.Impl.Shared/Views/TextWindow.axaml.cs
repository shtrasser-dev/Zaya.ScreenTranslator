using Avalonia.Controls;
using Avalonia.Interactivity;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class TextWindow : Window
{
    private bool _forceClose;

    public TextWindow()
    {
        InitializeComponent();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Keep the window instance for reuse on Hide, but allow real close on app shutdown.
        if (_forceClose)
            return;

        e.Cancel = true;
        Hide();
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }
}
