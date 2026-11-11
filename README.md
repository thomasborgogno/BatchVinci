# BatchVinci

A Windows desktop app (WinUI 3) for batch-editing the texts (Text+ / Fusion Titles) of a **DaVinci Resolve** project exported as `.drp`, without opening Resolve.

Pick a `.drp`, mark the labels you want to change as *parameters*, fill in the new values and export a **new** `.drp`. The original project is never modified.

## Download

Get the latest build from the [Releases page](https://github.com/thomasborgogno/BatchVinci/releases/latest): download the zip for your architecture (`win-x64` or `win-arm64`), extract it and run `BatchVinci.exe`. All versions are listed on the [Releases](https://github.com/thomasborgogno/BatchVinci/releases) page.

## Features

- **DRP scan**: finds every text in the Fusion compositions of timelines and compound clips (`SeqContainer/`), along with clip, sequence, template and position.
- **Configurations**: for each project, saves which labels are parameters, their name and value. Configurations can be reopened and reused.
- **Fill page**: one field per parameter, a reset button to restore the original text, and automatic saving (500 ms debounce).
- **Export**: writes a new `.drp` with the replaced texts and reports how many labels were updated.
- **Relocation**: if the `.drp` is moved you can point to its new location; parameters no longer present in the project are reported (matched by key, falling back to the original text).

## Solution layout

| Project | Description |
|---|---|
| `BatchVinci` | WinUI 3 app (`net9.0-windows10.0.19041.0`, Windows App SDK 1.8, self-contained, unpackaged). Pages: Home, Configs, Select, Fill. |
| `BatchVinci.Core` | UI-independent `net9.0` library: DRP reading/writing and configuration storage. |

Main files in `BatchVinci.Core`:

- `DrpProject.cs`: `Scan` (lists the texts) and `Export` (creates a new drp with the texts replaced).
- `CompositionCodec.cs`: decodes and re-encodes the `<CompositionBA>` block (hex → zlib → binary `0_data` map → header + zlib → Fusion script).
- `FusionText.cs`: converts between plain text and Lua/Fusion strings (`\n`, `\"`, `\\`, …).
- `ProjectConfig.cs`: `ProjectConfig` / `ParameterConfig` models and `ConfigStore`.

## How it works

A `.drp` is a zip archive of XML files. Each Fusion composition is stored in `<CompositionBA>` as hex-encoded compressed data. `Export` copies the zip entry by entry and rewrites only the compositions whose texts changed, leaving everything else untouched. Unrecognized compositions are skipped.

Each text is identified by a key of the form `entry|clipId|compositionIndex|textIndex`.

## Local data

Configurations are JSON files in `%LOCALAPPDATA%\BatchVinci\configs\`.

## Requirements and build

- Windows 10 1809 (10.0.17763) or later
- .NET 9 SDK
- Platforms: x86, x64, ARM64

```powershell
dotnet build BatchVinci.sln -c Release -p:Platform=x64
```

To run the app: `dotnet run --project BatchVinci -p:Platform=x64`, or open `BatchVinci.sln` in Visual Studio 2022.

## Releases

Releases are created by pushing a git tag in the form `vMAJOR.MINOR.PATCH` (e.g. `v1.0.0`, or `v1.0.0-beta.1` for a pre-release). The `.github/workflows/release.yml` pipeline builds the app for x64 and ARM64 using the version from the tag and publishes the zips to GitHub Releases.

```bash
git tag v1.0.0
git push origin v1.0.0
```

## License

[MIT](LICENSE)
