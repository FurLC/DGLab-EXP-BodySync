using System;
using DGLab.BepInEx.Protocol;

namespace DGLab.BepInEx.Network.Bluetooth
{
    internal sealed class DGLabBluetoothTransport : IDGLabTransport
    {
        private readonly IBleGattClient _ble;
        private readonly DGLabBluetoothProfile _profile;
        private readonly string _deviceNameOrAddress;
        private int _strengthA;
        private int _strengthB;
        private DGLabBleWaveUnit[] _waveA = new DGLabBleWaveUnit[0];
        private DGLabBleWaveUnit[] _waveB = new DGLabBleWaveUnit[0];
        private int _sequence;

        public string ClientId { get; private set; }
        public string TargetId { get; private set; }
        public bool IsConnected => _ble.IsConnected;

        public event Action<string> OnRawMessage;
#pragma warning disable CS0067
        public event Action<DGLabMessage> OnMessage;
#pragma warning restore CS0067
        public event Action OnConnected;
        public event Action<string> OnClosed;
        public event Action<Exception> OnError;

        public DGLabBluetoothTransport(IBleGattClient ble, DGLabBluetoothProfile profile, string deviceNameOrAddress)
        {
            _ble = ble ?? throw new ArgumentNullException(nameof(ble));
            _profile = profile;
            _deviceNameOrAddress = deviceNameOrAddress;

            _ble.Connected += HandleConnected;
            _ble.Disconnected += HandleDisconnected;
            _ble.Error += ex => OnError?.Invoke(ex);
            _ble.Notification += HandleNotification;
        }

        public void Connect()
        {
            _ble.Connect(_deviceNameOrAddress);
        }

        public void Disconnect()
        {
            ClientId = null;
            TargetId = null;
            _ble.Disconnect();
        }

        public void Send(object payload)
        {
        }

        public void SendStrengthSet(int channel, int strength)
        {
            if (channel == DGLabProtocol.ChannelB) _strengthB = DGLabBleWave.Clamp(strength, 0, 200);
            else _strengthA = DGLabBleWave.Clamp(strength, 0, 200);
            FlushStrengthAndWave();
        }

        public void SendStrengthDelta(int channel, bool increase)
        {
            var current = channel == DGLabProtocol.ChannelB ? _strengthB : _strengthA;
            SendStrengthSet(channel, current + (increase ? 1 : -1));
        }

        public void SendRawCommand(string command)
        {
            if (string.Equals(command, "clear-1", StringComparison.OrdinalIgnoreCase))
            {
                _waveA = new DGLabBleWaveUnit[0];
                SendStrengthSet(DGLabProtocol.ChannelA, 0);
                return;
            }
            if (string.Equals(command, "clear-2", StringComparison.OrdinalIgnoreCase))
            {
                _waveB = new DGLabBleWaveUnit[0];
                SendStrengthSet(DGLabProtocol.ChannelB, 0);
                return;
            }
            if (string.Equals(command, "stop_pattern", StringComparison.OrdinalIgnoreCase))
            {
                _waveA = new DGLabBleWaveUnit[0];
                _waveB = new DGLabBleWaveUnit[0];
                _strengthA = 0;
                _strengthB = 0;
                FlushStrengthAndWave();
            }
        }

        public void SendWave(string channel, int timeSeconds, string wavePayload)
        {
            var units = DGLabBleWave.FromSocketWavePayload(channel, wavePayload);
            if (string.Equals(channel, "B", StringComparison.OrdinalIgnoreCase)) _waveB = units;
            else _waveA = units;
            FlushStrengthAndWave();
        }

        public void Dispose()
        {
            _ble.Dispose();
        }

        private void HandleConnected()
        {
            ClientId = "bluetooth";
            TargetId = _profile == DGLabBluetoothProfile.V2 ? "dglab-v2" : "dglab-v3";
            if (_profile == DGLabBluetoothProfile.V3)
            {
                _ble.Subscribe(DGLabBleUuids.V3PulseService, DGLabBleUuids.V3Notify);
                _ble.Write(DGLabBleUuids.V3PulseService, DGLabBleUuids.V3Write, DGLabBleV3Protocol.BuildBf(200, 200), withResponse: false);
            }
            OnConnected?.Invoke();
        }

        private void HandleDisconnected(string reason)
        {
            ClientId = null;
            TargetId = null;
            OnClosed?.Invoke(reason);
        }

        private void HandleNotification(Guid service, Guid characteristic, byte[] value)
        {
            var hex = value == null ? string.Empty : BitConverter.ToString(value).Replace("-", string.Empty);
            OnRawMessage?.Invoke(hex);
        }

        private void FlushStrengthAndWave()
        {
            if (!_ble.IsConnected) return;

            if (_profile == DGLabBluetoothProfile.V2)
            {
                _ble.Write(DGLabBleUuids.V2PulseService, DGLabBleUuids.V2Strength, DGLabBleV2Protocol.BuildStrength(_strengthA, _strengthB), withResponse: false);
                if (_waveA.Length > 0) _ble.Write(DGLabBleUuids.V2PulseService, DGLabBleV2Protocol.WaveCharacteristicForChannel(DGLabProtocol.ChannelA), DGLabBleV2Protocol.BuildWave(_waveA[0]), withResponse: false);
                if (_waveB.Length > 0) _ble.Write(DGLabBleUuids.V2PulseService, DGLabBleV2Protocol.WaveCharacteristicForChannel(DGLabProtocol.ChannelB), DGLabBleV2Protocol.BuildWave(_waveB[0]), withResponse: false);
                return;
            }

            var mode = StrengthMode();
            var seq = mode == 0 ? 0 : NextSequence();
            var packet = DGLabBleV3Protocol.BuildB0(seq, mode, _strengthA, _strengthB, _waveA, _waveB);
            _ble.Write(DGLabBleUuids.V3PulseService, DGLabBleUuids.V3Write, packet, withResponse: false);
        }

        private int StrengthMode()
        {
            var modeA = 3;
            var modeB = 3;
            return ((modeA & 0x03) << 2) | (modeB & 0x03);
        }

        private int NextSequence()
        {
            _sequence = (_sequence % 15) + 1;
            return _sequence;
        }
    }
}
