using Avalonia.Media.Imaging;

namespace Zaya.ScreenTranslator.Impl.Shared.Models;

/// <summary>
/// Describes a running window for target selection.
/// </summary>
public sealed class WindowInfo
{
    public static WindowInfo Loading { get; } = new() { IsLoadingPlaceholder = true };

    public nint Handle { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public Bitmap? Icon { get; init; }
    public bool IsLoadingPlaceholder { get; init; }

    public bool HasIcon => Icon is not null;

    public override string ToString() =>
        IsLoadingPlaceholder ? string.Empty : $"{Title}  [{ProcessName}]";
}
