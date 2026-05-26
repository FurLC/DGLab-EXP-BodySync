# DG-Lab EXP BodySync - BepInEx Plugin

English | [简体中文](./README.zh-CN.md)

This folder contains the BepInEx 5 plugin used by `DG-Lab EXP BodySync`.

For installation, configuration, safety notes, and in-game usage, see the root documentation:

- [README.md](../README.md)
- [README.zh-CN.md](../README.zh-CN.md)

## What Is Here

| Path | Purpose |
| --- | --- |
| `DGLab.BepInEx.csproj` | BepInEx plugin project |
| `src/` | Plugin UI, configuration, QR, game hooks, body routing, and wave output code |
| `lib/` | Reference libraries used by the project |

## Build From Source

The project targets `.NET Framework 4.8` / `net48`.

Run from the repository root:

```powershell
dotnet build "BepInExPlugin\DGLab.BepInEx.csproj" -c Release
```

Build output is written to:

```text
BepInExPlugin\bin\Release\net48\
```

## Output Files

The main runtime files produced by the build are:

- `DGLab.BepInEx.dll`
- `DGLab.Core.dll`
- `DGLab.Game.dll`
- `QRCoder.dll`
- `websocket-sharp.dll`

`Newtonsoft.Json.dll` is also required if it is not already available in the target runtime.

## Notes

- This is the game-specific BepInEx implementation.
- The standalone Unity wrapper is documented separately in [UnityPlugin/README.md](../UnityPlugin/README.md).
- Generated BepInEx config files are created by the game at runtime under `BepInEx/config`.
