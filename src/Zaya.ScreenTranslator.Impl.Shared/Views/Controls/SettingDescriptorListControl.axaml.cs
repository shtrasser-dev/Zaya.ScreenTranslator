using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Views.Controls;

/// <summary>
/// Renders a list of <see cref="SettingDescriptor"/> editors, owns change/visibility handling,
/// and raises <see cref="SettingChanged"/> after a user edit is applied to <see cref="Values"/>.
/// </summary>
public partial class SettingDescriptorListControl : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<SettingDescriptor>?> DescriptorsProperty =
        AvaloniaProperty.Register<SettingDescriptorListControl, IReadOnlyList<SettingDescriptor>?>(nameof(Descriptors));

    public static readonly StyledProperty<IDictionary<string, object>?> ValuesProperty =
        AvaloniaProperty.Register<SettingDescriptorListControl, IDictionary<string, object>?>(nameof(Values));

    public static readonly StyledProperty<IReadOnlyDictionary<string, object>?> HostSettingsProperty =
        AvaloniaProperty.Register<SettingDescriptorListControl, IReadOnlyDictionary<string, object>?>(nameof(HostSettings));

    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<SettingDescriptorListControl, CultureInfo?>(nameof(Culture));

    public static readonly StyledProperty<ILocalizationService?> LocalizationProperty =
        AvaloniaProperty.Register<SettingDescriptorListControl, ILocalizationService?>(nameof(Localization));

    public static readonly StyledProperty<TranslationModuleKind> ModuleKindProperty =
        AvaloniaProperty.Register<SettingDescriptorListControl, TranslationModuleKind>(
            nameof(ModuleKind),
            defaultValue: TranslationModuleKind.None);

    private int _rebuildGeneration;
    private bool _rebuildQueued;

    public SettingDescriptorListControl()
    {
        InitializeComponent();
    }

    public IReadOnlyList<SettingDescriptor>? Descriptors
    {
        get => GetValue(DescriptorsProperty);
        set => SetValue(DescriptorsProperty, value);
    }

    /// <summary>Mutable plugin settings bag written on user edits.</summary>
    public IDictionary<string, object>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>Host/screen-translator settings merged into visibility/context evaluation.</summary>
    public IReadOnlyDictionary<string, object>? HostSettings
    {
        get => GetValue(HostSettingsProperty);
        set => SetValue(HostSettingsProperty, value);
    }

    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    public ILocalizationService? Localization
    {
        get => GetValue(LocalizationProperty);
        set => SetValue(LocalizationProperty, value);
    }

    public TranslationModuleKind ModuleKind
    {
        get => GetValue(ModuleKindProperty);
        set => SetValue(ModuleKindProperty, value);
    }

    public event EventHandler? SettingChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DescriptorsProperty
            || change.Property == ValuesProperty
            || change.Property == HostSettingsProperty
            || change.Property == CultureProperty
            || change.Property == LocalizationProperty)
        {
            QueueRebuild();
        }
    }

    private void QueueRebuild()
    {
        if (_rebuildQueued)
            return;

        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild();
        }, DispatcherPriority.Loaded);
    }

    public void Rebuild()
    {
        var generation = ++_rebuildGeneration;
        RebuildCore(generation);
    }

    private void RebuildCore(int generation)
    {
        Root.Children.Clear();

        var descriptors = Descriptors;
        var values = Values;
        if (descriptors is null || values is null)
            return;

        var culture = Culture ?? CultureInfo.CurrentUICulture;
        var loc = Localization
            ?? throw new InvalidOperationException($"{nameof(SettingDescriptorListControl)}.{nameof(Localization)} must be set.");
        var allSettings = MergeSettings(HostSettings, values);

        foreach (var desc in descriptors)
        {
            if (ManagedSettingKeys.IsHostManaged(desc))
                continue;

            var currentValue = values.TryGetValue(desc.Key, out var val) ? val : null;
            var control = SettingControlFactory.CreateControl(
                desc,
                currentValue,
                allSettings,
                (_, newVal) =>
                {
                    if (generation != _rebuildGeneration)
                        return;
                    OnEditorChanged(desc, newVal);
                },
                culture,
                loc);

            control.Tag = desc;
            control.IsVisible = desc.IsVisible?.Invoke(allSettings) ?? true;
            Root.Children.Add(control);
        }

        SettingControlFactory.SyncTrailingDividers(Root);
    }

    private void OnEditorChanged(SettingDescriptor desc, object? newVal)
    {
        var values = Values;
        if (values is null)
            return;

        if (IsSettingValueUnchanged(values, desc, newVal))
            return;

        if (newVal is null)
            values.Remove(desc.Key);
        else
            values[desc.Key] = newVal;

        UpdateVisibility();
        SettingChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisibility()
    {
        var values = Values;
        if (values is null)
            return;

        var allSettings = MergeSettings(HostSettings, values);
        foreach (Control child in Root.Children)
        {
            if (child.Tag is SettingDescriptor desc)
                child.IsVisible = desc.IsVisible?.Invoke(allSettings) ?? true;
        }

        SettingControlFactory.SyncTrailingDividers(Root);
    }

    private static IReadOnlyDictionary<string, object?> MergeSettings(
        IReadOnlyDictionary<string, object>? hostSettings,
        IDictionary<string, object> pluginSettings)
    {
        var merged = new Dictionary<string, object?>(
            (hostSettings?.Count ?? 0) + pluginSettings.Count);

        if (hostSettings is not null)
        {
            foreach (var kv in hostSettings)
                merged[kv.Key] = kv.Value;
        }

        foreach (var kv in pluginSettings)
            merged[kv.Key] = kv.Value;

        return merged;
    }

    private static bool IsSettingValueUnchanged(
        IDictionary<string, object> pluginSettings,
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
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static bool TryCompareStructured(object left, object right, out bool equal)
    {
        equal = false;

        if (left is IDictionary leftMap && right is IDictionary rightMap)
        {
            if (leftMap.Count != rightMap.Count)
                return true;

            foreach (DictionaryEntry entry in leftMap)
            {
                if (entry.Key is not string key)
                    return true;

                if (!rightMap.Contains(key) || !SettingValuesEqual(entry.Value, rightMap[key]))
                    return true;
            }

            equal = true;
            return true;
        }

        if (left is IEnumerable leftSeq and not string
            && right is IEnumerable rightSeq and not string
            && left is not IDictionary
            && right is not IDictionary)
        {
            using var leftEnum = leftSeq.Cast<object?>().GetEnumerator();
            using var rightEnum = rightSeq.Cast<object?>().GetEnumerator();
            while (true)
            {
                var leftMoved = leftEnum.MoveNext();
                var rightMoved = rightEnum.MoveNext();
                if (leftMoved != rightMoved)
                    return true;

                if (!leftMoved)
                {
                    equal = true;
                    return true;
                }

                if (!SettingValuesEqual(leftEnum.Current, rightEnum.Current))
                    return true;
            }
        }

        return false;
    }

    private static bool IsNumeric(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;
}
