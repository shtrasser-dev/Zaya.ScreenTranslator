using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Zaya.Primitives;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>Creates scalar setting editors (enum, bool, text, paths, …).</summary>
internal static class SettingEditorFactory
{
    internal const double TableBoolIconSize = 20;

    public static Control? CreateEditor(
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
                return CreateBoolEditor(boolDesc, currentValue, onChanged, culture, tableCell);

            case IntegerSettingDescriptor intDesc:
                return CreateIntegerEditor(intDesc, currentValue, onChanged);

            case StringSettingDescriptor stringDesc:
                return CreateTextBoxEditor(
                    ResolveStringValue(currentValue, stringDesc.DefaultValue),
                    onChanged,
                    SettingControlFactory.ControlWidth);

            case UrlSettingDescriptor urlDesc:
                return CreateTextBoxEditor(
                    ResolveStringValue(currentValue, urlDesc.DefaultValue),
                    onChanged,
                    SettingControlFactory.ControlWidth);

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
                    Width = SettingControlFactory.ControlWidth,
                };
        }
    }

    /// <summary>
    /// Uses the stored value when present; otherwise falls back to the descriptor default.
    /// Empty/whitespace stored values are treated as unset so defaults still show in UI.
    /// </summary>
    public static object? ResolveStringValue(object? currentValue, string? defaultValue)
    {
        if (currentValue is string s)
            return string.IsNullOrWhiteSpace(s) ? defaultValue : s;
        return currentValue ?? defaultValue;
    }

    private static Control CreateEnumEditor(
        EnumSettingDescriptor desc, object? currentValue,
        Action<string, object?> onChanged, CultureInfo culture)
    {
        var combo = new ComboBox { Width = SettingControlFactory.ControlWidth };

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
        CultureInfo culture, bool tableCell = false)
    {
        if (tableCell && !string.IsNullOrWhiteSpace(desc.IconSvg))
            return CreateSvgBoolToggle(desc, currentValue, onChanged, culture);

        var toggle = new ToggleSwitch
        {
            OnContent = string.Empty,
            OffContent = string.Empty,
            IsChecked = currentValue is bool b ? b : desc.DefaultValue,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = tableCell ? HorizontalAlignment.Center : HorizontalAlignment.Right,
        };
        toggle.IsCheckedChanged += (_, _) =>
            onChanged(desc.Key, toggle.IsChecked == true);
        return toggle;
    }

    private static Control CreateSvgBoolToggle(
        BooleanSettingDescriptor desc,
        object? currentValue,
        Action<string, object?> onChanged,
        CultureInfo culture)
    {
        var value = currentValue is bool b ? b : desc.DefaultValue;
        var image = SettingSvgIconHelper.CreateIconImage(desc.IconSvg!, value, TableBoolIconSize);
        var host = new Border
        {
            Child = image,
            Background = Brushes.Transparent,
            Padding = new Avalonia.Thickness(2, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(host, desc.DisplayName.GetValue(culture));

        void ApplyVisual()
        {
            image.Source = SettingSvgIconHelper.CreateImageSource(desc.IconSvg!, value);
        }

        host.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
                return;
            value = !value;
            ApplyVisual();
            onChanged(desc.Key, value);
            e.Handled = true;
        };

        return host;
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
            Width = SettingControlFactory.ControlWidth,
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
            Width = SettingControlFactory.ControlWidthWide,
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
            Width = SettingControlFactory.ControlWidthWide,
        };
        tb.TextChanged += (_, _) => onChanged(desc.Key, tb.Text ?? string.Empty);
        return tb;
    }

    private static Control CreatePasswordEditor(object? currentValue, Action<string, object?> onChanged)
    {
        var tb = new TextBox
        {
            Text = currentValue?.ToString() ?? string.Empty,
            Width = SettingControlFactory.ControlWidthWide,
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
