using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DGLab.BepInEx
{
    public static class DamageHooks
    {
        private static ManualLogSource _log;
        private static volatile DGLabClient _client;
        private static ConfigEntry<float> _minDamage;
        private static ConfigEntry<float> _damageCooldownSeconds;
        private static ConfigEntry<float> _impactMinForce;
        private static ConfigEntry<float> _impactCooldownSeconds;
        private static ConfigEntry<int> _breakBoneIntensity;
        private static ConfigEntry<int> _dislocateIntensity;
        private static ConfigEntry<int> _dismemberIntensity;
        private static ConfigEntry<int> _selfHarmIntensity;
        private static volatile DGLabWaveRouter _waveRouter;
        private static volatile DGLabOutputState _state;
        private static volatile DGLabStrengthEnvelope _strengthEnvelope;
        private static readonly Dictionary<string, float> _lastTriggerByKey = new Dictionary<string, float>();

        public static void Initialize(
            ManualLogSource log,
            DGLabClient client,
            ConfigEntry<float> minDamage,
            ConfigEntry<float> damageCooldownSeconds,
            ConfigEntry<float> impactMinForce,
            ConfigEntry<float> impactCooldownSeconds,
            ConfigEntry<int> breakBoneIntensity,
            ConfigEntry<int> dislocateIntensity,
            ConfigEntry<int> dismemberIntensity,
            ConfigEntry<int> selfHarmIntensity,
            DGLabWaveRouter waveRouter,
            DGLabOutputState state,
            DGLabStrengthEnvelope strengthEnvelope)
        {
            _log = log;
            _minDamage = minDamage;
            _damageCooldownSeconds = damageCooldownSeconds;
            _impactMinForce = impactMinForce;
            _impactCooldownSeconds = impactCooldownSeconds;
            _breakBoneIntensity = breakBoneIntensity;
            _dislocateIntensity = dislocateIntensity;
            _dismemberIntensity = dismemberIntensity;
            _selfHarmIntensity = selfHarmIntensity;
            UpdateContext(client, waveRouter, state, strengthEnvelope);
            _lastTriggerByKey.Clear();
        }

        public static void UpdateContext(
            DGLabClient client,
            DGLabWaveRouter waveRouter,
            DGLabOutputState state,
            DGLabStrengthEnvelope strengthEnvelope)
        {
            _client = client;
            _waveRouter = waveRouter;
            _state = state;
            _strengthEnvelope = strengthEnvelope;
        }

        internal static bool CanTrigger(string key, float cooldownSeconds)
        {
            var now = UnityEngine.Time.time;
            if (_lastTriggerByKey.TryGetValue(key, out var lastTime) && now - lastTime < cooldownSeconds)
            {
                return false;
            }

            _lastTriggerByKey[key] = now;
            return true;
        }

        internal static void TriggerIntensity(int intensity, float severity = 1f)
        {
            var maxRatio = Math.Min(1f, Math.Max(0f, intensity / 200f));
            var ratio = ScaleSeverity(maxRatio * Math.Min(1f, Math.Max(0f, severity)));
            _log?.LogInfo("DG-Lab event strength trigger: intensity=" + intensity + ", severity=" + severity.ToString("0.00") + ", ratio=" + ratio.ToString("0.00"));
            _strengthEnvelope?.TriggerSpikeBoth(ratio, "event");
        }

        private static void PushCondition(string condition)
        {
            _state?.PushInstantCondition(condition);
        }

        private static float ScaleSeverity(float value)
        {
            value = Math.Min(1f, Math.Max(0f, value));
            return Math.Min(1f, 0.12f + value * value * 0.88f);
        }

        internal static void TriggerCoilShock()
        {
            if (!CanTrigger("coil", 0.5f)) return;
            _log?.LogInfo("DG-Lab coil shock detected.");
            TriggerIntensity(200, 1f);
            PushCondition("shock+nerve");
            _waveRouter?.TriggerEvent("shock");
        }

        public static void TriggerBodyStateSpike(string key, float severity)
        {
            if (!CanTrigger(key, 1.25f)) return;
            severity = Math.Min(1f, Math.Max(0f, severity));
            _log?.LogInfo("DG-Lab body state spike: " + key + ", severity=" + severity.ToString("0.00"));
            PushCondition(key == "body-shock" ? "shock" : key == "body-consciousness" ? "nerve" : key);
            _strengthEnvelope?.TriggerSpikeBoth(severity, key);
            _waveRouter?.TriggerEvent("shock");
        }

        internal static void TriggerDamage(float damage)
        {
            if (damage < _minDamage.Value)
            {
                return;
            }

            if (!CanTrigger("damage", _damageCooldownSeconds.Value))
            {
                return;
            }

            TriggerIntensity(_breakBoneIntensity.Value, damage / 18f);
            PushCondition("pain+injury");
            if (_waveRouter != null && _waveRouter.TriggerEvent("damage"))
            {
                return;
            }
        }

        internal static void TriggerImpact(float force)
        {
            if (force < _impactMinForce.Value)
            {
                return;
            }

            if (!CanTrigger("impact", _impactCooldownSeconds.Value))
            {
                return;
            }

            TriggerIntensity(_dismemberIntensity.Value, force / 70f);
            PushCondition("impact+injury");
            if (_waveRouter != null && _waveRouter.TriggerEvent("impact"))
            {
                return;
            }
        }

        internal static void TriggerBreakBone()
        {
            if (!CanTrigger("break", 0.5f))
            {
                return;
            }

            TriggerIntensity(_breakBoneIntensity.Value);
            PushCondition("pain+injury");
            if (_waveRouter != null && _waveRouter.TriggerEvent("break"))
            {
                return;
            }
        }

        internal static void TriggerDislocate()
        {
            if (!CanTrigger("dislocate", 0.5f))
            {
                return;
            }

            TriggerIntensity(_dislocateIntensity.Value);
            PushCondition("pain+injury");
            if (_waveRouter != null && _waveRouter.TriggerEvent("dislocate"))
            {
                return;
            }
        }

        internal static void TriggerDismember()
        {
            if (!CanTrigger("dismember", 1.5f))
            {
                return;
            }

            TriggerIntensity(_dismemberIntensity.Value);
            PushCondition("injury+bleeding");
            if (_waveRouter != null && _waveRouter.TriggerEvent("dismember"))
            {
                return;
            }
        }

        internal static void TriggerSelfHarm()
        {
            if (!CanTrigger("selfharm", 2.0f))
            {
                return;
            }

            TriggerIntensity(_selfHarmIntensity.Value);
            PushCondition("pain+injury");
            if (_waveRouter != null && _waveRouter.TriggerEvent("selfharm"))
            {
                return;
            }
        }

        internal static void TriggerTreatmentSting(float severity)
        {
            if (!CanTrigger("treatment-sting", 0.35f))
            {
                return;
            }

            severity = Mathf.Clamp01(severity);
            _log?.LogInfo("DG-Lab treatment sting detected, severity=" + severity.ToString("0.00"));
            _strengthEnvelope?.TriggerSpikeBoth(ScaleSeverity(0.45f + severity * 0.35f), "treatment-sting");
            PushCondition("pain");
            _waveRouter?.TriggerEvent("treatment-sting");
        }
    }

    [HarmonyPatch(typeof(Damageable), "Damage")]
    internal static class Damageable_Damage_Patch
    {
        private static void Postfix(float damage)
        {
            DamageHooks.TriggerDamage(damage);
        }
    }

    [HarmonyPatch(typeof(Limb), "ImpactDamage")]
    internal static class Limb_ImpactDamage_Patch
    {
        private static void Postfix(float force)
        {
            DamageHooks.TriggerImpact(force);
        }
    }

    [HarmonyPatch(typeof(Limb), "BreakBone")]
    internal static class Limb_BreakBone_Patch
    {
        private static void Postfix()
        {
            DamageHooks.TriggerBreakBone();
        }
    }

    [HarmonyPatch(typeof(Limb), "Dislocate")]
    internal static class Limb_Dislocate_Patch
    {
        private static void Postfix()
        {
            DamageHooks.TriggerDislocate();
        }
    }

    [HarmonyPatch(typeof(Limb), "Dismember")]
    internal static class Limb_Dismember_Patch
    {
        private static void Postfix()
        {
            DamageHooks.TriggerDismember();
        }
    }

    [HarmonyPatch(typeof(SelfHarmer), "AttemptHarm")]
    internal static class SelfHarmer_AttemptHarm_Patch
    {
        private static void Prefix()
        {
            DamageHooks.TriggerSelfHarm();
        }
    }

    [HarmonyPatch(typeof(CoilScript), "Shock")]
    internal static class CoilScript_Shock_Patch
    {
        private static void Postfix()
        {
            DamageHooks.TriggerCoilShock();
        }
    }

    [HarmonyPatch(typeof(CoilScript), "OnCollisionEnter2D")]
    internal static class CoilScript_OnCollisionEnter2D_Patch
    {
        private static void Prefix(CoilScript __instance, Collision2D collision)
        {
            if (__instance == null || collision == null || __instance.cooldown > 0f) return;
            var collider = collision.collider;
            if (collider == null) return;

            if (collider.GetComponent<Body>() != null || collider.GetComponent<Limb>() != null)
            {
                DamageHooks.TriggerCoilShock();
            }
        }
    }

    [HarmonyPatch(typeof(ShrapnelMinigame), "BreakGrasp")]
    internal static class ShrapnelMinigame_BreakGrasp_Patch
    {
        private static void Postfix()
        {
            DamageHooks.TriggerTreatmentSting(1f);
        }
    }

    [HarmonyPatch(typeof(DislocationMinigame), "CheckForHit")]
    internal static class DislocationMinigame_CheckForHit_Patch
    {
        private static void Prefix(DislocationMinigame __instance, out float __state)
        {
            __state = TryReadLimbPain(__instance);
        }

        private static void Postfix(DislocationMinigame __instance, float __state)
        {
            var after = TryReadLimbPain(__instance);
            if (after > __state + 2f) DamageHooks.TriggerTreatmentSting(Mathf.Clamp01((after - __state) / 24f));
        }

        private static float TryReadLimbPain(DislocationMinigame minigame)
        {
            if (minigame == null) return 0f;
            var field = AccessTools.Field(typeof(DislocationMinigame), "limb");
            var limb = field != null ? field.GetValue(minigame) as Limb : null;
            return limb != null ? limb.pain : 0f;
        }
    }
}
