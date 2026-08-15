using System.Diagnostics.CodeAnalysis;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Reads and writes JSON files with shared host serializer options.
/// </summary>
public interface IJsonConfigurationService
{
    /// <summary>
    /// Deserializes <typeparamref name="T"/> from <paramref name="path"/>.
    /// Returns <c>null</c> when the file is missing or cannot be deserialized.
    /// </summary>
    T Read<T>(string path);

    T Read<T>(Stream stream);

    bool TryRead<T>(string path, [NotNullWhen(true)] out T? value);

    /// <summary>
    /// Serializes <paramref name="value"/> and writes atomically to <paramref name="path"/>.
    /// </summary>
    void Write<T>(string path, T value);
}
