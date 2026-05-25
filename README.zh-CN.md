# DG-Lab EXP BodySync / DG-Lab EXP 体感同步

[English](./README.md) | 简体中文

`DG-Lab EXP BodySync`，中文名 `DG-Lab EXP 体感同步`，是面向 `Casualties Unknown` 的 BepInEx 5 模组。它会根据游戏内伤害与身体状态，将疼痛、休克、意识、出血、缺氧、感染、温度等状态映射为 DG-Lab Socket v2 的 A/B 通道输出。

它不是简单的"受伤触发器"，而是会持续采样身体状态，并根据严重程度、身体部位绑定和通道上限进行动态混合输出。

## 安全警告

> [!WARNING]
> 本模组会通过游戏事件驱动真实 DG-Lab 输出。请把它当作真实的物理刺激工具，而不是普通视觉 Mod。

- 从非常低的 A/B 通道强度上限开始，确认当前设置的体感后再缓慢增加。
- 不要因为游戏画面看起来激烈就测试高数值；设备输出是真实的，连续峰值可能比预期更强。
- 如果出现疼痛、麻木、头晕、恐慌、皮肤不适或任何异常感受，请立即停止。
- 不要在疲劳、饮酒、生病、分心或无法快速停止会话时使用。
- 保持 DG-Lab App 或设备的停止控制可随时触达，不要只依赖游戏内菜单。
- 游戏状态可能突然变化，请在自己的安全范围内使用。
- 你需要自行负责设备、身体、设置和安全。

使用比你认为能承受的更低的强度。目标是沉浸感，而不是证明忍耐力。

## 目标环境

| 项目 | 值 |
| --- | --- |
| 游戏 | `Casualties Unknown` |
| Mod 加载器 | BepInEx `5.4.23.5` |
| 运行时 | .NET Framework `4.8` |
| Unity | `2022.3.62f3` |
| 平台 | Windows x64 |
| DG-Lab 协议 | Socket v2 |

## 功能特性

- 在游戏进程内嵌 DG-Lab WebSocket 后端。
- 生成本地 QR PNG，方便 DG-Lab App 扫码连接。
- 支持外部后端模式，提供官方 Socket 和第三方控制器两种配置。
- 自动后端选择：优先尝试外部后端，不可用时自动回退到内置后端。
- 插件自带 IMGUI 控制菜单，不依赖翻译插件。
- 可拖拽的紧凑状态悬浮窗。
- A/B 通道强度上限滑块控制。
- 可配置 15 个身体部位到 A/B 通道的绑定。
- 根据游戏伤害和持续身体状态实时缩放输出。
- 未连接 DG-Lab 设备时提供离线模拟显示。
- 所有控件支持鼠标悬停提示。

## 项目结构

运行时拆分为三个程序集：

| 程序集 | 说明 |
| --- | --- |
| `DGLab.Core.dll` | Socket v2 DTO、协议工具、内嵌/外部 WebSocket 传输、`DGLabClient` |
| `DGLab.Game.dll` | 游戏 Hook、身体评分、输出状态、强度包络、波形路由 |
| `DGLab.BepInEx.dll` | BepInEx 生命周期、配置、独立 IMGUI、状态悬浮窗、QR 生成、组件组合 |

## 安装

将运行所需文件复制到：

```text
<GameDir>\BepInEx\plugins\DG-Lab\
```

必需文件：

- `DGLab.BepInEx.dll`
- `DGLab.Core.dll`
- `DGLab.Game.dll`
- `QRCoder.dll`
- `websocket-sharp.dll`

不要把配置文件复制到插件目录。配置文件应位于 `BepInEx\config`。

## 配置

主配置文件：

```text
<GameDir>\BepInEx\config\dglab.socket.cfg
```

常用配置项：

| 配置项 | 说明 |
| --- | --- |
| `General/Enabled` | 启用或禁用模组 |
| `Network/AutoSelectBackend` | 自动优先尝试外部后端，不可用时回退到内置后端 |
| `Network/UseEmbeddedServer` | AutoSelectBackend 为 false 时的手动模式 |
| `Network/EmbeddedServerAddress` | QR 中显示的地址（留空自动检测局域网 IP） |
| `Network/EmbeddedServerPort` | 内嵌后端端口（默认 `9999`） |
| `Network/RefreshEmbeddedTerminalIdOnStart` | 每次后端启动时生成新终端 ID |
| `Network/InvalidateQrOnDisconnect` | 手机断开时使 QR 失效 |
| `Network/ExternalBackendProfile` | `OfficialSocket` 或 `ThirdPartyController` |
| `Network/OfficialSocketUrl` | 官方 DG-Lab Socket 后端地址 |
| `Network/ThirdPartyControllerUrl` | 第三方控制器后端地址 |
| `Network/QrWebSocketUrl` | 覆盖 QR 中的 WebSocket 地址（留空使用当前后端地址） |
| `Network/EnableQrOutput` | 生成本地 QR PNG |
| `UI/EnableMenu` | 启用游戏内菜单 |

高级配置文件：

```text
<GameDir>\BepInEx\config\dglab.settings.cfg
```

重要配置项：

- `Control/StrengthA` 与 `Control/StrengthB` — 运行时最大强度上限（0–200），事件按比例缩放到此值。
- `Control/EnableDamageHook` — 启用游戏伤害与身体状态 Hook。
- `Wave/EnableWaveEvents` — 受伤事件时发送波形脉冲。
- `Wave/EnableConditionMixer` — 持续采样身体状态并混合持续波形。
- `Binding/ChannelABodyParts` — 映射到 A 通道的身体部位。
- `Binding/ChannelBBodyParts` — 映射到 B 通道的身体部位。
- `UI/MenuToggleKey` — 菜单切换键（默认 `F10`，支持 F1–F12 和导航键）。
- `UI/MenuToggleAltRequired` — 需要 `Alt + MenuToggleKey` 才能切换菜单。
- `UI/MiniOverlayEnabled` — 显示紧凑状态悬浮窗。

身体部位绑定支持 15 个索引：

| 索引 | 部位 |
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

分组名称：`ArmF` = `3,4,5`，`ArmB` = `6,7,8`，`LegF` = `9,10,11`，`LegB` = `12,13,14`。支持逗号组合，例如 `Head,UpTorso,ArmF,ArmB`。

## QR 与网络

内嵌后端模式会在游戏进程内启动 WebSocket 服务，默认监听 `0.0.0.0:9999`。

本地 QR 图片：

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

主菜单提供：重启后端、刷新二维码、打开二维码文件、断开连接。迷你悬浮窗只显示状态，避免误操作。

如果 QR 使用了错误 IP（例如虚拟网卡地址），请将 `Network/EmbeddedServerAddress` 手动设置为局域网地址：

```text
192.168.x.x
```

点击菜单中的**重启后端**会清除缓存的局域网 IP 并重新连接。若当前已处于内置后端模式，重启后仍保持内置模式。

## 界面操作

- `F10` 切换主菜单（可自定义；支持 F1–F12 和导航键；可选 Alt 组合）。
- 主菜单包含：状态、后端类型切换、QR 操作、强度上限、通道绑定和设置。
- 迷你悬浮窗是可拖拽的状态视图，显示模式、设备状态、实时强度、配置上限、最近输出和激活的状态层。
- 鼠标悬停在任意控件上可查看说明提示。

## 构建

1. 安装支持构建 `net48` 项目的 .NET SDK。
2. 将引用 DLL 放到 `BepInExPlugin\lib`。
3. 本仓库要求第三方 DLL 全部内置在 `BepInExPlugin\lib`，仓库完整后构建不需要额外下载步骤。
4. 准备 GitHub 发布版本时，确保以下 DLL 已放入 `BepInExPlugin\lib`：

```text
QRCoder.dll
websocket-sharp.dll
Newtonsoft.Json.dll
BepInEx.dll
0Harmony.dll
UnityEngine*.dll
Assembly-CSharp.dll
```

5. 在项目根目录运行：

```powershell
dotnet build "BepInExPlugin\DGLab.BepInEx.csproj" -c Release
```

6. 将 `BepInExPlugin\bin\Release\net48\` 中的文件部署到 `<GameDir>\BepInEx\plugins\DG-Lab\`。

## 第三方鸣谢

- BepInEx：Unity Mod 加载器，用作插件运行时。
- HarmonyX / Harmony：通过 BepInEx 使用的运行时补丁库。
- QRCoder：用于生成本地 DG-Lab 扫码 PNG。
- websocket-sharp：用于 DG-Lab Socket 传输的 WebSocket 服务器/客户端库。
- Newtonsoft.Json：用于 DG-Lab Socket 消息的 JSON 序列化。
- DG-Lab Socket 协议与 DG-Lab App/设备生态：本项目仅集成公开协议行为，不拥有 DG-Lab。
- Casualties Unknown：目标游戏。本项目是非官方 Mod，与游戏开发者无从属关系。
