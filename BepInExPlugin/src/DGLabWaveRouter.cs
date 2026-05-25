using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace DGLab.BepInEx
{
    public sealed class DGLabWaveRouter
    {
        public delegate string[] WaveSelector(string eventKey, string[] defaultWave);

        private readonly ManualLogSource _log;
        private readonly DGLabClient _client;
        private readonly DGLabOutputState _state;
        private readonly WaveSelector _waveSelector;
        private readonly Dictionary<string, string[]> _eventWaves = new Dictionary<string, string[]>();
        private readonly Dictionary<string, ConfigEntry<bool>> _eventEnabled = new Dictionary<string, ConfigEntry<bool>>();
        private readonly Dictionary<string, ConfigEntry<int>> _eventDuration = new Dictionary<string, ConfigEntry<int>>();
        private readonly Dictionary<string, ConfigEntry<int>> _eventCooldown = new Dictionary<string, ConfigEntry<int>>();
        private readonly Dictionary<string, float> _lastEventTime = new Dictionary<string, float>();
        private readonly DGLabPersistentOutput _persistent;

        public DGLabWaveRouter(ManualLogSource log, DGLabClient client, DGLabPersistentOutput persistent, WaveSelector waveSelector = null, DGLabOutputState state = null)
        {
            _log = log;
            _client = client;
            _persistent = persistent;
            _waveSelector = waveSelector;
            _state = state;
        }

        public void RegisterEvent(
            string eventKey,
            string[] defaultWave,
            ConfigEntry<bool> enabled,
            ConfigEntry<int> durationSeconds,
            ConfigEntry<int> cooldownSeconds)
        {
            _eventWaves[eventKey] = defaultWave;
            _eventEnabled[eventKey] = enabled;
            _eventDuration[eventKey] = durationSeconds;
            _eventCooldown[eventKey] = cooldownSeconds;
        }

        public bool TriggerEvent(string eventKey)
        {
            if (!_eventEnabled.TryGetValue(eventKey, out var enabled) || !enabled.Value)
            {
                return false;
            }

            var now = UnityEngine.Time.time;
            if (_eventCooldown.TryGetValue(eventKey, out var cooldown))
            {
                if (_lastEventTime.TryGetValue(eventKey, out var lastTime) && now - lastTime < cooldown.Value)
                {
                    return true;
                }
            }

            if (!_eventWaves.TryGetValue(eventKey, out var wave))
            {
                return false;
            }

            _lastEventTime[eventKey] = now;

            if (_eventDuration.TryGetValue(eventKey, out var duration))
            {
                var selectedWave = _waveSelector != null ? _waveSelector(eventKey, wave) : wave;
                _state?.SetWave(eventKey, selectedWave == wave ? "default" : "timed");
                if (_client != null && _client.HasTarget)
                {
                    _client.SendWaveA(selectedWave, duration.Value);
                }
                return true;
            }

            return false;
        }

        public void StartPersistent(string stateKey, string[] wave, int durationSeconds)
        {
            _persistent?.Start(stateKey, wave, durationSeconds);
        }

        public void StopPersistent(string stateKey)
        {
            _persistent?.Stop(stateKey);
        }
    }
}
