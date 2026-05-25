using System;

namespace DGLab.BepInEx
{
    public sealed class DGLabOutputState
    {
        private const float InstantConditionSeconds = 2.5f;

        public int StrengthA { get; private set; }
        public int StrengthB { get; private set; }
        public int RuntimeStrengthA => StrengthA;
        public int RuntimeStrengthB => StrengthB;
        public string LastEvent { get; private set; } = "idle";
        public string LastWave { get; private set; } = "none";
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

        public void SetWave(string eventKey, string profile)
        {
            LastEvent = string.IsNullOrEmpty(eventKey) ? "wave" : eventKey;
            LastWave = string.IsNullOrEmpty(profile) ? "default" : profile;
            LastUpdateTime = UnityEngine.Time.time;
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
            LastEvent = isInactive || IsClearReason(reason) || string.IsNullOrEmpty(reason) ? "none" : reason;
            LastWave = "none";
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
