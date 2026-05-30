using System;

namespace DGLab.BepInEx.Network.Bluetooth
{
    public sealed class MissingBleGattClient : IBleGattClient
    {
#pragma warning disable CS0067
        public event Action Connected;
#pragma warning restore CS0067
        public event Action<string> Disconnected;
        public event Action<Exception> Error;
#pragma warning disable CS0067
        public event Action<Guid, Guid, byte[]> Notification;
#pragma warning restore CS0067

        public bool IsConnected => false;

        public void Connect(string deviceNameOrAddress)
        {
            Error?.Invoke(new NotSupportedException("No platform BLE implementation is bundled. Add an IBleGattClient implementation for Windows or Linux."));
            Disconnected?.Invoke("missing BLE implementation");
        }

        public void Disconnect()
        {
            Disconnected?.Invoke("disconnect requested");
        }

        public void Write(Guid serviceUuid, Guid characteristicUuid, byte[] value, bool withResponse)
        {
        }

        public void Subscribe(Guid serviceUuid, Guid characteristicUuid)
        {
        }

        public void Dispose()
        {
        }
    }
}
