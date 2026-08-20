using Zaya.Primitives;
using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Layout.Services;

/// <summary>
/// Draws text overlays on top of a target window from text-layout results.
/// </summary>
public interface IOverlayLayoutService : IDisposable
{
    string EngineId { get; }
    LocalizedString DisplayName { get; }
    LocalizedString Description { get; }
    bool IsAvailable { get; }
    IReadOnlyList<SettingDescriptor> Settings { get; }

    /// <summary>
    /// Creates a session. <paramref name="engineSettings"/> must include plugin keys and
    /// host-injected <c>targetWindowHandle</c> (<see cref="IntPtr"/> or <see cref="long"/>).
    /// When <paramref name="translate"/> is set, the session owns join/split and translate requests.
    /// </summary>
    Task<IOverlayLayoutSession> CreateSessionAsync(
        IReadOnlyDictionary<string, object> engineSettings,
        OverlayTranslateCallback? translate = null,
        CancellationToken cancellationToken = default);
}
