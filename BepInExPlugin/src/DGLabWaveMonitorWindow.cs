using System;
using System.Collections.Generic;
using UnityEngine;

namespace DGLab.BepInEx
{
    internal sealed class DGLabWaveMonitorWindow
    {
        private const int WindowId = 54643323;
        private const float WindowWidth = 760f;
        private const float WindowHeight = 500f;
        private const float PreviewMaxByte = 0x96;
        private const int MaxGraphFrames = 120;
        private const float LiveGraphRefreshSeconds = 0.2f;

        private readonly DGLabPlugin _owner;
        private Rect _windowRect = new Rect(640f, 20f, WindowWidth, WindowHeight);
        private Vector2 _scrollPosition;
        private bool _isMouseDownOnWindow;
        private GUIStyle _wrappedLabelStyle;
        private GUIStyle _smallLabelStyle;
        private GUIStyle _centerSmallLabelStyle;
        private GUIStyle _rightSmallLabelStyle;
        private int _libraryIndex;
        private bool _showLibrary;
        private readonly Dictionary<string, WaveGraphCache> _liveGraphCache = new Dictionary<string, WaveGraphCache>();
        private readonly Dictionary<string, float> _lastLiveGraphRefreshTime = new Dictionary<string, float>();
        private readonly Dictionary<string, WaveGraphCache> _libraryGraphCache = new Dictionary<string, WaveGraphCache>();

        public bool IsShown { get; set; }

        public DGLabWaveMonitorWindow(DGLabPlugin owner)
        {
            _owner = owner;
        }

        public void OnGUI()
        {
            if (!IsShown) return;

            if (Mathf.Abs(_windowRect.width - WindowWidth) > 0.1f || Mathf.Abs(_windowRect.height - WindowHeight) > 0.1f)
            {
                _windowRect.width = WindowWidth;
                _windowRect.height = WindowHeight;
                _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, Mathf.Max(0f, Screen.width - _windowRect.width));
                _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, Mathf.Max(0f, Screen.height - _windowRect.height));
            }

            GUI.depth = -9999;
            GUI.Box(_windowRect, GUIContent.none);
            _windowRect = GUI.Window(WindowId, _windowRect, CreateWindowUI, _owner.T("DG-Lab Wave Monitor", "DG-Lab 波形监视器"));

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
            var windowWidth = _windowRect.width;
            var windowHeight = _windowRect.height;
            var entries = _showLibrary ? WaveLibraryEntries() : null;
            var contentWidth = windowWidth - spacing * 2f;
            var scrollContentWidth = contentWidth - 18f;
            var libraryButtonAreaWidth = scrollContentWidth - spacing * 2f;
            var libraryButtonRows = entries != null ? CountLibraryButtonRows(libraryButtonAreaWidth, entries) : 0;
            var libraryBoxHeight = _showLibrary ? Mathf.Max(220f, 52f + libraryButtonRows * (row + 6f)) : 0f;
            const float libraryBottomPadding = 88f;
            var viewport = new Rect(spacing, 28f, contentWidth, windowHeight - 38f);
            var contentHeight = _showLibrary ? 520f + libraryBoxHeight + libraryBottomPadding : 454f;
            var y = 0f;

            if (GUI.Button(new Rect(windowWidth - 24f, 3f, 20f, 18f), "X"))
            {
                IsShown = false;
                _owner.SetWaveMonitorOpen(false);
            }

            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, new Rect(0f, 0f, contentWidth - 18f, contentHeight));
            contentWidth -= 18f;

            GUI.Box(new Rect(0f, y, contentWidth, 88f), "");
            GUI.Label(new Rect(spacing, y + 8f, contentWidth - spacing * 2f, row), Section(_owner.T("Last Output", "最近输出")));
            DrawWrappedLabel(new Rect(spacing, y + 32f, contentWidth - spacing * 2f, 48f),
                _owner.T("State: ", "状态：") + _owner.LastOutputEventText + "   " + _owner.T("Pattern: ", "波形：") + _owner.LastWaveProfileText + "   " + _owner.LastWaveDurationSeconds + "s");
            y += 98f;

            GUI.Box(new Rect(0f, y, contentWidth, 246f), "");
            GUI.Label(new Rect(spacing, y + 8f, contentWidth - spacing * 2f, row), Section(_owner.T("Live Wave", "实时波形")));
            var liveGraphWidth = contentWidth - spacing * 2f;
            DrawChannelLiveWave(new Rect(spacing, y + 36f, liveGraphWidth, 92f), _owner.T("Output A", "输出 A"), _owner.IsOutputChannelEnabled(1), _owner.LastWaveFramesA, "live-a");
            DrawChannelLiveWave(new Rect(spacing, y + 138f, liveGraphWidth, 92f), _owner.T("Output B", "输出 B"), _owner.IsOutputChannelEnabled(2), _owner.LastWaveFramesB, "live-b");
            y += 256f;

            _showLibrary = GUI.Toggle(new Rect(0f, y, 220f, row), _showLibrary, _owner.T("Show library viewer", "显示波形库查看"));
            GUI.Label(new Rect(contentWidth - 220f, y, 210f, row), _owner.T("Shortcut: ", "快捷键：") + _owner.WaveMonitorToggleKeyDisplay);
            y += row + 6f;

            if (_showLibrary)
            {
                if (entries == null) entries = WaveLibraryEntries();
                _libraryIndex = Mathf.Clamp(_libraryIndex, 0, entries.Length - 1);
                var selected = entries[_libraryIndex];

                var graphWidth = contentWidth - spacing * 2f;
                GUI.Box(new Rect(0f, y, contentWidth, 190f), "");
                GUI.Label(new Rect(spacing, y + 8f, contentWidth - spacing * 2f, row), Section(selected.Label + _owner.T(" pattern preview", " 节奏预览")));
                DrawWaveGraph(new Rect(spacing, y + 36f, graphWidth, 138f), selected.Wave, false, selected.Label);
                y += 200f;

                GUI.Box(new Rect(0f, y, contentWidth, libraryBoxHeight), "");
                GUI.Label(new Rect(spacing, y + 8f, contentWidth - spacing * 2f - 132f, row), Section(_owner.T("Wave Library", "波形库")));
                if (GUI.Button(new Rect(contentWidth - 132f, y + 8f, 60f, row), _owner.T("Prev", "上一个")))
                    _libraryIndex = (_libraryIndex + entries.Length - 1) % entries.Length;
                if (GUI.Button(new Rect(contentWidth - 66f, y + 8f, 60f, row), _owner.T("Next", "下一个")))
                    _libraryIndex = (_libraryIndex + 1) % entries.Length;
                y += 36f;

                DrawLibraryButtons(new Rect(spacing, y, contentWidth - spacing * 2f, libraryBoxHeight - 42f), entries, row);
            }
            else
            {
                DrawWrappedLabel(new Rect(0f, y, contentWidth, 52f),
                    _owner.T("The chart previews this pattern as one feel-strength curve. Body bindings decide whether the whole pattern is sent to output A, B, or both.",
                             "图表把这个波形显示为一条体感强弱曲线。身体部位绑定只决定整个波形发到设备 A、B 或 A+B。"));
            }

            GUI.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, windowWidth - 28f, 24f));
        }

        private static string Section(string title) => "-- " + title + " --";

        private void DrawChannelLiveWave(Rect rect, string title, bool channelEnabled, string[] frames, string cacheKey)
        {
            var labelWidth = 58f;
            GUI.Label(new Rect(rect.x, rect.y + 2f, labelWidth, 20f), title, _smallLabelStyle ?? GUI.skin.label);
            var graph = new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height);
            if (!channelEnabled)
            {
                GUI.Box(graph, "");
                GUI.Label(new Rect(graph.x + 8f, graph.y + 26f, graph.width - 16f, 20f), _owner.T("This channel is disabled by the configured or phone limit.", "该通道已被配置上限或手机端上限禁用。"));
                return;
            }
            if (frames == null || frames.Length == 0)
            {
                GUI.Box(graph, "");
                GUI.Label(new Rect(graph.x + 8f, graph.y + 26f, graph.width - 16f, 20f), _owner.T("No active wave data for this channel.", "该通道当前没有正在输出的波形。"));
                return;
            }

            DrawWaveGraph(graph, frames, true, cacheKey);
        }

        private void DrawWaveGraph(Rect rect, string[] wave, bool throttleRefresh, string cacheKey)
        {
            GUI.Box(rect, "");
            if (wave == null || wave.Length == 0) return;
            var cache = GetWaveGraphCache(wave, throttleRefresh, cacheKey);
            if (cache == null || cache.FeelSamples.Count == 0) return;

            if (_smallLabelStyle == null) _smallLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            if (_centerSmallLabelStyle == null) _centerSmallLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };

            if (_rightSmallLabelStyle == null) _rightSmallLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleRight };

            var graph = new Rect(rect.x + 36f, rect.y + 10f, rect.width - 48f, rect.height - 34f);
            if (Event.current.type != EventType.Repaint)
            {
                DrawLegend(graph, rect.yMax - 18f);
                return;
            }

            GUI.BeginGroup(graph);
            var linePadding = 6f;
            var localGraph = new Rect(linePadding, linePadding, graph.width - linePadding * 2f, graph.height - linePadding * 2f);
            DrawGrid(localGraph);
            DrawLineGraph(localGraph, cache.FeelSamples, new Color(0.25f, 0.65f, 1f, 1f), 1.25f);
            GUI.EndGroup();
            GUI.Label(new Rect(rect.x + 4f, graph.y - 8f, 30f, 16f), _owner.T("High", "强"), _smallLabelStyle);
            GUI.Label(new Rect(rect.x + 4f, graph.y + graph.height * 0.5f - 8f, 30f, 16f), _owner.T("Mid", "中"), _smallLabelStyle);
            GUI.Label(new Rect(rect.x + 4f, graph.yMax - 8f, 30f, 16f), _owner.T("Low", "弱"), _smallLabelStyle);
            DrawLegend(graph, rect.yMax - 18f);
        }

        private WaveGraphCache GetWaveGraphCache(string[] wave, bool throttleRefresh, string cacheKey)
        {
            wave = LimitGraphFrames(wave);
            var signature = WaveSignature(wave);
            if (!throttleRefresh)
            {
                WaveGraphCache cached;
                if (_libraryGraphCache.TryGetValue(cacheKey, out cached) && cached.Signature == signature) return cached;
                cached = BuildWaveGraphCache(wave, signature);
                _libraryGraphCache[cacheKey] = cached;
                return cached;
            }

            var now = Time.realtimeSinceStartup;
            WaveGraphCache liveCache;
            float lastRefresh;
            _liveGraphCache.TryGetValue(cacheKey, out liveCache);
            _lastLiveGraphRefreshTime.TryGetValue(cacheKey, out lastRefresh);
            if (liveCache != null && now - lastRefresh < LiveGraphRefreshSeconds) return liveCache;
            if (liveCache != null && liveCache.Signature == signature) return liveCache;

            liveCache = BuildWaveGraphCache(wave, signature);
            _liveGraphCache[cacheKey] = liveCache;
            _lastLiveGraphRefreshTime[cacheKey] = now;
            return liveCache;
        }

        private static WaveGraphCache BuildWaveGraphCache(string[] wave, string signature)
        {
            var samplesA = new List<float>(wave.Length * 4);
            var samplesB = new List<float>(wave.Length * 4);
            for (var i = 0; i < wave.Length; i++) DecodeWaveFrameSamples(wave[i], samplesA, samplesB);
            var feelSamples = new List<float>(Mathf.Max(samplesA.Count, samplesB.Count));
            var count = Mathf.Max(samplesA.Count, samplesB.Count);
            for (var i = 0; i < count; i++)
            {
                var a = i < samplesA.Count ? samplesA[i] : 0f;
                var b = i < samplesB.Count ? samplesB[i] : 0f;
                feelSamples.Add(Mathf.Max(a, b));
            }
            return new WaveGraphCache(signature, feelSamples);
        }

        private static string WaveSignature(string[] wave)
        {
            if (wave == null || wave.Length == 0) return string.Empty;
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < wave.Length; i++) hash = hash * 31 + (wave[i] != null ? wave[i].GetHashCode() : 0);
                return wave.Length + ":" + hash;
            }
        }

        private void DrawLegend(Rect graph, float y)
        {
            GUI.Label(new Rect(graph.x, y, graph.width - 92f, 16f), _owner.T("Feel-strength curve", "体感强弱曲线"), _centerSmallLabelStyle);
            GUI.Label(new Rect(graph.xMax - 86f, y, 58f, 16f), _owner.T("Output", "输出"), _rightSmallLabelStyle);
            DrawSolidRect(new Rect(graph.xMax - 24f, y + 7f, 18f, 3f), new Color(0.25f, 0.65f, 1f, 1f));
        }

        private static void DrawLineGraph(Rect graph, List<float> samples, Color lineColor, float lineWidth)
        {
            var prev = Vector2.zero;
            for (var i = 0; i < samples.Count; i++)
            {
                var x = graph.x + (samples.Count == 1 ? graph.width * 0.5f : i * graph.width / (samples.Count - 1));
                var y = graph.yMax - samples[i] * graph.height;
                var point = new Vector2(x, y);
                if (i > 0) DrawSegmentedLine(graph, prev, point, lineColor, lineWidth);
                prev = point;
            }
        }

        private static void DrawSegmentedLine(Rect clip, Vector2 from, Vector2 to, Color color, float width)
        {
            var half = width * 0.5f;
            clip = new Rect(clip.x + half, clip.y + half, Mathf.Max(0f, clip.width - width), Mathf.Max(0f, clip.height - width));
            if (!ClipLineToRect(ref from, ref to, clip)) return;

            var distance = Vector2.Distance(from, to);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(1f, width * 0.65f)));
            for (var i = 0; i <= steps; i++)
            {
                var point = Vector2.Lerp(from, to, i / (float)steps);
                DrawSolidRect(new Rect(point.x - half, point.y - half, width, width), color);
            }
        }

        private static void DrawClippedPoint(Rect clip, Vector2 point, Color color, float width)
        {
            var half = width * 0.5f;
            var x = Mathf.Clamp(point.x, clip.xMin + half, clip.xMax - half);
            var y = Mathf.Clamp(point.y, clip.yMin + half, clip.yMax - half);
            DrawSolidRect(new Rect(x - half, y - half, width, width), color);
        }

        private static bool ClipLineToRect(ref Vector2 from, ref Vector2 to, Rect rect)
        {
            var x0 = from.x;
            var y0 = from.y;
            var x1 = to.x;
            var y1 = to.y;
            var dx = x1 - x0;
            var dy = y1 - y0;
            var t0 = 0f;
            var t1 = 1f;

            if (!ClipLineEdge(-dx, x0 - rect.xMin, ref t0, ref t1)) return false;
            if (!ClipLineEdge(dx, rect.xMax - x0, ref t0, ref t1)) return false;
            if (!ClipLineEdge(-dy, y0 - rect.yMin, ref t0, ref t1)) return false;
            if (!ClipLineEdge(dy, rect.yMax - y0, ref t0, ref t1)) return false;

            from = new Vector2(x0 + t0 * dx, y0 + t0 * dy);
            to = new Vector2(x0 + t1 * dx, y0 + t1 * dy);
            return true;
        }

        private static bool ClipLineEdge(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 0.0001f) return q >= 0f;
            var r = q / p;
            if (p < 0f)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }
            return true;
        }

        private static void DecodeWaveFrameSamples(string hex, List<float> first, List<float> second)
        {
            if (string.IsNullOrEmpty(hex)) return;

            var bytes = Math.Min(8, hex.Length / 2);
            for (var i = 0; i < 4; i++) first.Add(i < bytes ? Mathf.Clamp01(ParseByte(hex, i * 2) / PreviewMaxByte) : 0f);
            for (var i = 4; i < 8; i++) second.Add(i < bytes ? Mathf.Clamp01(ParseByte(hex, i * 2) / PreviewMaxByte) : 0f);
        }

        private static string[] LimitGraphFrames(string[] wave)
        {
            if (wave == null || wave.Length <= MaxGraphFrames) return wave;

            var result = new string[MaxGraphFrames];
            for (var i = 0; i < MaxGraphFrames; i++)
            {
                var sourceIndex = Mathf.RoundToInt(i * (wave.Length - 1f) / (MaxGraphFrames - 1f));
                result[i] = wave[sourceIndex];
            }
            return result;
        }

        private static void DrawGrid(Rect graph)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(graph.x, graph.y, graph.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(graph.x, graph.y + graph.height * 0.5f, graph.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(graph.x, graph.yMax, graph.width, 1f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static int ParseByte(string hex, int index)
        {
            if (index + 1 >= hex.Length) return 0;
            return HexNibble(hex[index]) * 16 + HexNibble(hex[index + 1]);
        }

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static bool DrawToggleButton(Rect rect, string label, bool active)
        {
            var old = GUI.color;
            if (active) GUI.color = new Color(0.4f, 0.8f, 0.4f, 1f);
            var clicked = GUI.Button(rect, label);
            GUI.color = old;
            return clicked && !active;
        }

        private void DrawLibraryButtons(Rect rect, WavePreviewEntry[] entries, float row)
        {
            if (entries == null || entries.Length == 0) return;

            const float gap = 6f;
            var columns = LibraryGridColumns(rect.width);
            var cellWidth = LibraryGridCellWidth(rect.width, columns);
            var rows = Mathf.CeilToInt(entries.Length / (float)columns);
            for (var r = 0; r < rows; r++)
            {
                var y = rect.y + r * (row + gap);
                if (y + row > rect.yMax) break;
                for (var c = 0; c < columns; c++)
                {
                    var index = r * columns + c;
                    if (index >= entries.Length) return;
                    var x = rect.x + c * (cellWidth + gap);
                    if (DrawToggleButton(new Rect(x, y, cellWidth, row), entries[index].Label, index == _libraryIndex)) _libraryIndex = index;
                }
            }
        }

        private static int CountLibraryButtonRows(float width, WavePreviewEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return 0;
            var columns = LibraryGridColumns(width);
            return Mathf.CeilToInt(entries.Length / (float)columns);
        }

        private static int LibraryGridColumns(float availableWidth)
        {
            const float targetCellWidth = 96f;
            const float gap = 6f;
            var columns = Mathf.FloorToInt((availableWidth + gap) / (targetCellWidth + gap));
            return Mathf.Max(1, columns);
        }

        private static float LibraryGridCellWidth(float availableWidth, int columns)
        {
            const float gap = 6f;
            var width = (availableWidth - gap * (columns - 1)) / columns;
            return Mathf.Clamp(width, 72f, 140f);
        }

        private void DrawWrappedLabel(Rect rect, string text)
        {
            if (_wrappedLabelStyle == null)
                _wrappedLabelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, clipping = TextClipping.Clip };
            GUI.Label(rect, text, _wrappedLabelStyle);
        }

        private sealed class WavePreviewEntry
        {
            public string Label;
            public string[] Wave;

            public WavePreviewEntry(string label, string[] wave)
            {
                Label = label;
                Wave = wave;
            }
        }

        private sealed class WaveGraphCache
        {
            public readonly string Signature;
            public readonly List<float> FeelSamples;

            public WaveGraphCache(string signature, List<float> feelSamples)
            {
                Signature = signature;
                FeelSamples = feelSamples;
            }
        }

        private WavePreviewEntry[] WaveLibraryEntries()
        {
            return new[]
            {
                new WavePreviewEntry(_owner.T("Damage", "受伤"), DGLabWaveLibrary.DamagePulse),
                new WavePreviewEntry(_owner.T("Impact", "冲击"), DGLabWaveLibrary.ImpactPulse),
                new WavePreviewEntry(_owner.T("Bone break", "骨折"), DGLabWaveLibrary.BreakPulse),
                new WavePreviewEntry(_owner.T("Dislocation", "脱臼"), DGLabWaveLibrary.DislocatePulse),
                new WavePreviewEntry(_owner.T("Dismember", "断肢"), DGLabWaveLibrary.DismemberPulse),
                new WavePreviewEntry(_owner.T("Self-harm", "自伤"), DGLabWaveLibrary.SelfHarmPulse),
                new WavePreviewEntry(_owner.T("Coil shock", "电击"), DGLabWaveLibrary.Sting),
                new WavePreviewEntry(_owner.T("Treatment", "治疗"), DGLabWaveLibrary.TreatmentSting),
                new WavePreviewEntry(_owner.T("Pain", "疼痛"), DGLabWaveLibrary.PainThrob),
                new WavePreviewEntry(_owner.T("Pain", "疼痛"), DGLabWaveLibrary.PainTremor),
                new WavePreviewEntry(_owner.T("Pain", "疼痛"), DGLabWaveLibrary.SeverePainTremor),
                new WavePreviewEntry(_owner.T("Bone break", "骨折"), DGLabWaveLibrary.FractureThrob),
                new WavePreviewEntry(_owner.T("Dislocation", "脱臼"), DGLabWaveLibrary.JointPulse),
                new WavePreviewEntry(_owner.T("Injury", "损伤"), DGLabWaveLibrary.InjuryAche),
                new WavePreviewEntry(_owner.T("Bleeding", "出血"), DGLabWaveLibrary.BleedingDrain),
                new WavePreviewEntry(_owner.T("Oxygen", "缺氧"), DGLabWaveLibrary.OxygenStutter),
                new WavePreviewEntry(_owner.T("Heartbeat", "心跳"), DGLabWaveLibrary.Heartbeat),
                new WavePreviewEntry(_owner.T("Infection", "感染"), DGLabWaveLibrary.InfectionCrawl),
                new WavePreviewEntry(_owner.T("Sickness", "疾病"), DGLabWaveLibrary.SicknessRoll),
                new WavePreviewEntry(_owner.T("Hunger", "饥饿"), DGLabWaveLibrary.HungerGnaw),
                new WavePreviewEntry(_owner.T("Thirst", "口渴"), DGLabWaveLibrary.ThirstNeedle),
                new WavePreviewEntry(_owner.T("Temperature", "体温"), DGLabWaveLibrary.TemperatureWave),
                new WavePreviewEntry(_owner.T("Fatigue", "疲劳"), DGLabWaveLibrary.FatiguePulse),
                new WavePreviewEntry(_owner.T("Mood", "情绪"), DGLabWaveLibrary.MoodSink),
                new WavePreviewEntry(_owner.T("Shock", "休克"), DGLabWaveLibrary.ShockSpike),
                new WavePreviewEntry(_owner.T("Shock", "休克"), DGLabWaveLibrary.HeavyShock),
                new WavePreviewEntry(_owner.T("Critical", "危急"), DGLabWaveLibrary.CriticalLoop),
                new WavePreviewEntry(_owner.T("Death", "死亡"), DGLabWaveLibrary.DeathLoop)
            };
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
