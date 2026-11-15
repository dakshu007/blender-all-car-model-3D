// LifeAudio.cs - street sound for the living world (procedural, no audio files; ProcAudio helpers):
//   * city hum: a soft 2D loop of distant traffic that swells with Jade Harbour City presence and the number
//     of AI vehicles nearby (quieter at night);
//   * passing engine: one 3D loop riding on the nearest moving AI vehicle, pitch from its speed;
//   * one-shots on a pool of three 3D sources: two-tone horns (blocked by the player, a bump), the bus's door
//     hiss at stops, and a soft cartoon "bonk" when a pedestrian stumbles (cosy, never painful).
// Volumes follow GameSettings.SfxVolume; AudioListener.pause (game pause) silences everything.
using UnityEngine;

namespace BlossomRally
{
    internal sealed class LifeAudio
    {
        const int Rate = 22050;
        static AudioClip humClip, engineClip, hornClip, hissClip, bonkClip;

        readonly LifeDirector d;
        readonly AudioSource hum, engine;
        readonly AudioSource[] shots = new AudioSource[3];
        int nextShot;
        float hornGate;

        public LifeAudio(LifeDirector director)
        {
            d = director;
            Generate();
            var host = new GameObject("LifeAudio");
            host.transform.SetParent(director.transform, false);
            hum = host.AddComponent<AudioSource>();
            Setup(hum, humClip, true, 0f);
            var eg = new GameObject("LifeEngine");
            eg.transform.SetParent(director.transform, false);
            engine = eg.AddComponent<AudioSource>();
            Setup(engine, engineClip, true, 1f);
            engine.minDistance = 4f;
            engine.maxDistance = 45f;
            for (int i = 0; i < shots.Length; i++)
            {
                var g = new GameObject("LifeShot" + i);
                g.transform.SetParent(director.transform, false);
                shots[i] = g.AddComponent<AudioSource>();
                Setup(shots[i], null, false, 1f);
                shots[i].minDistance = 6f;
                shots[i].maxDistance = 90f;
            }
        }

        static void Setup(AudioSource s, AudioClip c, bool loop, float spatial)
        {
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.priority = 170;
            s.volume = 0f;
            s.clip = c;
            s.loop = loop;
            if (loop && c != null)
            {
                s.Play();
                s.timeSamples = Random.Range(0, c.samples);
            }
        }

        public void Tick(float dt)
        {
            float sfx = GameSettings.SfxVolume;
            hornGate -= dt;
            if (!LifeDirector.Active) { hum.volume = Mathf.MoveTowards(hum.volume, 0f, dt); engine.volume = 0f; return; }
            var tr = d.Traffic;
            float city = RegionRuntime.Presence(RegionId.City);
            float busy = tr != null ? Mathf.Clamp01(tr.NearCount / 12f) : 0f;
            float want = (0.05f * city + 0.035f * busy) * Mathf.Lerp(1f, 0.45f, LifeDirector.Night) * sfx;
            hum.volume = Mathf.Lerp(hum.volume, want, 1f - Mathf.Exp(-dt * 0.8f));

            var near = tr != null ? tr.Nearest : null;
            if (near != null && near.active)
            {
                engine.transform.position = near.pos;
                float k = Mathf.Clamp01(near.speed / 20f);
                engine.pitch = (near.kind == 2 ? 0.6f : 0.85f) + 0.9f * k;
                engine.volume = Mathf.Lerp(engine.volume, (0.08f + 0.14f * k) * sfx, 1f - Mathf.Exp(-dt * 4f));
            }
            else engine.volume = Mathf.MoveTowards(engine.volume, 0f, dt * 0.5f);
        }

        void Shot(AudioClip c, Vector3 p, float vol, float pitch)
        {
            var s = shots[nextShot];
            nextShot = (nextShot + 1) % shots.Length;
            s.transform.position = p;
            s.clip = c;
            s.pitch = pitch;
            s.volume = vol * GameSettings.SfxVolume;
            s.Play();
        }

        public void Horn(Vector3 p, float pitch)
        {
            if (hornGate > 0f) return;
            hornGate = 2.5f;
            Shot(hornClip, p, 0.32f, pitch);
        }

        public void Hiss(Vector3 p) { Shot(hissClip, p, 0.22f, Random.Range(0.95f, 1.05f)); }
        public void Oof(Vector3 p) { Shot(bonkClip, p, 0.3f, Random.Range(0.9f, 1.2f)); }

        // ------------------------------------------------------------------ synthesis (once, cached)
        static void Generate()
        {
            if (humClip != null) return;
            var rng = new XRng(0x5EED1234u);
            // distant traffic: brown-ish noise with a slow swell, low-passed, seamless loop
            int n = Rate * 4;
            var hum = ProcAudio.WhiteLoop(n, ref rng);
            ProcAudio.OnePoleLoop(hum, 260f, Rate);
            ProcAudio.OnePoleLoop(hum, 420f, Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                hum[i] *= 0.75f + 0.25f * Mathf.Sin(t * ProcAudio.TwoPi * 3f) * Mathf.Sin(t * ProcAudio.TwoPi * 2f);
                hum[i] += 0.08f * Mathf.Sin(t * ProcAudio.TwoPi * 4f * 55f);
            }
            ProcAudio.RemoveDc(hum);
            ProcAudio.Normalize(hum, 0.7f);
            humClip = ProcAudio.MakeClip("BR_LifeCityHum", hum, Rate, 1);

            // engine: 40 Hz firing (whole cycles in the loop) with harmonics and a little rasp
            int en = Rate / 2;
            var eng = ProcAudio.WhiteLoop(en, ref rng);
            ProcAudio.OnePoleLoop(eng, 900f, Rate);
            for (int i = 0; i < en; i++)
            {
                float ph = i / (float)en * 20f * ProcAudio.TwoPi;
                float s = 0.6f * Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2f + 0.4f) + 0.2f * Mathf.Sin(ph * 3f + 1.1f) + 0.1f * Mathf.Sin(ph * 5f);
                eng[i] = s + eng[i] * 0.35f * (0.6f + 0.4f * Mathf.Sin(ph));
            }
            ProcAudio.RemoveDc(eng);
            ProcAudio.Normalize(eng, 0.7f);
            engineClip = ProcAudio.MakeClip("BR_LifeEngine", eng, Rate, 1);

            // horn: two-tone, buzzy (odd harmonics), soft attack and release
            int hn = (int)(Rate * 0.42f);
            var horn = new float[hn];
            for (int i = 0; i < hn; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((0.42f - t) / 0.05f);
                float s = 0f;
                for (int h = 1; h <= 7; h += 2)
                    s += (Mathf.Sin(t * ProcAudio.TwoPi * 415f * h) + Mathf.Sin(t * ProcAudio.TwoPi * 523f * h)) / h;
                horn[i] = s * env;
            }
            ProcAudio.Finish(horn, 0.8f);
            hornClip = ProcAudio.MakeClip("BR_LifeHorn", horn, Rate, 1);

            // bus door hiss
            int sn = (int)(Rate * 0.7f);
            var hiss = new float[sn];
            ProcAudio.NoiseBurst(hiss, 0, Rate, 5200f, 0.6f, 0.25f, 0.8f, ref rng);
            ProcAudio.NoiseBurst(hiss, (int)(Rate * 0.05f), Rate, 3000f, 0.8f, 0.3f, 0.5f, ref rng);
            ProcAudio.Finish(hiss, 0.7f);
            hissClip = ProcAudio.MakeClip("BR_LifeBusHiss", hiss, Rate, 1);

            // stumble: a soft thump and a cartoon "boing" chime (cosy, never painful)
            int bn = (int)(Rate * 0.6f);
            var bonk = new float[bn];
            ProcAudio.Tone(bonk, 0, Rate, 120f, 0.8f, 0.09f, 0.002f);
            ProcAudio.NoiseBurst(bonk, 0, Rate, 380f, 0.7f, 0.05f, 0.5f, ref rng);
            ProcAudio.Chime(bonk, (int)(Rate * 0.03f), Rate, 660f, 0.35f, 0.25f);
            ProcAudio.Chime(bonk, (int)(Rate * 0.11f), Rate, 880f, 0.25f, 0.22f);
            ProcAudio.Finish(bonk, 0.8f);
            bonkClip = ProcAudio.MakeClip("BR_LifeBonk", bonk, Rate, 1);
        }
    }
}
