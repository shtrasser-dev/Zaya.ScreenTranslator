namespace Zaya.ScreenTranslator.Impl.Shared.Exceptions;

public enum InvalidPluginManifestReason
{
    InvalidManifest,
    IncompatibleInterface,
    MissingEntryPoint,
    EntryPointNotFound,
    ProbeFailed,
}

public sealed class InvalidPluginManifestException : Exception
{
    public InvalidPluginManifestReason Reason { get; }

    public InvalidPluginManifestException(InvalidPluginManifestReason reason, Exception? innerException = null)
        : base(reason.ToString(), innerException)
    {
        Reason = reason;
    }
}
