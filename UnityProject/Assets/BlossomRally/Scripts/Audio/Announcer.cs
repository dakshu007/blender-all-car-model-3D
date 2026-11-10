// Announcer.cs - rally announcer voice (the ElevenLabs voice pack in Resources/Audio/Voice/).
// Listens to the stage sounds (countdown, GO, checkpoints, finish, medals) and the gameplay moments
// (perfect drift, big air, near miss, wrong way) and plays one short line at a time:
//   * important lines (countdown, GO, checkpoint, finish, medal, record, wrong way) interrupt the
//     small ones and queue behind each other (finish -> medal -> new record);
//   * small "hype" lines (drift, air, near miss) only play when nothing else is talking and
//     not more often than every few seconds, so the voice never becomes noise.
// Missing files are simply skipped: without a voice pack the game plays exactly as before.
// Settings > "Announcer voice" turns it off. Volume follows the effects volume.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class Announcer : MonoBehaviour
    {
        /// <summary>Clip names (Resources/Audio/Voice/&lt;name&gt;.mp3/.ogg/.wav) and the line each one says.</summary>
        public static readonly string[,] Lines =
        {
            { "Go",             "[excited, shouting] GO! GO! GO!" },
            { "Three",          "[intense, building] Three!" },
            { "Two",            "[intense, building] Two!" },
            { "One",            "[intense, building] One!" },
            { "Welcome",        "[warm, excited] Welcome to... Blossom Rally!" },
            { "Checkpoint",     "[energetic] Checkpoint!" },
            { "CheckpointBest", "[excited] Faster than your best! Keep it up!" },
            { "PerfectDrift",   "[hyped, shouting] PERFECT DRIFT!" },
            { "BigAir",         "[amazed, shouting] Big air!" },
            { "NearMiss",       "[gasps, excited] Ooh, near miss!" },
            { "WrongWay",       "[urgent] Wrong way! Turn around!" },
            { "Finish",         "[triumphant] Finish! What a run!" },
            { "MedalGold",      "[ecstatic, shouting] GOLD MEDAL! That is legendary driving!" },
            { "MedalSilver",    "[enthusiastic] Silver medal! Great driving!" },
            { "MedalBronze",    "[encouraging] Bronze medal! Keep pushing, you can go faster!" },
            { "NewRecord",      "[thrilled] New record!" },
        };

        public const string VoiceFolder = "Audio/Voice/";
        const float HypeGap = 4.5f;          // seconds between two small lines
        const float VoiceGain = 1.0f;

        static Announcer instance;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static bool loaded, anyVoice;

        AudioSource src;
        bool currentImportant;
        readonly Queue<string> queue = new Queue<string>();
        float lastHype = -100f, welcomeAt = -1f;
        bool welcomed, wasWrongWay;
        float wrongWayRepeat;

        /// <summary>True when at least one voice clip is in the project.</summary>
        public static bool HasVoicePack { get { Load(); return anyVoice; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            WorldHooks.WorldBuilt += w => { Ensure(); if (instance != null && !instance.welcomed) instance.welcomeAt = Time.unscaledTime + 1.0f; };
            GameEvents.DriftBanked += (pts, peak) => { if (pts >= 1500f || peak >= 42f) Hype("PerfectDrift", 1f); };
            GameEvents.Landed += (air, vy) => { if (air > 0.8f) Hype("BigAir", 1f); };
            GameEvents.NearMiss += (p, clear) => Hype("NearMiss", 0.5f);
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            for (int i = 0; i < Lines.GetLength(0); i++)
            {
                AudioClip c = null;
                try { c = Resources.Load<AudioClip>(VoiceFolder + Lines[i, 0]); } catch (System.Exception) { }
                if (c != null) { clips[Lines[i, 0]] = c; anyVoice = true; }
            }
        }

        static void Ensure()
        {
            if (instance != null) return;
            Load();
            var go = new GameObject("BR_Announcer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Announcer>();
        }

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.loop = false;
            src.priority = 16;          // voice wins over engine / ambience voices
            src.bypassReverbZones = true;
        }

        // ---------------------------------------------------------------- inputs

        /// <summary>Called with every UI / stage sound id (GameManager.PlaySfx).</summary>
        public static void Hear(string id)
        {
            switch (id)
            {
                case "Countdown":
                    var sm = StageManager.Instance;
                    int n = sm != null ? sm.CountdownNumber : 0;
                    if (n == 3) Say("Three"); else if (n == 2) Say("Two"); else if (n == 1) Say("One");
                    break;
                case "CountdownGo": Say("Go"); break;
                case "Checkpoint": Say("Checkpoint"); break;
                case "CheckpointBest": Say("CheckpointBest"); break;
                case "Finish": Say("Finish"); break;
                case "MedalGold":
                case "MedalSilver":
                case "MedalBronze":
                    Queue(id);
                    var s2 = StageManager.Instance;
                    if (s2 != null && s2.NewBest) Queue("NewRecord");
                    break;
            }
        }

        /// <summary>Important line: interrupts small lines and anything already queued.</summary>
        static void Say(string line)
        {
            if (!Ready(line)) return;
            instance.queue.Clear();
            instance.Play(line, true);
        }

        /// <summary>Important line that waits for the current one to finish.</summary>
        static void Queue(string line)
        {
            if (!Ready(line)) return;
            if (instance.src.isPlaying && instance.currentImportant) instance.queue.Enqueue(line);
            else instance.Play(line, true);
        }

        /// <summary>Small line: only when quiet, rate-limited, with a chance to skip.</summary>
        static void Hype(string line, float chance)
        {
            if (!Ready(line)) return;
            var a = instance;
            if (a.src.isPlaying || a.queue.Count > 0) return;
            if (Time.unscaledTime - a.lastHype < HypeGap) return;
            if (Random.value > chance) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.Car == null) return;
            a.lastHype = Time.unscaledTime;
            a.Play(line, false);
        }

        static bool Ready(string line)
        {
            if (!GameSettings.Announcer) return false;
            Ensure();
            return instance != null && clips.ContainsKey(line);
        }

        void Play(string line, bool important)
        {
            AudioClip c;
            if (!clips.TryGetValue(line, out c) || c == null) return;
            src.Stop();
            src.clip = c;
            src.pitch = 1f;
            src.volume = Mathf.Clamp01(VoiceGain * GameSettings.SfxVolume);
            src.Play();
            currentImportant = important;
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (!GameSettings.Announcer) { if (src.isPlaying) src.Stop(); queue.Clear(); return; }

            if (!src.isPlaying && queue.Count > 0) Play(queue.Dequeue(), true);

            // welcome line once, shortly after the valley appears on the title screen
            if (!welcomed && welcomeAt > 0f && Time.unscaledTime >= welcomeAt)
            {
                welcomed = true;
                if (clips.ContainsKey("Welcome") && !src.isPlaying) Play("Welcome", true);
            }

            // wrong way: say it when the warning appears, repeat every 6 s while it stays
            var sm = StageManager.Instance;
            bool wrong = sm != null && sm.WrongWay;
            if (wrong)
            {
                wrongWayRepeat -= Time.unscaledDeltaTime;
                if (!wasWrongWay || wrongWayRepeat <= 0f)
                {
                    wrongWayRepeat = 6f;
                    if (clips.ContainsKey("WrongWay") && !(src.isPlaying && currentImportant)) Play("WrongWay", true);
                }
            }
            wasWrongWay = wrong;

            if (src.isPlaying) src.volume = Mathf.Clamp01(VoiceGain * GameSettings.SfxVolume);
        }
    }
}
