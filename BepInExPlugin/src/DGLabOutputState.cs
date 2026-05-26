using System;

namespace DGLab.BepInEx
{
    public sealed class DGLabOutputState
    {
        private const float InstantConditionSeconds = 2.5f;

        public int StrengthA { get; private set; }
        public int StrengthB { get; private set; }
        public int DeviceStrengthA { get; private set; }
        public int DeviceStrengthB { get; private set; }
        public int DeviceLimitA { get; private set; } = 200;
        public int DeviceLimitB { get; private set; } = 200;
        public bool HasDeviceStrengthState { get; private set; }
        public int RuntimeStrengthA => StrengthA;
        public int RuntimeStrengthB => StrengthB;
        public string LastEvent { get; private set; } = "idle";
        public string LastWave { get; private set; } = "none";
        public string LastWaveSource { get; private set; } = "none";
        public int LastWaveDurationSeconds { get; private set; }
        public string[] LastWaveFrames { get; private set; }
        public string LastWaveA { get; private set; } = "none";
        public string LastWaveSourceA { get; private set; } = "none";
        public int LastWaveDurationSecondsA { get; private set; }
        public string[] LastWaveFramesA { get; private set; }
        public string LastWaveB { get; private set; } = "none";
        public string LastWaveSourceB { get; private set; } = "none";
        public int LastWaveDurationSecondsB { get; private set; }
        public string[] LastWaveFramesB { get; private set; }
        public string ActiveConditions { get; private set; } = "none";
        public string OutputConditionsA { get; private set; } = "none";
        public string OutputConditionsB { get; private set; } = "none";
        private string PersistentConditions { get; set; } = "none";
        private string InstantConditions { get; set; } = string.Empty;
        private float InstantConditionsExpireTime { get; set; }
        public float LastUpdateTime { get; private set; }

        public void SetStrength(int channel, int value, string reason)
        {
            var clamped = Math.Min(200, Math.Max(0, value));
            if (channel == 2) StrengthB = clamped;
            else StrengthA = clamped;
            if (IsClearReason(reason))
            {
                LastEvent = "none";
            }
            else if (!string.Equals(reason, "decay", StringComparison.OrdinalIgnoreCase) || clamped > 0)
            {
                LastEvent = string.IsNullOrEmpty(reason) ? "strength" : reason;
            }
            LastUpdateTime = UnityEngine.Time.time;
        }

        public void SetDeviceStrengthState(int strengthA, int strengthB, int limitA, int limitB)
        {
            DeviceStrengthA = Math.Min(200, Math.Max(0, strengthA));
            DeviceStrengthB = Math.Min(200, Math.Max(0, strengthB));
            DeviceLimitA = Math.Min(200, Math.Max(0, limitA));
            DeviceLimitB = Math.Min(200, Math.Max(0, limitB));
            HasDeviceStrengthState = true;
            LastUpdateTime = UnityEngine.Time.time;
        }

        public void SetWave(string eventKey, string profile)
        {
            LastEvent = string.IsNullOrEmpty(eventKey) ? "wave" : eventKey;
            LastWave = string.IsNullOrEmpty(profile) ? "default" : profile;
            LastUpdateTime = UnityEngine.Time.time;
        }

        public void SetWave(string eventKey, string profile, string[] frames, int durationSeconds)
        {
            SetWave(eventKey, profile);
            LastWaveSource = LastEvent;
            LastWaveDurationSeconds = Math.Max(0, durationSeconds);
            LastWaveFrames = frames != null ? (string[])frames.Clone() : null;
        }

        public void SetWave(int channel, string eventKey, string profile, string[] frames, int durationSeconds)
        {
            SetWave(eventKey, profile, frames, durationSeconds);
            var source = LastWaveSource;
            var wave = LastWave;
            var duration = LastWaveDurationSeconds;
            var copy = frames != null ? (string[])frames.Clone() : null;
            if (channel == 2)
            {
                LastWaveSourceB = source;
                LastWaveB = wave;
                LastWaveDurationSecondsB = duration;
                LastWaveFramesB = copy;
            }
            else
            {
                LastWaveSourceA = source;
                LastWaveA = wave;
                LastWaveDurationSecondsA = duration;
                LastWaveFramesA = copy;
            }
        }

        public void SetOutputConditions(string conditionsA, string conditionsB)
        {
            OutputConditionsA = NormalizeConditions(conditionsA);
            OutputConditionsB = NormalizeConditions(conditionsB);
            LastUpdateTime = UnityEngine.Time.time;
        }

        public void SetConditions(string conditions)
        {
            conditions = string.IsNullOrEmpty(conditions) ? "none" : conditions;
            PersistentConditions = conditions;
            if (!string.IsNullOrEmpty(InstantConditions) && UnityEngine.Time.time <= InstantConditionsExpireTime)
            {
                conditions = conditions == "none" ? InstantConditions : MergeConditions(InstantConditions, conditions);
            }
            ActiveConditions = conditions;
            LastUpdateTime = UnityEngine.Time.time;
        }

        public void PushInstantCondition(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition)) return;

            InstantConditions = string.IsNullOrEmpty(InstantConditions) ? condition.Trim() : MergeConditions(InstantConditions, condition.Trim());
            InstantConditionsExpireTime = UnityEngine.Time.time + InstantConditionSeconds;
            SetConditions(PersistentConditions);
        }

        public void Reset(string reason)
        {
            var isInactive = IsInactiveReason(reason);
            StrengthA = 0;
            StrengthB = 0;
            DeviceStrengthA = 0;
            DeviceStrengthB = 0;
            HasDeviceStrengthState = false;
            LastEvent = isInactive || IsClearReason(reason) || string.IsNullOrEmpty(reason) ? "none" : reason;
            LastWave = "none";
            LastWaveSource = "none";
            LastWaveDurationSeconds = 0;
            LastWaveFrames = null;
            LastWaveA = "none";
            LastWaveSourceA = "none";
            LastWaveDurationSecondsA = 0;
            LastWaveFramesA = null;
            LastWaveB = "none";
            LastWaveSourceB = "none";
            LastWaveDurationSecondsB = 0;
            LastWaveFramesB = null;
            ActiveConditions = isInactive || IsClearReason(reason) || string.IsNullOrEmpty(reason) ? "none" : reason;
            OutputConditionsA = "none";
            OutputConditionsB = "none";
            PersistentConditions = ActiveConditions;
            InstantConditions = string.Empty;
            InstantConditionsExpireTime = 0f;
            LastUpdateTime = UnityEngine.Time.time;
        }

        private static string NormalizeConditions(string conditions)
        {
            return string.IsNullOrWhiteSpace(conditions) ? "none" : conditions.Trim();
        }

        private static bool IsClearReason(string reason)
        {
            return string.Equals(reason, "condition-clear", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "body-init", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "baseline", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInactiveReason(string reason)
        {
            return string.Equals(reason, "no-body", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "no-limbs", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "dead", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(reason, "no-world", StringComparison.OrdinalIgnoreCase);
        }

        private static string MergeConditions(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || first == "none") return string.IsNullOrWhiteSpace(second) ? "none" : second;
            if (string.IsNullOrWhiteSpace(second) || second == "none") return first;

            var merged = first;
            var parts = second.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (part.Length == 0) continue;
                if (ContainsCondition(merged, part)) continue;
                merged += "+" + part;
            }
            return merged;
        }

        private static bool ContainsCondition(string conditions, string condition)
        {
            var parts = conditions.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.Equals(parts[i].Trim(), condition, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
