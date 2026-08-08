using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Zaya.ScreenTranslator.Impl.Shared.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>
/// Window target picker: icon/title rows, loading placeholder, refresh on open.
/// </summary>
public partial class WindowPickerComboBox : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<WindowPickerComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<WindowInfo?> SelectedItemProperty =
        AvaloniaProperty.Register<WindowPickerComboBox, WindowInfo?>(
            nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    private bool _wired;
    private bool _syncingSelection;

    public WindowPickerComboBox()
    {
        InitializeComponent();
        Loaded += OnControlLoaded;
    }

    private void OnControlLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureWired();
        ApplyItemsSource();
        ApplySelectedItem();
    }

    private void EnsureWired()
    {
        if (_wired)
            return;

        _wired = true;
        Combo.SelectionChanged += OnComboSelectionChanged;
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public WindowInfo? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Raised when the dropdown opens so the host can refresh the window list.</summary>
    public event EventHandler? DropDownOpened;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (!_wired)
            return;

        if (change.Property == ItemsSourceProperty)
            ApplyItemsSource();
        else if (change.Property == SelectedItemProperty)
            ApplySelectedItem();
    }

    private void ApplyItemsSource()
    {
        _syncingSelection = true;
        try
        {
            Combo.ItemsSource = ItemsSource;
            ApplySelectedItem();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void ApplySelectedItem()
    {
        _syncingSelection = true;
        try
        {
            Combo.SelectedItem = SelectedItem;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
            return;

        SelectedItem = Combo.SelectedItem as WindowInfo;
    }

    private void OnComboDropDownOpened(object? sender, EventArgs e) =>
        DropDownOpened?.Invoke(this, EventArgs.Empty);
}
