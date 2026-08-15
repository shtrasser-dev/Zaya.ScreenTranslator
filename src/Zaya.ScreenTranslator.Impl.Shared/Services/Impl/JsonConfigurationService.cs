using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Zaya.Logging.Models;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Converters;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Default <see cref="IJsonConfigurationService"/> using shared indented / case-insensitive options
/// and <see cref="SettingsJsonConverter"/> for profile dictionaries.
/// </summary>
[Log(LogLevel.Debug, LogParameters = true)]
public sealed class JsonConfigurationService : IJsonConfigurationService
{
    /// <summary>
    /// Shared options for file JSON and rare stream deserializations (embedded templates).
    /// </summary>
    private static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new SettingsJsonConverter() },
    };

    public T Read<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new ArgumentNullException();
    }

    public T Read<T>(Stream stream)
    {
        return JsonSerializer.Deserialize<T>(stream, Options) ?? throw new ArgumentNullException();
    }

    public bool TryRead<T>(string path, [NotNullWhen(true)] out T? value)
    {
        try
        {
            value = Read<T>(path) ?? throw new ArgumentNullException();
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public void Write<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tmp = string.Concat(path, FileExtensionConstants.Tmp);
        var json = JsonSerializer.Serialize(value, Options);
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}
