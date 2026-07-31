using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>Builds table setting editors (filter rules, …).</summary>
internal static class SettingTableEditor
{
    private const double TableHeaderIconSize = 14;
    private const double TableColumnSpacing = 8;
    private const double TableRowDisabledOpacity = 0.6;
    private const string TableRowEnabledKey = "enabled";

    public static Control Create(
        TableSettingDescriptor tableDesc,
        object? currentValue,
        Action<string, object?> onChanged,
        CultureInfo culture)
    {
        var rows = SettingTableLayout.NormalizeMutableRows(currentValue);
        var root = new StackPanel { Spacing = 6 };
        var rowsPanel = new StackPanel { Spacing = 4 };
        Grid.SetIsSharedSizeScope(rowsPanel, true);
        root.Children.Add(rowsPanel);

        void Persist() => onChanged(tableDesc.Key, rows);

        void RebuildRows()
        {
            rowsPanel.Children.Clear();

            var columnDefs = SettingTableLayout.BuildColumnDefinitions(tableDesc.Columns);

            var header = new Grid
            {
                ColumnDefinitions = SettingTableLayout.CloneColumnDefinitions(columnDefs),
                ColumnSpacing = TableColumnSpacing,
            };
            for (var c = 0; c < tableDesc.Columns.Count; c++)
            {
                var col = tableDesc.Columns[c];
                var isBool = col is BooleanSettingDescriptor;
                var hdr = CreateTableColumnHeader(col, culture, isBool);
                Grid.SetColumn(hdr, c);
                header.Children.Add(hdr);
            }

            var headerDeleteSpacer = new Border
            {
                MinWidth = SettingTableLayout.DeleteButtonWidth,
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
                    ColumnDefinitions = SettingTableLayout.CloneColumnDefinitions(columnDefs),
                    ColumnSpacing = TableColumnSpacing,
                };
                ApplyTableRowEnabledOpacity(rowGrid, row, tableDesc);

                for (var c = 0; c < tableDesc.Columns.Count; c++)
                {
                    var col = tableDesc.Columns[c];
                    row.TryGetValue(col.Key, out var cellValue);
                    var emptySettings = new Dictionary<string, object?>();
                    var editor = SettingEditorFactory.CreateEditor(
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
                            if (IsTableRowEnabledColumn(col))
                                ApplyTableRowEnabledOpacity(rowGrid, row, tableDesc);
                        },
                        culture,
                        tableCell: true);

                    if (editor is not null)
                    {
                        var isBool = col is BooleanSettingDescriptor;
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
                    Width = SettingTableLayout.DeleteButtonWidth,
                    MinWidth = SettingTableLayout.DeleteButtonWidth,
                    Padding = new Avalonia.Thickness(6, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                ToolTip.SetTip(removeBtn, LocalizationService.Instance[LocalizationConstants.Filter.RemoveRule]);
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
            Content = LocalizationService.Instance[LocalizationConstants.Filter.AddRule],
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

    private static bool IsTableRowEnabledColumn(SettingDescriptor col)
        => col is BooleanSettingDescriptor
           && col.Key.Equals(TableRowEnabledKey, StringComparison.OrdinalIgnoreCase);

    private static bool IsTableRowEnabled(
        IReadOnlyDictionary<string, object> row,
        TableSettingDescriptor tableDesc)
    {
        var enabledCol = tableDesc.Columns
            .OfType<BooleanSettingDescriptor>()
            .FirstOrDefault(c => c.Key.Equals(TableRowEnabledKey, StringComparison.OrdinalIgnoreCase));
        if (enabledCol is null)
            return true;

        if (row.TryGetValue(enabledCol.Key, out var value) && value is bool b)
            return b;

        return enabledCol.DefaultValue;
    }

    private static void ApplyTableRowEnabledOpacity(
        Control rowControl,
        IReadOnlyDictionary<string, object> row,
        TableSettingDescriptor tableDesc)
    {
        rowControl.Opacity = IsTableRowEnabled(row, tableDesc) ? 1.0 : TableRowDisabledOpacity;
    }

    private static Control CreateTableColumnHeader(SettingDescriptor col, CultureInfo culture, bool isBool)
    {
        if (isBool && !string.IsNullOrWhiteSpace(col.IconSvg))
        {
            return new Border
            {
                Height = SettingEditorFactory.TableBoolIconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var displayName = col.DisplayName.GetValue(culture);
        Control content;
        if (!string.IsNullOrWhiteSpace(col.IconSvg))
        {
            content = SettingSvgIconHelper.CreateIconImage(col.IconSvg, checkedState: false, TableHeaderIconSize);
            content.Opacity = 0.75;
            content.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            content = new TextBlock
            {
                Text = displayName,
                FontSize = 11,
                Opacity = 0.7,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
        }

        content.VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(content, displayName);
        return content;
    }
}
