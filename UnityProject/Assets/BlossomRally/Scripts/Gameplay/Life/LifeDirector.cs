// LifeDirector.cs - WORLD LIFE module: bootstrap and shared per-frame state of the living open world
// (pedestrians, ambient traffic, city buses, valley cyclists, parked cars, fishing boats, street audio).
//
//   * Created on WorldHooks.WorldBuilt as one "BR_WorldLife" object (replaced when a new world is built).
//   * One Update drives every system in a fixed order: shared state -> traffic (fills the vehicle grid)
//     -> pedestrians (read it) -> extras -> audio. FixedUpdate only serves cars the player is about to hit.
//   * Everything is pooled; nothing allocates per frame; far agents tick at a lower rate; the module idles
//     while paused (deltaTime 0) and when there is no player (no car and nobody on foot).
//   * Cosy rules: nobody is ever hurt. Pedestrians hit by a car stumble, get up and shake a fist.
using UnityEngine;

namespace BlossomRally
{
    public sealed class LifeDirector : MonoBehaviour
    {
        public static LifeDirector Instance { get; private set; }
        static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += OnWorldBuilt;
        }

        static void OnWorldBuilt(WorldBuilder w)
        {
            if (w == null || w.Field == null) return;
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("BR_WorldLife");
            go.AddComponent<LifeDirector>().Init(w);
        }

        // ------------------------------------------------------------------ systems
        internal WorldBuilder World;
        internal WorldField Field;
        internal TrafficNet Net;
        internal PedSystem Peds;
        internal TrafficSystem Traffic;
        internal LifeExtras Extras;
        internal LifeAudio Audio;

        // ------------------------------------------------------------------ shared per-frame state
        internal static int Frame;
        internal static int Quality;
        /// <summary>A player exists (car or person) and the world should live around them.</summary>
        internal static bool Active;
        internal static Vector3 Player;
        internal static Vector2 Player2;
        internal static bool OnFoot;
        /// <summary>The player's car while driving (null on foot).</summary>
        internal static ArcadeCar PlayerCar;
        internal static Vector3 PlayerVel, PlayerFwd;
        internal static float PlayerSpeed;
        /// <summary>True for one frame after the player jumped far (fast travel, respawn, event start).</summary>
        internal static bool Teleported;
        internal static Vector3 Eye, EyeFwd = Vector3.forward;
        internal static float ViewCos = 0.55f;
        internal static float Night, Rain;
        /// <summary>Road index of the running rally event (-1 = none): traffic keeps off it.</summary>
        internal static int EventRoad = -1;
        internal static RegionId PlayerRegion;

        Camera cam;
        float camT;
        Vector3 lastPlayer;
        bool hadPlayer;
        bool loggedError;

        // ------------------------------------------------------------------ budgets (GameSettings.Quality 0..3)
        static int Pick(int low, int med, int high, int ultra)
        {
            switch (Quality) { case 0: return low; case 1: return med; case 2: return high; default: return ultra; }
        }
        internal static int PedBudget { get { return Pick(20, 40, 55, 70); } }
        internal static int CarBudget { get { return Pick(12, 25, 32, 40); } }
        internal static int BikeBudget { get { return Pick(2, 4, 5, 6); } }
        internal static int BusBudget { get { return Pick(1, 1, 2, 2); } }
        internal static int ParkedPerHub { get { return Pick(2, 4, 5, 6); } }
        internal static int BoatBudget { get { return Pick(2, 3, 4, 4); } }
        internal static float PedRadius { get { return Pick(85, 110, 125, 140); } }
        internal const float CarRadius = 350f;

        /// <summary>Traffic density per region (city busiest, desert emptiest).</summary>
        internal static float RegionTraffic(RegionId id)
        {
            switch (id)
            {
                case RegionId.City: return 1f;
                case RegionId.Valley: return 0.7f;
                case RegionId.Coast: return 0.65f;
                case RegionId.Mountains: return 0.5f;
                case RegionId.Snow: return 0.45f;
                default: return 0.38f;
            }
        }

        /// <summary>Fewer people and cars at night and in rain.</summary>
        internal static float PedTimeFactor { get { return Mathf.Lerp(1f, 0.32f, Night) * (1f - 0.45f * Rain); } }
        internal static float CarTimeFactor { get { return Mathf.Lerp(1f, 0.5f, Night) * (1f - 0.2f * Rain); } }

        // ------------------------------------------------------------------ life cycle
        void Init(WorldBuilder w)
        {
            Instance = this;
            World = w;
            Field = w.Field;
            Quality = GameSettings.Quality;
            Net = new TrafficNet();
            Net.Build(Field);
            Traffic = new TrafficSystem(this);
            Peds = new PedSystem(this);
            Extras = new LifeExtras(this);
            Audio = new LifeAudio(this);
            Debug.Log("[BlossomRally] world life: " + Net.DrivableRoads + " traffic roads, " + Net.LinkCount + " junction links, "
                      + Peds.SiteCount + " pedestrian sites");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Extras != null) Extras.Dispose();
            if (Traffic != null) Traffic.Dispose();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Frame++;
            Quality = GameSettings.Quality;
            Refresh(dt);
            if (dt <= 0f) return;            // paused / photo freeze: everything holds still
            try
            {
                Traffic.Tick(dt);
                Peds.Tick(dt);
                Extras.Tick(dt);
                Audio.Tick(dt);
            }
            catch (System.Exception e)
            {
                if (!loggedError) { loggedError = true; Debug.LogException(e); }
            }
        }

        void FixedUpdate()
        {
            if (!Active || Traffic == null) return;
            try { Traffic.FixedTick(Time.fixedDeltaTime); }
            catch (System.Exception e)
            {
                if (!loggedError) { loggedError = true; Debug.LogException(e); }
            }
        }

        void Refresh(float dt)
        {
            var gm = GameManager.Instance;
            OnFoot = PlayerState.OnFoot && PlayerState.Person != null;
            PlayerCar = (!OnFoot && gm != null) ? gm.Car : null;
            Active = OnFoot || PlayerCar != null;
            if (Active)
            {
                Player = PlayerState.Position;
                Player2 = new Vector2(Player.x, Player.z);
                Teleported = hadPlayer && (Player - lastPlayer).sqrMagnitude > 90f * 90f;
                if (PlayerCar != null)
                {
                    var rb = PlayerCar.Body;
                    PlayerVel = rb != null ? rb.GetVelocity() : Vector3.zero;
                    PlayerFwd = PlayerCar.transform.forward;
                }
                else
                {
                    Vector3 v = dt > 1e-4f ? (Player - lastPlayer) / dt : Vector3.zero;
                    if (Teleported || v.sqrMagnitude > 400f) v = Vector3.zero;
                    PlayerVel = Vector3.Lerp(PlayerVel, v, 0.3f);
                    PlayerFwd = PlayerState.Forward;
                }
                PlayerSpeed = new Vector2(PlayerVel.x, PlayerVel.z).magnitude;
                PlayerRegion = WorldLayout.At(Player2);
                lastPlayer = Player;
                hadPlayer = true;
            }
            else Teleported = false;

            camT -= Time.unscaledDeltaTime;
            if (cam == null || camT <= 0f) { cam = Camera.main; camT = 1f; }
            if (cam != null)
            {
                Eye = cam.transform.position;
                EyeFwd = cam.transform.forward;
                float tv = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float th = tv * Mathf.Max(0.5f, cam.aspect);
                ViewCos = Mathf.Cos(Mathf.Atan(Mathf.Sqrt(tv * tv + th * th)) + 0.1f);
            }
            else { Eye = Player + Vector3.up * 3f; EyeFwd = PlayerFwd; }

            Night = Mathf.Clamp01(Atmosphere.NightFactor);
            Rain = Mathf.Clamp01(Atmosphere.RainIntensity) * RegionRuntime.RainVisual;

            var sm = StageManager.Instance;
            int ev = sm != null && sm.Running ? sm.CurrentEvent : -1;
            var all = StageDef.All;
            EventRoad = ev >= 0 && all != null && ev < all.Length ? all[ev].road : -1;
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Roughly inside the camera view (slack widens the cone, in cosine units).</summary>
        internal static bool InView(Vector3 p, float slack = 0f)
        {
            Vector3 d = p - Eye;
            float m = d.magnitude;
            if (m < 2f) return true;
            return Vector3.Dot(d, EyeFwd) > (ViewCos - slack) * m;
        }

        internal static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        internal static float YawOf(Vector3 dir) { return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; }
        internal static float YawOf(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }

        /// <summary>Streamed content root of a region (falls back to this object).</summary>
        internal Transform RegionRoot(RegionId id)
        {
            var r = Field != null ? Field.regions[(int)id] : null;
            if (r != null && r.Context != null && r.Context.Root != null) return r.Context.Root;
            return transform;
        }

        /// <summary>True when a region's content (and its colliders) is loaded around the camera.</summary>
        internal bool RegionLive(RegionId id)
        {
            if (id == RegionId.Valley) return true;            // the valley is built into World_Static
            var r = Field != null ? Field.regions[(int)id] : null;
            if (r == null || r.Context == null || r.Context.Root == null) return true;
            return r.Context.Root.gameObject.activeInHierarchy;
        }

        internal float Ground(float x, float z) { return World.TerrainHeight(x, z); }

        /// <summary>Raycast mask for placement checks: everything solid except cars, grass, tree impostors, ignore-raycast.</summary>
        internal static int SolidMask
        {
            get { return ~((1 << CarExtras.CarLayer) | (1 << WorldBuilder.LayerGrass) | (1 << WorldBuilder.LayerTrees) | (1 << 2)); }
        }
    }

    // ====================================================================== spatial hash
    /// <summary>
    /// Fixed-size spatial hash over ids 0..capacity-1 (linked lists in arrays: no allocation after construction).
    /// Different cells can share a bucket, so queries must still test real positions.
    /// </summary>
    internal sealed class LifeGrid
    {
        const int Buckets = 1024;
        readonly int[] head = new int[Buckets];
        int[] next;
        readonly float inv;

        public LifeGrid(float cell, int capacity)
        {
            inv = 1f / cell;
            next = new int[Mathf.Max(8, capacity)];
            Clear();
        }

        public void Clear() { for (int i = 0; i < Buckets; i++) head[i] = -1; }

        public void Ensure(int capacity)
        {
            if (capacity <= next.Length) return;
            next = new int[capacity * 2];
            Clear();
        }

        static int Hash(int x, int z) { return ((x * 73856093) ^ (z * 19349663)) & (Buckets - 1); }

        public int CellOf(float v) { return Mathf.FloorToInt(v * inv); }

        public void Add(int id, float x, float z)
        {
            if (id < 0 || id >= next.Length) return;
            int b = Hash(CellOf(x), CellOf(z));
            next[id] = head[b];
            head[b] = id;
        }

        public int First(int cx, int cz) { return head[Hash(cx, cz)]; }
        public int Next(int id) { return next[id]; }
    }

    // ====================================================================== meshes
    internal static class LifeMesh
    {
        static Material vcol, glow, additive;

        /// <summary>Vertex-colour material without wind (props, proxies, boats, buses, benches).</summary>
        public static Material VertexColour
        {
            get
            {
                if (vcol == null)
                {
                    vcol = new Material(MaterialLibrary.FindShader("BlossomRally/VertexColorWind")) { name = "BR_LifeVertexColour" };
                    if (vcol.HasProperty("_WindStrength")) vcol.SetFloat("_WindStrength", 0f);
                    if (vcol.HasProperty("_Glossiness")) vcol.SetFloat("_Glossiness", 0.22f);
                }
                return vcol;
            }
        }

        /// <summary>Glowing vertex colour (bus windows, boat lanterns at night).</summary>
        public static Material Glow
        {
            get
            {
                if (glow == null) glow = MaterialLibrary.Glow;
                return glow;
            }
        }

        /// <summary>Soft additive glow (head-light cones, lamp cards). Intensity set per frame from the night factor.</summary>
        public static Material Additive
        {
            get
            {
                if (additive == null)
                {
                    additive = new Material(MaterialLibrary.FindShader("BlossomRally/Additive")) { name = "BR_LifeLights" };
                    if (additive.HasProperty("_Intensity")) additive.SetFloat("_Intensity", 1f);
                    if (additive.HasProperty("_Soft")) additive.SetFloat("_Soft", 1.6f);
                }
                return additive;
            }
        }

        public static GameObject Make(string name, LifeGeo g, Material m, Transform parent, bool shadows, int layer = 0)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            var mesh = g.ToMesh(name);
            mesh.UploadMeshData(true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }

        public static Color32 C(Color c, byte a = 0)
        {
            return new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), a);
        }
    }
}
