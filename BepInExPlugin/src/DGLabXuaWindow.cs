using UnityEngine;

namespace DGLab.BepInEx
{
    internal sealed class DGLabXuaWindow
    {
        private const int WindowId = 54643321;
        private const float WindowWidth = 600f;
        private const float WindowHeight = 640f;

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
                var size = _tooltipStyle.CalcSize(new GUIContent(_tooltip));
                size.x = Mathf.Min(size.x + 12f, 480f);
                size.y = _tooltipStyle.CalcHeight(new GUIContent(_tooltip), size.x) + 8f;
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
            var showServerInput = _owner.IsThirdPartyControllerProfile;
            var statusHeight = showServerInput ? 180f : 156f;
            GUI.Box(new Rect(0f, posY, contentWidth, statusHeight), "");
            GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("Status", "状态")));
            DrawPair(x, posY + 34f, labelWidth, w, _owner.T("Mode", "模式"), _owner.BackendModeText);

            // Profile toggle buttons
            GUI.Label(new Rect(x, posY + 58f, labelWidth, row), _owner.T("Profile", "类型") + ":");
            if (DrawToggleButton(new Rect(x + labelWidth, posY + 58f, 130f, row),
                _owner.T("Official Socket", "官方 Socket"), isOfficial))
                _owner.SwitchExternalBackendProfile("OfficialSocket");
            if (DrawToggleButton(new Rect(x + labelWidth + 136f, posY + 58f, 150f, row),
                _owner.T("Third-Party", "第三方控制器"), !isOfficial))
                _owner.SwitchExternalBackendProfile("ThirdPartyController");

            var statusRowY = posY + 82f;
            if (showServerInput)
            {
                GUI.Label(new Rect(x, statusRowY, labelWidth, row), _owner.T("Server", "服务器") + ":");
                _owner.ThirdPartyControllerUrlValue = GUI.TextField(new Rect(x + labelWidth, statusRowY, w - labelWidth, row), _owner.ThirdPartyControllerUrlValue);
                statusRowY += 24f;
            }

            DrawPair(x, statusRowY, labelWidth, w, _owner.T("Client ID", "终端ID"), _owner.ClientIdText);
            statusRowY += 24f;
            DrawPair(x, statusRowY, labelWidth, w, _owner.T("Target ID", "设备ID"), _owner.TargetIdText);
            statusRowY += 24f;
            DrawPair(x, statusRowY, labelWidth, w, _owner.T("QR Socket", "二维码地址"), _owner.QrWebSocketUrlText);
            posY += statusHeight + 10f;

            if (_owner.ShowQrPanel)
            {
                // ── QR Code ─────────────────────────────────────────────────
                var backendConnected = _owner.IsBackendConnected;
                var candidates = _owner.QrAddressCandidates;
                var showQrDetails = backendConnected && _owner.QrPanelExpanded;
                var ipRowHeight = showQrDetails && candidates.Count > 0 ? row + 6f : 0f;
                var qrTex = showQrDetails ? _owner.GetQrTexture() : null;
                var qrHeight = !backendConnected ? 32f : (showQrDetails ? (qrTex != null ? 200f : 50f) : 26f);
                GUI.Box(new Rect(0f, posY, contentWidth, 46f + qrHeight + ipRowHeight), "");
                GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("QR Code", "二维码")));
                GUI.enabled = backendConnected;
                if (GUI.Button(new Rect(x + w - 104f, posY + 7f, 104f, row), showQrDetails ? _owner.T("Collapse", "收起") : _owner.T("Show QR", "显示二维码")))
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
                if (showQrDetails && candidates.Count > 0)
                {
                    var ipY = posY + 34f + qrHeight + 4f;
                    var btnW = Mathf.Min(130f, (w - spacing * (candidates.Count - 1)) / candidates.Count);
                    for (var i = 0; i < candidates.Count; i++)
                    {
                        var ip = candidates[i];
                        var active = _owner.QrWebSocketUrlText.Contains(ip);
                        if (DrawToggleButton(new Rect(x + i * (btnW + spacing), ipY, btnW, row), ip, active))
                            _owner.SelectQrAddress(ip);
                    }
                }
                posY += 56f + qrHeight + ipRowHeight;
            }

            var y = posY;

            // ── Actions ─────────────────────────────────────────────────────
            const float actionsBoxHeight = 84f;
            var boxY = y;
            GUI.Box(new Rect(0f, boxY, innerWidth, actionsBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Actions", "操作")));
            y += 36f;

            if (TooltipButton(new Rect(spacing, y, 140f, row), _owner.T("Restart Backend", "重启后端"),
                _owner.T("Reconnect to the backend. Clears cached LAN IP.", "重连后端，同时清除缓存的局域网 IP。")))
                _owner.ReconnectFromMenu();
            GUI.enabled = _owner.IsBackendConnected;
            if (_owner.ShowQrPanel && TooltipButton(new Rect(spacing + 148f, y, 140f, row), _owner.T("Refresh QR", "刷新二维码"),
                    _owner.T("Regenerate the QR code image.", "重新生成二维码图片。")))
                    _owner.EnsureConnectedForQrFromMenu();
            GUI.enabled = true;
            if (TooltipButton(new Rect(_owner.ShowQrPanel ? spacing + 296f : spacing + 148f, y, 110f, row), _owner.T("Disconnect", "断开"),
                _owner.T("Disconnect the current device connection.", "断开当前设备连接。")))
                _owner.DisconnectFromMenu();
            y = boxY + actionsBoxHeight + spacing;

            // ── Strength Limits ─────────────────────────────────────────────
            const float strengthBoxHeight = 146f;
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
            if (TooltipButton(new Rect(spacing, y, 80f, row), _owner.T("Set A", "应用A"),
                _owner.T("Send the current Max A value to the device immediately.", "立即将当前 A 上限值发送到设备。")))
                _owner.ApplyStrengthAFromMenu();
            if (TooltipButton(new Rect(spacing + 88f, y, 80f, row), _owner.T("Set B", "应用B"),
                _owner.T("Send the current Max B value to the device immediately.", "立即将当前 B 上限值发送到设备。")))
                _owner.ApplyStrengthBFromMenu();
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

            // ── Settings ─────────────────────────────────────────────────────
            const float settingsBoxHeight = 178f;
            boxY = y;
            GUI.Box(new Rect(0f, boxY, innerWidth, settingsBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Settings", "设置")));
            y += 36f;

            var colGap = 8f;
            var colW = (innerWidth - spacing * 2f - colGap * 2f) / 3f;
            var col1 = spacing;
            var col2 = col1 + colW + colGap;
            var col3 = col2 + colW + colGap;

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
            y += row + 8f;

            GUI.Label(new Rect(col1, y, 72f, row), _owner.T("Language", "语言") + ":");
            if (DrawToggleButton(new Rect(col1 + 54f, y, 90f, row), "English", _owner.UiLanguageValue == "English")) _owner.UiLanguageValue = "English";
            if (DrawToggleButton(new Rect(col1 + 150f, y, 72f, row), "中文", _owner.UiLanguageValue == "Chinese")) _owner.UiLanguageValue = "Chinese";
            y = Mathf.Max(y + row, boxY + settingsBoxHeight) + spacing;

            // ── Key Bindings ────────────────────────────────────────────────
            const float keyBoxHeight = 126f;
            boxY = y;
            GUI.Box(new Rect(0f, boxY, innerWidth, keyBoxHeight), "");
            GUI.Label(new Rect(spacing, boxY + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Key Bindings", "快捷键")));
            y += 36f;

            DrawKeyBindColumn(new Rect(col1, y, colW, row * 3f + 4f), _owner.T("Menu", "主菜单"), _owner.WaitingForMenuKeyBind ? _owner.T("Waiting for key...", "正在等待按键...") : ((_owner.MenuToggleAltRequired ? "Alt+" : "") + _owner.MenuToggleKeyName), _owner.BeginMenuKeyBind, _owner.ResetMenuKeyBind);
            DrawKeyBindColumn(new Rect(col2, y, colW, row * 3f + 4f), _owner.T("Wave Viewer", "波形查看器"), _owner.WaitingForWaveMonitorKeyBind ? _owner.T("Waiting for key...", "正在等待按键...") : _owner.WaveMonitorToggleKeyDisplay, _owner.BeginWaveMonitorKeyBind, _owner.ResetWaveMonitorKeyBind);
            DrawKeyBindColumn(new Rect(col3, y, colW, row * 3f + 4f), _owner.T("Output Monitor", "输出监视器"), _owner.WaitingForStatusOverlayKeyBind ? _owner.T("Waiting for key...", "正在等待按键...") : _owner.MiniOverlayToggleKeyDisplay, _owner.BeginStatusOverlayKeyBind, _owner.ResetStatusOverlayKeyBind);
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
            return GUI.Button(rect, new GUIContent(label, tooltip));
        }

        private static bool TooltipToggle(Rect rect, bool value, string label, string tooltip)
        {
            return GUI.Toggle(rect, value, new GUIContent(label, tooltip));
        }

        private void DrawChannelPresetButtons(float spacing, ref float y, float row, float innerWidth)
        {
            GUI.Label(new Rect(spacing, y, 100f, row), _owner.T("Presets", "常用预设"));
            var x = spacing + 100f;
            if (GUI.Button(new Rect(x, y, 86f, row), _owner.T("A Upper", "A上半身"))) _owner.ChannelABodyPartsValue = "UpperBody";
            if (GUI.Button(new Rect(x + 92f, y, 86f, row), _owner.T("B Lower", "B下半身"))) _owner.ChannelBBodyPartsValue = "LowerBody";
            if (GUI.Button(new Rect(x + 184f, y, 86f, row), _owner.T("A Arms", "A双臂"))) _owner.ChannelABodyPartsValue = "Arms";
            if (GUI.Button(new Rect(x + 276f, y, 86f, row), _owner.T("B Legs", "B双腿"))) _owner.ChannelBBodyPartsValue = "Legs";
            y += row + 8f;
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
