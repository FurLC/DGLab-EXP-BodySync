DG-Lab EXP BodySync

A BepInEx 5 plugin for Casualties Unknown Demo that maps game damage and body conditions to DG-Lab Socket v2 output.

SAFETY WARNING
- This mod can trigger real DG-Lab output from game events.
- Start with very low A/B strength limits.
- Increase slowly and stop immediately if anything feels wrong.
- Keep the DG-Lab app/device stop controls reachable.
- Use within your own limit. Do not treat high settings as a challenge.

Runtime files
Copy these files to <GameDir>\BepInEx\plugins\DG-Lab\:
- DGLab.BepInEx.dll
- DGLab.Core.dll
- DGLab.Game.dll
- QRCoder.dll
- websocket-sharp.dll
- Newtonsoft.Json.dll if it is not already available to the game/plugin loader

Configuration
Main config:
- <GameDir>\BepInEx\config\dglab.socket.cfg

Advanced config:
- <GameDir>\BepInEx\config\dglab.settings.cfg

UI
- F10 toggles the main menu.
- The main menu contains QR actions, strength limits, settings, and channel bindings.
- The mini overlay is status-only and can be enabled or disabled from the main menu.

Body binding
- Supports 15 limb indices: 0 Head, 1 UpTorso, 2 DownTorso, 3 ArmFUpper, 4 ArmFLower, 5 HandF, 6 ArmBUpper, 7 ArmBLower, 8 HandB, 9 LegFUpper, 10 LegFLower, 11 FootF, 12 LegBUpper, 13 LegBLower, 14 FootB.
- Group names also work: ArmF=3-5, ArmB=6-8, LegF=9-11, LegB=12-14.

QR
- Embedded backend mode starts a WebSocket server in the game process.
- Local QR path: <GameDir>\BepInEx\plugins\DG-Lab\dglab-qr.png
- If the QR IP is wrong, set Network/EmbeddedServerAddress manually in dglab.socket.cfg.

Credits
This plugin uses third-party components that are not owned by this project:
- BepInEx
- HarmonyX / Harmony
- QRCoder
- websocket-sharp
- Newtonsoft.Json
- XUnity Auto Translator integration when present

Check upstream licenses before redistributing release packages.
