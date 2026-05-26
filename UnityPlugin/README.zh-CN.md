# DG-Lab Unity Plugin

[English](./README.md) | 简体中文

这个目录包含轻量级 DG-Lab Socket v2 Unity 端封装。

它是独立 Unity 集成示例，不是 `Casualties Unknown` BepInEx 发布包的必需内容。游戏 Mod 请查看：

- [README.zh-CN.md](../README.zh-CN.md)
- [BepInExPlugin/README.zh-CN.md](../BepInExPlugin/README.zh-CN.md)

## 目标

- 默认面向 Unity 2021 LTS。
- 适用于需要简单 DG-Lab Socket v2 客户端封装的 Unity 项目。

## 依赖

将以下 DLL 加入 Unity 项目：

- `websocket-sharp.dll`：WebSocket 支持。
- `Newtonsoft.Json.dll`：JSON 序列化。

## 推荐导入结构

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

## 使用方式

1. 将 `DGLabSampleController` 添加到一个 GameObject。
2. 启动 DG-Lab Socket v2 后端，例如 `ws://127.0.0.1:9999`。
3. 使用示例快捷键测试输出。

示例快捷键：

| 按键 | 功能 |
| --- | --- |
| `1` / `2` | 设置 A / B 通道强度 |
| `Q` / `A` | 增加 / 降低 A 通道 |
| `W` / `S` | 增加 / 降低 B 通道 |
| `Z` / `X` | 清除 A / B 通道波形队列 |
| `Space` | 发送示例波形 |

## 协议说明

- 后端负责绑定 `clientId` 和 `targetId`；首次连接时返回 `clientId`。
- 按官方文档使用 DG-Lab App 进行二维码绑定。
- 消息格式遵循 DG-Lab Socket v2 规范。

参考：<https://github.com/DG-LAB-OPENSOURCE/DG-LAB-OPENSOURCE/tree/main/socket/v2>
