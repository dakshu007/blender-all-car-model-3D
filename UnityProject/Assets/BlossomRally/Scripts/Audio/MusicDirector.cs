// MusicDirector.cs - procedural Chinese-flavoured soundtrack, generated at startup (no audio files).
// Two themes in pentatonic modes:
//   Calm  (Title / Select): G gong mode, 80 BPM, 12 bars (36 s) - pad, guzheng plucks + bell, erhu.
//   Drive (Drive)        : D gong mode, 120 BPM, 16 bars (32 s) - pad, guzheng arpeggios + glissandi,
//                          percussion (tanggu drum, bangu crack, woodblock, finger cymbal), erhu lead.
// Each theme's stems have exactly the same length, are rendered as circular buffers (every note tail,
// echo and filter wraps around the loop point) and are started together with PlayScheduled on one
// dspTime, so they loop sample-accurately in sync. Layers fade in/out with SetIntensity.
// Sample data is synthesised on a worker thread (time-sliced coroutine on WebGL); only
// AudioClip.Create / SetData run on the main thread. Until a theme is ready the director stays silent.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum MusicMode { Silent, Title, Select, Drive }

    public class MusicDirector : MonoBehaviour
    {
        const int Rate = 32000;
        const float MasterLevel = 0.7f;
        const float ModeFade = 1.5f;

        static MusicDirector inst;
        static MusicMode mode = MusicMode.Silent;
        static float intensity;

        /// <summary>Create the persistent music object if it does not exist yet (safe to call often).</summary>
        public static MusicDirector Ensure()
        {
            if (inst != null) return inst;
            var go = new GameObject("BR_MusicDirector");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<MusicDirector>();
            return inst;
        }

        /// <summary>Switch theme / arrangement; crossfades over ~1.5 s.</summary>
        public static void SetMode(MusicMode m)
        {
            mode = m;
            if (m != MusicMode.Silent) Ensure();
        }

        /// <summary>0..1 - in Drive mode the arrangement builds with it (pluck > 0.15, drums > 0.45, erhu > 0.7).</summary>
        public static void SetIntensity(float v) { intensity = Mathf.Clamp01(v); }

        public static MusicMode Mode { get { return mode; } }
        public static float Intensity { get { return intensity; } }
        /// <summary>True once the theme for the current mode has been generated.</summary>
        public static bool Ready
        {
            get
            {
                if (inst == null) return false;
                var t = inst.ThemeFor(mode);
                return t != null && t.ready;
            }
        }

        // ------------------------------------------------------------------ runtime state
        class Theme
        {
            public string name;
            public int frames;
            public int[] channels;
            public float[] mix;
            public float[][] data;
            public volatile bool generated;
            public AudioSource[] src;
            public AudioClip[] clips;
            public float[] layer;
            public float fade;
            public bool ready, playing;
            public int uploaded;
        }

        Theme calm, drive;
        float musicVol = 1f, smoothI, duck = 1f;
        volatile bool cancel;
        System.Exception genError;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
        System.Threading.Thread worker;
#endif

        void Awake()
        {
            if (inst != null && inst != this) { Destroy(gameObject); return; }
            inst = this;
            calm = MakeTheme("Calm", new[] { 2, 1, 1 }, new[] { 0.5f, 0.66f, 0.52f });
            drive = MakeTheme("Drive", new[] { 2, 1, 1, 1 }, new[] { 0.46f, 0.62f, 0.58f, 0.52f });
            musicVol = GameSettings.MusicVolume;
            GameSettings.Changed += OnSettingsChanged;
#if UNITY_WEBGL && !UNITY_EDITOR
            StartCoroutine(GenerateSliced());
#else
            worker = new System.Threading.Thread(GenerateThread) { IsBackground = true, Name = "BR_MusicGen" };
            worker.Start();
#endif
        }

        Theme MakeTheme(string name, int[] ch, float[] mix)
        {
            var t = new Theme { name = name, channels = ch, mix = mix, layer = new float[ch.Length] };
            t.src = new AudioSource[ch.Length];
            for (int i = 0; i < ch.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = true;
                s.spatialBlend = 0f;
                s.dopplerLevel = 0f;
                s.priority = 0;                  // music never gets culled
                s.ignoreListenerPause = true;    // keeps playing (ducked) in the pause menu
                s.volume = 0f;
                t.src[i] = s;
            }
            return t;
        }

        void OnSettingsChanged() { musicVol = GameSettings.MusicVolume; }

        void OnDestroy()
        {
            cancel = true;
            GameSettings.Changed -= OnSettingsChanged;
            if (inst == this) inst = null;
            foreach (var t in new[] { calm, drive })
                if (t != null && t.clips != null)
                    foreach (var c in t.clips) if (c != null) Destroy(c);
        }

        Theme ThemeFor(MusicMode m)
        {
            if (m == MusicMode.Title || m == MusicMode.Select) return calm;
            if (m == MusicMode.Drive) return drive;
            return null;
        }

        void Update()
        {
            if (genError != null) { Debug.LogException(genError); genError = null; }
            Upload(calm);
            Upload(drive);

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            smoothI = Mathf.MoveTowards(smoothI, intensity, dt * (intensity > smoothI ? 1.5f : 0.3f));
            duck = Mathf.MoveTowards(duck, Time.timeScale < 0.01f ? 0.6f : 1f, dt * 1.5f);
            var active = ThemeFor(mode);
            UpdateTheme(calm, active == calm, dt);
            UpdateTheme(drive, active == drive, dt);
        }

        void Upload(Theme t)
        {
            if (t == null || t.ready || !t.generated) return;
            // one stem per frame keeps the main-thread cost of SetData small
            if (t.clips == null) t.clips = new AudioClip[t.data.Length];
            int i = t.uploaded;
            t.clips[i] = ProcAudio.MakeClip("BR_Music_" + t.name + i, t.data[i], Rate, t.channels[i]);
            t.src[i].clip = t.clips[i];
            t.data[i] = null;   // free the float buffer; the clip owns the samples now
            if (++t.uploaded == t.data.Length)
            {
                t.data = null;
                t.ready = true;
            }
        }

        float LayerTarget(Theme t, int i)
        {
            if (t == calm)
            {
                if (mode == MusicMode.Select) return i == 0 ? 1f : i == 1 ? 0.85f : 0f;
                return i == 2 ? 0.9f : 1f;   // Title: full calm theme
            }
            switch (i)
            {
                case 0: return 1f;
                case 1: return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, 0.22f, smoothI));
                case 2: return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.52f, smoothI));
                default: return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.67f, 0.77f, smoothI));
            }
        }

        void UpdateTheme(Theme t, bool active, float dt)
        {
            if (t == null || !t.ready) return;
            if (active && !t.playing)
            {
                double at = AudioSettings.dspTime + 0.15;
                for (int i = 0; i < t.src.Length; i++)
                {
                    t.layer[i] = LayerTarget(t, i);
                    t.src[i].volume = 0f;
                    t.src[i].PlayScheduled(at);
                }
                t.playing = true;
                t.fade = 0f;
            }
            if (!t.playing) return;
            t.fade = Mathf.MoveTowards(t.fade, active ? 1f : 0f, dt / ModeFade);
            if (active)
                for (int i = 0; i < t.layer.Length; i++)
                {
                    float target = LayerTarget(t, i);
                    t.layer[i] = Mathf.MoveTowards(t.layer[i], target, dt * (target > t.layer[i] ? 1f / 1.2f : 1f / 2.5f));
                }
            float g = Mathf.Sin(t.fade * Mathf.PI * 0.5f) * musicVol * MasterLevel * duck;
            for (int i = 0; i < t.src.Length; i++) t.src[i].volume = t.mix[i] * t.layer[i] * t.layer[i] * g;
            if (!active && t.fade <= 0f)
            {
                for (int i = 0; i < t.src.Length; i++) t.src[i].Stop();
                t.playing = false;
            }
        }

        // ------------------------------------------------------------------ generation driver
        void GenerateThread()
        {
            try { foreach (int step in GenerateAll()) if (cancel) return; }
            catch (System.Exception e) { genError = e; }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        IEnumerator GenerateSliced()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var it = GenerateAll().GetEnumerator();
            while (true)
            {
                bool more;
                try { more = it.MoveNext(); }
                catch (System.Exception e) { genError = e; yield break; }
                if (!more || cancel) yield break;
                if (sw.ElapsedMilliseconds > 4) { yield return null; sw.Reset(); sw.Start(); }
            }
        }
#endif

        IEnumerable<int> GenerateAll()
        {
            foreach (int s in ComposeCalm(calm)) yield return s;
            foreach (int s in ComposeDrive(drive)) yield return s;
        }

        // ------------------------------------------------------------------ composition
        struct Note
        {
            public float beat, len, vel;
            public int midi;
            public Note(float beat, float len, int midi, float vel = 1f) { this.beat = beat; this.len = len; this.midi = midi; this.vel = vel; }
        }

        static double Hz(double midi) { return 440.0 * System.Math.Pow(2.0, (midi - 69.0) / 12.0); }

        static readonly int[] GongG = { 7, 9, 11, 2, 4 };
        static readonly int[] GongD = { 2, 4, 6, 9, 11 };

        IEnumerable<int> ComposeCalm(Theme t)
        {
            const int beat = Rate * 60 / 80;           // 24000 samples
            const int bars = 12;
            int frames = beat * 4 * bars;              // 36 s
            var rng = new XRng(8086u);
            var pad = new float[frames * 2];
            var pluck = new float[frames];
            var erhu = new float[frames];
            var wt = PadTable();

            // chord per 2 bars: G Em C D Em D (bass first)
            int[][] padChords =
            {
                new[] { 43, 55, 62, 67, 69 }, new[] { 40, 52, 59, 64, 67 }, new[] { 36, 48, 55, 62, 64 },
                new[] { 38, 50, 57, 62, 64 }, new[] { 40, 52, 59, 64, 67 }, new[] { 38, 50, 57, 62, 69 },
            };
            int[][] arps =
            {
                new[] { 43, 55, 62, 67, 71, 74 }, new[] { 40, 52, 59, 64, 67, 71 }, new[] { 48, 55, 64, 67, 69, 76 },
                new[] { 50, 57, 62, 64, 69, 74 }, new[] { 40, 52, 59, 64, 67, 71 }, new[] { 50, 57, 62, 64, 69, 74 },
            };
            int seg = beat * 8;
            for (int c = 0; c < padChords.Length; c++)
            {
                RenderPadChord(pad, frames, c * seg, seg, padChords[c], wt, 1.8f, 1.8f, ref rng);
                yield return 0;
            }
            ProcAudio.OnePoleLoop(pad, 1700f, Rate, 2, 0);
            ProcAudio.OnePoleLoop(pad, 1700f, Rate, 2, 1);
            yield return 0;

            // guzheng: slow rolling arpeggios, soft glissandi into bars 4/12, finger cymbal on sections
            int[] patA = { 0, 2, 3, 4, 5, -1, 4, 3 };
            int[] patB = { 1, -1, 3, 2, 4, -1, 5, -1 };
            for (int bar = 0; bar < bars; bar++)
            {
                var arp = arps[bar / 2];
                var pat = (bar & 1) == 0 ? patA : patB;
                bool gliss = bar == 3 || bar == 11 || bar == 7;
                for (int e = 0; e < 8; e++)
                {
                    if (pat[e] < 0) continue;
                    if (gliss && e >= 6) continue;
                    float b = bar * 4 + e * 0.5f;
                    int idx = pat[e];
                    float vel = (idx == 0 ? 0.8f : 0.45f + 0.12f * rng.Value) * (e == 0 ? 1.1f : e == 4 ? 1.0f : 0.9f);
                    int at = (int)(b * beat) + rng.Int(160);
                    ProcAudio.Pluck(pluck, at, Rate, Hz(arp[idx]), vel, idx == 0 ? 3.2 : 2.2, idx == 0 ? 0.55f : 0.7f, ref rng, true);
                }
                if (gliss) Gliss(pluck, (int)((bar * 4 + 3.1f) * beat), 67, 9, (int)(beat * 0.085f), 0.32f, GongG, ref rng);
                if (bar == 0 || bar == 8) ProcAudio.Bell(pluck, (int)(bar * 4 * beat), Rate, 0.18f, 1.6f, true);
                yield return 0;
            }
            ProcAudio.OnePoleLoop(pluck, 5500f, Rate);
            Ambience(pluck, frames, beat * 3 / 4, 0.32f, 3, 0.25f);
            yield return 0;

            var mel = new List<Note>
            {
                new Note(0, 3, 74), new Note(3, 1, 71), new Note(4, 2, 69), new Note(6, 1.6f, 71),
                new Note(8, 2, 69), new Note(10, 1, 67), new Note(11, 1, 64), new Note(12, 3.4f, 64),
                new Note(16, 2, 76), new Note(18, 1, 74), new Note(19, 1, 71), new Note(20, 1, 69), new Note(21, 2.4f, 67),
                new Note(24, 2, 69), new Note(26, 1, 71), new Note(27, 1, 74), new Note(28, 3.4f, 69),
                new Note(32, 2, 71), new Note(34, 1, 74), new Note(35, 1, 76), new Note(36, 1, 79), new Note(37, 2.4f, 76),
                new Note(40, 2, 74), new Note(42, 1, 71), new Note(43, 1, 69), new Note(44, 3.4f, 69),
            };
            foreach (var ph in Phrases(mel)) { ErhuPhrase(erhu, frames, ph, beat, 0.8f, 5.0f, ref rng); yield return 0; }
            Ambience(erhu, frames, beat * 3 / 4, 0.25f, 2, 0.3f);
            yield return 0;

            FinishStem(pad, 0.2f);
            FinishStem(pluck, 0.12f);
            FinishStem(erhu, 0.19f);
            t.frames = frames;
            t.data = new[] { pad, pluck, erhu };
            t.generated = true;
            yield return 1;
        }

        IEnumerable<int> ComposeDrive(Theme t)
        {
            const int beat = Rate * 60 / 120;          // 16000 samples
            const int bars = 16;
            int frames = beat * 4 * bars;              // 32 s
            var rng = new XRng(2024u);
            var pad = new float[frames * 2];
            var pluck = new float[frames];
            var perc = new float[frames];
            var erhu = new float[frames];
            var wt = PadTable();

            // chord per 2 bars: D Bm G A (x2)
            int[][] padChords =
            {
                new[] { 38, 50, 57, 62, 64, 69 }, new[] { 47, 54, 59, 62, 66 },
                new[] { 43, 50, 55, 59, 62, 64 }, new[] { 45, 52, 57, 59, 64 },
            };
            int[][] arps =
            {
                new[] { 50, 62, 69, 74, 76, 78 }, new[] { 47, 59, 66, 71, 74, 78 },
                new[] { 43, 55, 62, 67, 71, 74 }, new[] { 45, 57, 64, 69, 71, 76 },
            };
            int seg = beat * 8;
            for (int c = 0; c < 8; c++)
            {
                RenderPadChord(pad, frames, c * seg, seg, padChords[c % 4], wt, 0.9f, 1.2f, ref rng);
                yield return 0;
            }
            ProcAudio.OnePoleLoop(pad, 2000f, Rate, 2, 0);
            ProcAudio.OnePoleLoop(pad, 2000f, Rate, 2, 1);
            yield return 0;

            // guzheng: rolling eighth-note arpeggios, glissando into every fourth downbeat
            int[] patA = { 0, 2, 3, 4, 5, 4, 3, 2 };
            int[] patB = { 1, 3, 4, 5, 3, 4, 2, 4 };
            int[] patC = { 0, 3, 2, 4, 3, 5, 4, 5 };
            for (int bar = 0; bar < bars; bar++)
            {
                var arp = arps[(bar / 2) % 4];
                var pat = (bar & 1) == 1 ? patB : bar >= 8 ? patC : patA;
                bool gliss = (bar & 3) == 3;
                for (int e = 0; e < 8; e++)
                {
                    if (gliss && e >= 7) continue;
                    float b = bar * 4 + e * 0.5f;
                    int idx = pat[e];
                    float vel = (idx == 0 ? 0.8f : 0.42f + 0.14f * rng.Value) * ((e & 3) == 0 ? 1.12f : (e & 1) == 0 ? 1f : 0.85f);
                    int at = (int)(b * beat) + rng.Int(100);
                    ProcAudio.Pluck(pluck, at, Rate, Hz(arp[idx]), vel, idx == 0 ? 2.4 : 1.3, idx == 0 ? 0.6f : 0.8f, ref rng, true);
                }
                // sixteenth pick-up in the second half for momentum
                if (bar >= 8 && (bar & 3) == 1)
                {
                    ProcAudio.Pluck(pluck, (int)((bar * 4 + 3.75f) * beat), Rate, Hz(arp[4]), 0.4f, 1.0, 0.8f, ref rng, true);
                    ProcAudio.Pluck(pluck, (int)((bar * 4 + 3.25f) * beat), Rate, Hz(arp[3]), 0.35f, 1.0, 0.8f, ref rng, true);
                }
                if (gliss) Gliss(pluck, (int)((bar * 4 + 3.4f) * beat), 62, 10, (int)(beat * 0.058f), 0.36f, GongD, ref rng);
                yield return 0;
            }
            ProcAudio.OnePoleLoop(pluck, 6000f, Rate);
            Ambience(pluck, frames, beat * 3 / 4, 0.28f, 3, 0.22f);
            yield return 0;

            // percussion
            var drum = DrumHit(ref rng);
            var crack = CrackHit(ref rng);
            var block = BlockHit();
            var bell = new float[Rate * 2];
            ProcAudio.Bell(bell, 0, Rate, 0.6f, 1.3f);
            for (int bar = 0; bar < bars; bar++)
            {
                int b0 = bar * 4 * beat;
                bool fill = (bar & 3) == 3;
                Hit(perc, frames, drum, b0, 1f, ref rng);
                Hit(perc, frames, drum, b0 + (int)(1.75f * beat), 0.45f, ref rng);
                Hit(perc, frames, drum, b0 + (int)(2.5f * beat), 0.75f, ref rng);
                Hit(perc, frames, crack, b0 + beat, 0.6f, ref rng);
                if (!fill) Hit(perc, frames, crack, b0 + 3 * beat, 0.72f, ref rng);
                for (int e = 0; e < 4; e++)
                    Hit(perc, frames, block, b0 + (int)((e + 0.5f) * beat), e == 3 ? 0.5f : 0.38f, ref rng);
                if ((bar & 1) == 1) Hit(perc, frames, block, b0 + (int)(3.75f * beat), 0.22f, ref rng);
                if (fill)
                    for (int k = 0; k < 4; k++)
                        Hit(perc, frames, k < 2 ? crack : drum, b0 + (int)((3f + k * 0.25f) * beat), 0.5f + 0.12f * k, ref rng);
                if ((bar & 3) == 0) Hit(perc, frames, bell, b0, bar % 8 == 0 ? 0.35f : 0.2f, ref rng);
                yield return 0;
            }
            Ambience(perc, frames, beat * 3 / 4, 0.0f, 0, 0.2f);
            yield return 0;

            var mel = new List<Note>
            {
                new Note(0, 1.5f, 74), new Note(1.5f, 0.5f, 71), new Note(2, 2, 69), new Note(4, 1, 66), new Note(5, 1, 69), new Note(6, 1.6f, 74),
                new Note(8, 1.5f, 71), new Note(9.5f, 0.5f, 69), new Note(10, 1, 66), new Note(11, 1, 64), new Note(12, 3, 66), new Note(15, 0.7f, 69),
                new Note(16, 2, 71), new Note(18, 1, 74), new Note(19, 1, 76), new Note(20, 3, 74), new Note(23, 0.7f, 71),
                new Note(24, 2, 69), new Note(26, 1, 64), new Note(27, 1, 66), new Note(28, 3.5f, 69),
                new Note(32, 1.5f, 78), new Note(33.5f, 0.5f, 76), new Note(34, 2, 74), new Note(36, 1, 76), new Note(37, 1, 74), new Note(38, 1.6f, 69),
                new Note(40, 2, 71), new Note(42, 1, 74), new Note(43, 1, 71), new Note(44, 2, 69), new Note(46, 1.6f, 66),
                new Note(48, 1.5f, 71), new Note(49.5f, 0.5f, 74), new Note(50, 2, 76), new Note(52, 1, 74), new Note(53, 1, 71), new Note(54, 1.6f, 74),
                new Note(56, 3, 69), new Note(59, 1, 64), new Note(60, 3.4f, 69),
            };
            foreach (var ph in Phrases(mel)) { ErhuPhrase(erhu, frames, ph, beat, 0.85f, 5.6f, ref rng); yield return 0; }
            Ambience(erhu, frames, beat * 3 / 4, 0.22f, 2, 0.28f);
            yield return 0;

            FinishStem(pad, 0.19f);
            FinishStem(pluck, 0.13f);
            FinishStem(perc, 0.13f);
            FinishStem(erhu, 0.19f);
            t.frames = frames;
            t.data = new[] { pad, pluck, perc, erhu };
            t.generated = true;
            yield return 1;
        }

        // ------------------------------------------------------------------ instruments

        const int TableSize = 2048;

        static readonly float[] SineTable = MakeSineTable();
        static float[] MakeSineTable()
        {
            var w = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++) w[i] = (float)System.Math.Sin(2.0 * System.Math.PI * i / TableSize);
            return w;
        }

        /// <summary>Soft organ / sheng-like single-cycle wavetable for the pad.</summary>
        static float[] PadTable()
        {
            var w = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++)
            {
                double p = 2.0 * System.Math.PI * i / TableSize;
                w[i] = (float)(System.Math.Sin(p) + 0.3 * System.Math.Sin(2 * p) + 0.12 * System.Math.Sin(3 * p) + 0.05 * System.Math.Sin(4 * p));
            }
            return w;
        }

        /// <summary>One pad chord: detuned stereo pairs per voice, slow attack, release overlapping the next chord.</summary>
        static void RenderPadChord(float[] st, int frames, int start, int len, int[] notes, float[] wt, float attack, float release, ref XRng rng)
        {
            for (int v = 0; v < notes.Length; v++)
            {
                bool bass = v == 0;
                float amp = bass ? 0.42f : 0.32f / (1f + 0.12f * v);
                double f = Hz(notes[v]);
                double det = bass ? 0.0008 : 0.0022 + 0.0006 * rng.Value;
                double inc1 = f * (1 + det) / Rate * TableSize, inc2 = f * (1 - det) / Rate * TableSize;
                double p1 = rng.Value * TableSize, p2 = rng.Value * TableSize;
                int att = (int)(attack * Rate), rel = (int)(release * Rate);
                int total = len + rel;
                double lfoW = 2.0 * System.Math.PI * rng.Range(0.12f, 0.3f) / Rate;
                double lfoP = rng.Value * 6.28;
                float spread = bass ? 0.15f : 0.35f + 0.1f * (v & 1);
                float l1 = 0.5f + spread, r1 = 0.5f - spread;   // osc 1 leans left, osc 2 right
                float lfo = 1f;
                for (int n = 0; n < total; n++)
                {
                    float env = n < att ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * n / att) : 1f;
                    if (n >= len) env *= 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (n - len) / rel);
                    if ((n & 63) == 0) lfo = 1f + 0.1f * (float)System.Math.Sin(lfoW * n + lfoP);
                    float s1, s2;
                    if (bass) { s1 = Lookup(SineTable, p1); s2 = s1; }
                    else { s1 = Lookup(wt, p1); s2 = Lookup(wt, p2); }
                    p1 += inc1; if (p1 >= TableSize) p1 -= TableSize;
                    p2 += inc2; if (p2 >= TableSize) p2 -= TableSize;
                    float a = amp * env * lfo;
                    int o = (start + n) % frames;
                    st[o * 2] += (s1 * l1 + s2 * r1) * a;
                    st[o * 2 + 1] += (s1 * r1 + s2 * l1) * a;
                }
            }
        }

        static float Lookup(float[] wt, double p)
        {
            int i = (int)p;
            float fr = (float)(p - i);
            return wt[i] + (wt[i + 1] - wt[i]) * fr;
        }

        /// <summary>Guzheng glissando: fast pentatonic run upward.</summary>
        static void Gliss(float[] d, int start, int fromMidi, int count, int spacing, float amp, int[] scale, ref XRng rng)
        {
            int m = fromMidi;
            for (int i = 0; i < count; i++)
            {
                float a = amp * (0.6f + 0.4f * i / count);
                ProcAudio.Pluck(d, start + i * spacing, Rate, Hz(m), a, 1.4, 0.85f, ref rng, true);
                m = ProcAudio.NextInScale(m, scale);
            }
        }

        static IEnumerable<List<Note>> Phrases(List<Note> notes)
        {
            var cur = new List<Note>();
            for (int i = 0; i < notes.Count; i++)
            {
                if (cur.Count > 0)
                {
                    var p = cur[cur.Count - 1];
                    if (notes[i].beat > p.beat + p.len + 0.01f) { yield return cur; cur = new List<Note>(); }
                }
                cur.Add(notes[i]);
            }
            if (cur.Count > 0) yield return cur;
        }

        static double PolyBlep(double t, double dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1.0; }
            if (t > 1.0 - dt) { t = (t - 1.0) / dt; return t * t + t + t + 1.0; }
            return 0.0;
        }

        /// <summary>Erhu-like bowed line: band-limited saw + sine through nasal formants, slow bow attack,
        /// portamento between legato notes, delayed vibrato, gentle re-bow dips and a soft release.</summary>
        static void ErhuPhrase(float[] d, int frames, List<Note> ph, int beatLen, float amp, float vibRate, ref XRng rng)
        {
            float b0 = ph[0].beat;
            int start = (int)(b0 * beatLen);
            var last = ph[ph.Count - 1];
            int len = (int)((last.beat + last.len - b0) * beatLen);
            int att = (int)(0.17f * Rate), rel = (int)(0.24f * Rate);
            int total = len + rel;
            var lp = Biquad.LowPass(3200f, 0.7f, Rate);
            var f1 = Biquad.BandPass(950f, 1.6f, Rate);
            var f2 = Biquad.BandPass(2500f, 2.5f, Rate);
            var hp = Biquad.HighPass(150f, 0.7f, Rate);
            double phase = 0.0, vib = rng.Value * 6.28;
            int ni = 0, noteStart = 0;
            double baseF = Hz(ph[0].midi), fromF = baseF;
            float noiseLp = 0f;
            for (int n = 0; n < total; n++)
            {
                while (ni + 1 < ph.Count && n >= (int)((ph[ni + 1].beat - b0) * beatLen))
                {
                    ni++;
                    fromF = baseF;
                    noteStart = n;
                }
                float tIn = (n - noteStart) / (float)Rate;
                double target = Hz(ph[ni].midi);
                if (ni == 0) baseF = target;
                else
                {
                    float g = Mathf.Clamp01(tIn / 0.085f);
                    if (g >= 1f) baseF = target;
                    else
                    {
                        g = g * g * (3f - 2f * g);
                        baseF = fromF * System.Math.Pow(target / fromF, g);
                    }
                }
                float noteLenS = ph[ni].len * beatLen / (float)Rate;
                float vDepth = 0.011f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.6f, tIn));
                double f = baseF * (1.0 + vDepth * System.Math.Sin(vib));
                vib += 2.0 * System.Math.PI * vibRate / Rate;
                double dtp = f / Rate;
                phase += dtp;
                if (phase >= 1.0) phase -= 1.0;
                float saw = (float)(2.0 * phase - 1.0 - PolyBlep(phase, dtp));
                float sine = (float)System.Math.Sin(2.0 * System.Math.PI * phase);
                noiseLp += (rng.Signed - noiseLp) * 0.3f;
                float raw = saw * 0.5f + sine * 0.65f + noiseLp * 0.06f;
                float y = lp.Process(raw) * 0.55f + f1.Process(raw) * 0.9f + f2.Process(raw) * 0.3f;
                y = hp.Process(y);

                float env = n < att ? 0.5f - 0.5f * Mathf.Cos(Mathf.PI * n / att) : 1f;
                if (ni > 0 && tIn < 0.12f) env *= 1f - 0.2f * Mathf.Sin(Mathf.PI * tIn / 0.12f);   // re-bow
                env *= 0.82f + 0.18f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tIn / Mathf.Max(0.2f, noteLenS * 0.7f)));
                if (n >= len) env *= 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (n - len) / rel);
                d[(start + n) % frames] += y * env * amp * ph[ni].vel;
            }
        }

        static float[] DrumHit(ref XRng rng)
        {
            var b = new float[(int)(Rate * 0.5f)];
            double ph = 0;
            var bp = Biquad.BandPass(1800f, 0.8f, Rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 2.0 * System.Math.PI * (72.0 + 95.0 * System.Math.Exp(-t / 0.035)) / Rate;
                float body = (float)System.Math.Sin(ph) * Mathf.Exp(-t / 0.2f) * Mathf.Min(1f, t / 0.0015f);
                float click = bp.Process(rng.Signed) * Mathf.Exp(-t / 0.004f) * 0.6f;
                b[i] = ProcAudio.SoftClip((body + click) * 1.3f);
            }
            return b;
        }

        static float[] CrackHit(ref XRng rng)
        {
            var b = new float[(int)(Rate * 0.18f)];
            double ph = 0;
            var bp = Biquad.BandPass(2600f, 1.1f, Rate);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 2.0 * System.Math.PI * (380.0 + 260.0 * System.Math.Exp(-t / 0.008)) / Rate;
                float tone = (float)System.Math.Sin(ph) * Mathf.Exp(-t / 0.045f) * 0.6f;
                float noise = bp.Process(rng.Signed) * Mathf.Exp(-t / 0.03f) * 1.4f;
                b[i] = (tone + noise) * Mathf.Min(1f, t / 0.001f);
            }
            return b;
        }

        static float[] BlockHit()
        {
            var b = new float[(int)(Rate * 0.14f)];
            ProcAudio.Tone(b, 0, Rate, 820f, 0.8f, 0.028f, 0.0008f);
            ProcAudio.Tone(b, 0, Rate, 1960f, 0.4f, 0.016f, 0.0005f);
            ProcAudio.Tone(b, 0, Rate, 2900f, 0.15f, 0.009f, 0.0005f);
            return b;
        }

        /// <summary>Mix a pre-rendered hit into a looping stem with a little humanisation.</summary>
        static void Hit(float[] d, int frames, float[] hit, int at, float vel, ref XRng rng)
        {
            at += rng.Int(90) - 45;
            if (at < 0) at += frames;
            vel *= 0.92f + 0.16f * rng.Value;
            for (int i = 0; i < hit.Length; i++) d[(at + i) % frames] += hit[i] * vel;
        }

        /// <summary>Circular echo (soft, low-passed repeats) + short diffuse room taps.</summary>
        static void Ambience(float[] d, int frames, int delay, float feedback, int taps, float room)
        {
            var src = (float[])d.Clone();
            ProcAudio.OnePoleLoop(src, 2600f, Rate);
            float g = 1f;
            for (int k = 1; k <= taps; k++)
            {
                g *= feedback;
                int off = (delay * k) % frames;
                for (int i = 0; i < frames; i++) { int j = i - off; if (j < 0) j += frames; d[i] += src[j] * g; }
            }
            if (room <= 0f) return;
            int[] ms = { 23, 31, 43, 59, 73, 97, 131 };
            for (int k = 0; k < ms.Length; k++)
            {
                int off = Rate * ms[k] / 1000;
                float rg = room * (1f - k / (float)ms.Length) * ((k & 1) == 0 ? 1f : -1f) * 0.6f;
                for (int i = 0; i < frames; i++) { int j = i - off; if (j < 0) j += frames; d[i] += src[j] * rg; }
            }
        }

        /// <summary>Level a stem by loudness (RMS) rather than peak so sparse plucks and sustained
        /// lines sit together, then round off the few transients that exceed full scale.</summary>
        static void FinishStem(float[] d, float targetRms)
        {
            ProcAudio.RemoveDc(d);
            double s = 0;
            for (int i = 0; i < d.Length; i++) s += d[i] * d[i];
            float rms = (float)System.Math.Sqrt(s / d.Length);
            if (rms < 1e-6f) return;
            float k = targetRms / rms;
            for (int i = 0; i < d.Length; i++) d[i] = ProcAudio.SoftClip(d[i] * k) * 0.92f;
        }
    }
}
