DG-Lab Unity Plugin (WebSocket v2)

This folder contains a lightweight Unity-side plugin wrapper for DG-Lab Socket v2.
It targets Unity 2021 LTS by default and can be dropped into any Unity project.

Dependencies (add in Unity):
1) WebSocketSharp (DLL) for WebSocket support
2) Newtonsoft.Json (DLL) for JSON serialization

Suggested structure after import:
Assets/
  DGLab/
    Scripts/
      DGLabClient.cs
      Network/DGLabWebSocketClient.cs
      Protocol/DGLabMessages.cs
  Samples/
    DGLabSampleController.cs

Usage:
- Add DGLabSampleController to a GameObject
- Start the DG-Lab Socket v2 backend (default ws://127.0.0.1:9999)
- Use hotkeys:
  1/2 set channel A/B strength
  Q/A increase/decrease A
  W/S increase/decrease B
  Z/X clear wave queue A/B
  Space send a demo wave

Protocol notes:
- The backend binds clientId/targetId; clientId returned on first connect
- Use QR binding with the DG-Lab app as per the official docs
- All messages follow the DG-Lab Socket v2 specification

Docs: https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE/tree/main/socket/v2
