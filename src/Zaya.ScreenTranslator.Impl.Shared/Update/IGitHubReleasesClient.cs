namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IGitHubReleasesClient : IDisposable
{
    Task<GitHubReleaseInfo?> GetReleaseByTagAsync(
        string ownerRepo,
        string tag,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubReleaseInfo>> ListReleasesAsync(
        string ownerRepo,
        CancellationToken cancellationToken = default);

    Task<GitHubReleaseInfo?> GetHostLatestAsync(
        string ownerRepo,
        CancellationToken cancellationToken = default);

    Task<GitHubReleaseInfo?> GetChannelLatestAsync(
        string ownerRepo,
        string channel,
        string tagPrefix,
        CancellationToken cancellationToken = default);

    Task<GitHubReleaseInfo?> GetPluginInterfaceChannelLatestAsync(
        string ownerRepo,
        string interfaceName,
        string channel,
        CancellationToken cancellationToken = default);

    Task DownloadAssetAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
