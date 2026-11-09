// JuiceAudio.cs - procedural clips for the juice layer (generated once, cached): stones pinging in the
// wheel arches and knocking the floor, branch / grass scrape, twig snaps, a doppler fly-by whoosh,
// cockpit wind roar, water splash + churn, and the slow-motion / medal stamp / confetti accents.
// Loops are circular buffers (ProcAudio helpers) so they repeat without a seam.
using UnityEngine;

namespace BlossomRally
{
    internal static class JuiceAudio
    {
        const int Rate = 22050;
        static bool made;

        public static AudioClip[] StoneClicks, FloorKnocks, TwigSnaps;
        public static AudioClip StoneRattle, ScrapeLoop, Whoosh, WindRoarLoop, Splash, SplashSmall, ChurnLoop;
        public static AudioClip SlowMoIn, Stamp, Confetti, NearMiss;

        static int S(float s) { return (int)(Rate * s); }

        public static void Ensure()
        {
            if (made) return;
            made = true;
            var rng = new XRng(0xB10550u);
            StoneClicks = new AudioClip[4];
            for (int i = 0; i < StoneClicks.Length; i++) StoneClicks[i] = Make("BR_JStone" + i, StoneClick(ref rng, i));
            FloorKnocks = new AudioClip[3];
            for (int i = 0; i < FloorKnocks.Length; i++) FloorKnocks[i] = Make("BR_JKnock" + i, FloorKnock(ref rng, i));
            TwigSnaps = new AudioClip[3];
            for (int i = 0; i < TwigSnaps.Length; i++) TwigSnaps[i] = Make("BR_JTwig" + i, TwigSnap(ref rng));
            StoneRattle = Make("BR_JRattle", Rattle(ref rng));
            ScrapeLoop = Loop("BR_JScrape", Scrape(ref rng));
            Whoosh = Make("BR_JWhoosh", FlyBy(ref rng));
            WindRoarLoop = Loop("BR_JWind", WindRoar(ref rng));
            Splash = Make("BR_JSplash", SplashClip(ref rng, 1f));
            SplashSmall = Make("BR_JSplashS", SplashClip(ref rng, 0.45f));
            ChurnLoop = Loop("BR_JChurn", Churn(ref rng));
            SlowMoIn = Make("BR_JSlowMo", SlowMoClip(ref rng));
            Stamp = Make("BR_JStamp", StampClip(ref rng));
            Confetti = Make("BR_JConfetti", ConfettiClip(ref rng));
            NearMiss = Make("BR_JNearMiss", NearMissClip(ref rng));
        }

        static AudioClip Make(string name, float[] b) { ProcAudio.Finish(b, 0.85f); return ProcAudio.MakeClip(name, b, Rate, 1); }
        static AudioClip Loop(string name, float[] b) { ProcAudio.RemoveDc(b); ProcAudio.Normalize(b, 0.8f); return ProcAudio.MakeClip(name, b, Rate, 1); }

        // a single pebble pinging off the inside of the wheel arch: bright click + short metallic ring
        static float[] StoneClick(ref XRng rng, int v)
        {
            var b = new float[S(0.09f)];
            ProcAudio.NoiseBurst(b, 0, Rate, rng.Range(2600f, 4800f), 1.6f, rng.Range(0.0015f, 0.003f), 1f, ref rng);
            ProcAudio.Tone(b, 0, Rate, rng.Range(1700f, 3200f) * (1f + 0.1f * v), 0.35f, rng.Range(0.006f, 0.012f), 0.0003f);
            ProcAudio.Tone(b, 0, Rate, rng.Range(4200f, 6000f), 0.15f, 0.004f, 0.0002f);
            return b;
        }

        // a stone knocking the floor pan: dull "tonk"
        static float[] FloorKnock(ref XRng rng, int v)
        {
            var b = new float[S(0.16f)];
            ProcAudio.Tone(b, 0, Rate, 160f + 60f * v + rng.Range(-15f, 15f), 0.8f, 0.035f, 0.001f);
            ProcAudio.Tone(b, 0, Rate, 420f + 80f * v, 0.3f, 0.018f, 0.0006f);
            ProcAudio.NoiseBurst(b, 0, Rate, 900f, 1.0f, 0.006f, 0.6f, ref rng);
            return b;
        }

        // a handful of gravel thrown up at once
        static float[] Rattle(ref XRng rng)
        {
            var b = new float[S(0.32f)];
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                int at = S(rng.Range(0f, 0.22f));
                float a = rng.Range(0.3f, 1f);
                ProcAudio.NoiseBurst(b, at, Rate, rng.Range(2200f, 5000f), 1.5f, rng.Range(0.0012f, 0.0026f), a, ref rng);
                ProcAudio.Tone(b, at, Rate, rng.Range(1500f, 3400f), 0.25f * a, 0.007f, 0.0003f);
            }
            ProcAudio.Tone(b, S(0.02f), Rate, 190f, 0.4f, 0.03f, 0.001f);
            return b;
        }

        static float[] TwigSnap(ref XRng rng)
        {
            var b = new float[S(0.12f)];
            ProcAudio.NoiseBurst(b, 0, Rate, rng.Range(2200f, 3600f), 1.3f, 0.0025f, 1f, ref rng);
            ProcAudio.NoiseBurst(b, S(rng.Range(0.012f, 0.03f)), Rate, rng.Range(1200f, 2000f), 1.1f, 0.006f, 0.6f, ref rng);
            ProcAudio.Tone(b, 0, Rate, rng.Range(600f, 900f), 0.2f, 0.01f, 0.0005f);
            return b;
        }

        // leaves and twigs dragging along the bodywork (2 s seamless loop)
        static float[] Scrape(ref XRng rng)
        {
            int L = Rate * 2;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(d, Biquad.BandPass(3200f, 0.7f, Rate));
            var lo = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(lo, Biquad.BandPass(700f, 0.8f, Rate));
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                float sway = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(ProcAudio.TwoPi * 6f * t) * Mathf.Sin(ProcAudio.TwoPi * 3.5f * t + 0.6f));
                d[i] = d[i] * 0.6f * sway + lo[i] * 0.35f * (0.6f + 0.4f * Mathf.Sin(ProcAudio.TwoPi * 4f * t));
            }
            for (int g = 0; g < 160; g++)    // twig scratches
                ProcAudio.NoiseBurst(d, rng.Int(L), Rate, rng.Range(1500f, 4200f), 2f, rng.Range(0.002f, 0.008f), rng.Range(0.1f, 0.5f), ref rng, true);
            ProcAudio.FilterLoop(d, Biquad.LowPass(6500f, 0.7f, Rate));
            return d;
        }

        // fly-by: band noise that rises, peaks and falls in pitch (doppler-ish), 0.75 s
        static float[] FlyBy(ref XRng rng)
        {
            var b = new float[S(0.75f)];
            var bp = new Biquad();
            var lp = new Biquad();
            int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if ((i & 15) == 0)
                {
                    // approach: high and rising, pass at t = 0.35, then a falling tail
                    float fc = t < 0.35f ? Mathf.Lerp(1300f, 2300f, t / 0.35f) : Mathf.Lerp(1600f, 420f, Mathf.Sqrt((t - 0.35f) / 0.65f));
                    bp.SetBandPass(fc, 1.1f, Rate);
                    lp.SetLowPass(fc * 0.5f, 0.7f, Rate);
                }
                float env = t < 0.35f ? Mathf.Pow(t / 0.35f, 2.2f) : Mathf.Exp(-(t - 0.35f) * 7f);
                float x = rng.Signed;
                b[i] = (bp.Process(x) * 1.0f + lp.Process(x) * 0.5f) * env;
            }
            return b;
        }

        // cockpit wind roar with buffeting (2 s loop; modulation frequencies are whole cycles per loop)
        static float[] WindRoar(ref XRng rng)
        {
            int L = Rate * 2;
            var low = ProcAudio.WhiteLoop(L, ref rng);
            var mid = (float[])low.Clone();
            var hi = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.OnePoleLoop(low, 140f, Rate);
            ProcAudio.OnePoleLoop(low, 200f, Rate);
            ProcAudio.FilterLoop(mid, Biquad.BandPass(520f, 0.8f, Rate));
            ProcAudio.FilterLoop(hi, Biquad.BandPass(2400f, 1.2f, Rate));
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                float buffet = 0.75f + 0.15f * Mathf.Sin(ProcAudio.TwoPi * 7f * t) + 0.1f * Mathf.Sin(ProcAudio.TwoPi * 13f * t + 1.3f);
                low[i] = (low[i] * 9f + mid[i] * 0.55f) * buffet + hi[i] * 0.08f * (0.6f + 0.4f * Mathf.Sin(ProcAudio.TwoPi * 3f * t));
            }
            ProcAudio.FilterLoop(low, Biquad.HighPass(35f, 0.7f, Rate));
            return low;
        }

        // splash: a heavy low slap, a spray hiss and bubbly droplets falling back
        static float[] SplashClip(ref XRng rng, float size)
        {
            var b = new float[S(0.6f + 0.6f * size)];
            int n = b.Length;
            var lp = Biquad.LowPass(900f + 500f * size, 0.8f, Rate);
            var hp = Biquad.BandPass(3400f, 0.7f, Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = rng.Signed;
                float slap = Mathf.Exp(-t / (0.05f + 0.06f * size)) * Mathf.Min(1f, t / 0.004f);
                float spray = Mathf.Exp(-t / (0.18f + 0.2f * size)) * Mathf.Min(1f, t / 0.03f);
                b[i] = lp.Process(x) * slap * 2.2f + hp.Process(x) * spray * 0.7f;
            }
            int drops = (int)(14 + 30 * size);
            for (int k = 0; k < drops; k++)
            {
                int at = S(rng.Range(0.05f, 0.5f + 0.4f * size));
                float f = rng.Range(700f, 2400f);
                ProcAudio.Tone(b, at, Rate, f, rng.Range(0.05f, 0.18f), rng.Range(0.006f, 0.016f), 0.0008f);
                ProcAudio.Tone(b, at, Rate, f * 1.5f, 0.04f, 0.005f, 0.0005f);
            }
            return b;
        }

        // wading: sloshing low water noise with bubbles (2 s loop)
        static float[] Churn(ref XRng rng)
        {
            int L = Rate * 2;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            var h = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.OnePoleLoop(d, 420f, Rate);
            ProcAudio.FilterLoop(h, Biquad.BandPass(2600f, 0.8f, Rate));
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                float slosh = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(ProcAudio.TwoPi * 5f * t + 0.4f * Mathf.Sin(ProcAudio.TwoPi * 2f * t)));
                d[i] = d[i] * 3.5f * slosh + h[i] * 0.25f * slosh;
            }
            for (int k = 0; k < 90; k++)
                ProcAudio.Tone(d, rng.Int(L), Rate, rng.Range(500f, 1800f), rng.Range(0.04f, 0.14f), rng.Range(0.006f, 0.015f), 0.0008f, true);
            return d;
        }

        // slow-motion accent: deep falling "whomp" with a soft air swell
        static float[] SlowMoClip(ref XRng rng)
        {
            var b = new float[S(1.1f)];
            int n = b.Length;
            double ph = 0;
            var bp = new Biquad();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(150f, 42f, Mathf.Sqrt(Mathf.Clamp01(t / 0.9f)));
                ph += ProcAudio.TwoPi * f / Rate;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t / 0.35f);
                if ((i & 31) == 0) bp.SetBandPass(Mathf.Lerp(1400f, 300f, Mathf.Clamp01(t / 0.8f)), 1.0f, Rate);
                float air = bp.Process(rng.Signed) * Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI) * 0.35f;
                b[i] = (float)System.Math.Sin(ph) * env + air;
            }
            return b;
        }

        // medal stamp: heavy wooden thud + paper slap
        static float[] StampClip(ref XRng rng)
        {
            var b = new float[S(0.6f)];
            ProcAudio.Tone(b, 0, Rate, 68f, 1f, 0.12f, 0.002f);
            ProcAudio.Tone(b, 0, Rate, 132f, 0.5f, 0.05f, 0.001f);
            ProcAudio.NoiseBurst(b, 0, Rate, 420f, 0.9f, 0.025f, 0.9f, ref rng);
            ProcAudio.NoiseBurst(b, 0, Rate, 2600f, 1.4f, 0.003f, 0.6f, ref rng);
            return b;
        }

        // confetti cannons: two pops and a sparkle
        static float[] ConfettiClip(ref XRng rng)
        {
            var b = new float[S(1.2f)];
            ProcAudio.NoiseBurst(b, 0, Rate, 1100f, 0.9f, 0.012f, 1f, ref rng);
            ProcAudio.Tone(b, 0, Rate, 240f, 0.4f, 0.03f, 0.001f);
            ProcAudio.NoiseBurst(b, S(0.11f), Rate, 1300f, 0.9f, 0.01f, 0.8f, ref rng);
            ProcAudio.Tone(b, S(0.11f), Rate, 260f, 0.35f, 0.03f, 0.001f);
            ProcAudio.Bell(b, S(0.16f), Rate, 0.25f, 0.6f);
            for (int k = 0; k < 18; k++)
                ProcAudio.Tone(b, S(rng.Range(0.15f, 0.9f)), Rate, rng.Range(2400f, 5200f), rng.Range(0.03f, 0.08f), 0.03f, 0.001f);
            return b;
        }

        // near miss accent: a quick airy "fft" swish
        static float[] NearMissClip(ref XRng rng)
        {
            var b = new float[S(0.35f)];
            var bp = new Biquad();
            int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if ((i & 15) == 0) bp.SetBandPass(Mathf.Lerp(3800f, 900f, t), 1.3f, Rate);
                float env = Mathf.Min(1f, t / 0.08f) * Mathf.Exp(-t * 6f);
                b[i] = bp.Process(rng.Signed) * env;
            }
            return b;
        }
    }
}
