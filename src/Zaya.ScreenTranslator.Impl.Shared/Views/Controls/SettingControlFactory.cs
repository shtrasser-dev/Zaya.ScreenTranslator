using System.Collections;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

public static class SettingControlFactory
{
    /// <summary>Centered settings column width (Cursor-like).</summary>
    public const double SettingsColumnWidth = 700;

    /// <summary>Right-side editor width for scalar settings.</summary>
    public const double ControlWidth = 240;

    /// <summary>Wider editors (paths, long strings).</summary>
    public const double ControlWidthWide = 320;

    public static Control CreateControl(
        SettingDescriptor descriptor,
        object? currentValue,
        IReadOnlyDictionary<string, object?> allSettings,
        Action<string, object?> onChanged,
        CultureInfo culture)
    {
        var displayName = descriptor.DisplayName.GetValue(culture);
        var description = descriptor.Description?.GetValue(culture);

        if (descriptor is TableSettingDescriptor tableDesc)
        {
            var vertical = new StackPanel
            {
                Spacing = 8,
                Margin = new Avalonia.Thickness(0, 10, 0, 10),
            };
            var title = new TextBlock
            {
                Text = displayName,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(title, description ?? displayName);
            vertical.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(description))
            {
                vertical.Children.Add(new TextBlock
                {
                    Text = description,
                    FontSize = 12,
                    Opacity = 0.65,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(0, -4, 0, 0),
                });
            }

            vertical.Children.Add(CreateTableEditor(tableDesc, currentValue, onChanged, culture));
            return WithDivider(vertical);
        }

        var control = CreateEditor(descriptor, currentValue, allSettings, onChanged, culture);
        return WithDivider(CreateSettingsRow(displayName, description, control));
    }

    /// <summary>Thin horizontal divider between setting rows.</summary>
    public static Border CreateDivider()
    {
        var divider = new Border
        {
            Height = 1,
            Opacity = 0.18,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        divider.Classes.Add("settings-divider");
        return divider;
    }

    private static Control WithDivider(Control content)
    {
        var stack = new StackPanel();
        stack.Children.Add(content);
        stack.Children.Add(CreateDivider());
        return stack;
    }

    /// <summary>Cursor-style row: label on the left, control on the right.</summary>
    public static Grid CreateSettingsRow(string displayName, string? description, Control? control)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(0, 10),
        };
        row.Classes.Add("settings-row");

        var labelStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 16, 0),
        };

        var label = new TextBlock
        {
            Text = displayName,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        ToolTip.SetTip(label, description ?? displayName);
        labelStack.Children.Add(label);

        if (!string.IsNullOrWhiteSpace(description))
        {
            labelStack.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        row.Children.Add(labelStack);

        if (control is not null)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            control.HorizontalAlignment = HorizontalAlignment.Right;
            if (control is not CheckBox && (double.IsNaN(control.Width) || control.Width <= 0))
                control.Width = ControlWidth;

            Grid.SetColumn(control, 1);
            row.Children.Add(control);
        }

        return row;
    }

    private static Control? CreateEditor(
        SettingDescriptor descriptor,
        object? currentValue,
        IReadOnlyDictionary<string, object?> allSettings,
        Action<string, object?> onChanged,
        CultureInfo culture,
        bool tableCell = false)
    {
        switch (descriptor)
        {
            case EnumSettingDescriptor enumDesc:
                return CreateEnumEditor(enumDesc, currentValue, onChanged, culture);

            case BooleanSettingDescriptor boolDesc:
                return CreateBoolEditor(boolDesc, currentValue, onChanged, tableCell);

            case IntegerSettingDescriptor intDesc:
                return CreateIntegerEditor(intDesc, currentValue, onChanged);

            case StringSettingDescriptor stringDesc:
                return CreateTextBoxEditor(
                    ResolveStringValue(currentValue, stringDesc.DefaultValue), onChanged, ControlWidth);

            case UrlSettingDescriptor urlDesc:
                return CreateTextBoxEditor(
                    ResolveStringValue(currentValue, urlDesc.DefaultValue), onChanged, ControlWidth);

            case DirectoryPathSettingDescriptor dirDesc:
                return CreateDirectoryEditor(ResolveStringValue(currentValue, dirDesc.DefaultValue), onChanged);

            case FilePathSettingDescriptor fileDesc:
                return CreateFileEditor(fileDesc, ResolveStringValue(currentValue, fileDesc.DefaultValue), onChanged);

            case PasswordSettingDescriptor passwordDesc:
                return CreatePasswordEditor(ResolveStringValue(currentValue, passwordDesc.DefaultValue), onChanged);

            case TableSettingDescriptor:
                return null;

            default:
                return new TextBox
                {
                    Text = currentValue?.ToString() ?? string.Empty,
                    IsReadOnly = true,
                    Width = ControlWidth,
                };
        }
    }

    private static Control CreateTableEditor(
        TableSettingDescriptor tableDesc,
        object? currentValue,
        Action<string, object?> onChanged,
        CultureInfo culture)
    {
        var rows = NormalizeMutableRows(currentValue);
        var root = new StackPanel { Spacing = 6 };
        var rowsPanel = new StackPanel { Spacing = 4 };
        Grid.SetIsSharedSizeScope(rowsPanel, true);
        root.Children.Add(rowsPanel);

        void Persist() => onChanged(tableDesc.Key, rows);

        void RebuildRows()
        {
            rowsPanel.Children.Clear();

            // Single definition template so header and data rows share the same column geometry.
            var columnDefs = BuildColumnDefinitions(tableDesc.Columns);

            var header = new Grid { ColumnDefinitions = CloneColumnDefinitions(columnDefs) };
            for (var c = 0; c < tableDesc.Columns.Count; c++)
            {
                var col = tableDesc.Columns[c];
                var isBool = col is BooleanSettingDescriptor;
                var hdr = new TextBlock
                {
                    Text = col.DisplayName.GetValue(culture),
                    FontSize = 11,
                    Opacity = 0.7,
                    Margin = CellMargin(isBool),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(hdr, c);
                header.Children.Add(hdr);
            }

            // Placeholder so the delete column width matches data rows (keeps header/cells aligned).
            var headerDeleteSpacer = new Border
            {
                MinWidth = TableDeleteButtonWidth,
                Margin = CellMargin(isBool: false),
            };
            Grid.SetColumn(headerDeleteSpacer, tableDesc.Columns.Count);
            header.Children.Add(headerDeleteSpacer);
            rowsPanel.Children.Add(header);

            for (var i = 0; i < rows.Count; i++)
            {
                var index = i;
                var row = rows[i];
                var rowGrid = new Grid
                {
                    ColumnDefinitions = CloneColumnDefinitions(columnDefs),
                };

                for (var c = 0; c < tableDesc.Columns.Count; c++)
                {
                    var col = tableDesc.Columns[c];
                    row.TryGetValue(col.Key, out var cellValue);
                    var emptySettings = new Dictionary<string, object?>();
                    var editor = CreateEditor(
                        col,
                        cellValue,
                        emptySettings,
                        (key, val) =>
                        {
                            var cellKey = string.IsNullOrEmpty(key) ? col.Key : key;
                            if (val is null)
                                row.Remove(cellKey);
                            else
                                row[cellKey] = val;
                            Persist();
                        },
                        culture,
                        tableCell: true);

                    if (editor is not null)
                    {
                        var isBool = col is BooleanSettingDescriptor;
                        editor.Margin = CellMargin(isBool);
                        editor.Width = double.NaN;
                        editor.MinWidth = 0;
                        if (isBool)
                        {
                            editor.HorizontalAlignment = HorizontalAlignment.Center;
                            editor.VerticalAlignment = VerticalAlignment.Center;
                        }
                        else
                        {
                            editor.HorizontalAlignment = HorizontalAlignment.Stretch;
                        }

                        Grid.SetColumn(editor, c);
                        rowGrid.Children.Add(editor);
                    }
                }

                var removeBtn = new Button
                {
                    Content = "×",
                    Width = TableDeleteButtonWidth,
                    MinWidth = TableDeleteButtonWidth,
                    Padding = new Avalonia.Thickness(6, 2),
                    Margin = CellMargin(isBool: false),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                ToolTip.SetTip(removeBtn, LocalizationService.Instance["Filter_RemoveRule"]);
                removeBtn.Click += (_, _) =>
                {
                    if (index < 0 || index >= rows.Count)
                        return;
                    rows.RemoveAt(index);
                    Persist();
                    RebuildRows();
                };
                Grid.SetColumn(removeBtn, tableDesc.Columns.Count);
                rowGrid.Children.Add(removeBtn);

                rowsPanel.Children.Add(rowGrid);
            }
        }

        var addBtn = new Button
        {
            Content = LocalizationService.Instance["Filter_AddRule"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
        };
        addBtn.Click += (_, _) =>
        {
            rows.Add(tableDesc.CreateDefaultRow());
            Persist();
            RebuildRows();
        };

        root.Children.Add(addBtn);
        RebuildRows();
        return root;
    }

    private const double TableBoolColumnWidth = 96;
    private const double TableDeleteButtonWidth = 32;
    private const string TableBoolSharedSizeGroup = "TableBool";
    private const string TableDeleteSharedSizeGroup = "TableDelete";

    private static Avalonia.Thickness CellMargin(bool isBool)
        => new(0, 0, isBool ? 4 : 8, 0);

    private static ColumnDefinitions BuildColumnDefinitions(IReadOnlyList<SettingDescriptor> columns)
    {
        var defs = new ColumnDefinitions();
        foreach (var col in columns)
        {
            if (col is BooleanSettingDescriptor)
            {
                // Fixed width (~2× previous Auto/content width); does not grow with the window.
                defs.Add(new ColumnDefinition(new GridLength(TableBoolColumnWidth))
                {
                    SharedSizeGroup = TableBoolSharedSizeGroup,
                });
            }
            else
            {
                defs.Add(new ColumnDefinition(GridLength.Star));
            }
        }

        defs.Add(new ColumnDefinition(GridLength.Auto)
        {
            SharedSizeGroup = TableDeleteSharedSizeGroup,
            MinWidth = TableDeleteButtonWidth,
        });
        return defs;
    }

    private static ColumnDefinitions CloneColumnDefinitions(ColumnDefinitions source)
    {
        var defs = new ColumnDefinitions();
        foreach (var col in source)
        {
            defs.Add(new ColumnDefinition(col.Width)
            {
                SharedSizeGroup = col.SharedSizeGroup,
                MinWidth = col.MinWidth,
                MaxWidth = col.MaxWidth,
            });
        }

        return defs;
    }

    private static List<Dictionary<string, object>> NormalizeMutableRows(object? currentValue)
    {
        var rows = new List<Dictionary<string, object>>();
        if (currentValue is null)
            return rows;

        if (currentValue is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (item is Dictionary<string, object> dict)
                {
                    rows.Add(new Dictionary<string, object>(dict));
                    continue;
                }

                if (item is IReadOnlyDictionary<string, object> iro)
                {
                    rows.Add(new Dictionary<string, object>(iro));
                    continue;
                }

                if (item is IDictionary legacy)
                {
                    var converted = new Dictionary<string, object>();
                    foreach (DictionaryEntry entry in legacy)
                    {
                        if (entry.Key is string k && entry.Value is not null)
                            converted[k] = entry.Value;
                    }
                    rows.Add(converted);
                }
            }
        }

        return rows;
    }

    /// <summary>
    /// Uses the stored value when present; otherwise falls back to the descriptor default.
    /// Empty/whitespace stored values are treated as unset so defaults still show in UI.
    /// </summary>
    private static object? ResolveStringValue(object? currentValue, string? defaultValue)
    {
        if (currentValue is string s)
            return string.IsNullOrWhiteSpace(s) ? defaultValue : s;
        return currentValue ?? defaultValue;
    }

    private static Control CreateEnumEditor(
        EnumSettingDescriptor desc, object? currentValue,
        Action<string, object?> onChanged, CultureInfo culture)
    {
        var combo = new ComboBox { Width = ControlWidth };

        var items = desc.Options.Select(o => new EnumItem
        {
            Value = o.Value,
            Display = o.DisplayName.GetValue(culture)
        }).ToList();

        combo.ItemsSource = items;
        combo.DisplayMemberBinding = new Avalonia.Data.Binding("Display");

        var currentStr = currentValue?.ToString();
        var selected = items.FirstOrDefault(i => i.Value == currentStr)
                       ?? items.FirstOrDefault(i => i.Value == desc.DefaultValue)
                       ?? items.FirstOrDefault();

        combo.SelectedItem = selected;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is EnumItem item)
                onChanged(desc.Key, item.Value);
        };

        return combo;
    }

    private static Control CreateBoolEditor(
        BooleanSettingDescriptor desc, object? currentValue, Action<string, object?> onChanged,
        bool tableCell = false)
    {
        var check = new CheckBox();
        if (tableCell)
        {
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
        }

        check.IsChecked = currentValue is bool b ? b : desc.DefaultValue;
        check.IsCheckedChanged += (_, _) =>
            onChanged(desc.Key, check.IsChecked == true);
        return check;
    }

    private static Control CreateIntegerEditor(
        IntegerSettingDescriptor desc, object? currentValue,
        Action<string, object?> onChanged)
    {
        var tb = new TextBox
        {
            Text = currentValue is int i
                ? i.ToString()
                : desc.DefaultValue?.ToString() ?? "0",
            Width = ControlWidth,
        };
        tb.TextChanged += (_, _) =>
        {
            if (int.TryParse(tb.Text, out var val))
                onChanged(desc.Key, val);
        };
        return tb;
    }

    private static Control CreateTextBoxEditor(
        object? currentValue, Action<string, object?> onChanged, double width)
    {
        var tb = new TextBox
        {
            Text = currentValue?.ToString() ?? string.Empty,
            Width = width,
        };
        tb.TextChanged += (_, _) => onChanged(string.Empty, tb.Text ?? string.Empty);
        return tb;
    }

    private static Control CreateDirectoryEditor(object? currentValue, Action<string, object?> onChanged)
    {
        var tb = new TextBox
        {
            Text = currentValue?.ToString() ?? string.Empty,
            Width = ControlWidthWide,
        };
        tb.TextChanged += (_, _) => onChanged(string.Empty, tb.Text ?? string.Empty);
        return tb;
    }

    private static Control CreateFileEditor(
        FilePathSettingDescriptor desc, object? currentValue,
        Action<string, object?> onChanged)
    {
        var tb = new TextBox
        {
            Text = currentValue?.ToString() ?? string.Empty,
            Width = ControlWidthWide,
        };
        tb.TextChanged += (_, _) => onChanged(desc.Key, tb.Text ?? string.Empty);
        return tb;
    }

    private static Control CreatePasswordEditor(object? currentValue, Action<string, object?> onChanged)
    {
        var tb = new TextBox
        {
            Text = currentValue?.ToString() ?? string.Empty,
            Width = ControlWidthWide,
            PasswordChar = '•',
        };
        tb.TextChanged += (_, _) => onChanged(string.Empty, tb.Text ?? string.Empty);
        return tb;
    }

    private sealed record EnumItem
    {
        public required string Value { get; init; }
        public required string Display { get; init; }
    }
}
