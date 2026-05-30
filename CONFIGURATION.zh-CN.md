# 配置说明

[English](./CONFIGURATION.md)

大多数设置都可以在游戏内菜单调整。BepInEx 也会在以下目录自动生成配置文件：

```text
<GameDir>\BepInEx\config\
```

| 文件 | 用途 |
| --- | --- |
| `dglab.socket.cfg` | 插件主设置和网络设置 |
| `dglab.settings.cfg` | 强度、波形、UI、身体绑定、状态采样和调试设置 |

已有配置文件会保留之前保存的值。

## 主配置

`dglab.socket.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General/Enabled` | `true` | 启用或禁用插件 |
| `Network/AutoSelectBackend` | `true` | 优先尝试外部后端，失败后回退内置扫码后端 |
| `Network/UseEmbeddedServer` | `true` | 关闭自动选择时，是否使用内置后端 |
| `Network/EmbeddedServerAddress` | 空 | 内置二维码使用的局域网 IP。留空表示自动检测 |
| `Network/EmbeddedServerPort` | `9999` | 内置 WebSocket 后端端口 |
| `Network/ExternalBackendProfile` | `OfficialSocket` | `OfficialSocket`、`OtcController`、`BluetoothV2` 或 `BluetoothV3` |
| `Network/OfficialSocketUrl` | 空 | 官方 Socket 后端地址 |
| `Network/OtcControllerUrl` | `ws://127.0.0.1:60536/1` | OTC 控制器 WebSocket 地址。菜单只输入 IP，并保存为 `ws://<ip>:60536/1` |
| `Network/BluetoothDeviceName` | 空 | 从 BLE 扫描列表选择的蓝牙设备名或 ID |
| `Network/EnableQrOutput` | `true` | 扫码地址可用时保存本地二维码图片 |

本地二维码图片路径：

```text
<GameDir>\BepInEx\cache\DG-Lab\dglab-qr.png
```

## 控制

`dglab.settings.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Control/StrengthA` | `100` | A 通道运行时强度上限，范围 `0-200` |
| `Control/StrengthB` | `100` | B 通道运行时强度上限，范围 `0-200` |
| `Control/EnableDamageHook` | `true` | 启用瞬时受伤反应 |
| `Control/DamageTriggerMin` | `1.0` | 触发受伤反应的最小伤害值 |
| `Control/ImpactTriggerMin` | `8.0` | 触发冲击反应的最小冲击力 |
| `Control/EnableHotkeys` | `false` | 启用开发/演示热键 |

## 波形

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Wave/EnableWaveEvents` | `true` | 启用瞬时或特殊事件的短波形 |
| `Wave/EnableConditionMixer` | `true` | 启用持续身体状态采样 |
| `Wave/EnableDeathState` | `true` | 启用死亡状态循环 |
| `Wave/EnableCriticalState` | `true` | 启用危急状态循环 |
| `Wave/StopOutputWhenUnconscious` | `true` | 昏迷/失去意识时清空输出 |
| `Wave/UnconsciousRecoverySeconds` | `6` | 苏醒后输出恢复时间，范围 `0-30` 秒 |

## 身体部位绑定

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Binding/ChannelABodyParts` | `Head,UpTorso,DownTorso,LeftArm,RightArm` | A 通道绑定部位 |
| `Binding/ChannelBBodyParts` | `LeftLeg,RightLeg` | B 通道绑定部位 |

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

## 状态采样

每个状态都有两个配置项：

| 格式 | 说明 |
| --- | --- |
| `Condition/<Name>Enabled` | 启用或禁用该状态 |
| `Condition/<Name>Scale` | 状态强度倍率，范围 `0.0-2.0` |

示例：

| 配置项 | 说明 |
| --- | --- |
| `Condition/PainEnabled` | 启用疼痛采样 |
| `Condition/PainScale` | 疼痛输出倍率 |
| `Condition/ShockEnabled` | 启用休克采样 |
| `Condition/ShockScale` | 休克输出倍率 |

可配置状态包括疼痛、损伤、骨折、脱臼、出血、失血、低血压、高血压、内出血、感染、败血症、疾病、辐射病、缺氧、心律失常、心脏骤停、饥饿、口渴、体温、劳累、疲倦、情绪、恐惧、潮湿、脏污、低免疫、意识下降、神经功能、创伤、疼痛性休克和休克。

## UI

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `UI/MenuToggleKey` | `F10` | 主菜单快捷键 |
| `UI/OutputMonitorToggleKey` | `RightBracket` | 状态悬浮窗快捷键 |
| `UI/WaveViewerToggleKey` | `LeftBracket` | 波形查看器快捷键 |
| `UI/Language` | `English` | UI 语言 ID |

语言名称读取自：

```text
<GameDir>\CasualtiesUnknown_Data\Lang\*.json
```

## 网络说明

内置后端监听：

```text
0.0.0.0:9999
```

二维码仍需要手机可访问的局域网地址，例如 `192.168.x.x`。如果自动检测选择了错误地址，可以手动设置 `Network/EmbeddedServerAddress`。

OTC 默认地址：

```text
ws://127.0.0.1:60536/1
```

如果 OTC 运行在手机或其他设备上，把 `127.0.0.1` 改成对应设备的局域网 IP。

## 蓝牙说明

- Bluetooth V2 扫描名称以 `D-LAB` 开头的设备。
- Bluetooth V3 扫描名称以 `47` 开头的设备。
- 蓝牙模式需要发布包内包含 `InTheHand.BluetoothLE.dll`。
- 蓝牙是否可用取决于操作系统 BLE 栈和蓝牙适配器支持。
