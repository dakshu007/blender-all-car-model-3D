// GraphicsFX.cs - entry point of the graphics & lighting upgrade. Hooks into WorldHooks.WorldBuilt
// (no GameManager edits) and builds, over several frames:
//   DistantScenery - shan-shui mountain rings on the horizon, a cloud band the far peaks poke through,
//                    drifting cumulus over the valley
//   NightGlow      - warm light pools under lanterns, glowing house windows, wet-road light streaks
//   DappledLight   - soft sun flecks under the blossom trees (by day)
//   WaterFX        - planar lake reflection (High / Ultra), car ripples, water globals
// It also raises the camera far clip for the horizon and drives the global shader settings that the
// material detail (BR_VertexColorWind) reads. GraphicsFX.Enhanced = false restores today's look.
using System.Collections;
using UnityEngine;

namespace BlossomRally
{
    public static class GraphicsFX
    {
        static bool registered, loaded, enhanced = true;

        /// <summary>Camera far clip needed for the horizon mountains (CameraRig sets 1100).</summary>
        public const float FarClip = 3200f;

        /// <summary>Master switch for the whole graphics upgrade (saved). Off = the previous look.</summary>
        public static bool Enhanced
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    try { enhanced = PlayerPrefs.GetInt("BR_GfxEnhanced", 1) == 1; } catch (System.Exception) { }
                }
                return enhanced;
            }
            set
            {
                loaded = true;
                enhanced = value;
                try { PlayerPrefs.SetInt("BR_GfxEnhanced", value ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { }
                if (Changed != null) Changed();
            }
        }

        /// <summary>Raised when Enhanced changes (quality changes come through GameSettings.Changed).</summary>
        public static event System.Action Changed;

        /// <summary>Effective quality for the upgrade: -1 when the upgrade is switched off.</summary>
        public static int Q { get { return Enhanced ? GameSettings.Quality : -1; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += OnWorldBuilt;
        }

        static void OnWorldBuilt(WorldBuilder world)
        {
            if (world == null) return;
            var go = new GameObject("BR_Graphics");
            var hub = go.AddComponent<GraphicsHub>();
            hub.world = world;
        }
    }

    public class GraphicsHub : MonoBehaviour
    {
        public WorldBuilder world;
        public static GraphicsHub Instance { get; private set; }

        DistantScenery scenery;
        NightGlow glow;
        DappledLight dapple;
        WaterFX water;
        float clipTimer;

        static readonly int IdDetail = Shader.PropertyToID("_BR_DetailAmount");
        static readonly int IdDetailDist = Shader.PropertyToID("_BR_DetailDist");
        static readonly int IdNoise = Shader.PropertyToID("_BR_Noise");

        void Awake()
        {
            Instance = this;
        }

        IEnumerator Start()
        {
            Shader.SetGlobalTexture(IdNoise, GfxTextures.Noise);
            var terrain = MaterialLibrary.Terrain;
            if (terrain != null) terrain.SetFloat("_DetailPatterns", 0f);   // ground: soft variation only, no bricks
            ApplyGlobals();
            GameSettings.Changed += OnSettingsChanged;
            GraphicsFX.Changed += OnSettingsChanged;
            yield return null;

            water = gameObject.AddComponent<WaterFX>();
            water.world = world;
            yield return null;
            scenery = gameObject.AddComponent<DistantScenery>();
            yield return scenery.Build();
            glow = gameObject.AddComponent<NightGlow>();
            glow.world = world;
            yield return glow.Build();
            dapple = gameObject.AddComponent<DappledLight>();
            dapple.world = world;
            yield return dapple.Build();
            OnSettingsChanged();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= OnSettingsChanged;
            GraphicsFX.Changed -= OnSettingsChanged;
            if (Instance == this) Instance = null;
        }

        void OnSettingsChanged()
        {
            ApplyGlobals();
            if (scenery != null) scenery.ApplyQuality();
            if (glow != null) glow.ApplyQuality();
            if (dapple != null) dapple.ApplyQuality();
            if (water != null) water.ApplyQuality();
        }

        void ApplyGlobals()
        {
            int q = GraphicsFX.Q;
            // procedural detail on stone / tiles / wood / plaster up close (BR_VertexColorWind)
            float amount = q < 1 ? 0f : (q == 1 ? 0.75f : 1f);
            float dist = q < 1 ? 1f : new[] { 25f, 25f, 38f, 52f }[q];
            Shader.SetGlobalFloat(IdDetail, amount);
            Shader.SetGlobalFloat(IdDetailDist, dist);
        }

        void LateUpdate()
        {
            // the horizon rings sit up to ~2.3 km away: raise the far clip (never lower it)
            clipTimer -= Time.unscaledDeltaTime;
            if (clipTimer > 0f) return;
            clipTimer = 1f;
            var cam = Camera.main;
            if (cam == null) return;
            float want = GraphicsFX.Enhanced ? GraphicsFX.FarClip : 1500f;
            if (GraphicsFX.Enhanced ? cam.farClipPlane < want : cam.farClipPlane > want + 1f) cam.farClipPlane = want;
        }

        /// <summary>Ground height for decals: terrain, or the road surface where a road is close.</summary>
        public static float GroundY(WorldBuilder w, float x, float z)
        {
            float h = w.TerrainHeight(x, z);
            RoadSample rs;
            float d = w.NearestRoad(new Vector2(x, z), out rs, 14f);
            if (d < rs.halfWidth + 0.3f && !rs.bridge) h = Mathf.Max(h, rs.height);
            return h;
        }
    }
}
