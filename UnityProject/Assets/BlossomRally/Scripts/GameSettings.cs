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

        /// <summary>0 Low, 1 Medium, 2 High, 3 Ultra (always an int 0..3; other systems index arrays with it).</summary>
        public static int Quality { get; private set; } = 2;
        public static float GrassDensity { get; private set; } = 1f;     // 0..1 (shader thins blades)
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.6f;
        public static float SfxVolume { get; private set; } = 0.9f;
        public static bool VSync { get; private set; } = true;
        /// <summary>Index into FpsCaps / FpsCapNames (frame rate limit; with V-Sync on it only matters below the refresh rate).</summary>
        public static int FpsCapIndex { get; private set; } = 4;
        public static readonly int[] FpsCaps = { 30, 60, 120, 144, 200, 0 };     // 0 = unlimited
        public static readonly string[] FpsCapNames = { "30", "60", "120", "144", "200", "Unlimited" };
        /// <summary>Index into RenderScales / RenderScaleNames (fullscreen internal resolution; 0 = Auto by quality).</summary>
        public static int RenderScaleIndex { get; private set; }
        public static readonly float[] RenderScales = { -1f, 1f, 0.85f, 0.75f, 0.67f, 0.5f };
        public static readonly string[] RenderScaleNames = { "Auto", "100%", "85%", "75%", "67%", "50%" };
        /// <summary>Quality the hardware check picked on first launch (SystemInfo based).</summary>
        public static int DetectedQuality { get; private set; } = 2;
        public static bool ShowFps { get; set; }
        public static bool Rumble { get; private set; } = true;
        public static bool Announcer { get; private set; } = true;

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
            DetectedQuality = DetectQuality();
            Quality = Mathf.Clamp(GetI("BR_Quality", DetectedQuality), 0, 3);
            GrassDensity = Mathf.Clamp01(GetF("BR_Grass", 1f));
            MasterVolume = Mathf.Clamp01(GetF("BR_VolMaster", 1f));
            MusicVolume = Mathf.Clamp01(GetF("BR_VolMusic", 0.6f));
            SfxVolume = Mathf.Clamp01(GetF("BR_VolSfx", 0.9f));
            // never chosen: V-Sync follows the preset (off on Low so the high-refresh target can be reached)
            VSync = GetI("BR_VSync", Quality == 0 ? 0 : 1) == 1;
            FpsCapIndex = Mathf.Clamp(GetI("BR_FpsCap", 4), 0, FpsCaps.Length - 1);
            RenderScaleIndex = Mathf.Clamp(GetI("BR_RenderScale", 0), 0, RenderScales.Length - 1);
            Rumble = GetI("BR_Rumble", 1) == 1;
            Announcer = GetI("BR_Announcer", 1) == 1;
            foreach (var kv in defaults) bindings[kv.Key] = (KeyCode)GetI("BR_Key_" + kv.Key, (int)kv.Value);
            Apply();
            ApplyRenderScale();
        }

        /// <summary>
        /// First-launch preset from the hardware: integrated GPUs (Intel, AMD APUs, base Apple M chips),
        /// little memory or few cores = Low; mid-range = Medium; the rest High. The audience is mostly
        /// low-end laptops, so when in doubt this errs towards the fast preset.
        /// </summary>
        static int DetectQuality()
        {
            try
            {
                int mem = SystemInfo.systemMemorySize, vram = SystemInfo.graphicsMemorySize, cpus = SystemInfo.processorCount;
                string gpu = (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();
                bool apple = gpu.StartsWith("apple m");
                bool applePro = apple && (gpu.Contains("pro") || gpu.Contains("max") || gpu.Contains("ultra"));
                bool integrated = (gpu.Contains("intel") && !gpu.Contains(" arc")) || gpu.Contains("iris") || gpu.Contains("uhd graphics")
                                  || gpu.Contains("radeon(tm) graphics") || gpu.Contains("radeon graphics")
                                  || (gpu.Contains("vega") && !gpu.Contains("rx")) || gpu.Contains("adreno") || gpu.Contains("mali")
                                  || (apple && !applePro);
                if (integrated || mem < 7000 || cpus <= 4 || (vram > 0 && vram < 1800) || SystemInfo.graphicsShaderLevel < 45) return 0;
                if (applePro || mem < 12000 || (vram > 0 && vram < 4500)) return 1;
                return 2;
            }
            catch (System.Exception) { return 1; }
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

        public static void SetQuality(int q)
        {
            Quality = Mathf.Clamp(q, 0, 3);
            SetI("BR_Quality", Quality);
            if (!HasKey("BR_VSync")) VSync = Quality != 0;     // never chosen: follows the preset
            Apply();
            ApplyRenderScale();
        }
        public static void SetFpsCap(int i) { FpsCapIndex = (i % FpsCaps.Length + FpsCaps.Length) % FpsCaps.Length; SetI("BR_FpsCap", FpsCapIndex); Apply(); }
        public static void SetRenderScale(int i)
        {
            RenderScaleIndex = (i % RenderScales.Length + RenderScales.Length) % RenderScales.Length;
            SetI("BR_RenderScale", RenderScaleIndex);
            Save();
            ApplyRenderScale();
        }
        /// <summary>The player picked an explicit resolution: render scale goes back to 100% without touching the screen.</summary>
        public static void ClearRenderScale()
        {
            RenderScaleIndex = 1;
            SetI("BR_RenderScale", 1);
            SetI("BR_RSApplied", 0);
            Save();
        }
        static bool HasKey(string k) { try { return PlayerPrefs.HasKey(k); } catch (System.Exception) { return false; } }
        public static void SetGrassDensity(float v) { GrassDensity = Mathf.Clamp01(v); SetF("BR_Grass", GrassDensity); Apply(); }
        public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); SetF("BR_VolMaster", MasterVolume); Apply(); }
        public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); SetF("BR_VolMusic", MusicVolume); Apply(); }
        public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); SetF("BR_VolSfx", SfxVolume); Apply(); }
        public static void SetVSync(bool v) { VSync = v; SetI("BR_VSync", v ? 1 : 0); Apply(); }
        public static void SetRumble(bool v) { Rumble = v; SetI("BR_Rumble", v ? 1 : 0); Save(); }
        public static void SetAnnouncer(bool v) { Announcer = v; SetI("BR_Announcer", v ? 1 : 0); Save(); }

        static void Save() { try { PlayerPrefs.Save(); } catch (System.Exception) { } }

        /// <summary>Push the current settings into Unity (quality, audio, frame pacing, grass shader).</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            ApplyFramePacing();
            int q = Quality;
            // Low: no realtime shadows at all. That also drops the full-scene depth pre-pass the built-in
            // forward path renders for screen-space shadows, the single biggest GPU + CPU saving.
            // (Cars get a soft contact shadow instead, see ContactShadow.)
            QualitySettings.shadows = q == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            switch (q)
            {
                case 0: QualitySettings.shadowDistance = 0f; QualitySettings.shadowCascades = 1;
                        QualitySettings.shadowResolution = ShadowResolution.Low; break;
                case 1: QualitySettings.shadowDistance = 100f; QualitySettings.shadowCascades = 2;
                        QualitySettings.shadowCascade2Split = 0.28f;
                        QualitySettings.shadowResolution = ShadowResolution.Medium; break;
                case 2: QualitySettings.shadowDistance = 170f; QualitySettings.shadowCascades = 4;
                        QualitySettings.shadowResolution = ShadowResolution.VeryHigh; break;
                default: QualitySettings.shadowDistance = 240f; QualitySettings.shadowCascades = 4;
                        QualitySettings.shadowResolution = ShadowResolution.VeryHigh; break;
            }
            QualitySettings.shadowCascade4Split = new Vector3(0.06f, 0.16f, 0.4f);
            QualitySettings.antiAliasing = AntiAliasingQ[q];
            QualitySettings.pixelLightCount = PixelLightsQ[q];
            QualitySettings.lodBias = LodBiasQ[q];
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.anisotropicFiltering = q == 0 ? AnisotropicFiltering.Disable : (q == 1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable);
            QualitySettings.softParticles = false;                        // no BR shader uses them; they would want a depth texture
            QualitySettings.realtimeReflectionProbes = true;                // Low / Medium keep a sky-only car probe (Atmosphere)
            QualitySettings.particleRaycastBudget = q == 0 ? 64 : 256;
            Shader.SetGlobalFloat(IdGrassDensity, GrassDensity * GrassDensityQ[q]);
            Shader.SetGlobalFloat(IdGrassFade, GrassFadeQ[q]);
            Save();
            if (Changed != null) Changed();
        }

        static readonly int IdGrassDensity = Shader.PropertyToID("_BR_GrassDensity");
        static readonly int IdGrassFade = Shader.PropertyToID("_BR_GrassFade");

        // ---- per-preset tables (Low, Medium, High, Ultra) - static so the properties below never allocate
        static readonly int[] AntiAliasingQ = { 0, 2, 4, 8 };
        static readonly int[] PixelLightsQ = { 1, 2, 4, 6 };
        static readonly float[] LodBiasQ = { 0.7f, 1.0f, 1.5f, 2.0f };
        static readonly float[] GrassDensityQ = { 0.3f, 0.6f, 1f, 1f };
        static readonly float[] GrassFadeQ = { 46f, 78f, 110f, 140f };
        static readonly float[] GrassCullQ = { 56f, 92f, 130f, 165f };
        static readonly float[] TreeCullQ = { 240f, 320f, 400f, 520f };
        static readonly float[] StreamQ = { 500f, 610f, 720f, 860f };
        static readonly float[] ParticleQ = { 0.4f, 0.7f, 1f, 1.15f };
        static readonly float[] FogQ = { 1.3f, 1.12f, 1f, 1f };

        /// <summary>Distance (m) at which grass / trees are culled for the current quality.</summary>
        public static float GrassCullDistance { get { return GrassCullQ[Quality]; } }
        public static float TreeCullDistance { get { return TreeCullQ[Quality]; } }
        /// <summary>Distance (m) from a region's tile at which its content streams in (RegionRuntime).</summary>
        public static float StreamDistance { get { return StreamQ[Quality]; } }
        /// <summary>Multiplier for ambient particle counts (petals, rain, dust, snow): 0.4 on Low .. 1.15 on Ultra.</summary>
        public static float ParticleScale { get { return ParticleQ[Quality]; } }
        /// <summary>Scene fog density multiplier: Low / Medium draw less, so a slightly thicker haze hides the edge.</summary>
        public static float FogBoost { get { return FogQ[Quality]; } }
        /// <summary>True when realtime shadows are rendered (Medium and up).</summary>
        public static bool Shadows { get { return Quality >= 1; } }

        // ---------------------------------------------------------------- frame pacing
        static void ApplyFramePacing()
        {
            int cap = FpsCaps[Mathf.Clamp(FpsCapIndex, 0, FpsCaps.Length - 1)];
            if (VSync)
            {
                // V-Sync ignores targetFrameRate on desktop: a cap below the refresh rate becomes a sync interval
                int interval = 1;
                if (cap > 0)
                {
                    double hz = 60.0;
#if UNITY_2022_2_OR_NEWER
                    try { hz = Screen.currentResolution.refreshRateRatio.value; } catch (System.Exception) { }
#else
                    try { hz = Screen.currentResolution.refreshRate; } catch (System.Exception) { }
#endif
                    if (hz > 1.0) interval = Mathf.Clamp(Mathf.RoundToInt((float)(hz / cap)), 1, 4);
                }
                QualitySettings.vSyncCount = interval;
                Application.targetFrameRate = -1;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = cap > 0 ? cap : -1;
            }
        }

        // ---------------------------------------------------------------- render scale
        /// <summary>Internal resolution scale in fullscreen: explicit choice, or Auto (Low ~1080p, Medium ~1440p, else native).</summary>
        public static float EffectiveRenderScale
        {
            get
            {
                float s = RenderScales[Mathf.Clamp(RenderScaleIndex, 0, RenderScales.Length - 1)];
                if (s > 0f) return s;
                int nh = NativeHeight();
                if (nh <= 0) return 1f;
                float cap = Quality == 0 ? 1080f : (Quality == 1 ? 1440f : 1e6f);
                return Mathf.Clamp(cap / nh, 0.5f, 1f);
            }
        }

        static int NativeHeight()
        {
            try { int h = Display.main.systemHeight; if (h > 0) return h; } catch (System.Exception) { }
            return Screen.currentResolution.height;
        }

        static int NativeWidth()
        {
            try { int w = Display.main.systemWidth; if (w > 0) return w; } catch (System.Exception) { }
            return Screen.currentResolution.width;
        }

        /// <summary>
        /// Fullscreen only: render below the display's native resolution and let the display scaler upsample
        /// (works on every graphics API in the built-in pipeline, unlike dynamic resolution). A 2560x1600
        /// laptop panel on Low renders ~1728x1080, about 2.2x fewer pixels. Windowed sizes are left alone.
        /// </summary>
        public static void ApplyRenderScale()
        {
            if (Application.isEditor || !Screen.fullScreen) return;
            int nw = NativeWidth(), nh = NativeHeight();
            if (nw <= 0 || nh <= 0) return;
            float s = EffectiveRenderScale;
            if (s >= 0.999f)
            {
                // we lowered it before (this or an earlier session): give the native resolution back
                if (GetI("BR_RSApplied", 0) == 1)
                {
                    SetI("BR_RSApplied", 0);
                    Save();
                    if (Screen.width != nw || Screen.height != nh) Screen.SetResolution(nw, nh, Screen.fullScreenMode);
                }
                return;
            }
            int w = Mathf.RoundToInt(nw * s) & ~1, h = Mathf.RoundToInt(nh * s) & ~1;
            if (Mathf.Abs(Screen.width - w) > 2 || Mathf.Abs(Screen.height - h) > 2) Screen.SetResolution(w, h, Screen.fullScreenMode);
            SetI("BR_RSApplied", 1);
            Save();
        }

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
