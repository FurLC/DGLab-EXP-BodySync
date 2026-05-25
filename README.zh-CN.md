# DG-Lab EXP BodySync / DG-Lab EXP 体感同步

[English](./README.md) | 简体中文

`DG-Lab EXP BodySync`，中文名 `DG-Lab EXP 体感同步`，是面向 `Casualties Unknown` 的 BepInEx 5 模组。它会根据游戏内伤害与身体状态，将疼痛、休克、意识、出血、缺氧、感染、温度等状态映射为 DG-Lab Socket v2 的 A/B 通道输出。

它不是简单的“受伤触发器”，而是会持续采样身体状态，并根据严重程度、身体部位绑定和通道上限进行动态混合输出。

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

简单来说：使用比你认为能承受的更低的强度。目标是沉浸感，而不是证明忍耐力。

## 目标环境

| 项目 | 值 |
| --- | --- |
| 游戏 | `Casualties Unknown Demo` |
| Mod 加载器 | BepInEx `5.4.23.5` |
| 运行时 | .NET Framework `4.8` |
| Unity | `2022.3.62f3` |
| 平台 | Windows x64 |
| DG-Lab 协议 | Socket v2 |

## 功能特性

- 在游戏进程内嵌 DG-Lab WebSocket 后端。
- 生成本地 QR PNG，方便 DG-Lab App 扫码连接。
- 支持外部后端模式，适合高级网络部署。
- 插件自带 IMGUI 控制菜单，不依赖翻译插件。
- 可拖拽的紧凑状态悬浮窗。
- A/B 通道强度上限控制。
- 可配置 15 个身体部位到 A/B 通道的绑定。
- 根据游戏伤害和持续身体状态实时缩放输出。
- 未连接 DG-Lab 设备时提供离线模拟显示。

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
| `Network/UseEmbeddedServer` | 使用内嵌 WebSocket 后端 |
| `Network/EmbeddedServerAddress` | 内嵌后端监听地址 |
| `Network/EmbeddedServerPort` | 内嵌后端监听端口 |
| `Network/EmbeddedTerminalId` | 内嵌终端 ID |
| `Network/RefreshEmbeddedTerminalIdOnStart` | 启动时刷新终端 ID |
| `Network/InvalidateQrOnDisconnect` | 断开连接后使 QR 失效 |
| `Network/ServerUrl` | 外部后端地址 |
| `Network/QrWebSocketUrl` | QR 使用的 WebSocket 地址 |
| `Network/EnableQrOutput` | 启用 QR 图片输出 |
| `UI/EnableMenu` | 启用主菜单 |

高级配置文件：

```text
<GameDir>\BepInEx\config\dglab.settings.cfg
```

重要配置项：

- `Control/StrengthA` 与 `Control/StrengthB` 是运行时最大强度上限，事件会按比例缩放到这些上限以内。
- `Control/EnableDamageHook` 启用游戏伤害与身体状态 Hook。运行时切换 Hook 可能需要重启。
- `Wave/EnableWaveEvents` 启用事件波形路由。
- `Wave/EnableConditionMixer` 启用持续身体状态采样与混合波形。
- `Binding/ChannelABodyParts` 将游戏身体部位映射到 DG-Lab 通道 A。
- `Binding/ChannelBBodyParts` 将游戏身体部位映射到 DG-Lab 通道 B。
- `UI/MenuToggleKey` 只使用单个按键，不使用 Alt 组合键。
- `UI/MiniOverlayEnabled` 控制紧凑状态悬浮窗。

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

也支持分组名称：`ArmF` 表示 `3,4,5`，`ArmB` 表示 `6,7,8`，`LegF` 表示 `9,10,11`，`LegB` 表示 `12,13,14`。

## QR 与网络

内嵌后端模式会在游戏进程内启动 WebSocket 服务。

默认后端：

```text
0.0.0.0:9999
```

本地 QR 图片：

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

主菜单可以刷新后端、刷新 QR、打开 QR PNG 和断开连接。迷你悬浮窗只显示状态，避免误操作。

如果 QR 使用了错误 IP，例如虚拟网卡地址，请将 `Network/EmbeddedServerAddress` 手动设置为局域网地址：

```text
192.168.1.23
```

## 界面操作

- `F10` 切换主菜单。
- 主菜单包含控制项、QR 操作、通道绑定、语言、强度上限和运行时设置。
- 迷你悬浮窗是可拖拽的状态视图，显示模式、设备状态、实时强度、配置上限、最近输出和激活的状态层。
- QR 操作只放在主菜单中，避免从悬浮窗误触。
- 悬浮窗的关闭或隐藏由主菜单中的 `Mini Overlay` 设置控制。

## 构建

1. 安装支持构建 `net48` 项目的 .NET SDK。
2. 如有需要，将引用 DLL 放到 `BepInExPlugin\lib`。
3. 本仓库要求第三方 DLL 全部内置在 `BepInExPlugin\lib`，并由 `.csproj` 直接引用。仓库完整后，构建不需要额外下载步骤。
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

6. 将输出目录中的文件部署到插件目录：

```text
BepInExPlugin\bin\Release\net48\
```

```text
<GameDir>\BepInEx\plugins\DG-Lab\
```

## 第三方鸣谢

本项目使用或集成了以下第三方项目：

- BepInEx：Unity Mod 加载器，用作插件运行时。
- HarmonyX / Harmony：通过 BepInEx 使用的运行时补丁库。
- QRCoder：用于生成本地 DG-Lab 扫码 PNG。
- websocket-sharp：用于 DG-Lab Socket 传输的 WebSocket 服务器/客户端库。
- Newtonsoft.Json：用于 DG-Lab Socket 消息的 JSON 序列化。
- DG-Lab Socket 协议与 DG-Lab App/设备生态：本项目仅集成公开协议行为，不拥有 DG-Lab。
- Casualties Unknown Demo：目标游戏。本项目是非官方 Mod，与游戏开发者无从属关系。
