// RallyLife.cs - turns the valley into a living rally event: corner dressing (hay bales, tyre walls,
// tape, chevrons, distance boards, marshals), start ramps / finish gantries with live timing boards,
// spectator areas with cheering crowds, a service park, a camera helicopter, animals, farmers,
// fishermen, dragon boats, monks, chess players, tai chi, kids, fireworks, wind chimes, prayer flags.
//
// Plugs in through WorldHooks.WorldBuilt (no GameManager changes). Everything is built over several
// frames by one coroutine; each feature is a RallyModule (own seeded RNG, so the layout is stable and
// independent of the other modules). One Update drives all modules with distance throttling; nothing
// moves while Time.deltaTime == 0 (pause). All placement goes through PlacementRegistry.
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BlossomRally
{
    public class RallyLife : MonoBehaviour
    {
        static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += w => Create(w);
        }

        public static RallyLife Instance { get; private set; }

        public static RallyLife Create(WorldBuilder w)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("RallyLife");
            var r = go.AddComponent<RallyLife>();
            r.World = w;
            Instance = r;
            r.StartCoroutine(r.Build());
            return r;
        }

        // ---------------------------------------------------------------- shared state
        public WorldBuilder World { get; private set; }
        internal RallyTrack Track { get; private set; }
        internal RallyAudio Audio { get; private set; }
        internal readonly StageWatch Stage = new StageWatch();
        internal RallyCrowds Crowds { get; private set; }
        internal RallyStages Stages { get; private set; }
        internal RallyFestive Festive { get; private set; }
        /// <summary>Shared static prop chunks (merged when every module has been built).</summary>
        internal StaticChunks Static { get; private set; }
        public int StaticRenderers { get; private set; }
        public int StaticVertices { get; private set; }
        public bool Ready { get; private set; }

        /// <summary>Animation clock (advances only while unpaused).</summary>
        public float Clock { get; private set; }
        public int Frame { get; private set; }
        /// <summary>Player car being driven (not the turntable car).</summary>
        public bool HasCar { get; private set; }
        public Vector3 CarPos { get; private set; }
        public Vector3 CarVel { get; private set; }
        public float CarSpeed { get; private set; }
        public ArcadeCar Car { get; private set; }
        public Vector3 Eye { get; private set; }
        /// <summary>Density multiplier from the quality setting (Low 0.5 .. Ultra 1.25).</summary>
        public float Density { get; private set; } = 1f;

        readonly List<RallyModule> modules = new List<RallyModule>();
        bool wasPaused;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            for (int i = 0; i < modules.Count; i++) modules[i].Shutdown();
        }

        IEnumerator Build()
        {
            while (World != null && !World.Done) yield return null;
            if (World == null) yield break;
            yield return null;                   // let ValleyLife / WaterLife / other modules reserve first
            Physics.SyncTransforms();
            int q = Mathf.Clamp(GameSettings.Quality, 0, 3);
            Density = q == 0 ? 0.5f : q == 1 ? 0.75f : q == 2 ? 1f : 1.25f;
            Track = new RallyTrack(World);
            Audio = new RallyAudio(transform);
            yield return null;

            Static = new StaticChunks();
            Stages = new RallyStages();
            Crowds = new RallyCrowds();
            Festive = new RallyFestive();
            RallyModule[] order =
            {
                Stages, new RallyServicePark(), new RallyCorners(), Crowds, new RallyHelicopter(),
                new RallyAnimals(), new RallyLake(), new RallyFolk(), Festive
            };
            for (int i = 0; i < order.Length; i++)
            {
                var m = order[i];
                m.L = this;
                m.Rng = new System.Random(7100 + 977 * i);
                var go = new GameObject("Rally_" + m.GetType().Name.Replace("Rally", ""));
                go.transform.SetParent(transform, false);
                m.Root = go.transform;
                try { m.Build(); m.Live = true; modules.Add(m); }
                catch (System.Exception e) { Debug.LogException(e); }
                yield return null;
            }
            // every static prop of every module: one merged mesh per 128 m cell
            Static.Finish("RallyStatic", transform, MaterialLibrary.VertexColorWind);
            StaticRenderers = Static.Renderers;
            StaticVertices = Static.Vertices;
            var sb = new System.Text.StringBuilder("[RallyLife] built: ");
            foreach (var m in modules) sb.Append(m.GetType().Name.Replace("Rally", "")).Append(" {").Append(m.Summary).Append("} ");
            sb.Append("| static chunks ").Append(StaticRenderers).Append(" (").Append(StaticVertices).Append(" verts)");
            Debug.Log(sb.ToString());
            GameEvents.DriftBanked += OnDriftBanked;
            GameEvents.NearMiss += OnNearMiss;
            GameEvents.Landed += OnLanded;
            GameEvents.Finished += OnFinished;
            GameEvents.Cheer += OnCheer;
            Ready = true;
            Sense();
            Tick(0f);
        }

        void OnDisable()
        {
            GameEvents.DriftBanked -= OnDriftBanked;
            GameEvents.NearMiss -= OnNearMiss;
            GameEvents.Landed -= OnLanded;
            GameEvents.Finished -= OnFinished;
            GameEvents.Cheer -= OnCheer;
        }

        void OnEnable()
        {
            if (!Ready) return;
            GameEvents.DriftBanked += OnDriftBanked;
            GameEvents.NearMiss += OnNearMiss;
            GameEvents.Landed += OnLanded;
            GameEvents.Finished += OnFinished;
            GameEvents.Cheer += OnCheer;
        }

        void Update()
        {
            if (!Ready) return;
            float dt = Time.deltaTime;
            bool paused = dt <= 0f;
            if (paused != wasPaused)
            {
                wasPaused = paused;
                for (int i = 0; i < modules.Count; i++) modules[i].Pause(paused);
            }
            if (paused) return;
            Clock += dt;
            Frame++;
            Sense();
            Stage.Update(dt);
            Tick(dt);
        }

        void Tick(float dt)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                if (!m.Live) continue;
                try { m.Tick(dt); }
                catch (System.Exception e) { m.Live = false; Debug.LogException(e); }
            }
        }

        void Sense()
        {
            var gm = GameManager.Instance;
            ArcadeCar car = gm != null ? gm.Car : null;
            HasCar = gm != null && gm.IsDriving && car != null;
            Car = HasCar ? car : null;
            if (HasCar)
            {
                CarPos = car.transform.position;
                CarVel = car.Body != null ? car.Body.GetVelocity() : Vector3.zero;
                CarSpeed = CarVel.magnitude;
            }
            else { CarVel = Vector3.zero; CarSpeed = 0f; }
            Camera cam = Camera.main;
            Eye = cam != null ? cam.transform.position : (HasCar ? CarPos : Vector3.zero);
        }

        // ---------------------------------------------------------------- crowd reactions
        void OnDriftBanked(float pts, float peak)
        {
            if (!HasCar || Crowds == null) return;
            Crowds.Excite(CarPos, 45f, Mathf.Clamp01(0.45f + pts / 4000f), true);
        }

        void OnNearMiss(Vector3 p, float clearance)
        {
            if (Crowds == null) return;
            Crowds.Excite(p, 35f, Mathf.Clamp01(1f - clearance * 0.5f) * 0.8f + 0.2f, true);
        }

        void OnLanded(float air, float vy)
        {
            if (!HasCar || Crowds == null || air < 0.35f) return;
            Crowds.Excite(CarPos, 40f, Mathf.Clamp01(0.4f + air * 0.6f), true);
        }

        void OnFinished(int medal, float result)
        {
            Stage.OnFinished(result);
            var sm = StageManager.Instance;
            int ev = sm != null ? sm.CurrentEvent : -1;
            Vector3 fin;
            if (medal >= 1 && Festive != null && Stages != null && Stages.FinishPoint(ev, out fin)) Festive.Celebrate(fin, medal);
            if (Crowds != null && HasCar) Crowds.Excite(CarPos, 70f, 0.6f + 0.13f * medal, true);
        }

        void OnCheer(Vector3 p, float loud)
        {
            // cheers raised by other systems also set nearby crowds off (our own raises are ignored)
            if (Crowds == null || Crowds.Raising) return;
            Crowds.Excite(p, 40f, loud, false);
        }

        // ---------------------------------------------------------------- shared helpers
        public float Ground(float x, float z) { return World.TerrainHeight(x, z); }
        public Vector3 Ground(Vector2 p) { return new Vector3(p.x, World.TerrainHeight(p.x, p.y), p.y); }

        /// <summary>Distance from p to the nearest road edge (999 when no road within 40 m).</summary>
        public float EdgeDist(Vector2 p)
        {
            RoadSample rs;
            float d = World.NearestRoad(p, out rs, 40f);
            return d == float.MaxValue ? 999f : d - rs.halfWidth;
        }

        /// <summary>Terrain height spread over a disc (max - min of 5 samples).</summary>
        public float Bumpiness(Vector2 p, float r)
        {
            float h0 = Ground(p.x, p.y), lo = h0, hi = h0;
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                float h = Ground(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r);
                if (h < lo) lo = h;
                if (h > hi) hi = h;
            }
            return hi - lo;
        }

        /// <summary>No colliders (houses, posts, trunks...) in a small sphere at chest height.</summary>
        public bool ClearOfColliders(Vector2 p, float r)
        {
            // sphere bottom kept ~0.6 m above the ground so the terrain collider itself never counts
            float y = Ground(p.x, p.y);
            return !Physics.CheckSphere(new Vector3(p.x, y + 0.6f + r, p.y), r, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>ClearOfColliders sampled over a whole disc (centre + two rings).</summary>
        public bool AreaClear(Vector2 c, float radius)
        {
            if (!ClearOfColliders(c, 1.2f)) return false;
            for (int ring = 1; ring <= 2; ring++)
            {
                float rr = radius * (ring == 1 ? 0.5f : 0.92f);
                int n = ring == 1 ? 6 : 12;
                for (int k = 0; k < n; k++)
                {
                    float a = k * Mathf.PI * 2f / n;
                    if (!ClearOfColliders(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr, 1.0f)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Update throttling by distance to the camera: always near, every 3rd frame mid, every 8th far,
        /// never beyond 'far'. salt spreads the work over frames.
        /// </summary>
        public bool Due(Vector3 p, int salt, float near = 70f, float mid = 160f, float far = 320f)
        {
            float d = (p - Eye).sqrMagnitude;
            if (d < near * near) return true;
            if (d < mid * mid) return (Frame + salt) % 3 == 0;
            if (d < far * far) return (Frame + salt) % 8 == 0;
            return false;
        }

        public static float Yaw(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }
        public static float Yaw(Vector3 dir) { return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; }
    }

    // ==================================================================== module base
    internal abstract class RallyModule
    {
        public RallyLife L;
        public Transform Root;
        public System.Random Rng;
        public bool Live;

        protected WorldBuilder W { get { return L.World; } }
        protected float R(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
        protected int RI(int n) { return Rng.Next(n); }
        protected bool Chance(float p) { return Rng.NextDouble() < p; }

        public abstract void Build();
        public virtual void Tick(float dt) { }
        public virtual void Pause(bool paused) { }
        public virtual void Shutdown() { }
        /// <summary>One-line build summary for the log.</summary>
        public virtual string Summary { get { return ""; } }

        /// <summary>CanPlace + not inside any collider + roughly flat; reserves on success.</summary>
        protected bool TryPlace(Vector2 p, float radius, float roadClearance, float maxBump = 0.9f, bool colliders = true)
        {
            if (!PlacementRegistry.CanPlace(W, p, radius, roadClearance)) return false;
            if (maxBump > 0f && L.Bumpiness(p, Mathf.Max(0.5f, radius)) > maxBump) return false;
            if (colliders && !L.ClearOfColliders(p, Mathf.Min(radius, 1.2f))) return false;
            PlacementRegistry.Reserve(p, radius);
            return true;
        }
    }

    // ==================================================================== stage state
    /// <summary>Reads StageManager each frame: running event, phase, run time, countdown.</summary>
    internal sealed class StageWatch
    {
        public const int Idle = 0, Countdown = 1, Run = 2, Results = 3;
        public int Event = -1;
        public int Phase;
        public float Time;
        public float CountdownLeft;
        public float FinalTime = -1f;
        /// <summary>Clock value when the current phase started.</summary>
        public float PhaseAge;

        static FieldInfo fRun, fCount;
        static bool looked;
        float poll;

        public StageDef Def { get { return Event >= 0 && Event < StageDef.All.Length ? StageDef.All[Event] : null; } }

        public void Update(float dt)
        {
            if (!looked)
            {
                looked = true;
                try
                {
                    var bf = BindingFlags.Instance | BindingFlags.NonPublic;
                    fRun = typeof(StageManager).GetField("runTime", bf);
                    fCount = typeof(StageManager).GetField("countdown", bf);
                    if (fRun != null && fRun.FieldType != typeof(float)) fRun = null;
                    if (fCount != null && fCount.FieldType != typeof(float)) fCount = null;
                }
                catch (System.Exception) { fRun = null; fCount = null; }
            }
            var sm = StageManager.Instance;
            int ph = Idle, ev = -1;
            if (sm != null && sm.Running)
            {
                ev = sm.CurrentEvent;
                var gm = GameManager.Instance;
                ArcadeCar car = gm != null ? gm.Car : null;
                if (sm.ShowingResults) ph = Results;
                else if (car != null && !car.controlsEnabled) ph = Countdown;
                else ph = Run;
            }
            if (ph == Countdown && (Phase == Idle || Phase == Results || ev != Event))
            {
                Time = 0f;
                CountdownLeft = 3.8f;
                FinalTime = -1f;
            }
            if (ph != Phase) PhaseAge = 0f;
            PhaseAge += dt;
            if (ph == Idle) FinalTime = -1f;
            Phase = ph;
            Event = ev;
            if (ph == Countdown) CountdownLeft -= dt;
            if (ph == Run) Time += dt;
            poll -= dt;
            if (ph != Idle && poll <= 0f && sm != null)
            {
                poll = 0.1f;
                try
                {
                    if (fRun != null) Time = (float)fRun.GetValue(sm);
                    if (fCount != null && ph == Countdown) CountdownLeft = (float)fCount.GetValue(sm);
                }
                catch (System.Exception) { fRun = null; fCount = null; }
            }
        }

        public void OnFinished(float result)
        {
            var d = Def;
            if (d != null && d.kind == StageKind.TimeAttack) FinalTime = result;
        }
    }
}
