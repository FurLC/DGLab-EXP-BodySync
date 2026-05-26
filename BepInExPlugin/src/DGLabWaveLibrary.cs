namespace DGLab.BepInEx
{
    public static class DGLabWaveLibrary
    {
        // ── Event waves (short, triggered on hit events) ─────────────────────

        // Sharp impact: quick high-freq spike then fade
        public static readonly string[] DamagePulse =
        {
            "5A5A5A5A64646464",
            "3C3C3C3C50505050",
            "1E1E1E1E32323232",
            "0A0A0A0A1E1E1E1E"
        };

        // Bone break: sharp crack then throbbing ache
        public static readonly string[] BreakPulse =
        {
            "6464646478787878",
            "0A0A0A0A28282828",
            "5050505064646464",
            "0A0A0A0A1E1E1E1E",
            "3232323246464646",
            "0A0A0A0A28282828"
        };

        // Dismember: sustained high-intensity burst
        public static readonly string[] DismemberPulse =
        {
            "5050505082828282",
            "5A5A5A5A8C8C8C8C",
            "6464646482828282",
            "5050505078787878",
            "3C3C3C3C64646464",
            "1E1E1E1E46464646"
        };

        // Self-harm: deliberate sharp sting
        public static readonly string[] SelfHarmPulse =
        {
            "4646464678787878",
            "5A5A5A5A8C8C8C8C",
            "3232323264646464",
            "1414141446464646"
        };

        // Impact: blunt force, lower freq than damage
        public static readonly string[] ImpactPulse =
        {
            "1E1E1E1E64646464",
            "2828282878787878",
            "1E1E1E1E64646464",
            "0A0A0A0A32323232"
        };

        // Dislocate: sharp joint pop then ache
        public static readonly string[] DislocatePulse =
        {
            "5A5A5A5A78787878",
            "0A0A0A0A28282828",
            "3C3C3C3C5A5A5A5A",
            "0A0A0A0A1E1E1E1E"
        };

        // Coil shock / electric: high-freq burst
        public static readonly string[] Sting =
        {
            "6464646496969696",
            "5A5A5A5A78787878",
            "0A0A0A0A28282828",
            "4646464664646464",
            "0A0A0A0A1E1E1E1E"
        };

        // Treatment: brief clinical sting, intentionally much lower than shock
        public static readonly string[] TreatmentSting =
        {
            "3C3C3C3C46464646",
            "0A0A0A0A1E1E1E1E",
            "323232323C3C3C3C",
            "0A0A0A0A1E1E1E1E"
        };

        // ── Condition waves (looping, mixed by ConditionMixer) ───────────────

        // Pain throb: slow dull ache, low-mid freq
        public static readonly string[] PainThrob =
        {
            "0A0A0A0A32323232",
            "1414141446464646",
            "1E1E1E1E50505050",
            "1414141446464646",
            "0A0A0A0A32323232",
            "0A0A0A0A28282828"
        };

        // Pain tremor: moderate pain, irregular rhythm
        public static readonly string[] PainTremor =
        {
            "1E1E1E1E50505050",
            "2828282864646464",
            "1E1E1E1E50505050",
            "3232323264646464",
            "2424242450505050",
            "2828282864646464",
            "1E1E1E1E50505050",
            "2828282864646464"
        };

        // Severe pain tremor: high-intensity shaking
        public static readonly string[] SeverePainTremor =
        {
            "3C3C3C3C78787878",
            "5050505082828282",
            "4646464678787878",
            "5A5A5A5A8C8C8C8C",
            "3C3C3C3C6E6E6E6E",
            "5050505082828282",
            "4646464678787878",
            "5A5A5A5A8C8C8C8C",
            "3232323264646464",
            "4646464678787878",
            "5050505082828282",
            "3C3C3C3C6E6E6E6E"
        };

        // Fracture throb: sharp spike + long dull ache cycle
        public static readonly string[] FractureThrob =
        {
            "5050505064646464",
            "0A0A0A0A28282828",
            "1E1E1E1E46464646",
            "0A0A0A0A28282828",
            "2828282850505050",
            "0A0A0A0A1E1E1E1E",
            "1414141432323232",
            "0A0A0A0A1E1E1E1E"
        };

        // Joint pulse: dislocation ache, irregular
        public static readonly string[] JointPulse =
        {
            "1E1E1E1E46464646",
            "0A0A0A0A28282828",
            "2828282850505050",
            "0A0A0A0A1E1E1E1E",
            "1E1E1E1E46464646",
            "0A0A0A0A28282828"
        };

        // Injury ache: persistent wound soreness
        public static readonly string[] InjuryAche =
        {
            "0A0A0A0A3C3C3C3C",
            "1414141450505050",
            "1E1E1E1E50505050",
            "1414141446464646",
            "0A0A0A0A3C3C3C3C",
            "0A0A0A0A32323232"
        };

        // Bleeding drain: slow rhythmic pulse, fading feel
        public static readonly string[] BleedingDrain =
        {
            "0A0A0A0A3C3C3C3C",
            "1414141446464646",
            "1E1E1E1E50505050",
            "1414141446464646",
            "0A0A0A0A3C3C3C3C",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828"
        };

        // Oxygen stutter: irregular gasping rhythm
        public static readonly string[] OxygenStutter =
        {
            "3C3C3C3C64646464",
            "0A0A0A0A1E1E1E1E",
            "4646464664646464",
            "0A0A0A0A1E1E1E1E",
            "3232323250505050",
            "0A0A0A0A0A0A0A0A",
            "4646464664646464",
            "0A0A0A0A1E1E1E1E"
        };

        // Heartbeat: realistic lub-dub double-peak
        public static readonly string[] Heartbeat =
        {
            "1E1E1E1E50505050",
            "2828282864646464",
            "1E1E1E1E46464646",
            "0A0A0A0A0A0A0A0A",
            "0A0A0A0A0A0A0A0A",
            "1414141446464646",
            "1E1E1E1E50505050",
            "0A0A0A0A0A0A0A0A",
            "0A0A0A0A0A0A0A0A"
        };

        // Infection crawl: slow creeping sensation
        public static readonly string[] InfectionCrawl =
        {
            "0A0A0A0A28282828",
            "1414141432323232",
            "1E1E1E1E3C3C3C3C",
            "2828282846464646",
            "1E1E1E1E3C3C3C3C",
            "1414141432323232",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E"
        };

        // Sickness roll: nausea wave, slow undulation
        public static readonly string[] SicknessRoll =
        {
            "0A0A0A0A28282828",
            "1414141446464646",
            "1E1E1E1E50505050",
            "2828282846464646",
            "1E1E1E1E3C3C3C3C",
            "1414141428282828",
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828"
        };

        // Hunger gnaw: intermittent gnawing cramp
        public static readonly string[] HungerGnaw =
        {
            "1414141432323232",
            "1E1E1E1E46464646",
            "2828282850505050",
            "1E1E1E1E46464646",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E"
        };

        // Thirst needle: sharp dry throat prick
        public static readonly string[] ThirstNeedle =
        {
            "5050505046464646",
            "0A0A0A0A1E1E1E1E",
            "4646464646464646",
            "0A0A0A0A1E1E1E1E",
            "3C3C3C3C3C3C3C3C",
            "0A0A0A0A1E1E1E1E"
        };

        // Temperature wave: fever heat or cold chill
        public static readonly string[] TemperatureWave =
        {
            "0A0A0A0A28282828",
            "1414141432323232",
            "1E1E1E1E3C3C3C3C",
            "2828282846464646",
            "3232323250505050",
            "2828282846464646",
            "1E1E1E1E3C3C3C3C",
            "1414141432323232"
        };

        // Fatigue pulse: heavy limbs, very low freq
        public static readonly string[] FatiguePulse =
        {
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828"
        };

        // Mood sink: subtle persistent pressure
        public static readonly string[] MoodSink =
        {
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E"
        };

        // Shock spike: violent nerve jolt, irregular
        public static readonly string[] ShockSpike =
        {
            "5050505078787878",
            "0A0A0A0A1E1E1E1E",
            "464646466E6E6E6E",
            "0A0A0A0A1E1E1E1E",
            "3C3C3C3C64646464",
            "0A0A0A0A28282828"
        };

        // Heavy shock: sustained high-intensity convulsion
        public static readonly string[] HeavyShock =
        {
            "4646464678787878",
            "5050505082828282",
            "3C3C3C3C6E6E6E6E",
            "0A0A0A0A28282828",
            "5050505082828282",
            "323232325A5A5A5A",
            "0A0A0A0A1E1E1E1E"
        };

        // ── Ambient / fallback ───────────────────────────────────────────────

        public static readonly string[] GentlePulse =
        {
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E",
            "0A0A0A0A28282828"
        };

        public static readonly string[] SoftBuzz =
        {
            "0A0A0A0A28282828",
            "0A0A0A0A32323232",
            "1414141428282828",
            "0A0A0A0A32323232"
        };

        public static readonly string[] IntensePulse =
        {
            "2828282864646464",
            "3C3C3C3C78787878",
            "505050508C8C8C8C",
            "3C3C3C3C78787878"
        };

        // ── Persistent state loops ───────────────────────────────────────────

        public static readonly string[] CriticalLoop =
        {
            "1E1E1E1E50505050",
            "0A0A0A0A28282828",
            "2828282864646464",
            "0A0A0A0A28282828",
            "1E1E1E1E50505050",
            "0A0A0A0A1E1E1E1E"
        };

        public static readonly string[] DeathLoop =
        {
            "0A0A0A0A28282828",
            "1414141432323232",
            "0A0A0A0A28282828",
            "0A0A0A0A1E1E1E1E"
        };
    }
}
