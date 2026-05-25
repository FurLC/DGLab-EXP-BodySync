using System;
using DGLab.BepInEx.Protocol;
using Newtonsoft.Json;
using WebSocketSharp;

namespace DGLab.BepInEx.Network
{
    public sealed class DGLabWebSocketClient : IDGLabTransport
    {
        private readonly Uri _serverUri;
        private WebSocket _socket;

        public event Action<string> OnRawMessage;
        public event Action<DGLabMessage> OnMessage;
        public event Action OnConnected;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        public string ClientId { get; private set; }
        public string TargetId { get; private set; }

        public DGLabWebSocketClient(string serverUrl)
        {
            _serverUri = new Uri(serverUrl);
        }

        public void Connect()
        {
            Disconnect();

            _socket = new WebSocket(_serverUri.ToString());
            _socket.OnOpen += (_, __) => OnConnected?.Invoke();
            _socket.OnClose += (_, e) => OnClosed?.Invoke(e.Reason);
            _socket.OnError += (_, e) => OnError?.Invoke(e.Exception ?? new Exception(e.Message));
            _socket.OnMessage += (_, e) => HandleMessage(e);
            _socket.ConnectAsync();
        }

        public void Disconnect()
        {
            if (_socket == null) return;

            _socket.CloseAsync();
            _socket = null;
        }

        public void Send(object payload)
        {
            if (_socket == null || _socket.ReadyState != WebSocketState.Open) return;

            var json = JsonConvert.SerializeObject(payload);
            _socket.SendAsync(json, null);
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

        private void HandleMessage(MessageEventArgs e)
        {
            if (!e.IsText) return;

            var payload = e.Data;
            OnRawMessage?.Invoke(payload);

            try
            {
                var msg = JsonConvert.DeserializeObject<DGLabMessage>(payload);
                if (msg == null) return;

                if (!string.IsNullOrEmpty(msg.clientId)) ClientId = msg.clientId;
                if (!string.IsNullOrEmpty(msg.targetId)) TargetId = msg.targetId;
                OnMessage?.Invoke(msg);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
            }
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
