using UnityEngine;

namespace DGLab.BepInEx
{
    internal sealed class DGLabMiniOverlayWindow
    {
        private const int WindowId = 54643322;
        private const float CompactWidth = 320f;
        private const float MinHeight = 148f;
        private const float MaxHeight = 220f;
        private readonly DGLabPlugin _owner;
        private Rect _rect;
        private GUIStyle _wrappedLabelStyle;

        public DGLabMiniOverlayWindow(DGLabPlugin owner)
        {
            _owner = owner;
            _rect = new Rect(Mathf.Max(20f, Screen.width - 340f), 20f, 320f, 148f);
        }

        public void OnGUI()
        {
            _rect.width = CompactWidth;
            _rect.height = Mathf.Clamp(_rect.height, MinHeight, MaxHeight);
            _rect.x = Mathf.Clamp(_rect.x, 0f, Mathf.Max(0f, Screen.width - _rect.width));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, Screen.height - _rect.height));

            GUI.depth = -9999;
            _rect = GUI.Window(WindowId, _rect, Draw, _owner.T("DG-Lab Output Monitor", "DG-Lab 输出监视器"));
        }

        private void Draw(int id)
        {
            if (_wrappedLabelStyle == null)
                _wrappedLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };

            const float x = 10f;
            const float width = 290f;
            const float textBlockGap = 2f;
            var y = 22f;
            var outputText = _owner.T("Last output", "最近输出") + ": " + _owner.LastOutputEventText;
            var conditionText = _owner.T("Conditions", "状态层") + ": " + _owner.ActiveConditionsText;

            GUI.Label(new Rect(x, y, 240f, 20f), _owner.T("Mode", "模式") + ": " + _owner.BackendModeText);
            y += 20f;
            GUI.Label(new Rect(x, y, 240f, 20f), _owner.T("Device", "设备") + ": " + (_owner.DeviceConnected ? _owner.T("Connected", "已连接") : _owner.T("Offline simulation", "离线模拟")));
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), _owner.T("Live strength", "实时强度") + ": A " + _owner.RuntimeStrengthAText + " / B " + _owner.RuntimeStrengthBText);
            y += 20f;
            GUI.Label(new Rect(x, y, width, 20f), _owner.T("Effective limit", "有效上限") + ": A " + _owner.EffectiveLimitAText + " / B " + _owner.EffectiveLimitBText);
            y += 20f;
            var outputHeight = Mathf.Clamp(_wrappedLabelStyle.CalcHeight(new GUIContent(outputText), width), 20f, 88f);
            GUI.Label(new Rect(x, y, width, outputHeight), outputText, _wrappedLabelStyle);
            y += outputHeight + textBlockGap;
            var conditionHeight = Mathf.Clamp(_wrappedLabelStyle.CalcHeight(new GUIContent(conditionText), width), 20f, 120f);
            GUI.Label(new Rect(x, y, width, conditionHeight), conditionText, _wrappedLabelStyle);
            y += conditionHeight + 10f;

            _rect.width = CompactWidth;
            _rect.height = Mathf.Clamp(y, MinHeight, MaxHeight);

            GUI.DragWindow(new Rect(0f, 0f, CompactWidth, 22f));
        }
    }
}
