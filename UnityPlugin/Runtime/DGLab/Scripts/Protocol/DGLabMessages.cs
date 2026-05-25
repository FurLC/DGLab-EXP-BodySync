using System;
using System.Collections.Generic;

namespace DGLab.Protocol
{
    [Serializable]
    public sealed class DGLabMessage
    {
        public string type;
        public string clientId;
        public string targetId;
        public string message;

        public static DGLabMessage Create(string type, string clientId, string targetId, string message)
        {
            return new DGLabMessage
            {
                type = type,
                clientId = clientId,
                targetId = targetId,
                message = message
            };
        }
    }

    [Serializable]
    public sealed class ClientStrengthMessage
    {
        public int type = 3;
        public int channel = 1;
        public int strength = 0;
        public string message = "set channel";
        public string clientId;
        public string targetId;
    }

    [Serializable]
    public sealed class ClientDeltaStrengthMessage
    {
        public int type = 2;
        public int channel = 1;
        public string message = "set channel";
        public string clientId;
        public string targetId;
    }

    [Serializable]
    public sealed class ClientRawMessage
    {
        public int type = 4;
        public string message;
        public string clientId;
        public string targetId;
    }

    [Serializable]
    public sealed class ClientWaveMessage
    {
        public string type = "clientMsg";
        public string channel = "A";
        public int time = 5;
        public string message;
        public string clientId;
        public string targetId;
    }

    public static class DGLabProtocol
    {
        public const int ChannelA = 1;
        public const int ChannelB = 2;

        public static string BuildWavePayload(string channel, IList<string> hexSegments)
        {
            var joined = string.Join("\",\"", hexSegments);
            return string.Format("{0}:[\"{1}\"]", channel, joined);
        }
    }
}
