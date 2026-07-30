using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;
using Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private string? _saveAsNewResult;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        viewModel.OwnerWindow = this;
        viewModel.CloseRequested += OnCloseRequested;
        viewModel.SaveAsNewPrompt = ShowSaveAsNewDialog;

        RebuildPanel("OcrSettingsPanel", _viewModel.OcrDescriptors,
            () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        RebuildPanel("CaptureSettingsPanel", _viewModel.CaptureDescriptors,
            () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
        RebuildPanel("TextLayoutSettingsPanel", _viewModel.TextLayoutDescriptors,
            () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        RebuildPanel("TranslatorSettingsPanel", _viewModel.TranslatorDescriptors,
            () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
        RebuildPanel("OverlaySettingsPanel", _viewModel.OverlayLayoutDescriptors,
            () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
        RebuildPanel("FilterSettingsPanel", ScreenTranslatorSettingDescriptors.FilterDescriptors,
            () => ScreenTranslatorSettingDescriptors.StKey);

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.OcrDescriptors))
                RebuildPanel("OcrSettingsPanel", _viewModel.OcrDescriptors,
                    () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
            if (e.PropertyName == nameof(SettingsViewModel.CaptureDescriptors))
                RebuildPanel("CaptureSettingsPanel", _viewModel.CaptureDescriptors,
                    () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
            if (e.PropertyName == nameof(SettingsViewModel.TextLayoutDescriptors))
                RebuildPanel("TextLayoutSettingsPanel", _viewModel.TextLayoutDescriptors,
                    () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
            if (e.PropertyName == nameof(SettingsViewModel.TranslatorDescriptors))
                RebuildPanel("TranslatorSettingsPanel", _viewModel.TranslatorDescriptors,
                    () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
            if (e.PropertyName == nameof(SettingsViewModel.OverlayLayoutDescriptors))
                RebuildPanel("OverlaySettingsPanel", _viewModel.OverlayLayoutDescriptors,
                    () => _viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
        };
    }

    private async Task OnCloseRequested()
    {
        Close();
        await Task.CompletedTask;
    }

    private void RebuildPanel(string panelName,
        IReadOnlyList<SettingDescriptor>? descriptors,
        Func<string> getPluginId)
    {
        var panel = this.FindControl<StackPanel>(panelName);
        if (panel is null || descriptors is null)
            return;

        panel.Children.Clear();

        var stSettings = _viewModel.EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey];

        var pluginId = getPluginId();
        if (!_viewModel.EditingProfile.Settings.TryGetValue(pluginId, out var pluginSettings))
        {
            pluginSettings = new Dictionary<string, object>();
            _viewModel.EditingProfile.Settings[pluginId] = pluginSettings;
        }

        var allSettings = MergeSettings(stSettings, pluginSettings);

        foreach (var desc in descriptors)
        {
            if (ManagedSettingKeys.IsHostManaged(desc))
                continue;

            var currentValue = pluginSettings.TryGetValue(desc.Key, out var val) ? val : null;

            var control = SettingControlFactory.CreateControl(
                desc, currentValue, allSettings,
                (_, newVal) =>
                {
                    if (newVal is null)
                        pluginSettings.Remove(desc.Key);
                    else
                        pluginSettings[desc.Key] = newVal;
                    UpdateVisibility(panelName, getPluginId);
                },
                _viewModel.Loc.CurrentCulture);

            control.Tag = desc;
            control.IsVisible = desc.IsVisible.Invoke(allSettings);
            panel.Children.Add(control);
        }
    }

    private void UpdateVisibility(string panelName, Func<string> getPluginId)
    {
        var panel = this.FindControl<StackPanel>(panelName);
        if (panel is null)
            return;

        var stSettings = _viewModel.EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey];
        var pluginSettings = _viewModel.EditingProfile.Settings.TryGetValue(getPluginId(), out var ps)
            ? ps
            : new Dictionary<string, object>();

        var allSettings = MergeSettings(stSettings, pluginSettings);

        foreach (Control child in panel.Children)
        {
            if (child.Tag is SettingDescriptor desc)
            {
                bool visible = desc.IsVisible?.Invoke(allSettings) ?? true;
                child.IsVisible = visible;
            }
        }
    }

    private static IReadOnlyDictionary<string, object?> MergeSettings(
        Dictionary<string, object> stSettings,
        Dictionary<string, object> pluginSettings)
    {
        var merged = new Dictionary<string, object?>(stSettings.Count + pluginSettings.Count);
        foreach (var kv in stSettings)
            merged[kv.Key] = kv.Value;
        foreach (var kv in pluginSettings)
            merged[kv.Key] = kv.Value;
        return merged;
    }

    private async Task<string?> ShowSaveAsNewDialog()
    {
        var loc = _viewModel.Loc;
        var existingNames = _viewModel.ProfileNames;

        var nameBox = new TextBox { PlaceholderText = loc["SaveAsNew_Title"], Width = 250 };
        var errorLabel = new TextBlock { Foreground = Brushes.Red, IsVisible = false };

        var okBtn = new Button { Content = loc["SaveAsNew_OK"], MinWidth = 80 };
        var cancelBtn = new Button { Content = loc["SaveAsNew_Cancel"], MinWidth = 80 };

        var dialog = new Window
        {
            Title = loc["SaveAsNew_Title"],
            Width = 360,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Topmost = true,
        };

        okBtn.Click += (_, _) =>
        {
            var name = nameBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                errorLabel.Text = loc["SaveAsNew_Error_Empty"];
                errorLabel.IsVisible = true;
                return;
            }
            if (existingNames.Contains(name))
            {
                errorLabel.Text = loc["SaveAsNew_Error_Exists"];
                errorLabel.IsVisible = true;
                return;
            }
            _saveAsNewResult = name;
            dialog.Close();
        };
        cancelBtn.Click += (_, _) =>
        {
            _saveAsNewResult = null;
            dialog.Close();
        };

        var grid = new Grid { Margin = new Avalonia.Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        grid.Children.Add(nameBox);
        Grid.SetRow(nameBox, 0);

        grid.Children.Add(errorLabel);
        Grid.SetRow(errorLabel, 1);

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
        };
        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        grid.Children.Add(btnPanel);
        Grid.SetRow(btnPanel, 2);

        dialog.Content = grid;

        await dialog.ShowDialog(this);
        return _saveAsNewResult;
    }
}
