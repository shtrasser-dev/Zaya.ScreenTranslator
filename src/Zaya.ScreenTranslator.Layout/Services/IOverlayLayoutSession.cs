using Zaya.ScreenTranslator.Layout.Models;

namespace Zaya.ScreenTranslator.Layout.Services;

/// <summary>
/// Active overlay layout session bound to a target window.
/// </summary>
public interface IOverlayLayoutSession : IDisposable
{
    Task PresentAsync(OverlayPresentRequest request, CancellationToken cancellationToken = default);

    void SetVisible(bool visible);
    void Clear();
}
