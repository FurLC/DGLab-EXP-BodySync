# DG-Lab EXP BodySync - BepInEx 插件

[English](./README.md) | 简体中文

这个目录包含 `DG-Lab EXP BodySync` 使用的 BepInEx 5 插件实现。

安装、配置、安全说明和游戏内使用方式请查看根目录文档：

- [README.md](../README.md)
- [README.zh-CN.md](../README.zh-CN.md)

## 目录内容

| 路径 | 用途 |
| --- | --- |
| `DGLab.BepInEx.csproj` | BepInEx 插件项目 |
| `src/` | 插件 UI、配置、二维码、游戏 Hook、身体路由和波形输出代码 |
| `lib/` | 项目使用的引用库 |

## 从源码构建

项目目标框架为 `.NET Framework 4.8` / `net48`。

在仓库根目录运行：

```powershell
dotnet build "BepInExPlugin\DGLab.BepInEx.csproj" -c Release
```

构建输出目录：

```text
BepInExPlugin\bin\Release\net48\
```

## 输出文件

构建产生的主要运行文件包括：

- `DGLab.BepInEx.dll`
- `DGLab.Core.dll`
- `DGLab.Game.dll`
- `QRCoder.dll`
- `websocket-sharp.dll`

## 说明

- 这里是游戏专用的 BepInEx 实现。
- 独立 Unity 封装说明见 [UnityPlugin/README.zh-CN.md](../UnityPlugin/README.zh-CN.md)。
- BepInEx 配置文件会在游戏运行时自动生成到 `BepInEx/config`。
