// BlossomRallyAudio.cs - "Blossom Rally > Audio > ElevenLabs Voice & Engine Pack..."
// Puts the announcer voice lines and the recorded engine loops into the project:
//   1. Generate with ElevenLabs: paste your ElevenLabs API key (kept in EditorPrefs on this computer
//      only, never written into the project), press Generate. Every missing file is created with the
//      ElevenLabs text-to-speech / sound-effects API and saved to Resources/Audio/Voice and
//      Resources/Audio/Engines. Uses your ElevenLabs credits (about 2-3k for the whole pack).
//   2. Or import files you downloaded yourself (e.g. from the ElevenLabs flow): put them in one folder,
//      named like the list in the window (Go.mp3, Three.mp3, ..., GroupB_Low.mp3 ...), press Import.
// The game works without any of these files (procedural engine, no announcer voice).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace BlossomRally.EditorTools
{
    public class BlossomRallyAudio : EditorWindow
    {
        const string Root = "Assets/BlossomRally/Resources/";
        const string KeyPref = "BlossomRally.ElevenLabsKey";
        const string VoicePref = "BlossomRally.ElevenLabsVoice";
        const string DefaultVoice = "GyIXYY876myKNtA1j8NI";   // "Tyler - Arena Announcer"

        /// <summary>Engine loops: file name, sound-effect prompt.</summary>
        static readonly string[,] Engines =
        {
            { "GroupB_Low",  "1980s Group B turbocharged four-cylinder rally car engine idling at steady low rpm, rough lumpy burble with turbo whistle, recorded from outside behind the car, constant, seamless loop, no revving, no other sounds" },
            { "GroupB_High", "1980s Group B turbocharged rally car engine at steady high rpm under full throttle, aggressive raspy roar with turbo spool, recorded from outside behind the car, constant pitch, seamless loop, no gear changes, no other sounds" },
            { "Group4_Low",  "1970s Group 4 rally saloon car naturally aspirated four-cylinder engine idling at steady low rpm, twin carburettor intake growl, recorded from outside, constant, seamless loop, no revving, no other sounds" },
            { "Group4_High", "1970s Group 4 rally saloon car naturally aspirated four-cylinder engine at steady high rpm full throttle, howling carburettor intake and exhaust, recorded from outside, constant pitch, seamless loop, no gear changes, no other sounds" },
            { "Classic_Low", "1960s classic small rally car engine idling at steady low rpm, buzzy rear-engine flat four with sporty exhaust, recorded from outside, constant, seamless loop, no revving, no other sounds" },
            { "Classic_High","1960s classic small rally car engine at steady high rpm full throttle, buzzing rasping flat four with sporty exhaust, recorded from outside, constant pitch, seamless loop, no gear changes, no other sounds" },
        };

        string apiKey, voiceId;
        bool overwrite;
        Vector2 scroll;
        string status = "";

        // job queue (runs from EditorApplication.update)
        class Job { public string name, text, path; public bool sfx; public int attempt; }
        readonly Queue<Job> jobs = new Queue<Job>();
        Job current;
        UnityWebRequest req;
        int done, failed, total;

        [MenuItem("Blossom Rally/Audio/ElevenLabs Voice & Engine Pack...", false, 40)]
        static void Open()
        {
            var w = GetWindow<BlossomRallyAudio>(true, "Blossom Rally Audio", true);
            w.minSize = new Vector2(560, 560);
        }

        void OnEnable()
        {
            apiKey = EditorPrefs.GetString(KeyPref, "");
            voiceId = EditorPrefs.GetString(VoicePref, DefaultVoice);
        }

        void OnDisable() { StopAll(); }

        static string VoiceDir { get { return Root + Announcer.VoiceFolder; } }
        static string EngineDir { get { return Root + EngineAudioFolder; } }
        static string EngineAudioFolder { get { return EngineAudio.EngineFolder; } }

        static string Existing(string dir, string name)
        {
            foreach (var ext in new[] { ".mp3", ".ogg", ".wav", ".aiff", ".aif" })
                if (File.Exists(dir + name + ext)) return dir + name + ext;
            return null;
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            EditorGUILayout.LabelField("Announcer voice + recorded engine sounds", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Optional: without these files the game still plays (procedural engines, no announcer voice).\n" +
                                    "A: generate them with your ElevenLabs API key, or B: import files you downloaded.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("A. Generate with ElevenLabs", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            apiKey = EditorGUILayout.PasswordField(new GUIContent("API key", "elevenlabs.io > Developers > API Keys. Saved on this computer only."), apiKey);
            voiceId = EditorGUILayout.TextField(new GUIContent("Announcer voice id", "Default: Tyler - Arena Announcer"), voiceId);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(KeyPref, apiKey ?? "");
                EditorPrefs.SetString(VoicePref, string.IsNullOrEmpty(voiceId) ? DefaultVoice : voiceId);
            }
            overwrite = EditorGUILayout.Toggle(new GUIContent("Replace existing files"), overwrite);
            using (new EditorGUI.DisabledScope(current != null || string.IsNullOrEmpty(apiKey)))
            {
                if (GUILayout.Button("Generate voice lines + engine sounds", GUILayout.Height(30))) StartGenerate();
            }
            if (current != null && GUILayout.Button("Stop")) StopAll();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("B. Import downloaded files", EditorStyles.boldLabel);
            if (GUILayout.Button("Import audio files from a folder...", GUILayout.Height(26))) ImportFolder();

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Files (name them exactly like this, .mp3 / .ogg / .wav)", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < Announcer.Lines.GetLength(0); i++)
                Row(VoiceDir, Announcer.Lines[i, 0], Announcer.Lines[i, 1]);
            for (int i = 0; i < Engines.GetLength(0); i++)
                Row(EngineDir, Engines[i, 0], "engine loop");
            EditorGUILayout.EndScrollView();
        }

        static void Row(string dir, string name, string what)
        {
            bool has = Existing(dir, name) != null;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(has ? "✓" : "–", GUILayout.Width(16));
            GUILayout.Label(name, EditorStyles.boldLabel, GUILayout.Width(120));
            GUILayout.Label(what, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ import

        static string Norm(string s) { return s.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", ""); }

        void ImportFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Folder with the downloaded audio files", "", "");
            if (string.IsNullOrEmpty(folder)) return;
            var byName = new Dictionary<string, string>();
            foreach (var f in Directory.GetFiles(folder))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".mp3" && ext != ".ogg" && ext != ".wav" && ext != ".aiff" && ext != ".aif") continue;
                byName[Norm(Path.GetFileNameWithoutExtension(f))] = f;
            }
            int n = 0;
            var wanted = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < Announcer.Lines.GetLength(0); i++) wanted.Add(new KeyValuePair<string, string>(VoiceDir, Announcer.Lines[i, 0]));
            for (int i = 0; i < Engines.GetLength(0); i++) wanted.Add(new KeyValuePair<string, string>(EngineDir, Engines[i, 0]));
            foreach (var w in wanted)
            {
                string src;
                if (!byName.TryGetValue(Norm(w.Value), out src)) continue;
                Directory.CreateDirectory(w.Key);
                string old = Existing(w.Key, w.Value);
                if (old != null) AssetDatabase.DeleteAsset(old);
                File.Copy(src, w.Key + w.Value + Path.GetExtension(src).ToLowerInvariant(), true);
                n++;
            }
            AssetDatabase.Refresh();
            status = n > 0 ? "Imported " + n + " file(s). Press Play to hear them."
                           : "No matching files found. Name them like the list below (e.g. Go.mp3, GroupB_Low.mp3).";
        }

        // ------------------------------------------------------------------ generate

        void StartGenerate()
        {
            StopAll();
            jobs.Clear();
            done = failed = 0;
            for (int i = 0; i < Announcer.Lines.GetLength(0); i++)
            {
                string name = Announcer.Lines[i, 0];
                if (!overwrite && Existing(VoiceDir, name) != null) continue;
                jobs.Enqueue(new Job { name = name, text = Announcer.Lines[i, 1], path = VoiceDir + name + ".mp3" });
            }
            for (int i = 0; i < Engines.GetLength(0); i++)
            {
                string name = Engines[i, 0];
                if (!overwrite && Existing(EngineDir, name) != null) continue;
                jobs.Enqueue(new Job { name = name, text = Engines[i, 1], path = EngineDir + name + ".mp3", sfx = true });
            }
            total = jobs.Count;
            if (total == 0) { status = "Everything is already there (tick 'Replace existing files' to make new takes)."; return; }
            EditorApplication.update += Tick;
            Next();
        }

        void StopAll()
        {
            EditorApplication.update -= Tick;
            if (req != null) { req.Abort(); req.Dispose(); req = null; }
            current = null;
            jobs.Clear();
            EditorUtility.ClearProgressBar();
        }

        static string Esc(string s)
        {
            var b = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < ' ') b.Append(' ');
                else b.Append(c);
            }
            return b.ToString();
        }

        static string StripTags(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s, @"\[[^\]]*\]\s*", "").Trim();
        }

        void Next()
        {
            if (jobs.Count == 0)
            {
                StopAll();
                AssetDatabase.Refresh();
                status = "Done: " + done + " created" + (failed > 0 ? ", " + failed + " failed (see Console)" : "") + ".";
                Repaint();
                return;
            }
            current = jobs.Dequeue();
            current.attempt = 0;
            Send();
        }

        void Send()
        {
            string url, body;
            if (current.sfx)
            {
                url = "https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128";
                body = current.attempt == 0
                    ? "{\"text\":\"" + Esc(current.text) + "\",\"duration_seconds\":6,\"prompt_influence\":0.6,\"loop\":true,\"model_id\":\"eleven_text_to_sound_v2\"}"
                    : "{\"text\":\"" + Esc(current.text) + "\",\"duration_seconds\":6,\"prompt_influence\":0.6}";
            }
            else
            {
                string voice = string.IsNullOrEmpty(voiceId) ? DefaultVoice : voiceId.Trim();
                url = "https://api.elevenlabs.io/v1/text-to-speech/" + voice + "?output_format=mp3_44100_128";
                // eleven_v3 understands the [excited] style tags; older models get the plain line
                body = current.attempt == 0
                    ? "{\"text\":\"" + Esc(current.text) + "\",\"model_id\":\"eleven_v3\"}"
                    : "{\"text\":\"" + Esc(StripTags(current.text)) + "\",\"model_id\":\"eleven_multilingual_v2\"}";
            }
            req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "audio/mpeg");
            req.SetRequestHeader("xi-api-key", apiKey.Trim());
            req.timeout = 120;
            req.SendWebRequest();
        }

        void Tick()
        {
            if (current == null || req == null) return;
            int index = total - jobs.Count;
            if (EditorUtility.DisplayCancelableProgressBar("ElevenLabs", "Generating " + current.name + "  (" + index + " / " + total + ")", (index - 0.5f) / total))
            {
                StopAll();
                status = "Stopped. " + done + " file(s) created.";
                AssetDatabase.Refresh();
                return;
            }
            if (!req.isDone) return;

            long code = req.responseCode;
            byte[] data = req.downloadHandler != null ? req.downloadHandler.data : null;
            string err = req.error;
            string text = code >= 400 && req.downloadHandler != null ? req.downloadHandler.text : "";
            req.Dispose();
            req = null;

            if (code >= 200 && code < 300 && data != null && data.Length > 1000)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(current.path));
                string old = Existing(Path.GetDirectoryName(current.path).Replace('\\', '/') + "/", current.name);
                if (old != null && old != current.path) AssetDatabase.DeleteAsset(old);
                File.WriteAllBytes(current.path, data);
                done++;
                Next();
                return;
            }
            if (code == 401)
            {
                StopAll();
                status = "ElevenLabs refused the API key (401). Check the key and that it has text-to-speech and sound-effects access.";
                Debug.LogError("[BlossomRally] ElevenLabs: " + text);
                Repaint();
                return;
            }
            if (current.attempt == 0 && code >= 400 && code < 500 && code != 402 && code != 429)
            {
                current.attempt = 1;   // try the simpler request (older model / no loop flag)
                Send();
                return;
            }
            failed++;
            Debug.LogWarning("[BlossomRally] ElevenLabs " + current.name + " failed (" + code + " " + err + "): " + text);
            if (code == 402) { StopAll(); status = "Out of ElevenLabs credits (402). " + done + " file(s) created."; AssetDatabase.Refresh(); Repaint(); return; }
            Next();
        }
    }
}
