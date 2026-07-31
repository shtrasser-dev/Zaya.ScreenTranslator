using Zaya.Primitives;
using Zaya.ScreenTranslator.Impl.Shared.Constants;
using Zaya.ScreenTranslator.Impl.Shared.Models;
using Zaya.Translator.Services;

namespace Zaya.ScreenTranslator.Impl.Shared.Services;

/// <summary>
/// Built-in translator that returns OCR text unchanged (for copy without translation).
/// Lives in the host assembly — not a plugin DLL.
/// </summary>
public sealed class NoTranslationTranslatorService : ITranslatorService
{
    public const string EngineIdValue = ScreenTranslatorSettingDescriptors.TranslatorNone;

    private bool _disposed;

    private static LocalizedString Loc(string key)
        => new(key, culture => Properties.Resources.Instance.GetString(key, culture));

    public string EngineId => EngineIdValue;

    public LocalizedString DisplayName => Loc(LocalizationConstants.Settings.TranslatorNone);

    public LocalizedString Description => Loc(LocalizationConstants.Settings.TranslatorNone);

    public bool IsAvailable => true;

    public IReadOnlyList<SettingDescriptor> Settings { get; } = [];

    public Task<ITranslatorSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ITranslatorSession>(new NoTranslationTranslatorSession());
    }

    public Task<ITranslatorSession> CreateSessionAsync(
        IReadOnlyDictionary<string, object> engineSettings,
        CancellationToken cancellationToken = default)
        => CreateSessionAsync(cancellationToken);

    public void Dispose()
    {
        _disposed = true;
    }

    private sealed class NoTranslationTranslatorSession : ITranslatorSession
    {
        public Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(text);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(text);
        }

        public Task<IReadOnlyList<string>> TranslateAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(texts);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(texts);
        }

        public void Dispose() { }
    }
}
