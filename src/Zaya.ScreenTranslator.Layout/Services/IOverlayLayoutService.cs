using Zaya.Primitives;
using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Layout.Services;

/// <summary>
/// Draws text overlays on top of a target window from <see cref="OverlayItem"/> lists.
/// </summary>
public interface IOverlayLayoutService : IDisposable
{
    string EngineId { get; }
    LocalizedString DisplayName { get; }
    LocalizedString Description { get; }
    bool IsAvailable { get; }
    IReadOnlyList<SettingDescriptor> Settings { get; }

    Task<IOverlayLayoutSession> CreateSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a session. <paramref name="engineSettings"/> must include plugin keys and
    /// host-injected <c>targetWindowHandle</c> (<see cref="IntPtr"/> or <see cref="long"/>).
    /// </summary>
    Task<IOverlayLayoutSession> CreateSessionAsync(
        IReadOnlyDictionary<string, object> engineSettings,
        CancellationToken cancellationToken = default);
}
