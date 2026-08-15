using System.Diagnostics;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class HostVersionChecker : IHostVersionChecker
{
    private readonly IGitHubReleasesClient _gitHubReleasesClient;
    private readonly string _hostRepo;

    public HostVersionChecker(IGitHubReleasesClient gitHubReleasesClient, string hostRepo = "shtrasser-dev/Zaya.ScreenTranslator")
    {
        _gitHubReleasesClient = gitHubReleasesClient;
        _hostRepo = hostRepo;
    }

    public async Task<HostUpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _gitHubReleasesClient.GetHostLatestAsync(_hostRepo, cancellationToken)
                .ConfigureAwait(false);
            if (release is null)
                return new HostUpdateInfo { UpdateAvailable = false };

            var remote = TryParseHostVersion(release);
            if (remote is null)
                return new HostUpdateInfo { UpdateAvailable = false };

            var local = HostChannel.ThreePartAssemblyVersion;
            if (remote <= local)
                return new HostUpdateInfo { UpdateAvailable = false };

            return new HostUpdateInfo
            {
                UpdateAvailable = true,
                RemoteVersion = remote,
                ReleaseHtmlUrl = release.HtmlUrl,
                ReleaseName = release.Name,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return new HostUpdateInfo { UpdateAvailable = false };
        }
    }

    private static Version? TryParseHostVersion(GitHubReleaseInfo release)
    {
        const string tagPrefix = "app-v";
        if (release.TagName.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var rest = release.TagName[tagPrefix.Length..];
            if (Version.TryParse(rest, out var fromTag))
                return NormalizeThreePart(fromTag);
        }

        return release.ParsedVersion is { } parsed
            ? NormalizeThreePart(parsed)
            : null;
    }

    private static Version NormalizeThreePart(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public void OpenReleasePage(string htmlUrl)
    {
        if (string.IsNullOrWhiteSpace(htmlUrl))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = htmlUrl,
            UseShellExecute = true,
        });
    }
}
