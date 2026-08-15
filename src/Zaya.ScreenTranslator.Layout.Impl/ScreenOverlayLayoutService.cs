using Avalonia.Threading;
using Zaya.Logging.Services;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Layout.Impl.Constants;
using Zaya.ScreenTranslator.Layout.Impl.Services;
using Zaya.ScreenTranslator.Layout.Services;

namespace Zaya.ScreenTranslator.Layout.Impl;

/// <summary>
/// Default overlay layout engine: draws text above/over/below item bounds.
/// </summary>
public sealed class ScreenOverlayLayoutService : IOverlayLayoutService
{
    public const string EngineIdValue = OverlayLayoutSettingKeys.EngineId;

    private static readonly IReadOnlyList<SettingDescriptor> SettingsList = SettingsDescriptorsConstants.Settings;
    private readonly ILoggingWrapper _loggingWrapper;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance using <see cref="EmptyLoggingWrapper.Instance"/>.
    /// </summary>
    public ScreenOverlayLayoutService()
        : this(EmptyLoggingWrapper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified logging wrapper.
    /// </summary>
    /// <param name="loggingWrapper">Logging wrapper used when creating sessions.</param>
    public ScreenOverlayLayoutService(ILoggingWrapper loggingWrapper)
    {
        _loggingWrapper = loggingWrapper ?? EmptyLoggingWrapper.Instance;
    }

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.ResourceManager.GetString(key, culture) ?? key);

    public string EngineId => EngineIdValue;
    public LocalizedString DisplayName => Loc(LocalizationConstants.Overlay.EngineName);
    public LocalizedString Description => Loc(LocalizationConstants.Overlay.EngineDesc);
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<SettingDescriptor> Settings => SettingsList;

    public Task<IOverlayLayoutSession> CreateSessionAsync(CancellationToken cancellationToken = default)
        => CreateSessionAsync(new Dictionary<string, object>(), translate: null, cancellationToken);

    public Task<IOverlayLayoutSession> CreateSessionAsync(
        IReadOnlyDictionary<string, object> engineSettings,
        CancellationToken cancellationToken = default)
        => CreateSessionAsync(engineSettings, translate: null, cancellationToken);

    public async Task<IOverlayLayoutSession> CreateSessionAsync(
        IReadOnlyDictionary<string, object> engineSettings,
        OverlayTranslateCallback? translate,
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
            throw new ArgumentException($"{OverlayLayoutSettingKeys.TargetWindowHandle} is required.", nameof(engineSettings));

        if (Dispatcher.UIThread.CheckAccess())
            return CreateSessionCore(list, hwnd, translate);

        return await Dispatcher.UIThread.InvokeAsync(() => CreateSessionCore(list, hwnd, translate));
    }

    private IOverlayLayoutSession CreateSessionCore(
        SettingDescriptorList list,
        IntPtr hwnd,
        OverlayTranslateCallback? translate)
        => _loggingWrapper.Wrap<IOverlayLayoutSession>(
            new ScreenOverlayLayoutSession(list, hwnd, translate));

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

    public void Dispose() => _disposed = true;
}
