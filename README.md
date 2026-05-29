# DG-Lab EXP BodySync

English | [简体中文](./README.zh-CN.md)

A BepInEx 5 mod for [Casualties Unknown](https://store.steampowered.com/app/4576490/) that maps character injuries, body states, and body-part changes to DG-Lab output.

> [!WARNING]
> This mod maps in-game injuries and body states into simulated DG-Lab waveforms and strength changes. Start with low A/B strength limits, keep the DG-Lab app or device stop controls reachable, and stop immediately if you feel pain, numbness, dizziness, panic, skin irritation, or anything wrong.

## Features

- Embedded DG-Lab Socket backend with in-game QR generation for the DG-Lab app.
- External Socket backend support for existing controllers or custom network setups.
- A/B channel strength limits, body-part binding, and local-injury routing.
- Continuous sampling of pain, injury, fracture, dislocation, bleeding, infection, shock, hypoxia, temperature, consciousness, and related states.
- Per-condition sampling controls for enabling/disabling each sustained state and tuning its strength scale.
- UI language list and body-state names follow the game's language files in `CasualtiesUnknown_Data\Lang`.
- In-game menu, QR code, status overlay, wave monitor, and debug display.
- Offline status and waveform display for checking configuration before connecting a device.

## How It Works

BodySync is not a simple “output when damaged” trigger. It routes local conditions to the channel bound to the affected body parts, then mixes systemic states such as shock, hypoxia, temperature, and consciousness as background output.

Example:

```text
Channel A: Head,UpTorso,LeftArm,RightArm
Channel B: DownTorso,LeftLeg,RightLeg
```

With this binding, head, upper torso, and arms mainly output to A; abdomen and legs mainly output to B; systemic states may affect both channels.

## Requirements

| Item | Requirement |
| --- | --- |
| Game | Casualties Unknown |
| Mod loader | BepInEx 5.x |
| DG-Lab protocol | Socket v2 |
| Runtime | .NET Framework 4.8 / `net48` |

## Installation

1. Install BepInEx 5.
2. Copy the whole `DGLab-EXP-BodySync` folder from the release package to:

```text
<GameDir>\BepInEx\plugins\
```

3. Confirm the final path looks like:

```text
<GameDir>\BepInEx\plugins\DGLab-EXP-BodySync\
```

4. The folder should contain at least:

| File | Purpose |
| --- | --- |
| `DGLab.BepInEx.dll` | BepInEx plugin entry |
| `DGLab.Core.dll` | DG-Lab Socket client and protocol |
| `DGLab.Game.dll` | Game body-state adapter |
| `QRCoder.dll` | QR generation |
| `websocket-sharp.dll` | WebSocket transport |

## Quick Start

1. Start the game and press `F10` to open the DG-Lab menu.
2. Use the embedded backend, or make sure your external Socket backend is reachable.
3. Keep the phone and the PC running the game on the same local network.
4. Scan the generated QR code with the DG-Lab app.
5. Set safe low A/B strength limits.
6. Configure A/B body-part bindings and condition sampling if needed.
7. Open the status overlay and wave monitor to verify connection, strength, and output.

## In-Game Menu

Default menu key: `F10`. The menu includes:

- Device connection state and Socket backend switch/restart.
- QR generation and refresh. The embedded backend auto-selects the advertised LAN address.
- A/B maximum strength.
- A/B body-part binding.
- Condition sampling controls for each sustained body state.
- Status overlay, wave monitor, and debug display toggles.

The three output switches are intentionally separate:

| Switch | Purpose |
| --- | --- |
| `React to hits` | Enables instant injury triggers such as hits, impacts, fractures, dislocations, dismemberment, and self-harm. |
| `Event pulses` | Enables short waveform pulses for instant or special events. Runtime strength can still change when this is off. |
| `Ongoing body state` | Enables continuous body-state sampling. Turning it off clears sustained output, but does not overwrite per-condition settings. |

Default shortcuts:

| Shortcut | Action | Notes |
| --- | --- | --- |
| `F10` | Toggle main menu | Configurable |
| `Alt + [` | Toggle wave monitor | `Alt` required by default |
| `Alt + ]` | Toggle status overlay | `Alt` required by default |

Developer/demo hotkeys are disabled by default. Set `Control/EnableHotkeys = true` to enable them:

| Shortcut | Action |
| --- | --- |
| `1` / `2` | Apply menu strength to channel A / B |
| `Q` / `A` | Increase / decrease channel A strength |
| `W` / `S` | Increase / decrease channel B strength |
| `Z` / `X` | Clear channel A / B waveform |
| `Space` | Send a test waveform to enabled channels |

## Configuration

BepInEx generates config files automatically. Config files do not need to be placed in the plugin folder.

| File | Purpose |
| --- | --- |
| `<GameDir>\BepInEx\config\dglab.socket.cfg` | Main config: enable state, backend mode, QR, and network address |
| `<GameDir>\BepInEx\config\dglab.settings.cfg` | Advanced config: strength, waves, UI, body binding, and debug options |

Common entries:

| Entry | Default | Description |
| --- | --- | --- |
| `General/Enabled` | `true` | Enable or disable the plugin |
| `Network/AutoSelectBackend` | `true` | Try external backend first, then fall back to embedded QR backend |
| `Network/UseEmbeddedServer` | `true` | Use embedded backend when auto-selection is disabled |
| `Network/EmbeddedServerAddress` | empty | LAN IP used in QR; empty means auto-detect |
| `Network/EmbeddedServerPort` | `9999` | Embedded WebSocket backend port |
| `Network/ThirdPartyControllerUrl` | `ws://127.0.0.1:9999` | Third-party controller backend URL |
| `Network/OfficialSocketUrl` | empty | Official Socket backend URL |
| `Control/StrengthA` | `100` | Channel A runtime strength limit, range `0-200` |
| `Control/StrengthB` | `100` | Channel B runtime strength limit, range `0-200` |
| `Control/EnableDamageHook` | `true` | Enable instant injury reactions such as hits, impacts, fractures, dislocations, dismemberment, and self-harm |
| `Wave/EnableWaveEvents` | `true` | Enable short waveform pulses for instant or special events |
| `Wave/EnableConditionMixer` | `true` | Enable continuous body-state sampling and sustained strength output |
| `Wave/StopOutputWhenUnconscious` | `true` | Clear output while the character is unconscious |
| `Wave/UnconsciousRecoverySeconds` | `6` | Ramp output back after regaining consciousness, range `0-30` seconds |
| `Binding/ChannelABodyParts` | `Head,UpTorso,DownTorso,LeftArm,RightArm` | Body parts bound to channel A |
| `Binding/ChannelBBodyParts` | `LeftLeg,RightLeg` | Body parts bound to channel B |
| `UI/MenuToggleKey` | `F10` | Main menu key |
| `UI/OutputMonitorToggleKey` | `RightBracket` | Status overlay key |
| `UI/WaveViewerToggleKey` | `LeftBracket` | Wave monitor key |
| `UI/Language` | `English` | UI language ID. Language names are read from `<GameDir>\CasualtiesUnknown_Data\Lang\*.json` |

Existing config files keep their saved values. If a switch appears off even though its default is `true`, check the generated `dglab.settings.cfg` for a previously saved value.

## Condition Sampling

The `Ongoing body state` switch is a master runtime switch. It only pauses or resumes sampling. It does not rewrite individual condition settings.

Per-condition controls are available in the main menu under `Condition Sampling`:

- Toggle whether each state is sampled.
- Tune each state's sustained-output scale from `0.00x` to `2.00x`.
- Settings are persisted in the `[Condition]` section of `dglab.settings.cfg`.

Examples:

| Entry | Meaning |
| --- | --- |
| `Condition/PainEnabled` | Enable or disable pain sampling |
| `Condition/PainScale` | Pain output multiplier, range `0.0-2.0` |
| `Condition/ShockEnabled` | Enable or disable shock sampling |
| `Condition/ShockScale` | Shock output multiplier, range `0.0-2.0` |

## Language Files

The language selector reads language names from fixed game language files:

```text
<GameDir>\CasualtiesUnknown_Data\Lang\*.json
```

Each file's top-level `name` field is shown in the menu. Body-state names in the overlay and wave monitor follow the selected game language when available.

## Body Binding

Binding values can be exact body-part names, numeric indices, or group aliases. Separate multiple values with commas.

| Index | Name | Region |
| --- | --- | --- |
| `0` | `Head` | Head |
| `1` | `UpTorso` | Upper torso / chest |
| `2` | `DownTorso` | Lower torso / abdomen |
| `3` | `LeftUpperArm` | Left upper arm |
| `4` | `LeftForearm` | Left forearm |
| `5` | `LeftHand` | Left hand |
| `6` | `RightUpperArm` | Right upper arm |
| `7` | `RightForearm` | Right forearm |
| `8` | `RightHand` | Right hand |
| `9` | `LeftThigh` | Left thigh |
| `10` | `LeftLowerLeg` | Left lower leg |
| `11` | `LeftFoot` | Left foot |
| `12` | `RightThigh` | Right thigh |
| `13` | `RightLowerLeg` | Right lower leg |
| `14` | `RightFoot` | Right foot |

Common groups:

| Group | Parts |
| --- | --- |
| `LeftArm` / `RightArm` | Upper arm, forearm, and hand on that side |
| `LeftLeg` / `RightLeg` | Thigh, lower leg, and foot on that side |
| `Arms` / `Hands` | Both arms / both hands |
| `Legs` / `Feet` | Both legs / both feet |
| `UpperBody` / `LowerBody` | Upper body / lower body |

## Network and QR

Embedded backend default listen address:

```text
0.0.0.0:9999
```

`0.0.0.0` means the server accepts connections on all network adapters. The QR code still needs one concrete address that the phone can reach, such as `192.168.x.x`. BodySync auto-detects and advertises the most likely reachable LAN address. Virtual adapters, proxies, or VPNs may cause auto-detection to choose the wrong address.

If auto-detection is not enough, edit:

```text
<GameDir>\BepInEx\config\dglab.socket.cfg
Network/EmbeddedServerAddress = 192.168.x.x
```

Local QR image path:

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

## Development

Source build and integration notes:

| Path | Purpose |
| --- | --- |
| [BepInExPlugin/README.md](./BepInExPlugin/README.md) | BepInEx mod build, runtime files, and release packaging |
| [UnityPlugin/README.md](./UnityPlugin/README.md) | Standalone Unity Socket v2 wrapper sample |

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) / [HarmonyX](https://github.com/BepInEx/HarmonyX)
- [QRCoder](https://github.com/Shane32/QRCoder)
- [websocket-sharp](https://github.com/sta/websocket-sharp)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- [DG-LAB-OPENSOURCE](https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE)
- [Casualties Unknown](https://store.steampowered.com/app/4576490)
