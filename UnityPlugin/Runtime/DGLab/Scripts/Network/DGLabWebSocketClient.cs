using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DGLab.Protocol;
using Newtonsoft.Json;
using WebSocketSharp;

namespace DGLab.Network
{
    public sealed class DGLabWebSocketClient : IDisposable
    {
        private readonly Uri _serverUri;
        private WebSocket _socket;
        private readonly SynchronizationContext _unityContext;

        public event Action<string> OnRawMessage;
        public event Action<DGLabMessage> OnMessage;
        public event Action OnConnected;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        public string ClientId { get; private set; }
        public string TargetId { get; private set; }

        public DGLabWebSocketClient(string serverUrl, SynchronizationContext unityContext = null)
        {
            _serverUri = new Uri(serverUrl);
            _unityContext = unityContext ?? SynchronizationContext.Current;
        }

        public void Connect()
        {
            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }

            _socket = new WebSocket(_serverUri.ToString());
            _socket.OnOpen += (_, __) => PostToUnity(() => OnConnected?.Invoke());
            _socket.OnClose += (_, e) => PostToUnity(() => OnClosed?.Invoke(e.Reason));
            _socket.OnError += (_, e) => PostToUnity(() => OnError?.Invoke(e.Exception ?? new Exception(e.Message)));
            _socket.OnMessage += (_, e) =>
            {
                if (!e.IsText)
                {
                    return;
                }

                var payload = e.Data;
                PostToUnity(() => OnRawMessage?.Invoke(payload));

                try
                {
                    var msg = JsonConvert.DeserializeObject<DGLabMessage>(payload);
                    if (msg != null)
                    {
                        if (!string.IsNullOrEmpty(msg.clientId))
                        {
                            ClientId = msg.clientId;
                        }

                        if (!string.IsNullOrEmpty(msg.targetId))
                        {
                            TargetId = msg.targetId;
                        }

                        PostToUnity(() => OnMessage?.Invoke(msg));
                    }
                }
                catch (Exception ex)
                {
                    PostToUnity(() => OnError?.Invoke(ex));
                }
            };

            _socket.ConnectAsync();
        }

        public void Disconnect()
        {
            if (_socket == null)
            {
                return;
            }

            _socket.CloseAsync();
            _socket = null;
        }

        public void Send(object payload)
        {
            if (_socket == null || _socket.ReadyState != WebSocketState.Open)
            {
                return;
            }

            var json = JsonConvert.SerializeObject(payload);
            _socket.SendAsync(json, null);
        }

        public void SendStrengthSet(int channel, int strength)
        {
            var msg = new ClientStrengthMessage
            {
                channel = channel,
                strength = strength,
                clientId = ClientId,
                targetId = TargetId
            };

            Send(msg);
        }

        public void SendStrengthDelta(int channel, bool increase)
        {
            var msg = new ClientDeltaStrengthMessage
            {
                type = increase ? 2 : 1,
                channel = channel,
                clientId = ClientId,
                targetId = TargetId
            };

            Send(msg);
        }

        public void SendRawCommand(string command)
        {
            var msg = new ClientRawMessage
            {
                message = command,
                clientId = ClientId,
                targetId = TargetId
            };

            Send(msg);
        }

        public void SendWave(string channel, int timeSeconds, string wavePayload)
        {
            var msg = new ClientWaveMessage
            {
                channel = channel,
                time = timeSeconds,
                message = wavePayload,
                clientId = ClientId,
                targetId = TargetId
            };

            Send(msg);
        }

        private void PostToUnity(Action action)
        {
            if (_unityContext == null)
            {
                action?.Invoke();
                return;
            }

            _unityContext.Post(_ => action?.Invoke(), null);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
