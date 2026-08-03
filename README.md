# Zaya.ScreenTranslator

Real-time on-screen text translator for Windows. Captures a window or region, runs OCR, optionally translates, and shows the result in a text window or overlay.

## Version

**1.0.8** — Primitives compatibility channel `1.0`. See [versioning](docs/versioning.md).

Host release tags: `app-v1.0.8` / `app-v1.0-latest`. Plugin channels: `plugin-{Interface}-v{channel}-latest` (e.g. `plugin-Zaya.OCR-v1.0-latest`). Layout plugin (ships with host): `1.0.0.2`.

## Features

- Capture via Windows Graphics Capture plugin
- Per-profile capture / ignore regions (editor on the main window)
- OCR (OneOCR, Windows Media OCR) + proximity text layout
- Translation plugins (Google, Yandex) or built-in “No translation”
- In-memory translation cache plugin
- Overlay / text-window display modes
- Text filter rules (skip / strip)
- UI languages: en, ru, de, fr, ja, ko, pl, pt, tr, uk, zh-Hans
- Per-application profiles
- Plugin updater via GitHub Releases; host opens the release page for app updates (does not self-replace the exe)

## Architecture

- **Impl.Windows** — Windows host (`Zaya.ScreenTranslator.exe`)
- **Impl.Shared** — Avalonia UI, pipeline, plugin loader, updater
- **Layout** / **Layout.Impl** — overlay layout abstractions + default Windows overlay engine (ships with the host)

## Dependencies

Pinned in `Directory.Build.props`:

| Package | Version |
|---------|---------|
| [Zaya.Primitives](https://github.com/shtrasser-dev/Zaya.Primitives) | 1.0.0 |
| [Zaya.Screenshot](https://github.com/shtrasser-dev/Zaya.Screenshot) | 1.0.0 |
| [Zaya.OCR](https://github.com/shtrasser-dev/Zaya.OCR) | 1.0.0 |
| [Zaya.Translator](https://github.com/shtrasser-dev/Zaya.Translator) | 1.1.0 |
| [Zaya.TranslatorCache](https://github.com/shtrasser-dev/Zaya.Translator) | 1.0.0 |

## Build

```bat
build.cmd
```

Publishes a single-file host and packs it into `out\Zaya.ScreenTranslator.zip` (contains `Zaya.ScreenTranslator.exe`). Also writes `out\version.txt` / `out\channel.txt`.

## Publish

GitHub Actions workflow **Publish** (`workflow_dispatch` only). Bump host version in `Directory.Build.props`, push, then run the workflow manually (optional **changelog** input becomes the GitHub Release notes). Creates/replaces `app-v{version}` and `app-v{channel}-latest` with `Zaya.ScreenTranslator.zip`.

## Local plugins

```bat
build-plugins.cmd
```

Builds sibling OCR / Screenshot / Translator (+ cache) plugin zips and copies them into `%AppData%\Zaya\ScreenTranslator\plugins`.

## License

MIT
