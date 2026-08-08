using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>
/// ComboBox with top action rows, item selection, and click-to-rename on the value area
/// (chevron stays pick-only; rename overlay leaves the arrow visible).
/// </summary>
public partial class ActionEditableComboBox : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ActionsProperty =
        AvaloniaProperty.Register<ActionEditableComboBox, IEnumerable?>(nameof(Actions));

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ActionEditableComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<ActionEditableComboBox, object?>(
            nameof(SelectedItem),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsRenameEnabledProperty =
        AvaloniaProperty.Register<ActionEditableComboBox, bool>(nameof(IsRenameEnabled), defaultValue: true);

    /// <summary>Right-side chevron hit zone (Fluent ComboBox has no separate Button part).</summary>
    private const double DropDownHitWidth = 34;

    private bool _wired;
    private bool _editMode;
    private bool _exitingEditMode;
    private bool _blockEditEntry;
    private int _editBlockGeneration;
    private bool _syncingSelection;
    private INotifyCollectionChanged? _itemsCollection;
    private INotifyCollectionChanged? _actionsCollection;

    public ActionEditableComboBox()
    {
        InitializeComponent();
        Loaded += OnControlLoaded;
    }

    private void OnControlLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureWired();
        RebuildEntries();
    }

    private void EnsureWired()
    {
        if (_wired)
            return;

        _wired = true;
        Combo.ContainerPrepared += OnContainerPrepared;
        Combo.DropDownOpened += (_, _) => _blockEditEntry = true;
        Combo.DropDownClosed += (_, _) => BlockEditEntryTemporarily();
        Combo.SelectionChanged += OnComboSelectionChanged;
        Combo.AddHandler(
            InputElement.PointerPressedEvent,
            OnComboPointerPressed,
            RoutingStrategies.Tunnel);

        RenameBox.LostFocus += OnRenameBoxLostFocus;
        RenameBox.KeyDown += OnRenameBoxKeyDown;

        AddHandler(
            InputElement.PointerPressedEvent,
            OnRootPointerPressedWhileEditing,
            RoutingStrategies.Tunnel);
    }

    public IEnumerable? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public bool IsRenameEnabled
    {
        get => GetValue(IsRenameEnabledProperty);
        set => SetValue(IsRenameEnabledProperty, value);
    }

    public event EventHandler<ActionEditableComboBoxActionEventArgs>? ActionInvoked;
    public event EventHandler<ActionEditableComboBoxItemEventArgs>? ItemSelected;
    public event EventHandler<ActionEditableComboBoxRenameEventArgs>? RenameRequested;
    public event EventHandler? RenameCancelled;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ActionsProperty)
        {
            HookCollection(ref _actionsCollection, change.OldValue as IEnumerable, change.NewValue as IEnumerable);
            if (_wired)
                RebuildEntries();
        }
        else if (change.Property == ItemsSourceProperty)
        {
            HookCollection(ref _itemsCollection, change.OldValue as IEnumerable, change.NewValue as IEnumerable);
            if (_wired)
                RebuildEntries();
        }
        else if (change.Property == SelectedItemProperty && _wired)
        {
            SyncComboToSelectedItem();
        }
    }

    private void HookCollection(
        ref INotifyCollectionChanged? field,
        IEnumerable? oldValue,
        IEnumerable? newValue)
    {
        if (field is not null)
            field.CollectionChanged -= OnSourceCollectionChanged;

        field = newValue as INotifyCollectionChanged;
        if (field is not null)
            field.CollectionChanged += OnSourceCollectionChanged;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_wired)
            RebuildEntries();
    }

    private void RebuildEntries()
    {
        var entries = new List<ActionEditableComboBoxEntry>();

        foreach (var action in EnumerateActions())
        {
            entries.Add(new ActionEditableComboBoxEntry
            {
                Display = action.Header,
                ActionId = action.Id,
            });
        }

        if (entries.Count > 0)
            entries.Add(ActionEditableComboBoxEntry.Separator);

        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is null)
                    continue;

                entries.Add(new ActionEditableComboBoxEntry
                {
                    Display = item.ToString() ?? string.Empty,
                    Item = item,
                });
            }
        }

        _syncingSelection = true;
        try
        {
            Combo.ItemsSource = entries;
            SyncComboToSelectedItem();
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private IEnumerable<ActionEditableComboBoxAction> EnumerateActions()
    {
        if (Actions is null)
            yield break;

        foreach (var value in Actions)
        {
            if (value is ActionEditableComboBoxAction action)
                yield return action;
        }
    }

    private void SyncComboToSelectedItem()
    {
        if (Combo.ItemsSource is not IList entries)
            return;

        ActionEditableComboBoxEntry? match = null;
        var selected = SelectedItem;
        foreach (var entryObj in entries)
        {
            if (entryObj is ActionEditableComboBoxEntry { IsSeparator: false, IsAction: false } entry
                && Equals(entry.Item, selected))
            {
                match = entry;
                break;
            }
        }

        _syncingSelection = true;
        try
        {
            Combo.SelectedItem = match;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private void BlockEditEntryTemporarily()
    {
        _blockEditEntry = true;
        var generation = ++_editBlockGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (generation == _editBlockGeneration)
                _blockEditEntry = false;
        }, DispatcherPriority.Background);
    }

    private static void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is not ComboBoxItem container)
            return;

        container.ClearValue(InputElement.IsEnabledProperty);
        container.ClearValue(InputElement.FocusableProperty);
        container.ClearValue(Decorator.PaddingProperty);

        if (container.DataContext is ActionEditableComboBoxEntry { IsSeparator: true })
        {
            container.IsEnabled = false;
            container.Focusable = false;
        }
    }

    private void OnComboPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsRenameEnabled || _editMode || _blockEditEntry)
            return;

        if (Combo.IsDropDownOpen || IsDropDownZoneHit(e))
            return;

        e.Handled = true;
        EnterEditMode();
    }

    private void OnRootPointerPressedWhileEditing(object? sender, PointerPressedEventArgs e)
    {
        if (!_editMode)
            return;

        if (IsVisualInside(RenameBox, e.Source as Visual))
            return;

        var openDropDown = IsVisualInside(Combo, e.Source as Visual) && IsDropDownZoneHit(e);
        ExitEditMode(commit: true);

        if (openDropDown)
        {
            Dispatcher.UIThread.Post(
                () => Combo.IsDropDownOpen = true,
                DispatcherPriority.Input);
        }
    }

    private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || Combo is not { IsDropDownOpen: true })
            return;

        if (Combo.SelectedItem is not ActionEditableComboBoxEntry entry || entry.IsSeparator)
        {
            SyncComboToSelectedItem();
            return;
        }

        _blockEditEntry = true;
        if (_editMode)
            ExitEditMode(commit: false);

        Combo.IsDropDownOpen = false;

        if (entry.IsAction && entry.ActionId is not null)
        {
            SyncComboToSelectedItem();
            ActionInvoked?.Invoke(this, new ActionEditableComboBoxActionEventArgs(entry.ActionId));
        }
        else if (entry.Item is not null)
        {
            if (!Equals(SelectedItem, entry.Item))
                SelectedItem = entry.Item;

            ItemSelected?.Invoke(this, new ActionEditableComboBoxItemEventArgs(entry.Item));
        }

        SyncComboToSelectedItem();
        TopLevel.GetTopLevel(this)?.Focus();

        BlockEditEntryTemporarily();
        Dispatcher.UIThread.Post(() =>
        {
            SyncComboToSelectedItem();
            if (_editMode)
                ExitEditMode(commit: false);
            TopLevel.GetTopLevel(this)?.Focus();
        }, DispatcherPriority.Background);
    }

    private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_editMode || _exitingEditMode)
            return;

        ExitEditMode(commit: true);
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_editMode)
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ExitEditMode(commit: true);
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ExitEditMode(commit: false);
        }
    }

    private void EnterEditMode()
    {
        _editMode = true;
        Combo.IsDropDownOpen = false;
        RenameBox.Text = SelectedItem?.ToString() ?? string.Empty;
        RenameBox.IsVisible = true;

        Dispatcher.UIThread.Post(() =>
        {
            if (!_editMode)
                return;

            RenameBox.Focus();
            RenameBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void ExitEditMode(bool commit)
    {
        if (!_editMode || _exitingEditMode)
            return;

        _exitingEditMode = true;
        try
        {
            if (commit)
                RenameRequested?.Invoke(this, new ActionEditableComboBoxRenameEventArgs(RenameBox.Text ?? string.Empty));
            else
                RenameCancelled?.Invoke(this, EventArgs.Empty);

            _editMode = false;
            RenameBox.IsVisible = false;
            SyncComboToSelectedItem();
            TopLevel.GetTopLevel(this)?.Focus();
        }
        finally
        {
            _exitingEditMode = false;
        }
    }

    private bool IsDropDownZoneHit(PointerPressedEventArgs e)
    {
        var width = Combo.Bounds.Width;
        if (width <= 0)
            return false;

        return e.GetPosition(Combo).X >= width - DropDownHitWidth;
    }

    private static bool IsVisualInside(Visual root, Visual? node)
    {
        for (var current = node; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, root))
                return true;
        }

        return false;
    }
}
