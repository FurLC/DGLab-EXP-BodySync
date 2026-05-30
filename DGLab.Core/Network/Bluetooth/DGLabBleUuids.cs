using System;

namespace DGLab.BepInEx.Network.Bluetooth
{
    internal static class DGLabBleUuids
    {
        public static readonly Guid BatteryService = Uuid16(0x180A);
        public static readonly Guid BatteryLevel = Uuid16(0x1500);

        public static readonly Guid V2PulseService = Guid.Parse("955a180b-0fe2-f5aa-a094-84b8d4f3e8ad");
        public static readonly Guid V2Strength = Guid.Parse("955a1504-0fe2-f5aa-a094-84b8d4f3e8ad");
        public static readonly Guid V2WaveA = Guid.Parse("955a1506-0fe2-f5aa-a094-84b8d4f3e8ad");
        public static readonly Guid V2WaveB = Guid.Parse("955a1505-0fe2-f5aa-a094-84b8d4f3e8ad");

        public static readonly Guid V3PulseService = Uuid16(0x180C);
        public static readonly Guid V3Write = Uuid16(0x150A);
        public static readonly Guid V3Notify = Uuid16(0x150B);

        private static Guid Uuid16(int value)
        {
            return Guid.Parse("0000" + value.ToString("x4") + "-0000-1000-8000-00805f9b34fb");
        }
    }
}
