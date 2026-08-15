using Avalonia.Media.Imaging;
using System.Diagnostics;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

public interface IProcessIconLoader
{
    Bitmap? GetIcon(Process process);

    Bitmap? GetIcon(string? executablePath);
}
