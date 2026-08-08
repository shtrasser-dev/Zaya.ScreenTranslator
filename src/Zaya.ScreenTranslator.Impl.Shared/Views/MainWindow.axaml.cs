using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;
using Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _forceClose;
    private int _startupX;
    private int _startupY;

    private const double ExpandedHeight = 750;
    private const double ExpandedMinHeight = 610;

    /// <summary>Last non-default frame position; used because <see cref="Window.Position"/> can be zeroed while closing.</summary>
    public PixelPoint LastKnownPosition { get; private set; }

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
        PositionChanged += (_, _) => LastKnownPosition = Position;
    }

    /// <summary>
    /// Restores saved X/Y, or on first launch centers as if the settings panel were open
    /// so the collapsed window sits higher and expands into the true center.
    /// Applied when the window has opened so platform layout does not overwrite it.
    /// </summary>
    public void ApplyStartupPosition(int savedX, int savedY)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        _startupX = savedX;
        _startupY = savedY;
        Opened += OnOpenedApplyStartupPosition;
    }

    private void OnOpenedApplyStartupPosition(object? sender, EventArgs e)
    {
        Opened -= OnOpenedApplyStartupPosition;

        if (_startupX != 0 || _startupY != 0)
        {
            var restored = new PixelPoint(_startupX, _startupY);
            Position = restored;
            // Saved coords may point at a disconnected monitor — Avalonia will not move the window back.
            if (!IsWindowVisibleOnAnyScreen(restored))
                CenterAsIfSettingsExpanded();
        }
        else
        {
            CenterAsIfSettingsExpanded();
        }

        LastKnownPosition = Position;
    }

    /// <summary>
    /// True if any part of the window at <paramref name="topLeft"/> intersects a connected screen's working area.
    /// </summary>
    private bool IsWindowVisibleOnAnyScreen(PixelPoint topLeft)
    {
        var screens = Screens?.All;
        if (screens is null || screens.Count == 0)
            return true;

        var scale = Screens?.ScreenFromPoint(topLeft)?.Scaling
            ?? Screens?.Primary?.Scaling
            ?? 1.0;
        var pixelWidth = Math.Max(1, (int)Math.Round(Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Round(Height * scale));
        var windowBounds = new PixelRect(topLeft.X, topLeft.Y, pixelWidth, pixelHeight);

        foreach (var screen in screens)
        {
            if (screen.WorkingArea.Intersects(windowBounds))
                return true;
        }

        return false;
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

    private async void OnWindowPickerDropDownOpened(object? sender, EventArgs e)
    {
        await _viewModel.LoadWindowsAsync();
    }

    private async void OnProfileActionInvoked(object? sender, ActionEditableComboBoxActionEventArgs e)
    {
        await _viewModel.OnProfileActionAsync(e.ActionId);
    }

    private async void OnProfileItemSelected(object? sender, ActionEditableComboBoxItemEventArgs e)
    {
        await _viewModel.OnProfileItemSelectedAsync(e.Item as string ?? e.Item.ToString());
    }

    private void OnProfileRenameRequested(object? sender, ActionEditableComboBoxRenameEventArgs e)
    {
        if (!_viewModel.CommitProfileRename(e.NewName))
            e.Cancel = true;
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
        // Second close pass (after Cancel) often reports Position as 0,0 — do not overwrite a good value.
        if (Position.X != 0 || Position.Y != 0)
            LastKnownPosition = Position;

        if (_forceClose)
            return;

        e.Cancel = true;
        _forceClose = true;

        await _viewModel.StopLoopAsync();
        Close();
    }
}
