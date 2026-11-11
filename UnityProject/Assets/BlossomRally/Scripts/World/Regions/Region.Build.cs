// Region.Build.cs - the content half of a region: what it builds into the world and what it does per frame.
// WorldBuilder runs every region's Build() coroutine during loading (after the terrain, roads and the valley),
// then RegionRuntime streams each region's content in and out and calls Tick() while it is near the camera.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public abstract partial class Region
    {
        public RegionContext Context { get; internal set; }

        /// <summary>
        /// Build everything this region shows (buildings, trees, rocks, water, lights, effects). Yield often
        /// (every few ms of work) so the loading screen keeps moving; set ctx.Progress 0..1 as you go.
        /// Static geometry: stamp PropModels into ctx (merged per material + 334 m chunk).
        /// </summary>
        public virtual IEnumerator Build(RegionContext ctx) { yield break; }

        /// <summary>Per frame while the region's content is active. presence = 0..1 how much the camera is inside the region.</summary>
        public virtual void Tick(float dt, Vector3 eye, float presence) { }

        /// <summary>The player's car entered this region.</summary>
        public virtual void OnEnter() { }

        /// <summary>Colour grade while the camera is in this region (blended at the borders).</summary>
        public virtual Grade Grade { get { return Grade.Identity; } }
    }

    /// <summary>Everything a region needs while building. One per region.</summary>
    public sealed class RegionContext
    {
        public WorldBuilder World { get; internal set; }
        public WorldField Field { get; internal set; }
        public Region Region { get; internal set; }
        /// <summary>Streamed root: deactivated when the camera is far from the region.</summary>
        public Transform Root { get; internal set; }
        /// <summary>Always-active root for tall landmarks seen from other regions (skyline towers, summit, lighthouse).</summary>
        public Transform Skyline { get; internal set; }
        /// <summary>Parent for collider-only objects (under Root).</summary>
        public Transform Colliders { get; internal set; }
        public System.Random Rng { get; internal set; }
        /// <summary>0..1 build progress for the loading bar.</summary>
        public float Progress { get; set; }

        internal readonly PropMeshes Meshes = new PropMeshes();
        internal readonly PropMeshes SkylineMeshes = new PropMeshes();
        internal Material GlowMat, StrongMat, WaterMat, StreamMat, ShaftMat;
        readonly Dictionary<long, List<Vector3>> occupied = new Dictionary<long, List<Vector3>>();
        public int ColliderCount { get; private set; }

        public float Rand(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
        public bool Chance(float p) { return Rng.NextDouble() < p; }

        /// <summary>Height of the actual terrain mesh at (x,z) - use for placing anything on the ground.</summary>
        public float Ground(float x, float z) { return World.TerrainHeight(x, z); }
        public float Ground(Vector2 p) { return World.TerrainHeight(p.x, p.y); }

        /// <summary>Distance from p to the nearest road edge (99 when none within 40 m).</summary>
        public float EdgeDist(Vector2 p) { return Field.EdgeDistance(p); }

        /// <summary>True when p is inside this region's tile, shrunk by margin.</summary>
        public bool InRegion(Vector2 p, float margin = 0f)
        {
            Rect r = Region.Rect;
            return p.x > r.xMin + margin && p.x < r.xMax - margin && p.y > r.yMin + margin && p.y < r.yMax - margin;
        }

        // ---- occupancy (keeps props from overlapping each other; 16 m hash)
        static long Cell(float x, float z) { return WorldField.Key(Mathf.FloorToInt(x / 16f), Mathf.FloorToInt(z / 16f)); }

        public bool Free(Vector2 p, float r)
        {
            int cx = Mathf.FloorToInt(p.x / 16f), cz = Mathf.FloorToInt(p.y / 16f);
            int span = 1 + Mathf.CeilToInt(r / 16f);
            for (int dx = -span; dx <= span; dx++)
                for (int dz = -span; dz <= span; dz++)
                {
                    List<Vector3> l;
                    if (!occupied.TryGetValue(WorldField.Key(cx + dx, cz + dz), out l)) continue;
                    foreach (var o in l)
                    {
                        float rr = r + o.z;
                        if ((new Vector2(o.x, o.y) - p).sqrMagnitude < rr * rr) return false;
                    }
                }
            return true;
        }

        public void Reserve(Vector2 p, float r)
        {
            long k = Cell(p.x, p.y);
            List<Vector3> l;
            if (!occupied.TryGetValue(k, out l)) { l = new List<Vector3>(); occupied[k] = l; }
            l.Add(new Vector3(p.x, p.y, r));
        }

        // ---- static geometry
        /// <summary>Merge a model into the region's static meshes (streamed with the region).</summary>
        internal void Stamp(PropModel m, Vector3 pos, float yaw, float scale = 1f) { Meshes.Stamp(m, pos, yaw, scale); }
        internal void Stamp(PropModel m, Matrix4x4 mat) { Meshes.Stamp(m, mat, mat.MultiplyPoint3x4(Vector3.zero)); }
        /// <summary>Merge a model into the always-visible skyline meshes (tall landmarks only).</summary>
        internal void StampSkyline(PropModel m, Vector3 pos, float yaw, float scale = 1f) { SkylineMeshes.Stamp(m, pos, yaw, scale); }
        internal void StampSkyline(PropModel m, Matrix4x4 mat) { SkylineMeshes.Stamp(m, mat, mat.MultiplyPoint3x4(Vector3.zero)); }

        /// <summary>Static box collider (world centre, full size, yaw degrees). Optional driving surface on top.</summary>
        public GameObject Box(Vector3 centre, Vector3 size, float yaw, Surface? surface = null)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(Colliders, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<BoxCollider>().size = size;
            if (surface.HasValue) go.AddComponent<SurfaceTag>().surface = surface.Value;
            ColliderCount++;
            return go;
        }

        /// <summary>Static capsule collider (tree trunks, poles).</summary>
        public GameObject Capsule(Vector3 bottom, float radius, float height)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(Colliders, false);
            go.transform.position = bottom;
            var c = go.AddComponent<CapsuleCollider>();
            c.radius = radius; c.height = height; c.center = new Vector3(0f, height * 0.5f, 0f);
            ColliderCount++;
            return go;
        }

        /// <summary>A standalone mesh object (for things that need their own material or animation).</summary>
        public GameObject AddMesh(string name, Mesh mesh, Material mat, int layer = 0, bool shadows = true, bool skyline = false, Transform parent = null)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent != null ? parent : (skyline ? Skyline : Root), false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;
            return go;
        }

        /// <summary>Mesh collider from a mesh (use sparingly: terrain-like surfaces, ramps, bridges).</summary>
        public GameObject MeshColliderFor(string name, Mesh mesh, Surface surface)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Colliders, false);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<SurfaceTag>().surface = surface;
            ColliderCount++;
            return go;
        }

        // ---- shared materials (same set as WorldProps; glow follows the night)
        public Material GlowMaterial { get { return GlowMat; } }
        public Material WaterMaterial { get { return WaterMat; } }

        internal void MakeMaterials()
        {
            foreach (var pm in new[] { Meshes, SkylineMeshes })
            {
                var m = pm.mats;
                m[(int)PK.Props] = MaterialLibrary.VertexColorWind;
                m[(int)PK.Landmark] = MaterialLibrary.VertexColorWind;
                if (StrongMat == null)
                {
                    StrongMat = new Material(MaterialLibrary.VertexColorWind) { name = "BR_RegionWindStrong" };
                    StrongMat.SetFloat("_WindStrength", 0.42f);
                    StrongMat.SetFloat("_WindSpeed", 1.6f);
                    GlowMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropGlow")) { name = "BR_RegionGlow" };
                    WaterMat = new Material(MaterialLibrary.Water) { name = "BR_RegionWater" };
                    StreamMat = new Material(MaterialLibrary.FindShader("BlossomRally/FlowFoam")) { name = "BR_RegionStream" };
                    StreamMat.SetColor("_WaterColor", new Color(0.62f, 0.82f, 0.9f, 0.55f));
                    StreamMat.SetColor("_FoamColor", new Color(1f, 1f, 1f, 0.9f));
                    StreamMat.SetVector("_Tiling", new Vector4(4f, 1f, 0f, 0f));
                    StreamMat.SetFloat("_Speed", 0.9f);
                    StreamMat.SetFloat("_Streak", 0.6f);
                    StreamMat.SetFloat("_Lit", 0.6f);
                    ShaftMat = new Material(MaterialLibrary.FindShader("BlossomRally/PropAdditive")) { name = "BR_RegionShafts" };
                    ShaftMat.SetTexture("_MainTex", ProceduralTextures.Soft);
                    ShaftMat.SetVector("_Fade", new Vector4(3f, 14f, 150f, 230f));
                    ShaftMat.SetFloat("_EdgeFade", 1f);
                    ShaftMat.SetFloat("_VertexGamma", 1f);
                }
                m[(int)PK.Strong] = StrongMat;
                m[(int)PK.Glow] = GlowMat;
                m[(int)PK.Water] = WaterMat;
                m[(int)PK.Stream] = StreamMat;
                m[(int)PK.Blades] = MaterialLibrary.Grass;
                m[(int)PK.Shafts] = ShaftMat;
                m[(int)PK.Karst] = MaterialLibrary.Terrain;
            }
        }

        internal void UpdateMaterials()
        {
            float night = Atmosphere.NightFactor;
            if (GlowMat != null) GlowMat.SetFloat("_Glow", 0.3f + 2.3f * night + 0.25f * Atmosphere.CloudCover);
            if (ShaftMat != null)
            {
                float a = (1f - night) * (1f - Atmosphere.CloudCover * 0.8f) * (1f - Atmosphere.RainIntensity) * 0.55f;
                Color sun = new Color(1f, 0.95f, 0.82f);
                var s = Atmosphere.Sun;
                if (s != null) sun = Color.Lerp(sun, s.color, 0.5f);
                sun.a = Mathf.Clamp01(a);
                ShaftMat.SetColor("_Tint", sun);
            }
        }

        internal void Finish()
        {
            MakeMaterials();
            Meshes.Finish(Root);
            SkylineMeshes.Finish(Skyline);
            UpdateMaterials();
            Debug.Log("[BlossomRally] Region " + Region.Name + ": " + (Meshes.Renderers + SkylineMeshes.Renderers) + " merged renderers, "
                      + (Meshes.Triangles + SkylineMeshes.Triangles) + " tris, " + ColliderCount + " colliders");
        }
    }
}
