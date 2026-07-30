using Avalonia.Threading;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Layout.Impl.Constants;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Layout.Impl.Services;

/// <summary>
/// Default in-host overlay layout engine: draws text above/over/below item bounds.
/// </summary>
public sealed class ScreenOverlayLayoutService : IOverlayLayoutService
{
    public const string EngineIdValue = "screen-overlay";

    private static readonly IReadOnlyList<SettingDescriptor> SettingsList = BuildSettings();
    private bool _disposed;

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.ResourceManager.GetString(key, culture) ?? key);

    public string EngineId => EngineIdValue;
    public LocalizedString DisplayName => Loc("Overlay_EngineName");
    public LocalizedString Description => Loc("Overlay_EngineDesc");
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<SettingDescriptor> Settings => SettingsList;

    public Task<IOverlayLayoutSession> CreateSessionAsync(CancellationToken cancellationToken = default)
        => CreateSessionAsync(new Dictionary<string, object>(), cancellationToken);

    public async Task<IOverlayLayoutSession> CreateSessionAsync(
        IReadOnlyDictionary<string, object> engineSettings,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Screen overlay requires Windows.");

        var list = new SettingDescriptorList(SettingsList);
        list.Bind(engineSettings);

        var hwnd = ResolveHandle(engineSettings);
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("targetWindowHandle is required.", nameof(engineSettings));

        if (Dispatcher.UIThread.CheckAccess())
            return CreateSessionCore(list, hwnd);

        return await Dispatcher.UIThread.InvokeAsync(() => CreateSessionCore(list, hwnd));
    }

    private static IOverlayLayoutSession CreateSessionCore(SettingDescriptorList list, IntPtr hwnd)
        => new ScreenOverlayLayoutSession(list, hwnd);

    private static IntPtr ResolveHandle(IReadOnlyDictionary<string, object> settings)
    {
        if (!settings.TryGetValue(OverlayLayoutSettingKeys.TargetWindowHandle, out var raw) || raw is null)
            return IntPtr.Zero;

        return raw switch
        {
            IntPtr p => p,
            long l => new IntPtr(l),
            int i => new IntPtr(i),
            _ => IntPtr.Zero,
        };
    }

    private static IReadOnlyList<SettingDescriptor> BuildSettings() =>
    [
        new EnumSettingDescriptor(OverlayLayoutSettingKeys.Placement, Loc("Overlay_Placement"))
        {
            Description = Loc("Overlay_Placement_Desc"),
            DefaultValue = OverlayLayoutSettingKeys.PlacementAbove,
            Options =
            [
                new(OverlayLayoutSettingKeys.PlacementAbove, Loc("Overlay_Placement_Above")),
                new(OverlayLayoutSettingKeys.PlacementOver, Loc("Overlay_Placement_Over")),
                new(OverlayLayoutSettingKeys.PlacementBelow, Loc("Overlay_Placement_Below")),
            ],
        },
        new IntegerSettingDescriptor(OverlayLayoutSettingKeys.FontScale, Loc("Overlay_FontScale"))
        {
            Description = Loc("Overlay_FontScale_Desc"),
            DefaultValue = 60,
            MinValue = 20,
            MaxValue = 150,
        },
        new IntegerSettingDescriptor(OverlayLayoutSettingKeys.OffsetY, Loc("Overlay_OffsetY"))
        {
            Description = Loc("Overlay_OffsetY_Desc"),
            DefaultValue = 4,
            MinValue = -200,
            MaxValue = 200,
        },
        new IntegerSettingDescriptor(OverlayLayoutSettingKeys.Padding, Loc("Overlay_Padding"))
        {
            Description = Loc("Overlay_Padding_Desc"),
            DefaultValue = 2,
            MinValue = 0,
            MaxValue = 40,
        },
        new EnumSettingDescriptor(OverlayLayoutSettingKeys.Background, Loc("Overlay_Background"))
        {
            Description = Loc("Overlay_Background_Desc"),
            DefaultValue = OverlayLayoutSettingKeys.BackgroundNone,
            Options =
            [
                new(OverlayLayoutSettingKeys.BackgroundNone, Loc("Overlay_Background_None")),
                new(OverlayLayoutSettingKeys.BackgroundSoft, Loc("Overlay_Background_Soft")),
                new(OverlayLayoutSettingKeys.BackgroundOpaque, Loc("Overlay_Background_Opaque")),
            ],
        },
        new IntegerSettingDescriptor(OverlayLayoutSettingKeys.BackgroundOpacity, Loc("Overlay_BackgroundOpacity"))
        {
            Description = Loc("Overlay_BackgroundOpacity_Desc"),
            DefaultValue = 70,
            MinValue = 0,
            MaxValue = 100,
            IsVisible = s => (s.GetValueOrDefault(OverlayLayoutSettingKeys.Background) as string
                              ?? OverlayLayoutSettingKeys.BackgroundNone)
                             != OverlayLayoutSettingKeys.BackgroundNone,
        },
        new EnumSettingDescriptor(OverlayLayoutSettingKeys.TextColor, Loc("Overlay_TextColor"))
        {
            Description = Loc("Overlay_TextColor_Desc"),
            DefaultValue = OverlayLayoutSettingKeys.TextColorAuto,
            Options =
            [
                new(OverlayLayoutSettingKeys.TextColorAuto, Loc("Overlay_TextColor_Auto")),
                new(OverlayLayoutSettingKeys.TextColorLight, Loc("Overlay_TextColor_Light")),
                new(OverlayLayoutSettingKeys.TextColorDark, Loc("Overlay_TextColor_Dark")),
            ],
        },
        new BooleanSettingDescriptor(OverlayLayoutSettingKeys.Outline, Loc("Overlay_Outline"))
        {
            Description = Loc("Overlay_Outline_Desc"),
            DefaultValue = true,
        },
        new EnumSettingDescriptor(OverlayLayoutSettingKeys.FitMode, Loc("Overlay_FitMode"))
        {
            Description = Loc("Overlay_FitMode_Desc"),
            DefaultValue = OverlayLayoutSettingKeys.FitShrink,
            Options =
            [
                new(OverlayLayoutSettingKeys.FitShrink, Loc("Overlay_FitMode_Shrink")),
                new(OverlayLayoutSettingKeys.FitWrap, Loc("Overlay_FitMode_Wrap")),
                new(OverlayLayoutSettingKeys.FitClip, Loc("Overlay_FitMode_Clip")),
            ],
        },
    ];

    public void Dispose() => _disposed = true;
}
