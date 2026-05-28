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
        public const string PluginVersion = "0.2.1";

        private ManualLogSource _log;
        private DGLabClient _client;
        private string _advancedConfigPath;

        private bool _menuOpen;
        private bool _nativeF10WasDown;
        private bool _nativeF9WasDown;
        private bool _nativeMiniOverlayKeyWasDown;
        private bool _nativeWaveMonitorKeyWasDown;
        private bool _nativeConfiguredKeyWasDown;
        private int _lastUiToggleFrame = -1;
        private float _lastMenuToggleTime = -10f;
        private float _lastWaveMonitorToggleTime = -10f;
        private float _lastMiniOverlayToggleTime = -10f;
        private float _lastReconnectTime = -10f;
        private float _pendingReconnectTime = -1f;
        private bool _intentionalDisconnect;
        private bool _runtimeEmbeddedBackend;
        private bool _autoBackendForcedEmbedded;
        private bool _externalProbeActive;
        private float _externalProbeDeadline = -1f;
        private bool _updateLogged;
        private bool _startLogged;
        private bool _applicationQuitting;
        private int _lastHostedUpdateFrame = -1;
        private float _nextRunnerHealthCheckTime = 2f;
        private int _runnerCreateAttempts;
        private DGLabXuaWindow _xuaWindow;
        private DGLabMiniOverlayWindow _miniOverlayWindow;
        private DGLabWaveMonitorWindow _waveMonitorWindow;
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
        private bool _wasCritical;
        private bool _waitingForMenuKeyBind;
        private string _waitingForUiKeyBind;
        private Harmony _harmony;
        private bool _qrPanelExpanded = true;

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
            _log.LogInfo("DG-Lab menu toggle default/emergency key: F10. Optional Alt modifier is configurable.");
            _log.LogInfo("DG-Lab forced menu diagnostics enabled.");
            _log.LogInfo(_useEmbeddedServer.Value ? "DG-Lab embedded backend mode enabled. A QR will be generated for the phone app to connect to this game." : "DG-Lab external backend mode enabled. Start the backend before launching the game.");
            _log.LogInfo("DG-Lab external backend profile: " + _externalBackendProfile.Value + ", URL=" + ResolveExternalBackendUrl());
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
                GetOrCreateEmbeddedTerminalId,
                () => _embeddedServerAddress.Value,
                () => _embeddedServerPort.Value,
                () => ResolveExternalBackendUrl(),
                () => _qrWebSocketUrl.Value,
                () => Path.Combine(Paths.BepInExRootPath, "cache", "DG-Lab"));
            GetOrCreateEmbeddedTerminalId();
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
                    _strengthEnvelope,
                    () => _channelABodyParts.Value,
                    () => _channelBBodyParts.Value);
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
                if (_runtimeEmbeddedBackend)
                {
                    RefreshEmbeddedTerminalIdIfNeeded(true);
                    _client = new DGLabClient("0.0.0.0", _embeddedServerPort.Value, _embeddedTerminalId.Value);
                }
                else
                {
                    var externalUrl = ResolveExternalBackendUrl();
                    if (string.IsNullOrWhiteSpace(externalUrl))
                    {
                        _runtimeEmbeddedBackend = true;
                        RefreshEmbeddedTerminalIdIfNeeded(true);
                        _client = new DGLabClient("0.0.0.0", _embeddedServerPort.Value, _embeddedTerminalId.Value);
                    }
                    else
                    {
                        _client = new DGLabClient(externalUrl);
                    }
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
                _log.LogInfo(_runtimeEmbeddedBackend ? "DG-Lab embedded WebSocket backend initialized: 0.0.0.0:" + _embeddedServerPort.Value : "DG-Lab external WebSocket client initialized: " + ResolveExternalBackendUrl());

                _persistent = new DGLabPersistentOutput(_client, null, _outputState, IsOutputChannelEnabled);
                _strengthEnvelope = new DGLabStrengthEnvelope(() => _client, () => _strengthA.Value, () => _strengthB.Value, _outputState);
                _conditionMixer = CreateConditionMixer();
                _waveRouter = new DGLabWaveRouter(_log, _client, _persistent, null, _outputState, IsOutputChannelEnabled);
                RegisterDefaultWaves();
                DamageHooks.UpdateContext(_client, _enableWaveEvents.Value ? _waveRouter : null, _outputState, _strengthEnvelope);
            }
            catch (System.Exception ex)
            {
                _log.LogError("DG-Lab WebSocket initialization failed. Menu will remain available.");
                _log.LogError(ex);
                _client = null;
                _persistent = new DGLabPersistentOutput(_client, null, _outputState, IsOutputChannelEnabled);
                _strengthEnvelope = new DGLabStrengthEnvelope(() => _client, () => _strengthA.Value, () => _strengthB.Value, _outputState);
                _conditionMixer = CreateConditionMixer();
                _waveRouter = new DGLabWaveRouter(_log, _client, _persistent, null, _outputState, IsOutputChannelEnabled);
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
                IsOutputChannelEnabled,
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
                var testWave = new[]
                {
                    "0A0A0A0A64646464",
                    "1414141464646464",
                    "1E1E1E1E64646464",
                    "2828282864646464"
                };
                if (_client != null && _client.HasTarget)
                {
                    if (IsOutputChannelEnabled(1))
                    {
                        _outputState?.SetWave(1, "hotkey", "test", testWave, 5);
                        _client.SendWaveA(testWave, 5);
                    }
                    if (IsOutputChannelEnabled(2))
                    {
                        _outputState?.SetWave(2, "hotkey", "test", testWave, 5);
                        _client.SendWaveB(testWave, 5);
                    }
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

            if (CapturePendingUiKeyBinding()) return;

            if (IsMenuTogglePressed())
            {
                _menuOpen = !_menuOpen;
                if (_xuaWindow != null) _xuaWindow.IsShown = _menuOpen;
                LogMenuDebug("DG-Lab menu toggled from " + source + ": " + (_menuOpen ? "open" : "closed"));
                RefreshOverlayMenu();
                _lastUiToggleFrame = Time.frameCount;
                return;
            }

            if (_lastUiToggleFrame != Time.frameCount && IsWaveMonitorTogglePressed())
            {
                InitializeWaveMonitorWindow();
                if (_waveMonitorWindow != null)
                {
                    _waveMonitorWindow.IsShown = !_waveMonitorWindow.IsShown;
                    LogMenuDebug("DG-Lab wave monitor toggled from " + source + ": " + (_waveMonitorWindow.IsShown ? "open" : "closed"));
                }
                _lastUiToggleFrame = Time.frameCount;
                return;
            }

            if (_lastUiToggleFrame != Time.frameCount && IsMiniOverlayTogglePressed())
            {
                _miniOverlayEnabled.Value = !_miniOverlayEnabled.Value;
                LogMenuDebug("DG-Lab status overlay toggled from " + source + ": " + (_miniOverlayEnabled.Value ? "open" : "closed"));
                _lastUiToggleFrame = Time.frameCount;
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

            if (body.sleeping)
            {
                ClearOutputForInactiveBody("sleep");
                return;
            }

            _outputClearedForNoBody = false;
            _lastInactiveReason = string.Empty;
            _strengthEnvelope?.Tick();
            _persistent?.Tick();

            var isCritical = body.isCriticallyDying;
            var isDead = !body.alive;

            ObserveBodyState(body);

            if (_enableWaveEvents == null || !_enableWaveEvents.Value) return;

            if (_enableConditionMixer != null && _enableConditionMixer.Value)
            {
                _conditionMixer?.Tick(body);
            }

            if (_enableDeathState.Value)
            {
                if (isDead) _waveRouter.StartPersistent("death", DGLabWaveLibrary.DeathLoop, _deathWaveDuration.Value);
                else _waveRouter.StopPersistent("death");
            }

            if (_enableCriticalState.Value)
            {
                if (isCritical && !isDead && !_wasCritical) _waveRouter.StartPersistent("critical", DGLabWaveLibrary.CriticalLoop, _criticalWaveDuration.Value);
                else if (!isCritical && _wasCritical) _waveRouter.StopPersistent("critical");
            }

            _wasCritical = isCritical && !isDead;
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

            if (_waveMonitorWindow != null && _waveMonitorWindow.IsShown) _waveMonitorWindow.OnGUI();
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

        private void InitializeWaveMonitorWindow()
        {
            if (_waveMonitorWindow != null) return;

            _waveMonitorWindow = new DGLabWaveMonitorWindow(this);
            LogMenuDebug("DG-Lab wave monitor initialized.");
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
        internal string ExternalBackendProfileText => _externalBackendProfile != null ? _externalBackendProfile.Value : "<not loaded>";
        internal string ExternalBackendUrlText => ResolveExternalBackendUrl();

        internal string ThirdPartyControllerUrlValue
        {
            get => _thirdPartyControllerUrl != null ? _thirdPartyControllerUrl.Value : string.Empty;
            set
            {
                if (_thirdPartyControllerUrl == null) return;
                var next = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
                if (string.Equals(_thirdPartyControllerUrl.Value, next, StringComparison.Ordinal)) return;
                _thirdPartyControllerUrl.Value = next;
                _advancedConfig?.Save();
            }
        }

        internal bool IsOfficialSocketProfile => _externalBackendProfile != null &&
            string.Equals(_externalBackendProfile.Value, "OfficialSocket", StringComparison.OrdinalIgnoreCase);

        internal bool IsThirdPartyControllerProfile => _externalBackendProfile != null &&
            string.Equals(_externalBackendProfile.Value, "ThirdPartyController", StringComparison.OrdinalIgnoreCase);

        internal bool ShowQrPanel => IsEmbeddedBackendActive || IsOfficialSocketProfile;

        internal bool IsBackendConnected => _client != null;

        internal bool QrPanelExpanded => _qrPanelExpanded;

        internal void ToggleQrPanelExpanded()
        {
            _qrPanelExpanded = !_qrPanelExpanded;
            if (!_qrPanelExpanded) InvalidateQrTexture();
        }

        internal void SwitchExternalBackendProfile(string profile)
        {
            if (_externalBackendProfile == null) return;
            if (string.Equals(_externalBackendProfile.Value, profile, StringComparison.OrdinalIgnoreCase)) return;
            _externalBackendProfile.Value = profile;
            _advancedConfig.Save();

            // Only force-reconnect to external if the new profile has a valid URL
            var url = ResolveExternalBackendUrl();
            if (!string.IsNullOrWhiteSpace(url) && !url.Equals(_serverUrl?.Value, StringComparison.OrdinalIgnoreCase))
            {
                _autoBackendForcedEmbedded = false;
                _runtimeEmbeddedBackend = false;
                _lastReconnectTime = -1f;
                _qrService?.InvalidateAddressCache();
                InitializeClient();
            }
        }

        internal bool MenuToggleAltRequired
        {
            get => _menuToggleAltRequired != null && _menuToggleAltRequired.Value;
            set { if (_menuToggleAltRequired != null) _menuToggleAltRequired.Value = value; }
        }

        internal string ClientIdText
        {
            get
            {
                return _client != null && !string.IsNullOrEmpty(_client.ClientId) ? _client.ClientId : "<not bound>";
            }
        }

        internal string TargetIdText => _client != null && !string.IsNullOrEmpty(_client.TargetId) ? _client.TargetId : "<not bound>";

        internal string MenuToggleKeyName => _menuToggleKey != null ? _menuToggleKey.Value.ToString() : "F10";

        internal string MiniOverlayToggleKeyName => _miniOverlayToggleKey != null ? _miniOverlayToggleKey.Value.ToString() : "F8";

        internal string WaveMonitorToggleKeyName => _waveMonitorToggleKey != null ? _waveMonitorToggleKey.Value.ToString() : "F9";

        internal string MiniOverlayToggleKeyDisplay => (_miniOverlayToggleAltRequired != null && _miniOverlayToggleAltRequired.Value ? "Alt+" : "") + MiniOverlayToggleKeyName;

        internal string WaveMonitorToggleKeyDisplay => (_waveMonitorToggleAltRequired != null && _waveMonitorToggleAltRequired.Value ? "Alt+" : "") + WaveMonitorToggleKeyName;

        internal bool WaitingForMenuKeyBind => _waitingForMenuKeyBind;

        internal bool WaitingForStatusOverlayKeyBind => string.Equals(_waitingForUiKeyBind, "status", StringComparison.OrdinalIgnoreCase);

        internal bool WaitingForWaveMonitorKeyBind => string.Equals(_waitingForUiKeyBind, "wave", StringComparison.OrdinalIgnoreCase);

        internal void BeginMenuKeyBind()
        {
            _waitingForMenuKeyBind = true;
            _waitingForUiKeyBind = null;
        }

        internal void BeginStatusOverlayKeyBind()
        {
            _waitingForUiKeyBind = "status";
            _waitingForMenuKeyBind = false;
        }

        internal void BeginWaveMonitorKeyBind()
        {
            _waitingForUiKeyBind = "wave";
            _waitingForMenuKeyBind = false;
        }

        internal void ResetMenuKeyBind()
        {
            _waitingForMenuKeyBind = false;
            if (string.Equals(_waitingForUiKeyBind, "menu", StringComparison.OrdinalIgnoreCase)) _waitingForUiKeyBind = null;
            _menuToggleKey.Value = KeyCode.F10;
            _nativeConfiguredKeyWasDown = false;
            _nativeF10WasDown = false;
        }

        internal void ResetStatusOverlayKeyBind()
        {
            if (string.Equals(_waitingForUiKeyBind, "status", StringComparison.OrdinalIgnoreCase)) _waitingForUiKeyBind = null;
            _miniOverlayToggleKey.Value = KeyCode.RightBracket;
            if (_miniOverlayToggleAltRequired != null) _miniOverlayToggleAltRequired.Value = true;
            _nativeMiniOverlayKeyWasDown = false;
        }

        internal void ResetWaveMonitorKeyBind()
        {
            if (string.Equals(_waitingForUiKeyBind, "wave", StringComparison.OrdinalIgnoreCase)) _waitingForUiKeyBind = null;
            _waveMonitorToggleKey.Value = KeyCode.LeftBracket;
            if (_waveMonitorToggleAltRequired != null) _waveMonitorToggleAltRequired.Value = true;
            _nativeWaveMonitorKeyWasDown = false;
            _nativeF9WasDown = false;
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

        private Texture2D _qrTexture;
        private string _qrTextureUrl;

        internal void InvalidateQrTexture()
        {
            if (_qrTexture != null) { UnityEngine.Object.Destroy(_qrTexture); _qrTexture = null; }
            _qrTextureUrl = null;
        }

        internal System.Collections.Generic.List<string> QrAddressCandidates =>
            _qrService != null ? _qrService.GetAdvertiseAddressList() : new System.Collections.Generic.List<string>();

        internal void SelectQrAddress(string address)
        {
            _qrService?.SetAdvertiseAddressOverride(address);
            InvalidateQrTexture();
        }

        internal Texture2D GetQrTexture()
        {
            var url = QrUrlText;
            if (url.StartsWith("<"))
            {
                InvalidateQrTexture();
                return null;
            }
            if (url == _qrTextureUrl && _qrTexture != null) return _qrTexture;
            var path = EnsureQrImage(url);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return null;
                if (_qrTexture != null) UnityEngine.Object.Destroy(_qrTexture);
                _qrTexture = tex;
                _qrTextureUrl = url;
                return _qrTexture;
            }
            catch { return null; }
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
            if (_autoBackendForcedEmbedded || (!_autoSelectBackend.Value && _useEmbeddedServer.Value)) return true;
            return _autoSelectBackend.Value && string.IsNullOrWhiteSpace(ResolveExternalBackendUrl());
        }

        internal string BackendModeText
        {
            get
            {
                if (_autoSelectBackend.Value) return _runtimeEmbeddedBackend ? T("Auto -> Embedded", "自动 -> 内置后端") : T("Auto -> External", "自动 -> 外部后端");
                return _runtimeEmbeddedBackend ? T("Embedded", "内置后端") : T("External", "外部后端");
            }
        }

        internal string WaveProfileText => T("Default", "默认");

        internal string RuntimeStrengthAText => _outputState != null ? _outputState.RuntimeStrengthA.ToString() : "0";

        internal string RuntimeStrengthBText => _outputState != null ? _outputState.RuntimeStrengthB.ToString() : "0";

        internal string MaxStrengthAText => _strengthA != null ? _strengthA.Value.ToString() : "0";

        internal string MaxStrengthBText => _strengthB != null ? _strengthB.Value.ToString() : "0";

        internal string EffectiveLimitAText => FormatEffectiveLimitText(1);

        internal string EffectiveLimitBText => FormatEffectiveLimitText(2);

        private string FormatEffectiveLimitText(int channel)
        {
            var configured = Mathf.Clamp(channel == 2 ? (_strengthB != null ? _strengthB.Value : 0) : (_strengthA != null ? _strengthA.Value : 0), 0, 200);
            if (_outputState == null || !_outputState.HasDeviceStrengthState) return configured.ToString();

            var device = Mathf.Clamp(channel == 2 ? _outputState.DeviceLimitB : _outputState.DeviceLimitA, 0, 200);
            var effective = Mathf.Min(configured, device);
            return device < configured ? effective + " (phone)" : effective.ToString();
        }

        internal bool IsOutputChannelEnabled(int channel)
        {
            var configured = Mathf.Clamp(channel == 2 ? (_strengthB != null ? _strengthB.Value : 0) : (_strengthA != null ? _strengthA.Value : 0), 0, 200);
            if (configured <= 0) return false;

            if (_outputState == null || !_outputState.HasDeviceStrengthState) return true;

            var deviceLimit = Mathf.Clamp(channel == 2 ? _outputState.DeviceLimitB : _outputState.DeviceLimitA, 0, 200);
            return deviceLimit > 0;
        }

        internal string ChannelABodyPartsValue
        {
            get => _channelABodyParts.Value;
            set => _channelABodyParts.Value = value == null ? string.Empty : value.Trim();
        }

        internal bool IsChannelABodyPartSelected(string token) => DGLabBodyBinding.BindingContainsToken(ChannelABodyPartsValue, token);

        internal void ToggleChannelABodyPart(string token)
        {
            ChannelABodyPartsValue = DGLabBodyBinding.ToggleBindingToken(ChannelABodyPartsValue, token);
        }

        internal string ChannelBBodyPartsValue
        {
            get => _channelBBodyParts.Value;
            set => _channelBBodyParts.Value = value == null ? string.Empty : value.Trim();
        }

        internal bool IsChannelBBodyPartSelected(string token) => DGLabBodyBinding.BindingContainsToken(ChannelBBodyPartsValue, token);

        internal void ToggleChannelBBodyPart(string token)
        {
            ChannelBBodyPartsValue = DGLabBodyBinding.ToggleBindingToken(ChannelBBodyPartsValue, token);
        }

        internal string LastOutputEventText => FormatLastOutputText();

        internal string LastWaveText => FormatWaveText(_outputState != null ? _outputState.LastWave : "none");

        internal string LastWaveSourceText => FormatDisplayKey(_outputState != null ? _outputState.LastWaveSource : "none", DisplayKeyKind.Event);

        internal string LastWaveProfileText => FormatWaveText(_outputState != null ? _outputState.LastWave : "none");

        internal string[] LastWaveFrames => _outputState != null && _outputState.LastWaveFrames != null ? (string[])_outputState.LastWaveFrames.Clone() : null;

        internal int LastWaveDurationSeconds => _outputState != null ? _outputState.LastWaveDurationSeconds : 0;

        internal string LastWaveSourceTextA => FormatDisplayKey(_outputState != null ? _outputState.LastWaveSourceA : "none", DisplayKeyKind.Event);

        internal string LastWaveProfileTextA => FormatWaveText(_outputState != null ? _outputState.LastWaveA : "none");

        internal string[] LastWaveFramesA => _outputState != null && _outputState.LastWaveFramesA != null ? (string[])_outputState.LastWaveFramesA.Clone() : null;

        internal int LastWaveDurationSecondsA => _outputState != null ? _outputState.LastWaveDurationSecondsA : 0;

        internal string LastWaveSourceTextB => FormatDisplayKey(_outputState != null ? _outputState.LastWaveSourceB : "none", DisplayKeyKind.Event);

        internal string LastWaveProfileTextB => FormatWaveText(_outputState != null ? _outputState.LastWaveB : "none");

        internal string[] LastWaveFramesB => _outputState != null && _outputState.LastWaveFramesB != null ? (string[])_outputState.LastWaveFramesB.Clone() : null;

        internal int LastWaveDurationSecondsB => _outputState != null ? _outputState.LastWaveDurationSecondsB : 0;

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
                case "pain1": return T("Discomfort", "不适");
                case "pain2": return T("Pain", "疼痛");
                case "pain3": return T("Severe pain", "剧烈疼痛");
                case "pain4": return T("Agonizing pain", "剧痛难忍");
                case "confused1": return T("Dizziness", "困惑");
                case "confused2": return T("Confusion", "非常困惑");
                case "confused3": return T("Near unconscious", "昏厥");
                case "injury": return T("Injury", "损伤");
                case "fracture": return T("Fracture", "骨折");
                case "dislocation": return T("Dislocation", "脱臼");
                case "bleeding": return T("Bleeding", "出血");
                case "bleeding1": return T("Minor bleeding", "轻微出血");
                case "bleeding2": return T("Moderate bleeding", "中度出血");
                case "bleeding3": return T("Severe bleeding", "严重出血");
                case "bleeding4": return T("Catastrophic bleeding", "灾难性出血");
                case "blood-loss": return T("Hypovolemic", "低血容量");
                case "hypotension": return T("Hypotension", "低血压");
                case "hypertension": return T("Hypertension", "高血压");
                case "hypotension1": return T("Mild hypotension", "轻度低血压");
                case "hypotension2": return T("Moderate hypotension", "中度低血压");
                case "hypotension3": return T("Severe hypotension", "严重低血压");
                case "hypotension4": return T("Fatal hypotension", "致命低血压");
                case "hypertension1": return T("Mild hypertension", "轻度高血压");
                case "hypertension2": return T("Moderate hypertension", "中度高血压");
                case "hypertension3": return T("Severe hypertension", "严重高血压");
                case "hypertension4": return T("Fatal hypertension", "致命高血压");
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

        internal bool HasRuntimeOutput => _outputState != null && (_outputState.RuntimeStrengthA > 0 || _outputState.RuntimeStrengthB > 0);

        internal bool MiniOverlayEnabledValue
        {
            get => _miniOverlayEnabled.Value;
            set => _miniOverlayEnabled.Value = value;
        }

        internal bool WaveMonitorEnabledValue
        {
            get => _waveMonitorWindow != null && _waveMonitorWindow.IsShown;
            set
            {
                InitializeWaveMonitorWindow();
                if (_waveMonitorWindow != null) _waveMonitorWindow.IsShown = value;
            }
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

        internal void SetWaveMonitorOpen(bool isOpen)
        {
            if (isOpen) InitializeWaveMonitorWindow();
            if (_waveMonitorWindow != null) _waveMonitorWindow.IsShown = isOpen;
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
            _qrPanelExpanded = true;
            if (_autoSelectBackend.Value && !_runtimeEmbeddedBackend) _autoBackendForcedEmbedded = false;
            _qrService?.InvalidateAddressCache();
            InitializeClient();
        }

        internal void EnsureConnectedForQrFromMenu()
        {
            if (HasQrClientId(_client != null ? _client.ClientId : null))
            {
                InvalidateQrTexture();
                EnsureQrImage(QrUrlText);
                return;
            }

            if (_client == null)
            {
                _log.LogInfo("DG-Lab QR refresh ignored because the backend is disconnected. Use Restart Backend first.");
                return;
            }

            _log.LogInfo("DG-Lab QR refresh requested, but the backend has not assigned a clientId yet.");
        }

        internal void DisconnectFromMenu()
        {
            _pendingReconnectTime = -1f;
            _externalProbeActive = false;
            _externalProbeDeadline = -1f;
            DisconnectClientIntentional();
            _client = null;
            _qrPanelExpanded = false;
            InvalidateQrAfterDisconnect();
            _persistent = new DGLabPersistentOutput(_client, null, _outputState, IsOutputChannelEnabled);
            _strengthEnvelope = new DGLabStrengthEnvelope(() => _client, () => _strengthA.Value, () => _strengthB.Value, _outputState);
            _conditionMixer = CreateConditionMixer();
            _waveRouter = new DGLabWaveRouter(_log, _client, _persistent, null, _outputState, IsOutputChannelEnabled);
            RegisterDefaultWaves();
            DamageHooks.UpdateContext(_client, _enableWaveEvents.Value ? _waveRouter : null, _outputState, _strengthEnvelope);
            _log.LogInfo("DG-Lab disconnected from Socket backend.");
        }

        private void HandleClientClosed(string reason)
        {
            _log.LogWarning("DG-Lab closed: " + reason);
            InvalidateQrTexture();
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
            _log.LogWarning("DG-Lab external Socket V2 backend did not provide a clientId in time. Falling back to embedded QR backend.");
            DisconnectClientForReconnect();
            StartEmbeddedFallback("external backend probe timed out");
        }

        private void StartEmbeddedFallback(string reason)
        {
            _runtimeEmbeddedBackend = true;
            _autoBackendForcedEmbedded = true;
            _externalProbeActive = false;
            _externalProbeDeadline = -1f;
            GetOrCreateEmbeddedTerminalId();
            InvalidateQrTexture();
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

        private void InvalidateQrAfterDisconnect()
        {
            InvalidateQrTexture();
        }

        internal void OpenQrUrlFromMenu()
        {
            OpenQrImageFromMenu();
        }

        internal void OpenQrImageFromMenu()
        {
            if (!HasQrClientId(_client != null ? _client.ClientId : null))
            {
                _log.LogInfo(_client == null
                    ? "DG-Lab QR image requested while the backend is disconnected. Use Restart Backend first."
                    : "DG-Lab QR image requested before clientId is assigned.");
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
            if (_debugLog.Value) _log.LogInfo("DG-Lab msg: " + msg.type + " | " + msg.message);
            TryParseDeviceStrengthState(msg.message);
            if (!_enableQrOutput.Value) return;

            if (msg.type == "bind" && !string.IsNullOrEmpty(msg.clientId) && (!_runtimeEmbeddedBackend || msg.message == "200"))
            {
                var qrId = _runtimeEmbeddedBackend ? GetOrCreateEmbeddedTerminalId() : msg.clientId;
                var qrUrl = _qrService.BuildQrUrl(qrId);
                _log.LogInfo(_runtimeEmbeddedBackend ? "DG-Lab embedded app paired." : "DG-Lab backend assigned terminal clientId: " + msg.clientId);
                if (_runtimeEmbeddedBackend) _strengthEnvelope?.ForceResend("paired");
                _log.LogInfo("DG-Lab QR URL: " + qrUrl);
                _log.LogInfo("DG-Lab local QR image: " + EnsureQrImage(qrUrl));
            }
        }

        private void TryParseDeviceStrengthState(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !message.StartsWith("strength-", StringComparison.OrdinalIgnoreCase)) return;
            var parts = message.Substring("strength-".Length).Split('+');
            if (parts.Length < 4) return;
            if (!int.TryParse(parts[0], out var strengthA)) return;
            if (!int.TryParse(parts[1], out var strengthB)) return;
            if (!int.TryParse(parts[2], out var limitA)) return;
            if (!int.TryParse(parts[3], out var limitB)) return;
            _outputState?.SetDeviceStrengthState(strengthA, strengthB, limitA, limitB);
        }

        private bool IsMenuTogglePressed()
        {
            if (!_enableMenu.Value) return false;

            if (_waitingForMenuKeyBind)
            {
                var captured = CaptureMenuKeyBinding();
                if (captured == KeyCode.None) return false;

                _menuToggleKey.Value = captured;
                _menuToggleAltRequired.Value = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || IsNativeKeyDown(0x12);
                _waitingForMenuKeyBind = false;
                _nativeConfiguredKeyWasDown = false;
                _nativeF10WasDown = false;
                _lastMenuToggleTime = Time.realtimeSinceStartup;
                _log.LogInfo("DG-Lab menu toggle key changed to " + captured + ". Alt required=" + _menuToggleAltRequired.Value + ".");
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

            if (!pressed && _menuToggleKey.Value != KeyCode.F10)
            {
                var nativeToggleKey = ToVirtualKey(_menuToggleKey.Value);
                pressed = Input.GetKeyDown(_menuToggleKey.Value) || (nativeToggleKey != 0 && IsNativeKeyPressed(nativeToggleKey, ref _nativeConfiguredKeyWasDown));
            }

            if (!pressed) return false;

            if (_menuToggleAltRequired != null && _menuToggleAltRequired.Value)
            {
                var altDownNow = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || IsNativeKeyDown(0x12);
                if (!altDownNow) return false;
            }

            _lastMenuToggleTime = Time.realtimeSinceStartup;
            LogMenuDebug("DG-Lab input accepted: F10/config single key.");
            return true;
        }

        private bool IsWaveMonitorTogglePressed()
        {
            if (!_enableMenu.Value || _waitingForMenuKeyBind || !string.IsNullOrEmpty(_waitingForUiKeyBind)) return false;
            if (Time.realtimeSinceStartup - _lastWaveMonitorToggleTime < 0.25f) return false;

            var key = _waveMonitorToggleKey != null ? _waveMonitorToggleKey.Value : KeyCode.F9;
            var nativeKey = ToVirtualKey(key);
            var pressed = false;
            if (Input.GetKeyDown(key))
            {
                pressed = true;
                if (nativeKey != 0) _nativeWaveMonitorKeyWasDown = IsNativeKeyDown(nativeKey);
                if (key == KeyCode.F9) _nativeF9WasDown = IsNativeKeyDown(0x78);
            }
            else if (nativeKey != 0 && IsNativeKeyPressed(nativeKey, ref _nativeWaveMonitorKeyWasDown))
            {
                pressed = true;
            }

            if (!pressed) return false;

            if (_waveMonitorToggleAltRequired != null && _waveMonitorToggleAltRequired.Value && !IsAltDown()) return false;

            _lastWaveMonitorToggleTime = Time.realtimeSinceStartup;
            return true;
        }

        private bool IsMiniOverlayTogglePressed()
        {
            if (!_enableMenu.Value || _waitingForMenuKeyBind || !string.IsNullOrEmpty(_waitingForUiKeyBind)) return false;
            if (Time.realtimeSinceStartup - _lastMiniOverlayToggleTime < 0.25f) return false;

            var key = _miniOverlayToggleKey != null ? _miniOverlayToggleKey.Value : KeyCode.F8;
            var nativeKey = ToVirtualKey(key);
            var pressed = false;
            if (Input.GetKeyDown(key))
            {
                pressed = true;
                if (nativeKey != 0) _nativeMiniOverlayKeyWasDown = IsNativeKeyDown(nativeKey);
            }
            else if (nativeKey != 0 && IsNativeKeyPressed(nativeKey, ref _nativeMiniOverlayKeyWasDown))
            {
                pressed = true;
            }

            if (!pressed) return false;

            if (_miniOverlayToggleAltRequired != null && _miniOverlayToggleAltRequired.Value && !IsAltDown()) return false;

            _lastMiniOverlayToggleTime = Time.realtimeSinceStartup;
            return true;
        }

        private bool CapturePendingUiKeyBinding()
        {
            if (string.IsNullOrEmpty(_waitingForUiKeyBind)) return false;

            var captured = CaptureMenuKeyBinding();
            if (captured == KeyCode.None) return true;

            if (string.Equals(_waitingForUiKeyBind, "status", StringComparison.OrdinalIgnoreCase))
            {
                _miniOverlayToggleKey.Value = captured;
                if (_miniOverlayToggleAltRequired != null) _miniOverlayToggleAltRequired.Value = IsAltDown();
                _nativeMiniOverlayKeyWasDown = false;
                _lastMiniOverlayToggleTime = Time.realtimeSinceStartup;
                _log.LogInfo("DG-Lab output viewer toggle key changed to " + captured + ". Alt required=" + (_miniOverlayToggleAltRequired != null && _miniOverlayToggleAltRequired.Value) + ".");
            }
            else if (string.Equals(_waitingForUiKeyBind, "wave", StringComparison.OrdinalIgnoreCase))
            {
                _waveMonitorToggleKey.Value = captured;
                if (_waveMonitorToggleAltRequired != null) _waveMonitorToggleAltRequired.Value = IsAltDown();
                _nativeWaveMonitorKeyWasDown = false;
                _nativeF9WasDown = false;
                _lastWaveMonitorToggleTime = Time.realtimeSinceStartup;
                _log.LogInfo("DG-Lab wave viewer toggle key changed to " + captured + ". Alt required=" + (_waveMonitorToggleAltRequired != null && _waveMonitorToggleAltRequired.Value) + ".");
            }

            _waitingForUiKeyBind = null;
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

            // Detect function keys and navigation keys via native API that Unity may miss
            int[] nativeExtras = { 0x70,0x71,0x72,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7A,0x7B, // F1-F12
                                   0x2D,0x2E,0x24,0x23,0x21,0x22, // Insert,Delete,Home,End,PgUp,PgDn
                                   0xDB,0xDD }; // [ ]
            foreach (var vk in nativeExtras)
            {
                if ((GetAsyncKeyState(vk) & 0x8001) == 0x8001)
                {
                    var kc = VirtualKeyToKeyCode(vk);
                    if (kc != KeyCode.None && IsBindableMenuKey(kc)) return kc;
                }
            }

            return KeyCode.None;
        }

        private static KeyCode VirtualKeyToKeyCode(int vk)
        {
            if (vk >= 0x70 && vk <= 0x7B) return KeyCode.F1 + (vk - 0x70);
            if (vk == 0x2D) return KeyCode.Insert;
            if (vk == 0x2E) return KeyCode.Delete;
            if (vk == 0x24) return KeyCode.Home;
            if (vk == 0x23) return KeyCode.End;
            if (vk == 0x21) return KeyCode.PageUp;
            if (vk == 0x22) return KeyCode.PageDown;
            if (vk == 0xDB) return KeyCode.LeftBracket;
            if (vk == 0xDD) return KeyCode.RightBracket;
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

        private static bool IsAltDown()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) || IsNativeKeyDown(0x12);
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
            if (key == KeyCode.LeftBracket) return 0xDB;
            if (key == KeyCode.RightBracket) return 0xDD;
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

            ForceRefreshEmbeddedTerminalId();
        }

        private void ForceRefreshEmbeddedTerminalId()
        {
            _embeddedTerminalId.Value = GenerateSecureSessionId();
            Config.Save();
            _log.LogInfo("DG-Lab generated embedded terminal ID for this backend session: " + _embeddedTerminalId.Value);
        }

        private string GetOrCreateEmbeddedTerminalId()
        {
            if (!string.IsNullOrWhiteSpace(_embeddedTerminalId.Value)) return _embeddedTerminalId.Value;
            RefreshEmbeddedTerminalIdIfNeeded(true);
            return _embeddedTerminalId.Value;
        }

        private static string GenerateSecureSessionId()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return new Guid(bytes).ToString("D");
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

            if (!body.sleeping && body.shock >= 20f && shockRise > 5f && HasInjuryEvidenceForShock(body, upperPainRise, lowerPainRise))
            {
                DamageHooks.TriggerBodyStateSpike("body-shock", ScaleCriticalSeverity(body.shock / 55f));
            }
            if (upperPain >= 20f && upperPainRise > 5f)
            {
                _log.LogInfo("DG-Lab upper body pain spike: pain=" + upperPain.ToString("0.0"));
                _outputState?.PushInstantCondition("pain");
                _strengthEnvelope?.TriggerSpike(1, ScaleBodySeverity(upperPain / 85f), "upper-pain");
                _waveRouter?.TriggerEvent("damage", 1);
            }
            if (lowerPain >= 20f && lowerPainRise > 5f)
            {
                _log.LogInfo("DG-Lab lower body pain spike: pain=" + lowerPain.ToString("0.0"));
                _outputState?.PushInstantCondition("pain");
                _strengthEnvelope?.TriggerSpike(2, ScaleBodySeverity(lowerPain / 85f), "lower-pain");
                _waveRouter?.TriggerEvent("damage", 2);
            }
            if (!body.sleeping && consciousnessDrop > 20f)
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
            return null;
        }

        private void ClearOutputForInactiveBody(string reason)
        {
            reason = string.IsNullOrEmpty(reason) ? "no-body" : reason;
            if (_outputClearedForNoBody && _lastInactiveReason == reason) return;

            _outputClearedForNoBody = true;
            _lastInactiveReason = reason;
            _strengthEnvelope?.Clear(reason);
            _persistent?.Stop("death");
            _persistent?.Stop("critical");
            if (_client != null && _client.HasTarget)
            {
                _client.SetStrengthA(0);
                _client.SetStrengthB(0);
                _client.ClearWaveA();
                _client.ClearWaveB();
            }
            _outputState?.Reset(reason);
            _wasCritical = false;
            _lastObservedShock = 0f;
            _lastObservedUpperPain = 0f;
            _lastObservedLowerPain = 0f;
            _lastObservedConsciousness = 100f;
            _lastObservedBody = null;
            _bodyObservationBaselineReady = false;
            _log.LogInfo("DG-Lab cleared runtime output because no active gameplay body is available: " + reason + ".");
        }

        private static bool HasInjuryEvidenceForShock(Body body, float upperPainRise, float lowerPainRise)
        {
            if (body == null) return false;
            if (upperPainRise > 2f || lowerPainRise > 2f) return true;
            if (ValidBodyPercent(body.averagePain, 0f) >= 18f) return true;
            if (ValidBodyPercent(body.traumaAmount, 0f) >= 8f) return true;
            if (body.totalBleedSpeed > 0.03f) return true;
            if (ValidBodyPercent(body.bloodVolume, 100f) <= 85f) return true;
            if (ValidBodyPercent(body.bloodOxygen, 100f) <= 88f) return true;
            if (body.painShock > 0.08f) return true;
            return body.inCardiacArrest;
        }

        private static float ValidBodyPercent(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            if (value < 0f) return fallback;
            return Mathf.Clamp(value, 0f, 100f);
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
            _waveRouter.RegisterEvent("treatment-sting", DGLabWaveLibrary.TreatmentSting, WaveEnabled("TreatmentStingWave"), _damageWaveDuration, _damageWaveCooldown);
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

        private string ResolveExternalBackendUrl()
        {
            if (_externalBackendProfile == null) return _serverUrl != null ? _serverUrl.Value : "";

            var profile = (_externalBackendProfile.Value ?? string.Empty).Trim();
            if (profile.Equals("OfficialSocket", StringComparison.OrdinalIgnoreCase))
            {
                if (_officialSocketUrl != null && !string.IsNullOrWhiteSpace(_officialSocketUrl.Value)) return _officialSocketUrl.Value.Trim();
                return string.Empty;
            }
            else
            {
                if (_thirdPartyControllerUrl != null && !string.IsNullOrWhiteSpace(_thirdPartyControllerUrl.Value)) return _thirdPartyControllerUrl.Value.Trim();
            }

            return _serverUrl != null ? _serverUrl.Value : string.Empty;
        }
    }
}
