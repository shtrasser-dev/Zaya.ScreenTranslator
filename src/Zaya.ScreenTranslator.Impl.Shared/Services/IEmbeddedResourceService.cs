namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IEmbeddedResourceService
{
    Stream GetStream(string name);
}
