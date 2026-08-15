using Zaya.Logging.Services;
using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.Translator.Services;
using Zaya.TranslatorCache.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services.Impl;

/// <summary>
/// Built-in cache engine that returns the inner translator session unchanged.
/// </summary>
public sealed class NoTranslatorCacheService : ITranslatorCacheService
{
    public const string EngineIdValue = ScreenTranslatorSettingDescriptors.TranslatorCacheNone;

    private bool _disposed;

    public NoTranslatorCacheService()
        : this(EmptyLoggingWrapper.Instance)
    {
    }

    public NoTranslatorCacheService(ILoggingWrapper loggingWrapper)
    {
        _ = loggingWrapper;
    }

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.Instance.GetString(key, culture));

    public string EngineId => EngineIdValue;

    public LocalizedString DisplayName => Loc(LocalizationConstants.Settings.TranslatorCacheNone);

    public LocalizedString Description => Loc(LocalizationConstants.Settings.TranslatorCacheNone);

    public bool IsAvailable => true;

    public IReadOnlyList<SettingDescriptor> Settings { get; } = [];

    public Task<ITranslatorSession> WrapSessionAsync(
        ITranslatorSession inner,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(inner);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(inner);
    }

    public Task<ITranslatorSession> WrapSessionAsync(
        ITranslatorSession inner,
        IReadOnlyDictionary<string, object> engineSettings,
        CancellationToken cancellationToken = default)
        => WrapSessionAsync(inner, cancellationToken);

    public void Dispose()
    {
        _disposed = true;
    }
}
