# Versioning (Zaya.ScreenTranslator)

## Host

| Artifact | Rule |
|----------|------|
| App `Version` (`Directory.Build.props`) | `app-v{ver}` / `app-v{channel}-latest` |
| Primitives channel | `MAJOR.MINOR` of Primitives (= plugin `primitivesChannel`) |

Host does not self-replace the exe; it opens the GitHub release page when a newer `app-v*` exists.

## Plugins (three axes)

| Axis | Meaning |
|------|---------|
| `primitivesChannel` | Ecosystem / floating tag `plugin-v{channel}-latest` |
| `interfaceVersion` | Must equal host-shipped `Zaya.OCR` / `Zaya.Translator` / `Zaya.Screenshot` assembly version |
| `pluginVersion` | Per-engine zip; updater compares per asset from release body (`asset.zip=x.y.z`) |

`PluginLoader` skips zips whose `interfaceVersion` does not match the host interface assembly.
