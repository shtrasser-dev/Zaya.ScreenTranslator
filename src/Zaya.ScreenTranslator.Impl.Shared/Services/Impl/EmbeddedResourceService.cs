using Zaya.Logging.Models;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

public sealed class EmbeddedResourceService : IEmbeddedResourceService
{
    [Log(LogLevel.Debug, LogParameters = true)]
    public Stream GetStream(string name)
    {
        var asm = typeof(EmbeddedResourceService).Assembly;
        return asm.GetManifestResourceStream(name) ?? throw new FileNotFoundException(name);
    }
}
