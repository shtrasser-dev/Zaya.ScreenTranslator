namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public interface IHostVersionChecker
{
    Task<HostUpdateInfo> CheckAsync(CancellationToken cancellationToken = default);

    void OpenReleasePage(string htmlUrl);
}
