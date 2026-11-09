// RallyAudio.cs - procedural sounds for RallyLife (no audio files): crowd cheers, helicopter rotor
// loop, wind-chime notes, firework launch / burst, dragon-boat drum. Clips are synthesised once
// (static cache) with the ProcAudio / XRng / Biquad toolkit from Sfx.cs and played through a small
// pool of 3D AudioSources, scaled by GameSettings.SfxVolume. AudioListener.pause (game pause) pauses them.
using UnityEngine;

namespace BlossomRally
{
    internal sealed class RallyAudio
    {
        const int Rate = 22050;
        static AudioClip[] cheers, chimes;
        static AudioClip rotor, launch, burst, drum;
        static readonly float[] sine = BuildSine();

        readonly AudioSource[] pool = new AudioSource[8];
        int next;

        public RallyAudio(Transform parent)
        {
            var host = new GameObject("RallyAudio");
            host.transform.SetParent(parent, false);
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Src" + i);
                go.transform.SetParent(host.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.dopplerLevel = 0f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                pool[i] = s;
            }
        }

        static float[] BuildSine()
        {
            var t = new float[1024];
            for (int i = 0; i < t.Length; i++) t[i] = Mathf.Sin(i * ProcAudio.TwoPi / t.Length);
            return t;
        }

        static float Sin(double phase) { return sine[(int)(phase * 1024.0) & 1023]; }

        /// <summary>One-shot 3D sound. Returns the source (or null).</summary>
        public AudioSource Play(AudioClip clip, Vector3 pos, float volume, float pitch = 1f, float minDist = 8f, float maxDist = 220f)
        {
            if (clip == null) return null;
            float v = Mathf.Clamp01(volume) * GameSettings.SfxVolume;
            if (v < 0.005f) return null;
            AudioSource src = null;
            for (int i = 0; i < pool.Length; i++)
            {
                var s = pool[(next + i) % pool.Length];
                if (s != null && !s.isPlaying) { src = s; next = (next + i + 1) % pool.Length; break; }
            }
            if (src == null) { src = pool[next]; next = (next + 1) % pool.Length; }
            if (src == null) return null;
            src.transform.position = pos;
            src.clip = clip;
            src.volume = v;
            src.pitch = pitch;
            src.minDistance = minDist;
            src.maxDistance = maxDist;
            src.Play();
            return src;
        }

        public static AudioSource MakeLoop(Transform parent, AudioClip clip, float minDist, float maxDist)
        {
            var s = parent.gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.dopplerLevel = 0.4f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minDist;
            s.maxDistance = maxDist;
            s.volume = 0f;
            return s;
        }

        // ------------------------------------------------------------------ clips
        public static AudioClip Cheer(int variant)
        {
            if (cheers == null) cheers = new AudioClip[2];
            variant = Mathf.Clamp(variant, 0, 1);
            if (cheers[variant] == null) cheers[variant] = MakeCheer(variant);
            return cheers[variant];
        }

        public static AudioClip Chime(int note)
        {
            if (chimes == null) chimes = new AudioClip[5];
            note = Mathf.Clamp(note, 0, 4);
            if (chimes[note] == null)
            {
                float[] f = { 1174.66f, 1318.51f, 1479.98f, 1760f, 1975.53f };
                var b = new float[(int)(Rate * 2.6f)];
                ProcAudio.Chime(b, 0, Rate, f[note], 0.8f, 0.55f);
                ProcAudio.Tone(b, 0, Rate, f[note] * 2.76f, 0.12f, 0.25f, 0.002f);
                ProcAudio.Finish(b, 0.8f);
                chimes[note] = ProcAudio.MakeClip("BR_Chime" + note, b, Rate, 1);
            }
            return chimes[note];
        }

        public static AudioClip Rotor
        {
            get
            {
                if (rotor != null) return rotor;
                var b = new float[Rate];                     // exactly 1 s: 6 blade slaps, seamless
                var rng = new XRng(4242);
                for (int k = 0; k < 6; k++)
                {
                    int s = k * Rate / 6;
                    ProcAudio.NoiseBurst(b, s, Rate, 140f, 0.8f, 0.035f, 0.9f, ref rng, true);
                    ProcAudio.NoiseBurst(b, s, Rate, 600f, 1.2f, 0.012f, 0.35f, ref rng, true);
                    ProcAudio.Tone(b, s, Rate, 48f, 0.8f, 0.05f, 0.004f, true);
                }
                for (int k = 0; k < 28; k++)
                    ProcAudio.NoiseBurst(b, k * Rate / 28, Rate, 900f, 2f, 0.006f, 0.1f, ref rng, true);   // tail rotor buzz
                // turbine whine (whole cycles in 1 s -> seamless)
                for (int i = 0; i < b.Length; i++)
                {
                    double t = i / (double)Rate;
                    b[i] += 0.05f * Sin(t * 1180.0) + 0.03f * Sin(t * 2360.0);
                }
                var hiss = ProcAudio.WhiteLoop(b.Length, ref rng);
                ProcAudio.FilterLoop(hiss, Biquad.BandPass(2500f, 0.6f, Rate));
                for (int i = 0; i < b.Length; i++) b[i] += hiss[i] * 0.06f;
                ProcAudio.RemoveDc(b);
                ProcAudio.Normalize(b, 0.85f);
                rotor = ProcAudio.MakeClip("BR_Rotor", b, Rate, 1);
                return rotor;
            }
        }

        public static AudioClip Launch
        {
            get
            {
                if (launch != null) return launch;
                var b = new float[(int)(Rate * 1.1f)];
                var rng = new XRng(77);
                ProcAudio.NoiseBurst(b, 0, Rate, 120f, 0.7f, 0.05f, 1f, ref rng);
                ProcAudio.Tone(b, 0, Rate, 60f, 0.7f, 0.06f, 0.002f);
                double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float f = 900f + 1900f * t;
                    ph += f / Rate;
                    float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1.1f - t) / 0.5f) * 0.18f;
                    b[i] += Sin(ph) * env + rng.Signed * env * 0.25f;
                }
                ProcAudio.Finish(b, 0.8f);
                launch = ProcAudio.MakeClip("BR_FwLaunch", b, Rate, 1);
                return launch;
            }
        }

        public static AudioClip Burst
        {
            get
            {
                if (burst != null) return burst;
                var b = new float[(int)(Rate * 2.4f)];
                var rng = new XRng(991);
                ProcAudio.NoiseBurst(b, 0, Rate, 90f, 0.6f, 0.22f, 1.2f, ref rng);
                ProcAudio.NoiseBurst(b, 0, Rate, 400f, 0.7f, 0.06f, 0.6f, ref rng);
                ProcAudio.Tone(b, 0, Rate, 42f, 0.9f, 0.25f, 0.003f);
                for (int k = 0; k < 70; k++)
                {
                    int s = (int)(Rate * (0.25f + rng.Value * 1.9f));
                    ProcAudio.NoiseBurst(b, s, Rate, 2500f + rng.Value * 3000f, 2f, 0.004f, 0.25f * (1f - s / (float)b.Length), ref rng);
                }
                ProcAudio.Finish(b, 0.9f);
                burst = ProcAudio.MakeClip("BR_FwBurst", b, Rate, 1);
                return burst;
            }
        }

        public static AudioClip Drum
        {
            get
            {
                if (drum != null) return drum;
                var b = new float[(int)(Rate * 0.5f)];
                var rng = new XRng(31);
                double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)Rate;
                    float f = 70f + 60f * Mathf.Exp(-t / 0.03f);
                    ph += f / Rate;
                    b[i] += Sin(ph) * Mathf.Exp(-t / 0.16f) * Mathf.Min(1f, t / 0.002f);
                }
                ProcAudio.NoiseBurst(b, 0, Rate, 300f, 0.8f, 0.012f, 0.5f, ref rng);
                ProcAudio.Finish(b, 0.9f);
                drum = ProcAudio.MakeClip("BR_Drum", b, Rate, 1);
                return drum;
            }
        }

        /// <summary>
        /// A crowd: band-passed roar, two dozen rising "woo" voices (formant-filtered harmonics with
        /// vibrato), scattered hand claps and a couple of whistles. ~3.2 s.
        /// </summary>
        static AudioClip MakeCheer(int variant)
        {
            int n = (int)(Rate * 3.2f);
            var b = new float[n];
            var rng = new XRng((uint)(1234 + variant * 777));
            // roar bed
            var bp1 = Biquad.BandPass(700f, 0.6f, Rate);
            var bp2 = Biquad.BandPass(1600f, 0.9f, Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((3.2f - t) / 1.6f);
                float x = rng.Signed;
                b[i] += (bp1.Process(x) * 0.9f + bp2.Process(x) * 0.4f) * env * 0.55f;
            }
            // voices
            var voice = new float[n];
            for (int v = 0; v < 24; v++)
            {
                System.Array.Clear(voice, 0, n);
                float f0 = rng.Range(170f, 430f), rise = rng.Range(1.15f, 1.5f);
                float start = rng.Range(0f, 0.7f), dur = rng.Range(0.7f, 2.2f);
                float vib = rng.Range(4.5f, 7f), vibAmt = rng.Range(0.01f, 0.03f);
                int s0 = (int)(start * Rate), len = Mathf.Min(n - s0, (int)(dur * Rate));
                double ph = 0;
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)Rate, u = t / dur;
                    float f = f0 * Mathf.Lerp(1f, rise, Mathf.Min(1f, u * 2.5f)) * (1f + vibAmt * Sin(t * vib));
                    ph += f / Rate;
                    float env = Mathf.Clamp01(t / 0.06f) * Mathf.Clamp01((dur - t) / (dur * 0.5f));
                    float x = Sin(ph) + 0.6f * Sin(ph * 2.0) + 0.4f * Sin(ph * 3.0) + 0.25f * Sin(ph * 4.0);
                    voice[s0 + i] = x * env;
                }
                // vowel-ish formant
                var fa = Biquad.BandPass(rng.Range(600f, 900f), 1.4f, Rate);
                var fb = Biquad.BandPass(rng.Range(1000f, 1400f), 1.8f, Rate);
                float gain = rng.Range(0.18f, 0.32f);
                for (int i = s0; i < s0 + len; i++)
                {
                    float x = voice[i];
                    b[i] += (fa.Process(x) + 0.6f * fb.Process(x) + 0.08f * x) * gain;
                }
            }
            // claps
            for (int k = 0; k < 90; k++)
            {
                int s = (int)(Rate * rng.Range(0.2f, 3.0f));
                ProcAudio.NoiseBurst(b, s, Rate, rng.Range(1300f, 2600f), 1.1f, 0.008f, rng.Range(0.15f, 0.35f), ref rng);
            }
            // whistles
            for (int k = 0; k < 2; k++)
            {
                float st = rng.Range(0.3f, 1.4f), du = rng.Range(0.4f, 0.8f), f0 = rng.Range(1900f, 2500f);
                int s0 = (int)(st * Rate), len = Mathf.Min(n - s0, (int)(du * Rate));
                double ph = 0;
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)Rate;
                    float f = f0 * (1f + 0.25f * Mathf.Sin(Mathf.PI * t / du));
                    ph += f / Rate;
                    b[s0 + i] += Sin(ph) * 0.12f * Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((du - t) / 0.1f);
                }
            }
            ProcAudio.Finish(b, 0.85f);
            return ProcAudio.MakeClip("BR_Cheer" + variant, b, Rate, 1);
        }
    }
}
