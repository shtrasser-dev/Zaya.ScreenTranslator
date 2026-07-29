namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Describes a running window for target selection.
/// </summary>
public sealed class WindowInfo
{
    public nint Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;

    public override string ToString() => $"{Title}  [{ProcessName}]";
}
