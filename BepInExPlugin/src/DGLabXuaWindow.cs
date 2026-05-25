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
                size.y += 8f;
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

            // ── Status ──────────────────────────────────────────────────────
            GUI.Box(new Rect(spacing, posY, contentWidth, 180f), "");
            GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("Status", "状态")));
            DrawPair(x, posY + 34f, labelWidth, w, _owner.T("Mode", "模式"), _owner.BackendModeText);

            // Profile toggle buttons
            GUI.Label(new Rect(x, posY + 58f, labelWidth, row), _owner.T("Profile", "类型") + ":");
            var isOfficial = _owner.IsOfficialSocketProfile;
            if (DrawToggleButton(new Rect(x + labelWidth, posY + 58f, 130f, row),
                _owner.T("Official Socket", "官方 Socket"), isOfficial))
                _owner.SwitchExternalBackendProfile("OfficialSocket");
            if (DrawToggleButton(new Rect(x + labelWidth + 136f, posY + 58f, 150f, row),
                _owner.T("Third-Party", "第三方控制器"), !isOfficial))
                _owner.SwitchExternalBackendProfile("ThirdPartyController");

            DrawPair(x, posY + 82f, labelWidth, w, _owner.T("Server", "服务器"), _owner.ExternalBackendUrlText);
            DrawPair(x, posY + 106f, labelWidth, w, _owner.T("Client ID", "终端ID"), _owner.ClientIdText);
            DrawPair(x, posY + 130f, labelWidth, w, _owner.T("Target ID", "设备ID"), _owner.TargetIdText);
            DrawPair(x, posY + 154f, labelWidth, w, _owner.T("QR Socket", "二维码地址"), _owner.QrWebSocketUrlText);
            posY += 190f;

            // ── QR Code ─────────────────────────────────────────────────────
            GUI.Box(new Rect(spacing, posY, contentWidth, 122f), "");
            GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("QR Code", "二维码")));
            GUI.TextArea(new Rect(x, posY + 34f, w, 36f), _owner.QrUrlText);
            GUI.TextArea(new Rect(x, posY + 76f, w, 36f), _owner.QrImageUrlText);
            posY += 132f;

            // ── Scroll area ─────────────────────────────────────────────────
            _scrollPosition = GUI.BeginScrollView(
                new Rect(spacing, posY, WindowWidth - spacing * 2f, WindowHeight - posY - spacing),
                _scrollPosition,
                new Rect(0f, 0f, WindowWidth - spacing * 4f, 960f));

            var innerWidth = WindowWidth - spacing * 4f;
            var y = 0f;

            // ── Actions ─────────────────────────────────────────────────────
            GUI.Box(new Rect(0f, y, innerWidth, 80f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Actions", "操作")));
            y += 36f;

            if (TooltipButton(new Rect(spacing, y, 126f, row), _owner.T("Restart Backend", "重启后端"),
                _owner.T("Reconnect to the backend. Clears cached LAN IP.", "重连后端，同时清除缓存的局域网 IP。")))
                _owner.ReconnectFromMenu();
            if (TooltipButton(new Rect(spacing + 134f, y, 126f, row), _owner.T("Refresh QR", "刷新二维码"),
                _owner.T("Regenerate the QR code image.", "重新生成二维码图片。")))
                _owner.EnsureConnectedForQrFromMenu();
            if (TooltipButton(new Rect(spacing + 268f, y, 126f, row), _owner.T("Open QR File", "打开二维码文件"),
                _owner.T("Open the local QR PNG in the default viewer.", "用默认程序打开本地二维码 PNG。")))
                _owner.OpenQrImageFromMenu();
            if (TooltipButton(new Rect(spacing + 402f, y, 110f, row), _owner.T("Disconnect", "断开"),
                _owner.T("Disconnect the current device connection.", "断开当前设备连接。")))
                _owner.DisconnectFromMenu();
            y += row + spacing;

            // ── Strength Limits ─────────────────────────────────────────────
            GUI.Box(new Rect(0f, y, innerWidth, 132f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Strength Limits", "强度上限")));
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
            y += row + spacing;

            // ── Channel Bindings ─────────────────────────────────────────────
            GUI.Box(new Rect(0f, y, innerWidth, 200f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Channel Bindings", "通道绑定")));
            y += 36f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Channel A", "A通道"));
            _owner.ChannelABodyPartsValue = GUI.TextField(new Rect(spacing + labelWidth, y, innerWidth - labelWidth - spacing * 2f, row), _owner.ChannelABodyPartsValue);
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Channel B", "B通道"));
            _owner.ChannelBBodyPartsValue = GUI.TextField(new Rect(spacing + labelWidth, y, innerWidth - labelWidth - spacing * 2f, row), _owner.ChannelBBodyPartsValue);
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Presets", "常用预设"));
            if (GUI.Button(new Rect(spacing + labelWidth, y, 86f, row), _owner.T("A Upper", "A上半身"))) _owner.ChannelABodyPartsValue = "Head,UpTorso,DownTorso,ArmF,ArmB";
            if (GUI.Button(new Rect(spacing + labelWidth + 92f, y, 86f, row), _owner.T("B Lower", "B下半身"))) _owner.ChannelBBodyPartsValue = "LegF,LegB";
            if (GUI.Button(new Rect(spacing + labelWidth + 184f, y, 86f, row), _owner.T("A Arms", "A双臂"))) _owner.ChannelABodyPartsValue = "ArmF,ArmB";
            if (GUI.Button(new Rect(spacing + labelWidth + 276f, y, 86f, row), _owner.T("B Legs", "B双腿"))) _owner.ChannelBBodyPartsValue = "LegF,LegB";
            y += row + 6f;
            DrawWrappedLabel(new Rect(spacing, y, innerWidth - spacing * 2f, 72f),
                _owner.T("Groups: UpperBody=Head+Chest+ArmF+ArmB, LowerBody=Hips+LegF+LegB. Combine with commas. Precise names (ArmFUpper, LegBLower…) and indices 0-14 also work.",
                          "分组：上半身=头+胸+前臂+后臂，下半身=股+前腿+后腿。逗号组合。也支持精确名称（ArmFUpper、LegBLower…）和 0-14 序号。"));
            y += 78f;

            // ── Settings ─────────────────────────────────────────────────────
            GUI.Box(new Rect(0f, y, innerWidth, 240f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Settings", "设置")));
            y += 36f;

            _owner.EnabledValue = TooltipToggle(new Rect(spacing, y, 200f, row), _owner.EnabledValue,
                _owner.T("Enable BodySync", "启用体感同步"),
                _owner.T("Master switch. Disabling stops all DG-Lab output.", "总开关，关闭后停止所有 DG-Lab 输出。"));
            _owner.EnableWaveEventsValue = TooltipToggle(new Rect(spacing + 210f, y, 200f, row), _owner.EnableWaveEventsValue,
                _owner.T("Event pulses", "事件脉冲"),
                _owner.T("Send waveform pulses on hit events in addition to strength changes.", "受伤事件除强度变化外还发送波形脉冲。"));
            y += row + 4f;

            _owner.EnableDamageHookValue = TooltipToggle(new Rect(spacing, y, 200f, row), _owner.EnableDamageHookValue,
                _owner.T("React to hits", "受伤时有反应"),
                _owner.T("Trigger on damage, impact, fracture, dislocation, shock.", "受伤、撞击、骨折、脱臼、电击时触发。"));
            _owner.ConditionMixerEnabledValue = TooltipToggle(new Rect(spacing + 210f, y, 200f, row), _owner.ConditionMixerEnabledValue,
                _owner.T("Ongoing body state", "持续身体状态"),
                _owner.T("Sample pain, shock, bleeding, oxygen, infection, temperature, fatigue, mood.", "采样疼痛、休克、出血、缺氧、感染、体温、疲劳、情绪。"));
            y += row + 4f;

            _owner.TimeBasedWavesEnabledValue = TooltipToggle(new Rect(spacing, y, 200f, row), _owner.TimeBasedWavesEnabledValue,
                _owner.T("Time-based feel", "按时间切换手感"),
                _owner.T("Switch pulse shape by local clock time.", "按本地时钟切换脉冲形状。"));
            _owner.MiniOverlayEnabledValue = TooltipToggle(new Rect(spacing + 210f, y, 200f, row), _owner.MiniOverlayEnabledValue,
                _owner.T("Status overlay", "状态悬浮窗"),
                _owner.T("Show the compact draggable status window.", "显示可拖拽的迷你状态悬浮窗。"));
            y += row + 4f;

            _owner.DebugLogValue = TooltipToggle(new Rect(spacing, y, 200f, row), _owner.DebugLogValue,
                _owner.T("Debug log", "调试日志"),
                _owner.T("Log detailed diagnostics including all incoming messages.", "输出详细诊断日志，包括所有收到的消息。"));
            y += row + 4f;

            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row),
                _owner.T("Body state: ", "当前身体状态：") + _owner.ActiveConditionsText);
            y += row + 4f;

            // Language
            GUI.Label(new Rect(spacing, y, 80f, row), _owner.T("Language", "语言"));
            if (GUI.Button(new Rect(spacing + 88f, y, 80f, row), "English")) _owner.UiLanguageValue = "English";
            if (GUI.Button(new Rect(spacing + 176f, y, 80f, row), "中文")) _owner.UiLanguageValue = "Chinese";
            y += row + 6f;

            // Menu key binding
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Menu Key", "菜单键"));
            var keyLabel = _owner.WaitingForMenuKeyBind
                ? _owner.T("Press a key…", "按下按键…")
                : (_owner.MenuToggleAltRequired ? "Alt+" : "") + _owner.MenuToggleKeyName;
            GUI.Label(new Rect(spacing + labelWidth, y, 200f, row), keyLabel);
            if (GUI.Button(new Rect(spacing + labelWidth + 208f, y, 80f, row), _owner.T("Change", "修改")))
                _owner.BeginMenuKeyBind();
            if (GUI.Button(new Rect(spacing + labelWidth + 296f, y, 80f, row), _owner.T("Reset", "重置")))
                _owner.ResetMenuKeyBind();
            y += row + 10f;

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
