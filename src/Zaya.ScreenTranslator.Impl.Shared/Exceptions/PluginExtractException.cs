namespace Zaya.ScreenTranslator.Impl.Shared.Exceptions;

public enum PluginExtractReason
{
    ExtractFailed,
    DeleteFailed,
}

public sealed class PluginExtractException : Exception
{
    public PluginExtractReason Reason { get; }

    public PluginExtractException(PluginExtractReason reason, Exception? innerException = null)
        : base(reason.ToString(), innerException)
    {
        Reason = reason;
    }
}
