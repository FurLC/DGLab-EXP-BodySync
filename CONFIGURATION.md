# Configuration

[简体中文](./CONFIGURATION.zh-CN.md)

Most settings can be changed from the in-game menu. BepInEx also generates config files under:

```text
<GameDir>\BepInEx\config\
```

| File | Purpose |
| --- | --- |
| `dglab.socket.cfg` | Main plugin and network settings |
| `dglab.settings.cfg` | Strength, waves, UI, body binding, condition sampling, and debug settings |

Existing config files keep their saved values after updates.

## Main Config

`dglab.socket.cfg`

| Entry | Default | Description |
| --- | --- | --- |
| `General/Enabled` | `true` | Enable or disable the plugin |
| `Network/AutoSelectBackend` | `true` | Try external backend first, then fall back to embedded QR backend |
| `Network/UseEmbeddedServer` | `true` | Use embedded backend when auto-selection is disabled |
| `Network/EmbeddedServerAddress` | empty | LAN IP used in embedded QR. Empty means auto-detect |
| `Network/EmbeddedServerPort` | `9999` | Embedded WebSocket backend port |
| `Network/ExternalBackendProfile` | `OfficialSocket` | `OfficialSocket`, `OtcController`, `BluetoothV2`, or `BluetoothV3` |
| `Network/OfficialSocketUrl` | empty | Official Socket backend URL |
| `Network/OtcControllerUrl` | `ws://127.0.0.1:60536/1` | OTC controller WebSocket URL. The menu accepts IP only and stores `ws://<ip>:60536/1` |
| `Network/BluetoothDeviceName` | empty | Bluetooth device name or ID selected from BLE scan |
| `Network/EnableQrOutput` | `true` | Save a local QR image when a scan URL is available |

Local QR image path:

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

## Control

`dglab.settings.cfg`

| Entry | Default | Description |
| --- | --- | --- |
| `Control/StrengthA` | `100` | Channel A runtime strength limit, range `0-200` |
| `Control/StrengthB` | `100` | Channel B runtime strength limit, range `0-200` |
| `Control/EnableDamageHook` | `true` | Enable instant injury reactions |
| `Control/DamageTriggerMin` | `1.0` | Minimum damage value for injury reactions |
| `Control/ImpactTriggerMin` | `8.0` | Minimum impact force for impact reactions |
| `Control/EnableHotkeys` | `false` | Enable developer/demo hotkeys |

## Waves

| Entry | Default | Description |
| --- | --- | --- |
| `Wave/EnableWaveEvents` | `true` | Enable short wave pulses for instant or special events |
| `Wave/EnableConditionMixer` | `true` | Enable ongoing body-state sampling |
| `Wave/EnableDeathState` | `true` | Enable death-state loop |
| `Wave/EnableCriticalState` | `true` | Enable critical-state loop |
| `Wave/StopOutputWhenUnconscious` | `true` | Clear output while unconscious/comatose |
| `Wave/UnconsciousRecoverySeconds` | `6` | Ramp output back after waking, range `0-30` seconds |

## Body Binding

| Entry | Default | Description |
| --- | --- | --- |
| `Binding/ChannelABodyParts` | `Head,UpTorso,DownTorso,LeftArm,RightArm` | Body parts mapped to channel A |
| `Binding/ChannelBBodyParts` | `LeftLeg,RightLeg` | Body parts mapped to channel B |

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

## Condition Sampling

Each condition has two entries:

| Pattern | Description |
| --- | --- |
| `Condition/<Name>Enabled` | Enable or disable the condition |
| `Condition/<Name>Scale` | Condition strength multiplier, range `0.0-2.0` |

Examples:

| Entry | Description |
| --- | --- |
| `Condition/PainEnabled` | Enable pain sampling |
| `Condition/PainScale` | Pain output multiplier |
| `Condition/ShockEnabled` | Enable shock sampling |
| `Condition/ShockScale` | Shock output multiplier |

Available conditions include pain, injury, fracture, dislocation, bleeding, blood loss, hypotension, hypertension, internal bleeding, infection, sepsis, sickness, radiation, oxygen deficit, arrhythmia, cardiac arrest, hunger, thirst, temperature, exertion, tiredness, mood, panic, wetness, dirtyness, low immunity, consciousness, nerve, trauma, pain shock, and shock.

## UI

| Entry | Default | Description |
| --- | --- | --- |
| `UI/MenuToggleKey` | `F10` | Main menu key |
| `UI/OutputMonitorToggleKey` | `RightBracket` | Status overlay key |
| `UI/WaveViewerToggleKey` | `LeftBracket` | Wave viewer key |
| `UI/Language` | `English` | UI language ID |

Language names are read from:

```text
<GameDir>\CasualtiesUnknown_Data\Lang\*.json
```

## Network Notes

Embedded backend listens on:

```text
0.0.0.0:9999
```

The QR code still needs a reachable LAN address, such as `192.168.x.x`. If auto-detection chooses the wrong address, set `Network/EmbeddedServerAddress` manually.

OTC default URL:

```text
ws://127.0.0.1:60536/1
```

If OTC runs on a phone or another device, replace `127.0.0.1` with that device's LAN IP.

## Bluetooth Notes

- Bluetooth V2 scans for device names starting with `D-LAB`.
- Bluetooth V3 scans for device names starting with `47`.
- Bluetooth modes require `InTheHand.BluetoothLE.dll` in the release folder.
- Bluetooth availability depends on the operating system BLE stack and adapter support.
