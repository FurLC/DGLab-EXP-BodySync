using System;
using System.Collections.Generic;
using DGLab.BepInEx.Protocol;
using Newtonsoft.Json;
using WebSocketSharp;

namespace DGLab.BepInEx.Network
{
    internal sealed class DGLabOtcWebSocketClient : IDGLabTransport
    {
        private readonly Uri _serverUri;
        private readonly object _socketLock = new object();
        private WebSocket _socket;
        private int _connectionGeneration;
        private int _lastStrengthA;
        private int _lastStrengthB;

        public string ClientId { get; private set; }
        public string TargetId { get; private set; }
        public bool IsConnected
        {
            get
            {
                WebSocket socket;
                lock (_socketLock) { socket = _socket; }
                return socket != null && socket.ReadyState == WebSocketState.Open;
            }
        }

        public event Action<string> OnRawMessage;
        public event Action<DGLabMessage> OnMessage;
        public event Action OnConnected;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        public DGLabOtcWebSocketClient(string serverUrl)
        {
            _serverUri = new Uri(serverUrl);
        }

        public void Connect()
        {
            Disconnect();

            WebSocket socket;
            int generation;
            lock (_socketLock)
            {
                generation = ++_connectionGeneration;
                socket = new WebSocket(_serverUri.ToString());
                _socket = socket;
            }

            socket.OnOpen += (_, __) =>
            {
                if (!IsCurrentGeneration(generation)) return;
                ClientId = "otc";
                TargetId = "otc";
                OnConnected?.Invoke();
            };
            socket.OnClose += (_, e) =>
            {
                if (!IsCurrentGeneration(generation)) return;
                ClientId = null;
                TargetId = null;
                OnClosed?.Invoke(e.Reason);
            };
            socket.OnError += (_, e) =>
            {
                if (!IsCurrentGeneration(generation)) return;
                OnError?.Invoke(e.Exception ?? new Exception(e.Message));
            };
            socket.OnMessage += (_, e) => HandleMessage(e, generation);
            socket.ConnectAsync();
        }

        public void Disconnect()
        {
            WebSocket socket;
            lock (_socketLock)
            {
                socket = _socket;
                _socket = null;
                _connectionGeneration++;
            }
            ClientId = null;
            TargetId = null;
            if (socket == null) return;
            try { socket.CloseAsync(); } catch { }
        }

        public void Send(object payload)
        {
            WebSocket socket;
            lock (_socketLock) { socket = _socket; }
            if (socket == null || socket.ReadyState != WebSocketState.Open) return;

            var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            socket.SendAsync(json, null);
        }

        public void SendStrengthSet(int channel, int strength)
        {
            var value = ScaleBodySyncStrength(strength);
            if (channel == DGLabProtocol.ChannelB) _lastStrengthB = value;
            else _lastStrengthA = value;

            Send(new OtcSetPatternMessage
            {
                A_intensity = channel == DGLabProtocol.ChannelA ? (int?)_lastStrengthA : null,
                B_intensity = channel == DGLabProtocol.ChannelB ? (int?)_lastStrengthB : null
            });
        }

        public void SendStrengthDelta(int channel, bool increase)
        {
            var current = channel == DGLabProtocol.ChannelB ? _lastStrengthB : _lastStrengthA;
            SendStrengthSet(channel, current + (increase ? 5 : -5));
        }

        public void SendRawCommand(string command)
        {
            if (string.Equals(command, "clear-1", StringComparison.OrdinalIgnoreCase))
            {
                SendStrengthSet(DGLabProtocol.ChannelA, 0);
                return;
            }
            if (string.Equals(command, "clear-2", StringComparison.OrdinalIgnoreCase))
            {
                SendStrengthSet(DGLabProtocol.ChannelB, 0);
                return;
            }
            if (string.Equals(command, "stop_pattern", StringComparison.OrdinalIgnoreCase))
            {
                Send(new OtcStopPatternMessage());
            }
        }

        public void SendWave(string channel, int timeSeconds, string wavePayload)
        {
            var units = BuildPatternUnits(channel, wavePayload);
            var ticks = Math.Max(0, timeSeconds * 10);

            if (string.Equals(channel, "B", StringComparison.OrdinalIgnoreCase))
            {
                Send(new OtcSetPatternMessage
                {
                    B_pattern_units = units,
                    B_intensity = _lastStrengthB,
                    B_ticks = ticks
                });
            }
            else
            {
                Send(new OtcSetPatternMessage
                {
                    A_pattern_units = units,
                    A_intensity = _lastStrengthA,
                    A_ticks = ticks
                });
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        private void HandleMessage(MessageEventArgs e, int generation)
        {
            if (!e.IsText) return;

            if (!IsCurrentGeneration(generation)) return;

            OnRawMessage?.Invoke(e.Data);
            try
            {
                var msg = JsonConvert.DeserializeObject<DGLabMessage>(e.Data);
                if (msg != null) OnMessage?.Invoke(msg);
            }
            catch
            {
                // OTC replies use controller-specific payloads; raw logging is enough here.
            }
        }

        private bool IsCurrentGeneration(int generation)
        {
            lock (_socketLock)
            {
                return generation == _connectionGeneration;
            }
        }

        private static OtcPatternUnit[] BuildPatternUnits(string channel, string wavePayload)
        {
            var normalized = NormalizeWavePayload(channel, wavePayload);
            if (string.IsNullOrWhiteSpace(normalized)) return new OtcPatternUnit[0];

            var parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var units = new List<OtcPatternUnit>(parts.Length);
            foreach (var part in parts)
            {
                var hex = part.Trim().Trim('"');
                if (hex.Length < 4) continue;

                var half = hex.Length / 2;
                if (half % 2 != 0) half--;
                if (half <= 0) half = 2;

                var intensity = AverageHexBytes(hex, 0, half);
                var frequency = AverageHexBytes(hex, half, hex.Length - half);
                units.Add(new OtcPatternUnit
                {
                    pattern_intensity = ClampPercent(intensity),
                    frequency = ClampPercent(frequency)
                });
            }

            return units.ToArray();
        }

        private static string NormalizeWavePayload(string channel, string wavePayload)
        {
            if (string.IsNullOrEmpty(wavePayload)) return string.Empty;
            var prefix = channel + ":[";
            if (wavePayload.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && wavePayload.EndsWith("]", StringComparison.Ordinal))
            {
                return wavePayload.Substring(prefix.Length, wavePayload.Length - prefix.Length - 1);
            }
            return wavePayload;
        }

        private static int AverageHexBytes(string hex, int start, int length)
        {
            var end = Math.Min(hex.Length, start + length);
            var sum = 0;
            var count = 0;
            for (var i = start; i + 1 < end; i += 2)
            {
                try
                {
                    sum += Convert.ToInt32(hex.Substring(i, 2), 16);
                    count++;
                }
                catch
                {
                    return 0;
                }
            }
            return count == 0 ? 0 : (int)Math.Round(sum / (double)count);
        }

        private static int ClampPercent(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        private static int ScaleBodySyncStrength(int value)
        {
            if (value <= 0) return 0;
            if (value >= 200) return 100;
            return (int)Math.Round(value / 2.0);
        }

        private sealed class OtcSetPatternMessage
        {
            public string cmd = "set_pattern";
            public OtcPatternUnit[] A_pattern_units;
            public OtcPatternUnit[] B_pattern_units;
            public int? A_intensity;
            public int? B_intensity;
            public int? A_ticks;
            public int? B_ticks;
        }

        private sealed class OtcStopPatternMessage
        {
            public string cmd = "stop_pattern";
        }

        private sealed class OtcPatternUnit
        {
            public int pattern_intensity;
            public int frequency;
        }
    }
}
