# DG-Lab EXP BodySync

English | [简体中文](./README.zh-CN.md)

<p align="center">
  <strong>DG-Lab EXP BodySync / DG-Lab EXP 体感同步</strong><br>
  A BepInEx 5 mod that maps in-game damage and body conditions to DG-Lab Socket v2 output.
</p>

<p align="center">
  <img alt="BepInEx" src="https://img.shields.io/badge/BepInEx-5.4.23.5-blue">
  <img alt=".NET Framework" src="https://img.shields.io/badge/.NET%20Framework-4.8-purple">
  <img alt="Unity" src="https://img.shields.io/badge/Unity-2022.3.62f3-black">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%20x64-lightgrey">
</p>

`DG-Lab EXP BodySync` is a BepInEx 5 mod for `Casualties Unknown`. It maps in-game damage and body conditions to DG-Lab Socket v2 A/B channel output.

It is not a simple damage trigger. It continuously samples body states such as pain, shock, consciousness, bleeding, hypoxia, infection, and temperature, then mixes output based on severity, body-part bindings, and channel strength limits.

## Safety Warning

> [!WARNING]
> This mod can drive real DG-Lab output from game events. Treat it like a real physical stimulation tool, not just a visual mod.

- Start with very low A/B strength limits. Increase slowly only after you know what the current settings feel like.
- Do not test high values just because the game looks intense. Device output is real, and repeated spikes can feel much stronger than expected.
- Stop immediately if you feel pain, numbness, dizziness, panic, skin irritation, or anything that feels wrong.
- Do not use this while tired, drunk, sick, distracted, or unable to stop the session quickly.
- Keep the DG-Lab app or device stop controls reachable. Do not rely only on the in-game menu.
- Game state can change suddenly. Use it within your own limits.
- You are responsible for your own device, body, settings, and safety.

In plain words: use less than you think you can handle. The goal is immersion, not proving endurance.

## Target Environment

| Item | Value |
| --- | --- |
| Game | `Casualties Unknown Demo` |
| Mod loader | BepInEx `5.4.23.5` |
| Runtime | .NET Framework `4.8` |
| Unity | `2022.3.62f3` |
| Platform | Windows x64 |
| DG-Lab protocol | Socket v2 |

## Features

- Embedded DG-Lab WebSocket backend inside the game process.
- Local QR PNG generation for the DG-Lab app.
- External backend mode for advanced network setups.
- Plugin-owned IMGUI control menu with no translator dependency.
- Compact draggable status overlay.
- A/B channel strength limits.
- Configurable 15-limb body-part binding for A/B channels.
- Runtime output scaling from game damage and ongoing body conditions.
- Offline simulation display when no DG-Lab device is connected.

## Project Structure

The runtime is split into three assemblies:

| Assembly | Description |
| --- | --- |
| `DGLab.Core.dll` | Socket v2 DTOs, protocol helpers, embedded/external WebSocket transport, `DGLabClient` |
| `DGLab.Game.dll` | Game hooks, body scoring, output state, strength envelope, wave routing |
| `DGLab.BepInEx.dll` | BepInEx lifecycle, config, standalone IMGUI, status overlay, QR generation, composition |

## Installation

Copy the runtime files to:

```text
<GameDir>\BepInEx\plugins\DG-Lab\
```

Required files:

- `DGLab.BepInEx.dll`
- `DGLab.Core.dll`
- `DGLab.Game.dll`
- `QRCoder.dll`
- `websocket-sharp.dll`

Do not copy config files into the plugin folder. Config files belong under `BepInEx\config`.

## Configuration

Main BepInEx config:

```text
<GameDir>\BepInEx\config\dglab.socket.cfg
```

Common entries:

| Entry | Description |
| --- | --- |
| `General/Enabled` | Enables or disables the mod |
| `Network/UseEmbeddedServer` | Uses the embedded WebSocket backend |
| `Network/EmbeddedServerAddress` | Embedded backend bind address |
| `Network/EmbeddedServerPort` | Embedded backend bind port |
| `Network/EmbeddedTerminalId` | Embedded terminal ID |
| `Network/RefreshEmbeddedTerminalIdOnStart` | Refreshes terminal ID on startup |
| `Network/InvalidateQrOnDisconnect` | Invalidates QR after disconnect |
| `Network/ServerUrl` | External backend URL |
| `Network/QrWebSocketUrl` | WebSocket URL used in the QR code |
| `Network/EnableQrOutput` | Enables QR image output |
| `UI/EnableMenu` | Enables the main menu |

Advanced config:

```text
<GameDir>\BepInEx\config\dglab.settings.cfg
```

Important entries:

- `Control/StrengthA` and `Control/StrengthB` are maximum runtime strength limits. Events scale proportionally up to these values.
- `Control/EnableDamageHook` enables game damage and body-state hooks. Runtime hook changes may require a restart.
- `Wave/EnableWaveEvents` enables event waveform routing.
- `Wave/EnableConditionMixer` enables continuous body-state sampling and mixed condition waves.
- `Binding/ChannelABodyParts` maps game body parts to DG-Lab channel A.
- `Binding/ChannelBBodyParts` maps game body parts to DG-Lab channel B.
- `UI/MenuToggleKey` uses a single key only. Alt combinations are not used.
- `UI/MiniOverlayEnabled` controls the compact status overlay.

Body binding supports 15 limb indices:

| Index | Body part |
| --- | --- |
| `0` | `Head` |
| `1` | `UpTorso` |
| `2` | `DownTorso` |
| `3` | `ArmFUpper` |
| `4` | `ArmFLower` |
| `5` | `HandF` |
| `6` | `ArmBUpper` |
| `7` | `ArmBLower` |
| `8` | `HandB` |
| `9` | `LegFUpper` |
| `10` | `LegFLower` |
| `11` | `FootF` |
| `12` | `LegBUpper` |
| `13` | `LegBLower` |
| `14` | `FootB` |

Group names are also supported: `ArmF` means `3,4,5`, `ArmB` means `6,7,8`, `LegF` means `9,10,11`, and `LegB` means `12,13,14`.

## QR and Network

Embedded backend mode starts a WebSocket server inside the game process.

Default backend:

```text
0.0.0.0:9999
```

Local QR image:

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

The main menu can refresh the backend, refresh the QR, open the QR PNG, and disconnect. The mini overlay is intentionally status-only to avoid accidental operation.

If the QR uses a wrong IP such as a virtual adapter address, set `Network/EmbeddedServerAddress` manually to your LAN address:

```text
192.168.1.23
```

## UI

- `F10` toggles the main menu.
- The main menu contains controls, QR actions, channel binding, language, limits, and runtime settings.
- The mini overlay is a draggable status view. It shows mode, device state, live strength, configured limits, recent output, and active condition layers.
- QR actions are only in the main menu to avoid accidental operation from the overlay.
- Closing or hiding the overlay is controlled from the main menu setting `Mini Overlay`.

## Build

1. Install a .NET SDK capable of building `net48` projects.
2. Put required reference DLLs under `BepInExPlugin\lib` when needed.
3. This repo expects all third-party DLLs to be stored in-repo under `BepInExPlugin\lib` and referenced by the `.csproj` files. No external download step is required during build once the repo is complete.
4. If you are preparing a clean GitHub release, ensure the following DLLs are present under `BepInExPlugin\lib`:

```text
QRCoder.dll
websocket-sharp.dll
Newtonsoft.Json.dll
BepInEx.dll
0Harmony.dll
UnityEngine*.dll
Assembly-CSharp.dll
```
5. From the project root, run:

```powershell
dotnet build "BepInExPlugin\DGLab.BepInEx.csproj" -c Release
```

6. Deploy output from:

```text
BepInExPlugin\bin\Release\net48\
```

To:

```text
<GameDir>\BepInEx\plugins\DG-Lab\
```

## Third-Party Credits

This project uses or integrates with the following third-party projects:

- BepInEx: Unity mod loader. Used as the plugin runtime.
- HarmonyX / Harmony: runtime patching library used through BepInEx.
- QRCoder: QR code generation library. Used to generate the local DG-Lab scan PNG.
- websocket-sharp: WebSocket server/client library. Used for DG-Lab Socket transport.
- Newtonsoft.Json: JSON serialization for DG-Lab Socket messages.
- DG-Lab Socket protocol and DG-Lab app/device ecosystem: this project only integrates with public protocol behavior and does not own DG-Lab.
- Casualties Unknown Demo: target game. This project is an unofficial mod and is not affiliated with the game developer.
