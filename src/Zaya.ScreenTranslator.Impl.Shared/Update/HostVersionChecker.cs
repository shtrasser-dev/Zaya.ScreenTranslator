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

    public async Task<HostUpdateInfo> CheckAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var release = await _client.GetChannelLatestAsync(_hostRepo, channel, "app-v", cancellationToken)
                .ConfigureAwait(false);
            if (release is null)
                return new HostUpdateInfo { UpdateAvailable = false };

            var remote = release.ParsedVersion;
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
