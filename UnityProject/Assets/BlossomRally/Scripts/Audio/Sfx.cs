// Sfx.cs - short procedural UI / event sounds (menu ticks, countdown, checkpoint chimes, medals,
// camera + photo shutter, whoosh) played 2D through a small pool of AudioSources that keep playing
// while the game is paused. Also hosts ProcAudio: the tiny DSP toolkit shared by every procedural
// audio script (noise, filters, Karplus-Strong pluck, seamless circular loops, clip creation).
// Nothing here touches the Unity API off the main thread; ProcAudio's math is thread-safe.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum SfxId
    {
        MenuMove, MenuSelect, MenuBack, Countdown, CountdownGo, Checkpoint, CheckpointBest, Finish,
        MedalBronze, MedalSilver, MedalGold, Camera, PhotoShutter, Whoosh
    }

    /// <summary>Fire-and-forget 2D sound effects. Sfx.Play(SfxId.MenuSelect).</summary>
    public static class Sfx
    {
        const int Rate = 44100;
        const int PoolSize = 8;

        static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        static AudioSource[] pool;
        static int next;
        static GameObject host;

        // per-sound loudness trim so the set sits together
        static float Trim(SfxId id)
        {
            switch (id)
            {
                case SfxId.MenuMove: return 0.32f;
                case SfxId.MenuSelect: return 0.5f;
                case SfxId.MenuBack: return 0.42f;
                case SfxId.Countdown: return 0.5f;
                case SfxId.CountdownGo: return 0.6f;
                case SfxId.Checkpoint: return 0.5f;
                case SfxId.CheckpointBest: return 0.55f;
                case SfxId.Finish: return 0.7f;
                case SfxId.MedalBronze: return 0.55f;
                case SfxId.MedalSilver: return 0.6f;
                case SfxId.MedalGold: return 0.68f;
                case SfxId.Camera: return 0.3f;
                case SfxId.PhotoShutter: return 0.55f;
                default: return 0.45f;
            }
        }

        /// <summary>Play a sound once (2D). volume is a 0..1 multiplier on top of the SFX setting.</summary>
        public static void Play(SfxId id, float volume = 1f)
        {
            EnsurePool();
            var clip = Clip(id);
            if (clip == null) return;
            AudioSource src = null;
            for (int i = 0; i < pool.Length; i++)
            {
                var s = pool[(next + i) % pool.Length];
                if (s != null && !s.isPlaying) { src = s; next = (next + i + 1) % pool.Length; break; }
            }
            if (src == null) { src = pool[next]; next = (next + 1) % pool.Length; }
            if (src == null) return;
            src.Stop();
            src.clip = clip;
            src.pitch = 1f;
            src.volume = Mathf.Clamp01(volume) * Trim(id) * GameSettings.SfxVolume;
            src.Play();
        }

        /// <summary>Optionally generate every clip up front (a few ms) so the first press never hitches.</summary>
        public static void Preload()
        {
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId))) Clip(id);
        }

        static void EnsurePool()
        {
            if (host != null && pool != null) return;
            host = new GameObject("BR_Sfx");
            Object.DontDestroyOnLoad(host);
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.ignoreListenerPause = true;   // menu sounds keep working while paused
                s.dopplerLevel = 0f;
                s.priority = 32;
                pool[i] = s;
            }
        }

        static AudioClip Clip(SfxId id)
        {
            AudioClip c;
            if (clips.TryGetValue(id, out c) && c != null) return c;
            float[] d = Generate(id);
            c = ProcAudio.MakeClip("BR_Sfx_" + id, d, Rate, 1);
            clips[id] = c;
            return c;
        }

        // ------------------------------------------------------------------ sound design
        static float[] Buf(float seconds) { return new float[(int)(Rate * seconds)]; }
        static int S(float seconds) { return (int)(Rate * seconds); }

        static float[] Generate(SfxId id)
        {
            var rng = new XRng(1234u + (uint)id * 7919u);
            float[] b;
            switch (id)
            {
                case SfxId.MenuMove:
                    b = Buf(0.12f);
                    ProcAudio.Tone(b, 0, Rate, 1480f, 0.8f, 0.022f, 0.002f);
                    ProcAudio.Tone(b, 0, Rate, 2960f, 0.25f, 0.010f, 0.001f);
                    break;
                case SfxId.MenuSelect:
                    b = Buf(0.9f);
                    ProcAudio.Pluck(b, 0, Rate, 587.33, 0.8f, 0.7, 0.75f, ref rng, false);
                    ProcAudio.Pluck(b, S(0.07f), Rate, 880.0, 0.75f, 0.8, 0.75f, ref rng, false);
                    break;
                case SfxId.MenuBack:
                    b = Buf(0.8f);
                    ProcAudio.Pluck(b, 0, Rate, 440.0, 0.7f, 0.6, 0.6f, ref rng, false);
                    ProcAudio.Pluck(b, S(0.07f), Rate, 329.63, 0.65f, 0.7, 0.55f, ref rng, false);
                    break;
                case SfxId.Countdown:
                    b = Buf(0.5f);
                    ProcAudio.Chime(b, 0, Rate, 784f, 0.8f, 0.16f);
                    break;
                case SfxId.CountdownGo:
                    b = Buf(1.2f);
                    ProcAudio.Chime(b, 0, Rate, 1174.66f, 0.7f, 0.45f);
                    ProcAudio.Chime(b, 0, Rate, 1567.98f, 0.45f, 0.4f);
                    ProcAudio.Pluck(b, 0, Rate, 587.33, 0.6f, 1.0, 0.8f, ref rng, false);
                    ProcAudio.Pluck(b, S(0.015f), Rate, 880.0, 0.5f, 1.0, 0.8f, ref rng, false);
                    break;
                case SfxId.Checkpoint:
                    b = Buf(0.8f);
                    ProcAudio.Chime(b, 0, Rate, 1318.51f, 0.7f, 0.22f);
                    ProcAudio.Chime(b, S(0.08f), Rate, 1760f, 0.6f, 0.3f);
                    break;
                case SfxId.CheckpointBest:
                    b = Buf(1.1f);
                    {
                        float[] f = { 1174.66f, 1318.51f, 1479.98f, 1760f };
                        for (int i = 0; i < f.Length; i++) ProcAudio.Chime(b, S(0.06f * i), Rate, f[i], 0.6f, 0.25f + 0.08f * i);
                    }
                    break;
                case SfxId.Finish:
                    b = Buf(2.4f);
                    ProcAudio.Gong(b, 0, Rate, 262f, 0.9f, 0.12f);
                    Gliss(b, S(0.05f), 62, 12, 0.035f, 0.45f, ref rng);
                    break;
                case SfxId.MedalBronze:
                    b = Buf(1.3f);
                    Roll(b, 0, new[] { 62, 66, 69 }, 0.035f, 0.6f, ref rng);
                    ProcAudio.Chime(b, S(0.1f), Rate, 880f, 0.35f, 0.3f);
                    break;
                case SfxId.MedalSilver:
                    b = Buf(1.5f);
                    Roll(b, 0, new[] { 62, 69, 74, 78 }, 0.035f, 0.6f, ref rng);
                    ProcAudio.Chime(b, S(0.14f), Rate, 1760f, 0.4f, 0.4f);
                    break;
                case SfxId.MedalGold:
                    b = Buf(2.3f);
                    Gliss(b, 0, 62, 10, 0.03f, 0.4f, ref rng);
                    Roll(b, S(0.32f), new[] { 74, 78, 81, 86 }, 0.03f, 0.7f, ref rng);
                    ProcAudio.Bell(b, S(0.32f), Rate, 0.35f, 1.2f);
                    ProcAudio.Gong(b, S(0.32f), Rate, 330f, 0.35f, 0.06f);
                    break;
                case SfxId.Camera:
                    b = Buf(0.12f);
                    ProcAudio.NoiseBurst(b, 0, Rate, 3200f, 1.2f, 0.004f, 0.9f, ref rng);
                    ProcAudio.Tone(b, 0, Rate, 900f, 0.4f, 0.015f, 0.001f);
                    ProcAudio.NoiseBurst(b, S(0.03f), Rate, 1800f, 0.8f, 0.03f, 0.18f, ref rng);
                    break;
                case SfxId.PhotoShutter:
                    b = Buf(0.22f);
                    ProcAudio.NoiseBurst(b, 0, Rate, 4200f, 1.5f, 0.003f, 1f, ref rng);
                    ProcAudio.Tone(b, 0, Rate, 1800f, 0.5f, 0.008f, 0.0005f);
                    ProcAudio.NoiseBurst(b, S(0.012f), Rate, 700f, 0.9f, 0.018f, 0.25f, ref rng);
                    ProcAudio.NoiseBurst(b, S(0.072f), Rate, 3400f, 1.5f, 0.003f, 0.8f, ref rng);
                    ProcAudio.Tone(b, S(0.072f), Rate, 1400f, 0.45f, 0.01f, 0.0005f);
                    break;
                default: // Whoosh
                    b = Buf(0.65f);
                    {
                        var bp = new Biquad();
                        int n = b.Length;
                        for (int i = 0; i < n; i++)
                        {
                            float t = i / (float)n;
                            if ((i & 31) == 0)
                            {
                                float fc = 450f + 2200f * Mathf.Sin(t * Mathf.PI) * (1f - 0.35f * t);
                                bp.SetBandPass(fc, 1.4f, Rate);
                            }
                            float env = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.6f);
                            b[i] = bp.Process(rng.Signed) * env;
                        }
                    }
                    break;
            }
            ProcAudio.Finish(b, 0.8f);
            return b;
        }

        static double Hz(int midi) { return 440.0 * System.Math.Pow(2.0, (midi - 69) / 12.0); }

        static void Roll(float[] b, int start, int[] notes, float spacing, float amp, ref XRng rng)
        {
            for (int i = 0; i < notes.Length; i++)
                ProcAudio.Pluck(b, start + S(spacing * i), Rate, Hz(notes[i]), amp * (1f - 0.08f * i), 1.2, 0.75f, ref rng, false);
        }

        /// <summary>Guzheng-style pentatonic glissando (D gong mode) upward from a MIDI note.</summary>
        static void Gliss(float[] b, int start, int fromMidi, int count, float spacing, float amp, ref XRng rng)
        {
            int m = fromMidi;
            for (int i = 0; i < count; i++)
            {
                ProcAudio.Pluck(b, start + S(spacing * i), Rate, Hz(m), amp * (0.7f + 0.3f * i / count), 0.9, 0.8f, ref rng, false);
                m = ProcAudio.NextPentatonic(m);
            }
        }
    }

    // ====================================================================== shared DSP toolkit

    /// <summary>Small fast xorshift RNG (value type, thread-safe per instance).</summary>
    internal struct XRng
    {
        uint s;
        public XRng(uint seed) { s = seed == 0 ? 0x9E3779B9u : seed; }
        public uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        public float Value { get { return (Next() & 0xFFFFFF) / 16777216f; } }
        public float Signed { get { return Value * 2f - 1f; } }
        public float Range(float a, float b) { return a + (b - a) * Value; }
        public int Int(int max) { return (int)(Next() % (uint)max); }
    }

    /// <summary>RBJ biquad (direct form I). Coefficients can be changed while running.</summary>
    internal struct Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;

        public static Biquad LowPass(float f, float q, int rate) { var b = new Biquad(); b.SetLowPass(f, q, rate); return b; }
        public static Biquad HighPass(float f, float q, int rate) { var b = new Biquad(); b.SetHighPass(f, q, rate); return b; }
        public static Biquad BandPass(float f, float q, int rate) { var b = new Biquad(); b.SetBandPass(f, q, rate); return b; }

        void Set(double nb0, double nb1, double nb2, double a0, double na1, double na2)
        {
            b0 = (float)(nb0 / a0); b1 = (float)(nb1 / a0); b2 = (float)(nb2 / a0); a1 = (float)(na1 / a0); a2 = (float)(na2 / a0);
        }
        static void W(float f, float q, int rate, out double cs, out double alpha)
        {
            double w0 = 2.0 * System.Math.PI * System.Math.Min(f, rate * 0.45f) / rate;
            cs = System.Math.Cos(w0);
            alpha = System.Math.Sin(w0) / (2.0 * q);
        }
        public void SetLowPass(float f, float q, int rate)
        {
            double cs, al; W(f, q, rate, out cs, out al);
            Set((1 - cs) * 0.5, 1 - cs, (1 - cs) * 0.5, 1 + al, -2 * cs, 1 - al);
        }
        public void SetHighPass(float f, float q, int rate)
        {
            double cs, al; W(f, q, rate, out cs, out al);
            Set((1 + cs) * 0.5, -(1 + cs), (1 + cs) * 0.5, 1 + al, -2 * cs, 1 - al);
        }
        public void SetBandPass(float f, float q, int rate)
        {
            double cs, al; W(f, q, rate, out cs, out al);
            Set(al, 0, -al, 1 + al, -2 * cs, 1 - al);
        }
        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            if (y > -1e-15f && y < 1e-15f) y1 = 0f;   // flush denormals
            return y;
        }
    }

    internal static class ProcAudio
    {
        public const float TwoPi = 6.2831853f;
        static readonly int[] PentaD = { 2, 4, 6, 9, 11 };   // D gong mode pitch classes

        public static AudioClip MakeClip(string name, float[] data, int rate, int channels)
        {
            var c = AudioClip.Create(name, data.Length / channels, channels, rate, false);
            c.SetData(data, 0);
            c.hideFlags = HideFlags.DontSave;
            return c;
        }

        /// <summary>Gentle saturation: linear for small signals, rounds off peaks (never exceeds 1).</summary>
        public static float SoftClip(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);   // tanh approximation
        }

        /// <summary>Normalize to peak, soft-clip, fade the last 4 ms (one-shot buffers).</summary>
        public static void Finish(float[] b, float peak)
        {
            Normalize(b, 1.15f);
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i]) * peak;
            int f = System.Math.Min(b.Length, 180);
            for (int i = 0; i < f; i++) b[b.Length - 1 - i] *= i / (float)f;
        }

        public static void Normalize(float[] b, float peak)
        {
            float m = 0f;
            for (int i = 0; i < b.Length; i++) { float a = b[i] < 0 ? -b[i] : b[i]; if (a > m) m = a; }
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        /// <summary>Next higher note in the D-gong pentatonic scale.</summary>
        public static int NextPentatonic(int midi) { return NextInScale(midi, PentaD); }

        /// <summary>Next higher note whose pitch class is in pcs.</summary>
        public static int NextInScale(int midi, int[] pcs)
        {
            for (int m = midi + 1; m < midi + 13; m++)
                if (System.Array.IndexOf(pcs, ((m % 12) + 12) % 12) >= 0) return m;
            return midi + 2;
        }

        // ---- circular processing: the buffer is treated as a loop so the result repeats seamlessly ----

        /// <summary>Run a biquad over a looping buffer: one warm-up lap, then the real pass (no seam).</summary>
        public static void FilterLoop(float[] d, Biquad f, int stride = 1, int offset = 0)
        {
            for (int i = offset; i < d.Length; i += stride) f.Process(d[i]);
            for (int i = offset; i < d.Length; i += stride) d[i] = f.Process(d[i]);
        }

        /// <summary>One-pole low pass over a looping buffer (warm-up lap first).</summary>
        public static void OnePoleLoop(float[] d, float cutoff, int rate, int stride = 1, int offset = 0)
        {
            float a = 1f - Mathf.Exp(-TwoPi * cutoff / rate), y = 0f;
            for (int i = offset; i < d.Length; i += stride) y += (d[i] - y) * a;
            for (int i = offset; i < d.Length; i += stride) { y += (d[i] - y) * a; d[i] = y; }
        }

        /// <summary>Remove DC from a looping buffer.</summary>
        public static void RemoveDc(float[] d)
        {
            double s = 0; for (int i = 0; i < d.Length; i++) s += d[i];
            float m = (float)(s / d.Length);
            for (int i = 0; i < d.Length; i++) d[i] -= m;
        }

        public static float[] WhiteLoop(int n, ref XRng rng)
        {
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = rng.Signed;
            return d;
        }

        // ---- building blocks (wrap = write modulo length, for loops) ----

        /// <summary>Sine with exponential decay and a short linear attack.</summary>
        public static void Tone(float[] b, int start, int rate, float freq, float amp, float decay, float attack, bool wrap = false)
        {
            int n = (int)(rate * decay * 7f);
            int L = b.Length;
            double w = TwoPi * freq / rate;
            for (int i = 0; i < n; i++)
            {
                int o = start + i;
                if (o >= L) { if (!wrap) break; o %= L; }
                float t = i / (float)rate;
                float env = Mathf.Exp(-t / decay) * (attack > 0f ? Mathf.Min(1f, t / attack) : 1f);
                b[o] += (float)System.Math.Sin(w * i) * amp * env;
            }
        }

        /// <summary>Soft bell / chime: sine plus slightly detuned overtones.</summary>
        public static void Chime(float[] b, int start, int rate, float freq, float amp, float decay, bool wrap = false)
        {
            Tone(b, start, rate, freq, amp, decay, 0.003f, wrap);
            Tone(b, start, rate, freq * 2.005f, amp * 0.28f, decay * 0.5f, 0.002f, wrap);
            Tone(b, start, rate, freq * 3.01f, amp * 0.10f, decay * 0.3f, 0.002f, wrap);
            Tone(b, start, rate, freq * 0.5f, amp * 0.12f, decay * 1.2f, 0.004f, wrap);
        }

        /// <summary>Small finger-cymbal shimmer (inharmonic partials, long decay).</summary>
        public static void Bell(float[] b, int start, int rate, float amp, float decay, bool wrap = false)
        {
            float[] f = { 2950f, 4130f, 5520f, 7010f };
            float[] a = { 1f, 0.6f, 0.4f, 0.25f };
            for (int i = 0; i < f.Length; i++)
                Tone(b, start, rate, f[i], amp * a[i] * 0.5f, decay * (1f - 0.18f * i), 0.001f, wrap);
        }

        /// <summary>Small Chinese opera gong: inharmonic partials whose pitch rises slightly.</summary>
        public static void Gong(float[] b, int start, int rate, float f0, float amp, float rise)
        {
            float[] ratio = { 1f, 1.52f, 2.03f, 2.71f, 3.4f };
            float[] a = { 1f, 0.6f, 0.45f, 0.3f, 0.2f };
            float[] dec = { 1.5f, 1.1f, 0.85f, 0.6f, 0.4f };
            int L = b.Length;
            for (int p = 0; p < ratio.Length; p++)
            {
                double ph = 0;
                int n = System.Math.Min(L - start, (int)(rate * dec[p] * 6f));
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float f = f0 * ratio[p] * (1f + rise * (1f - Mathf.Exp(-t / 0.15f)));
                    ph += TwoPi * f / rate;
                    float env = Mathf.Exp(-t / dec[p]) * Mathf.Min(1f, t / 0.002f);
                    b[start + i] += (float)System.Math.Sin(ph) * amp * a[p] * env * 0.5f;
                }
            }
        }

        /// <summary>Band-passed noise burst with exponential decay.</summary>
        public static void NoiseBurst(float[] b, int start, int rate, float freq, float q, float decay, float amp, ref XRng rng, bool wrap = false)
        {
            var bp = Biquad.BandPass(freq, q, rate);
            int n = (int)(rate * decay * 7f) + 8;
            int L = b.Length;
            for (int i = 0; i < n; i++)
            {
                int o = start + i;
                if (o >= L) { if (!wrap) break; o %= L; }
                float env = Mathf.Exp(-i / (rate * decay));
                b[o] += bp.Process(rng.Signed) * env * amp * 2f;
            }
        }

        /// <summary>
        /// Karplus-Strong plucked string (guzheng-like) with all-pass fractional tuning.
        /// sustain = seconds to -60 dB, bright = 0..1 excitation brightness. With wrap the tail
        /// continues at the start of the buffer (for seamless loops). stride/channel write interleaved.
        /// </summary>
        public static void Pluck(float[] b, int start, int rate, double freq, float amp, double sustain, float bright,
                                 ref XRng rng, bool wrap, int stride = 1, int channel = 0)
        {
            double P = rate / freq - 0.5;
            int N = (int)System.Math.Floor(P);
            double frac = P - N;
            if (frac < 0.15) { N -= 1; frac += 1.0; }
            if (N < 2) N = 2;
            float C = (float)((1.0 - frac) / (1.0 + frac));
            var dl = new float[N];
            float lp = 0f, k = 0.06f + 0.36f * Mathf.Clamp01(bright);   // softened noise burst
            float mean = 0f;
            for (int i = 0; i < N; i++) { lp += (rng.Signed - lp) * k; dl[i] = lp; mean += lp; }
            mean /= N;
            int pos = System.Math.Max(1, N / 7);               // pluck position comb (near the bridge)
            var ex = new float[N];
            for (int i = 0; i < N; i++) ex[i] = (dl[i] - mean) - 0.55f * (dl[(i + N - pos) % N] - mean);
            System.Array.Copy(ex, dl, N);

            float rho = (float)System.Math.Pow(0.001, 1.0 / (freq * sustain));
            int len = (int)(rate * System.Math.Min(sustain * 1.1, 5.0));
            int fade = System.Math.Max(64, len / 6);
            int frames = b.Length / stride;
            int idx = 0;
            float prev = 0f, apx = 0f, apy = 0f;
            float atk = rate * 0.0015f;                       // 1.5 ms attack: soft but crisp
            for (int n = 0; n < len; n++)
            {
                float x = dl[idx];
                float avg = 0.5f * (x + prev) * rho;
                prev = x;
                float ap = C * avg + apx - C * apy;
                apx = avg; apy = ap;
                dl[idx] = ap;
                if (++idx == N) idx = 0;
                int o = start + n;
                if (o >= frames) { if (!wrap) break; o %= frames; }
                float env = n > len - fade ? (len - n) / (float)fade : 1f;
                if (n < atk) env *= n / atk;
                b[o * stride + channel] += x * amp * env;
            }
        }
    }
}
