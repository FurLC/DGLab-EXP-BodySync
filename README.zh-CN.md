# DG-Lab EXP BodySync

[English](./README.md) | 简体中文

面向 [Casualties Unknown](https://store.steampowered.com/app/4576490/) 的 BepInEx 5 模组，将角色受伤和身体状态转换为 DG-Lab 输出。

> [!WARNING]
> 首次使用请把 A/B 强度上限调低，并确保 DG-Lab App 或设备停止控制随时可触达。出现疼痛、麻木、头晕、恐慌、皮肤不适等异常感受时请立即停止。

## 功能

- 游戏内 DG-Lab 菜单，默认 `F10` 打开。
- 支持内置扫码后端、官方 Socket、OpenDGLAB Controller (OTC)，以及实验性蓝牙 V2/V3 模式。
- 支持 A/B 通道强度上限和身体部位绑定。
- 支持受击、冲击、骨折、脱臼、断肢、自伤等瞬时反应。
- 支持疼痛、出血、休克、缺氧、体温、疲劳、情绪、意识等持续身体状态输出。
- 提供状态悬浮窗和波形查看器。
- UI 语言和身体状态名称会尽量跟随游戏语言文件。

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 游戏 | Casualties Unknown |
| Mod 加载器 | BepInEx 5.x |
| 运行框架 | .NET Framework 4.8 / `net48` |
| DG-Lab 协议 | Socket v2 兼容 App/后端、OTC，或受支持的蓝牙主机 |

## 安装

1. 安装 BepInEx 5。
2. 将发布包中的 `DGLab-EXP-BodySync` 文件夹复制到：

```text
<GameDir>\BepInEx\plugins\
```

3. 最终路径应类似：

```text
<GameDir>\BepInEx\plugins\DGLab-EXP-BodySync\
```

4. 如果使用蓝牙模式，请确认发布包内包含 `InTheHand.BluetoothLE.dll`。

## 快速开始

1. 启动游戏并按 `F10`。
2. 选择连接模式。
3. 先把 A/B 强度上限调低。
4. 连接 DG-Lab App、控制器或设备。
5. 按需要调整身体部位绑定和状态采样。
6. 用状态悬浮窗或波形查看器确认输出。

## 连接模式

| 模式 | 支持的主机/设备 | 连接方式 | 二维码 |
| --- | --- | --- | --- |
| 内置扫码 | DG-Lab Socket v2 App 流程 | 用 DG-Lab App 扫描游戏内二维码 | 是 |
| 官方 Socket | 官方 DG-Lab Socket v2 App/后端流程 | 刷新 ID/二维码后，用 DG-Lab App 扫描 | 是 |
| OTC Controller | OpenDGLAB Controller / OTC Socket v2 配置 | 输入 OTC 设备 IP，端口和路径固定为 `60536/1` | 否 |
| Bluetooth V2 | DG-Lab / 郊狼 2.0 蓝牙主机，名称通常为 `D-LAB...` | 在菜单中扫描并选择 BLE 设备 | 否 |
| Bluetooth V3 | DG-Lab / 郊狼 3.0 蓝牙主机，名称通常为 `47...` | 在菜单中扫描并选择 BLE 设备 | 否 |

说明：

- 官方 Socket 不能由模组主动连接。请刷新 ID/二维码，然后在 DG-Lab App 中扫码。
- OTC 和蓝牙是模组主动连接模式，不显示二维码面板。
- 蓝牙支持仍是实验功能，实际可用性取决于操作系统 BLE 栈。

## 菜单基础

默认快捷键：

| 快捷键 | 功能 |
| --- | --- |
| `F10` | 打开/关闭主菜单 |
| `Alt + [` | 打开/关闭波形查看器 |
| `Alt + ]` | 打开/关闭状态悬浮窗 |

主要输出开关：

| 开关 | 作用 |
| --- | --- |
| `受伤时有反应` | 控制瞬时受伤反应 |
| `事件脉冲` | 控制瞬时/特殊事件的短波形 |
| `持续身体状态` | 控制持续身体状态采样 |

## 身体状态行为

BodySync 会区分剧烈伤害和持续身体状态：

- 损伤、骨折、脱臼、出血、严重休克可以增强输出。
- 意识下降、昏厥风险、失血、低血压、缺氧更偏向削弱或限制持续输出。
- 饥饿、口渴、疲劳、潮湿、脏污、情绪、低免疫只作为低强度提示。
- 启用 `StopOutputWhenUnconscious` 时，昏迷/失去意识会清空输出。

## 配置

大多数设置可在游戏内菜单调整。配置文件和高级选项见 [CONFIGURATION.zh-CN.md](./CONFIGURATION.zh-CN.md)。

自动生成的配置文件：

| 文件 | 用途 |
| --- | --- |
| `<GameDir>\BepInEx\config\dglab.socket.cfg` | 主要网络和后端设置 |
| `<GameDir>\BepInEx\config\dglab.settings.cfg` | 强度、波形、UI、身体绑定和状态设置 |

## 鸣谢

- [BepInEx](https://github.com/BepInEx/BepInEx) / [HarmonyX](https://github.com/BepInEx/HarmonyX)
- [QRCoder](https://github.com/Shane32/QRCoder)
- [websocket-sharp](https://github.com/sta/websocket-sharp)
- [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
- [InTheHand.BluetoothLE](https://github.com/inthehand/32feet)
- [DG-LAB-OPENSOURCE](https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE)
- [Casualties Unknown](https://store.steampowered.com/app/4576490)
