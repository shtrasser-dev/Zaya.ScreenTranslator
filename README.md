# Zaya.ScreenTranslator

Real-time on-screen text translator for Windows. Captures text from games, movies, and applications, recognizes it with OCR, translates it, and displays the result as an overlay.

## Features

- Capture text from any region of the screen
- OCR using multiple engines (OneOCR, Tesseract, Windows.Media.Ocr)
- Translation via DeepL, LibreTranslate, Yandex
- Real-time overlay rendering
- Per-application profiles

## Architecture

- **Zaya.ScreenTranslator** — WPF application: pipeline, overlay, UI

## Dependencies

- [Zaya.Primitives](https://github.com/shtrasser-dev/Zaya.Primitives)
- [Zaya.Screenshot](https://github.com/shtrasser-dev/Zaya.Screenshot)
- [Zaya.OCR](https://github.com/shtrasser-dev/Zaya.OCR)

## License

MIT
