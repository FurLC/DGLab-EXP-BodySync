using System;
using UnityEngine;

namespace DGLab.BepInEx
{
    public sealed class DGLabStrengthEnvelope
    {
        private const float RisePerSecond = 2.2f;
        private const float DecayPerSecond = 0.32f;
        private const float SendIntervalSeconds = 0.2f;

        private readonly Func<DGLabClient> _clientProvider;
        private readonly Func<int> _maxStrengthAProvider;
        private readonly Func<int> _maxStrengthBProvider;
        private readonly DGLabOutputState _state;
        private float _currentRatioA;
        private float _currentRatioB;
        private float _sustainedRatioA;
        private float _sustainedRatioB;
        private float _lastTickTime;
        private float _nextSendTimeA;
        private float _nextSendTimeB;
        private int _lastSentStrengthA = -1;
        private int _lastSentStrengthB = -1;

        public float CurrentRatio => Mathf.Max(_currentRatioA, _currentRatioB);
        public float CurrentRatioA => _currentRatioA;
        public float CurrentRatioB => _currentRatioB;
        public float SustainedRatioA => _sustainedRatioA;
        public float SustainedRatioB => _sustainedRatioB;

        public DGLabStrengthEnvelope(Func<DGLabClient> clientProvider, Func<int> maxStrengthProvider, DGLabOutputState state)
            : this(clientProvider, maxStrengthProvider, maxStrengthProvider, state)
        {
        }

        public DGLabStrengthEnvelope(Func<DGLabClient> clientProvider, Func<int> maxStrengthAProvider, Func<int> maxStrengthBProvider, DGLabOutputState state)
        {
            _clientProvider = clientProvider;
            _maxStrengthAProvider = maxStrengthAProvider;
            _maxStrengthBProvider = maxStrengthBProvider;
            _state = state;
            _lastTickTime = Time.time;
        }

        public void TriggerSpike(float ratio, string reason)
        {
            TriggerSpike(1, ratio, reason);
        }

        public void TriggerSpikeBoth(float ratio, string reason)
        {
            TriggerSpike(1, ratio, reason);
            TriggerSpike(2, ratio, reason);
        }

        public void TriggerSpike(int channel, float ratio, string reason)
        {
            ratio = Mathf.Clamp01(ratio);
            if (ratio > 0f) ratio = Mathf.Max(0.08f, ratio);
            if (channel == 2) _currentRatioB = Mathf.Max(_currentRatioB, ratio);
            else _currentRatioA = Mathf.Max(_currentRatioA, ratio);
            Apply(channel, reason, force: true);
        }

        public void SetSustained(float ratio, string reason)
        {
            SetSustainedBoth(ratio, reason);
        }

        public void SetSustainedBoth(float ratio, string reason)
        {
            SetSustained(1, ratio, reason);
            SetSustained(2, ratio, reason);
        }

        public void SetSustained(int channel, float ratio, string reason)
        {
            ratio = Mathf.Clamp01(ratio);
            if (channel == 2)
            {
                _sustainedRatioB = ratio;
                if (_currentRatioB < _sustainedRatioB) _currentRatioB = _sustainedRatioB;
            }
            else
            {
                _sustainedRatioA = ratio;
                if (_currentRatioA < _sustainedRatioA) _currentRatioA = _sustainedRatioA;
            }

            Apply(channel, reason, force: false);
        }

        public void Clear(string reason)
        {
            _currentRatioA = 0f;
            _currentRatioB = 0f;
            _sustainedRatioA = 0f;
            _sustainedRatioB = 0f;
            Apply(1, reason, force: true);
            Apply(2, reason, force: true);
        }

        public void Tick()
        {
            var now = Time.time;
            var delta = Mathf.Max(0f, now - _lastTickTime);
            _lastTickTime = now;

            _currentRatioA = MoveRatio(_currentRatioA, _sustainedRatioA, delta);
            _currentRatioB = MoveRatio(_currentRatioB, _sustainedRatioB, delta);

            if (_currentRatioA <= 0f && _currentRatioB <= 0f && _sustainedRatioA <= 0f && _sustainedRatioB <= 0f)
            {
                return;
            }

            Apply(1, "decay", force: false);
            Apply(2, "decay", force: false);
        }

        private static float MoveRatio(float current, float target, float delta)
        {
            var speed = current < target ? RisePerSecond : DecayPerSecond;
            return Mathf.MoveTowards(current, target, speed * delta);
        }

        private void Apply(int channel, string reason, bool force)
        {
            var maxProvider = channel == 2 ? _maxStrengthBProvider : _maxStrengthAProvider;
            var ratio = channel == 2 ? _currentRatioB : _currentRatioA;
            var maxStrength = Mathf.Clamp(maxProvider != null ? maxProvider() : 0, 0, 200);
            var strength = Mathf.Clamp(Mathf.RoundToInt(maxStrength * ratio), 0, maxStrength);
            _state?.SetStrength(channel, strength, reason);

            var now = Time.time;
            var nextSendTime = channel == 2 ? _nextSendTimeB : _nextSendTimeA;
            var lastSentStrength = channel == 2 ? _lastSentStrengthB : _lastSentStrengthA;
            if (!force && now < nextSendTime && Mathf.Abs(strength - lastSentStrength) < 2) return;

            if (channel == 2)
            {
                _nextSendTimeB = now + SendIntervalSeconds;
                _lastSentStrengthB = strength;
            }
            else
            {
                _nextSendTimeA = now + SendIntervalSeconds;
                _lastSentStrengthA = strength;
            }

            var client = _clientProvider != null ? _clientProvider() : null;
            if (client == null || !client.HasTarget) return;
            if (channel == 2) client.SetStrengthB(strength);
            else client.SetStrengthA(strength);
        }
    }
}
