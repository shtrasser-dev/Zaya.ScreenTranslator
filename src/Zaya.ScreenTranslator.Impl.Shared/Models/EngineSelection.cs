using Zaya.ScreenTranslator.Impl.Shared.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Picks an engine from the available list: preferred id when present, otherwise the first entry.
/// </summary>
internal static class EngineSelection
{
    public static EngineInfo? Pick(IReadOnlyList<EngineInfo> available, string? preferredId)
    {
        if (available.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            var match = available.FirstOrDefault(e =>
                string.Equals(e.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return available[0];
    }

    public static string? PickId(IReadOnlyList<EngineInfo> available, string? preferredId)
        => Pick(available, preferredId)?.Id;
}
