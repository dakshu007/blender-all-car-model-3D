// AmbientAudio.cs - procedural world ambience (no audio files): birdsong by day, crickets at night,
// a wind bed that follows Atmosphere.Gust, a rain loop for Atmosphere.RainIntensity and a gentle
// river / lake babble that swells near water. All loops are seamless circular buffers generated once
// (static cache); birds are short one-shot chirps scheduled at random. 2D, very light on CPU.
using UnityEngine;

namespace BlossomRally
{
    public class AmbientAudio : MonoBehaviour
    {
        const int Rate = 22050;

        static AmbientAudio inst;
        static AudioClip windClip, rainClip, cricketClip, waterClip;
        static AudioClip[] birdClips;

        AudioSource wind, rain, crickets, water;
        AudioSource[] birds = new AudioSource[3];
        float nextBird, waterNear, waterCheck;
        WorldBuilder world;
        float worldSearch;

        /// <summary>Create the persistent ambience object if it does not exist yet.</summary>
        public static AmbientAudio Ensure()
        {
            if (inst != null) return inst;
            var go = new GameObject("BR_AmbientAudio");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<AmbientAudio>();
            return inst;
        }

        void Awake()
        {
            if (inst != null && inst != this) { Destroy(gameObject); return; }
            inst = this;
            Generate();
            wind = Loop(windClip);
            rain = Loop(rainClip);
            crickets = Loop(cricketClip);
            water = Loop(waterClip);
            for (int i = 0; i < birds.Length; i++)
            {
                birds[i] = gameObject.AddComponent<AudioSource>();
                Setup(birds[i]);
            }
            nextBird = Time.time + 1.5f;
        }

        void OnDestroy() { if (inst == this) inst = null; }

        static void Setup(AudioSource s)
        {
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            s.priority = 160;
        }

        AudioSource Loop(AudioClip c)
        {
            var s = gameObject.AddComponent<AudioSource>();
            Setup(s);
            s.clip = c;
            s.loop = true;
            s.Play();
            s.timeSamples = Random.Range(0, c.samples);
            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;    // paused: AudioListener.pause silences these sources
            float sfx = GameSettings.SfxVolume;
            float rainI = Mathf.Clamp01(Atmosphere.RainIntensity) * RegionRuntime.RainVisual;
            float night = Mathf.Clamp01(Atmosphere.NightFactor);
            float gust = Mathf.Max(0f, Atmosphere.Gust);
            // regions: high country is windy, snow and desert have few birds, the city has no crickets
            float pSnow = RegionRuntime.Presence(RegionId.Snow), pMtn = RegionRuntime.Presence(RegionId.Mountains);
            float pCanyon = RegionRuntime.Presence(RegionId.Canyon), pCity = RegionRuntime.Presence(RegionId.City);
            float windy = 1f + 1.4f * pSnow + 0.6f * pMtn + 0.4f * pCanyon + 1.2f * pSnow * Mathf.Clamp01(Atmosphere.RainIntensity);
            float birdsHere = Mathf.Clamp01(1f - 0.85f * pSnow - 0.7f * pCanyon - 0.55f * pCity);
            float bugsHere = Mathf.Clamp01(1f - pSnow - 0.6f * pCity);

            Smooth(wind, (0.035f + 0.075f * Mathf.Min(gust, 1.5f) + 0.03f * rainI) * windy * sfx, dt, 1.5f);
            wind.pitch = 0.85f + 0.25f * Mathf.Min(gust, 1.5f);
            Smooth(rain, 0.3f * rainI * sfx, dt, 0.8f);
            Smooth(crickets, 0.05f * bugsHere * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, night)) * (1f - 0.75f * rainI) * sfx, dt, 0.5f);

            // water: louder near the river / lake (global and very subtle when the world is unknown)
            waterCheck -= dt;
            if (waterCheck <= 0f)
            {
                waterCheck = 0.5f;
                waterNear = WaterProximity();
            }
            Smooth(water, (0.008f + 0.11f * waterNear) * (1f - 0.4f * rainI) * sfx, dt, 0.6f);

            // birdsong by day
            float day = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.6f, night));
            float birdGain = day * (1f - 0.9f * rainI) * birdsHere;
            if (Time.time >= nextBird)
            {
                nextBird = Time.time + Random.Range(0.7f, 4.2f) / Mathf.Max(0.3f, birdGain);
                if (birdGain > 0.05f) Chirp(birdGain * sfx);
            }
        }

        static void Smooth(AudioSource s, float target, float dt, float rate)
        {
            s.volume = Mathf.Lerp(s.volume, target, 1f - Mathf.Exp(-dt * rate));   // exponential glide
        }

        void Chirp(float gain)
        {
            var src = PickBird();
            if (src == null) return;
            int v = Random.Range(0, birdClips.Length);
            src.clip = birdClips[v];
            src.panStereo = Random.Range(-0.75f, 0.75f);
            src.pitch = Random.Range(0.9f, 1.12f);
            src.volume = Random.Range(0.035f, 0.09f) * gain;
            src.Play();
            // sometimes a second bird answers with the same call
            if (Random.value < 0.3f)
            {
                var other = PickBird();
                if (other != null && other != src)
                {
                    other.clip = birdClips[v];
                    other.panStereo = -src.panStereo * Random.Range(0.5f, 1f);
                    other.pitch = src.pitch * Random.Range(0.95f, 1.05f);
                    other.volume = src.volume * 0.6f;
                    other.PlayDelayed(Random.Range(0.35f, 0.8f));
                }
            }
        }

        AudioSource PickBird()
        {
            for (int i = 0; i < birds.Length; i++) if (!birds[i].isPlaying) return birds[i];
            return null;
        }

        float WaterProximity()
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            if (world == null && Time.time >= worldSearch)
            {
                worldSearch = Time.time + 3f;
                var all = Resources.FindObjectsOfTypeAll<WorldBuilder>();
                for (int i = 0; i < all.Length; i++)
                    if (all[i] != null && all[i].gameObject.scene.IsValid()) { world = all[i]; break; }
            }
            Vector3 p3 = cam.transform.position;
            var p = new Vector2(p3.x, p3.z);
            float near = 0f;
            try
            {
                if (world != null) near = 1f - Mathf.InverseLerp(12f, 70f, world.RiverDistance(p));
                near = Mathf.Max(near, 1f - Mathf.InverseLerp(1.0f, 1.45f, MapData.LakeK(p)));
            }
            catch (System.Exception) { near = 0f; }
            return Mathf.Clamp01(near);
        }

        // ================================================================= generation (cached)

        static void Generate()
        {
            if (windClip != null && rainClip != null && cricketClip != null && waterClip != null && birdClips != null && birdClips[0] != null) return;
            var rng = new XRng(31337u);
            windClip = ProcAudio.MakeClip("BR_Amb_Wind", WindLoop(ref rng), Rate, 1);
            rainClip = ProcAudio.MakeClip("BR_Amb_Rain", RainLoop(ref rng), Rate, 1);
            cricketClip = ProcAudio.MakeClip("BR_Amb_Crickets", CricketLoop(ref rng), Rate, 1);
            waterClip = ProcAudio.MakeClip("BR_Amb_Water", WaterLoop(ref rng), Rate, 1);
            birdClips = new AudioClip[8];
            for (int i = 0; i < birdClips.Length; i++)
                birdClips[i] = ProcAudio.MakeClip("BR_Amb_Bird" + i, Bird(i, ref rng), Rate, 1);
        }

        // soft rushing wind: brown noise + airy band, slow swells (integer cycles per loop)
        static float[] WindLoop(ref XRng rng)
        {
            int L = Rate * 4;
            var lo = ProcAudio.WhiteLoop(L, ref rng);
            var hi = (float[])lo.Clone();
            ProcAudio.OnePoleLoop(lo, 220f, Rate);
            ProcAudio.OnePoleLoop(lo, 400f, Rate);
            ProcAudio.FilterLoop(hi, Biquad.BandPass(700f, 0.9f, Rate));
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                float swell = 0.65f + 0.35f * Mathf.Sin(ProcAudio.TwoPi * t + 0.4f) * Mathf.Sin(ProcAudio.TwoPi * 3f * t);
                lo[i] = (lo[i] * 5f + hi[i] * 0.35f) * swell;
            }
            ProcAudio.RemoveDc(lo);
            ProcAudio.Normalize(lo, 0.8f);
            return lo;
        }

        // rain: dense high hiss + random droplet ticks
        static float[] RainLoop(ref XRng rng)
        {
            int L = Rate * 3;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(d, Biquad.HighPass(900f, 0.7f, Rate));
            ProcAudio.FilterLoop(d, Biquad.LowPass(6500f, 0.7f, Rate));
            var body = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.OnePoleLoop(body, 500f, Rate);
            for (int i = 0; i < L; i++) d[i] = d[i] * 0.45f + body[i] * 0.6f;
            for (int k = 0; k < 900; k++)
            {
                float a = rng.Value; a = a * a * 0.6f;
                ProcAudio.NoiseBurst(d, rng.Int(L), Rate, rng.Range(1800f, 6000f), 2f, rng.Range(0.0008f, 0.0025f), a, ref rng, true);
            }
            ProcAudio.RemoveDc(d);
            ProcAudio.Normalize(d, 0.8f);
            return d;
        }

        // crickets: two insects with pulsed chirps (integer-Hz carriers over 4 s) + a faint far chorus
        static float[] CricketLoop(ref XRng rng)
        {
            int L = Rate * 4;
            var d = new float[L];
            Cricket(d, 4400f, 0.43f, 3, 0.9f, ref rng);
            Cricket(d, 4950f, 0.37f, 4, 0.55f, ref rng);
            Cricket(d, 4100f, 0.61f, 2, 0.35f, ref rng);
            var chorus = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(chorus, Biquad.BandPass(4600f, 6f, Rate));
            for (int i = 0; i < L; i++)
            {
                float t = i / (float)L;
                d[i] += chorus[i] * 0.25f * (0.6f + 0.4f * Mathf.Sin(ProcAudio.TwoPi * 37f * t));
            }
            ProcAudio.Normalize(d, 0.75f);
            return d;
        }

        static void Cricket(float[] d, float carrier, float every, int pulses, float amp, ref XRng rng)
        {
            int L = d.Length;
            float t0 = rng.Value * every;
            int pulseLen = (int)(Rate * 0.014f), gap = (int)(Rate * 0.026f);
            for (float t = t0; t < L / (float)Rate; t += every * rng.Range(0.92f, 1.08f))
            {
                if (rng.Value < 0.12f) continue;   // occasional pause
                int s0 = (int)(t * Rate);
                for (int p = 0; p < pulses; p++)
                {
                    int s = s0 + p * (pulseLen + gap);
                    for (int i = 0; i < pulseLen; i++)
                    {
                        int o = (s + i) % L;
                        float env = Mathf.Sin(Mathf.PI * i / pulseLen); env *= env;
                        d[o] += Mathf.Sin(ProcAudio.TwoPi * carrier * o / Rate) * env * amp;
                    }
                }
            }
        }

        // river babble: low watery noise bed + many small rising "bloops"
        static float[] WaterLoop(ref XRng rng)
        {
            int L = Rate * 4;
            var d = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(d, Biquad.BandPass(520f, 0.7f, Rate));
            var hiss = ProcAudio.WhiteLoop(L, ref rng);
            ProcAudio.FilterLoop(hiss, Biquad.BandPass(2400f, 0.8f, Rate));
            for (int i = 0; i < L; i++) d[i] = d[i] * 0.8f + hiss[i] * 0.12f;
            for (int k = 0; k < 110; k++)
            {
                int at = rng.Int(L);
                float f0 = rng.Range(280f, 700f), rise = rng.Range(1.3f, 2.2f), dur = rng.Range(0.018f, 0.05f);
                float a = rng.Range(0.15f, 0.5f);
                int n = (int)(dur * Rate);
                double ph = 0;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)n;
                    ph += ProcAudio.TwoPi * f0 * (1f + (rise - 1f) * t) / Rate;
                    float env = Mathf.Sin(Mathf.PI * t);
                    d[(at + i) % L] += (float)System.Math.Sin(ph) * env * a;
                }
            }
            ProcAudio.FilterLoop(d, Biquad.LowPass(3000f, 0.7f, Rate));
            ProcAudio.RemoveDc(d);
            ProcAudio.Normalize(d, 0.75f);
            return d;
        }

        // a handful of bird calls: falling chirp runs, trills, two-note whistles, warbles
        static float[] Bird(int variant, ref XRng rng)
        {
            float len = 0.9f;
            var d = new float[(int)(Rate * len)];
            int kind = variant % 4;
            if (kind == 0)
            {   // run of 2-4 quick falling chirps
                int count = 2 + rng.Int(3);
                float f = rng.Range(3600f, 4600f);
                for (int c = 0; c < count; c++)
                    Sweep(d, (int)(Rate * (0.02f + c * rng.Range(0.09f, 0.13f))), 0.07f, f, f * 0.62f, 0f, 0f, 0.8f - 0.1f * c);
            }
            else if (kind == 1)
            {   // trill
                float f = rng.Range(2800f, 3600f);
                Sweep(d, (int)(Rate * 0.02f), rng.Range(0.3f, 0.5f), f, f * 1.05f, rng.Range(22f, 32f), rng.Range(350f, 650f), 0.7f);
            }
            else if (kind == 2)
            {   // two-note whistle "fee-bee"
                float f = rng.Range(3000f, 3500f);
                Sweep(d, (int)(Rate * 0.02f), 0.22f, f, f * 1.02f, 0f, 0f, 0.8f);
                Sweep(d, (int)(Rate * 0.3f), 0.28f, f * 0.82f, f * 0.8f, 5f, 40f, 0.7f);
            }
            else
            {   // warble: wandering pitch with fast flutter
                float f = rng.Range(2500f, 3300f);
                Sweep(d, (int)(Rate * 0.02f), 0.16f, f, f * 1.4f, 18f, 200f, 0.6f);
                Sweep(d, (int)(Rate * 0.2f), 0.14f, f * 1.3f, f * 0.95f, 18f, 200f, 0.55f);
                Sweep(d, (int)(Rate * 0.36f), 0.2f, f * 1.1f, f * 1.5f, 24f, 260f, 0.6f);
            }
            ProcAudio.Finish(d, 0.8f);
            return d;
        }

        static void Sweep(float[] d, int start, float dur, float f0, float f1, float fmRate, float fmDepth, float amp)
        {
            int n = (int)(dur * Rate);
            double ph = 0;
            for (int i = 0; i < n && start + i < d.Length; i++)
            {
                float t = i / (float)n;
                float tt = i / (float)Rate;
                float f = Mathf.Lerp(f0, f1, t) + fmDepth * Mathf.Sin(ProcAudio.TwoPi * fmRate * tt);
                ph += ProcAudio.TwoPi * f / Rate;
                float env = Mathf.Sin(Mathf.PI * t);
                env *= env;
                d[start + i] += ((float)System.Math.Sin(ph) + 0.15f * (float)System.Math.Sin(2 * ph)) * env * amp;
            }
        }
    }
}
