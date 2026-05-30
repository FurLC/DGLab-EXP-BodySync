using System;
using System.Collections.Generic;

namespace DGLab.BepInEx.Network.Bluetooth
{
    internal struct DGLabBleWaveUnit
    {
        public int Intensity;
        public int Frequency;
    }

    internal static class DGLabBleWave
    {
        public static DGLabBleWaveUnit[] FromSocketWavePayload(string channel, string wavePayload)
        {
            var normalized = NormalizeWavePayload(channel, wavePayload);
            if (string.IsNullOrWhiteSpace(normalized)) return new DGLabBleWaveUnit[0];

            var parts = normalized.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var units = new List<DGLabBleWaveUnit>(parts.Length);
            foreach (var part in parts)
            {
                var hex = part.Trim().Trim('"');
                if (hex.Length < 4) continue;

                var half = hex.Length / 2;
                if (half % 2 != 0) half--;
                if (half <= 0) half = 2;

                var intensity = AverageHexBytes(hex, 0, half);
                var frequency = AverageHexBytes(hex, half, hex.Length - half);
                units.Add(new DGLabBleWaveUnit
                {
                    Intensity = Clamp(intensity, 0, 100),
                    Frequency = Clamp(frequency, 10, 1000)
                });
            }

            return units.ToArray();
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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

        private static int AverageHexBytes(string hex, int start, int length)
        {
            var end = Math.Min(hex.Length, start + length);
            var sum = 0;
            var count = 0;
            for (var i = start; i + 1 < end; i += 2)
            {
                try
                {
                    sum += Convert.ToInt32(hex.Substring(i, 2), 16);
                    count++;
                }
                catch
                {
                    return 0;
                }
            }
            return count == 0 ? 0 : (int)Math.Round(sum / (double)count);
        }
    }
}
