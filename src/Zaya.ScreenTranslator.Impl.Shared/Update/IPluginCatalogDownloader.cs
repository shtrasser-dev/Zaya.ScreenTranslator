namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IPluginCatalogDownloader
{
    Task DownloadCatalogEntryAsync(
        BuiltinPluginEntry entry,
        string fallbackChannel,
        List<string> downloaded,
        CancellationToken cancellationToken);

    Task UpdateFromReleasesAsync(
        IReadOnlyList<BuiltinPluginEntry> catalog,
        string fallbackChannel,
        bool updateOptional,
        List<string> downloaded,
        IProgress<string>? status,
        CancellationToken cancellationToken);
}
