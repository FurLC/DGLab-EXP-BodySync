using System.IO;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DGLab.BepInEx
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed partial class DGLabPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "dglab.socket";
        public const string PluginName = "DG-Lab EXP BodySync";
        public const string PluginVersion = "0.1.32";

        private ManualLogSource _log;
        private DGLabClient _client;
        private string _advancedConfigPath;

        private bool _menuOpen;
        private bool _nativeF10WasDown;
        private bool _nativeConfiguredKeyWasDown;
        private float _lastMenuToggleTime = -10f;
        private float _lastReconnectTime = -10f;
        private float _pendingReconnectTime = -1f;
        private bool _intentionalDisconnect;
        private bool _runtimeEmbeddedBackend;
        private bool _autoBackendForcedEmbedded;
        private bool _externalProbeActive;
        private bool _fallbackToEmbeddedAfterDisconnect;
        private float _externalProbeDeadline = -1f;
        private bool _updateLogged;
        private bool _startLogged;
        private bool _applicationQuitting;
        private int _lastHostedUpdateFrame = -1;
        private float _nextRunnerHealthCheckTime = 2f;
        private int _runnerCreateAttempts;
        private DGLabXuaWindow _xuaWindow;
        private DGLabMiniOverlayWindow _miniOverlayWindow;
        private GameObject _standaloneImGuiRunnerObject;
        private bool _bootstrapperCreated;
        private DGLabOutputState _outputState;
        private DGLabWaveRouter _waveRouter;
        private DGLabPersistentOutput _persistent;
        private DGLabConditionMixer _conditionMixer;
        private DGLabStrengthEnvelope _strengthEnvelope;
        private DGLabQrService _qrService;
        private DGLabImGuiRunner _standaloneImGuiRunner;
        private float _lastObservedShock;
        private float _lastObservedUpperPain;
        private float _lastObservedLowerPain;
        private float _lastObservedConsciousness = 100f;
        private Body _lastObservedBody;
        private bool _bodyObservationBaselineReady;
        private bool _outputClearedForNoBody = true;
        private string _lastInactiveReason = string.Empty;
        private bool _wasDead;
        private bool _wasCritical;
        private bool _waitingForMenuKeyBind;
        private Harmony _harmony;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKeyCode);

        public void Awake()
        {
            _log = Logger;
            DontDestroyOnLoad(gameObject);
            BindPluginConfig();

            _menuOpen = false;

            _log.LogInfo("DG-Lab plugin initializing.");
            _log.LogInfo("DG-Lab IMGUI menu config: Enabled=" + _enableMenu.Value + ", AlwaysVisible=" + _menuAlwaysVisible.Value + ", MenuStartsClosed=True, MenuToggleKey=" + _menuToggleKey.Value);
            _log.LogInfo("DG-Lab menu toggle uses single key only. Default/emergency: F10. Alt hotkeys disabled.");
            _log.LogInfo("DG-Lab forced menu diagnostics enabled.");
            _log.LogInfo(_useEmbeddedServer.Value ? "DG-Lab embedded backend mode enabled. A QR will be generated for the phone app to connect to this game." : "DG-Lab external backend mode enabled. Start the backend before launching the game.");
            if (_debugLog.Value)
            {
                _log.LogInfo("DG-Lab debug logging enabled.");
                _log.LogInfo("DG-Lab advanced config: " + _advancedConfigPath);
            }

            _runtimeEmbeddedBackend = ShouldStartEmbeddedBackend();

            if (!_enabled.Value)
            {
                _log.LogWarning("DG-Lab plugin disabled by config.");
                return;
            }

            _harmony = new Harmony(PluginGuid);

            _outputState = new DGLabOutputState();
            _qrService = new DGLabQrService(
                _log,
                () => IsEmbeddedBackendActive,
                () => _embeddedTerminalId.Value,
                () => _embeddedServerAddress.Value,
                () => _embeddedServerPort.Value,
                () => _serverUrl.Value,
                () => _qrWebSocketUrl.Value,
                () => Path.Combine(Paths.BepInExRootPath, "cache", "DG-Lab"));
            InitializeClient();
            CreateImGuiBootstrapper();
            EnsureStandaloneImGuiRunner();

            if (_enableDamageHook.Value)
            {
                DamageHooks.Initialize(
                    _log,
                    _client,
                    _damageTriggerMin,
                    _damageCooldown,
                    _impactTriggerMin,
                    _impactCooldown,
                    _breakBoneIntensity,
                    _dislocateIntensity,
                    _dismemberIntensity,
                    _selfHarmIntensity,
                    _enableWaveEvents.Value ? _waveRouter : null,
                    _outputState,
                    _strengthEnvelope);
                _harmony.PatchAll();
                _log.LogInfo("DG-Lab damage hook enabled.");
            }
        }

        public void Start()
        {
            if (!_startLogged)
            {
                _startLogged = true;
                _log.LogInfo("DG-Lab plugin Start invoked.");
            }
        }

        private void InitializeClient()
        {
            try
            {
                DisconnectClientForReconnect();

                _runtimeEmbeddedBackend = ShouldStartEmbeddedBackend();
                _externalProbeActive = _autoSelectBackend.Value && !_runtimeEmbeddedBackend;
                _externalProbeDeadline = _externalProbeActive ? Time.realtimeSinceStartup + _externalBackendProbeSeconds.Value : -1f;
                _fallbackToEmbeddedAfterDisconnect = false;

                if (_runtimeEmbeddedBackend)
                {
                    RefreshEmbeddedTerminalIdIfNeeded(true);
                    _client = new DGLabClient("0.0.0.0", _embeddedServerPort.Value, _embeddedTerminalId.Value);
                }
                else
                {
                    _client = new DGLabClient(_serverUrl.Value);
                }

                _client.OnConnected += () =>
                {
                    _log.LogInfo(_runtimeEmbeddedBackend ? "DG-Lab embedded WebSocket backend started on port " + _embeddedServerPort.Value + "." : "DG-Lab connected to external Socket V2 backend. Waiting for backend-assigned clientId.");
                    LogQrUrls(_runtimeEmbeddedBackend ? "embedded backend started" : "external backend connected");
                };
                _client.OnClosed += HandleClientClosed;
                _client.OnError += ex => _log.LogError(ex);
                _client.OnMessage += HandleMessage;
                _client.Connect();
                _log.LogInfo(_runtimeEmbeddedBackend ? "DG-Lab embedded WebSocket backend initialized: 0.0.0.0:" + _embeddedServerPort.Value : "DG-Lab external WebSocket client initialized: " + _serverUrl.Value);

                _persistent = new DGLabPersistentOutput(_client, SelectWaveForTime, _outputState);
                _strengthEnvelope = new DGLabStrengthEnvelope(() => _client, () => _strengthA.Value, () => _strengthB.Value, _outputState);
                _conditionMixer = CreateConditionMixer();
                _waveRouter = new DGLabWaveRouter(_log, _client, _persistent, SelectWaveForTime, _outputState);
                RegisterDefaultWaves();
                DamageHooks.UpdateContext(_client, _enableWaveEvents.Value ? _waveRouter : null, _outputState, _strengthEnvelope);
            }
            catch (System.Exception ex)
            {
                _log.LogError("DG-Lab WebSocket initialization failed. Menu will remain available.");
                _log.LogError(ex);
                _client = null;
                _persistent = new DGLabPersistentOutput(_client, SelectWaveForTime, _outputState);
                _strengthEnvelope = new DGLabStrengthEnvelope(() => _client, () => _strengthA.Value, () => _strengthB.Value, _outputState);
                _conditionMixer = CreateConditionMixer();
                _waveRouter = new DGLabWaveRouter(_log, _client, _persistent, SelectWaveForTime, _outputState);
                RegisterDefaultWaves();
                DamageHooks.UpdateContext(_client, _enableWaveEvents.Value ? _waveRouter : null, _outputState, _strengthEnvelope);
            }
        }

        private DGLabConditionMixer CreateConditionMixer()
        {
            return new DGLabConditionMixer(
                _log,
                _client,
                _outputState,
                _strengthEnvelope,
                () => _channelABodyParts.Value,
                () => _channelBBodyParts.Value,
                () => _realtimeTestLog.Value,
                () => _realtimeTestLogInterval.Value);
        }

        public void Update()
        {
            if (!_updateLogged)
            {
                _updateLogged = true;
                _log.LogInfo("DG-Lab plugin Update invoked.");
            }

            HostedMenuUpdate("Plugin.Update");
            TickAutoBackendProbe();
            TickPendingReconnect();
            TickRunnerHealthCheck();

            RefreshOverlayMenu();

            if (!_enableHotkeys.Value)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyStrengthAFromMenu();
            if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyStrengthBFromMenu();
            if (Input.GetKeyDown(KeyCode.Q) && _client != null && _client.HasTarget) _client.IncreaseStrengthA();
            if (Input.GetKeyDown(KeyCode.A) && _client != null && _client.HasTarget) _client.DecreaseStrengthA();
            if (Input.GetKeyDown(KeyCode.W) && _client != null && _client.HasTarget) _client.IncreaseStrengthB();
            if (Input.GetKeyDown(KeyCode.S) && _client != null && _client.HasTarget) _client.DecreaseStrengthB();
            if (Input.GetKeyDown(KeyCode.Z) && _client != null && _client.HasTarget) _client.ClearWaveA();
            if (Input.GetKeyDown(KeyCode.X) && _client != null && _client.HasTarget) _client.ClearWaveB();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var testWave = SelectWaveForTime("hotkey", new[]
                {
                    "0A0A0A0A64646464",
                    "1414141464646464",
                    "1E1E1E1E64646464",
                    "2828282864646464"
                });
                _outputState?.SetWave("hotkey", "test");
                if (_client != null && _client.HasTarget)
                {
                    _client.SendWaveA(testWave, 5);
                }
            }
        }

        public void OnGUI()
        {
            HostedMenuOnGUI("Plugin.OnGUI");
        }

        internal void HostedMenuUpdate(string source)
        {
            if (_enableMenu == null || !_enableMenu.Value) return;
            if (_lastHostedUpdateFrame == Time.frameCount) return;
            _lastHostedUpdateFrame = Time.frameCount;

            TickRealtimeOutput(source);

            if (IsMenuTogglePressed())
            {
                _menuOpen = !_menuOpen;
                if (_xuaWindow != null) _xuaWindow.IsShown = _menuOpen;
                LogMenuDebug("DG-Lab menu toggled from " + source + ": " + (_menuOpen ? "open" : "closed"));
                RefreshOverlayMenu();
            }
        }

        private void TickRealtimeOutput(string source)
        {
            var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            var inactiveReason = GetInactiveBodyReason(body);
            if (inactiveReason != null)
            {
                ClearOutputForInactiveBody(inactiveReason);
                return;
            }

            _outputClearedForNoBody = false;
            _lastInactiveReason = string.Empty;
            _strengthEnvelope?.Tick();
            _persistent?.Tick();

            if (_enableWaveEvents == null || !_enableWaveEvents.Value) return;

            var isDead = !body.alive;
            var isCritical = body.isCriticallyDying;

            ObserveBodyState(body);

            if (_enableConditionMixer != null && _enableConditionMixer.Value && !isDead)
            {
                _conditionMixer?.Tick(body);
            }

            if (_enableDeathState.Value)
            {
                if (isDead && !_wasDead) _waveRouter.StartPersistent("death", DGLabWaveLibrary.DeathLoop, _deathWaveDuration.Value);
                else if (!isDead && _wasDead) _waveRouter.StopPersistent("death");
            }

            if (_enableCriticalState.Value)
            {
                if (isCritical && !_wasCritical) _waveRouter.StartPersistent("critical", DGLabWaveLibrary.CriticalLoop, _criticalWaveDuration.Value);
                else if (!isCritical && _wasCritical) _waveRouter.StopPersistent("critical");
            }

            _wasDead = isDead;
            _wasCritical = isCritical;
        }

        internal void HostedMenuOnGUI(string source)
        {
            if (_enableMenu == null || !_enableMenu.Value) return;

            InitializeMiniOverlayWindow();
            if (_miniOverlayWindow != null && _miniOverlayEnabled.Value) _miniOverlayWindow.OnGUI();

            InitializeXuaWindow();
            if (_xuaWindow != null && (_xuaWindow.IsShown || (_menuAlwaysVisible != null && _menuAlwaysVisible.Value)))
            {
                _xuaWindow.OnGUI();
            }
        }

        internal void LogMenuDebug(string message)
        {
            _log?.LogInfo(message);
        }

        private void InitializeXuaWindow()
        {
            if (_xuaWindow != null || _enableMenu == null || !_enableMenu.Value) return;

            _xuaWindow = new DGLabXuaWindow(this, _menuOpen || (_menuAlwaysVisible != null && _menuAlwaysVisible.Value));
            LogMenuDebug("DG-Lab IMGUI window initialized. IsShown=" + _xuaWindow.IsShown);
        }

        private void InitializeMiniOverlayWindow()
        {
            if (_miniOverlayWindow != null) return;

            _miniOverlayWindow = new DGLabMiniOverlayWindow(this);
            LogMenuDebug("DG-Lab mini overlay initialized.");
        }

        internal void EnsureStandaloneImGuiRunnerFromBootstrapper()
        {
            EnsureStandaloneImGuiRunner();
        }

        private void CreateImGuiBootstrapper()
        {
            if (_bootstrapperCreated || _enableMenu == null || !_enableMenu.Value) return;

            _bootstrapperCreated = true;
            var go = new GameObject("DG-Lab EXP BodySync IMGUI Bootstrapper");
            go.hideFlags = HideFlags.HideAndDontSave;
            var bootstrapper = go.AddComponent<DGLabImGuiBootstrapper>();
            bootstrapper.Owner = this;
            DontDestroyOnLoad(go);
            LogMenuDebug("DG-Lab IMGUI bootstrapper created.");
        }

        private void EnsureStandaloneImGuiRunner()
        {
            if (_enableMenu == null || !_enableMenu.Value) return;
            if (_standaloneImGuiRunnerObject != null)
            {
                if (_standaloneImGuiRunner == null) _standaloneImGuiRunner = _standaloneImGuiRunnerObject.GetComponent<DGLabImGuiRunner>();
                if (_standaloneImGuiRunner != null)
                {
                    _standaloneImGuiRunner.Owner = this;
                    _standaloneImGuiRunner.enabled = true;
                    return;
                }
            }

            var existing = GameObject.Find("DG-Lab EXP BodySync IMGUI Runner");
            if (existing != null)
            {
                _standaloneImGuiRunnerObject = existing;
                _standaloneImGuiRunner = existing.GetComponent<DGLabImGuiRunner>() ?? existing.AddComponent<DGLabImGuiRunner>();
                _standaloneImGuiRunner.Owner = this;
                _standaloneImGuiRunner.enabled = true;
                DontDestroyOnLoad(existing);
                LogMenuDebug("DG-Lab standalone IMGUI runner reused. Active=" + existing.activeInHierarchy + ", RunnerEnabled=" + _standaloneImGuiRunner.enabled);
                return;
            }

            var go = new GameObject("DG-Lab EXP BodySync IMGUI Runner");
            go.hideFlags = HideFlags.HideAndDontSave;
            _standaloneImGuiRunnerObject = go;
            _standaloneImGuiRunner = go.AddComponent<DGLabImGuiRunner>();
            _standaloneImGuiRunner.Owner = this;
            _standaloneImGuiRunner.enabled = true;
            DontDestroyOnLoad(go);
            LogMenuDebug("DG-Lab standalone IMGUI runner created. Active=" + go.activeInHierarchy + ", RunnerEnabled=" + _standaloneImGuiRunner.enabled);
        }

        private void RecreateStandaloneImGuiRunner(string reason)
        {
            _runnerCreateAttempts++;
            if (_runnerCreateAttempts > 5)
            {
                LogMenuDebug("DG-Lab standalone IMGUI runner recreate skipped after repeated failures. Last reason=" + reason);
                return;
            }

            LogMenuDebug("DG-Lab recreating standalone IMGUI runner: " + reason);
            if (_standaloneImGuiRunnerObject != null) Destroy(_standaloneImGuiRunnerObject);
            _standaloneImGuiRunnerObject = null;
            _standaloneImGuiRunner = null;
            EnsureStandaloneImGuiRunner();
        }

        private void TickRunnerHealthCheck()
        {
            if (_enableMenu == null || !_enableMenu.Value || Time.realtimeSinceStartup < _nextRunnerHealthCheckTime) return;

            _nextRunnerHealthCheckTime = Time.realtimeSinceStartup + 2f;
            if (_standaloneImGuiRunner == null || _standaloneImGuiRunnerObject == null)
            {
                RecreateStandaloneImGuiRunner("runner object missing");
                return;
            }

            if (!_standaloneImGuiRunner.HasUpdated)
            {
                RecreateStandaloneImGuiRunner("runner never received Update");
            }
        }

        internal void LogRunnerInfo(string message)
        {
            _log?.LogInfo(message);
        }

        internal string ServerUrl => _serverUrl != null ? _serverUrl.Value : "<not loaded>";

        internal string ClientIdText => _client != null && !string.IsNullOrEmpty(_client.ClientId) ? _client.ClientId : "<not bound>";

        internal string TargetIdText => _client != null && !string.IsNullOrEmpty(_client.TargetId) ? _client.TargetId : "<not bound>";

        internal string MenuToggleKeyName => _menuToggleKey != null ? _menuToggleKey.Value.ToString() : "F10";

        internal bool WaitingForMenuKeyBind => _waitingForMenuKeyBind;

        internal void BeginMenuKeyBind()
        {
            _waitingForMenuKeyBind = true;
        }

        internal void ResetMenuKeyBind()
        {
            _waitingForMenuKeyBind = false;
            _menuToggleKey.Value = KeyCode.F10;
            _nativeConfiguredKeyWasDown = false;
            _nativeF10WasDown = false;
        }

        internal string QrUrlText
        {
            get
            {
                var clientId = _client != null ? _client.ClientId : null;
                return HasQrClientId(clientId) ? _qrService.BuildQrUrl(clientId) : "<click Connect after backend is running; waiting for backend-assigned clientId>";
            }
        }

        internal string QrWebSocketUrlText => HasQrClientId(_client != null ? _client.ClientId : null) ? _qrService.BuildQrWebSocketUrl() : "<not connected>";

        internal string QrClientIdText
        {
            get
            {
                var clientId = _client != null ? _client.ClientId : null;
                return HasQrClientId(clientId) ? _qrService.GetQrClientId(clientId) : "<waiting for backend-assigned clientId>";
            }
        }

        internal string QrImageUrlText
        {
            get
            {
                var clientId = _client != null ? _client.ClientId : null;
                return HasQrClientId(clientId) ? EnsureQrImage(_qrService.BuildQrUrl(clientId)) : "<not available until clientId is assigned>";
            }
        }

        internal bool EnabledValue
        {
            get => _enabled.Value;
            set => _enabled.Value = value;
        }

        internal bool EnableDamageHookValue
        {
            get => _enableDamageHook.Value;
            set => _enableDamageHook.Value = value;
        }

        internal bool EnableWaveEventsValue
        {
            get => _enableWaveEvents.Value;
            set => _enableWaveEvents.Value = value;
        }

        internal bool DebugLogValue
        {
            get => _debugLog.Value;
            set => _debugLog.Value = value;
        }

        internal bool IsChineseUi => string.Equals(_uiLanguage.Value, "Chinese", StringComparison.OrdinalIgnoreCase) || _uiLanguage.Value == "中文";

        internal string UiLanguageValue
        {
            get => IsChineseUi ? "Chinese" : "English";
            set => _uiLanguage.Value = string.Equals(value, "Chinese", StringComparison.OrdinalIgnoreCase) || value == "中文" ? "Chinese" : "English";
        }

        internal string T(string english, string chinese)
        {
            return IsChineseUi ? chinese : english;
        }

        internal bool IsEmbeddedBackendActive => _runtimeEmbeddedBackend;

        private bool ShouldStartEmbeddedBackend()
        {
            return _autoBackendForcedEmbedded || (!_autoSelectBackend.Value && _useEmbeddedServer.Value);
        }

        internal string BackendModeText
        {
            get
            {
                if (_autoSelectBackend.Value) return _runtimeEmbeddedBackend ? T("Auto -> Embedded", "自动 -> 内置后端") : T("Auto -> External", "自动 -> 外部后端");
                return _runtimeEmbeddedBackend ? T("Embedded", "内置后端") : T("External", "外部后端");
            }
        }

        internal string WaveProfileText => GetCurrentWaveProfileName();

        internal string RuntimeStrengthAText => _outputState != null ? _outputState.RuntimeStrengthA.ToString() : "0";

        internal string RuntimeStrengthBText => _outputState != null ? _outputState.RuntimeStrengthB.ToString() : "0";

        internal string MaxStrengthAText => _strengthA != null ? _strengthA.Value.ToString() : "0";

        internal string MaxStrengthBText => _strengthB != null ? _strengthB.Value.ToString() : "0";

        internal string ChannelABodyPartsValue
        {
            get => _channelABodyParts.Value;
            set => _channelABodyParts.Value = string.IsNullOrWhiteSpace(value) ? "Head,UpTorso,DownTorso,ArmF,ArmB" : value.Trim();
        }

        internal string ChannelBBodyPartsValue
        {
            get => _channelBBodyParts.Value;
            set => _channelBBodyParts.Value = string.IsNullOrWhiteSpace(value) ? "LegF,LegB" : value.Trim();
        }

        internal string LastOutputEventText => FormatLastOutputText();

        internal string LastWaveText => FormatWaveText(_outputState != null ? _outputState.LastWave : "none");

        internal string ActiveConditionsText => FormatConditionsText(_outputState != null ? _outputState.ActiveConditions : "none");

        private enum DisplayKeyKind
        {
            Event,
            Condition,
            Wave
        }

        private string FormatConditionsText(string raw)
        {
            raw = string.IsNullOrWhiteSpace(raw) ? "none" : raw.Trim();
            var signature = raw;
            var ratioIndex = raw.LastIndexOf(" A", StringComparison.Ordinal);
            if (ratioIndex >= 0)
            {
                signature = raw.Substring(0, ratioIndex).Trim();
            }

            return FormatConditionSignature(signature);
        }

        private string FormatLastOutputText()
        {
            if (_outputState == null) return FormatDisplayKey("none", DisplayKeyKind.Event);
            if (_outputState.RuntimeStrengthA <= 0 && _outputState.RuntimeStrengthB <= 0) return FormatDisplayKey("none", DisplayKeyKind.Event);

            var merged = MergeConditionSignatures(
                _outputState.RuntimeStrengthA > 0 ? _outputState.OutputConditionsA : "none",
                _outputState.RuntimeStrengthB > 0 ? _outputState.OutputConditionsB : "none");
            return FormatConditionSignature(merged);
        }

        private static string MergeConditionSignatures(string first, string second)
        {
            var merged = new List<string>();
            AddSignatureParts(merged, first);
            AddSignatureParts(merged, second);
            return merged.Count == 0 ? "none" : string.Join("+", merged.ToArray());
        }

        private static void AddSignatureParts(List<string> merged, string signature)
        {
            if (string.IsNullOrWhiteSpace(signature) || string.Equals(signature, "none", StringComparison.OrdinalIgnoreCase)) return;

            var parts = signature.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.Length == 0) continue;
                var exists = false;
                for (var j = 0; j < merged.Count; j++)
                {
                    if (!string.Equals(merged[j], part, StringComparison.OrdinalIgnoreCase)) continue;
                    exists = true;
                    break;
                }
                if (!exists) merged.Add(part);
            }
        }

        private string FormatWaveText(string raw)
        {
            raw = string.IsNullOrWhiteSpace(raw) ? "none" : raw.Trim();
            if (raw.StartsWith("mixed:", StringComparison.OrdinalIgnoreCase)) return T("Mixed: ", "混合波形：") + FormatConditionSignature(raw.Substring(6));
            if (raw.IndexOf('+') >= 0) return FormatConditionSignature(raw);
            return FormatDisplayKey(raw, DisplayKeyKind.Wave);
        }

        private string FormatConditionSignature(string raw)
        {
            raw = string.IsNullOrWhiteSpace(raw) ? "none" : raw.Trim();
            if (raw.StartsWith("mixed:", StringComparison.OrdinalIgnoreCase)) raw = raw.Substring(6);

            var parts = raw.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return FormatDisplayKey(raw, DisplayKeyKind.Condition);

            var labels = new List<string>();
            for (var i = 0; i < parts.Length; i++) labels.Add(FormatDisplayKey(parts[i].Trim(), DisplayKeyKind.Condition));
            return string.Join(IsChineseUi ? "、" : " + ", labels.ToArray());
        }

        private string FormatDisplayKey(string key, DisplayKeyKind kind)
        {
            key = string.IsNullOrWhiteSpace(key) ? "none" : key.Trim();
            var normalized = key.ToLowerInvariant();
            switch (normalized)
            {
                case "idle": return T("Idle", "待机");
                case "none": return T("None", "无");
                case "initializing": return T("Initializing", "初始化中");
                case "baseline": return T("Baseline", "基准状态");
                case "strength": return T("Strength update", "强度更新");
                case "wave": return T("Wave output", "波形输出");
                case "default": return T("Default wave", "默认波形");
                case "timed": return T("Time-based wave", "按时间波形");
                case "test": return T("Test output", "测试输出");
                case "hotkey": return T("Hotkey test", "快捷键测试");
                case "event": return T("Event output", "事件输出");
                case "damage": return T("Damage", "受伤");
                case "impact": return T("Impact", "冲击");
                case "break": return T("Bone break", "骨折");
                case "dislocate": return T("Dislocation", "脱臼");
                case "dismember": return T("Dismemberment", "断肢");
                case "selfharm": return T("Self-harm", "自伤");
                case "shock": return T("Shock", "休克");
                case "pain-shock": return T("Shock", "休克");
                case "trauma": return T("Trauma", "创伤");
                case "body-shock": return T("Shock", "休克");
                case "body-consciousness": return T("Consciousness drop", "意识下降");
                case "upper-pain": return T("Upper body pain", "上半身疼痛");
                case "lower-pain": return T("Lower body pain", "下半身疼痛");
                case "condition": return T("Body condition", "身体状态");
                case "condition-clear": return T("Condition cleared", "状态已清除");
                case "body-init": return T("Body initialized", "身体状态初始化");
                case "no-body": return T("No active body", "未检测到角色身体");
                case "no-limbs": return T("No body parts detected", "未检测到身体部位");
                case "no-world": return T("Not in active gameplay", "未进入游戏内");
                case "decay": return T("Fading", "逐渐衰减");
                case "death": return T("Death state", "死亡状态");
                case "critical": return T("Critical state", "危急状态");
                case "pain": return T("Pain", "疼痛");
                case "injury": return T("Physical injury", "身体损伤");
                case "bleeding": return T("Bleeding", "出血");
                case "blood-loss": return T("Hypovolemic", "低血容量");
                case "hypotension": return T("Hypotension", "低血压");
                case "hypertension": return T("Hypertension", "高血压");
                case "internal-bleeding": return T("Internal bleeding", "内出血");
                case "infection": return T("Infection", "感染");
                case "sepsis": return T("Sepsis", "败血症");
                case "sickness": return T("Sickness", "疾病");
                case "radiation": return T("Radiation sickness", "辐射病");
                case "oxygen": return T("Hypoxemia", "低氧血症");
                case "arrhythmia": return T("Arrhythmia", "心律失常");
                case "cardiac-arrest": return T("Cardiac arrest", "心脏骤停");
                case "heart": return T("Arrhythmia", "心律失常");
                case "hunger": return T("Hunger", "饥饿");
                case "thirst": return T("Thirst", "口渴");
                case "temperature": return T("Abnormal temperature", "体温异常");
                case "exertion": return T("Exertion", "劳累");
                case "tired": return T("Tired", "疲倦");
                case "fatigue": return T("Fatigue", "疲劳");
                case "mood": return T("Depressed mood", "情绪低落");
                case "panic": return T("Horrified", "恐惧");
                case "wet": return T("Wet", "潮湿");
                case "dirty": return T("Dirty", "脏污");
                case "low-immunity": return T("Low immunity", "免疫力低下");
                case "nerve": return T("Neurological impairment", "神经功能受损");
                default:
                    return kind == DisplayKeyKind.Condition ? key : T(ToTitleText(key), key);
            }
        }

        private static string ToTitleText(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "None";

            var parts = key.Replace('-', ' ').Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0) continue;
                parts[i] = char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part.Substring(1).ToLowerInvariant() : string.Empty);
            }

            return string.Join(" ", parts);
        }

        internal bool DeviceConnected => _client != null && _client.HasTarget;

        internal bool MiniOverlayEnabledValue
        {
            get => _miniOverlayEnabled.Value;
            set => _miniOverlayEnabled.Value = value;
        }

        internal bool TimeBasedWavesEnabledValue
        {
            get => _enableTimeBasedWaves.Value;
            set => _enableTimeBasedWaves.Value = value;
        }

        internal bool ConditionMixerEnabledValue
        {
            get => _enableConditionMixer.Value;
            set => _enableConditionMixer.Value = value;
        }

        internal int StrengthAValue
        {
            get => _strengthA.Value;
            set => _strengthA.Value = value;
        }

        internal int StrengthBValue
        {
            get => _strengthB.Value;
            set => _strengthB.Value = value;
        }

        internal void SetMenuOpenFromWindow(bool isOpen)
        {
            _menuOpen = isOpen;
            RefreshOverlayMenu();
        }

        internal void ToggleMenuFromMiniOverlay()
        {
            _menuOpen = !_menuOpen;
            if (_xuaWindow != null) _xuaWindow.IsShown = _menuOpen;
            RefreshOverlayMenu();
        }

        internal void ReconnectFromMenu()
        {
            if (Time.realtimeSinceStartup - _lastReconnectTime < 2f)
            {
                LogMenuDebug("DG-Lab reconnect ignored because it was requested too recently.");
                return;
            }

            _lastReconnectTime = Time.realtimeSinceStartup;
            if (_autoSelectBackend.Value) _autoBackendForcedEmbedded = false;
            InitializeClient();
        }

        internal void EnsureConnectedForQrFromMenu()
        {
            if (HasQrClientId(_client != null ? _client.ClientId : null))
            {
                EnsureQrImage(QrUrlText);
                return;
            }

            _log.LogInfo("DG-Lab QR requested. Connecting to Socket backend now; local QR image will be regenerated after the scan URL is available.");
            ReconnectFromMenu();
        }

        internal void DisconnectFromMenu()
        {
            DisconnectClientIntentional();
            _client = null;
            _persistent = new DGLabPersistentOutput(_client, SelectWaveForTime, _outputState);
            _strengthEnvelope = new DGLabStrengthEnvelope(() => _client, () => _strengthA.Value, () => _strengthB.Value, _outputState);
            _conditionMixer = CreateConditionMixer();
            _waveRouter = new DGLabWaveRouter(_log, _client, _persistent, SelectWaveForTime, _outputState);
            RegisterDefaultWaves();
            DamageHooks.UpdateContext(_client, _enableWaveEvents.Value ? _waveRouter : null, _outputState, _strengthEnvelope);
            _log.LogInfo("DG-Lab disconnected from Socket backend.");
        }

        private void HandleClientClosed(string reason)
        {
            _log.LogWarning("DG-Lab closed: " + reason);
            if (_intentionalDisconnect)
            {
                _intentionalDisconnect = false;
                return;
            }

            if (_autoSelectBackend.Value && !_runtimeEmbeddedBackend)
            {
                StartEmbeddedFallback("external backend closed unexpectedly");
                return;
            }

            ScheduleReconnect("backend closed unexpectedly");
        }

        private void TickAutoBackendProbe()
        {
            if (!_autoSelectBackend.Value || !_externalProbeActive || _runtimeEmbeddedBackend) return;
            if (_client != null && HasQrClientId(_client.ClientId))
            {
                _externalProbeActive = false;
                _externalProbeDeadline = -1f;
                _log.LogInfo("DG-Lab auto backend selected external Socket V2 backend: " + _serverUrl.Value);
                return;
            }
            if (_externalProbeDeadline < 0f || Time.realtimeSinceStartup < _externalProbeDeadline) return;

            _externalProbeActive = false;
            _externalProbeDeadline = -1f;
            _fallbackToEmbeddedAfterDisconnect = true;
            _log.LogWarning("DG-Lab external Socket V2 backend did not provide a clientId in time. Falling back to embedded QR backend.");
            DisconnectClientForReconnect();
            if (_fallbackToEmbeddedAfterDisconnect)
            {
                _fallbackToEmbeddedAfterDisconnect = false;
                StartEmbeddedFallback("external backend probe timed out");
            }
        }

        private void StartEmbeddedFallback(string reason)
        {
            _runtimeEmbeddedBackend = true;
            _autoBackendForcedEmbedded = true;
            _externalProbeActive = false;
            _externalProbeDeadline = -1f;
            _fallbackToEmbeddedAfterDisconnect = false;
            _pendingReconnectTime = Time.realtimeSinceStartup + 0.2f;
            _log.LogWarning("DG-Lab auto backend fallback to embedded mode: " + reason + ".");
        }

        private void TickPendingReconnect()
        {
            if (_pendingReconnectTime < 0f || Time.realtimeSinceStartup < _pendingReconnectTime) return;

            _pendingReconnectTime = -1f;
            InitializeClient();
        }

        private void DisconnectClientForReconnect()
        {
            if (_client == null) return;

            _intentionalDisconnect = true;
            _client.Disconnect();
        }

        private void DisconnectClientIntentional()
        {
            if (_client == null) return;

            _pendingReconnectTime = -1f;
            _intentionalDisconnect = true;
            _client.Disconnect();
        }

        internal void OpenQrUrlFromMenu()
        {
            OpenQrImageFromMenu();
        }

        internal void OpenQrImageFromMenu()
        {
            if (!HasQrClientId(_client != null ? _client.ClientId : null))
            {
                _log.LogInfo("DG-Lab QR image requested before clientId is assigned. Connecting to Socket backend now.");
                EnsureConnectedForQrFromMenu();
                return;
            }

            var path = EnsureQrImage(QrUrlText);
            Application.OpenURL("file:///" + path.Replace('\\', '/'));
            _log.LogInfo("DG-Lab opened local QR image: " + path);
        }

        internal void ApplyStrengthAFromMenu()
        {
            if (_client != null && _client.HasTarget) _client.SetStrengthA(_strengthA.Value);
        }

        internal void ApplyStrengthBFromMenu()
        {
            if (_client != null && _client.HasTarget) _client.SetStrengthB(_strengthB.Value);
        }

        private void RefreshOverlayMenu()
        {
            // Standard UI path is the plugin-owned persistent IMGUI runner.
        }

        private void HandleMessage(DGLab.BepInEx.Protocol.DGLabMessage msg)
        {
            _log.LogInfo("DG-Lab msg: " + msg.type + " | " + msg.message);
            if (!_enableQrOutput.Value) return;

            if (msg.type == "bind" && !string.IsNullOrEmpty(msg.clientId))
            {
                var qrUrl = _qrService.BuildQrUrl(msg.clientId);
                _log.LogInfo("DG-Lab backend assigned terminal clientId: " + msg.clientId);
                _log.LogInfo("DG-Lab QR URL: " + qrUrl);
                _log.LogInfo("DG-Lab local QR image: " + EnsureQrImage(qrUrl));
            }
        }

        private bool IsMenuTogglePressed()
        {
            if (!_enableMenu.Value) return false;

            if (_waitingForMenuKeyBind)
            {
                var captured = CaptureMenuKeyBinding();
                if (captured == KeyCode.None) return false;

                _menuToggleKey.Value = captured;
                _waitingForMenuKeyBind = false;
                _nativeConfiguredKeyWasDown = false;
                _nativeF10WasDown = false;
                _lastMenuToggleTime = Time.realtimeSinceStartup;
                _log.LogInfo("DG-Lab menu toggle key changed to " + captured + ".");
                return false;
            }

            if (Time.realtimeSinceStartup - _lastMenuToggleTime < 0.25f) return false;

            var pressed = false;

            if (Input.GetKeyDown(KeyCode.F10))
            {
                pressed = true;
            }
            else if (IsNativeKeyPressed(0x79, ref _nativeF10WasDown))
            {
                pressed = true;
            }
            else if (_menuToggleKey.Value != KeyCode.F10)
            {
                var nativeToggleKey = ToVirtualKey(_menuToggleKey.Value);
                pressed = Input.GetKeyDown(_menuToggleKey.Value) || (nativeToggleKey != 0 && IsNativeKeyPressed(nativeToggleKey, ref _nativeConfiguredKeyWasDown));
            }

            if (!pressed) return false;

            _lastMenuToggleTime = Time.realtimeSinceStartup;
            LogMenuDebug("DG-Lab input accepted: F10/config single key.");
            return true;
        }

        private static KeyCode CaptureMenuKeyBinding()
        {
            var e = Event.current;
            if (e != null && e.isKey && e.type == EventType.KeyDown && IsBindableMenuKey(e.keyCode)) return e.keyCode;

            for (var key = KeyCode.Backspace; key <= KeyCode.Menu; key++)
            {
                if (IsBindableMenuKey(key) && Input.GetKeyDown(key)) return key;
            }

            return KeyCode.None;
        }

        private static bool IsBindableMenuKey(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (key == KeyCode.Mouse0 || key == KeyCode.Mouse1 || key == KeyCode.Mouse2 || key == KeyCode.Mouse3 || key == KeyCode.Mouse4 || key == KeyCode.Mouse5 || key == KeyCode.Mouse6) return false;
            if (key == KeyCode.LeftAlt || key == KeyCode.RightAlt || key == KeyCode.LeftControl || key == KeyCode.RightControl || key == KeyCode.LeftShift || key == KeyCode.RightShift || key == KeyCode.LeftCommand || key == KeyCode.RightCommand) return false;
            return true;
        }

        private static bool IsNativeKeyPressed(int virtualKeyCode, ref bool wasDown)
        {
            var isDown = IsNativeKeyDown(virtualKeyCode);
            var pressed = isDown && !wasDown;
            wasDown = isDown;
            return pressed;
        }

        private static bool IsNativeKeyDown(int virtualKeyCode)
        {
            return (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;
        }

        private static int ToVirtualKey(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z) return 0x41 + (key - KeyCode.A);
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return 0x30 + (key - KeyCode.Alpha0);
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return 0x60 + (key - KeyCode.Keypad0);
            if (key >= KeyCode.F1 && key <= KeyCode.F12) return 0x70 + (key - KeyCode.F1);
            if (key == KeyCode.Space) return 0x20;
            if (key == KeyCode.Tab) return 0x09;
            if (key == KeyCode.Escape) return 0x1B;
            if (key == KeyCode.Insert) return 0x2D;
            if (key == KeyCode.Delete) return 0x2E;
            if (key == KeyCode.Home) return 0x24;
            if (key == KeyCode.End) return 0x23;
            if (key == KeyCode.PageUp) return 0x21;
            if (key == KeyCode.PageDown) return 0x22;
            return 0;
        }

        private string EnsureQrImage(string scanUrl)
        {
            return _qrService != null ? _qrService.EnsureQrImage(scanUrl) : "<failed to initialize QR service>";
        }

        private bool HasQrClientId(string clientId)
        {
            return _qrService != null && _qrService.HasQrClientId(clientId);
        }

        private void LogQrUrls(string source)
        {
            if (!_enableQrOutput.Value) return;

            var clientId = _client != null ? _client.ClientId : null;
            if (!HasQrClientId(clientId))
            {
                _log.LogInfo("DG-Lab QR URL (" + source + "): waiting for Socket backend to assign clientId.");
                _log.LogInfo("DG-Lab Socket v2 QR format: https://www.dungeon-lab.com/app-download.php#DGLAB-SOCKET#<ws-backend-url>/<backend-assigned-clientId>");
                return;
            }

            _log.LogInfo("DG-Lab QR URL (" + source + "): " + QrUrlText);
            _log.LogInfo("DG-Lab local QR image (" + source + "): " + QrImageUrlText);
        }

        private void RefreshEmbeddedTerminalIdIfNeeded(bool forceWhenEmpty)
        {
            if (!_refreshEmbeddedTerminalIdOnStart.Value && (!forceWhenEmpty || !string.IsNullOrWhiteSpace(_embeddedTerminalId.Value))) return;

            _embeddedTerminalId.Value = GenerateSecureSessionId();
            Config.Save();
            _log.LogInfo("DG-Lab generated embedded terminal ID for this backend session: " + _embeddedTerminalId.Value);
        }

        private static string GenerateSecureSessionId()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return new Guid(bytes).ToString("D");
        }

        private string[] SelectWaveForTime(string key, string[] defaultWave)
        {
            if (_enableTimeBasedWaves == null || !_enableTimeBasedWaves.Value) return defaultWave;

            var now = DateTime.Now.TimeOfDay;
            if (IsNowInRange(now, _intenseTimeRange.Value)) return DGLabWaveLibrary.IntensePulse;
            if (IsNowInRange(now, _gentleTimeRange.Value)) return DGLabWaveLibrary.GentlePulse;
            return defaultWave;
        }

        private string GetCurrentWaveProfileName()
        {
            if (_enableTimeBasedWaves == null || !_enableTimeBasedWaves.Value) return T("Default", "默认");

            var now = DateTime.Now.TimeOfDay;
            if (IsNowInRange(now, _intenseTimeRange.Value)) return T("Intense", "强烈");
            if (IsNowInRange(now, _gentleTimeRange.Value)) return T("Gentle", "柔和");
            return T("Default", "默认");
        }

        private static bool IsNowInRange(TimeSpan now, string range)
        {
            if (string.IsNullOrWhiteSpace(range)) return false;

            var parts = range.Split(new[] { '-' });
            if (parts.Length != 2) return false;
            if (!TryParseHourMinute(parts[0], out var start)) return false;
            if (!TryParseHourMinute(parts[1], out var end)) return false;

            if (start <= end) return now >= start && now <= end;
            return now >= start || now <= end;
        }

        private static bool TryParseHourMinute(string value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var parts = value.Trim().Split(new[] { ':' });
            if (parts.Length != 2) return false;

            int hour;
            int minute;
            if (!int.TryParse(parts[0], out hour) || !int.TryParse(parts[1], out minute)) return false;
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return false;

            time = new TimeSpan(hour, minute, 0);
            return true;
        }

        private void ObserveBodyState(Body body)
        {
            if (_lastObservedBody != body || !_bodyObservationBaselineReady)
            {
                _lastObservedBody = body;
                _bodyObservationBaselineReady = true;
                _lastObservedShock = body.shock;
                _lastObservedUpperPain = DGLabBodyBinding.GetBoundPain(body, _channelABodyParts.Value);
                _lastObservedLowerPain = DGLabBodyBinding.GetBoundPain(body, _channelBBodyParts.Value);
                _lastObservedConsciousness = body.consciousness;
                if (_realtimeTestLog.Value) _log.LogInfo("DG-Lab realtime test: observe baseline shock=" + _lastObservedShock.ToString("0.0") + ", ABindPain=" + _lastObservedUpperPain.ToString("0.0") + ", BBindPain=" + _lastObservedLowerPain.ToString("0.0") + ", consciousness=" + _lastObservedConsciousness.ToString("0.0"));
                return;
            }

            var shockRise = body.shock - _lastObservedShock;
            var upperPain = DGLabBodyBinding.GetBoundPain(body, _channelABodyParts.Value);
            var lowerPain = DGLabBodyBinding.GetBoundPain(body, _channelBBodyParts.Value);
            var upperPainRise = upperPain - _lastObservedUpperPain;
            var lowerPainRise = lowerPain - _lastObservedLowerPain;
            var consciousnessDrop = _lastObservedConsciousness - body.consciousness;

            if (body.shock >= 20f && shockRise > 5f)
            {
                DamageHooks.TriggerBodyStateSpike("body-shock", ScaleCriticalSeverity(body.shock / 55f));
            }
            if (upperPain >= 20f && upperPainRise > 5f)
            {
                _log.LogInfo("DG-Lab upper body pain spike: pain=" + upperPain.ToString("0.0"));
                _outputState?.PushInstantCondition("pain");
                _strengthEnvelope?.TriggerSpike(1, ScaleBodySeverity(upperPain / 85f), "upper-pain");
                _waveRouter?.TriggerEvent("damage");
            }
            if (lowerPain >= 20f && lowerPainRise > 5f)
            {
                _log.LogInfo("DG-Lab lower body pain spike: pain=" + lowerPain.ToString("0.0"));
                _outputState?.PushInstantCondition("pain");
                _strengthEnvelope?.TriggerSpike(2, ScaleBodySeverity(lowerPain / 85f), "lower-pain");
                _waveRouter?.TriggerEvent("damage");
            }
            if (consciousnessDrop > 20f)
            {
                DamageHooks.TriggerBodyStateSpike("body-consciousness", ScaleCriticalSeverity(consciousnessDrop / 75f));
            }

            _lastObservedShock = body.shock;
            _lastObservedUpperPain = upperPain;
            _lastObservedLowerPain = lowerPain;
            _lastObservedConsciousness = body.consciousness;
        }

        private string GetInactiveBodyReason(Body body)
        {
            if (body == null) return "no-body";
            if (body.limbs == null || body.limbs.Length < 15) return "no-limbs";
            if (WorldGeneration.world == null) return "no-world";
            if (WorldGeneration.world.generatingWorld || !WorldGeneration.world.worldExists) return "no-world";
            return null;
        }

        private void ClearOutputForInactiveBody(string reason)
        {
            reason = string.IsNullOrEmpty(reason) ? "no-body" : reason;
            if (_outputClearedForNoBody && _lastInactiveReason == reason) return;

            _outputClearedForNoBody = true;
            _lastInactiveReason = reason;
            _strengthEnvelope?.Clear("no-body");
            _persistent?.Stop("death");
            _persistent?.Stop("critical");
            _outputState?.Reset(reason);
            _wasDead = false;
            _wasCritical = false;
            _lastObservedShock = 0f;
            _lastObservedUpperPain = 0f;
            _lastObservedLowerPain = 0f;
            _lastObservedConsciousness = 100f;
            _lastObservedBody = null;
            _bodyObservationBaselineReady = false;
            _log.LogInfo("DG-Lab cleared runtime output because no active gameplay body is available: " + reason + ".");
        }

        private static float ScaleBodySeverity(float value)
        {
            value = Mathf.Clamp01(value);
            return Mathf.Clamp01(0.1f + value * value * 0.8f);
        }

        private static float ScaleCriticalSeverity(float value)
        {
            value = Mathf.Clamp01(value);
            return Mathf.Clamp01(0.18f + value * 0.9f);
        }

        private void RegisterDefaultWaves()
        {
            if (!_enableWaveEvents.Value) return;

            _waveRouter.RegisterEvent("damage", DGLabWaveLibrary.DamagePulse, WaveEnabled("DamageWave"), _damageWaveDuration, _damageWaveCooldown);
            _waveRouter.RegisterEvent("impact", DGLabWaveLibrary.ImpactPulse, WaveEnabled("ImpactWave"), _impactWaveDuration, _impactWaveCooldown);
            _waveRouter.RegisterEvent("break", DGLabWaveLibrary.BreakPulse, WaveEnabled("BreakWave"), _breakWaveDuration, _breakWaveCooldown);
            _waveRouter.RegisterEvent("dislocate", DGLabWaveLibrary.DislocatePulse, WaveEnabled("DislocateWave"), _dislocateWaveDuration, _dislocateWaveCooldown);
            _waveRouter.RegisterEvent("dismember", DGLabWaveLibrary.DismemberPulse, WaveEnabled("DismemberWave"), _dismemberWaveDuration, _dismemberWaveCooldown);
            _waveRouter.RegisterEvent("selfharm", DGLabWaveLibrary.SelfHarmPulse, WaveEnabled("SelfHarmWave"), _selfHarmWaveDuration, _selfHarmWaveCooldown);
            _waveRouter.RegisterEvent("shock", DGLabWaveLibrary.ShockSpike, WaveEnabled("ShockWave"), _impactWaveDuration, _impactWaveCooldown);
            _waveRouter.RegisterEvent("treatment-sting", DGLabWaveLibrary.Sting, WaveEnabled("TreatmentStingWave"), _damageWaveDuration, _damageWaveCooldown);
        }

        public void OnDestroy()
        {
            if (!_applicationQuitting)
            {
                _log?.LogWarning("DG-Lab plugin OnDestroy ignored because the application is not quitting. Keeping backend and UI runner alive.");
                EnsureStandaloneImGuiRunner();
                _nextRunnerHealthCheckTime = Time.realtimeSinceStartup + 0.5f;
                if (_useEmbeddedServer != null && _useEmbeddedServer.Value) ScheduleReconnect("plugin object was destroyed before application quit");
                return;
            }

            _log?.LogWarning("DG-Lab plugin OnDestroy invoked; shutting down backend and UI runner.");
            if (_harmony != null) _harmony.UnpatchSelf();
            if (_standaloneImGuiRunner != null) Destroy(_standaloneImGuiRunner.gameObject);
            DisconnectClientIntentional();
        }

        public void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void ScheduleReconnect(string reason)
        {
            if (!_enabled.Value || !IsEmbeddedBackendActive || _pendingReconnectTime >= 0f) return;

            _pendingReconnectTime = Time.realtimeSinceStartup + 1f;
            _log?.LogWarning("DG-Lab embedded backend reconnect scheduled: " + reason);
        }
    }
}
