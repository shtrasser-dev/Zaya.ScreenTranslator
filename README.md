# Zaya.ScreenTranslator

Real-time on-screen text translator for Windows. Captures a window or region, runs OCR, optionally translates, and shows the result in a text window or overlay.

## Version

**0.4.0** — Primitives compatibility channel `0.4`. See [versioning](docs/versioning.md).

## Features

- Capture via Windows Graphics Capture plugin
- OCR (OneOCR, Windows Media OCR) + proximity text layout
- Translation plugins (Google, Yandex) or built-in “No translation”
- Overlay / text-window display modes
- Per-application profiles
- Plugin updater via GitHub Releases (`plugin-v0.4-latest`); host opens release page for app updates

## Architecture

- **Impl.Windows** — Windows host (`Zaya.ScreenTranslator.Impl.Windows.exe`)
- **Impl.Shared** — Avalonia UI, pipeline, plugin loader, updater
- **Impl.Layout** — overlay layout engine (ships with the host)

## Dependencies

- [Zaya.Primitives](https://github.com/shtrasser-dev/Zaya.Primitives) 0.4.0
- [Zaya.Screenshot](https://github.com/shtrasser-dev/Zaya.Screenshot)
- [Zaya.OCR](https://github.com/shtrasser-dev/Zaya.OCR)
- [Zaya.Translator](https://github.com/shtrasser-dev/Zaya.Translator)

## Local plugins

```bat
build-plugins.cmd
```

Copies sibling repo plugin zips into `%AppData%\Zaya\ScreenTranslator\plugins`.

## License

MIT
