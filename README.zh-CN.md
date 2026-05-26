# DG-Lab EXP BodySync

[English](./README.md) | 简体中文

面向 [Casualties Unknown](https://store.steampowered.com/app/4576490/) 的 BepInEx 5 模组，将角色伤害、身体状态和部位变化转换为 DG-Lab Socket v2 输出。

> [!WARNING]
> 这个模组会根据游戏里的受伤和身体状态，让 DG-Lab 设备输出模拟对应体感的波形和强度。首次使用请把 A/B 强度上限调低，并确保 DG-Lab App 或设备停止控制随时可触达。出现疼痛、麻木、头晕、恐慌、皮肤不适等异常感受时请立即停止。

## 功能概览

- 内置 DG-Lab Socket 后端，可在游戏内生成二维码连接 DG-Lab App。
- 支持外部 Socket 后端，适合已有控制器或自定义网络环境。
- 支持 A/B 双通道强度上限、身体部位绑定和局部伤害路由。
- 持续读取疼痛、损伤、骨折、脱臼、出血、感染、休克、缺氧、体温、意识等状态。
- 提供游戏内菜单、连接二维码、状态悬浮窗、波形监视器和调试显示。
- 未连接设备时也可查看状态与波形，方便先检查配置。

## 工作方式

BodySync 不是简单的“受伤就输出”。它会优先把局部状态路由到绑定了对应身体部位的通道，再把休克、缺氧、体温、意识等全身状态作为背景混合输出。

例如：

```text
A 通道：Head,UpTorso,LeftArm,RightArm
B 通道：DownTorso,LeftLeg,RightLeg
```

这个配置表示头部、上躯干和手臂主要作用于 A；腹部和腿部主要作用于 B；全身状态可能同时影响两个通道。

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 游戏 | Casualties Unknown |
| Mod 加载器 | BepInEx 5.x |
| DG-Lab 协议 | Socket v2 |
| 运行框架 | .NET Framework 4.8 / `net48` |

## 安装

1. 安装 BepInEx 5。
2. 从发布包复制整个 `DGLab-EXP-BodySync` 文件夹到：

```text
<GameDir>\BepInEx\plugins\
```

3. 确认最终目录类似：

```text
<GameDir>\BepInEx\plugins\DGLab-EXP-BodySync\
```

4. 文件夹内至少应包含：

| 文件 | 说明 |
| --- | --- |
| `DGLab.BepInEx.dll` | BepInEx 插件入口 |
| `DGLab.Core.dll` | DG-Lab Socket 客户端与协议 |
| `DGLab.Game.dll` | 游戏身体状态适配 |
| `QRCoder.dll` | 二维码生成 |
| `websocket-sharp.dll` | WebSocket 通信 |

## 快速开始

1. 启动游戏，按 `F10` 打开 DG-Lab 菜单。
2. 确认使用内置后端，或确认外部 Socket 后端可连接。
3. 确保运行游戏的电脑和手机在同一局域网。
4. 在菜单中选择正确的局域网地址并刷新二维码。
5. 使用 DG-Lab App 扫码连接。
6. 将 A/B 强度上限调到安全低值。
7. 设置 A/B 通道身体部位绑定。
8. 打开状态悬浮窗和波形监视器，确认连接、强度和当前输出。

## 游戏内菜单

默认主菜单快捷键为 `F10`。菜单包含：

- 设备连接状态与 Socket 后端切换/重启。
- 二维码生成、刷新和局域网地址选择。
- A/B 通道最大强度。
- A/B 通道身体部位绑定。
- 状态悬浮窗、波形监视器和调试显示开关。

默认快捷键：

| 快捷键 | 功能 | 备注 |
| --- | --- | --- |
| `F10` | 打开/关闭主菜单 | 可在配置中修改 |
| `Alt + [` | 打开/关闭波形监视器 | 默认需要 `Alt` |
| `Alt + ]` | 打开/关闭状态悬浮窗 | 默认需要 `Alt` |

开发/演示热键默认关闭。设置 `Control/EnableHotkeys = true` 后可用：

| 快捷键 | 功能 |
| --- | --- |
| `1` / `2` | 应用菜单强度到 A / B 通道 |
| `Q` / `A` | 增加 / 降低 A 通道强度 |
| `W` / `S` | 增加 / 降低 B 通道强度 |
| `Z` / `X` | 清除 A / B 通道波形 |
| `Space` | 向已启用通道发送测试波形 |

## 配置文件

BepInEx 会自动生成配置文件，不需要手动放入插件目录。

| 文件 | 用途 |
| --- | --- |
| `<GameDir>\BepInEx\config\dglab.socket.cfg` | 主配置：启用状态、后端模式、二维码和网络地址 |
| `<GameDir>\BepInEx\config\dglab.settings.cfg` | 高级配置：强度、波形、UI、身体绑定和调试项 |

常用配置项：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General/Enabled` | `true` | 启用或禁用插件 |
| `Network/AutoSelectBackend` | `true` | 优先尝试外部后端，失败后回退内置扫码后端 |
| `Network/UseEmbeddedServer` | `true` | 关闭自动选择时，是否使用内置后端 |
| `Network/EmbeddedServerAddress` | 空 | 二维码使用的局域网 IP，留空自动检测 |
| `Network/EmbeddedServerPort` | `9999` | 内置 WebSocket 后端端口 |
| `Network/ThirdPartyControllerUrl` | `ws://127.0.0.1:9999` | 第三方控制器后端地址 |
| `Network/OfficialSocketUrl` | 空 | 官方 Socket 后端地址 |
| `Control/StrengthA` | `100` | A 通道运行时强度上限，范围 `0-200` |
| `Control/StrengthB` | `100` | B 通道运行时强度上限，范围 `0-200` |
| `Control/EnableDamageHook` | `true` | 启用伤害与身体状态 Hook |
| `Wave/EnableWaveEvents` | `true` | 启用事件波形 |
| `Wave/EnableConditionMixer` | `true` | 启用持续身体状态混波 |
| `Binding/ChannelABodyParts` | `Head,UpTorso,DownTorso,LeftArm,RightArm` | A 通道绑定部位 |
| `Binding/ChannelBBodyParts` | `LeftLeg,RightLeg` | B 通道绑定部位 |
| `UI/MenuToggleKey` | `F10` | 主菜单快捷键 |
| `UI/OutputMonitorToggleKey` | `RightBracket` | 状态悬浮窗快捷键 |
| `UI/WaveViewerToggleKey` | `LeftBracket` | 波形监视器快捷键 |
| `UI/Language` | `English` | UI 语言：`English` 或 `Chinese` |

## 身体部位绑定

绑定值可使用精确部位名、数字索引或分组名，多个值用英文逗号分隔。

| 索引 | 名称 | 中文 |
| --- | --- | --- |
| `0` | `Head` | 头部 |
| `1` | `UpTorso` | 上躯干 / 胸部 |
| `2` | `DownTorso` | 下躯干 / 腹部 |
| `3` | `LeftUpperArm` | 左上臂 |
| `4` | `LeftForearm` | 左前臂 |
| `5` | `LeftHand` | 左手 |
| `6` | `RightUpperArm` | 右上臂 |
| `7` | `RightForearm` | 右前臂 |
| `8` | `RightHand` | 右手 |
| `9` | `LeftThigh` | 左大腿 |
| `10` | `LeftLowerLeg` | 左小腿 |
| `11` | `LeftFoot` | 左脚 |
| `12` | `RightThigh` | 右大腿 |
| `13` | `RightLowerLeg` | 右小腿 |
| `14` | `RightFoot` | 右脚 |

常用分组：

| 分组 | 包含部位 |
| --- | --- |
| `LeftArm` / `RightArm` | 对应手臂的上臂、前臂、手 |
| `LeftLeg` / `RightLeg` | 对应腿部的大腿、小腿、脚 |
| `Arms` / `Hands` | 双臂 / 双手 |
| `Legs` / `Feet` | 双腿 / 双脚 |
| `UpperBody` / `LowerBody` | 上半身 / 下半身 |

## 网络与二维码

内置后端默认监听：

```text
0.0.0.0:9999
```

`0.0.0.0` 表示服务端接受所有网卡连接，但二维码里必须是手机可访问的具体地址，例如 `192.168.x.x`。如果电脑存在虚拟网卡、代理或 VPN，自动选择的地址可能不正确。

推荐在游戏内菜单中选择与手机同一局域网的地址，然后刷新二维码。仍无法连接时，手动修改：

```text
<GameDir>\BepInEx\config\dglab.socket.cfg
Network/EmbeddedServerAddress = 192.168.x.x
```

本地二维码图片路径：

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

## 开发说明

源码构建与集成说明：

| 路径 | 用途 |
| --- | --- |
| [BepInExPlugin/README.zh-CN.md](./BepInExPlugin/README.zh-CN.md) | BepInEx 模组构建、运行文件和发布打包说明 |
| [UnityPlugin/README.zh-CN.md](./UnityPlugin/README.zh-CN.md) | 独立 Unity Socket v2 封装示例说明 |

## 鸣谢

- [BepInEx](https://github.com/BepInEx/BepInEx) / [HarmonyX](https://github.com/BepInEx/HarmonyX)
- [QRCoder](https://github.com/Shane32/QRCoder)
- [websocket-sharp](https://github.com/sta/websocket-sharp)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- [DG-LAB-OPENSOURCE](https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE)
- [Casualties Unknown](https://store.steampowered.com/app/4576490)
