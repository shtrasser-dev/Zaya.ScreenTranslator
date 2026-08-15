# Versioning (Zaya.ScreenTranslator)

## Host

| Artifact | Rule |
|----------|------|
| App `Version` | `Major.HostMinor.HostPatch` from `Directory.Build.props` (`ZayaHostVersionMinor` / `Patch`) → currently `1.2.0` |
| Release tags | Immutable `app-v{ver}`; optional floating `app-v{channel}-latest` (`channel` = `MAJOR.MINOR`); asset `Zaya.ScreenTranslator-{ver}.zip` |

Host does not self-replace the exe; it opens the GitHub release page when a newer immutable `app-v*` exists (newest version across all channels).

## Layout plugin (in-repo)

| Package | Properties | Version |
|---------|------------|---------|
| **Zaya.ScreenTranslator.Layout** | `ZayaVersionInterface` | `Major.Interface.0` → `1.2.0` |
| **Zaya.ScreenTranslator.Layout.Impl** | `ZayaVersionImpMajor` / `ImpMinor` | `Major.Interface.ImpMajor.ImpMinor` → `1.2.0.0` |

Same rules as OCR / Screenshot / Translator plugins. Major comes from `ZayaPrimitivesVersion`.

## External plugins

| Axis | Meaning |
|------|---------|
| `interfaceVersion` | Must equal host-shipped `Zaya.OCR` / `Zaya.Translator` / `Zaya.Screenshot` / layout interface assembly version |
| `pluginVersion` | Per-engine zip; updater compares per asset |
| `updateChannel` | Interface `MAJOR.MINOR` → floating tag `plugin-{interface}-v{channel}-latest` |

`PluginLoader` skips zips whose `interfaceVersion` does not match the host interface assembly.

## Changelog

Use root [`CHANGELOG.md`](../CHANGELOG.md) ([Keep a Changelog](https://keepachangelog.com/)):

1. While working, append notes under `## [Unreleased]`.
2. Run the Publish workflow — GitHub Release body is taken from `[Unreleased]` (plus release metadata). There is no changelog input on the action.
3. After a successful publish, move that block to a dated section, e.g. `## [1.2.0] - 2026-08-15`, and leave `[Unreleased]` empty for the next cycle.

Do not backfill older releases; history starts from the current line.

## Bumping (host)

1. Raise `ZayaHostVersionMinor` / `ZayaHostVersionPatch` in `Directory.Build.props`.
2. Update `CHANGELOG.md` `[Unreleased]`, then run `build.cmd` / Publish workflow.
