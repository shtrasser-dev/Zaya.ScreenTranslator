# Versioning (Zaya.ScreenTranslator)

## Host

| Artifact | Rule |
|----------|------|
| App `Version` | `Major.HostMinor.HostPatch` from `Directory.Build.props` (`ZayaHostVersionMinor` / `Patch`) → currently `1.1.2` |
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
