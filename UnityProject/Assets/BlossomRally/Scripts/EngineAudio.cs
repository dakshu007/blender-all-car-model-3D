// EngineAudio.cs - car audio: optional recorded engine loops (Resources/Audio/Engines) over procedural layers: four engine loops (low/high rpm x on/off
// throttle) crossfaded by rpm and load, gear-change cut + pop, lift-off backfire crackles, Group B
// turbo whistle + blow-off valve, tyre squeal on hard surfaces, per-surface rolling noise and wind.
// Every loop is generated once (cached in static fields, per car class for the engine) as a seamless
// circular buffer; at runtime only pitch / volume are modulated, so the CPU cost is tiny.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    [RequireComponent(typeof(ArcadeCar))]
    public class EngineAudio : MonoBehaviour
    {
        const int Rate = 22050;
        // layer reference rpm: firing frequency of a four-cylinder (2 pulses / rev) at these rpm is
        // exactly Rate / period, so a whole number of cycles fits the loop
        const int LowPeriod = 330, LowCycles = 100;     // 66.8 Hz  ~ 2004 rpm
        const int HighPeriod = 132, HighCycles = 252;   // 167.0 Hz ~ 5011 rpm
        const float LowRefRpm = Rate * 30f / LowPeriod;
        const float HighRefRpm = Rate * 30f / HighPeriod;
        // recorded engine loops (ElevenLabs pack, Resources/Audio/Engines/<Class>_Low / _High):
        // the "low" file is a steady idle, the "high" file a steady full-throttle hold
        public const string EngineFolder = "Audio/Engines/";
        const float SampleLowRpm = 1100f, SampleHighRpm = 6200f;

        // ------------------------------------------------------------- static clip cache
        static readonly Dictionary<string, AudioClip[]> engineCache = new Dictionary<string, AudioClip[]>();
        static AudioClip[] popClips;
        static AudioClip shiftClip, turboClip, bovClip, squealClip, windClip;
        static AudioClip[] surfaceClips;   // Grass, Asphalt, Gravel, Dirt (indexed by SurfaceLoop)

        enum SurfaceLoop { Grass = 0, Asphalt = 1, Gravel = 2, Dirt = 3 }

        // ------------------------------------------------------------- public hooks (visual effects)
        /// <summary>An exhaust pop / backfire was played: car, loudness 0..1 (exhaust flames, light flicker).</summary>
        public static event System.Action<ArcadeCar, float> Backfire;
        /// <summary>The gearbox changed gear: car, previous gear, new gear.</summary>
        public static event System.Action<ArcadeCar, int, int> GearShift;

        void RaiseBackfire(float loud)
        {
            if (Backfire != null && car != null) Backfire(car, Mathf.Clamp01(loud));
        }

        ArcadeCar car;
        AudioSource[] engine = new AudioSource[4];      // lowOn, lowOff, highOn, highOff
        AudioSource squeal, wind, turbo, oneShot;
        AudioSource sampleLow, sampleHigh;   // recorded engine loops (null = procedural only)
        float procMix = 1f;
        AudioSource[] surface = new AudioSource[4];
        bool started;

        bool groupB, classic;
        float classPitch = 1f, popChance = 1f;
        float load, spool, gearCut, cutTimer;
        int lastGear = 1;
        float prevThrottle, peakThrottle, liftTime = -10f;
        int crackles;
        float nextCrackle, bovCooldown;
        readonly float[] surfVol = new float[4];
        float squealVol;

        void Start()
        {
            car = GetComponent<ArcadeCar>();
            string cls = car.spec != null && car.spec.carClass != null ? car.spec.carClass : "Group 4";
            groupB = cls == "Group B";
            classic = cls == "Classic RWD";
            classPitch = groupB ? 1.04f : classic ? 0.9f : 1f;
            popChance = groupB ? 1.3f : classic ? 0.45f : 1f;

            var clips = EngineClips(cls);
            EnsureSharedClips();

            for (int i = 0; i < 4; i++) engine[i] = MakeLoop(clips[i], 0.6f);
            for (int i = 0; i < 4; i++) surface[i] = MakeLoop(surfaceClips[i], 0.45f);
            squeal = MakeLoop(squealClip, 0.6f);
            wind = MakeLoop(windClip, 0.1f);
            if (groupB) turbo = MakeLoop(turboClip, 0.6f);
            // recorded engine pack for this car class, if present: it carries the character and
            // the procedural layers stay underneath (quieter) for throttle load and texture
            string key = groupB ? "GroupB" : classic ? "Classic" : "Group4";
            AudioClip lo = null, hi = null;
            try
            {
                lo = Resources.Load<AudioClip>(EngineFolder + key + "_Low");
                hi = Resources.Load<AudioClip>(EngineFolder + key + "_High");
            }
            catch (System.Exception) { }
            if (lo != null && hi != null)
            {
                sampleLow = MakeLoop(lo, 0.6f);
                sampleHigh = MakeLoop(hi, 0.6f);
                procMix = 0.45f;
            }
            oneShot = gameObject.AddComponent<AudioSource>();
            Setup(oneShot, 0.6f);
            lastGear = car.Gear;
            started = true;
        }

        void Setup(AudioSource s, float spatial)
        {
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 8f;
            s.maxDistance = 400f;
            s.volume = 0f;
        }

        AudioSource MakeLoop(AudioClip clip, float spatial)
        {
            var s = gameObject.AddComponent<AudioSource>();
            Setup(s, spatial);
            s.clip = clip;
            s.loop = true;
            s.Play();
            // random start offset so identical loops on different sources never phase together
            if (clip != null) s.timeSamples = Random.Range(0, clip.samples);
            return s;
        }

        // ------------------------------------------------------------- runtime
        float showFade = 1f;

        void Update()
        {
            if (!started || car == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // paused (AudioListener.pause handles the sound itself)
            // quiet idle on the garage turntable / title screen, silent while a replay freezes the car
            bool frozen = car.Body != null && car.Body.isKinematic;
            showFade = Mathf.MoveTowards(showFade, frozen ? (car.enabled ? 0.25f : 0f) : 1f, dt * 2f);
            float sfx = GameSettings.SfxVolume * showFade;
            float rpm = car.EngineRpm;
            float rpm01 = Mathf.InverseLerp(900f, 7600f, rpm);
            float thr = Mathf.Clamp01(car.throttle);
            bool live = car.controlsEnabled;

            load = Mathf.MoveTowards(load, thr, dt * (thr > load ? 7f : 4f));

            // ---- gear change: short ignition cut + pop + tiny rumble on upshift
            int gear = car.Gear;
            if (gear != lastGear)
            {
                if (gear > lastGear && thr > 0.4f)
                {
                    cutTimer = 0.09f;
                    PlayOne(shiftClip, 0.42f * sfx, Random.Range(0.92f, 1.08f));
                    if (live) InputReader.Rumble(0.22f, 0.12f, 0.07f);
                }
                else if (gear < lastGear && thr < 0.3f && rpm > 3000f && Random.value < 0.5f * popChance)
                {
                    PlayOne(popClips[Random.Range(0, popClips.Length)], 0.22f * sfx, Random.Range(1.0f, 1.2f));   // downshift blip
                    RaiseBackfire(0.5f);
                }
                if (GearShift != null) GearShift(car, lastGear, gear);
                lastGear = gear;
            }
            if (cutTimer > 0f) { cutTimer -= dt; gearCut = Mathf.MoveTowards(gearCut, 1f, dt * 30f); }
            else gearCut = Mathf.MoveTowards(gearCut, 0f, dt * 7f);
            float cutGain = 1f - 0.65f * gearCut;

            // ---- engine layers
            float x = Mathf.InverseLerp(2700f, 5300f, rpm);
            float lowW = Mathf.Cos(x * Mathf.PI * 0.5f), highW = Mathf.Sin(x * Mathf.PI * 0.5f);
            float onW = Mathf.Sqrt(load), offW = Mathf.Sqrt(1f - load) * 0.85f;
            float gain = 0.34f * (0.6f + 0.4f * load) * (0.78f + 0.22f * rpm01) * cutGain * sfx * procMix;
            float pLow = Mathf.Clamp(rpm / LowRefRpm * classPitch, 0.3f, 2.9f);
            float pHigh = Mathf.Clamp(rpm / HighRefRpm * classPitch, 0.3f, 2.9f);
            engine[0].volume = gain * lowW * onW; engine[0].pitch = pLow;
            engine[1].volume = gain * lowW * offW; engine[1].pitch = pLow;
            engine[2].volume = gain * highW * onW; engine[2].pitch = pHigh;
            engine[3].volume = gain * highW * offW; engine[3].pitch = pHigh;

            // ---- recorded engine loops: idle file below ~3000 rpm, full-throttle file above
            if (sampleLow != null && sampleHigh != null)
            {
                float sx = Mathf.InverseLerp(1800f, 4200f, rpm);
                float sLowW = Mathf.Cos(sx * Mathf.PI * 0.5f), sHighW = Mathf.Sin(sx * Mathf.PI * 0.5f);
                float sg = 0.5f * (0.5f + 0.5f * load) * (0.8f + 0.2f * rpm01) * cutGain * sfx;
                sampleLow.volume = sg * sLowW;
                sampleLow.pitch = Mathf.Clamp(rpm / SampleLowRpm, 0.6f, 2.4f);
                sampleHigh.volume = sg * sHighW;
                sampleHigh.pitch = Mathf.Clamp(rpm / SampleHighRpm, 0.5f, 1.5f);
            }

            // ---- lift-off backfire crackles
            float now = Time.time;
            peakThrottle = Mathf.Max(peakThrottle * Mathf.Exp(-dt * 1.5f), thr);
            bool lifted = thr < 0.2f && prevThrottle >= 0.2f;
            if (lifted)
            {
                liftTime = now;
                if (rpm > 4300f && peakThrottle > 0.7f && Random.value < 0.85f * Mathf.Min(1f, popChance))
                {
                    crackles = Random.Range(2, groupB ? 8 : 6);
                    nextCrackle = now + Random.Range(0.03f, 0.12f);
                }
                if (groupB && spool > 0.4f && bovCooldown <= 0f)
                {
                    PlayOne(bovClip, 0.3f * spool * sfx, Random.Range(0.94f, 1.06f));
                    bovCooldown = 0.7f;
                }
            }
            if (thr > 0.35f) crackles = 0;
            if (crackles > 0 && now >= nextCrackle)
            {
                crackles--;
                nextCrackle = now + Random.Range(0.045f, 0.2f);
                float popVol = Random.Range(0.18f, 0.36f);
                PlayOne(popClips[Random.Range(0, popClips.Length)], popVol * sfx, Random.Range(0.85f, 1.18f));
                RaiseBackfire(popVol / 0.36f);
            }
            // occasional overrun pops on a long lift at high revs
            if (thr < 0.1f && rpm > 4800f && now - liftTime > 0.4f && Random.value < dt * 1.2f * popChance * rpm01)
            {
                PlayOne(popClips[Random.Range(0, popClips.Length)], Random.Range(0.1f, 0.2f) * sfx, Random.Range(0.9f, 1.2f));
                RaiseBackfire(0.35f);
            }
            prevThrottle = thr;
            if (bovCooldown > 0f) bovCooldown -= dt;

            // ---- turbo spool (Group B only)
            if (turbo != null)
            {
                float target = Mathf.InverseLerp(1800f, 7400f, rpm) * thr;
                spool = Mathf.MoveTowards(spool, target, dt * (target > spool ? 0.9f : 3.5f));
                turbo.pitch = 0.5f + 0.8f * spool;
                turbo.volume = 0.11f * spool * spool * sfx;
            }

            // ---- surfaces
            float speed = Mathf.Abs(car.SpeedKmh);
            float sf = Mathf.InverseLerp(3f, 130f, speed);
            float moving = Mathf.Clamp01(speed / 10f);
            float g0 = 0f, g1 = 0f, g2 = 0f, g3 = 0f, hard = 0f, slip = 0f;
            int n = car.wheels != null ? car.wheels.Length : 0;
            for (int i = 0; i < n; i++)
            {
                var w = car.wheels[i];
                if (w == null || !w.grounded) continue;
                switch (w.surface)
                {
                    case Surface.Grass: g0 += 1f; break;
                    case Surface.Asphalt: g1 += 1f; hard += 1f; break;
                    case Surface.Paving: g1 += 1.15f; hard += 1f; break;
                    case Surface.Gravel: g2 += 1f; break;
                    default: g3 += 1f; break;
                }
                if (w.surface == Surface.Asphalt || w.surface == Surface.Paving)
                    slip = Mathf.Max(slip, Mathf.InverseLerp(2.2f, 7.5f, w.lateralSlip));
            }
            float inv = n > 0 ? 1f / n : 0f;
            float wet = Atmosphere.Wetness;
            SurfaceUpdate(0, g0 * inv * 0.17f, sf, moving, 0.8f, dt, sfx);
            SurfaceUpdate(1, g1 * inv * (0.13f + 0.12f * wet), sf, moving, 0.85f + 0.15f * wet, dt, sfx);
            SurfaceUpdate(2, g2 * inv * 0.26f, sf, moving, 0.85f, dt, sfx);
            SurfaceUpdate(3, g3 * inv * 0.24f, sf, moving, 0.8f, dt, sfx);

            // ---- tyre squeal on asphalt / paving
            float sq = Mathf.Max(car.DriftIntensity, slip) * (n > 0 ? Mathf.Clamp01(hard * inv * 1.5f) : 0f) * moving;
            sq *= 1f - 0.6f * wet;   // wet tyres hiss more than they squeal
            squealVol = Mathf.MoveTowards(squealVol, sq, dt * (sq > squealVol ? 5f : 3f));
            squeal.volume = 0.26f * squealVol * sfx;
            squeal.pitch = 0.88f + 0.22f * squealVol + 0.03f * Mathf.Sin(now * 7.3f);

            // ---- wind
            wind.volume = (0.2f * sf * sf + 0.015f * Atmosphere.Gust * sf) * sfx;
            wind.pitch = 0.7f + 0.6f * sf;
        }

        void SurfaceUpdate(int i, float weight, float sf, float moving, float basePitch, float dt, float sfx)
        {
            float target = weight * (0.25f + 0.75f * sf) * moving;
            surfVol[i] = Mathf.MoveTowards(surfVol[i], target, dt * 2.5f);
            surface[i].volume = surfVol[i] * sfx;
            surface[i].pitch = basePitch * (0.75f + 0.55f * sf);
        }

        void PlayOne(AudioClip c, float vol, float pitch)
        {
            if (c == null || oneShot == null) return;
            oneShot.pitch = pitch;
            oneShot.PlayOneShot(c, Mathf.Clamp01(vol));
        }

        // ============================================================= clip generation

        struct EngineProfile
        {
            public float[] harm;      // harmonic weights of the smooth tone
            public float tone, burst, burstDecay, formant, rasp, rough, sub, cutoff, drive;
        }

        static AudioClip[] EngineClips(string cls)
        {
            AudioClip[] c;
            if (engineCache.TryGetValue(cls, out c) && c != null && c[0] != null) return c;
            bool b = cls == "Group B", classic = cls == "Classic RWD";
            float bright = b ? 1.2f : classic ? 0.8f : 1f;
            float rasp = b ? 1.3f : classic ? 0.7f : 1f;
            float rough = classic ? 0.7f : 1f;
            float sub = classic ? 1.4f : 1f;

            var on = new EngineProfile
            {
                harm = new[] { 1f, 0.72f, 0.52f, 0.46f, 0.3f, 0.24f, 0.17f, 0.12f, 0.08f, 0.05f },
                tone = 0.7f, burst = 0.55f * rasp, burstDecay = 7f, formant = 5f, rasp = 0.22f * rasp,
                rough = 0.16f * rough, sub = 0.22f * sub, cutoff = 2400f * bright, drive = 1.8f
            };
            var off = new EngineProfile
            {
                harm = new[] { 1f, 0.45f, 0.2f, 0.1f, 0.05f },
                tone = 0.75f, burst = 0.18f, burstDecay = 9f, formant = 3f, rasp = 0.07f * rasp,
                rough = 0.3f * rough, sub = 0.32f * sub, cutoff = 950f * bright, drive = 1.2f
            };
            var onHigh = on; onHigh.cutoff *= 1.6f; onHigh.rough *= 0.6f;
            var offHigh = off; offHigh.cutoff *= 1.5f;

            uint seed = (uint)(cls.GetHashCode() & 0x7fffffff) | 1u;
            c = new[]
            {
                ProcAudio.MakeClip("BR_Eng_LowOn", Engine(on, LowPeriod, LowCycles, seed), Rate, 1),
                ProcAudio.MakeClip("BR_Eng_LowOff", Engine(off, LowPeriod, LowCycles, seed + 11), Rate, 1),
                ProcAudio.MakeClip("BR_Eng_HighOn", Engine(onHigh, HighPeriod, HighCycles, seed + 23), Rate, 1),
                ProcAudio.MakeClip("BR_Eng_HighOff", Engine(offHigh, HighPeriod, HighCycles, seed + 37), Rate, 1),
            };
            engineCache[cls] = c;
            return c;
        }

        /// <summary>One engine loop: per-cycle wavetable (harmonics + combustion burst), cycle-to-cycle
        /// roughness, cylinder imbalance (sub-harmonics), pulse-gated exhaust rasp; all circular.</summary>
        static float[] Engine(EngineProfile p, int period, int cycles, uint seed)
        {
            var rng = new XRng(seed);
            int L = period * cycles;
            var table = new float[period];
            var phase = new float[p.harm.Length];
            for (int h = 0; h < phase.Length; h++) phase[h] = rng.Value * ProcAudio.TwoPi;
            for (int j = 0; j < period; j++)
            {
                float ph = j / (float)period;
                float s = 0f;
                for (int h = 0; h < p.harm.Length; h++) s += p.harm[h] * Mathf.Sin(ProcAudio.TwoPi * (h + 1) * ph + phase[h]);
                float burst = Mathf.Exp(-ph * p.burstDecay) * Mathf.Sin(ProcAudio.TwoPi * p.formant * ph);
                table[j] = s * p.tone * 0.35f + burst * p.burst;
            }
            // per-cycle amplitudes: random roughness x four-cylinder imbalance
            float[] cyl = { 1f, 0.9f, 1.06f, 0.95f };
            var amp = new float[cycles];
            for (int k = 0; k < cycles; k++) amp[k] = (1f + p.rough * rng.Signed) * cyl[k & 3];

            var noise = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(noise, Biquad.BandPass(1400f, 0.7f, Rate));

            var d = new float[L];
            for (int k = 0; k < cycles; k++)
            {
                float a0 = amp[k], a1 = amp[(k + 1) % cycles];
                int baseIdx = k * period;
                for (int j = 0; j < period; j++)
                {
                    float t = j / (float)period;
                    float a = a0 + (a1 - a0) * t;
                    float gate = Mathf.Exp(-t * 5f);
                    float subPh = ((k & 1) + t) * 0.5f;                       // half firing frequency
                    float v = table[j] * a + p.sub * Mathf.Sin(ProcAudio.TwoPi * subPh) * a
                              + noise[baseIdx + j] * p.rasp * gate * a * 3f;
                    d[baseIdx + j] = v;
                }
            }
            ProcAudio.FilterLoop(d, Biquad.LowPass(p.cutoff, 0.7f, Rate));
            ProcAudio.FilterLoop(d, Biquad.HighPass(28f, 0.7f, Rate));
            ProcAudio.Normalize(d, 1f);
            float norm = 1f / ProcAudio.SoftClip(p.drive);
            for (int i = 0; i < L; i++) d[i] = ProcAudio.SoftClip(d[i] * p.drive) * norm * 0.85f;
            return d;
        }

        static void EnsureSharedClips()
        {
            if (popClips != null && popClips.Length > 0 && popClips[0] != null && windClip != null) return;
            var rng = new XRng(4242u);

            popClips = new AudioClip[5];
            for (int v = 0; v < popClips.Length; v++)
            {
                var b = new float[(int)(Rate * 0.16f)];
                int clicks = v < 2 ? 1 : v < 4 ? 3 : 5;      // single pops and short crackle runs
                int at = 0;
                for (int c = 0; c < clicks; c++)
                {
                    float a = c == 0 ? 1f : rng.Range(0.4f, 0.85f);
                    ProcAudio.NoiseBurst(b, at, Rate, rng.Range(500f, 1400f), 0.9f, rng.Range(0.006f, 0.016f), a, ref rng);
                    ProcAudio.Tone(b, at, Rate, rng.Range(70f, 115f), a * 0.9f, 0.025f, 0.0008f);
                    at += (int)(Rate * rng.Range(0.012f, 0.03f));
                }
                ProcAudio.OnePoleLoop(b, 3500f, Rate);
                ProcAudio.Finish(b, 0.9f);
                popClips[v] = ProcAudio.MakeClip("BR_Pop" + v, b, Rate, 1);
            }

            {   // gear shift: dull thump + short mechanical click + exhaust chuff
                var b = new float[(int)(Rate * 0.2f)];
                ProcAudio.Tone(b, 0, Rate, 62f, 1f, 0.035f, 0.002f);
                ProcAudio.NoiseBurst(b, 0, Rate, 900f, 0.8f, 0.02f, 0.7f, ref rng);
                ProcAudio.NoiseBurst(b, (int)(Rate * 0.004f), Rate, 3000f, 2f, 0.003f, 0.35f, ref rng);
                ProcAudio.OnePoleLoop(b, 2500f, Rate);
                ProcAudio.Finish(b, 0.9f);
                shiftClip = ProcAudio.MakeClip("BR_Shift", b, Rate, 1);
            }

            {   // turbo whistle: integer-Hz partials over a 1 s loop + airy band noise
                int L = Rate;
                var b = ProcAudio.WhiteLoop(L, ref rng);
                ProcAudio.FilterLoop(b, Biquad.BandPass(3200f, 3f, Rate));
                for (int i = 0; i < L; i++)
                {
                    float t = i / (float)Rate;
                    float wob = 1f + 0.08f * Mathf.Sin(ProcAudio.TwoPi * 3f * t);
                    b[i] = b[i] * 0.5f + (0.6f * Mathf.Sin(ProcAudio.TwoPi * 2400f * t) + 0.12f * Mathf.Sin(ProcAudio.TwoPi * 4800f * t)) * wob;
                }
                ProcAudio.Normalize(b, 0.8f);
                turboClip = ProcAudio.MakeClip("BR_Turbo", b, Rate, 1);
            }

            {   // blow-off valve "pssh": falling band-passed noise with a little flutter
                var b = new float[(int)(Rate * 0.55f)];
                var bp = new Biquad();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    if ((i & 15) == 0) bp.SetBandPass(Mathf.Lerp(4200f, 1700f, Mathf.Sqrt(t / 0.55f)), 1.1f, Rate);
                    float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t / 0.13f) * (1f + 0.25f * Mathf.Sin(ProcAudio.TwoPi * 26f * t));
                    b[i] = bp.Process(rng.Signed) * env;
                }
                ProcAudio.Finish(b, 0.85f);
                bovClip = ProcAudio.MakeClip("BR_BOV", b, Rate, 1);
            }

            {   // tyre squeal: two resonant noise bands with a slow wobble (2 s loop, 3 wobble cycles)
                int L = Rate * 2;
                var a = ProcAudio.WhiteLoop(L, ref rng);
                var c2 = (float[])a.Clone();
                ProcAudio.FilterLoop(a, Biquad.BandPass(1050f, 9f, Rate));
                ProcAudio.FilterLoop(c2, Biquad.BandPass(1580f, 12f, Rate));
                for (int i = 0; i < L; i++)
                {
                    float t = i / (float)L;
                    a[i] = a[i] * (0.8f + 0.2f * Mathf.Sin(ProcAudio.TwoPi * 3f * t)) + c2[i] * 0.55f * (0.7f + 0.3f * Mathf.Sin(ProcAudio.TwoPi * 5f * t + 1f));
                }
                ProcAudio.Normalize(a, 0.8f);
                squealClip = ProcAudio.MakeClip("BR_Squeal", a, Rate, 1);
            }

            {   // wind: brown-ish noise with slow gusting (2 s loop)
                int L = Rate * 2;
                var b = ProcAudio.WhiteLoop(L, ref rng);
                var hi = (float[])b.Clone();
                ProcAudio.OnePoleLoop(b, 380f, Rate);
                ProcAudio.FilterLoop(hi, Biquad.BandPass(650f, 0.9f, Rate));
                for (int i = 0; i < L; i++)
                {
                    float t = i / (float)L;
                    b[i] = b[i] * 2.5f + hi[i] * 0.18f * (0.6f + 0.4f * Mathf.Sin(ProcAudio.TwoPi * 2f * t));
                }
                ProcAudio.OnePoleLoop(b, 2200f, Rate);
                ProcAudio.RemoveDc(b);
                ProcAudio.Normalize(b, 0.8f);
                windClip = ProcAudio.MakeClip("BR_Wind", b, Rate, 1);
            }

            surfaceClips = new AudioClip[4];
            surfaceClips[(int)SurfaceLoop.Grass] = ProcAudio.MakeClip("BR_Surf_Grass", GrassLoop(ref rng), Rate, 1);
            surfaceClips[(int)SurfaceLoop.Asphalt] = ProcAudio.MakeClip("BR_Surf_Asphalt", AsphaltLoop(ref rng), Rate, 1);
            surfaceClips[(int)SurfaceLoop.Gravel] = ProcAudio.MakeClip("BR_Surf_Gravel", GravelLoop(ref rng), Rate, 1);
            surfaceClips[(int)SurfaceLoop.Dirt] = ProcAudio.MakeClip("BR_Surf_Dirt", DirtLoop(ref rng), Rate, 1);
        }

        // smooth road hiss: soft pink-ish noise
        static float[] AsphaltLoop(ref XRng rng)
        {
            int L = Rate * 2;
            var a = ProcAudio.WhiteLoop(L, ref rng);
            var b = (float[])a.Clone();
            ProcAudio.OnePoleLoop(a, 900f, Rate);
            ProcAudio.OnePoleLoop(a, 1800f, Rate);
            ProcAudio.OnePoleLoop(b, 240f, Rate);
            for (int i = 0; i < L; i++) a[i] = a[i] * 0.8f + b[i] * 2.4f;
            ProcAudio.FilterLoop(a, Biquad.HighPass(60f, 0.7f, Rate));
            ProcAudio.Normalize(a, 0.75f);
            return a;
        }

        // gravel crunch: dense granular clicks over a gritty bed
        static float[] GravelLoop(ref XRng rng)
        {
            int L = Rate * 2;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(d, Biquad.BandPass(700f, 0.6f, Rate));
            for (int i = 0; i < L; i++) d[i] *= 0.25f;
            int grains = 1400;
            for (int g = 0; g < grains; g++)
            {
                int at = rng.Int(L);
                float amp = rng.Value; amp *= amp;
                ProcAudio.NoiseBurst(d, at, Rate, rng.Range(1200f, 4200f), 1.4f, rng.Range(0.0012f, 0.004f), 0.35f + amp, ref rng, true);
                if ((g & 7) == 0) ProcAudio.Tone(d, at, Rate, rng.Range(180f, 420f), 0.25f * amp, 0.01f, 0.0005f, true);
            }
            ProcAudio.FilterLoop(d, Biquad.LowPass(5200f, 0.7f, Rate));
            ProcAudio.Normalize(d, 0.8f);
            return d;
        }

        // dirt: low rumble with soft uneven thumps
        static float[] DirtLoop(ref XRng rng)
        {
            int L = Rate * 2;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.OnePoleLoop(d, 160f, Rate);
            ProcAudio.OnePoleLoop(d, 220f, Rate);
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                d[i] *= 6f * (0.75f + 0.25f * Mathf.Sin(ProcAudio.TwoPi * 7f * t));
            }
            for (int g = 0; g < 26; g++)
                ProcAudio.Tone(d, rng.Int(L), Rate, rng.Range(45f, 80f), rng.Range(0.1f, 0.3f), 0.04f, 0.004f, true);
            var hiss = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(hiss, Biquad.BandPass(900f, 0.7f, Rate));
            for (int i = 0; i < L; i++) d[i] += hiss[i] * 0.06f;
            ProcAudio.FilterLoop(d, Biquad.HighPass(30f, 0.7f, Rate));
            ProcAudio.Normalize(d, 0.8f);
            return d;
        }

        // grass: soft swish (airy band noise with a gentle sway)
        static float[] GrassLoop(ref XRng rng)
        {
            int L = Rate * 2;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(d, Biquad.BandPass(2600f, 0.6f, Rate));
            ProcAudio.FilterLoop(d, Biquad.LowPass(5000f, 0.7f, Rate));
            var lo = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.OnePoleLoop(lo, 300f, Rate);
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                float sway = 0.55f + 0.45f * Mathf.Sin(ProcAudio.TwoPi * 5f * t) * Mathf.Sin(ProcAudio.TwoPi * 3f * t + 0.7f);
                d[i] = d[i] * sway + lo[i] * 1.2f;
            }
            ProcAudio.Normalize(d, 0.75f);
            return d;
        }
    }
}
