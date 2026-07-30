using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

/// <summary>
/// Thin GitHub Releases API client. Session-scoped cache to reduce rate-limit pressure.
/// </summary>
public sealed class GitHubReleasesClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, GitHubReleaseInfo?> _releaseByTagCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<GitHubReleaseInfo>> _releasesListCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public GitHubReleasesClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Zaya.ScreenTranslator", HostChannel.Current));
        if (!_http.DefaultRequestHeaders.Accept.Any())
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<GitHubReleaseInfo?> GetReleaseByTagAsync(
        string ownerRepo,
        string tag,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{ownerRepo}@{tag}";
        if (_releaseByTagCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var url = $"https://api.github.com/repos/{ownerRepo}/releases/tags/{Uri.EscapeDataString(tag)}";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _releaseByTagCache[cacheKey] = null;
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ReleaseDto>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        var info = Map(dto);
        _releaseByTagCache[cacheKey] = info;
        return info;
    }

    public async Task<IReadOnlyList<GitHubReleaseInfo>> ListReleasesAsync(
        string ownerRepo,
        CancellationToken cancellationToken = default)
    {
        if (_releasesListCache.TryGetValue(ownerRepo, out var cached))
            return cached;

        var url = $"https://api.github.com/repos/{ownerRepo}/releases?per_page=30";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<List<ReleaseDto>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? [];
        var list = dtos.Select(Map).Where(r => r is not null).Cast<GitHubReleaseInfo>().ToList();
        _releasesListCache[ownerRepo] = list;
        return list;
    }

    /// <summary>
    /// Channel floating tag first; on 404 fall back to max immutable <c>plugin-v{channel}.*</c> / <c>app-v{channel}.*</c>.
    /// </summary>
    public async Task<GitHubReleaseInfo?> GetChannelLatestAsync(
        string ownerRepo,
        string channel,
        string tagPrefix,
        CancellationToken cancellationToken = default)
    {
        var floating = $"{tagPrefix}{channel}-latest";
        var release = await GetReleaseByTagAsync(ownerRepo, floating, cancellationToken).ConfigureAwait(false);
        if (release is not null && !release.Prerelease)
            return release;

        var prefix = $"{tagPrefix}{channel}.";
        var releases = await ListReleasesAsync(ownerRepo, cancellationToken).ConfigureAwait(false);
        return releases
            .Where(r => !r.Prerelease
                        && r.TagName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && !r.TagName.EndsWith("-latest", StringComparison.OrdinalIgnoreCase)
                        && !r.TagName.Contains('-', StringComparison.Ordinal)) // skip -beta etc beyond channel
            .Select(r => (Release: r, Version: r.ParsedVersion ?? TryParseTagVersion(r.TagName, tagPrefix)))
            .Where(x => x.Version is not null)
            .OrderByDescending(x => x.Version)
            .Select(x => x.Release)
            .FirstOrDefault();
    }

    public async Task DownloadAssetAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var tempPath = destinationPath + ".tmp";

        using var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var local = File.Create(tempPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;
            if (total is > 0)
                progress?.Report(readTotal / (double)total.Value);
        }

        await local.FlushAsync(cancellationToken).ConfigureAwait(false);
        local.Close();

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);
        File.Move(tempPath, destinationPath);
        progress?.Report(1);
    }

    private static Version? TryParseTagVersion(string tagName, string tagPrefix)
    {
        if (!tagName.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var rest = tagName[tagPrefix.Length..];
        return Version.TryParse(rest, out var v) ? v : null;
    }

    private static GitHubReleaseInfo? Map(ReleaseDto? dto)
    {
        if (dto is null)
            return null;

        return new GitHubReleaseInfo
        {
            TagName = dto.TagName ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Body = dto.Body ?? string.Empty,
            HtmlUrl = dto.HtmlUrl ?? string.Empty,
            Prerelease = dto.Prerelease,
            Assets = (dto.Assets ?? [])
                .Select(a => new GitHubReleaseAsset
                {
                    Name = a.Name ?? string.Empty,
                    BrowserDownloadUrl = a.BrowserDownloadUrl ?? string.Empty,
                    Size = a.Size,
                    Digest = a.Digest,
                })
                .ToList(),
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _http.Dispose();
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<AssetDto>? Assets { get; set; }
    }

    private sealed class AssetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}
