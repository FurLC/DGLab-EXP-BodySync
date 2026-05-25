using System;
using System.Collections.Generic;
using System.Threading;
using DGLab.Network;
using DGLab.Protocol;

namespace DGLab
{
    public sealed class DGLabClient
    {
        private readonly DGLabWebSocketClient _socket;

        public string ClientId => _socket.ClientId;
        public string TargetId => _socket.TargetId;

        public event Action<string> OnRawMessage
        {
            add => _socket.OnRawMessage += value;
            remove => _socket.OnRawMessage -= value;
        }

        public event Action<DGLabMessage> OnMessage
        {
            add => _socket.OnMessage += value;
            remove => _socket.OnMessage -= value;
        }

        public event Action OnConnected
        {
            add => _socket.OnConnected += value;
            remove => _socket.OnConnected -= value;
        }

        public event Action<string> OnClosed
        {
            add => _socket.OnClosed += value;
            remove => _socket.OnClosed -= value;
        }

        public event Action<Exception> OnError
        {
            add => _socket.OnError += value;
            remove => _socket.OnError -= value;
        }

        public DGLabClient(string serverUrl, SynchronizationContext unityContext = null)
        {
            _socket = new DGLabWebSocketClient(serverUrl, unityContext);
        }

        public void Connect()
        {
            _socket.Connect();
        }

        public void Disconnect()
        {
            _socket.Disconnect();
        }

        public void SetStrengthA(int value)
        {
            _socket.SendStrengthSet(DGLabProtocol.ChannelA, value);
        }

        public void SetStrengthB(int value)
        {
            _socket.SendStrengthSet(DGLabProtocol.ChannelB, value);
        }

        public void IncreaseStrengthA()
        {
            _socket.SendStrengthDelta(DGLabProtocol.ChannelA, true);
        }

        public void DecreaseStrengthA()
        {
            _socket.SendStrengthDelta(DGLabProtocol.ChannelA, false);
        }

        public void IncreaseStrengthB()
        {
            _socket.SendStrengthDelta(DGLabProtocol.ChannelB, true);
        }

        public void DecreaseStrengthB()
        {
            _socket.SendStrengthDelta(DGLabProtocol.ChannelB, false);
        }

        public void ClearWaveA()
        {
            _socket.SendRawCommand("clear-1");
        }

        public void ClearWaveB()
        {
            _socket.SendRawCommand("clear-2");
        }

        public void SendWaveA(IList<string> hexSegments, int timeSeconds = 5)
        {
            var payload = DGLabProtocol.BuildWavePayload("A", hexSegments);
            _socket.SendWave("A", timeSeconds, payload);
        }

        public void SendWaveB(IList<string> hexSegments, int timeSeconds = 5)
        {
            var payload = DGLabProtocol.BuildWavePayload("B", hexSegments);
            _socket.SendWave("B", timeSeconds, payload);
        }
    }
}
