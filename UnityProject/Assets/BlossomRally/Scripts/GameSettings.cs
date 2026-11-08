// GameSettings.cs - player settings shared by every system, persisted in PlayerPrefs.
// Quality preset, grass density, volumes, resolution / fullscreen / vsync, key bindings,
// per-car livery choice and best times. All values are safe to read at any time.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum GameAction { Throttle, Brake, SteerLeft, SteerRight, Handbrake, Reset, Camera, Replay, Photo }

    public static class GameSettings
    {
        public static readonly string[] QualityNames = { "Low", "Medium", "High", "Ultra" };

        public static int Quality { get; private set; } = 2;
        public static float GrassDensity { get; private set; } = 1f;     // 0..1 (shader thins blades)
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.6f;
        public static float SfxVolume { get; private set; } = 0.9f;
        public static bool VSync { get; private set; } = true;
        public static bool ShowFps { get; set; }
        public static bool Rumble { get; private set; } = true;

        public static event System.Action Changed;

        static readonly Dictionary<GameAction, KeyCode> defaults = new Dictionary<GameAction, KeyCode>
        {
            { GameAction.Throttle, KeyCode.W }, { GameAction.Brake, KeyCode.S }, { GameAction.SteerLeft, KeyCode.A },
            { GameAction.SteerRight, KeyCode.D }, { GameAction.Handbrake, KeyCode.Space }, { GameAction.Reset, KeyCode.R },
            { GameAction.Camera, KeyCode.C }, { GameAction.Replay, KeyCode.V }, { GameAction.Photo, KeyCode.O },
        };
        static readonly Dictionary<GameAction, KeyCode> bindings = new Dictionary<GameAction, KeyCode>();
        static bool loaded;

        static float GetF(string k, float d) { try { return PlayerPrefs.GetFloat(k, d); } catch (System.Exception) { return d; } }
        static int GetI(string k, int d) { try { return PlayerPrefs.GetInt(k, d); } catch (System.Exception) { return d; } }
        static void SetF(string k, float v) { try { PlayerPrefs.SetFloat(k, v); } catch (System.Exception) { } }
        static void SetI(string k, int v) { try { PlayerPrefs.SetInt(k, v); } catch (System.Exception) { } }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Quality = Mathf.Clamp(GetI("BR_Quality", SystemInfo.systemMemorySize < 7000 ? 1 : 2), 0, 3);
            GrassDensity = Mathf.Clamp01(GetF("BR_Grass", 1f));
            MasterVolume = Mathf.Clamp01(GetF("BR_VolMaster", 1f));
            MusicVolume = Mathf.Clamp01(GetF("BR_VolMusic", 0.6f));
            SfxVolume = Mathf.Clamp01(GetF("BR_VolSfx", 0.9f));
            VSync = GetI("BR_VSync", 1) == 1;
            Rumble = GetI("BR_Rumble", 1) == 1;
            foreach (var kv in defaults) bindings[kv.Key] = (KeyCode)GetI("BR_Key_" + kv.Key, (int)kv.Value);
            Apply();
        }

        public static KeyCode Binding(GameAction a)
        {
            Load();
            KeyCode k;
            return bindings.TryGetValue(a, out k) ? k : defaults[a];
        }

        public static void SetBinding(GameAction a, KeyCode k)
        {
            Load();
            bindings[a] = k;
            SetI("BR_Key_" + a, (int)k);
            Save();
        }

        public static void ResetBindings()
        {
            foreach (var kv in defaults) SetBinding(kv.Key, kv.Value);
        }

        public static void SetQuality(int q) { Quality = Mathf.Clamp(q, 0, 3); SetI("BR_Quality", Quality); Apply(); }
        public static void SetGrassDensity(float v) { GrassDensity = Mathf.Clamp01(v); SetF("BR_Grass", GrassDensity); Apply(); }
        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); SetF("BR_VolMaster", MasterVolume); Apply(); }
        public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); SetF("BR_VolMusic", MusicVolume); Apply(); }
        public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); SetF("BR_VolSfx", SfxVolume); Apply(); }
        public static void SetVSync(bool v) { VSync = v; SetI("BR_VSync", v ? 1 : 0); Apply(); }
        public static void SetRumble(bool v) { Rumble = v; SetI("BR_Rumble", v ? 1 : 0); Save(); }

        static void Save() { try { PlayerPrefs.Save(); } catch (System.Exception) { } }

        /// <summary>Push the current settings into Unity (quality, audio, frame pacing, grass shader).</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 120;
            switch (Quality)
            {
                case 0: QualitySettings.shadowDistance = 70f; QualitySettings.shadowCascades = 2; QualitySettings.antiAliasing = 0;
                        QualitySettings.shadowResolution = ShadowResolution.Medium; break;
                case 1: QualitySettings.shadowDistance = 110f; QualitySettings.shadowCascades = 2; QualitySettings.antiAliasing = 2;
                        QualitySettings.shadowResolution = ShadowResolution.High; break;
                case 2: QualitySettings.shadowDistance = 170f; QualitySettings.shadowCascades = 4; QualitySettings.antiAliasing = 4;
                        QualitySettings.shadowResolution = ShadowResolution.VeryHigh; break;
                default: QualitySettings.shadowDistance = 240f; QualitySettings.shadowCascades = 4; QualitySettings.antiAliasing = 8;
                        QualitySettings.shadowResolution = ShadowResolution.VeryHigh; break;
            }
            float qGrass = new[] { 0.35f, 0.65f, 1f, 1f }[Quality];
            Shader.SetGlobalFloat("_BR_GrassDensity", GrassDensity * qGrass);
            Shader.SetGlobalFloat("_BR_GrassFade", new[] { 55f, 85f, 110f, 140f }[Quality]);
            Save();
            if (Changed != null) Changed();
        }

        /// <summary>Distance (m) at which grass / trees are culled for the current quality.</summary>
        public static float GrassCullDistance { get { return new[] { 70f, 100f, 130f, 165f }[Quality]; } }
        public static float TreeCullDistance { get { return new[] { 260f, 330f, 400f, 520f }[Quality]; } }

        // ---- livery choice per car (index into LiveryLibrary) ----
        public static int LiveryColour(string carId) { return GetI("BR_LivCol_" + carId, 0); }
        public static int LiveryPattern(string carId) { return GetI("BR_LivPat_" + carId, 0); }
        public static void SetLivery(string carId, int colour, int pattern)
        {
            SetI("BR_LivCol_" + carId, colour);
            SetI("BR_LivPat_" + carId, pattern);
            Save();
        }

        // ---- records ----
        public static float BestTime(string key) { return GetF("BR_Best_" + key, 0f); }
        public static void SetBestTime(string key, float t) { SetF("BR_Best_" + key, t); Save(); }
        public static int BestMedal(string key) { return GetI("BR_Medal_" + key, 0); }
        public static void SetBestMedal(string key, int m) { SetI("BR_Medal_" + key, m); Save(); }
    }
}
