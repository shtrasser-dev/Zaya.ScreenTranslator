using System.Diagnostics;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class HostVersionChecker
{
    private readonly GitHubReleasesClient _client;
    private readonly string _hostRepo;

    public HostVersionChecker(GitHubReleasesClient client, string hostRepo = "shtrasser-dev/Zaya.ScreenTranslator")
    {
        _client = client;
        _hostRepo = hostRepo;
    }

    /// <summary>
    /// Compares the local host version to the newest immutable <c>app-v*</c> release (any channel).
    /// </summary>
    public async Task<HostUpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _client.GetHostLatestAsync(_hostRepo, cancellationToken)
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
        catch (Exception ex)
        {
            Debug.WriteLine($"[HostVersionChecker] {ex.Message}");
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

    public static void OpenReleasePage(string htmlUrl)
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
