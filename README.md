# Zaya.ScreenTranslator

Real-time on-screen text translator for Windows. Captures a window or region, runs OCR, optionally translates, and shows the result in a text window or overlay.

## Version

**1.0.3** — Primitives compatibility channel `1.0`. See [versioning](docs/versioning.md).

Host release tags: `app-v1.0.3` / `app-v1.0-latest`. Plugin update channel: `plugin-v1.0-latest`.

## Features

- Capture via Windows Graphics Capture plugin
- OCR (OneOCR, Windows Media OCR) + proximity text layout
- Translation plugins (Google, Yandex) or built-in “No translation”
- Overlay / text-window display modes
- Per-application profiles
- Plugin updater via GitHub Releases; host opens the release page for app updates (does not self-replace the exe)

## Architecture

- **Impl.Windows** — Windows host (`Zaya.ScreenTranslator.exe`)
- **Impl.Shared** — Avalonia UI, pipeline, plugin loader, updater
- **Layout** / **Layout.Impl** — overlay layout abstractions + default Windows overlay engine (ships with the host)

## Dependencies

Pinned in `Directory.Build.props` (currently **1.0.0**):

- [Zaya.Primitives](https://github.com/shtrasser-dev/Zaya.Primitives)
- [Zaya.Screenshot](https://github.com/shtrasser-dev/Zaya.Screenshot)
- [Zaya.OCR](https://github.com/shtrasser-dev/Zaya.OCR)
- [Zaya.Translator](https://github.com/shtrasser-dev/Zaya.Translator)

## Build

```bat
build.cmd
```

Publishes a single-file host and packs it into `out\Zaya.ScreenTranslator.zip` (contains `Zaya.ScreenTranslator.exe`). Also writes `out\version.txt` / `out\channel.txt`.

## Publish

GitHub Actions workflow **Publish** (`workflow_dispatch` only). Bump host version in `Directory.Build.props`, push, then run the workflow manually. Creates/replaces `app-v{version}` and `app-v{channel}-latest` with `Zaya.ScreenTranslator.zip`.

## Local plugins

```bat
build-plugins.cmd
```

Builds sibling OCR / Screenshot / Translator plugin zips and copies them into `%AppData%\Zaya\ScreenTranslator\plugins`.

## License

MIT
