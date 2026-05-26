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
        private static Func<string> _channelABindingProvider;
        private static Func<string> _channelBBindingProvider;
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
            DGLabStrengthEnvelope strengthEnvelope,
            Func<string> channelABindingProvider,
            Func<string> channelBBindingProvider)
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
            _channelABindingProvider = channelABindingProvider;
            _channelBBindingProvider = channelBBindingProvider;
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

        // Realistic pain ratio table per event (floor / ceiling), tuned to feel
        // proportional to real-world severity. Each event maps the configured
        // intensity (0..200) and runtime severity (0..1) into the [floor, ceil]
        // range using a smooth curve so mid-severity feels mid, not maxed.
        private static float MapEventRatio(string key, int intensity, float severity)
        {
            severity = Mathf.Clamp01(severity);
            var intensityRatio = Mathf.Clamp01(intensity / 200f);
            var blend = severity * 0.7f + intensityRatio * 0.3f;
            var shaped = Mathf.Pow(Mathf.Clamp01(blend), 1.6f);

            float floor;
            float ceil;
            switch (key)
            {
                case "coil":             floor = 0.75f; ceil = 1.00f; break; // electric shock, can max
                case "dismember":        floor = 0.74f; ceil = 1.00f; break; // limb removal, peak event
                case "break":            floor = 0.48f; ceil = 0.78f; break; // sharp fracture pain
                case "selfharm":         floor = 0.35f; ceil = 0.62f; break; // intentional cut, sharp but not max
                case "dislocate":        floor = 0.38f; ceil = 0.68f; break; // joint pop, very painful
                case "impact":           floor = 0.12f; ceil = 0.58f; break; // blunt force
                case "damage":           floor = 0.10f; ceil = 0.62f; break; // generic hit
                case "treatment-sting":  floor = 0.08f; ceil = 0.28f; break; // medical procedure pain
                default:                 floor = 0.10f; ceil = 0.62f; break;
            }
            return Mathf.Clamp01(floor + (ceil - floor) * shaped);
        }

        internal static void TriggerEventRatio(string key, int intensity, float severity)
        {
            var ratio = MapEventRatio(key, intensity, severity);
            _log?.LogInfo("DG-Lab event strength trigger: key=" + key + ", intensity=" + intensity + ", severity=" + severity.ToString("0.00") + ", ratio=" + ratio.ToString("0.00"));
            _strengthEnvelope?.TriggerSpikeBoth(ratio, key);
        }

        internal static void TriggerEventRatioForLimb(string key, int intensity, float severity, Limb limb)
        {
            var ratio = MapEventRatio(key, intensity, severity);
            var channelMask = ChannelMaskForLimb(limb);
            _log?.LogInfo("DG-Lab limb event strength trigger: key=" + key + ", intensity=" + intensity + ", severity=" + severity.ToString("0.00") + ", ratio=" + ratio.ToString("0.00") + ", channels=" + ChannelMaskText(channelMask) + ", limb=" + LimbLabel(limb));
            if ((channelMask & 1) != 0) _strengthEnvelope?.TriggerSpike(1, ratio, key);
            if ((channelMask & 2) != 0) _strengthEnvelope?.TriggerSpike(2, ratio, key);
        }

        private static int ChannelMaskForLimb(Limb limb)
        {
            if (limb == null || limb.body == null || limb.body.limbs == null) return 3;
            var index = Array.IndexOf(limb.body.limbs, limb);
            if (index < 0) return 3;
            var mask = 0;
            if (DGLabBodyBinding.IsLimbBound(_channelABindingProvider != null ? _channelABindingProvider() : null, index)) mask |= 1;
            if (DGLabBodyBinding.IsLimbBound(_channelBBindingProvider != null ? _channelBBindingProvider() : null, index)) mask |= 2;
            return mask != 0 ? mask : 3;
        }

        private static string ChannelMaskText(int mask)
        {
            if ((mask & 3) == 3) return "A+B";
            if ((mask & 1) != 0) return "A";
            if ((mask & 2) != 0) return "B";
            return "none";
        }

        private static string LimbLabel(Limb limb)
        {
            if (limb == null) return "unknown";
            if (!string.IsNullOrEmpty(limb.shortName)) return limb.shortName;
            if (!string.IsNullOrEmpty(limb.fullName)) return limb.fullName;
            return limb.name;
        }

        private static void PushCondition(string condition)
        {
            _state?.PushInstantCondition(condition);
        }

        internal static void TriggerCoilShock()
        {
            if (!CanTrigger("coil", 0.5f)) return;
            _log?.LogInfo("DG-Lab coil shock detected.");
            TriggerEventRatio("coil", 200, 1f);
            PushCondition("shock+nerve");
            _waveRouter?.TriggerEvent("shock", 3);
        }

        // Body-state spikes ramp gradually: low severity stays low so creeping
        // shock or fading consciousness feels like a build-up, not a slam.
        public static void TriggerBodyStateSpike(string key, float severity)
        {
            if (!CanTrigger(key, 1.25f)) return;
            severity = Mathf.Clamp01(severity);
            float floor;
            float ceil;
            switch (key)
            {
                case "body-shock":         floor = 0.14f; ceil = 0.76f; break;
                case "body-consciousness": floor = 0.10f; ceil = 0.62f; break;
                default:                   floor = 0.10f; ceil = 0.70f; break;
            }
            var ratio = Mathf.Clamp01(floor + (ceil - floor) * Mathf.Pow(severity, 1.4f));
            _log?.LogInfo("DG-Lab body state spike: " + key + ", severity=" + severity.ToString("0.00") + ", ratio=" + ratio.ToString("0.00"));
            PushCondition(key == "body-shock" ? "shock" : key == "body-consciousness" ? "nerve" : key);
            _strengthEnvelope?.TriggerSpikeBoth(ratio, key);
            _waveRouter?.TriggerEvent("shock", 3);
        }

        internal static void TriggerDamage(float damage, Limb limb)
        {
            if (damage < _minDamage.Value) return;
            if (!CanTrigger("damage", _damageCooldownSeconds.Value)) return;

            // Map raw damage 1..30 into severity 0..1 with a soft top
            var sev = Mathf.Clamp01(damage / 30f);
            TriggerEventRatioForLimb("damage", 115, sev, limb);
            PushCondition("pain+injury");
            _waveRouter?.TriggerEvent("damage", ChannelMaskForLimb(limb));
        }

        internal static void TriggerImpact(float force, Limb limb)
        {
            if (force < _impactMinForce.Value) return;
            if (!CanTrigger("impact", _impactCooldownSeconds.Value)) return;

            // Most blunt hits sit in 8..45 range; map to 0..1
            var sev = Mathf.Clamp01(force / 45f);
            TriggerEventRatioForLimb("impact", 130, sev, limb);
            PushCondition("impact+injury");
            _waveRouter?.TriggerEvent("impact", ChannelMaskForLimb(limb));
        }

        internal static void TriggerBreakBone(Limb limb)
        {
            if (!CanTrigger("break", 0.5f)) return;
            TriggerEventRatioForLimb("break", _breakBoneIntensity.Value, 0.85f, limb);
            PushCondition("pain+injury");
            _waveRouter?.TriggerEvent("break", ChannelMaskForLimb(limb));
        }

        internal static void TriggerDislocate(Limb limb)
        {
            if (!CanTrigger("dislocate", 0.5f)) return;
            TriggerEventRatioForLimb("dislocate", _dislocateIntensity.Value, 0.85f, limb);
            PushCondition("pain+injury");
            _waveRouter?.TriggerEvent("dislocate", ChannelMaskForLimb(limb));
        }

        internal static void TriggerDismember(Limb limb)
        {
            if (!CanTrigger("dismember", 1.5f)) return;
            TriggerEventRatioForLimb("dismember", _dismemberIntensity.Value, 1f, limb);
            PushCondition("injury+bleeding");
            _waveRouter?.TriggerEvent("dismember", ChannelMaskForLimb(limb));
        }

        internal static void TriggerSelfHarm()
        {
            if (!CanTrigger("selfharm", 2.0f)) return;
            TriggerEventRatio("selfharm", _selfHarmIntensity.Value, 0.85f);
            PushCondition("pain+injury");
            _waveRouter?.TriggerEvent("selfharm", 3);
        }

        internal static void TriggerTreatmentSting(float severity, Limb limb = null)
        {
            if (!CanTrigger("treatment-sting", 0.35f)) return;
            severity = Mathf.Clamp01(severity);
            _log?.LogInfo("DG-Lab treatment sting detected, severity=" + severity.ToString("0.00"));
            TriggerEventRatioForLimb("treatment-sting", 90, severity, limb);
            PushCondition("pain");
            _waveRouter?.TriggerEvent("treatment-sting", ChannelMaskForLimb(limb));
        }
    }

    [HarmonyPatch(typeof(Damageable), "Damage")]
    internal static class Damageable_Damage_Patch
    {
        private static void Postfix(Damageable __instance, float damage)
        {
            var limb = __instance != null ? __instance.GetComponentInParent<Limb>() : null;
            if (limb != null) DamageHooks.TriggerDamage(damage, limb);
        }
    }

    [HarmonyPatch(typeof(Limb), "ImpactDamage")]
    internal static class Limb_ImpactDamage_Patch
    {
        private static void Postfix(Limb __instance, float force)
        {
            DamageHooks.TriggerImpact(force, __instance);
        }
    }

    [HarmonyPatch(typeof(Limb), "BreakBone")]
    internal static class Limb_BreakBone_Patch
    {
        private static void Postfix(Limb __instance)
        {
            DamageHooks.TriggerBreakBone(__instance);
        }
    }

    [HarmonyPatch(typeof(Limb), "Dislocate")]
    internal static class Limb_Dislocate_Patch
    {
        private static void Postfix(Limb __instance)
        {
            DamageHooks.TriggerDislocate(__instance);
        }
    }

    [HarmonyPatch(typeof(Limb), "Dismember")]
    internal static class Limb_Dismember_Patch
    {
        private static void Postfix(Limb __instance)
        {
            DamageHooks.TriggerDismember(__instance);
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
            if (after > __state + 2f) DamageHooks.TriggerTreatmentSting(Mathf.Clamp01((after - __state) / 24f), TryReadLimb(__instance));
        }

        private static float TryReadLimbPain(DislocationMinigame minigame)
        {
            var limb = TryReadLimb(minigame);
            return limb != null ? limb.pain : 0f;
        }

        private static Limb TryReadLimb(DislocationMinigame minigame)
        {
            if (minigame == null) return null;
            var field = AccessTools.Field(typeof(DislocationMinigame), "limb");
            return field != null ? field.GetValue(minigame) as Limb : null;
        }
    }
}
