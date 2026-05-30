# DG-Lab EXP BodySync

English | [简体中文](./README.zh-CN.md)

A BepInEx 5 mod for [Casualties Unknown](https://store.steampowered.com/app/4576490/) that maps character injuries and body states to DG-Lab output.

> [!WARNING]
> Start with low A/B strength limits. Keep the DG-Lab app or device stop controls reachable. Stop immediately if you feel pain, numbness, dizziness, panic, skin irritation, or anything wrong.

## Features

- In-game DG-Lab menu opened with `F10`.
- Embedded QR backend, Official Socket, OpenDGLAB Controller (OTC), and experimental Bluetooth V2/V3 modes.
- A/B channel strength limits and body-part binding.
- Instant injury reactions for hits, impacts, fractures, dislocations, dismemberment, and self-harm.
- Ongoing body-state output for pain, bleeding, shock, hypoxia, temperature, fatigue, mood, consciousness, and similar states.
- Status overlay and wave viewer for checking current output.
- UI language and body-state names follow the game's language files when available.

## Requirements

| Item | Requirement |
| --- | --- |
| Game | Casualties Unknown |
| Mod loader | BepInEx 5.x |
| Runtime | .NET Framework 4.8 / `net48` |
| DG-Lab protocol | Socket v2 compatible app/backend, OTC, or supported Bluetooth host |

## Installation

1. Install BepInEx 5.
2. Copy the release folder `DGLab-EXP-BodySync` to:

```text
<GameDir>\BepInEx\plugins\
```

3. The final path should look like:

```text
<GameDir>\BepInEx\plugins\DGLab-EXP-BodySync\
```

4. For Bluetooth modes, make sure the release folder contains `InTheHand.BluetoothLE.dll`.

## Quick Start

1. Start the game and press `F10`.
2. Choose a connection mode.
3. Set low A/B strength limits first.
4. Connect the DG-Lab app/controller/device.
5. Adjust body-part binding and condition sampling if needed.
6. Use the status overlay or wave viewer to confirm output.

## Connection Modes

| Mode | Supported host/device | How to connect | QR |
| --- | --- | --- | --- |
| Embedded QR | DG-Lab Socket v2 app flow | Scan the in-game QR code from the DG-Lab app | Yes |
| Official Socket | Official DG-Lab Socket v2 app/backend flow | Refresh ID/QR, then scan from the DG-Lab app | Yes |
| OTC Controller | OpenDGLAB Controller / OTC Socket v2 setup | Enter the OTC device IP. Port/path are fixed to `60536/1` | No |
| Bluetooth V2 | DG-Lab / Coyote 2.0 Bluetooth host, commonly named `D-LAB...` | Scan and select the BLE device in the menu | No |
| Bluetooth V3 | DG-Lab / Coyote 3.0 Bluetooth host, commonly named `47...` | Scan and select the BLE device in the menu | No |

Notes:

- Official Socket cannot be actively connected by the mod. Refresh the ID/QR and scan it in the DG-Lab app.
- OTC and Bluetooth modes connect directly from the mod and do not show QR panels.
- Bluetooth support is experimental and depends on the operating system BLE stack.

## Menu Basics

Default shortcuts:

| Shortcut | Action |
| --- | --- |
| `F10` | Toggle main menu |
| `Alt + [` | Toggle wave viewer |
| `Alt + ]` | Toggle status overlay |

Main output switches:

| Switch | Purpose |
| --- | --- |
| `React to hits` | Instant injury reactions |
| `Event pulses` | Short wave pulses for instant/special events |
| `Ongoing body state` | Continuous body-state sampling |

## Body-State Behavior

BodySync separates sharp injuries from ongoing body state:

- Injury, fracture, dislocation, bleeding, and severe shock can increase output.
- Low consciousness, faintness, blood loss, low blood pressure, and hypoxia tend to weaken or limit sustained output.
- Hunger, thirst, fatigue, wetness, dirtyness, mood, and low immunity are low-intensity hints.
- Unconscious/coma clears output when `StopOutputWhenUnconscious` is enabled.

## Configuration

Most settings can be changed in the in-game menu. For config files and advanced options, see [CONFIGURATION.md](./CONFIGURATION.md).

Generated config files:

| File | Purpose |
| --- | --- |
| `<GameDir>\BepInEx\config\dglab.socket.cfg` | Main network/backend settings |
| `<GameDir>\BepInEx\config\dglab.settings.cfg` | Strength, waves, UI, body binding, and condition options |

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx) / [HarmonyX](https://github.com/BepInEx/HarmonyX)
- [QRCoder](https://github.com/Shane32/QRCoder)
- [websocket-sharp](https://github.com/sta/websocket-sharp)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- [InTheHand.BluetoothLE](https://github.com/inthehand/32feet)
- [DG-LAB-OPENSOURCE](https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE)
- [Casualties Unknown](https://store.steampowered.com/app/4576490)
