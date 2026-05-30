using UnityEngine;

namespace DGLab.BepInEx
{
    internal sealed class DGLabXuaWindow
    {
        private const int WindowId = 54643321;
        private const float WindowWidth = 600f;
        private const float WindowHeight = 640f;
        private static readonly GUIContent SharedContent = new GUIContent();

        private readonly DGLabPlugin _owner;
        private Rect _windowRect = new Rect(20f, 20f, WindowWidth, WindowHeight);
        private Vector2 _scrollPosition;
        private bool _isMouseDownOnWindow;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _tooltipStyle;
        private string _tooltip = string.Empty;
        private float _scrollContentHeight = 1100f;

        public bool IsShown { get; set; }

        public DGLabXuaWindow(DGLabPlugin owner, bool isShown)
        {
            _owner = owner;
            IsShown = isShown;
        }

        public void OnGUI()
        {
            GUI.depth = -10000;
            GUI.Box(_windowRect, GUIContent.none);
            _windowRect = GUI.Window(WindowId, _windowRect, CreateWindowUI, _owner.T("DG-Lab EXP BodySync", "DG-Lab EXP 体感同步"));

            // Draw tooltip outside the window so it's not clipped
            if (!string.IsNullOrEmpty(_tooltip))
            {
                if (_tooltipStyle == null)
                    _tooltipStyle = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 6, 4, 4) };
                var mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                var tooltipContent = Content(_tooltip);
                var size = _tooltipStyle.CalcSize(tooltipContent);
                size.x = Mathf.Min(size.x + 12f, 480f);
                size.y = _tooltipStyle.CalcHeight(tooltipContent, size.x) + 8f;
                var tx = Mathf.Clamp(mp.x + 14f, 0f, Screen.width - size.x);
                var ty = Mathf.Clamp(mp.y + 14f, 0f, Screen.height - size.y);
                GUI.Box(new Rect(tx, ty, size.x, size.y), _tooltip, _tooltipStyle);
            }

            if (IsAnyMouseButtonOrScrollWheelDown())
            {
                var point = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                _isMouseDownOnWindow = _windowRect.Contains(point);
            }

            if (!_isMouseDownOnWindow || !IsAnyMouseButtonOrScrollWheel()) return;

            GUI.FocusWindow(WindowId);
            var currentPoint = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (_windowRect.Contains(currentPoint)) Input.ResetInputAxes();
        }

        private void CreateWindowUI(int id)
        {
            const float spacing = 10f;
            const float row = 24f;
            const float labelWidth = 120f;
            var posY = 26f;

            if (GUI.Button(new Rect(WindowWidth - 24f, 3f, 20f, 18f), "X"))
            {
                IsShown = false;
                _owner.SetMenuOpenFromWindow(false);
            }

            var contentWidth = WindowWidth - spacing * 2f;
            var x = spacing * 2f;
            var w = WindowWidth - spacing * 4f;
            var innerWidth = WindowWidth - spacing * 4f;

            var scrollViewportHeight = WindowHeight - posY - spacing;
            _scrollPosition = GUI.BeginScrollView(
                new Rect(spacing, posY, WindowWidth - spacing * 2f, scrollViewportHeight),
                _scrollPosition,
                new Rect(0f, 0f, innerWidth, Mathf.Max(scrollViewportHeight, _scrollContentHeight)));

            x = spacing;
            w = innerWidth - spacing * 2f;
            contentWidth = innerWidth;
            posY = 0f;

            // ── Status ──────────────────────────────────────────────────────
            var isOfficial = _owner.IsOfficialSocketProfile;
            var isOtc = _owner.IsOtcControllerProfile;
            var isBluetooth = _owner.IsBluetoothProfile;
            var profileItems = new[]
            {
                ButtonLayoutItem.Toggle(_owner.T("Official Socket", "官方 Socket"), isOfficial, () => _owner.SwitchExternalBackendProfile("OfficialSocket")),
                ButtonLayoutItem.Toggle(_owner.T("OTC Controller", "OTC 控制器"), isOtc, () => _owner.SwitchExternalBackendProfile("OtcController")),
                ButtonLayoutItem.Toggle(_owner.T("Bluetooth V2", "蓝牙 V2"), _owner.IsBluetoothV2Profile, () => _owner.SwitchExternalBackendProfile("BluetoothV2")),
                ButtonLayoutItem.Toggle(_owner.T("Bluetooth V3", "蓝牙 V3"), _owner.IsBluetoothV3Profile, () => _owner.SwitchExternalBackendProfile("BluetoothV3"))
            };
            var profileFlowHeight = MeasureButtonFlowHeight(w - labelWidth, row, profileItems);
            var backendStatusHeight = MeasureWrappedLabelHeight(_owner.BackendStatusDetailText, w - labelWidth, row);
            var bluetoothStatusHeight = isBluetooth ? MeasureWrappedLabelHeight(_owner.BluetoothStatusText, w, row) : 0f;
            var bleScannerHeight = isBluetooth ? MeasureBleScannerHeight(row) : 0f;
            var qrSocketHeight = _owner.ShowQrSocketStatus ? row + 4f : 0f;
            var statusHeight = 8f + row + 4f + row + 4f + backendStatusHeight + 4f + profileFlowHeight + 8f;
            if (isOtc) statusHeight += row + 4f + row + 8f;
            if (isBluetooth) statusHeight += row + 4f + bluetoothStatusHeight + 6f + bleScannerHeight + 8f;
            statusHeight += row + 4f + row + 4f + qrSocketHeight + spacing;
            GUI.Box(new Rect(0f, posY, contentWidth, statusHeight), "");
            var statusRowY = posY + 8f;
            GUI.Label(new Rect(x, statusRowY, w, row), Section(_owner.T("Status", "状态")));
            statusRowY += row + 4f;
            DrawPair(x, statusRowY, labelWidth, w, _owner.T("Mode", "模式"), _owner.BackendModeText);
            statusRowY += row + 4f;
            GUI.Label(new Rect(x, statusRowY, labelWidth, row), _owner.T("State", "状态") + ":");
            DrawWrappedLabel(new Rect(x + labelWidth, statusRowY, w - labelWidth, backendStatusHeight), _owner.BackendStatusDetailText);
            statusRowY += backendStatusHeight + 4f;

            // Profile toggle buttons
            GUI.Label(new Rect(x, statusRowY, labelWidth, row), _owner.T("Profile", "类型") + ":");
            var profileY = statusRowY;
            DrawButtonFlow(x + labelWidth, ref profileY, w - labelWidth, row, profileItems);

            statusRowY = profileY + 8f;
            if (isOtc)
            {
                GUI.Label(new Rect(x, statusRowY, labelWidth, row), _owner.T("OTC IP", "OTC IP") + ":");
                _owner.OtcControllerIpValue = GUI.TextField(new Rect(x + labelWidth, statusRowY, w - labelWidth, row), _owner.OtcControllerIpValue);
                statusRowY += row + 4f;
                DrawWrappedLabel(new Rect(x + labelWidth, statusRowY, w - labelWidth, row), _owner.T("Port/path are fixed: 60536/1", "端口和路径固定：60536/1"));
                statusRowY += row + 8f;
            }

            if (isBluetooth)
            {
                GUI.Label(new Rect(x, statusRowY, labelWidth, row), _owner.T("BLE Device", "蓝牙设备") + ":");
                _owner.BluetoothDeviceNameValue = GUI.TextField(new Rect(x + labelWidth, statusRowY, w - labelWidth, row), _owner.BluetoothDeviceNameValue);
                statusRowY += row + 4f;
                DrawWrappedLabel(new Rect(x, statusRowY, w, bluetoothStatusHeight), _owner.BluetoothStatusText);
                statusRowY += bluetoothStatusHeight + 6f;
                DrawBleScanner(x, ref statusRowY, w, row);
                statusRowY += 8f;
            }

            DrawPair(x, statusRowY, labelWidth, w, _owner.T("Client ID", "终端ID"), _owner.ClientIdText);
            statusRowY += row + 4f;
            DrawPair(x, statusRowY, labelWidth, w, _owner.T("Target ID", "设备ID"), _owner.TargetIdText);
            statusRowY += row + 4f;
            if (_owner.ShowQrSocketStatus)
            {
                DrawPair(x, statusRowY, labelWidth, w, _owner.T("QR Socket", "二维码地址"), _owner.QrWebSocketUrlText);
            }
            posY += statusHeight + 10f;

            if (_owner.ShowQrPanel)
            {
                // ── QR Code ─────────────────────────────────────────────────
                var backendConnected = _owner.IsBackendConnected;
                var showQrDetails = backendConnected && _owner.QrPanelExpanded;
                var qrTex = showQrDetails ? _owner.GetQrTexture() : null;
                var qrHeight = !backendConnected ? 32f : (showQrDetails ? (qrTex != null ? 200f : 50f) : 26f);
                GUI.Box(new Rect(0f, posY, contentWidth, 46f + qrHeight), "");
                GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("QR Code", "二维码")));
                GUI.enabled = backendConnected;
                var qrButtonLabel = showQrDetails ? _owner.T("Collapse", "收起") : _owner.T("Show QR", "显示二维码");
                var qrButtonWidth = Mathf.Min(CalcButtonWidth(qrButtonLabel, 80f, 160f), w * 0.45f);
                if (GUI.Button(new Rect(x + w - qrButtonWidth, posY + 7f, qrButtonWidth, row), qrButtonLabel))
                    _owner.ToggleQrPanelExpanded();
                GUI.enabled = true;
                if (!backendConnected)
                    GUI.Label(new Rect(x, posY + 34f, w, row), _owner.T("Backend disconnected. Restart the backend to generate a new QR code.", "后端已断开。请重启后端以生成新的二维码。"));
                else if (!showQrDetails)
                    GUI.Label(new Rect(x, posY + 34f, w, row), _owner.T("QR code hidden.", "二维码已省略。"));
                else if (qrTex != null)
                    GUI.DrawTexture(new Rect(x + (w - 192f) * 0.5f, posY + 34f, 192f, 192f), qrTex, ScaleMode.ScaleToFit);
                else
                    GUI.Label(new Rect(x, posY + 34f, w, row), _owner.T("Waiting for QR data…", "等待二维码数据…"));
                posY += 56f + qrHeight;
            }

            var y = posY;

            // ── Actions ─────────────────────────────────────────────────────
            var boxY = y;
            var actionItems = BuildActionItems();
            var actionRowsHeight = MeasureButtonFlowHeight(innerWidth - spacing * 2f, row, actionItems);
            var actionsBoxHeight = Mathf.Max(84f, 36f + actionRowsHeight + spacing);
            GUI.Box(new Rect(0f, boxY, innerWidth, actionsBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Actions", "操作")));
            y += 36f;
            DrawButtonFlow(spacing, ref y, innerWidth - spacing * 2f, row, actionItems);
            y = boxY + actionsBoxHeight + spacing;

            // ── Strength Limits ─────────────────────────────────────────────
            const float strengthBoxHeight = 110f;
            boxY = y;
            GUI.Box(new Rect(0f, boxY, innerWidth, strengthBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Strength Limits", "强度上限")));
            y += 36f;
            _owner.StrengthAValue = DrawSlider(_owner.T("Max A", "A上限"), _owner.StrengthAValue, 0, 200, spacing, y, labelWidth, innerWidth,
                _owner.T("Maximum runtime strength for channel A (0-200). Events scale up to this value.", "A 通道运行时强度上限 (0-200)，事件按比例缩放到此值。"));
            y += 36f;
            _owner.StrengthBValue = DrawSlider(_owner.T("Max B", "B上限"), _owner.StrengthBValue, 0, 200, spacing, y, labelWidth, innerWidth,
                _owner.T("Maximum runtime strength for channel B (0-200). Events scale up to this value.", "B 通道运行时强度上限 (0-200)，事件按比例缩放到此值。"));
            y += 36f;
            y = boxY + strengthBoxHeight + spacing;

            // ── Channel Bindings ─────────────────────────────────────────────
            const float bindingBoxHeight = 612f;
            boxY = y;
            GUI.Box(new Rect(0f, boxY, innerWidth, bindingBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Channel Bindings", "通道绑定")));
            y += 36f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Channel A", "A通道"));
            _owner.ChannelABodyPartsValue = GUI.TextField(new Rect(spacing + labelWidth, y, innerWidth - labelWidth - spacing * 2f, row), _owner.ChannelABodyPartsValue);
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Channel B", "B通道"));
            _owner.ChannelBBodyPartsValue = GUI.TextField(new Rect(spacing + labelWidth, y, innerWidth - labelWidth - spacing * 2f, row), _owner.ChannelBBodyPartsValue);
            y += row + 6f;
            DrawChannelPresetButtons(spacing, ref y, row, innerWidth);
            DrawBodyPartPicker(spacing, ref y, row, innerWidth);
            y = Mathf.Max(y, boxY + bindingBoxHeight) + spacing;

            // ── Condition Sampling ───────────────────────────────────────────
            boxY = y;
            var conditionWidth = innerWidth - spacing * 2f;
            var conditionIntro = _owner.T("The Ongoing Body State switch above only pauses sampling; these per-condition settings are preserved.",
                                          "上面的持续身体状态总开关只暂停采样；这里每个状态的启用和倍率配置都会保留。");
            var conditionIntroHeight = MeasureWrappedLabelHeight(conditionIntro, conditionWidth, 34f);
            var conditionBoxHeight = 36f + conditionIntroHeight + 6f + MeasureConditionConfigHeight(row, conditionWidth) + spacing;
            GUI.Box(new Rect(0f, boxY, innerWidth, conditionBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, conditionWidth, row), Section(_owner.T("Condition Sampling", "状态采样配置")));
            y += 36f;
            DrawWrappedLabel(new Rect(spacing, y, conditionWidth, conditionIntroHeight), conditionIntro);
            y += conditionIntroHeight + 6f;
            DrawConditionConfig(spacing, ref y, conditionWidth, row);
            y = boxY + conditionBoxHeight + spacing;

            // ── Settings ─────────────────────────────────────────────────────
            boxY = y;
            var colGap = 8f;
            var colW = (innerWidth - spacing * 2f - colGap * 2f) / 3f;
            var col1 = spacing;
            var col2 = col1 + colW + colGap;
            var col3 = col2 + colW + colGap;
            var languageHeight = MeasureLanguageButtonsHeight(new Rect(col1, 0f, innerWidth - spacing * 2f, row));
            var settingsBoxHeight = Mathf.Max(208f, 36f + row * 4f + 4f * 3f + 8f + languageHeight + spacing);
            GUI.Box(new Rect(0f, boxY, innerWidth, settingsBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Settings", "设置")));
            y += 36f;

            _owner.EnabledValue = TooltipToggle(new Rect(col1, y, colW, row), _owner.EnabledValue,
                _owner.T("Enable BodySync", "启用体感同步"),
                _owner.T("Master switch. Disabling stops all DG-Lab output.", "总开关，关闭后停止所有 DG-Lab 输出。"));
            _owner.EnableWaveEventsValue = TooltipToggle(new Rect(col2, y, colW, row), _owner.EnableWaveEventsValue,
                _owner.T("Event pulses", "事件脉冲"),
                _owner.T("Send waveform pulses on hit events in addition to strength changes.", "受伤事件除强度变化外还发送波形脉冲。"));
            _owner.EnableDamageHookValue = TooltipToggle(new Rect(col3, y, colW, row), _owner.EnableDamageHookValue,
                _owner.T("React to hits", "受伤时有反应"),
                _owner.T("Trigger on damage, impact, fracture, dislocation, shock.", "受伤、撞击、骨折、脱臼、电击时触发。"));
            y += row + 4f;

            _owner.ConditionMixerEnabledValue = TooltipToggle(new Rect(col1, y, colW, row), _owner.ConditionMixerEnabledValue,
                _owner.T("Ongoing body state", "持续身体状态"),
                _owner.T("Sample pain, shock, bleeding, oxygen, infection, temperature, fatigue, mood.", "采样疼痛、休克、出血、缺氧、感染、体温、疲劳、情绪。"));
            _owner.WaveMonitorEnabledValue = TooltipToggle(new Rect(col2, y, colW, row), _owner.WaveMonitorEnabledValue,
                _owner.T("Wave viewer", "波形查看器"),
                _owner.T("Show the draggable window that displays the current output waveform.", "显示可拖拽窗口，用于查看当前实际输出的波形。"));
            _owner.MiniOverlayEnabledValue = TooltipToggle(new Rect(col3, y, colW, row), _owner.MiniOverlayEnabledValue,
                _owner.T("Output monitor", "输出监视器"),
                _owner.T("Show the compact draggable output monitor.", "显示可拖拽的迷你输出监视器。"));
            y += row + 4f;

            _owner.DebugLogValue = TooltipToggle(new Rect(col1, y, colW, row), _owner.DebugLogValue,
                _owner.T("Debug log", "调试日志"),
                _owner.T("Log detailed diagnostics including all incoming messages.", "输出详细诊断日志，包括所有收到的消息。"));
            y += row + 4f;

            GUI.Label(new Rect(col1, y, colW, row), _owner.T("Wake recovery", "苏醒恢复") + ": " + _owner.UnconsciousRecoverySecondsValue + "s");
            if (GUI.Button(new Rect(col2, y, 32f, row), "-")) _owner.UnconsciousRecoverySecondsValue = _owner.UnconsciousRecoverySecondsValue - 1;
            if (GUI.Button(new Rect(col2 + 36f, y, 32f, row), "+")) _owner.UnconsciousRecoverySecondsValue = _owner.UnconsciousRecoverySecondsValue + 1;
            GUI.Label(new Rect(col2 + 74f, y, colW - 74f, row), _owner.T("0-30 seconds", "0-30 秒"));
            y += row + 8f;

            y += DrawLanguageButtons(new Rect(col1, y, innerWidth - spacing * 2f, row));
            y = boxY + settingsBoxHeight + spacing;

            // ── Key Bindings ────────────────────────────────────────────────
            boxY = y;
            var keyHeight = MeasureKeyBindFlowHeight(innerWidth - spacing * 2f, row);
            var keyBoxHeight = Mathf.Max(126f, 36f + keyHeight + spacing);
            GUI.Box(new Rect(0f, boxY, innerWidth, keyBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Key Bindings", "快捷键")));
            y += 36f;

            DrawKeyBindFlow(spacing, y, innerWidth - spacing * 2f, row);
            y = boxY + keyBoxHeight + spacing;
            _scrollContentHeight = y + spacing;

            // Collect tooltip from IMGUI
            _tooltip = GUI.tooltip;

            GUI.EndScrollView();
            GUI.DragWindow();
        }

        private static string Section(string title) => "-- " + title + " --";

        private void DrawPair(float x, float y, float labelWidth, float width, string label, string value)
        {
            GUI.Label(new Rect(x, y, labelWidth, 22f), label + ":");
            DrawWrappedLabel(new Rect(x + labelWidth, y, width - labelWidth, 22f), value);
        }

        private static bool DrawToggleButton(Rect rect, string label, bool active)
        {
            var prev = GUI.color;
            if (active) GUI.color = new Color(0.4f, 0.8f, 0.4f, 1f);
            var clicked = GUI.Button(rect, label);
            GUI.color = prev;
            return clicked && !active;
        }

        private static bool TooltipButton(Rect rect, string label, string tooltip)
        {
            return string.IsNullOrEmpty(tooltip)
                ? GUI.Button(rect, label)
                : GUI.Button(rect, new GUIContent(label, tooltip));
        }

        private static bool TooltipToggle(Rect rect, bool value, string label, string tooltip)
        {
            return GUI.Toggle(rect, value, new GUIContent(label, tooltip));
        }

        private ButtonLayoutItem[] BuildActionItems()
        {
            var connected = _owner.IsBackendConnected;
            if (_owner.ShowQrPanel)
            {
                if (connected)
                {
                    return new[]
                    {
                        ButtonLayoutItem.Action(_owner.T("Disconnect", "断开"), _owner.T("Disconnect the current device connection.", "断开当前设备连接。"), () => _owner.DisconnectFromMenu()),
                        ButtonLayoutItem.Action(_owner.T("Refresh ID / QR", "刷新 ID/二维码"), _owner.T("Generate a new Socket ID and QR code. Scan it again in the DG-Lab app.", "生成新的 Socket ID 和二维码。请在 DG-Lab App 中重新扫码。"), () => _owner.RefreshOfficialSocketIdFromMenu())
                    };
                }

                return new[]
                {
                    ButtonLayoutItem.Action(_owner.T("Refresh ID / QR", "刷新 ID/二维码"), _owner.T("Generate a new Socket ID and QR code. Scan it in the DG-Lab app to connect.", "生成新的 Socket ID 和二维码。请在 DG-Lab App 中扫码连接。"), () => _owner.RefreshOfficialSocketIdFromMenu())
                };
            }

            var connectToggle = connected
                ? ButtonLayoutItem.Action(_owner.T("Disconnect", "断开"), _owner.T("Disconnect the current device connection.", "断开当前设备连接。"), () => _owner.DisconnectFromMenu())
                : ButtonLayoutItem.Action(_owner.T("Connect", "连接"), _owner.T("Connect to the selected backend.", "连接当前选择的后端。"), () => _owner.ConnectFromMenu());

            return new[]
            {
                connectToggle,
                ButtonLayoutItem.Action(_owner.T("Reconnect", "重连"), _owner.T("Reconnect to the selected backend.", "重新连接当前选择的后端。"), () => _owner.ReconnectFromMenu())
            };
        }

        private float MeasureBleScannerHeight(float row)
        {
            return row + 4f + Mathf.Min(4, _owner.BleDeviceOptions.Count) * (row + 2f);
        }

        private void DrawBleScanner(float x, ref float y, float width, float row)
        {
            var buttonWidth = 108f;
            GUI.enabled = !_owner.BleScanInProgress;
            if (GUI.Button(new Rect(x, y, buttonWidth, row), _owner.BleScanInProgress ? _owner.T("Scanning...", "扫描中...") : _owner.T("Scan BLE", "扫描蓝牙")))
            {
                _owner.StartBleScanFromMenu();
            }
            GUI.enabled = true;
            DrawWrappedLabel(new Rect(x + buttonWidth + 8f, y, width - buttonWidth - 8f, row), _owner.BleScanStatusText);
            y += row + 4f;

            var devices = _owner.BleDeviceOptions;
            var rows = Mathf.Min(4, devices.Count);
            for (var i = 0; i < rows; i++)
            {
                var device = devices[i];
                var selected = string.Equals(_owner.BluetoothDeviceNameValue, device.Id, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_owner.BluetoothDeviceNameValue, device.Name, System.StringComparison.OrdinalIgnoreCase);
                if (DrawActionToggleButton(new Rect(x, y, width, row), device.DisplayName, selected)) _owner.SelectBleDevice(device);
                y += row + 2f;
            }
        }

        private float DrawLanguageButtons(Rect rect)
        {
            var label = _owner.T("Language", "语言") + ":";
            var labelWidth = Mathf.Max(54f, GUI.skin.label.CalcSize(new GUIContent(label)).x + 8f);
            GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), label);

            var options = _owner.UiLanguageOptions;
            var selected = _owner.UiLanguageValue;
            var items = new ButtonLayoutItem[options.Count];
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                items[i] = ButtonLayoutItem.Toggle(option.Name, string.Equals(selected, option.Id, System.StringComparison.OrdinalIgnoreCase), () => _owner.UiLanguageValue = option.Id);
            }

            var buttonY = rect.y;
            DrawButtonFlow(rect.x + labelWidth, ref buttonY, rect.width - labelWidth, rect.height, items);
            return Mathf.Max(rect.height, buttonY - rect.y);
        }

        private float MeasureLanguageButtonsHeight(Rect rect)
        {
            var label = _owner.T("Language", "语言") + ":";
            var labelWidth = Mathf.Max(54f, GUI.skin.label.CalcSize(new GUIContent(label)).x + 8f);
            var options = _owner.UiLanguageOptions;
            var items = new ButtonLayoutItem[options.Count];
            for (var i = 0; i < options.Count; i++) items[i] = ButtonLayoutItem.Action(options[i].Name, null, null);
            return Mathf.Max(rect.height, MeasureButtonFlowHeight(rect.width - labelWidth, rect.height, items));
        }

        private void DrawChannelPresetButtons(float spacing, ref float y, float row, float innerWidth)
        {
            GUI.Label(new Rect(spacing, y, 100f, row), _owner.T("Presets", "常用预设"));
            var buttonY = y;
            DrawButtonFlow(spacing + 100f, ref buttonY, innerWidth - spacing * 2f - 100f, row, new[]
            {
                ButtonLayoutItem.Action(_owner.T("A Upper", "A上半身"), null, () => _owner.ChannelABodyPartsValue = "UpperBody"),
                ButtonLayoutItem.Action(_owner.T("B Lower", "B下半身"), null, () => _owner.ChannelBBodyPartsValue = "LowerBody"),
                ButtonLayoutItem.Action(_owner.T("A Arms", "A双臂"), null, () => _owner.ChannelABodyPartsValue = "Arms"),
                ButtonLayoutItem.Action(_owner.T("B Legs", "B双腿"), null, () => _owner.ChannelBBodyPartsValue = "Legs")
            });
            y = buttonY + 8f;
        }

        private void DrawBodyPartPicker(float spacing, ref float y, float row, float innerWidth)
        {
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Click A/B beside each body part:", "点击每个部位旁的 A/B 按钮："));
            y += row + 4f;

            var usableWidth = innerWidth - spacing * 2f;
            var gap = 8f;
            var colW = (usableWidth - gap) * 0.5f;

            y += DrawBodyPartGroup(spacing, y, usableWidth, _owner.T("Core", "躯干"), new[]
            {
                new BodyPartOption("Head", _owner.T("Head", "头")),
                new BodyPartOption("UpTorso", _owner.T("Chest", "胸")),
                new BodyPartOption("DownTorso", _owner.T("Abdomen/Hips", "腹/股部")),
                new BodyPartOption("Torso", _owner.T("Torso", "躯干"))
            }, row, 2) + 8f;

            var leftArmHeight = DrawBodyPartGroup(spacing, y, colW, _owner.T("Left arm", "左臂"), new[]
            {
                new BodyPartOption("LeftArm", _owner.T("Whole arm", "整臂")),
                new BodyPartOption("LeftUpperArm", _owner.T("Upper arm", "上臂")),
                new BodyPartOption("LeftForearm", _owner.T("Forearm", "前臂")),
                new BodyPartOption("LeftHand", _owner.T("Hand", "手"))
            }, row, 2);
            var rightArmHeight = DrawBodyPartGroup(spacing + colW + gap, y, colW, _owner.T("Right arm", "右臂"), new[]
            {
                new BodyPartOption("RightArm", _owner.T("Whole arm", "整臂")),
                new BodyPartOption("RightUpperArm", _owner.T("Upper arm", "上臂")),
                new BodyPartOption("RightForearm", _owner.T("Forearm", "前臂")),
                new BodyPartOption("RightHand", _owner.T("Hand", "手"))
            }, row, 2);
            y += Mathf.Max(leftArmHeight, rightArmHeight) + 8f;

            var leftLegHeight = DrawBodyPartGroup(spacing, y, colW, _owner.T("Left leg", "左腿"), new[]
            {
                new BodyPartOption("LeftLeg", _owner.T("Whole leg", "整腿")),
                new BodyPartOption("LeftThigh", _owner.T("Thigh", "大腿")),
                new BodyPartOption("LeftLowerLeg", _owner.T("Lower leg", "小腿")),
                new BodyPartOption("LeftFoot", _owner.T("Foot", "脚"))
            }, row, 2);
            var rightLegHeight = DrawBodyPartGroup(spacing + colW + gap, y, colW, _owner.T("Right leg", "右腿"), new[]
            {
                new BodyPartOption("RightLeg", _owner.T("Whole leg", "整腿")),
                new BodyPartOption("RightThigh", _owner.T("Thigh", "大腿")),
                new BodyPartOption("RightLowerLeg", _owner.T("Lower leg", "小腿")),
                new BodyPartOption("RightFoot", _owner.T("Foot", "脚"))
            }, row, 2);
            y += Mathf.Max(leftLegHeight, rightLegHeight) + 8f;

            y += DrawBodyPartGroup(spacing, y, usableWidth, _owner.T("Common groups", "常用分组"), new[]
            {
                new BodyPartOption("UpperBody", _owner.T("Upper body", "上半身")),
                new BodyPartOption("LowerBody", _owner.T("Lower body", "下半身")),
                new BodyPartOption("Arms", _owner.T("Both arms", "双臂")),
                new BodyPartOption("Legs", _owner.T("Both legs", "双腿")),
                new BodyPartOption("Hands", _owner.T("Both hands", "双手")),
                new BodyPartOption("Feet", _owner.T("Both feet", "双脚"))
            }, row, 3) + 8f;

            DrawWrappedLabel(new Rect(spacing, y, innerWidth - spacing * 2f, 56f), _owner.T("A/B buttons toggle the part for that channel. Text fields above are kept for advanced manual editing.", "A/B 按钮会把该部位加入或移出对应通道。上方输入框保留给高级手动编辑。"));
            y += 60f;
        }

        private float MeasureConditionConfigHeight(float row, float width)
        {
            var keys = _owner.ConditionKeys;
            var columns = ConditionConfigColumns(width);
            var rows = Mathf.CeilToInt(keys.Count / (float)columns);
            return Mathf.Max(row, rows * 30f);
        }

        private void DrawConditionConfig(float x, ref float y, float width, float row)
        {
            var keys = _owner.ConditionKeys;
            var columns = ConditionConfigColumns(width);
            const float gap = 10f;
            var cellWidth = (width - gap * (columns - 1)) / columns;
            var rows = Mathf.CeilToInt(keys.Count / (float)columns);
            var startY = y;
            for (var i = 0; i < keys.Count; i++)
            {
                var col = i % columns;
                var line = i / columns;
                var rect = new Rect(x + col * (cellWidth + gap), startY + line * 30f, cellWidth, row);
                DrawConditionConfigRow(rect, keys[i]);
            }
            y = startY + rows * 30f;
        }

        private static int ConditionConfigColumns(float width)
        {
            const float minReadableColumnWidth = 320f;
            return Mathf.Max(1, Mathf.FloorToInt((width + 10f) / (minReadableColumnWidth + 10f)));
        }

        private void DrawConditionConfigRow(Rect rect, string key)
        {
            var toggleWidth = 52f;
            var valueWidth = 46f;
            var labelWidth = Mathf.Clamp(rect.width * 0.34f, 112f, 170f);
            GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), _owner.ConditionDisplayName(key));
            var enabled = GUI.Toggle(new Rect(rect.x + labelWidth, rect.y, toggleWidth, rect.height), _owner.GetConditionEnabledValue(key), _owner.T("On", "开"));
            _owner.SetConditionEnabledValue(key, enabled);

            var sliderX = rect.x + labelWidth + toggleWidth + 4f;
            var sliderWidth = rect.width - labelWidth - toggleWidth - valueWidth - 8f;
            GUI.enabled = enabled;
            var scale = _owner.GetConditionScaleValue(key);
            var next = GUI.HorizontalSlider(new Rect(sliderX, rect.y + 5f, sliderWidth, rect.height), scale, 0f, 2f);
            if (Mathf.Abs(next - scale) > 0.001f) _owner.SetConditionScaleValue(key, next);
            GUI.Label(new Rect(sliderX + sliderWidth + 4f, rect.y, valueWidth, rect.height), next.ToString("0.00") + "x");
            GUI.enabled = true;
        }

        private float DrawBodyPartGroup(float x, float y, float width, string title, BodyPartOption[] parts, float row, int columns)
        {
            var rows = Mathf.CeilToInt(parts.Length / (float)columns);
            var headerHeight = 26f;
            var topPadding = 6f;
            var innerGap = 4f;
            var buttonHeight = row + 2f;
            var bottomPadding = 8f;
            var height = topPadding + headerHeight + rows * buttonHeight + Mathf.Max(0, rows - 1) * innerGap + bottomPadding;
            var rect = new Rect(x, y, width, height);
            GUI.Box(rect, "");
            GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 22f), title);
            var gap = 6f;
            var cellWidth = (rect.width - 12f - gap * (columns - 1)) / columns;
            var startY = rect.y + topPadding + headerHeight;
            for (var i = 0; i < parts.Length; i++)
            {
                var col = i % columns;
                var line = i / columns;
                var bx = rect.x + 6f + col * (cellWidth + gap);
                var yy = startY + line * (buttonHeight + innerGap);
                DrawBodyPartToggle(new Rect(bx, yy, cellWidth, buttonHeight), parts[i].Token, parts[i].Label);
            }
            return height;
        }

        private void DrawBodyPartToggle(Rect rect, string token, string label)
        {
            var half = (rect.width - 4f) * 0.5f;
            var activeA = _owner.IsChannelABodyPartSelected(token);
            var activeB = _owner.IsChannelBBodyPartSelected(token);
            if (DrawActionToggleButton(new Rect(rect.x, rect.y, half, rect.height), "A " + label, activeA)) _owner.ToggleChannelABodyPart(token);
            if (DrawActionToggleButton(new Rect(rect.x + half + 4f, rect.y, half, rect.height), "B " + label, activeB)) _owner.ToggleChannelBBodyPart(token);
        }

        private static bool DrawActionToggleButton(Rect rect, string label, bool active)
        {
            var prev = GUI.color;
            if (active) GUI.color = new Color(0.4f, 0.8f, 0.4f, 1f);
            var clicked = GUI.Button(rect, label);
            GUI.color = prev;
            return clicked;
        }

        private sealed class BodyPartOption
        {
            public readonly string Token;
            public readonly string Label;

            public BodyPartOption(string token, string label)
            {
                Token = token;
                Label = label;
            }
        }

        private void DrawKeyBindColumn(Rect rect, string title, string keyLabel, System.Action change, System.Action reset)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 24f), title + ":");
            GUI.Label(new Rect(rect.x, rect.y + 22f, rect.width, 24f), keyLabel);
            var buttonWidth = (rect.width - 6f) * 0.5f;
            if (GUI.Button(new Rect(rect.x, rect.y + 50f, buttonWidth, 22f), _owner.T("Set", "设置"))) change?.Invoke();
            if (GUI.Button(new Rect(rect.x + buttonWidth + 6f, rect.y + 50f, buttonWidth, 22f), new GUIContent(_owner.T("Reset", "重置"), DefaultKeyTooltip(title)))) reset?.Invoke();
        }

        private string DefaultKeyTooltip(string title)
        {
            if (title == _owner.T("Menu", "主菜单")) return _owner.T("Reset to F10", "重置为 F10");
            if (title == _owner.T("Wave Viewer", "波形查看器")) return _owner.T("Reset to Alt+[", "重置为 Alt+[");
            return _owner.T("Reset to Alt+]", "重置为 Alt+]");
        }

        private float DrawKeyBindFlow(float x, float y, float width, float row)
        {
            var startY = y;
            var items = GetKeyBindItems();
            var cursorX = x;
            var cursorY = y;
            const float gap = 8f;
            const float minColumnWidth = 150f;
            const float maxColumnWidth = 220f;
            var columnHeight = row * 3f + 8f;

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var desired = Mathf.Clamp(GUI.skin.label.CalcSize(new GUIContent(item.Title)).x + 40f, minColumnWidth, maxColumnWidth);
                if (cursorX > x && cursorX + desired > x + width)
                {
                    cursorX = x;
                    cursorY += columnHeight + gap;
                }

                var columnWidth = Mathf.Min(desired, x + width - cursorX);
                DrawKeyBindColumn(new Rect(cursorX, cursorY, columnWidth, columnHeight), item.Title, item.KeyLabel, item.Change, item.Reset);
                cursorX += columnWidth + gap;
            }

            return cursorY + columnHeight - startY;
        }

        private float MeasureKeyBindFlowHeight(float width, float row)
        {
            var items = GetKeyBindItems();
            const float gap = 8f;
            const float minColumnWidth = 150f;
            const float maxColumnWidth = 220f;
            var columnHeight = row * 3f + 8f;
            var cursorX = 0f;
            var rows = 1;

            for (var i = 0; i < items.Length; i++)
            {
                var desired = Mathf.Clamp(GUI.skin.label.CalcSize(new GUIContent(items[i].Title)).x + 40f, minColumnWidth, maxColumnWidth);
                if (cursorX > 0f && cursorX + desired > width)
                {
                    cursorX = 0f;
                    rows++;
                }
                cursorX += Mathf.Min(desired, width - cursorX) + gap;
            }

            return rows * columnHeight + (rows - 1) * gap;
        }

        private KeyBindLayoutItem[] GetKeyBindItems()
        {
            return new[]
            {
                new KeyBindLayoutItem(_owner.T("Menu", "主菜单"), _owner.WaitingForMenuKeyBind ? _owner.T("Waiting for key...", "正在等待按键...") : ((_owner.MenuToggleAltRequired ? "Alt+" : "") + _owner.MenuToggleKeyName), _owner.BeginMenuKeyBind, _owner.ResetMenuKeyBind),
                new KeyBindLayoutItem(_owner.T("Wave Viewer", "波形查看器"), _owner.WaitingForWaveMonitorKeyBind ? _owner.T("Waiting for key...", "正在等待按键...") : _owner.WaveMonitorToggleKeyDisplay, _owner.BeginWaveMonitorKeyBind, _owner.ResetWaveMonitorKeyBind),
                new KeyBindLayoutItem(_owner.T("Output Monitor", "输出监视器"), _owner.WaitingForStatusOverlayKeyBind ? _owner.T("Waiting for key...", "正在等待按键...") : _owner.MiniOverlayToggleKeyDisplay, _owner.BeginStatusOverlayKeyBind, _owner.ResetStatusOverlayKeyBind)
            };
        }

        private static void DrawButtonFlow(float x, ref float y, float width, float row, ButtonLayoutItem[] items)
        {
            const float gap = 6f;
            var cursorX = x;
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var buttonWidth = Mathf.Min(CalcButtonWidth(item.Label, 58f, 170f), width);
                if (cursorX > x && cursorX + buttonWidth > x + width)
                {
                    cursorX = x;
                    y += row + gap;
                }

                var rect = new Rect(cursorX, y, Mathf.Min(buttonWidth, x + width - cursorX), row);
                var wasEnabled = GUI.enabled;
                GUI.enabled = wasEnabled && item.Enabled;
                var clicked = item.IsToggle ? DrawToggleButton(rect, item.Label, item.Active) : TooltipButton(rect, item.Label, item.Tooltip);
                GUI.enabled = wasEnabled;
                if (clicked) item.OnClick?.Invoke();
                cursorX += rect.width + gap;
            }

            y += row;
        }

        private static float MeasureButtonFlowHeight(float width, float row, ButtonLayoutItem[] items)
        {
            const float gap = 6f;
            if (items == null || items.Length == 0) return row;
            var cursorX = 0f;
            var rows = 1;
            for (var i = 0; i < items.Length; i++)
            {
                var buttonWidth = Mathf.Min(CalcButtonWidth(items[i].Label, 58f, 170f), width);
                if (cursorX > 0f && cursorX + buttonWidth > width)
                {
                    cursorX = 0f;
                    rows++;
                }
                cursorX += buttonWidth + gap;
            }

            return rows * row + (rows - 1) * gap;
        }

        private static float CalcButtonWidth(string label, float min, float max)
        {
            return Mathf.Clamp(GUI.skin.button.CalcSize(Content(label ?? string.Empty)).x + 18f, min, max);
        }

        private static GUIContent Content(string text)
        {
            SharedContent.text = text ?? string.Empty;
            SharedContent.tooltip = string.Empty;
            SharedContent.image = null;
            return SharedContent;
        }

        private struct ButtonLayoutItem
        {
            public string Label;
            public string Tooltip;
            public System.Action OnClick;
            public bool IsToggle;
            public bool Active;
            public bool Enabled;

            public static ButtonLayoutItem Action(string label, string tooltip, System.Action action, bool enabled = true)
            {
                return new ButtonLayoutItem { Label = label, Tooltip = tooltip, OnClick = action, Enabled = enabled };
            }

            public static ButtonLayoutItem Toggle(string label, bool active, System.Action action, bool enabled = true)
            {
                return new ButtonLayoutItem { Label = label, OnClick = action, IsToggle = true, Active = active, Enabled = enabled };
            }
        }

        private struct KeyBindLayoutItem
        {
            public readonly string Title;
            public readonly string KeyLabel;
            public readonly System.Action Change;
            public readonly System.Action Reset;

            public KeyBindLayoutItem(string title, string keyLabel, System.Action change, System.Action reset)
            {
                Title = title;
                KeyLabel = keyLabel;
                Change = change;
                Reset = reset;
            }
        }

        private int DrawSlider(string label, int value, int min, int max, float x, float y, float labelWidth, float width, string tooltip)
        {
            GUI.Label(new Rect(x, y, labelWidth, 24f), new GUIContent(label, tooltip));
            var next = (int)GUI.HorizontalSlider(new Rect(x + labelWidth, y + 5f, width - labelWidth - 54f, 24f), value, min, max);
            GUI.Label(new Rect(width - 42f, y, 42f, 24f), next.ToString());
            return next;
        }

        private void DrawWrappedLabel(Rect rect, string text)
        {
            if (_wrappedLabelStyle == null)
                _wrappedLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, clipping = TextClipping.Clip };
            GUI.Label(rect, text, _wrappedLabelStyle);
        }

        private float MeasureWrappedLabelHeight(string text, float width, float minHeight)
        {
            if (_wrappedLabelStyle == null)
                _wrappedLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, clipping = TextClipping.Clip };
            return Mathf.Max(minHeight, _wrappedLabelStyle.CalcHeight(new GUIContent(text ?? string.Empty), width));
        }

        private static bool IsAnyMouseButtonOrScrollWheelDown()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || Input.mouseScrollDelta.sqrMagnitude > 0f;
        }

        private static bool IsAnyMouseButtonOrScrollWheel()
        {
            return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.mouseScrollDelta.sqrMagnitude > 0f;
        }
    }
}
