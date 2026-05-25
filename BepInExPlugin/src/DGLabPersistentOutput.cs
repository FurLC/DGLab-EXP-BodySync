using System.Collections.Generic;
namespace DGLab.BepInEx
{
    public sealed class DGLabPersistentOutput
    {
        public delegate string[] WaveSelector(string stateKey, string[] defaultWave);

        private readonly DGLabClient _client;
        private readonly WaveSelector _waveSelector;
        private readonly DGLabOutputState _state;
        private readonly Dictionary<string, PersistentState> _states = new Dictionary<string, PersistentState>();

        private sealed class PersistentState
        {
            public string[] Wave;
            public int DurationSeconds;
            public float NextSendTime;
            public bool Active;
        }

        public DGLabPersistentOutput(DGLabClient client, WaveSelector waveSelector = null, DGLabOutputState state = null)
        {
            _client = client;
            _waveSelector = waveSelector;
            _state = state;
        }

        public void Start(string key, string[] wave, int durationSeconds)
        {
            if (wave == null || wave.Length == 0 || durationSeconds <= 0)
            {
                return;
            }

            _states[key] = new PersistentState
            {
                Wave = wave,
                DurationSeconds = durationSeconds,
                NextSendTime = 0f,
                Active = true
            };
        }

        public void Stop(string key)
        {
            if (_states.TryGetValue(key, out var state))
            {
                state.Active = false;
            }
        }

        public void Tick()
        {
            var now = UnityEngine.Time.time;
            foreach (var kvp in _states)
            {
                var state = kvp.Value;
                if (!state.Active)
                {
                    continue;
                }

                if (now < state.NextSendTime)
                {
                    continue;
                }

                var selectedWave = _waveSelector != null ? _waveSelector(kvp.Key, state.Wave) : state.Wave;
                _state?.SetWave(kvp.Key, selectedWave == state.Wave ? "default" : "timed");
                if (_client != null && _client.HasTarget)
                {
                    _client.SendWaveA(selectedWave, state.DurationSeconds);
                }
                state.NextSendTime = now + state.DurationSeconds;
            }
        }
    }
}
