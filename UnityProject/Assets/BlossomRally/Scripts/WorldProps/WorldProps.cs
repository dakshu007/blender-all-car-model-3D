// WorldProps.cs - fills the Blossom Valley with Chinese village, culture and nature props: tea house by the
// lake, market stalls along Lantern Street, lanterns + firecrackers strung high over the street, moon-gate
// gardens, courtyard houses with door gods and couplets, guardian lions, incense burners, a temple with a
// drum tower and a bell tower (the bell rings when you drive past), water wheels, washing lines, bicycles,
// rickshaws, carts and old trucks; willows, Huangshan pines, plum / peach blossom, rice paddies, rapeseed and
// wisteria, karst pillars, scholar rocks, brooks with stepping stones, a lotus pond, bamboo light shafts,
// falling leaves, butterflies, dragonflies and fireflies.
//
// Hooks in by itself (WorldHooks.WorldBuilt). Everything static is merged into a handful of world-space
// meshes per material and map chunk (see PropMeshes), animated parts (water wheels, bell, striker log) are
// one CPU-posed LifeBatch, effects are 5 particle systems. Fixed random seed -> identical layout every run.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public partial class WorldProps : MonoBehaviour
    {
        static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += w => Create(w);
        }

        public static WorldProps Instance { get; private set; }
        public bool Ready { get; private set; }

        public static WorldProps Create(WorldBuilder w)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("WorldProps");
            var p = go.AddComponent<WorldProps>();
            Instance = p;
            p.world = w;
            p.colRoot = new GameObject("Props_Colliders").transform;
            p.colRoot.SetParent(go.transform, false);
            // the street market is built right away so the villagers (placed next frame) keep clear of the stalls
            if (w != null && w.Done) p.BuildStreet();
            p.StartCoroutine(p.Build());
            return p;
        }

        WorldBuilder world;
        Transform colRoot;
        readonly System.Random rng = new System.Random(88017);
        readonly PropMeshes meshes = new PropMeshes();
        readonly PropFX fx = new PropFX();
        readonly List<string> countKeys = new List<string>();
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        int colliders;
        bool streetBuilt;
        Material glowMat, shaftMat;
        Camera cam;
        float camT, matT;
        List<Vector2> river;

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        void Count(string k, int n = 1)
        {
            int c;
            if (!counts.TryGetValue(k, out c)) { countKeys.Add(k); c = 0; }
            counts[k] = c + n;
        }

        IEnumerator Build()
        {
            while (world != null && !world.Done) yield return null;
            if (world == null) yield break;
            float t0 = Time.realtimeSinceStartup;
            river = Spline.Sample(MapData.River, false, 6f);
            if (!streetBuilt) BuildStreet();
            yield return null;
            BuildTeaHouse(); yield return null;
            BuildTemple(); yield return null;
            BuildCourtyards(); yield return null;
            BuildGardens(); yield return null;
            BuildWaterWheels(); yield return null;
            BuildPaddies(); yield return null;
            BuildStreams(); yield return null;
            BuildWillows(); yield return null;
            BuildPines(); yield return null;
            BuildBlossomTrees(); yield return null;
            BuildFlowerFields(); yield return null;
            BuildVehicles(); yield return null;
            BuildKarsts();
            BuildBambooLight();
            BuildBugSpots();
            yield return null;
            MakeMaterials();
            meshes.Finish(transform);
            FinishMovers();
            fx.Build(transform);
            BuildAudio();
            Physics.SyncTransforms();
            Ready = true;
            UpdateMaterials();
            var sb = new System.Text.StringBuilder();
            sb.Append("[BlossomRally] WorldProps placed: ");
            for (int i = 0; i < countKeys.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(countKeys[i]).Append(' ').Append(counts[countKeys[i]]);
            }
            sb.Append(" | ").Append(meshes.Renderers).Append(" merged renderers + 1 movers + ").Append(fx.Systems).Append(" particle systems")
              .Append(", ").Append(meshes.Triangles).Append(" tris, ").Append(colliders).Append(" colliders, built in ")
              .Append(((Time.realtimeSinceStartup - t0) * 1000f).ToString("0")).Append(" ms");
            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------ materials
        void MakeMaterials()
        {
            var m = meshes.mats;
            m[(int)PK.Props] = MaterialLibrary.VertexColorWind;
            m[(int)PK.Landmark] = MaterialLibrary.VertexColorWind;
            var strong = new Material(MaterialLibrary.VertexColorWind) { name = "BR_PropsWindStrong" };
            strong.SetFloat("_WindStrength", 0.42f);
            strong.SetFloat("_WindSpeed", 1.6f);
            m[(int)PK.Strong] = strong;
            glowMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropGlow")) { name = "BR_PropsGlow" };
            m[(int)PK.Glow] = glowMat;
            var water = new Material(MaterialLibrary.Water) { name = "BR_PropsPaddyWater" };
            water.SetColor("_Shallow", new Color(0.62f, 0.80f, 0.82f, 0.62f));
            water.SetColor("_Deep", new Color(0.42f, 0.60f, 0.66f, 0.82f));
            water.SetFloat("_RippleScale", 0.9f);
            water.SetFloat("_RippleSpeed", 0.3f);
            m[(int)PK.Water] = water;
            var stream = new Material(MaterialLibrary.FindShader("BlossomRally/FlowFoam")) { name = "BR_PropsStream" };
            stream.SetColor("_WaterColor", new Color(0.62f, 0.82f, 0.9f, 0.55f));
            stream.SetColor("_FoamColor", new Color(1f, 1f, 1f, 0.9f));
            stream.SetVector("_Tiling", new Vector4(4f, 1f, 0f, 0f));
            stream.SetFloat("_Speed", 0.9f);
            stream.SetFloat("_Streak", 0.6f);
            stream.SetFloat("_Lit", 0.6f);
            m[(int)PK.Stream] = stream;
            m[(int)PK.Blades] = MaterialLibrary.Grass;
            shaftMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropAdditive")) { name = "BR_PropsLightShafts" };
            shaftMat.SetTexture("_MainTex", ProceduralTextures.Soft);
            shaftMat.SetVector("_Fade", new Vector4(3f, 14f, 150f, 230f));
            shaftMat.SetFloat("_EdgeFade", 1f);
            shaftMat.SetFloat("_VertexGamma", 1f);
            m[(int)PK.Shafts] = shaftMat;
            m[(int)PK.Karst] = MaterialLibrary.Terrain;
        }

        void UpdateMaterials()
        {
            float night = Atmosphere.NightFactor;
            if (glowMat != null) glowMat.SetFloat("_Glow", 0.3f + 2.3f * night + 0.25f * Atmosphere.CloudCover);
            if (shaftMat != null)
            {
                float a = (1f - night) * (1f - Atmosphere.CloudCover * 0.8f) * (1f - Atmosphere.RainIntensity) * 0.55f;
                Color sun = new Color(1f, 0.95f, 0.82f);
                var s = Atmosphere.Sun;
                if (s != null) sun = Color.Lerp(sun, s.color, 0.5f);
                sun.a = Mathf.Clamp01(a);
                shaftMat.SetColor("_Tint", sun);
            }
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (!Ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;      // paused
            camT -= dt;
            if (cam == null || camT <= 0f) { cam = Camera.main; camT = 1f; }
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            UpdateMovers(dt, eye);
            UpdateTowers(dt);
            fx.Tick(dt, eye, MapData.InBambooForest(new Vector2(eye.x, eye.z)));
            matT -= dt;
            if (matT <= 0f) { matT = 0.5f; UpdateMaterials(); }
        }

        // ------------------------------------------------------------------ placement helpers
        float Ground(float x, float z) { return world.TerrainHeight(x, z); }
        float Ground(Vector2 p) { return world.TerrainHeight(p.x, p.y); }
        static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
        static float YawTo(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }

        /// <summary>Distance from p to the nearest road edge (99 when no road within 40 m).</summary>
        float EdgeDist(Vector2 p)
        {
            RoadSample rs;
            float d = world.NearestRoad(p, out rs, 40f);
            if (d >= 40f) return 99f;
            return d - rs.halfWidth;
        }

        /// <summary>Yaw facing the nearest road (fallback: towards the valley centre).</summary>
        float FaceRoadYaw(Vector2 p)
        {
            RoadSample rs;
            float d = world.NearestRoad(p, out rs, 120f);
            Vector2 dir = d < 120f ? rs.pos - p : -p;
            return YawTo(dir);
        }

        /// <summary>Yaw parallel to the nearest road (parked vehicles).</summary>
        float AlongRoadYaw(Vector2 p)
        {
            RoadSample rs;
            float d = world.NearestRoad(p, out rs, 120f);
            if (d >= 120f) return Rand(0f, 360f);
            return YawTo(rs.tangent) + (rng.NextDouble() < 0.5 ? 180f : 0f);
        }

        void Spread(Vector2 p, float r, out float mn, out float mx)
        {
            mn = mx = Ground(p);
            for (int ring = 1; ring <= 2; ring++)
            {
                float rr = r * ring / 2f;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI * 0.25f;
                    float h = Ground(p.x + Mathf.Cos(a) * rr, p.y + Mathf.Sin(a) * rr);
                    if (h < mn) mn = h;
                    if (h > mx) mx = h;
                }
            }
        }

        bool PhysFree(Vector3 c, Vector3 half, float yaw)
        {
            int mask = ~((1 << WorldBuilder.LayerGrass) | (1 << CarExtras.CarLayer));
            return !Physics.CheckBox(c, half, Quaternion.Euler(0f, yaw, 0f), mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Spiral search around target for a free, flat-enough spot clear of roads, rivers and the lake.</summary>
        bool FindSite(Vector2 target, float searchR, float radius, float roadClear, float flatTol, out Vector2 site, System.Func<Vector2, bool> extra = null)
        {
            float a0 = Rand(0f, Mathf.PI * 2f);
            for (float r = 0f; r <= searchR; r += 2.5f)
            {
                int steps = r < 0.1f ? 1 : Mathf.Max(6, Mathf.CeilToInt(Mathf.PI * 2f * r / 3f));
                for (int k = 0; k < steps; k++)
                {
                    float a = a0 + k * Mathf.PI * 2f / steps;
                    Vector2 p = target + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!PlacementRegistry.CanPlace(world, p, radius, roadClear)) continue;
                    if (Vector2.Distance(p, Garage.Spot) < radius + 9f) continue;
                    if (extra != null && !extra(p)) continue;
                    if (flatTol < 50f)
                    {
                        float mn, mx;
                        Spread(p, radius, out mn, out mx);
                        if (mx - mn > flatTol) continue;
                    }
                    site = p;
                    return true;
                }
            }
            site = target;
            return false;
        }

        void BoxCol(Vector3 c, Vector3 size, float yaw)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(colRoot, false);
            go.transform.SetPositionAndRotation(c, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            colliders++;
        }

        void CapCol(Vector3 basePos, float r, float h)
        {
            var go = new GameObject("Trunk");
            go.transform.SetParent(colRoot, false);
            go.transform.position = basePos;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = r;
            cc.height = h;
            cc.center = new Vector3(0f, h * 0.5f, 0f);
            colliders++;
        }

        /// <summary>Local point of a prop placed at (pos, yaw) to world.</summary>
        static Vector3 L2W(Vector3 pos, float yaw, Vector3 local) { return pos + Quaternion.Euler(0f, yaw, 0f) * local; }

        /// <summary>Find a spot near target and stamp a model there (optionally with a box collider).</summary>
        bool PlaceModel(PropModel m, Vector2 target, float searchR, float radius, float roadClear, float yaw, Vector3 colSize, Vector3 colCentre, string key, float flatTol = 0.9f, System.Func<Vector2, bool> extra = null)
        {
            System.Func<Vector2, bool> test = p =>
            {
                if (extra != null && !extra(p)) return false;
                if (colSize.sqrMagnitude < 0.01f) return true;
                float y = Ground(p);
                return PhysFree(new Vector3(p.x, y + Mathf.Max(0.9f, colSize.y * 0.5f + 0.4f), p.y), new Vector3(radius * 0.8f, colSize.y * 0.35f, radius * 0.8f), 0f);
            };
            Vector2 site;
            if (!FindSite(target, searchR, radius, roadClear, flatTol, out site, test)) return false;
            float yw = float.IsNaN(yaw) ? AlongRoadYaw(site) : yaw;
            Vector3 pos = V3(site, Ground(site) - 0.02f);
            meshes.Stamp(m, pos, yw);
            if (colSize.sqrMagnitude > 0.01f) BoxCol(L2W(pos, yw, colCentre), colSize, yw);
            PlacementRegistry.Reserve(site, radius);
            Count(key);
            return true;
        }

        static readonly Color32[] BikeColours = { LifeGeo.C(30, 30, 36), LifeGeo.C(40, 110, 80), LifeGeo.C(44, 80, 150), LifeGeo.C(170, 44, 44), LifeGeo.C(200, 190, 170) };

        PropModel BikeModel()
        {
            var m = new PropModel();
            var bike = PropShapes.Bicycle(BikeColours[rng.Next(BikeColours.Length)], rng.NextDouble() < 0.6, rng);
            PG.Append(m[PK.Props], bike, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, rng.NextDouble() < 0.5 ? 9f : -9f), Vector3.one));
            return m;
        }

        void PlaceBikes(Vector2 near, int n, float searchR)
        {
            for (int i = 0; i < n; i++)
                PlaceModel(BikeModel(), near + new Vector2(Rand(-3f, 3f), Rand(-3f, 3f)), searchR, 0.9f, 1.6f, Rand(0f, 360f), Vector3.zero, Vector3.zero, "bicycles");
        }
    }
}
