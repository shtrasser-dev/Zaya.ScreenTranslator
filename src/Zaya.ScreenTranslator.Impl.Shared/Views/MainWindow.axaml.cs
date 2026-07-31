using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _forceClose;
    private bool _skipProfileRenameOnLostFocus;

    private const double ExpandedHeight = 700;
    private const double ExpandedMinHeight = 560;

    /// <summary>Required by Avalonia XAML tooling (AVLN3001). App uses <see cref="MainWindow(MainViewModel)"/>.</summary>
    public MainWindow()
    {
        _viewModel = null!;
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplySettingsExpandedSize(viewModel.IsSettingsOpen);
    }

    /// <summary>
    /// Restores saved X/Y, or on first launch centers as if the settings panel were open
    /// so the collapsed window sits higher and expands into the true center.
    /// </summary>
    public void ApplyStartupPosition(int savedX, int savedY)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (savedX != 0 || savedY != 0)
        {
            Position = new PixelPoint(savedX, savedY);
            return;
        }

        CenterAsIfSettingsExpanded();
    }

    private void CenterAsIfSettingsExpanded()
    {
        var screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var pixelWidth = (int)Math.Round(Width * scale);
        var pixelExpandedHeight = (int)Math.Round(ExpandedHeight * scale);

        var x = area.X + Math.Max(0, (area.Width - pixelWidth) / 2);
        var y = area.Y + Math.Max(0, (area.Height - pixelExpandedHeight) / 2);
        Position = new PixelPoint(x, y);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSettingsOpen))
            ApplySettingsExpandedSize(_viewModel.IsSettingsOpen);
    }

    private async void OnWindowComboDropDownOpened(object? sender, EventArgs e)
    {
        await _viewModel.LoadWindowsAsync();
    }

    private async void OnProfileComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Only react to picks from the open dropdown — not to text matching while typing.
        if (sender is not ComboBox { IsDropDownOpen: true, SelectedItem: string name } combo)
            return;

        // Closing the dropdown / clearing focus raises LostFocus (possibly more than once).
        // Keep rename suppressed until the focus cycle finishes.
        _skipProfileRenameOnLostFocus = true;

        combo.IsDropDownOpen = false;
        await _viewModel.OnProfilePickedFromListAsync(name);
        combo.Text = _viewModel.SelectedProfileName;
        ClearProfileComboFocus(combo);

        Dispatcher.UIThread.Post(() => _skipProfileRenameOnLostFocus = false, DispatcherPriority.Background);
    }

    private void OnProfileComboLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo)
            return;

        if (_skipProfileRenameOnLostFocus)
            return;

        if (!_viewModel.CommitProfileRename(combo.Text))
            combo.Text = _viewModel.SelectedProfileName;

        ClearEditableComboSelection(combo);
    }

    private void OnProfileComboKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not ComboBox combo)
            return;

        e.Handled = true;
        if (!_viewModel.CommitProfileRename(combo.Text))
        {
            combo.Text = _viewModel.SelectedProfileName;
            return;
        }

        _skipProfileRenameOnLostFocus = true;
        ClearProfileComboFocus(combo);
        Dispatcher.UIThread.Post(() => _skipProfileRenameOnLostFocus = false, DispatcherPriority.Background);
    }

    private void ClearProfileComboFocus(ComboBox combo)
    {
        // Move focus off the editable ComboBox, then drop leftover SelectAll highlight.
        Focus();
        ClearEditableComboSelection(combo);
        Dispatcher.UIThread.Post(() => ClearEditableComboSelection(combo), DispatcherPriority.Input);
    }

    private static void ClearEditableComboSelection(ComboBox combo)
    {
        var textBox = combo.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        if (textBox is null)
            return;

        var caret = textBox.Text?.Length ?? 0;
        textBox.CaretIndex = caret;
        textBox.SelectionStart = caret;
        textBox.SelectionEnd = caret;
    }

    private void ApplySettingsExpandedSize(bool open)
    {
        // Keep top-left fixed: expand/collapse grows or shrinks downward.
        // Collapsed: hug content so the window ends under the Settings button.
        // Expanded: fixed height; settings row fills the remaining space.
        if (MainLayout.RowDefinitions.Count > 3)
        {
            MainLayout.RowDefinitions[3].Height = open
                ? new GridLength(1, GridUnitType.Star)
                : GridLength.Auto;
        }

        if (open)
        {
            SizeToContent = SizeToContent.Manual;
            MinHeight = ExpandedMinHeight;
            Height = ExpandedHeight;
        }
        else
        {
            MinHeight = 0;
            SizeToContent = SizeToContent.Height;
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose)
            return;

        e.Cancel = true;
        _forceClose = true;

        await _viewModel.StopLoopAsync();
        Close();
    }
}
