using System;
using DGLab.BepInEx.Protocol;
using Newtonsoft.Json;
using WebSocketSharp;

namespace DGLab.BepInEx.Network
{
    public sealed class DGLabWebSocketClient : IDGLabTransport
    {
        private readonly Uri _serverUri;
        private readonly object _socketLock = new object();
        private WebSocket _socket;
        private int _connectionGeneration;

        public event Action<string> OnRawMessage;
        public event Action<DGLabMessage> OnMessage;
        public event Action OnConnected;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        private volatile string _clientId;
        private volatile string _targetId;
        public string ClientId => _clientId;
        public string TargetId => _targetId;
        public bool IsConnected
        {
            get
            {
                WebSocket socket;
                lock (_socketLock) { socket = _socket; }
                return socket != null && socket.ReadyState == WebSocketState.Open;
            }
        }

        public DGLabWebSocketClient(string serverUrl)
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
                OnConnected?.Invoke();
            };
            socket.OnClose += (_, e) =>
            {
                if (!IsCurrentGeneration(generation)) return;
                ClearBindingState();
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
            ClearBindingState();
            if (socket == null) return;
            try { socket.CloseAsync(); } catch { }
        }

        public void Send(object payload)
        {
            WebSocket socket;
            lock (_socketLock) { socket = _socket; }
            if (socket == null || socket.ReadyState != WebSocketState.Open) return;

            var json = JsonConvert.SerializeObject(payload);
            socket.SendAsync(json, null);
        }

        public void SendStrengthSet(int channel, int strength)
        {
            SendSocketV2Command("strength-" + channel + "+2+" + strength);
        }

        public void SendStrengthDelta(int channel, bool increase)
        {
            SendSocketV2Command("strength-" + channel + "+" + (increase ? "1" : "0") + "+1");
        }

        public void SendRawCommand(string command)
        {
            SendSocketV2Command(command);
        }

        public void SendWave(string channel, int timeSeconds, string wavePayload)
        {
            SendSocketV2Command("pulse-" + channel + ":[" + NormalizeWavePayload(channel, wavePayload) + "]");
        }

        private void SendSocketV2Command(string command)
        {
            if (string.IsNullOrEmpty(ClientId) || string.IsNullOrEmpty(TargetId)) return;

            Send(new DGLabMessage
            {
                type = "msg",
                clientId = ClientId,
                targetId = TargetId,
                message = command
            });
        }

        public void Dispose()
        {
            Disconnect();
        }

        private void HandleMessage(MessageEventArgs e, int generation)
        {
            if (!e.IsText) return;

            if (!IsCurrentGeneration(generation)) return;

            var payload = e.Data;
            OnRawMessage?.Invoke(payload);

            try
            {
                var msg = JsonConvert.DeserializeObject<DGLabMessage>(payload);
                if (msg == null) return;

                if (!string.IsNullOrEmpty(msg.clientId)) _clientId = msg.clientId;
                if (!string.IsNullOrEmpty(msg.targetId)) _targetId = msg.targetId;
                OnMessage?.Invoke(msg);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
        }

        private bool IsCurrentGeneration(int generation)
        {
            lock (_socketLock)
            {
                return generation == _connectionGeneration;
            }
        }

        private void ClearBindingState()
        {
            _clientId = null;
            _targetId = null;
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
    }
}
