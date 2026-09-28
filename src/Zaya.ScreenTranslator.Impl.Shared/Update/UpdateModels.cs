namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public sealed class HostUpdateInfo
{
    public bool UpdateAvailable { get; init; }
    public Version? RemoteVersion { get; init; }
    public string? ReleaseHtmlUrl { get; init; }
    public string? ReleaseName { get; init; }
}

public static class HostChannel
{
    /// <summary>Host app MAJOR.MINOR (used as plugin updater fallback channel).</summary>
    public static string Current
    {
        get
        {
            var ver = AssemblyVersion;
            return $"{ver.Major}.{ver.Minor}";
        }
    }

    public static Version AssemblyVersion =>
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version
        ?? typeof(HostChannel).Assembly.GetName().Version
        ?? new Version(0, 4, 0, 0);

    public static Version ThreePartAssemblyVersion
    {
        get
        {
            var v = AssemblyVersion;
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }
}
