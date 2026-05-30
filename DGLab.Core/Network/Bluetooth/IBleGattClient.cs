using System;

namespace DGLab.BepInEx.Network.Bluetooth
{
    public struct BleDeviceInfo
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Rssi;

        public BleDeviceInfo(string id, string name, int rssi)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Rssi = rssi;
        }

        public string DisplayName
        {
            get
            {
                var label = string.IsNullOrWhiteSpace(Name) ? Id : Name;
                if (string.IsNullOrWhiteSpace(label)) label = "Unknown BLE device";
                return Rssi == int.MinValue ? label : label + " (RSSI " + Rssi + ")";
            }
        }
    }

    public interface IBleGattClient : IDisposable
    {
        event Action Connected;
        event Action<string> Disconnected;
        event Action<Exception> Error;
        event Action<Guid, Guid, byte[]> Notification;

        bool IsConnected { get; }

        void Connect(string deviceNameOrAddress);
        void Disconnect();
        void Write(Guid serviceUuid, Guid characteristicUuid, byte[] value, bool withResponse);
        void Subscribe(Guid serviceUuid, Guid characteristicUuid);
    }
}
