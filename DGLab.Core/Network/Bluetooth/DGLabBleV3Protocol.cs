namespace DGLab.BepInEx.Network.Bluetooth
{
    internal static class DGLabBleV3Protocol
    {
        public static byte[] BuildB0(int sequence, int strengthMode, int strengthA, int strengthB, DGLabBleWaveUnit[] waveA, DGLabBleWaveUnit[] waveB)
        {
            var packet = new byte[20];
            packet[0] = 0xB0;
            packet[1] = (byte)(((sequence & 0x0f) << 4) | (strengthMode & 0x0f));
            packet[2] = (byte)DGLabBleWave.Clamp(strengthA, 0, 200);
            packet[3] = (byte)DGLabBleWave.Clamp(strengthB, 0, 200);

            FillV3Wave(packet, 4, waveA, invalidWhenEmpty: true);
            FillV3Wave(packet, 12, waveB, invalidWhenEmpty: true);
            return packet;
        }

        public static byte[] BuildBf(int limitA, int limitB, int frequencyBalance = 0, int intensityBalance = 0)
        {
            return new[]
            {
                (byte)0xBF,
                (byte)DGLabBleWave.Clamp(limitA, 0, 200),
                (byte)DGLabBleWave.Clamp(limitB, 0, 200),
                (byte)DGLabBleWave.Clamp(frequencyBalance, 0, 255),
                (byte)DGLabBleWave.Clamp(frequencyBalance, 0, 255),
                (byte)DGLabBleWave.Clamp(intensityBalance, 0, 255),
                (byte)DGLabBleWave.Clamp(intensityBalance, 0, 255)
            };
        }

        private static void FillV3Wave(byte[] packet, int offset, DGLabBleWaveUnit[] wave, bool invalidWhenEmpty)
        {
            for (var i = 0; i < 4; i++)
            {
                if (wave == null || wave.Length == 0)
                {
                    packet[offset + i] = 0;
                    packet[offset + 4 + i] = (byte)(invalidWhenEmpty && i == 3 ? 101 : 0);
                    continue;
                }

                var unit = wave[i % wave.Length];
                packet[offset + i] = (byte)ScaleV3Frequency(unit.Frequency);
                packet[offset + 4 + i] = (byte)DGLabBleWave.Clamp(unit.Intensity, 0, 100);
            }
        }

        private static int ScaleV3Frequency(int value)
        {
            value = DGLabBleWave.Clamp(value, 10, 1000);
            if (value <= 100) return value;
            if (value <= 600) return (value - 100) / 5 + 100;
            return (value - 600) / 10 + 200;
        }
    }
}
