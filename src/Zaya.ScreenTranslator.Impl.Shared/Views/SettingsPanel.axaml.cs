using Avalonia.Controls;
using Avalonia.Threading;
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
    private int _ignoreSettingControlCallbacks;

    public SettingsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            SyncOwnerWindow();
            ScheduleRebuildAll();
        };
        SettingsTabControl.SelectionChanged += (_, _) => ScheduleRebuildAll();
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
        ScheduleRebuildAll();
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

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.OcrDescriptors))
                RebuildPanelIgnoringCallbacks("OcrSettingsPanel", viewModel.OcrDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
            if (e.PropertyName == nameof(SettingsViewModel.CaptureDescriptors))
                RebuildPanelIgnoringCallbacks("CaptureSettingsPanel", viewModel.CaptureDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
            if (e.PropertyName == nameof(SettingsViewModel.TextLayoutDescriptors))
                RebuildPanelIgnoringCallbacks("TextLayoutSettingsPanel", viewModel.TextLayoutDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
            if (e.PropertyName == nameof(SettingsViewModel.TranslatorDescriptors))
                RebuildPanelIgnoringCallbacks("TranslatorSettingsPanel", viewModel.TranslatorDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
            if (e.PropertyName == nameof(SettingsViewModel.TranslatorCacheDescriptors))
                RebuildPanelIgnoringCallbacks("TranslatorCacheSettingsPanel", viewModel.TranslatorCacheDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache));
            if (e.PropertyName == nameof(SettingsViewModel.OverlayLayoutDescriptors))
                RebuildPanelIgnoringCallbacks("OverlaySettingsPanel", viewModel.OverlayLayoutDescriptors,
                    () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
        };
    }

    private void RebuildPanelIgnoringCallbacks(
        string panelName,
        IReadOnlyList<SettingDescriptor>? descriptors,
        Func<string> getPluginId)
    {
        _ignoreSettingControlCallbacks++;
        try
        {
            RebuildPanel(panelName, descriptors, getPluginId);
        }
        finally
        {
            Dispatcher.UIThread.Post(
                () => _ignoreSettingControlCallbacks = Math.Max(0, _ignoreSettingControlCallbacks - 1),
                DispatcherPriority.Input);
        }
    }

    private void ScheduleRebuildAll()
    {
        // Tab content is swapped into the visual tree after SelectionChanged; defer one layout pass.
        Dispatcher.UIThread.Post(RebuildAllPanels, DispatcherPriority.Loaded);
    }

    private void RebuildAllPanels()
    {
        if (_viewModel is null)
            return;

        var viewModel = _viewModel;
        _ignoreSettingControlCallbacks++;
        try
        {
            RebuildPanel("OcrSettingsPanel", viewModel.OcrDescriptors,
                () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr));
            RebuildPanel("CaptureSettingsPanel", viewModel.CaptureDescriptors,
                () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Capture));
            RebuildPanel("TextLayoutSettingsPanel", viewModel.TextLayoutDescriptors,
                () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout));
            RebuildPanel("TranslatorSettingsPanel", viewModel.TranslatorDescriptors,
                () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.Translator));
            RebuildPanel("TranslatorCacheSettingsPanel", viewModel.TranslatorCacheDescriptors,
                () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache));
            RebuildPanel("OverlaySettingsPanel", viewModel.OverlayLayoutDescriptors,
                () => viewModel.EditingProfile.ScreenTranslatorSettings.GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout));
        }
        finally
        {
            // ComboBox/Toggle may raise change events when attached after this method returns.
            Dispatcher.UIThread.Post(
                () => _ignoreSettingControlCallbacks = Math.Max(0, _ignoreSettingControlCallbacks - 1),
                DispatcherPriority.Input);
        }
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
                    if (_ignoreSettingControlCallbacks > 0)
                        return;
                    if (IsSettingValueUnchanged(pluginSettings, desc, newVal))
                        return;

                    if (newVal is null)
                        pluginSettings.Remove(desc.Key);
                    else
                        pluginSettings[desc.Key] = newVal;
                    UpdateVisibility(panelName, getPluginId);
                    _viewModel.ApplyChanges(moduleHint: ModuleKindForPanel(panelName));
                },
                _viewModel.Localization.CurrentCulture);

            control.Tag = desc;
            control.IsVisible = desc.IsVisible.Invoke(allSettings);
            panel.Children.Add(control);
        }
    }

    private static TranslationModuleKind ModuleKindForPanel(string panelName) => panelName switch
    {
        "OcrSettingsPanel" => TranslationModuleKind.Ocr,
        "CaptureSettingsPanel" => TranslationModuleKind.Capture,
        "TextLayoutSettingsPanel" => TranslationModuleKind.TextLayout,
        "TranslatorSettingsPanel" => TranslationModuleKind.Translator,
        "TranslatorCacheSettingsPanel" => TranslationModuleKind.Translator,
        "OverlaySettingsPanel" => TranslationModuleKind.Overlay,
        _ => TranslationModuleKind.None,
    };

    private static bool IsSettingValueUnchanged(
        IReadOnlyDictionary<string, object> pluginSettings,
        SettingDescriptor desc,
        object? newVal)
    {
        if (pluginSettings.TryGetValue(desc.Key, out var existing))
            return SettingValuesEqual(existing, newVal);

        return SettingValuesEqual(GetDescriptorDefault(desc), newVal);
    }

    private static object? GetDescriptorDefault(SettingDescriptor desc) => desc switch
    {
        EnumSettingDescriptor e => e.DefaultValue,
        BooleanSettingDescriptor b => b.DefaultValue,
        IntegerSettingDescriptor i => i.DefaultValue,
        StringSettingDescriptor s => s.DefaultValue,
        UrlSettingDescriptor u => u.DefaultValue,
        DirectoryPathSettingDescriptor d => d.DefaultValue,
        FilePathSettingDescriptor f => f.DefaultValue,
        PasswordSettingDescriptor p => p.DefaultValue,
        TableSettingDescriptor => new List<Dictionary<string, object>>(),
        _ => null,
    };

    private static bool SettingValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;

        if (TryCompareStructured(left, right, out var structuredEqual))
            return structuredEqual;

        if (left.Equals(right))
            return true;

        if (IsNumeric(left) && IsNumeric(right))
            return Convert.ToDecimal(left) == Convert.ToDecimal(right);

        return string.Equals(
            Convert.ToString(left, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToString(right, System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Deep-compare lists/dicts. <see cref="Convert.ToString(object?)"/> on a List is the type name,
    /// so scalar string equality falsely treats distinct table instances as unchanged.
    /// </summary>
    private static bool TryCompareStructured(object left, object right, out bool equal)
    {
        equal = false;

        if (left is System.Collections.IDictionary leftMap && right is System.Collections.IDictionary rightMap)
        {
            if (leftMap.Count != rightMap.Count)
            {
                equal = false;
                return true;
            }

            foreach (System.Collections.DictionaryEntry entry in leftMap)
            {
                if (entry.Key is not string key)
                {
                    equal = false;
                    return true;
                }

                if (!rightMap.Contains(key) || !SettingValuesEqual(entry.Value, rightMap[key]))
                {
                    equal = false;
                    return true;
                }
            }

            equal = true;
            return true;
        }

        if (left is System.Collections.IEnumerable leftSeq and not string
            && right is System.Collections.IEnumerable rightSeq and not string
            && left is not System.Collections.IDictionary
            && right is not System.Collections.IDictionary)
        {
            using var leftEnum = leftSeq.Cast<object?>().GetEnumerator();
            using var rightEnum = rightSeq.Cast<object?>().GetEnumerator();
            while (true)
            {
                var leftMoved = leftEnum.MoveNext();
                var rightMoved = rightEnum.MoveNext();
                if (leftMoved != rightMoved)
                {
                    equal = false;
                    return true;
                }

                if (!leftMoved)
                {
                    equal = true;
                    return true;
                }

                if (!SettingValuesEqual(leftEnum.Current, rightEnum.Current))
                {
                    equal = false;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

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
