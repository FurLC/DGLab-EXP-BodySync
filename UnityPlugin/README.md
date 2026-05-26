# DG-Lab Unity Plugin

English | [简体中文](./README.zh-CN.md)

This folder contains a lightweight Unity-side wrapper for DG-Lab Socket v2.

It is a standalone Unity integration sample and is not required for the `Casualties Unknown` BepInEx release package. For the game mod, see:

- [README.md](../README.md)
- [BepInExPlugin/README.md](../BepInExPlugin/README.md)

## Target

- Unity 2021 LTS by default.
- Any Unity project that needs a simple DG-Lab Socket v2 client wrapper.

## Dependencies

Add these DLLs to the Unity project:

- `websocket-sharp.dll` for WebSocket support.
- `Newtonsoft.Json.dll` for JSON serialization.

## Suggested Import Layout

```text
Assets/
  DGLab/
    Scripts/
      DGLabClient.cs
      Network/DGLabWebSocketClient.cs
      Protocol/DGLabMessages.cs
  Samples/
    DGLabSampleController.cs
```

## Usage

1. Add `DGLabSampleController` to a GameObject.
2. Start a DG-Lab Socket v2 backend, for example `ws://127.0.0.1:9999`.
3. Use the sample hotkeys to test output.

Sample hotkeys:

| Key | Action |
| --- | --- |
| `1` / `2` | Set channel A / B strength |
| `Q` / `A` | Increase / decrease channel A |
| `W` / `S` | Increase / decrease channel B |
| `Z` / `X` | Clear channel A / B wave queue |
| `Space` | Send a demo wave |

## Protocol Notes

- The backend binds `clientId` and `targetId`; `clientId` is returned on first connect.
- Use QR binding with the DG-Lab app according to the official docs.
- Messages follow the DG-Lab Socket v2 specification.

Reference: <https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE/tree/main/socket/v2>
