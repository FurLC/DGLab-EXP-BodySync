using System.Collections.Generic;
namespace DGLab.BepInEx
{
    public sealed class DGLabPersistentOutput
    {
        public delegate string[] WaveSelector(string stateKey, string[] defaultWave);
        public delegate bool ChannelEnabledProvider(int channel);

        private readonly DGLabClient _client;
        private readonly WaveSelector _waveSelector;
        private readonly ChannelEnabledProvider _channelEnabledProvider;
        private readonly DGLabOutputState _state;
        private readonly object _lock = new object();
        private readonly Dictionary<string, PersistentState> _states = new Dictionary<string, PersistentState>();

        private sealed class PersistentState
        {
            public string[] Wave;
            public int DurationSeconds;
            public float NextSendTime;
            public bool Active;
        }

        public DGLabPersistentOutput(DGLabClient client, WaveSelector waveSelector = null, DGLabOutputState state = null, ChannelEnabledProvider channelEnabledProvider = null)
        {
            _client = client;
            _waveSelector = waveSelector;
            _state = state;
            _channelEnabledProvider = channelEnabledProvider;
        }

        public void Start(string key, string[] wave, int durationSeconds)
        {
            if (wave == null || wave.Length == 0 || durationSeconds <= 0) return;
            lock (_lock)
            {
                _states[key] = new PersistentState
                {
                    Wave = wave,
                    DurationSeconds = durationSeconds,
                    NextSendTime = 0f,
                    Active = true
                };
            }
        }

        public void Stop(string key)
        {
            lock (_lock)
            {
                if (_states.TryGetValue(key, out var state)) state.Active = false;
            }
        }

        public void Tick()
        {
            var now = UnityEngine.Time.time;
            KeyValuePair<string, PersistentState>[] snapshot;
            lock (_lock)
            {
                snapshot = new KeyValuePair<string, PersistentState>[_states.Count];
                var idx = 0;
                foreach (var kvp in _states) snapshot[idx++] = kvp;
            }

            foreach (var kvp in snapshot)
            {
                var state = kvp.Value;
                if (!state.Active || now < state.NextSendTime) continue;

                var selectedWave = _waveSelector != null ? _waveSelector(kvp.Key, state.Wave) : state.Wave;
                if (_client != null && _client.HasTarget)
                {
                    if (IsChannelEnabled(1))
                    {
                        _state?.SetWave(1, kvp.Key, "default", selectedWave, state.DurationSeconds);
                        _client.SendWaveA(selectedWave, state.DurationSeconds);
                    }
                    if (IsChannelEnabled(2))
                    {
                        _state?.SetWave(2, kvp.Key, "default", selectedWave, state.DurationSeconds);
                        _client.SendWaveB(selectedWave, state.DurationSeconds);
                    }
                }
                state.NextSendTime = now + state.DurationSeconds;
            }
        }

        private bool IsChannelEnabled(int channel)
        {
            return _channelEnabledProvider == null || _channelEnabledProvider(channel);
        }
    }
}
