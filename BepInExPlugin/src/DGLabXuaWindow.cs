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
            GUI.Box(new Rect(spacing, posY, contentWidth, 156f), "");
            var x = spacing * 2f;
            var w = WindowWidth - spacing * 4f;
            GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("Status", "状态")));
            DrawPair(x, posY + 34f, labelWidth, w, _owner.T("Mode", "模式"), _owner.BackendModeText);
            DrawPair(x, posY + 58f, labelWidth, w, _owner.T("Server", "服务器"), _owner.ServerUrl);
            DrawPair(x, posY + 82f, labelWidth, w, _owner.T("Client ID", "终端ID"), _owner.ClientIdText);
            DrawPair(x, posY + 106f, labelWidth, w, _owner.T("Target ID", "设备ID"), _owner.TargetIdText);
            DrawPair(x, posY + 130f, labelWidth, w, _owner.T("QR Socket", "二维码地址"), _owner.QrWebSocketUrlText);
            posY += 166f;

            GUI.Box(new Rect(spacing, posY, contentWidth, 122f), "");
            GUI.Label(new Rect(x, posY + 8f, w, row), Section(_owner.T("QR Code", "二维码")));
            GUI.TextArea(new Rect(x, posY + 34f, w, 36f), _owner.QrUrlText);
            GUI.TextArea(new Rect(x, posY + 76f, w, 36f), _owner.QrImageUrlText);
            posY += 132f;

            _scrollPosition = GUI.BeginScrollView(
                new Rect(spacing, posY, WindowWidth - spacing * 2f, WindowHeight - posY - spacing),
                _scrollPosition,
                new Rect(0f, 0f, WindowWidth - spacing * 4f, 940f));

            var innerWidth = WindowWidth - spacing * 4f;
            var y = 0f;

            GUI.Box(new Rect(0f, y, innerWidth, 116f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Actions", "操作")));
            y += 36f;

            if (GUI.Button(new Rect(spacing, y, 126f, row), _owner.T("Restart Backend", "重启后端"))) _owner.ReconnectFromMenu();
            if (GUI.Button(new Rect(spacing + 134f, y, 126f, row), _owner.T("Refresh QR", "刷新二维码"))) _owner.EnsureConnectedForQrFromMenu();
            if (GUI.Button(new Rect(spacing + 268f, y, 126f, row), _owner.T("Open QR File", "打开二维码文件"))) _owner.OpenQrImageFromMenu();
            if (GUI.Button(new Rect(spacing + 402f, y, 110f, row), _owner.T("Disconnect", "断开"))) _owner.DisconnectFromMenu();
            y += row + spacing;

            if (GUI.Button(new Rect(spacing, y, 126f, row), _owner.T("Set A", "设置A"))) _owner.ApplyStrengthAFromMenu();
            if (GUI.Button(new Rect(spacing + 134f, y, 126f, row), _owner.T("Set B", "设置B"))) _owner.ApplyStrengthBFromMenu();
            y += row + spacing;

            GUI.Box(new Rect(0f, y, innerWidth, 132f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Strength Limits", "强度上限")));
            y += 36f;
            _owner.StrengthAValue = DrawSlider(_owner.T("Max Strength A", "A通道上限"), _owner.StrengthAValue, 0, 200, spacing, y, labelWidth, innerWidth);
            y += 36f;
            _owner.StrengthBValue = DrawSlider(_owner.T("Max Strength B", "B通道上限"), _owner.StrengthBValue, 0, 200, spacing, y, labelWidth, innerWidth);
            y += 46f;

            GUI.Box(new Rect(0f, y, innerWidth, 264f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Channel Bindings", "通道绑定")));
            y += 36f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Channel A", "A通道"));
            _owner.ChannelABodyPartsValue = GUI.TextField(new Rect(spacing + labelWidth, y, innerWidth - labelWidth - spacing * 2f, row), _owner.ChannelABodyPartsValue);
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Channel B", "B通道"));
            _owner.ChannelBBodyPartsValue = GUI.TextField(new Rect(spacing + labelWidth, y, innerWidth - labelWidth - spacing * 2f, row), _owner.ChannelBBodyPartsValue);
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, labelWidth, row), _owner.T("Presets", "常用预设"));
            if (GUI.Button(new Rect(spacing + labelWidth, y, 86f, row), _owner.T("A Upper", "A上半身"))) _owner.ChannelABodyPartsValue = _owner.T("UpperBody", "上半身");
            if (GUI.Button(new Rect(spacing + labelWidth + 92f, y, 86f, row), _owner.T("B Lower", "B下半身"))) _owner.ChannelBBodyPartsValue = _owner.T("LowerBody", "下半身");
            if (GUI.Button(new Rect(spacing + labelWidth + 184f, y, 86f, row), _owner.T("A Arms", "A双臂"))) _owner.ChannelABodyPartsValue = _owner.T("Arms,Hands", "双臂,双手");
            if (GUI.Button(new Rect(spacing + labelWidth + 276f, y, 86f, row), _owner.T("B Legs", "B双腿"))) _owner.ChannelBBodyPartsValue = _owner.T("Legs,Feet", "双腿,双脚");
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("UpperBody = Head + Chest + both arms + both hands.", "上半身 = 头 + 胸 + 双臂 + 双手。"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("LowerBody = Hips/abdomen + both legs + both feet.", "下半身 = 股/腹部 + 双腿 + 双脚。"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Single parts also work: Head, Chest, Hips, Arms, Hands, Legs, Feet.", "也可以单独绑定：头、胸、股、双臂、双手、双腿、双脚。"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Use commas to combine parts. Example: Head,Chest,Hands", "多个部位用逗号组合。例如：头,胸,双手"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Advanced: ArmF/ArmB and LegF/LegB are game front/back limb groups, not guaranteed real left/right.", "进阶：ArmF/ArmB、LegF/LegB 是游戏前/后侧肢体组，不保证等于现实左/右。"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Very precise names and 0-14 indices are supported, but the simple names above are recommended.", "支持更精确的名称和 0-14 序号，但建议优先用上面的简单名称。"));
            y += row + spacing;

            GUI.Box(new Rect(0f, y, innerWidth, 330f), "");
            GUI.Label(new Rect(spacing, y + 8f, innerWidth - spacing * 2f, row), Section(_owner.T("Settings", "设置")));
            y += 36f;
            _owner.EnabledValue = GUI.Toggle(new Rect(spacing, y, 170f, row), _owner.EnabledValue, _owner.T("Enable BodySync", "启用体感同步"));
            _owner.EnableDamageHookValue = GUI.Toggle(new Rect(spacing + 180f, y, 180f, row), _owner.EnableDamageHookValue, _owner.T("React to hits", "受伤时有反应"));
            _owner.EnableWaveEventsValue = GUI.Toggle(new Rect(spacing + 370f, y, 170f, row), _owner.EnableWaveEventsValue, _owner.T("Event pulses", "事件脉冲"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Hits means damage, impact, broken bones, dislocation, shock, and similar sudden events.", "受伤反应包括伤害、撞击、骨折、脱臼、电击等突然事件。"));
            y += row;
            _owner.ConditionMixerEnabledValue = GUI.Toggle(new Rect(spacing, y, 220f, row), _owner.ConditionMixerEnabledValue, _owner.T("Ongoing body state", "持续身体状态"));
            _owner.MiniOverlayEnabledValue = GUI.Toggle(new Rect(spacing + 230f, y, 170f, row), _owner.MiniOverlayEnabledValue, _owner.T("Status overlay", "状态悬浮窗"));
            y += row + 4f;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Ongoing state follows pain, shock, bleeding, oxygen, infection, temperature, fatigue, and mood.", "持续状态会跟随疼痛、休克、出血、缺氧、感染、体温、疲劳和情绪。"));
            y += row;
            _owner.TimeBasedWavesEnabledValue = GUI.Toggle(new Rect(spacing, y, 220f, row), _owner.TimeBasedWavesEnabledValue, _owner.T("Time-based feel", "按时间切换手感"));
            _owner.DebugLogValue = GUI.Toggle(new Rect(spacing + 230f, y, 170f, row), _owner.DebugLogValue, _owner.T("Debug log", "调试日志"));
            y += row + 4f;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Time-based feel only changes the selected pulse shape by clock time; strength still follows the game.", "按时间切换手感只按时钟换脉冲形状；强度仍然跟随游戏状态。"));
            y += row;
            GUI.Label(new Rect(spacing, y, innerWidth - spacing * 2f, row), _owner.T("Current body state: ", "当前身体状态：") + _owner.ActiveConditionsText);
            y += row + 4f;
            GUI.Label(new Rect(spacing, y, 80f, row), _owner.T("Language", "语言"));
            if (GUI.Button(new Rect(spacing + 88f, y, 100f, row), "English")) _owner.UiLanguageValue = "English";
            if (GUI.Button(new Rect(spacing + 196f, y, 100f, row), "中文")) _owner.UiLanguageValue = "Chinese";
            GUI.Label(new Rect(spacing + 310f, y, 180f, row), _owner.T("Current: English", "当前：中文"));
            y += row + 6f;
            GUI.Label(new Rect(spacing, y, 80f, row), _owner.T("Menu Key", "菜单键"));
            GUI.Label(new Rect(spacing + 88f, y, 100f, row), _owner.WaitingForMenuKeyBind ? _owner.T("Press a key", "按下按键") : _owner.MenuToggleKeyName);
            if (GUI.Button(new Rect(spacing + 196f, y, 100f, row), _owner.T("Change", "修改"))) _owner.BeginMenuKeyBind();
            if (GUI.Button(new Rect(spacing + 304f, y, 100f, row), _owner.T("Reset", "重置"))) _owner.ResetMenuKeyBind();
            y += row + 6f;
            DrawWrappedLabel(new Rect(spacing, y, innerWidth - spacing * 2f, 48f), _owner.T("Default/emergency toggle is F10. You can bind a single keyboard key here; modifier combinations are not used. WebSocket errors do not close this UI.", "默认/应急菜单键为 F10。可在这里绑定一个单独键盘按键；不使用组合键。WebSocket 错误不会关闭菜单。"));
            y += 54f;
            if (GUI.Button(new Rect(spacing, y, 100f, row), "Close"))
            {
                IsShown = false;
                _owner.SetMenuOpenFromWindow(false);
            }

            GUI.EndScrollView();
            GUI.DragWindow();
        }

        private static string Section(string title)
        {
            return "-- " + title + " --";
        }

        private static void DrawPair(float x, float y, float labelWidth, float width, string label, string value)
        {
            GUI.Label(new Rect(x, y, labelWidth, 22f), label + ":");
            GUI.Label(new Rect(x + labelWidth, y, width - labelWidth, 22f), value);
        }

        private static int DrawSlider(string label, int value, int min, int max, float x, float y, float labelWidth, float width)
        {
            GUI.Label(new Rect(x, y, labelWidth, 24f), label);
            var next = (int)GUI.HorizontalSlider(new Rect(x + labelWidth, y + 5f, width - labelWidth - 54f, 24f), value, min, max);
            GUI.Label(new Rect(width - 42f, y, 42f, 24f), next.ToString());
            return next;
        }

        private void DrawWrappedLabel(Rect rect, string text)
        {
            if (_wrappedLabelStyle == null)
            {
                _wrappedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    clipping = TextClipping.Clip
                };
            }

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
