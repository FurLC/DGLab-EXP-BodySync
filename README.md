# DG-Lab EXP BodySync

English | [简体中文](./README.zh-CN.md)

<p align="center">
  <strong>DG-Lab EXP BodySync</strong><br>
  A BepInEx 5 mod that maps in-game damage and body conditions to DG-Lab Socket v2 output.
</p>

<p align="center">
  <img alt="BepInEx" src="https://img.shields.io/badge/BepInEx-5.4.23.5-blue">
  <img alt=".NET Framework" src="https://img.shields.io/badge/.NET%20Framework-4.8-purple">
  <img alt="Unity" src="https://img.shields.io/badge/Unity-2022.3.62f3-black">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%20x64-lightgrey">
</p>

`DG-Lab EXP BodySync` is a BepInEx 5 mod for `Casualties Unknown`. It maps in-game damage and body conditions to DG-Lab Socket v2 A/B channel output.

It is not a simple damage trigger. It continuously samples body states — pain, shock, consciousness, bleeding, hypoxia, infection, temperature — and mixes output based on severity, body-part bindings, and channel strength limits.

## Safety Warning

> [!WARNING]
> This mod drives real DG-Lab output from game events. Treat it like a real physical stimulation tool, not a visual mod.

- Start with very low A/B strength limits. Increase slowly only after confirming what the current settings feel like.
- Do not test high values just because the game looks intense. Device output is real, and repeated spikes can feel much stronger than expected.
- Stop immediately if you feel pain, numbness, dizziness, panic, skin irritation, or anything wrong.
- Do not use while tired, drunk, sick, distracted, or unable to stop the session quickly.
- Keep the DG-Lab app or device stop controls reachable at all times. Do not rely only on the in-game menu.
- Game state can change suddenly. Use within your own limits.
- You are responsible for your device, body, settings, and safety.

Use less than you think you can handle. The goal is immersion, not proving endurance.

## Target Environment

| Item | Value |
| --- | --- |
| Game | `Casualties Unknown` |
| Mod loader | BepInEx `5.4.23.5` |
| Runtime | .NET Framework `4.8` |
| Unity | `2022.3.62f3` |
| Platform | Windows x64 |
| DG-Lab protocol | Socket v2 |

## Features

- Embedded DG-Lab WebSocket backend inside the game process.
- Local QR PNG generation for the DG-Lab app to scan.
- External backend mode for advanced network setups, with Official Socket and Third-Party Controller profiles.
- Auto backend selection: tries external first, falls back to embedded if unavailable.
- Plugin-owned IMGUI control menu — no translator dependency.
- Compact draggable status overlay.
- A/B channel strength limits with in-menu sliders.
- Configurable 15-limb body-part binding for A/B channels.
- Runtime output scaling from game damage and ongoing body conditions.
- Offline simulation display when no DG-Lab device is connected.
- Hover tooltips on all controls.

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
| `Network/AutoSelectBackend` | Try external backend first, fall back to embedded if unavailable |
| `Network/UseEmbeddedServer` | Manual mode when AutoSelectBackend is false |
| `Network/EmbeddedServerAddress` | Address advertised in the QR (leave empty to auto-detect LAN IP) |
| `Network/EmbeddedServerPort` | Embedded backend port (default `9999`) |
| `Network/RefreshEmbeddedTerminalIdOnStart` | Generate a new terminal ID on each backend start |
| `Network/InvalidateQrOnDisconnect` | Invalidate QR when the phone disconnects |
| `Network/ExternalBackendProfile` | `OfficialSocket` or `ThirdPartyController` |
| `Network/OfficialSocketUrl` | Official DG-Lab Socket backend URL |
| `Network/ThirdPartyControllerUrl` | Third-party controller backend URL |
| `Network/QrWebSocketUrl` | Override WebSocket URL embedded in the QR (leave empty to use active backend) |
| `Network/EnableQrOutput` | Generate a local QR PNG |
| `UI/EnableMenu` | Enable the in-game menu |

Advanced config:

```text
<GameDir>\BepInEx\config\dglab.settings.cfg
```

Important entries:

- `Control/StrengthA` and `Control/StrengthB` — maximum runtime strength (0–200). Events scale proportionally up to these values.
- `Control/EnableDamageHook` — enable game damage and body-state hooks.
- `Wave/EnableWaveEvents` — send waveform pulses on hit events.
- `Wave/EnableConditionMixer` — continuously sample body conditions and mix persistent waves.
- `Binding/ChannelABodyParts` — body parts mapped to channel A.
- `Binding/ChannelBBodyParts` — body parts mapped to channel B.
- `UI/MenuToggleKey` — key to toggle the menu (default `F10`). F1–F12 and navigation keys are supported.
- `UI/MenuToggleAltRequired` — require `Alt + MenuToggleKey` to toggle.
- `UI/MiniOverlayEnabled` — show the compact status overlay.

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

Group names: `ArmF` = `3,4,5`, `ArmB` = `6,7,8`, `LegF` = `9,10,11`, `LegB` = `12,13,14`. Combine with commas, e.g. `Head,UpTorso,ArmF,ArmB`.

## QR and Network

Embedded backend mode starts a WebSocket server inside the game process, listening on `0.0.0.0:9999` by default.

Local QR image:

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

The main menu provides: Restart Backend, Refresh QR, Open QR File, and Disconnect. The mini overlay is status-only to avoid accidental operation.

If the QR shows a wrong IP (e.g. a virtual adapter address), set `Network/EmbeddedServerAddress` to your LAN IP manually:

```text
192.168.x.x
```

Clicking **Restart Backend** in the menu clears the cached LAN IP and reconnects. If the mod is already in embedded mode, it stays in embedded mode.

## UI

- `F10` toggles the main menu (configurable; F1–F12 and navigation keys supported; Alt modifier optional).
- The main menu contains: status, backend profile toggle, QR actions, strength limits, channel bindings, and settings.
- The mini overlay is a draggable status view showing mode, device state, live strength, limits, recent output, and active conditions.
- Hover over any control to see a tooltip description.

## Build

1. Install a .NET SDK capable of building `net48` projects.
2. Put required reference DLLs under `BepInExPlugin\lib`.
3. All third-party DLLs are stored in-repo under `BepInExPlugin\lib`. No external download step is needed once the repo is complete.
4. For a clean GitHub release, ensure the following DLLs are present under `BepInExPlugin\lib`:

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

6. Deploy output from `BepInExPlugin\bin\Release\net48\` to `<GameDir>\BepInEx\plugins\DG-Lab\`.

## Third-Party Credits

- BepInEx: Unity mod loader, used as the plugin runtime.
- HarmonyX / Harmony: runtime patching library used through BepInEx.
- QRCoder: QR code generation, used to produce the local DG-Lab scan PNG.
- websocket-sharp: WebSocket server/client library for DG-Lab Socket transport.
- Newtonsoft.Json: JSON serialization for DG-Lab Socket messages.
- DG-Lab Socket protocol and DG-Lab app/device ecosystem: this project only integrates with public protocol behavior and does not own DG-Lab.
- Casualties Unknown: target game. This is an unofficial mod with no affiliation to the game developer.
