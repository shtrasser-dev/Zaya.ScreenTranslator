# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
History starts at the current release line; older releases are not backfilled.

## [Unreleased]

### Changed

- Target **Zaya.Primitives / OCR / Screenshot / Translator / TranslatorCache `2.0.0`** (Logging stays `1.0.0`). Host `2.0.0`, layout `2.0.0` / `2.0.0.0`; plugin channels `plugin-*-v2.0-latest`.
- Settings types use `Zaya.Primitives.Settings`; OCR layout/result models use `Zaya.Primitives` / `Zaya.Primitives.OCR`.
- Default capture engine id: `graphics-capture` → `windows-graphics-capture`.
- Overlay layout public API: `CreateSessionAsync(settings, translate?)` and `PresentAsync(OverlayPresentRequest)` (`ITextResult` + origin + optional OCR for debug). Host no longer maps overlay frames; join/translate/wrap stay in Layout.Impl.

## [1.2.1] - 2026-08-16

### Changed

- File log line format is configurable in `log.json` via named placeholders (`{timestamp:…}`, `{level}`, `{category}`, `{message}`, `{newline}`, `{exception}`).
- Default log level is `Information` (was `Debug`).

## [1.2.0] - 2026-08-15

### Added

- Optional file logging via `%AppData%\Zaya\ScreenTranslator\log.json` (level, debug/file sinks, rolling size/count).
- Overlay: snap nearly-horizontal lines to the horizon (default `10` degrees).

### Changed

- Main window layout: wider min width, labels above fields, settings toggle beside status.
- Settings UI: clearer overlay units, engine picker beside each module, more consistent columns.
- Capture regions editor can open without a selected window when regions already exist.
- Missing engine id in a profile falls back to the first available engine.
- Plugins with missing/invalid `plugin.json` are skipped instead of partially loading.

## [1.1.3] - 2026-08-11

### Added

- Main-window theme toggle.

### Changed

- Soften sun emoji opacity on the theme toggle.
