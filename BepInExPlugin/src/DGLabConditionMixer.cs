using System.Collections.Generic;
using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace DGLab.BepInEx
{
    public sealed class DGLabConditionMixer
    {
        private const float SampleIntervalSeconds = 1f;
        private const int SendDurationSeconds = 2;

        private readonly ManualLogSource _log;
        private readonly DGLabClient _client;
        private readonly DGLabOutputState _state;
        private readonly DGLabStrengthEnvelope _strengthEnvelope;
        private readonly Func<string> _channelABindingProvider;
        private readonly Func<string> _channelBBindingProvider;
        private readonly Func<bool> _testLogEnabledProvider;
        private readonly Func<float> _testLogIntervalProvider;
        private readonly List<ConditionLayer> _layers = new List<ConditionLayer>();
        private readonly List<ConditionLayer> _channelALayers = new List<ConditionLayer>();
        private readonly List<ConditionLayer> _channelBLayers = new List<ConditionLayer>();
        private float _nextSampleTime;
        private float _nextSendTimeA;
        private float _nextSendTimeB;
        private string _lastSignature = string.Empty;
        private string _lastSignatureA = string.Empty;
        private string _lastSignatureB = string.Empty;
        private float _sustainedRatioA;
        private float _sustainedRatioB;
        private Body _lastBody;
        private int _stableSampleCount;
        private bool _baselineReady;
        private float _lastTestLogTime = -999f;

        private sealed class ConditionLayer
        {
            public string Key;
            public float Severity;
            public string[] Wave;
        }

        public DGLabConditionMixer(
            ManualLogSource log,
            DGLabClient client,
            DGLabOutputState state,
            DGLabStrengthEnvelope strengthEnvelope = null,
            Func<string> channelABindingProvider = null,
            Func<string> channelBBindingProvider = null,
            Func<bool> testLogEnabledProvider = null,
            Func<float> testLogIntervalProvider = null)
        {
            _log = log;
            _client = client;
            _state = state;
            _strengthEnvelope = strengthEnvelope;
            _channelABindingProvider = channelABindingProvider;
            _channelBBindingProvider = channelBBindingProvider;
            _testLogEnabledProvider = testLogEnabledProvider;
            _testLogIntervalProvider = testLogIntervalProvider;
        }

        public DGLabConditionMixer(DGLabClient client, DGLabOutputState state, DGLabStrengthEnvelope strengthEnvelope = null)
            : this(null, client, state, strengthEnvelope, null, null, null, null)
        {
        }

        public void Tick(Body body)
        {
            if (body == null) return;

            var now = Time.time;
            if (now < _nextSampleTime) return;
            _nextSampleTime = now + SampleIntervalSeconds;

            if (_lastBody != body)
            {
                _lastBody = body;
                _stableSampleCount = 0;
                _sustainedRatioA = 0f;
                _sustainedRatioB = 0f;
                _baselineReady = false;
                _strengthEnvelope?.SetSustained(1, 0f, "body-init");
                _strengthEnvelope?.SetSustained(2, 0f, "body-init");
                _state?.SetConditions("initializing");
                return;
            }

            _stableSampleCount++;
            if (_stableSampleCount < 2)
            {
                _state?.SetConditions("initializing");
                return;
            }

            Sample(body);
            if (!_baselineReady)
            {
                _baselineReady = true;
                _state?.SetConditions("baseline");
                _strengthEnvelope?.SetSustained(1, 0f, "baseline");
                _strengthEnvelope?.SetSustained(2, 0f, "baseline");
                LogTest("baseline established: " + BuildBodySnapshot(body));
                return;
            }

            if (_layers.Count == 0)
            {
                _state?.SetConditions("none");
                _state?.SetOutputConditions("none", "none");
                _strengthEnvelope?.SetSustained(1, 0f, "condition-clear");
                _strengthEnvelope?.SetSustained(2, 0f, "condition-clear");
                return;
            }

            var active = _layers.OrderByDescending(layer => layer.Severity).ToArray();
            var waveLayers = active.Take(4).ToArray();
            var signature = string.Join("+", active.Take(8).Select(layer => layer.Key).ToArray());
            var waveLayersA = BuildChannelWaveLayers(_channelALayers, active);
            var waveLayersB = BuildChannelWaveLayers(_channelBLayers, active);
            var signatureA = BuildSignature(waveLayersA);
            var signatureB = BuildSignature(waveLayersB);
            var mixedWaveA = Mix(waveLayersA);
            var mixedWaveB = Mix(waveLayersB);

            _strengthEnvelope?.SetSustained(1, _sustainedRatioA, "condition");
            _strengthEnvelope?.SetSustained(2, _sustainedRatioB, "condition");
            _state?.SetWave("condition", waveLayers.Length > 1 ? "mixed:" + string.Join("+", waveLayers.Select(layer => layer.Key).ToArray()) : signature);
            _state?.SetConditions(signature);
            _state?.SetOutputConditions(_sustainedRatioA > 0f ? signatureA : "none", _sustainedRatioB > 0f ? signatureB : "none");

            if (_client == null || !_client.HasTarget) return;

            if (now >= _nextSendTimeA || signatureA != _lastSignatureA || signature != _lastSignature)
            {
                _client.SendWaveA(mixedWaveA, SendDurationSeconds);
                _nextSendTimeA = now + SendDurationSeconds;
                _lastSignatureA = signatureA;
            }
            if (now >= _nextSendTimeB || signatureB != _lastSignatureB || signature != _lastSignature)
            {
                _client.SendWaveB(mixedWaveB, SendDurationSeconds);
                _nextSendTimeB = now + SendDurationSeconds;
                _lastSignatureB = signatureB;
            }
            _lastSignature = signature;
        }

        private void Sample(Body body)
        {
            _layers.Clear();
            _channelALayers.Clear();
            _channelBLayers.Clear();

            var maxLimbInfection = 0f;
            var maxLimbInjury = 0f;
            var maxLimbBleed = 0f;
            var maxLimbPain = 0f;
            var maxVitalPain = 0f;
            var channelARegionalSeverity = 0f;
            var channelBRegionalSeverity = 0f;
            var channelARegionalLabel = "none";
            var channelBRegionalLabel = "none";
            var brokenOrDislocated = 0f;
            var channelABinding = _channelABindingProvider != null ? _channelABindingProvider() : "Head,UpTorso,DownTorso,ArmF,ArmB";
            var channelBBinding = _channelBBindingProvider != null ? _channelBBindingProvider() : "LegF,LegB";
            if (body.limbs != null)
            {
                for (var i = 0; i < body.limbs.Length; i++)
                {
                    var limb = body.limbs[i];
                    if (limb == null || limb.dismembered) continue;

                    var skinHealth = ValidPercent(limb.skinHealth, 100f);
                    var muscleHealth = ValidPercent(limb.muscleHealth, 100f);
                    var pain = Mathf.Clamp01(Mathf.Max(ValidPercent(limb.pain, 0f) - body.curAdrenaline * 0.5f, 0f) / 100f);
                    var injury = Mathf.Clamp01(Mathf.Max(100f - skinHealth, 100f - muscleHealth) / 100f);
                    var bleed = Mathf.Clamp01(limb.totalBleedAmount / 25f);
                    var infection = Mathf.Clamp01(ValidPercent(limb.infectionAmount, 0f) / 100f);
                    var fracture = limb.broken ? 0.55f : 0f;
                    var dislocation = limb.dislocated ? 0.5f : 0f;
                    var structural = Mathf.Max(fracture, dislocation);
                    var nerve = (limb.strokeAffected && body.strokeAmount > 20f) || (muscleHealth > 0f && muscleHealth <= Limb.muscleDeathThreshold && pain > 0.2f) ? 0.55f : 0f;
                    var chronicInjury = Mathf.Clamp01(injury * 0.08f);
                    var limbSeverity = WeightedMax(
                        pain * 0.95f,
                        bleed * 0.78f,
                        infection * 0.35f,
                        structural * Mathf.Max(0.25f, pain),
                        nerve * Mathf.Max(0.2f, pain),
                        chronicInjury);

                    maxLimbInfection = Mathf.Max(maxLimbInfection, ValidPercent(limb.infectionAmount, 0f));
                    maxLimbInjury = Mathf.Max(maxLimbInjury, Mathf.Max(100f - skinHealth, 100f - muscleHealth));
                    maxLimbBleed = Mathf.Max(maxLimbBleed, limb.totalBleedAmount);
                    maxLimbPain = Mathf.Max(maxLimbPain, ValidPercent(limb.pain, 0f));
                    if (limb.isHead || limb.isVital || limb.isAbdomen) maxVitalPain = Mathf.Max(maxVitalPain, ValidPercent(limb.pain, 0f));
                    if (limb.broken || limb.dislocated) brokenOrDislocated = 1f;

                    var weightedSeverity = limbSeverity * LimbRegionWeight(i, limb);
                    var label = LimbLabel(i, limb) + ":p" + Percent(pain) + "/inj" + Percent(injury) + "/bleed" + Percent(bleed) + "/struct" + Percent(structural) + "/nerve" + Percent(nerve);
                    if (DGLabBodyBinding.IsLimbBound(channelABinding, i) && weightedSeverity > channelARegionalSeverity)
                    {
                        channelARegionalSeverity = weightedSeverity;
                        channelARegionalLabel = label;
                    }
                    if (DGLabBodyBinding.IsLimbBound(channelBBinding, i) && weightedSeverity > channelBRegionalSeverity)
                    {
                        channelBRegionalSeverity = weightedSeverity;
                        channelBRegionalLabel = label;
                    }

                    AddRegionalLayers(DGLabBodyBinding.IsLimbBound(channelABinding, i) ? _channelALayers : null, pain, injury, bleed, infection, fracture, dislocation, nerve);
                    AddRegionalLayers(DGLabBodyBinding.IsLimbBound(channelBBinding, i) ? _channelBLayers : null, pain, injury, bleed, infection, fracture, dislocation, nerve);
                }
            }

            var bloodOxygen = ValidPercent(body.bloodOxygen, 100f);
            var bloodVolume = ValidPercent(body.bloodVolume, 100f);
            var bloodPressure = body.bloodPressure > 0f ? body.bloodPressure : 120f;
            var respiratoryRate = body.respiratoryRate > 0f ? body.respiratoryRate : 100f;
            var temperature = body.temperature > 20f ? body.temperature : 37f;
            var hunger = ValidPercent(body.hunger, 100f);
            var thirst = body.thirst > 0f ? body.thirst : 100f;
            var stamina = ValidPercent(body.stamina, 100f);
            var energy = ValidPercent(body.energy, 100f);
            var brainHealth = ValidPercent(body.brainHealth, 100f);
            var consciousness = ValidPercent(body.consciousness, 100f);

            var totalPain = ValidPercent(body.averagePain, 0f);
            var painCap = PainOutputCap(totalPain);
            var painSeverity = painCap;
            var shockSeverity = ValidPercent(body.shock, 0f) / 55f;
            var painShockSeverity = Mathf.Clamp01(body.painShock) / 0.3f;
            var traumaSeverity = ThresholdSeverityHigh(ValidPercent(body.traumaAmount, 0f), 25f, 45f, 80f, 100f);
            var nerveSeverity = WeightedMax(
                ThresholdSeverityLow(consciousness, 90f, 72f, 55f, 30f),
                ThresholdSeverityLow(brainHealth, 75f, 50f, 25f, 5f),
                ThresholdSeverityHigh(ValidPercent(body.strokeAmount, 0f), 20f, 35f, 50f, 70f));
            var hypotensionSeverity = body.inCardiacArrest ? 1f : ThresholdSeverityLow(bloodPressure, 110f, 96f, 83f, 60f);
            var hypertensionSeverity = body.inCardiacArrest ? 0f : ThresholdSeverityHigh(bloodPressure, 130f, 145f, 162f, 180f);
            var bloodPressureSeverity = WeightedMax(
                ThresholdSeverityLow(bloodPressure, 110f, 96f, 83f, 60f),
                ThresholdSeverityHigh(bloodPressure, 130f, 145f, 162f, 180f));
            var bloodVolumeSeverity = ThresholdSeverityLow(bloodVolume, 80f, 60f, 40f, 25f);
            var bleedSpeedSeverity = ThresholdSeverityHigh(body.totalBleedSpeed, 0f, 0.06f, 0.15f, 0.3f);
            var limbBleedSeverity = maxLimbBleed / 25f;
            var bleedingSeverity = WeightedMax(bleedSpeedSeverity, limbBleedSeverity);
            var circulationSeverity = WeightedMax(bloodVolumeSeverity, bleedSpeedSeverity, limbBleedSeverity, bloodPressureSeverity);
            var oxygenSeverity = WeightedMax(
                ThresholdSeverityLow(bloodOxygen, 90f, 75f, 60f, 45f),
                body.breathing ? ThresholdSeverityLow(respiratoryRate, 90f, 50f, 25f, 5f) : 1f,
                ThresholdSeverityHigh(ValidPercent(body.fibrillationProgress, 0f), 15f, 50f, 75f, 95f),
                ThresholdSeverityHigh(ValidPercent(body.hemothorax, 0f), 40f, 55f, 70f, 100f));
            var infectionSeverity = WeightedMax(maxLimbInfection / 90f, ThresholdSeverityHigh(ValidPercent(body.septicShock, 0f), 35f, 60f, 75f, 82.5f), ThresholdSeverityHigh(ValidPercent(body.sicknessAmount, 0f), 20f, 55f, 85f, 100f), ThresholdSeverityHigh(ValidPercent(body.radiationSickness, 0f), 10f, 30f, 50f, 80f));
            var hungerSeverity = ThresholdSeverityLow(hunger, 35f, 20f, 10f, 0f);
            var thirstSeverity = WeightedMax(ThresholdSeverityLow(Mathf.Min(thirst, 100f), 35f, 20f, 10f, 0f), ThresholdSeverityHigh(thirst, 120f, 140f, 175f, 200f));
            var exertionSeverity = ThresholdSeverityLow(stamina, 70f, 50f, 35f, 15f);
            var tiredSeverity = ThresholdSeverityLow(energy, 35f, 25f, 15f, 7f);
            var metabolicSeverity = WeightedMax(hungerSeverity, thirstSeverity, exertionSeverity, tiredSeverity);
            var temperatureSeverity = WeightedMax(ThresholdSeverityHigh(temperature, 38.5f, 40f, 41f, 41.5f), ThresholdSeverityLow(temperature, 34.5f, 32f, 29f, 27f));
            var moodSeverity = ThresholdSeverityLow(body.totalHappiness, -20f, -50f, -75f, -90f);
            var panicSeverity = body.horrifiedLevel / 85f;
            var wetnessSeverity = ValidPercent(body.wetness, 0f) / 100f;
            var dirtSeverity = ValidPercent(body.dirtyness, 0f) / 100f;
            var radiationSeverity = ThresholdSeverityHigh(ValidPercent(body.radiationSickness, 0f), 10f, 30f, 50f, 80f);
            var sicknessSeverity = ThresholdSeverityHigh(ValidPercent(body.sicknessAmount, 0f), 20f, 55f, 85f, 100f);
            var septicSeverity = ThresholdSeverityHigh(ValidPercent(body.septicShock, 0f), 35f, 60f, 75f, 82.5f);
            var internalBleedingSeverity = WeightedMax(ThresholdSeverityHigh(ValidPercent(body.internalBleeding, 0f), 5f, 25f, 50f, 80f), ThresholdSeverityHigh(ValidPercent(body.hemothorax, 0f), 40f, 55f, 70f, 100f));
            var arrhythmiaSeverity = WeightedMax(ThresholdSeverityHigh(ValidPercent(body.fibrillationProgress, 0f), 15f, 50f, 75f, 95f), body.heartRate > 240f ? 1f : 0f, ThresholdSeverityHigh(body.heartRate > 0f ? body.heartRate : 70f, 160f, 200f, 240f, 260f));
            var cardiacArrestSeverity = body.inCardiacArrest ? 1f : 0f;
            var immunitySeverity = (50f - ValidPercent(body.immunity, 100f)) / 50f;
            var mitigation = ComputePositiveMitigation(body);

            Add("pain", painSeverity, SelectPainWave(painSeverity), 0.12f);
            Add("injury", Mathf.Max(maxLimbInjury / 100f, brokenOrDislocated), brokenOrDislocated > 0f ? DGLabWaveLibrary.FractureThrob : DGLabWaveLibrary.InjuryAche, 0.25f);
            Add("bleeding", bleedingSeverity, DGLabWaveLibrary.BleedingDrain, 0.12f);
            Add("blood-loss", bloodVolumeSeverity, DGLabWaveLibrary.BleedingDrain, 0.12f);
            Add("hypotension", hypotensionSeverity, DGLabWaveLibrary.OxygenStutter, 0.12f);
            Add("hypertension", hypertensionSeverity, DGLabWaveLibrary.ShockSpike, 0.12f);
            Add("internal-bleeding", internalBleedingSeverity, DGLabWaveLibrary.BleedingDrain, 0.12f);
            Add("infection", maxLimbInfection / 90f, DGLabWaveLibrary.InfectionCrawl, 0.14f);
            Add("sepsis", septicSeverity, DGLabWaveLibrary.InfectionCrawl, 0.14f);
            Add("sickness", sicknessSeverity, DGLabWaveLibrary.SicknessRoll, 0.16f);
            Add("radiation", radiationSeverity, DGLabWaveLibrary.SicknessRoll, 0.12f);
            Add("oxygen", oxygenSeverity, DGLabWaveLibrary.OxygenStutter, 0.12f);
            Add("arrhythmia", arrhythmiaSeverity, DGLabWaveLibrary.Heartbeat, 0.12f);
            Add("cardiac-arrest", cardiacArrestSeverity, DGLabWaveLibrary.ShockSpike, 0.12f);
            Add("hunger", hungerSeverity, DGLabWaveLibrary.HungerGnaw, 0.18f);
            Add("thirst", thirstSeverity, DGLabWaveLibrary.ThirstNeedle, 0.18f);
            Add("temperature", temperatureSeverity, DGLabWaveLibrary.TemperatureWave, 0.14f);
            Add("exertion", exertionSeverity, DGLabWaveLibrary.FatiguePulse, 0.2f);
            Add("tired", tiredSeverity, DGLabWaveLibrary.FatiguePulse, 0.2f);
            Add("mood", moodSeverity, DGLabWaveLibrary.MoodSink, 0.1f);
            Add("panic", panicSeverity, DGLabWaveLibrary.Heartbeat, 0.12f);
            Add("low-immunity", immunitySeverity, DGLabWaveLibrary.SicknessRoll, 0.2f);
            Add("nerve", nerveSeverity, DGLabWaveLibrary.ShockSpike, 0.1f);
            Add("trauma", traumaSeverity, DGLabWaveLibrary.InjuryAche, 0.08f);
            Add("pain-shock", painShockSeverity, DGLabWaveLibrary.HeavyShock, 0.08f);
            Add("shock", shockSeverity, DGLabWaveLibrary.HeavyShock, 0.08f);

            var criticalSeverity = WeightedMax(shockSeverity, painShockSeverity, nerveSeverity, circulationSeverity, oxygenSeverity);
            var systemic = WeightedMax(
                shockSeverity * 1.35f,
                painShockSeverity * 1.2f,
                traumaSeverity * 1.05f,
                nerveSeverity * 1.25f,
                circulationSeverity * 1.18f,
                oxygenSeverity * 1.2f,
                infectionSeverity * 0.75f,
                temperatureSeverity * 0.75f,
                panicSeverity * 0.45f,
                criticalSeverity > 0.82f ? 1f : 0f);

            _sustainedRatioA = ComputeRuntimeRatio(systemic, channelARegionalSeverity, painCap, mitigation, criticalSeverity);
            _sustainedRatioB = ComputeRuntimeRatio(systemic, channelBRegionalSeverity, painCap, mitigation, criticalSeverity);
            LogTest(
                "score " +
                "AReg=" + Percent(channelARegionalSeverity) + " " +
                "BReg=" + Percent(channelBRegionalSeverity) + " " +
                "PainCap=" + Percent(painCap) + " " +
                "Sys=" + Percent(systemic) + " " +
                "Mit=" + Percent(mitigation) + " " +
                "OutA=" + Percent(_sustainedRatioA) + " " +
                "OutB=" + Percent(_sustainedRatioB) + " " +
                "ATop=" + channelARegionalLabel + " " +
                "BTop=" + channelBRegionalLabel + " " +
                "pain=" + Percent(painSeverity) + " " +
                "shock=" + Percent(shockSeverity) + " " +
                "painShock=" + Percent(painShockSeverity) + " " +
                "trauma=" + Percent(traumaSeverity) + " " +
                "nerve=" + Percent(nerveSeverity) + " " +
                "blood=" + Percent(circulationSeverity) + " " +
                "bleed=" + Percent(bleedingSeverity) + " " +
                "vol=" + Percent(bloodVolumeSeverity) + " " +
                "hypo=" + Percent(hypotensionSeverity) + " " +
                "hyper=" + Percent(hypertensionSeverity) + " " +
                "arr=" + Percent(arrhythmiaSeverity) + " " +
                "arrest=" + Percent(cardiacArrestSeverity) + " " +
                "oxy=" + Percent(oxygenSeverity) + " " +
                "inf=" + Percent(infectionSeverity) + " " +
                "meta=" + Percent(metabolicSeverity) + " " +
                "exert=" + Percent(exertionSeverity) + " " +
                "tired=" + Percent(tiredSeverity) + " " +
                "temp=" + Percent(temperatureSeverity) + " " +
                "mood=" + Percent(moodSeverity) + " " +
                "wet=" + Percent(wetnessSeverity) + " " +
                "dirty=" + Percent(dirtSeverity) + " " +
                BuildBodySnapshot(body));
        }

        private void Add(string key, float severity, string[] wave, float threshold)
        {
            severity = Mathf.Clamp01(severity);
            if (severity < threshold) return;
            for (var i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].Key != key) continue;
                if (severity > _layers[i].Severity)
                {
                    _layers[i].Severity = severity;
                    _layers[i].Wave = wave;
                }
                return;
            }
            _layers.Add(new ConditionLayer { Key = key, Severity = severity, Wave = wave });
        }

        private static void AddRegionalLayers(List<ConditionLayer> target, float pain, float injury, float bleed, float infection, float fracture, float dislocation, float nerve)
        {
            if (target == null) return;
            AddTo(target, "pain", pain * 0.95f, SelectPainWave(pain * 0.95f), 0.12f);
            AddTo(target, "injury", injury, DGLabWaveLibrary.InjuryAche, 0.18f);
            AddTo(target, "injury", fracture, DGLabWaveLibrary.FractureThrob, 0.18f);
            AddTo(target, "injury", dislocation, DGLabWaveLibrary.JointPulse, 0.18f);
            AddTo(target, "bleeding", bleed, DGLabWaveLibrary.BleedingDrain, 0.12f);
            AddTo(target, "infection", infection, DGLabWaveLibrary.InfectionCrawl, 0.14f);
            AddTo(target, "nerve", nerve, DGLabWaveLibrary.ShockSpike, 0.1f);
        }

        private static void AddTo(List<ConditionLayer> target, string key, float severity, string[] wave, float threshold)
        {
            severity = Mathf.Clamp01(severity);
            if (severity < threshold) return;
            for (var i = 0; i < target.Count; i++)
            {
                if (target[i].Key != key) continue;
                if (severity > target[i].Severity)
                {
                    target[i].Severity = severity;
                    target[i].Wave = wave;
                }
                return;
            }
            target.Add(new ConditionLayer { Key = key, Severity = severity, Wave = wave });
        }

        private static ConditionLayer[] BuildChannelWaveLayers(List<ConditionLayer> regionalLayers, ConditionLayer[] systemicLayers)
        {
            var selected = new List<ConditionLayer>();
            if (regionalLayers != null) selected.AddRange(regionalLayers.OrderByDescending(layer => layer.Severity));
            if (systemicLayers != null)
            {
                for (var i = 0; i < systemicLayers.Length && selected.Count < 4; i++)
                {
                    if (!IsSharedLayer(systemicLayers[i].Key)) continue;
                    if (ContainsLayer(selected, systemicLayers[i].Key)) continue;
                    selected.Add(systemicLayers[i]);
                }
            }
            return selected.OrderByDescending(layer => layer.Severity).Take(4).ToArray();
        }

        private static bool IsSharedLayer(string key)
        {
            switch (key)
            {
                case "shock":
                case "pain-shock":
                case "trauma":
                case "nerve":
                case "oxygen":
                case "arrhythmia":
                case "cardiac-arrest":
                case "blood-loss":
                case "hypotension":
                case "hypertension":
                case "internal-bleeding":
                case "sepsis":
                case "sickness":
                case "radiation":
                case "hunger":
                case "thirst":
                case "temperature":
                case "exertion":
                case "tired":
                case "mood":
                case "panic":
                case "low-immunity":
                    return true;
                default:
                    return false;
            }
        }

        private static bool ContainsLayer(List<ConditionLayer> layers, string key)
        {
            for (var i = 0; i < layers.Count; i++)
            {
                if (layers[i].Key == key) return true;
            }
            return false;
        }

        private static string BuildSignature(ConditionLayer[] layers)
        {
            return layers == null || layers.Length == 0 ? "none" : string.Join("+", layers.Select(layer => layer.Key).ToArray());
        }

        private static float ComputeRuntimeRatio(float systemic, float regional, float painCap, float mitigation, float criticalSeverity)
        {
            var regionalCap = Mathf.Max(painCap, criticalSeverity >= 0.75f ? 1f : 0f);
            var cappedRegional = Mathf.Min(regional * 0.95f, regionalCap);
            var severity = WeightedMax(systemic, cappedRegional);
            var mitigationScale = criticalSeverity >= 0.9f ? 1f : (severity >= 0.9f ? 1f - mitigation * 0.2f : 1f - mitigation);
            severity = Mathf.Clamp01(severity * mitigationScale);
            if (severity < 0.12f) return 0f;
            return Mathf.Clamp01(severity);
        }

        private static string[] SelectPainWave(float severity)
        {
            if (severity >= 0.62f) return DGLabWaveLibrary.SeverePainTremor;
            if (severity >= 0.32f) return DGLabWaveLibrary.PainTremor;
            return DGLabWaveLibrary.PainThrob;
        }

        private static float PainOutputCap(float totalPain)
        {
            if (totalPain <= 10f) return 0f;
            if (totalPain <= 30f) return Mathf.Lerp(0.12f, 0.35f, Mathf.InverseLerp(10f, 30f, totalPain));
            if (totalPain <= 55f) return Mathf.Lerp(0.35f, 0.65f, Mathf.InverseLerp(30f, 55f, totalPain));
            if (totalPain <= 80f) return Mathf.Lerp(0.65f, 0.9f, Mathf.InverseLerp(55f, 80f, totalPain));
            return Mathf.Lerp(0.9f, 1f, Mathf.InverseLerp(80f, 100f, totalPain));
        }

        private static float ThresholdSeverityLow(float value, float mild, float moderate, float severe, float critical)
        {
            if (value >= mild) return 0f;
            if (value >= moderate) return Mathf.Lerp(0.12f, 0.35f, Mathf.InverseLerp(mild, moderate, value));
            if (value >= severe) return Mathf.Lerp(0.35f, 0.7f, Mathf.InverseLerp(moderate, severe, value));
            if (value >= critical) return Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(severe, critical, value));
            return 1f;
        }

        private static float ThresholdSeverityHigh(float value, float mild, float moderate, float severe, float critical)
        {
            if (value <= mild) return 0f;
            if (value <= moderate) return Mathf.Lerp(0.12f, 0.35f, Mathf.InverseLerp(mild, moderate, value));
            if (value <= severe) return Mathf.Lerp(0.35f, 0.7f, Mathf.InverseLerp(moderate, severe, value));
            if (value <= critical) return Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(severe, critical, value));
            return 1f;
        }

        private static float ComputePositiveMitigation(Body body)
        {
            var mitigation = 0f;
            mitigation += Mathf.Clamp01(body.curAdrenaline / 100f) * 0.18f;
            mitigation += Mathf.Clamp01(Mathf.Max(body.opiateHappiness, 0f) / 100f) * 0.28f;
            mitigation += Mathf.Clamp01((body.desensitizedMult - 1f) / 2f) * 0.1f;
            mitigation += Mathf.Clamp01(Mathf.Max(body.totalHappiness, 0f) / 100f) * 0.06f;
            if (body.sleeping) mitigation += 0.08f;

            if (body.TryGetComponent<Painkillers>(out var painkillers))
            {
                mitigation += Mathf.Clamp01(painkillers.actualOpiateReception / 100f) * 0.22f;
            }

            return Mathf.Clamp(mitigation, 0f, 0.55f);
        }

        private static float WeightedMax(params float[] values)
        {
            var max = 0f;
            if (values == null) return 0f;
            for (var i = 0; i < values.Length; i++) max = Mathf.Max(max, values[i]);
            return Mathf.Clamp01(max);
        }

        private static float ValidPercent(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return fallback;
            if (value < 0f) return fallback;
            return Mathf.Clamp(value, 0f, 100f);
        }

        private void LogTest(string message)
        {
            if (_log == null || _testLogEnabledProvider == null || !_testLogEnabledProvider()) return;

            var now = Time.realtimeSinceStartup;
            var interval = Mathf.Clamp(_testLogIntervalProvider != null ? _testLogIntervalProvider() : 1f, 0.25f, 10f);
            if (now - _lastTestLogTime < interval) return;

            _lastTestLogTime = now;
            _log.LogInfo("DG-Lab realtime test: " + message);
        }

        private static string BuildBodySnapshot(Body body)
        {
            return "raw[" +
                   "avgPain=" + body.averagePain.ToString("0.0") + "," +
                   "shock=" + body.shock.ToString("0.0") + "," +
                   "trauma=" + body.traumaAmount.ToString("0.0") + "," +
                   "painShock=" + body.painShock.ToString("0.00") + "," +
                   "brain=" + body.brainHealth.ToString("0.0") + "," +
                   "con=" + body.consciousness.ToString("0.0") + "," +
                   "stroke=" + body.strokeAmount.ToString("0.0") + "," +
                   "blood=" + body.bloodVolume.ToString("0.0") + "," +
                   "bleed=" + body.totalBleedSpeed.ToString("0.000") + "," +
                   "oxygen=" + body.bloodOxygen.ToString("0.0") + "," +
                   "bp=" + body.bloodPressure.ToString("0.0") + "," +
                   "rr=" + body.respiratoryRate.ToString("0.0") + "," +
                   "temp=" + body.temperature.ToString("0.0") + "," +
                   "wet=" + body.wetness.ToString("0.0") + "," +
                   "dirty=" + body.dirtyness.ToString("0.0") + "," +
                   "happy=" + body.totalHappiness.ToString("0.0") + "," +
                   "horror=" + body.horrifiedLevel.ToString("0.0") + "," +
                   "sick=" + body.sicknessAmount.ToString("0.0") + "," +
                   "rad=" + body.radiationSickness.ToString("0.0") + "," +
                   "adren=" + body.curAdrenaline.ToString("0.0") + "," +
                   "opiate=" + body.opiateHappiness.ToString("0.0") + "]";
        }

        private static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString();
        }

        private static float LimbRegionWeight(int index, Limb limb)
        {
            if (limb != null && limb.isHead) return 1.35f;
            if (limb != null && limb.isVital) return 1.25f;
            if (limb != null && limb.isAbdomen) return 1.18f;
            if (index == 0) return 1.35f;
            if (index == 1) return 1.25f;
            if (index == 2) return 1.18f;
            if (index >= 9) return 1.08f;
            return 1f;
        }

        private static string LimbLabel(int index, Limb limb)
        {
            if (limb != null && !string.IsNullOrEmpty(limb.shortName)) return limb.shortName;
            switch (index)
            {
                case 0: return "Head";
                case 1: return "UpTorso";
                case 2: return "DownTorso";
                case 3: return "ArmFUpper";
                case 4: return "ArmFLower";
                case 5: return "HandF";
                case 6: return "ArmBUpper";
                case 7: return "ArmBLower";
                case 8: return "HandB";
                case 9: return "LegFUpper";
                case 10: return "LegFLower";
                case 11: return "FootF";
                case 12: return "LegBUpper";
                case 13: return "LegBLower";
                case 14: return "FootB";
                default: return "Limb" + index;
            }
        }

        private static string[] Mix(IList<ConditionLayer> active)
        {
            if (active == null || active.Count == 0) return DGLabWaveLibrary.GentlePulse;
            if (active.Count == 1) return active[0].Wave;

            var length = active.Max(layer => layer.Wave.Length);
            var mixed = new string[length];
            for (var i = 0; i < length; i++)
            {
                var layer = active[i % active.Count];
                mixed[i] = layer.Wave[i % layer.Wave.Length];
            }

            return mixed;
        }
    }
}
