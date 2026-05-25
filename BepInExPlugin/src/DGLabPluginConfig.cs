using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace DGLab.BepInEx
{
    public sealed partial class DGLabPlugin
    {
        private const string AdvancedConfigFileName = "dglab.settings.cfg";

        private ConfigFile _advancedConfig;
        private ConfigEntry<bool> _enabled;
        private ConfigEntry<bool> _autoSelectBackend;
        private ConfigEntry<bool> _useEmbeddedServer;
        private ConfigEntry<float> _externalBackendProbeSeconds;
        private ConfigEntry<string> _embeddedServerAddress;
        private ConfigEntry<int> _embeddedServerPort;
        private ConfigEntry<string> _embeddedTerminalId;
        private ConfigEntry<bool> _refreshEmbeddedTerminalIdOnStart;
        private ConfigEntry<bool> _invalidateQrOnDisconnect;
        private ConfigEntry<string> _serverUrl;
        private ConfigEntry<string> _externalBackendProfile;
        private ConfigEntry<string> _officialSocketUrl;
        private ConfigEntry<string> _thirdPartyControllerUrl;
        private ConfigEntry<string> _qrWebSocketUrl;
        private ConfigEntry<bool> _enableQrOutput;
        private ConfigEntry<bool> _enableMenu;

        private ConfigEntry<int> _strengthA;
        private ConfigEntry<int> _strengthB;
        private ConfigEntry<bool> _enableHotkeys;
        private ConfigEntry<bool> _enableDamageHook;
        private ConfigEntry<float> _damageTriggerMin;
        private ConfigEntry<float> _damageCooldown;
        private ConfigEntry<float> _impactTriggerMin;
        private ConfigEntry<float> _impactCooldown;
        private ConfigEntry<int> _breakBoneIntensity;
        private ConfigEntry<int> _dislocateIntensity;
        private ConfigEntry<int> _dismemberIntensity;
        private ConfigEntry<int> _selfHarmIntensity;
        private ConfigEntry<bool> _enableWaveEvents;
        private ConfigEntry<bool> _enableDeathState;
        private ConfigEntry<bool> _enableCriticalState;
        private ConfigEntry<int> _deathWaveDuration;
        private ConfigEntry<int> _criticalWaveDuration;
        private ConfigEntry<int> _damageWaveDuration;
        private ConfigEntry<int> _impactWaveDuration;
        private ConfigEntry<int> _breakWaveDuration;
        private ConfigEntry<int> _dislocateWaveDuration;
        private ConfigEntry<int> _dismemberWaveDuration;
        private ConfigEntry<int> _selfHarmWaveDuration;
        private ConfigEntry<int> _damageWaveCooldown;
        private ConfigEntry<int> _impactWaveCooldown;
        private ConfigEntry<int> _breakWaveCooldown;
        private ConfigEntry<int> _dislocateWaveCooldown;
        private ConfigEntry<int> _dismemberWaveCooldown;
        private ConfigEntry<int> _selfHarmWaveCooldown;
        private ConfigEntry<bool> _menuAlwaysVisible;
        private ConfigEntry<bool> _menuShowOnStart;
        private ConfigEntry<KeyCode> _menuToggleKey;
        private ConfigEntry<bool> _menuToggleAltRequired;
        private ConfigEntry<string> _uiLanguage;
        private ConfigEntry<bool> _miniOverlayEnabled;
        private ConfigEntry<string> _channelABodyParts;
        private ConfigEntry<string> _channelBBodyParts;
        private ConfigEntry<bool> _enableTimeBasedWaves;
        private ConfigEntry<bool> _enableConditionMixer;
        private ConfigEntry<string> _gentleTimeRange;
        private ConfigEntry<string> _intenseTimeRange;
        private ConfigEntry<bool> _debugLog;
        private ConfigEntry<bool> _realtimeTestLog;
        private ConfigEntry<float> _realtimeTestLogInterval;

        private void BindPluginConfig()
        {
            MigrateMainConfig();

            _enabled = Config.Bind("General", "Enabled", true, Text("Enable or disable DG-Lab EXP BodySync.", "启用或禁用 DG-Lab EXP 体感同步。"));
            _autoSelectBackend = Config.Bind("Network", "AutoSelectBackend", true, Text("Automatically try the external Socket V2 backend first, then fall back to the embedded QR backend if it is unavailable.", "自动优先尝试外部 Socket V2 后端；不可用时回退到内置扫码后端。"));
            _useEmbeddedServer = Config.Bind("Network", "UseEmbeddedServer", true, Text("Manual fallback mode when AutoSelectBackend is false. True runs the embedded backend; false connects to ServerUrl.", "AutoSelectBackend 为 false 时的手动备用模式。true 使用内置后端；false 连接 ServerUrl。"));
            _externalBackendProbeSeconds = Config.Bind("Network", "ExternalBackendProbeSeconds", 2.5f, Range(Text("Seconds to wait for an external Socket V2 backend to assign a client ID before falling back to embedded mode.", "等待外部 Socket V2 后端分配 clientId 的秒数，超时后回退到内置模式。"), 0.5f, 10f));
            _embeddedServerAddress = Config.Bind("Network", "EmbeddedServerAddress", "", Text("Address advertised in the QR for embedded server mode. Leave empty to auto-detect the best reachable LAN IPv4 address. The server still listens on 0.0.0.0.", "内置后端二维码中显示的地址。留空时自动检测最可能可达的局域网 IPv4 地址。服务端仍监听 0.0.0.0。"));
            _embeddedServerPort = Config.Bind("Network", "EmbeddedServerPort", 9999, Range(Text("Port for the embedded WebSocket backend.", "内置 WebSocket 后端监听端口。"), 1, 65535));
            _embeddedTerminalId = Config.Bind("Network", "EmbeddedTerminalId", "", Text("Terminal ID used in the embedded QR path. Usually regenerated when the backend starts.", "内置二维码路径使用的终端 ID，通常在后端启动时重新生成。"));
            _refreshEmbeddedTerminalIdOnStart = Config.Bind("Network", "RefreshEmbeddedTerminalIdOnStart", true, Text("Generate a new embedded terminal ID whenever the embedded backend starts or restarts.", "每次内置后端启动或重启时生成新的终端 ID。"));
            _invalidateQrOnDisconnect = Config.Bind("Network", "InvalidateQrOnDisconnect", true, Text("Generate a new embedded terminal ID when the phone disconnects, invalidating old QR codes.", "手机断开时生成新的终端 ID，使旧二维码失效。"));
            _serverUrl = Config.Bind("Network", "ServerUrl", "ws://127.0.0.1:9999", Text("Legacy external backend URL (kept for compatibility). Prefer OfficialSocketUrl or ThirdPartyControllerUrl.", "兼容旧版的外部后端地址。建议改用 OfficialSocketUrl 或 ThirdPartyControllerUrl。"));
            _externalBackendProfile = Config.Bind("Network", "ExternalBackendProfile", "ThirdPartyController", Text("External backend profile: OfficialSocket or ThirdPartyController.", "外部后端类型：OfficialSocket 或 ThirdPartyController。"));
            _officialSocketUrl = Config.Bind("Network", "OfficialSocketUrl", "", Text("Official DG-Lab Socket backend URL. Used when ExternalBackendProfile is OfficialSocket.", "官方 DG-Lab Socket 后端地址。ExternalBackendProfile 为 OfficialSocket 时使用。"));
            _thirdPartyControllerUrl = Config.Bind("Network", "ThirdPartyControllerUrl", "ws://127.0.0.1:9999", Text("Third-party controller backend URL. Used when ExternalBackendProfile is ThirdPartyController.", "第三方控制器后端地址。ExternalBackendProfile 为 ThirdPartyController 时使用。"));
            _qrWebSocketUrl = Config.Bind("Network", "QrWebSocketUrl", "", Text("Optional WebSocket URL embedded in the DG-Lab scan QR. Leave empty to use the active backend URL.", "可选：写入 DG-Lab 扫码二维码的 WebSocket 地址。留空时使用当前后端地址。"));
            _enableQrOutput = Config.Bind("Network", "EnableQrOutput", true, Text("Generate a local QR PNG whenever the scan URL is available or changes.", "扫码地址可用或变化时生成本地二维码 PNG。"));
            _enableMenu = Config.Bind("UI", "EnableMenu", true, Text("Enable the in-game IMGUI control menu and compact status overlay.", "启用游戏内 IMGUI 控制菜单和迷你状态悬浮窗。"));

            _advancedConfigPath = Path.Combine(Paths.ConfigPath, AdvancedConfigFileName);
            MigrateLegacyAdvancedConfig(_advancedConfigPath);
            Directory.CreateDirectory(Path.GetDirectoryName(_advancedConfigPath));
            _advancedConfig = new ConfigFile(_advancedConfigPath, true);

            BindControlConfig();
            BindWaveConfig();
            BindUiConfig();
            BindBindingConfig();
            BindDebugConfig();
        }

        private void BindControlConfig()
        {
            _strengthA = _advancedConfig.Bind("Control", "StrengthA", 100, Range(Text("Maximum runtime strength for channel A. Events scale up to this value.", "A 通道运行时强度上限。事件会按比例缩放到此值。"), 0, 200));
            _strengthB = _advancedConfig.Bind("Control", "StrengthB", 100, Range(Text("Maximum runtime strength for channel B. Events scale up to this value.", "B 通道运行时强度上限。事件会按比例缩放到此值。"), 0, 200));
            _enableHotkeys = _advancedConfig.Bind("Control", "EnableHotkeys", false, Text("Enable developer/demo hotkeys.", "启用开发/演示热键。"));
            _enableDamageHook = _advancedConfig.Bind("Control", "EnableDamageHook", true, Text("Trigger DG-Lab output from game damage and body-state hooks.", "根据游戏伤害和身体状态 Hook 触发 DG-Lab 输出。"));
            _damageTriggerMin = _advancedConfig.Bind("Control", "DamageTriggerMin", 1.0f, Range(Text("Minimum damage value required to trigger output.", "触发输出所需的最小伤害值。"), 0f, 200f));
            _damageCooldown = _advancedConfig.Bind("Control", "DamageCooldownSeconds", 0.8f, Range(Text("Cooldown between damage triggers, in seconds.", "伤害触发之间的冷却时间，单位秒。"), 0f, 60f));
            _impactTriggerMin = _advancedConfig.Bind("Control", "ImpactTriggerMin", 8.0f, Range(Text("Minimum impact force required to trigger output.", "触发输出所需的最小冲击力。"), 0f, 500f));
            _impactCooldown = _advancedConfig.Bind("Control", "ImpactCooldownSeconds", 0.6f, Range(Text("Cooldown between impact triggers, in seconds.", "冲击触发之间的冷却时间，单位秒。"), 0f, 60f));
            _breakBoneIntensity = _advancedConfig.Bind("Control", "BreakBoneIntensity", 140, Range(Text("Relative event intensity for bone-break events.", "骨折事件的相对强度。"), 0, 200));
            _dislocateIntensity = _advancedConfig.Bind("Control", "DislocateIntensity", 100, Range(Text("Relative event intensity for dislocation events.", "脱臼事件的相对强度。"), 0, 200));
            _dismemberIntensity = _advancedConfig.Bind("Control", "DismemberIntensity", 170, Range(Text("Relative event intensity for dismember events.", "断肢事件的相对强度。"), 0, 200));
            _selfHarmIntensity = _advancedConfig.Bind("Control", "SelfHarmIntensity", 160, Range(Text("Relative event intensity for self-harm events.", "自伤事件的相对强度。"), 0, 200));
        }

        private static void MigrateLegacyAdvancedConfig(string configPath)
        {
            if (File.Exists(configPath)) return;

            var legacyConfigSubfolderPath = Path.Combine(Paths.ConfigPath, "DG-Lab", AdvancedConfigFileName);
            var legacyPluginPath = Path.Combine(Paths.PluginPath, "DG-Lab", AdvancedConfigFileName);
            var legacyPath = File.Exists(legacyConfigSubfolderPath) ? legacyConfigSubfolderPath : legacyPluginPath;
            if (!File.Exists(legacyPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            File.Copy(legacyPath, configPath, overwrite: false);
        }

        private static void MigrateMainConfig()
        {
            var currentPath = Path.Combine(Paths.ConfigPath, "dglab.socket.cfg");
            var legacyPath = Path.Combine(Paths.ConfigPath, "com.dglab.socket.cfg");
            if (File.Exists(currentPath) || !File.Exists(legacyPath)) return;

            File.Copy(legacyPath, currentPath, overwrite: false);
        }

        private void BindWaveConfig()
        {
            _enableWaveEvents = _advancedConfig.Bind("Wave", "EnableWaveEvents", true, Text("Use waveform events in addition to runtime strength changes.", "除实时强度变化外，同时使用波形事件。"));
            _enableDeathState = _advancedConfig.Bind("Wave", "EnableDeathState", true, Text("Enable persistent output when the character is dead.", "角色死亡时启用持续输出。"));
            _enableCriticalState = _advancedConfig.Bind("Wave", "EnableCriticalState", true, Text("Enable persistent output when the character is critically dying.", "角色濒死时启用持续输出。"));
            _deathWaveDuration = _advancedConfig.Bind("Wave", "DeathWaveDuration", 2, Duration(Text("Wave duration for the death loop.", "死亡循环波形持续时间。")));
            _criticalWaveDuration = _advancedConfig.Bind("Wave", "CriticalWaveDuration", 2, Duration(Text("Wave duration for the critical loop.", "濒死循环波形持续时间。")));
            _damageWaveDuration = _advancedConfig.Bind("Wave", "DamageWaveDuration", 1, Duration(Text("Wave duration for damage events.", "伤害事件波形持续时间。")));
            _impactWaveDuration = _advancedConfig.Bind("Wave", "ImpactWaveDuration", 1, Duration(Text("Wave duration for impact events.", "冲击事件波形持续时间。")));
            _breakWaveDuration = _advancedConfig.Bind("Wave", "BreakWaveDuration", 1, Duration(Text("Wave duration for bone-break events.", "骨折事件波形持续时间。")));
            _dislocateWaveDuration = _advancedConfig.Bind("Wave", "DislocateWaveDuration", 1, Duration(Text("Wave duration for dislocation events.", "脱臼事件波形持续时间。")));
            _dismemberWaveDuration = _advancedConfig.Bind("Wave", "DismemberWaveDuration", 2, Duration(Text("Wave duration for dismember events.", "断肢事件波形持续时间。")));
            _selfHarmWaveDuration = _advancedConfig.Bind("Wave", "SelfHarmWaveDuration", 2, Duration(Text("Wave duration for self-harm events.", "自伤事件波形持续时间。")));
            _damageWaveCooldown = _advancedConfig.Bind("Wave", "DamageWaveCooldown", 2, Cooldown(Text("Wave cooldown for damage events.", "伤害事件波形冷却时间。")));
            _impactWaveCooldown = _advancedConfig.Bind("Wave", "ImpactWaveCooldown", 2, Cooldown(Text("Wave cooldown for impact events.", "冲击事件波形冷却时间。")));
            _breakWaveCooldown = _advancedConfig.Bind("Wave", "BreakWaveCooldown", 4, Cooldown(Text("Wave cooldown for bone-break events.", "骨折事件波形冷却时间。")));
            _dislocateWaveCooldown = _advancedConfig.Bind("Wave", "DislocateWaveCooldown", 3, Cooldown(Text("Wave cooldown for dislocation events.", "脱臼事件波形冷却时间。")));
            _dismemberWaveCooldown = _advancedConfig.Bind("Wave", "DismemberWaveCooldown", 6, Cooldown(Text("Wave cooldown for dismember events.", "断肢事件波形冷却时间。")));
            _selfHarmWaveCooldown = _advancedConfig.Bind("Wave", "SelfHarmWaveCooldown", 6, Cooldown(Text("Wave cooldown for self-harm events.", "自伤事件波形冷却时间。")));
            _enableTimeBasedWaves = _advancedConfig.Bind("Wave", "EnableTimeBasedWaves", true, Text("Select wave profiles by local time ranges.", "按本地时间段选择波形配置。"));
            _enableConditionMixer = _advancedConfig.Bind("Wave", "EnableConditionMixer", true, Text("Sample ongoing body conditions and mix persistent waves for pain, sickness, infection, oxygen, hunger, thirst, temperature, fatigue, mood, and shock.", "采样持续身体状态，并为疼痛、疾病、感染、氧气、饥饿、口渴、温度、疲劳、情绪和休克混合持续波形。"));
            _gentleTimeRange = _advancedConfig.Bind("Wave", "GentleTimeRange", "00:00-08:00", Text("Local time range for the gentle wave profile, HH:mm-HH:mm.", "柔和波形的本地时间段，格式 HH:mm-HH:mm。"));
            _intenseTimeRange = _advancedConfig.Bind("Wave", "IntenseTimeRange", "20:00-23:59", Text("Local time range for the intense wave profile, HH:mm-HH:mm.", "强烈波形的本地时间段，格式 HH:mm-HH:mm。"));
        }

        private void BindUiConfig()
        {
            _menuAlwaysVisible = _advancedConfig.Bind("UI", "MenuAlwaysVisible", false, Text("Always show the main menu.", "始终显示主菜单。"));
            _menuShowOnStart = _advancedConfig.Bind("UI", "MenuShowOnStart", false, Text("Legacy setting. The main menu no longer opens automatically; use F10.", "旧设置。主菜单不再自动打开；请使用 F10。"));
            _menuToggleKey = _advancedConfig.Bind("UI", "MenuToggleKey", KeyCode.F10, Text("Menu toggle key. Use with Alt if MenuToggleAltRequired is true.", "菜单切换键。若启用 MenuToggleAltRequired，则需配合 Alt 使用。"));
            _menuToggleAltRequired = _advancedConfig.Bind("UI", "MenuToggleAltRequired", false, Text("Require Alt + MenuToggleKey to toggle the menu.", "要求 Alt + 菜单切换键 才能开关菜单。"));
            _uiLanguage = _advancedConfig.Bind("UI", "Language", "English", Text("UI language: English or Chinese.", "界面语言：English 或 Chinese。"));
            _miniOverlayEnabled = _advancedConfig.Bind("UI", "MiniOverlayEnabled", true, Text("Show a persistent compact status window. Controls stay in the main menu.", "显示常驻迷你状态悬浮窗。控制项只保留在主菜单。"));
        }

        private void BindBindingConfig()
        {
            _channelABodyParts = _advancedConfig.Bind("Binding", "ChannelABodyParts", "Head,UpTorso,DownTorso,ArmF,ArmB", Text("Body parts mapped to channel A. Use friendly groups like UpperBody, LowerBody, Arms, Hands, Legs, Feet, or precise limb names/0-14 indices.", "映射到 A 通道的身体部位。可用上半身、下半身、双臂、双手、双腿、双脚，也可用精确 limb 名称或 0-14 序号。"));
            _channelBBodyParts = _advancedConfig.Bind("Binding", "ChannelBBodyParts", "LegF,LegB", Text("Body parts mapped to channel B. Use commas to combine groups, for example Legs,Feet or LowerBody.", "映射到 B 通道的身体部位。用逗号组合，例如 双腿,双脚 或 下半身。"));
        }

        private void BindDebugConfig()
        {
            _debugLog = _advancedConfig.Bind("Debug", "VerboseLog", false, Text("Log detailed diagnostics.", "输出详细诊断日志。"));
            _realtimeTestLog = _advancedConfig.Bind("Debug", "RealtimeTestLog", false, Text("Log realtime body scoring details for testing.", "输出实时身体评分细节，方便测试。"));
            _realtimeTestLogInterval = _advancedConfig.Bind("Debug", "RealtimeTestLogInterval", 1.0f, Range(Text("Minimum seconds between realtime scoring test logs.", "实时评分测试日志的最小输出间隔，单位秒。"), 0.25f, 10f));
        }

        private ConfigEntry<bool> WaveEnabled(string key)
        {
            return _advancedConfig.Bind("Wave", key + "Enabled", true, Text("Enable wave event for " + key + ".", "启用 " + key + " 波形事件。"));
        }

        private static ConfigDescription Range(string description, int min, int max)
        {
            return new ConfigDescription(description, new AcceptableValueRange<int>(min, max));
        }

        private static ConfigDescription Range(string description, float min, float max)
        {
            return new ConfigDescription(description, new AcceptableValueRange<float>(min, max));
        }

        private static ConfigDescription Duration(string description)
        {
            return Range(description, 1, 30);
        }

        private static ConfigDescription Cooldown(string description)
        {
            return Range(description, 0, 120);
        }

        private static string Text(string english, string chinese)
        {
            return english + " / " + chinese;
        }
    }
}
