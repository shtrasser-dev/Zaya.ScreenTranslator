using System.Reflection;
using System.Text.Json;

namespace Zaya.ScreenTranslator.Impl.Shared.Update;

public static class BuiltinPluginCatalog
{
    private static readonly Lazy<IReadOnlyList<BuiltinPluginEntry>> _entries = new(Load);

    public static IReadOnlyList<BuiltinPluginEntry> Entries => _entries.Value;

    private static IReadOnlyList<BuiltinPluginEntry> Load()
    {
        var asm = typeof(BuiltinPluginCatalog).Assembly;
        const string resourceName = "Zaya.ScreenTranslator.Impl.Shared.Update.builtin-plugins.json";

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var list = JsonSerializer.Deserialize<List<BuiltinPluginEntry>>(stream)
            ?? [];
        return list.AsReadOnly();
    }
}
