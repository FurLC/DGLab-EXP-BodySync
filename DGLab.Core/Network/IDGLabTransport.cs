using System;
using DGLab.BepInEx.Protocol;

namespace DGLab.BepInEx.Network
{
    internal interface IDGLabTransport : IDisposable
    {
        string ClientId { get; }
        string TargetId { get; }

        event Action<string> OnRawMessage;
        event Action<DGLabMessage> OnMessage;
        event Action OnConnected;
        event Action<string> OnClosed;
        event Action<Exception> OnError;

        void Connect();
        void Disconnect();
        void Send(object payload);
        void SendStrengthSet(int channel, int strength);
        void SendStrengthDelta(int channel, bool increase);
        void SendRawCommand(string command);
        void SendWave(string channel, int timeSeconds, string wavePayload);
    }
}
