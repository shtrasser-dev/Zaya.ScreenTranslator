using Zaya.ScreenTranslator.Impl.Shared.Logging;

namespace Zaya.ScreenTranslator.Impl.Shared.Logging;

public interface ILogOptionsStore
{
    string GetConfigPath();

    string GetLogsDirectory();

    LogOptions LoadOrCreate();
}
