namespace Zaya.ScreenTranslator.Layout.Services;

/// <summary>
/// Host-provided translation used by overlay layout (batch or single texts).
/// Layout never references translator plugins directly.
/// </summary>
/// <param name="texts">Source strings in presentation order.</param>
/// <param name="cancellationToken">Cancellation for the translate call.</param>
/// <returns>Translations aligned with <paramref name="texts"/> (same count).</returns>
public delegate Task<IReadOnlyList<string>> OverlayTranslateCallback(
    IReadOnlyList<string> texts,
    CancellationToken cancellationToken);
