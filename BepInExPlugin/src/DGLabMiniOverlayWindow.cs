using UnityEngine;

namespace DGLab.BepInEx
{
    internal sealed class DGLabMiniOverlayWindow
    {
        private const int WindowId = 54643322;
        private readonly DGLabPlugin _owner;
        private Rect _rect;

        public DGLabMiniOverlayWindow(DGLabPlugin owner)
        {
            _owner = owner;
            _rect = new Rect(Mathf.Max(20f, Screen.width - 340f), 20f, 320f, 148f);
        }

        public void OnGUI()
        {
            GUI.depth = -9999;
            _rect = GUI.Window(WindowId, _rect, Draw, _owner.T("EXP BodySync", "EXP 体感同步"));
        }

        private void Draw(int id)
        {
            const float x = 10f;
            const float width = 290f;
            var y = 22f;
            var outputText = _owner.T("Last output", "最近输出") + ": " + _owner.LastOutputEventText;
            var conditionText = _owner.T("Conditions", "状态层") + ": " + _owner.ActiveConditionsText;
            var outputStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var conditionStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

            GUI.Label(new Rect(x, y, 240f, 20f), _owner.T("Mode", "模式") + ": " + _owner.BackendModeText);
            y += 20f;
            GUI.Label(new Rect(x, y, 240f, 20f), _owner.T("Device", "设备") + ": " + (_owner.DeviceConnected ? _owner.T("Connected", "已连接") : _owner.T("Offline simulation", "离线模拟")));
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), _owner.T("Live strength", "实时强度") + ": A " + _owner.RuntimeStrengthAText + " / B " + _owner.RuntimeStrengthBText);
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), _owner.T("Limit", "上限") + ": A " + _owner.MaxStrengthAText + " / B " + _owner.MaxStrengthBText);
            y += 20f;
            var outputHeight = Mathf.Clamp(outputStyle.CalcHeight(new GUIContent(outputText), width), 20f, 88f);
            GUI.Label(new Rect(x, y, width, outputHeight), outputText, outputStyle);
            y += outputHeight + 4f;
            var conditionHeight = Mathf.Clamp(conditionStyle.CalcHeight(new GUIContent(conditionText), width), 20f, 120f);
            GUI.Label(new Rect(x, y, width, conditionHeight), conditionText, conditionStyle);
            y += conditionHeight + 10f;

            _rect.height = Mathf.Clamp(y, 148f, 260f);

            GUI.DragWindow();
        }
    }
}
