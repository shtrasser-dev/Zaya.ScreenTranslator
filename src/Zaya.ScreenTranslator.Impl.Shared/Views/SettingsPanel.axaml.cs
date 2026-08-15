using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;
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
        AttachedToVisualTree += (_, _) =>
        {
            SyncOwnerWindow();
            ScheduleBindAll();
        };
        SettingsTabControl.SelectionChanged += (_, _) => ScheduleBindAll();
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
        ScheduleBindAll();
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
            if (e.PropertyName is nameof(SettingsViewModel.OcrDescriptors)
                or nameof(SettingsViewModel.CaptureDescriptors)
                or nameof(SettingsViewModel.TextLayoutDescriptors)
                or nameof(SettingsViewModel.TranslatorDescriptors)
                or nameof(SettingsViewModel.TranslatorCacheDescriptors)
                or nameof(SettingsViewModel.OverlayLayoutDescriptors))
            {
                BindAllLists();
            }
        };
    }

    private void ScheduleBindAll()
    {
        // Tab content is swapped into the visual tree after SelectionChanged; defer one layout pass.
        Dispatcher.UIThread.Post(BindAllLists, DispatcherPriority.Loaded);
    }

    private void BindAllLists()
    {
        if (_viewModel is null)
            return;

        var vm = _viewModel;
        var host = GetHostSettings(vm);
        var culture = vm.Localization.CurrentCulture;

        BindList(
            "OcrSettingsPanel",
            vm.OcrDescriptors,
            GetPluginSettings(vm, () => vm.EditingProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.Ocr)),
            host,
            culture,
            vm.Localization);
        BindList(
            "CaptureSettingsPanel",
            vm.CaptureDescriptors,
            GetPluginSettings(vm, () => vm.EditingProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.Capture)),
            host,
            culture,
            vm.Localization);
        BindList(
            "TextLayoutSettingsPanel",
            vm.TextLayoutDescriptors,
            GetPluginSettings(vm, () => vm.EditingProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.TextLayout)),
            host,
            culture,
            vm.Localization);
        BindList(
            "TranslatorSettingsPanel",
            vm.TranslatorDescriptors,
            GetPluginSettings(vm, () => vm.EditingProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.Translator)),
            host,
            culture,
            vm.Localization);
        BindList(
            "TranslatorCacheSettingsPanel",
            vm.TranslatorCacheDescriptors,
            GetPluginSettings(vm, () => vm.EditingProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.TranslatorCache)),
            host,
            culture,
            vm.Localization);
        BindList(
            "OverlaySettingsPanel",
            vm.OverlayLayoutDescriptors,
            GetPluginSettings(vm, () => vm.EditingProfile.ScreenTranslatorSettings
                .GetValueAsString(ScreenTranslatorSettingDescriptors.OverlayLayout)),
            host,
            culture,
            vm.Localization);
    }

    private void BindList(
        string controlName,
        IReadOnlyList<Zaya.Primitives.SettingDescriptor>? descriptors,
        IDictionary<string, object>? values,
        IReadOnlyDictionary<string, object>? hostSettings,
        System.Globalization.CultureInfo culture,
        ILocalizationService localizationService)
    {
        var control = FindListControl(controlName);
        if (control is null)
            return;

        control.SettingChanged -= OnListSettingChanged;
        control.SettingChanged += OnListSettingChanged;

        control.Culture = culture;
        control.Localization = localizationService;
        control.HostSettings = hostSettings;
        control.Values = values;
        control.Descriptors = descriptors;
    }

    private void OnListSettingChanged(object? sender, EventArgs e)
    {
        if (_viewModel is null || sender is not SettingDescriptorListControl control)
            return;

        _viewModel.ApplyChanges(moduleHint: control.ModuleKind);
    }

    private static IReadOnlyDictionary<string, object> GetHostSettings(SettingsViewModel vm) =>
        vm.EditingProfile.Settings[ScreenTranslatorSettingDescriptors.StKey];

    private static IDictionary<string, object> GetPluginSettings(SettingsViewModel vm, Func<string> getPluginId)
    {
        var pluginId = getPluginId();
        if (!vm.EditingProfile.Settings.TryGetValue(pluginId, out var pluginSettings))
        {
            pluginSettings = new Dictionary<string, object>();
            vm.EditingProfile.Settings[pluginId] = pluginSettings;
        }

        return pluginSettings;
    }

    private SettingDescriptorListControl? FindListControl(string name) =>
        this.GetVisualDescendants()
            .OfType<SettingDescriptorListControl>()
            .FirstOrDefault(c => c.Name == name);
}
