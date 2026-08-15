using Zaya.ScreenTranslator.Impl.Shared.Update;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public sealed class BootstrapResult
{
    public bool Success { get; init; }
    public string? ErrorTitle { get; init; }
    public string? ErrorMessage { get; init; }
    public HostUpdateInfo? HostUpdate { get; init; }
}

public interface IApplicationBootstrap
{
    Task<BootstrapResult> RunAsync(
        string channel,
        bool checkUpdatesOnStartup,
        IProgress<string>? status,
        CancellationToken cancellationToken = default);
}
