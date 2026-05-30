using System.Collections.Generic;
using DGLab.BepInEx.Network;
using DGLab.BepInEx.Network.Bluetooth;
using DGLab.BepInEx.Protocol;

namespace DGLab.BepInEx
{
    public sealed class DGLabClient
    {
        private readonly IDGLabTransport _socket;

        public string ClientId => _socket.ClientId;
        public string TargetId => _socket.TargetId;
        public bool IsConnected => _socket.IsConnected;
        public bool HasTarget => _socket.IsConnected && !string.IsNullOrEmpty(_socket.TargetId);

        public event System.Action<string> OnRawMessage
        {
            add => _socket.OnRawMessage += value;
            remove => _socket.OnRawMessage -= value;
        }

        public event System.Action<DGLabMessage> OnMessage
        {
            add => _socket.OnMessage += value;
            remove => _socket.OnMessage -= value;
        }

        public event System.Action OnConnected
        {
            add => _socket.OnConnected += value;
            remove => _socket.OnConnected -= value;
        }

        public event System.Action<string> OnClosed
        {
            add => _socket.OnClosed += value;
            remove => _socket.OnClosed -= value;
        }

        public event System.Action<System.Exception> OnError
        {
            add => _socket.OnError += value;
            remove => _socket.OnError -= value;
        }

        public DGLabClient(string serverUrl)
        {
            _socket = new DGLabWebSocketClient(serverUrl);
        }

        public DGLabClient(string serverUrl, bool useOtcController)
        {
            _socket = useOtcController ? (IDGLabTransport)new DGLabOtcWebSocketClient(serverUrl) : new DGLabWebSocketClient(serverUrl);
        }

        public DGLabClient(string bindAddress, int port, string terminalId)
        {
            _socket = new DGLabEmbeddedWebSocketServer(bindAddress, port, terminalId);
        }

        public DGLabClient(IBleGattClient ble, DGLabBluetoothProfile profile, string deviceNameOrAddress)
        {
            _socket = new DGLabBluetoothTransport(ble, profile, deviceNameOrAddress);
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
