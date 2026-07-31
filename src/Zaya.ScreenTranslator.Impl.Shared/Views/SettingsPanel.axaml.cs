using Avalonia.Controls;
using Avalonia.VisualTree;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.ViewModels;
using Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

namespace Zaya.ScreenTranslator.Impl.Shared.Views;

public partial class SettingsPanel : UserControl
{
    private SettingsViewModel? _viewModel;
    private bool _panelsWired;

    public SettingsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => SyncOwnerWindow();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel = null;
            _panelsWired = false;
        }

        if (DataContext is not SettingsViewModel vm)
            return;

        _viewModel = vm;
        SyncOwnerWindow();
        WirePanels(vm);
    }

    private void SyncOwnerWindow()
    {
        if (_viewModel is null)
            return;
        _viewModel.OwnerWindow = TopLevel.GetTopLevel(this) as Window;
    }

    private void WirePanels(SettingsViewModel viewModel)
    {
        if (_panelsWired)
            return;
        _panelsWired = true;

        RebuildPanel("OcrSettingsPanel", viewModel.OcrDescriptors,
            () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
        RebuildPanel("CaptureSettingsPanel", viewModel.CaptureDescriptors,
            () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
        RebuildPanel("TextLayoutSettingsPanel", viewModel.TextLayoutDescriptors,
            () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
        RebuildPanel("TranslatorSettingsPanel", viewModel.TranslatorDescriptors,
            () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
        RebuildPanel("OverlaySettingsPanel", viewModel.OverlayLayoutDescriptors,
            () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
        RebuildPanel("FilterSettingsPanel", ScreenTranslatorSettingDescriptors.FilterDescriptors,
            () => ScreenTranslatorSettingDescriptors.StKey);

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.OcrDescriptors))
                RebuildPanel("OcrSettingsPanel", viewModel.OcrDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
            if (e.PropertyName == nameof(SettingsViewModel.CaptureDescriptors))
                RebuildPanel("CaptureSettingsPanel", viewModel.CaptureDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
            if (e.PropertyName == nameof(SettingsViewModel.TextLayoutDescriptors))
                RebuildPanel("TextLayoutSettingsPanel", viewModel.TextLayoutDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
            if (e.PropertyName == nameof(SettingsViewModel.TranslatorDescriptors))
                RebuildPanel("TranslatorSettingsPanel", viewModel.TranslatorDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
            if (e.PropertyName == nameof(SettingsViewModel.OverlayLayoutDescriptors))
                RebuildPanel("OverlaySettingsPanel", viewModel.OverlayLayoutDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
        };
    }

    private void RebuildPanel(string panelName,
        IReadOnlyList<SettingDescriptor>? descriptors,
        Func<string> getPluginId)
    {
        if (_viewModel is null)
            return;

        var panel = FindNamedPanel(panelName);
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
                    _viewModel.ApplyChanges();
                },
                _viewModel.Localization.CurrentCulture);

            control.Tag = desc;
            control.IsVisible = desc.IsVisible.Invoke(allSettings);
            panel.Children.Add(control);
        }
    }

    private void UpdateVisibility(string panelName, Func<string> getPluginId)
    {
        if (_viewModel is null)
            return;

        var panel = FindNamedPanel(panelName);
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

    private StackPanel? FindNamedPanel(string panelName) =>
        this.GetVisualDescendants()
            .OfType<StackPanel>()
            .FirstOrDefault(p => p.Name == panelName);

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
}
