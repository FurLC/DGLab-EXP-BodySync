using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Bluetooth;

namespace DGLab.BepInEx.Network.Bluetooth
{
    public sealed class InTheHandBleGattClient : IBleGattClient
    {
        private readonly DGLabBluetoothProfile _profile;
        private readonly object _sync = new object();
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<Guid, GattService> _services = new Dictionary<Guid, GattService>();
        private readonly Dictionary<string, GattCharacteristic> _characteristics = new Dictionary<string, GattCharacteristic>();
        private BluetoothDevice _device;
        private int _generation;

        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<Exception> Error;
        public event Action<Guid, Guid, byte[]> Notification;

        public bool IsConnected => _device != null && _device.Gatt != null && _device.Gatt.IsConnected;

        public InTheHandBleGattClient(DGLabBluetoothProfile profile)
        {
            _profile = profile;
        }

        public static async Task<IReadOnlyList<BleDeviceInfo>> ScanDevicesAsync(DGLabBluetoothProfile profile, TimeSpan timeout)
        {
            var options = CreateRequestOptions(profile, string.Empty, timeout);
            using (var cts = new CancellationTokenSource(timeout))
            {
                var advertisements = await global::InTheHand.Bluetooth.Bluetooth.ScanForDevicesAsync(options, cts.Token).ConfigureAwait(false);
                var result = new List<BleDeviceInfo>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var device in advertisements)
                {
                    var id = device != null ? device.Id : string.Empty;
                    var name = device != null ? device.Name : string.Empty;
                    var key = !string.IsNullOrWhiteSpace(id) ? id : name;
                    if (string.IsNullOrWhiteSpace(key) || !seen.Add(key)) continue;
                    result.Add(new BleDeviceInfo(id, name, int.MinValue));
                }
                return result;
            }
        }

        public void Connect(string deviceNameOrAddress)
        {
            RunAsync(() => ConnectAsync(deviceNameOrAddress));
        }

        public void Disconnect()
        {
            try
            {
                if (_device != null && _device.Gatt != null) _device.Gatt.Disconnect();
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
            finally
            {
                lock (_sync)
                {
                    _generation++;
                    ClearCachesLocked();
                }
                Disconnected?.Invoke("disconnect requested");
            }
        }

        public void Write(Guid serviceUuid, Guid characteristicUuid, byte[] value, bool withResponse)
        {
            RunAsync(() => WriteAsync(serviceUuid, characteristicUuid, value, withResponse));
        }

        public void Subscribe(Guid serviceUuid, Guid characteristicUuid)
        {
            RunAsync(() => SubscribeAsync(serviceUuid, characteristicUuid));
        }

        public void Dispose()
        {
            Disconnect();
        }

        private async Task ConnectAsync(string deviceNameOrAddress)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            int generation;
            lock (_sync)
            {
                generation = ++_generation;
                ClearCachesLocked();
            }

            try
            {
                var options = CreateRequestOptions(_profile, deviceNameOrAddress, TimeSpan.FromSeconds(15));

                var device = await global::InTheHand.Bluetooth.Bluetooth.RequestDeviceAsync(options).ConfigureAwait(false);
                if (device == null)
                {
                    throw new InvalidOperationException("No DG-Lab Bluetooth device selected or found.");
                }

                device.GattServerDisconnected += (_, __) =>
                {
                    lock (_sync)
                    {
                        _generation++;
                        ClearCachesLocked();
                    }
                    Disconnected?.Invoke("GATT server disconnected");
                };

                await device.Gatt.ConnectAsync().ConfigureAwait(false);
                lock (_sync)
                {
                    if (generation != _generation)
                    {
                        try { device.Gatt.Disconnect(); } catch { }
                        return;
                    }
                    _device = device;
                }
                Connected?.Invoke();
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
                Disconnected?.Invoke(ex.Message);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task WriteAsync(Guid serviceUuid, Guid characteristicUuid, byte[] value, bool withResponse)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var characteristic = await GetCharacteristicAsync(serviceUuid, characteristicUuid).ConfigureAwait(false);
                if (withResponse) await characteristic.WriteValueWithResponseAsync(value).ConfigureAwait(false);
                else await characteristic.WriteValueWithoutResponseAsync(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task SubscribeAsync(Guid serviceUuid, Guid characteristicUuid)
        {
            await _operationLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var characteristic = await GetCharacteristicAsync(serviceUuid, characteristicUuid).ConfigureAwait(false);
                characteristic.CharacteristicValueChanged += (_, e) =>
                {
                    if (e.Error != null)
                    {
                        Error?.Invoke(e.Error);
                        return;
                    }
                    Notification?.Invoke(serviceUuid, characteristicUuid, e.Value);
                };
                await characteristic.StartNotificationsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task<GattCharacteristic> GetCharacteristicAsync(Guid serviceUuid, Guid characteristicUuid)
        {
            BluetoothDevice device;
            lock (_sync)
            {
                device = _device;
            }
            if (device == null || device.Gatt == null || !device.Gatt.IsConnected) throw new InvalidOperationException("Bluetooth device is not connected.");

            var key = serviceUuid.ToString("D") + ":" + characteristicUuid.ToString("D");
            GattCharacteristic characteristic;
            lock (_sync)
            {
                if (_characteristics.TryGetValue(key, out characteristic)) return characteristic;
            }

            GattService service;
            lock (_sync)
            {
                _services.TryGetValue(serviceUuid, out service);
            }
            if (service == null)
            {
                service = await device.Gatt.GetPrimaryServiceAsync(BluetoothUuid.FromGuid(serviceUuid)).ConfigureAwait(false);
                lock (_sync)
                {
                    if (!ReferenceEquals(device, _device)) throw new InvalidOperationException("Bluetooth device disconnected during operation.");
                    _services[serviceUuid] = service;
                }
            }

            characteristic = await service.GetCharacteristicAsync(BluetoothUuid.FromGuid(characteristicUuid)).ConfigureAwait(false);
            lock (_sync)
            {
                if (!ReferenceEquals(device, _device)) throw new InvalidOperationException("Bluetooth device disconnected during operation.");
                _characteristics[key] = characteristic;
            }
            return characteristic;
        }

        private void ClearCachesLocked()
        {
            _services.Clear();
            _characteristics.Clear();
            _device = null;
        }

        private void RunAsync(Func<Task> operation)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    operation().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Error?.Invoke(ex);
                }
            });
        }

        private static RequestDeviceOptions CreateRequestOptions(DGLabBluetoothProfile profile, string deviceNameOrAddress, TimeSpan timeout)
        {
            var prefix = profile == DGLabBluetoothProfile.V2 ? "D-LAB" : "47";
            var service = profile == DGLabBluetoothProfile.V2 ? DGLabBleUuids.V2PulseService : DGLabBleUuids.V3PulseService;
            var selector = string.IsNullOrWhiteSpace(deviceNameOrAddress) ? prefix : deviceNameOrAddress.Trim();

            var options = new RequestDeviceOptions { Timeout = timeout };
            var filter = new BluetoothLEScanFilter { NamePrefix = selector };
            filter.Services.Add(BluetoothUuid.FromGuid(service));
            options.Filters.Add(filter);
            options.OptionalServices.Add(BluetoothUuid.FromGuid(service));
            options.OptionalServices.Add(BluetoothUuid.FromGuid(DGLabBleUuids.BatteryService));
            return options;
        }
    }
}
