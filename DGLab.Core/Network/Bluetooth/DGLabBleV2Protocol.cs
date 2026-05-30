using DGLab.BepInEx.Protocol;

namespace DGLab.BepInEx.Network.Bluetooth
{
    internal static class DGLabBleV2Protocol
    {
        public static byte[] BuildStrength(int strengthA, int strengthB)
        {
            var a = DGLabBleWave.Clamp(strengthA, 0, 200) * 7;
            var b = DGLabBleWave.Clamp(strengthB, 0, 200) * 7;
            var packed = ((a & 0x7ff) << 11) | (b & 0x7ff);
            return new[]
            {
                (byte)((packed >> 16) & 0xff),
                (byte)((packed >> 8) & 0xff),
                (byte)(packed & 0xff)
            };
        }

        public static byte[] BuildWave(DGLabBleWaveUnit unit)
        {
            var frequency = DGLabBleWave.Clamp(unit.Frequency, 10, 1000);
            var x = DGLabBleWave.Clamp((int)System.Math.Round(System.Math.Sqrt(frequency / 1000.0) * 15.0), 1, 31);
            var y = DGLabBleWave.Clamp(frequency - x, 0, 1023);
            var z = DGLabBleWave.Clamp((int)System.Math.Round(unit.Intensity / 100.0 * 20.0), 0, 31);
            var packed = ((z & 0x1f) << 15) | ((y & 0x3ff) << 5) | (x & 0x1f);
            return new[]
            {
                (byte)((packed >> 16) & 0xff),
                (byte)((packed >> 8) & 0xff),
                (byte)(packed & 0xff)
            };
        }

        public static System.Guid WaveCharacteristicForChannel(int channel)
        {
            return channel == DGLabProtocol.ChannelB ? DGLabBleUuids.V2WaveB : DGLabBleUuids.V2WaveA;
        }
    }
}
