using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

public static class SettingControlFactory
{
    /// <summary>Left nav / settings tab strip width (kept in sync across main + settings).</summary>
    public const double MainNavWidth = 180;

    /// <summary>Max centered settings column width (shrinks with the window).</summary>
    public const double SettingsColumnWidth = 700;

    /// <summary>Right-side editor width for scalar settings.</summary>
    public const double ControlWidth = 240;

    /// <summary>Font size for setting name labels.</summary>
    public const double SettingsLabelFontSize = 16;

    /// <summary>Wider editors (paths, long strings).</summary>
    public const double ControlWidthWide = 320;

    public static Control CreateControl(
        SettingDescriptor descriptor,
        object? currentValue,
        IReadOnlyDictionary<string, object?> allSettings,
        Action<string, object?> onChanged,
        CultureInfo culture,
        ILocalizationService localizationService,
        bool includeTrailingDivider = true)
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
                FontSize = SettingsLabelFontSize,
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

            vertical.Children.Add(SettingTableEditor.Create(tableDesc, currentValue, onChanged, culture, localizationService));
            return includeTrailingDivider ? WithDivider(vertical) : vertical;
        }

        var control = SettingEditorFactory.CreateEditor(descriptor, currentValue, allSettings, onChanged, culture, localizationService);
        var row = CreateSettingsRow(displayName, description, control);
        return includeTrailingDivider ? WithDivider(row) : row;
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

    /// <summary>
    /// Shows dividers under every visible setting except the last visible one.
    /// Each child produced by <see cref="CreateControl"/> is expected to be a panel of [content, divider].
    /// </summary>
    public static void SyncTrailingDividers(Panel root)
    {
        Control? lastVisible = null;
        foreach (var child in root.Children)
        {
            if (child is not Control control || !control.IsVisible)
                continue;

            if (TryGetTrailingDivider(control, out var divider))
                divider.IsVisible = true;

            lastVisible = control;
        }

        if (lastVisible is not null && TryGetTrailingDivider(lastVisible, out var lastDivider))
            lastDivider.IsVisible = false;
    }

    private static bool TryGetTrailingDivider(Control control, out Border divider)
    {
        divider = null!;
        if (control is not Panel { Children.Count: >= 2 } panel)
            return false;
        if (panel.Children[^1] is not Border border || !border.Classes.Contains("settings-divider"))
            return false;
        divider = border;
        return true;
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
            FontSize = SettingsLabelFontSize,
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
            if (control is not CheckBox and not ToggleSwitch
                && (double.IsNaN(control.Width) || control.Width <= 0))
                control.Width = ControlWidth;

            Grid.SetColumn(control, 1);
            row.Children.Add(control);
        }

        return row;
    }
}
