using Avalonia.Controls;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _forceClose;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose)
            return;

        // Defer close until the translation loop and child windows are torn down.
        e.Cancel = true;
        _forceClose = true;

        await _viewModel.StopLoopAsync();
        Close();
    }
}
