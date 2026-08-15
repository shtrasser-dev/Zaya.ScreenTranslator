using Mel = Microsoft.Extensions.Logging;

namespace Zaya.ScreenTranslator.Impl.Shared.Logging;

/// <summary>
/// Host logging settings loaded from <c>%AppData%\Zaya\ScreenTranslator\log.json</c>.
/// </summary>
public sealed class LogOptions
{
    public const string FileName = "log.json";
    public const string DefaultLogsFolderName = "logs";

    /// <summary>
    /// MEL level name: Trace, Debug, Information, Warning, Error.
    /// </summary>
    public string Level { get; set; } = "Debug";

    /// <summary>
    /// When true, write to the debugger (Visual Studio Output / DebugView).
    /// </summary>
    public bool WriteToDebug { get; set; } = true;

    /// <summary>
    /// When true, write rolling files under the logs directory.
    /// </summary>
    public bool WriteToFile { get; set; } = true;

    /// <summary>
    /// Maximum size of the active log file in bytes before rotation.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum number of log files to keep (including the active file).
    /// </summary>
    public int MaxFileCount { get; set; } = 5;

    public Mel.LogLevel ResolveLevel()
    {
        if (Enum.TryParse<Mel.LogLevel>(Level, ignoreCase: true, out var mel)
            && mel != Mel.LogLevel.None)
            return mel;

        return Mel.LogLevel.Debug;
    }

    public long ResolveMaxFileSizeBytes()
        => MaxFileSizeBytes > 0 ? MaxFileSizeBytes : 5 * 1024 * 1024;

    public int ResolveMaxFileCount()
        => MaxFileCount > 0 ? MaxFileCount : 5;

    public static LogOptions CreateDefault() => new();
}
