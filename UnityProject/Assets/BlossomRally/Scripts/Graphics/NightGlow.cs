// NightGlow.cs - warm night lighting without real lights:
//  * light pools: additive soft blobs projected on the ground (5x5 grids conformed by raycasts) under every
//    lantern (all renderers using the lantern Glow material under World_Static / World_Dynamic: lantern
//    posts, house and gate lanterns). On wet roads the pool also draws the long warm reflection streak
//    of the lantern towards the camera (BR_LightPool).
//  * glowing house windows: additive panels behind the real lattice of every ENV_HouseTwoStorey /
//    ENV_HouseShop (window positions from the art pipeline's env_architecture.py), plus the shop interior.
//  * globals for other shaders: _BR_LightPools[16] (xyz ground centre, w radius, nearest to the camera),
//    _BR_LightPoolCount, _BR_LightPoolColor (linear, already scaled by night) - for BR_Road wet glints.
// One draw call each; both renderers switch off by day.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public class NightGlow : MonoBehaviour
    {
        public WorldBuilder world;

        struct Pool { public Vector3 ground; public float height, radius, intensity; public bool road; public int count; public Vector3 lamp; }

        readonly List<Pool> pools = new List<Pool>();
        MeshRenderer poolRenderer, windowRenderer;
        Material poolMat, windowMat;
        readonly Vector4[] nearest = new Vector4[16];
        readonly float[] nearestD = new float[16];
        float timer;
        bool qualityOk = true;

        const float GeoRadius = 8f;
        const int Grid = 5;

        static readonly int IdPools = Shader.PropertyToID("_BR_LightPools");
        static readonly int IdPoolCount = Shader.PropertyToID("_BR_LightPoolCount");
        static readonly int IdPoolColor = Shader.PropertyToID("_BR_LightPoolColor");

        static int RayMask { get { return ~((1 << WorldBuilder.LayerGrass) | (1 << WorldBuilder.LayerTrees) | (1 << CarExtras.CarLayer) | (1 << 2)); } }

        public IEnumerator Build()
        {
            var psh = Shader.Find("BlossomRally/LightPool");
            var wsh = Shader.Find("BlossomRally/WindowGlow");
            if (psh != null) poolMat = new Material(psh) { name = "BR_LightPool" };
            if (wsh != null) windowMat = new Material(wsh) { name = "BR_WindowGlow" };
            if (poolMat != null) poolMat.SetTexture("_NoiseTex", GfxTextures.Noise);

            // ---- lanterns ----
            var glowMat = MaterialLibrary.TryGet("Glow");
            var lamps = new List<Vector3>();
            foreach (var rootName in new[] { "World_Static", "World_Dynamic" })
            {
                var root = GameObject.Find(rootName);
                if (root == null || glowMat == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    var mats = r.sharedMaterials;
                    bool glow = false;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] == glowMat) { glow = true; break; }
                    if (!glow) continue;
                    Bounds b = r.bounds;
                    if (b.size.magnitude > 4f) continue;     // only lantern-sized glow parts
                    lamps.Add(b.center);
                }
            }
            // merge lanterns that hang close together into one brighter pool
            var used = new bool[lamps.Count];
            for (int i = 0; i < lamps.Count; i++)
            {
                if (used[i]) continue;
                Vector3 sum = lamps[i];
                int n = 1;
                for (int j = i + 1; j < lamps.Count; j++)
                {
                    if (used[j]) continue;
                    Vector3 d = lamps[j] - lamps[i];
                    if (new Vector2(d.x, d.z).sqrMagnitude < 2.2f * 2.2f && Mathf.Abs(d.y) < 3.5f) { used[j] = true; sum += lamps[j]; n++; }
                }
                Vector3 c = sum / n;
                bool road;
                float gy = Ground(c.x, c.z, c.y, out road);
                float hgt = Mathf.Clamp(c.y - gy, 0.5f, 9f);
                pools.Add(new Pool
                {
                    ground = new Vector3(c.x, gy, c.z), lamp = c, height = hgt, road = road, count = n,
                    radius = Mathf.Clamp(2.4f + hgt * 0.5f, 3f, 5.5f),
                    intensity = Mathf.Clamp(1.25f - hgt * 0.07f, 0.55f, 1.2f) * Mathf.Min(1.6f, 1f + (n - 1) * 0.25f)
                });
            }
            yield return null;
            if (poolMat != null && pools.Count > 0)
            {
                Mesh m = null;
                yield return BuildPoolMesh(mesh => m = mesh);
                if (m != null) poolRenderer = MakeRenderer("BR_LightPools", m, poolMat);
            }
            yield return null;

            // ---- house windows ----
            if (windowMat != null)
            {
                var wm = BuildWindowMesh();
                if (wm != null) windowRenderer = MakeRenderer("BR_HouseWindows", wm, windowMat);
            }
            ApplyQuality();
        }

        float Ground(float x, float z, float fromY, out bool road)
        {
            RaycastHit hit;
            road = false;
            if (Physics.Raycast(new Vector3(x, fromY + 0.05f, z), Vector3.down, out hit, 30f, RayMask, QueryTriggerInteraction.Ignore))
            {
                road = hit.collider.GetComponent<SurfaceTag>() != null;
                return hit.point.y;
            }
            return world != null ? world.TerrainHeight(x, z) : 0f;
        }

        IEnumerator BuildPoolMesh(System.Action<Mesh> done)
        {
            var verts = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var uv2 = new List<Vector4>();
            var cols = new List<Color32>();
            var tris = new List<int>();
            var rng = new System.Random(77);
            for (int p = 0; p < pools.Count; p++)
            {
                var pl = pools[p];
                int b = verts.Count;
                byte warm = (byte)(200 + rng.Next(55));
                var col = new Color32(255, (byte)(150 + rng.Next(30)), (byte)(80 + rng.Next(30)), warm);
                float phase = (float)rng.NextDouble() * 6.28f;
                for (int j = 0; j < Grid; j++)
                    for (int i = 0; i < Grid; i++)
                    {
                        float lx = (i / (float)(Grid - 1) * 2f - 1f) * GeoRadius, lz = (j / (float)(Grid - 1) * 2f - 1f) * GeoRadius;
                        float x = pl.ground.x + lx, z = pl.ground.z + lz;
                        bool r;
                        float y = Ground(x, z, pl.ground.y + 3f, out r);
                        if (Mathf.Abs(y - pl.ground.y) > 4f) y = pl.ground.y;     // walls / drops: stay flat
                        verts.Add(new Vector3(x, y + 0.05f, z));
                        uv0.Add(new Vector2(lx, lz));
                        uv1.Add(new Vector4(pl.ground.x, pl.ground.y, pl.ground.z, pl.height));
                        uv2.Add(new Vector4(pl.radius, pl.intensity, r || pl.road ? 1f : 0.35f, phase));
                        cols.Add(col);
                    }
                for (int j = 0; j < Grid - 1; j++)
                    for (int i = 0; i < Grid - 1; i++)
                    {
                        int a = b + j * Grid + i, c = a + Grid;
                        tris.Add(a); tris.Add(c); tris.Add(a + 1);
                        tris.Add(a + 1); tris.Add(c); tris.Add(c + 1);
                    }
                if (p % 24 == 23) yield return null;
            }
            var m = new Mesh { name = "BR_LightPools", indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetUVs(2, uv2);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            done(m);
        }

        // window layout per house model (Blender coordinates: x right, y = -front, z up) from
        // ArtPipeline/Blender/Scripts/environment/env_architecture.py. Unity local = (x, z, y), front = -Z.
        struct Win { public float x, y, z, w, h, nx, ny, glow; public Win(float x, float y, float z, float w, float h, float nx, float ny, float glow) { this.x = x; this.y = y; this.z = z; this.w = w; this.h = h; this.nx = nx; this.ny = ny; this.glow = glow; } }

        static readonly Win[] TwoStorey =
        {
            new Win(-2.4f, -2.642f, 1.95f, 1.4f, 1.0f, 0f, -1f, 1f), new Win(2.4f, -2.642f, 1.95f, 1.4f, 1.0f, 0f, -1f, 1f),
            new Win(3.642f, -0.8f, 1.95f, 1.0f, 0.9f, 1f, 0f, 0.9f), new Win(3.642f, 0.8f, 1.95f, 1.0f, 0.9f, 1f, 0f, 0.9f),
            new Win(-3.642f, -0.8f, 1.95f, 1.0f, 0.9f, -1f, 0f, 0.9f), new Win(-3.642f, 0.8f, 1.95f, 1.0f, 0.9f, -1f, 0f, 0.9f),
            new Win(-1.75f, -2.042f, 5.15f, 1.05f, 0.95f, 0f, -1f, 1f), new Win(0f, -2.042f, 5.15f, 1.05f, 0.95f, 0f, -1f, 1f),
            new Win(1.75f, -2.042f, 5.15f, 1.05f, 0.95f, 0f, -1f, 1f),
        };

        static readonly Win[] Shop =
        {
            new Win(3.942f, 0f, 1.9f, 1.0f, 1.0f, 1f, 0f, 0.9f), new Win(3.942f, 1.6f, 1.9f, 1.0f, 1.0f, 1f, 0f, 0.9f),
            new Win(-3.942f, 0f, 1.9f, 1.0f, 1.0f, -1f, 0f, 0.9f), new Win(-3.942f, 1.6f, 1.9f, 1.0f, 1.0f, -1f, 0f, 0.9f),
            new Win(0f, -1.315f, 2.02f, 5.7f, 1.65f, 0f, -1f, 0.65f),    // warm shop interior behind the counter
        };

        Mesh BuildWindowMesh()
        {
            var root = GameObject.Find("World_Static");
            if (root == null) return null;
            var verts = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var tris = new List<int>();
            var rng = new System.Random(31);
            foreach (Transform t in root.transform)
            {
                bool two = t.name == "ENV_HouseTwoStorey", shop = t.name == "ENV_HouseShop";
                if (!two && !shop) continue;
                // sanity check the model scale against the kit (8.29 m / 5.19 m tall)
                float scale = 1f;
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.EndsWith("_Mesh") || r.name.Contains("Lantern")) continue;
                    float s = r.bounds.size.y / (two ? 8.29f : 5.19f);
                    if (s > 0.6f && s < 1.7f && Mathf.Abs(s - 1f) > 0.08f) scale = s;
                    break;
                }
                var set = two ? TwoStorey : Shop;
                for (int k = 0; k < set.Length; k++)
                {
                    var w = set[k];
                    bool lit = w.glow < 0.8f || rng.NextDouble() < 0.8;
                    if (!lit) continue;
                    Vector3 c = t.TransformPoint(new Vector3(w.x, w.z, w.y) * scale);
                    Vector3 n = t.TransformDirection(new Vector3(w.nx, 0f, w.ny)).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, n).normalized * (w.w * 0.46f * scale);
                    Vector3 up = Vector3.up * (w.h * 0.45f * scale);
                    c += n * 0.004f;
                    int b = verts.Count;
                    verts.Add(c - right - up); verts.Add(c - right + up); verts.Add(c + right + up); verts.Add(c + right - up);
                    uv0.Add(new Vector2(0f, 0f)); uv0.Add(new Vector2(0f, 1f)); uv0.Add(new Vector2(1f, 1f)); uv0.Add(new Vector2(1f, 0f));
                    var data = new Vector4(w.glow * Mathf.Lerp(0.75f, 1.15f, (float)rng.NextDouble()), (float)rng.NextDouble() * 6.28f,
                                           rng.NextDouble() < 0.25 ? 1f : 0f, (float)rng.NextDouble());
                    for (int v = 0; v < 4; v++) uv1.Add(data);
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            if (verts.Count == 0) return null;
            var m = new Mesh { name = "BR_HouseWindows" };
            m.SetVertices(verts);
            m.SetUVs(0, uv0);
            m.SetUVs(1, uv1);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        MeshRenderer MakeRenderer(string name, Mesh m, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return mr;
        }

        public void ApplyQuality()
        {
            qualityOk = GraphicsFX.Q >= 0;
            if (poolMat != null) poolMat.SetFloat("_WetStreaks", GraphicsFX.Q >= 1 ? 1f : 0f);
            Refresh();
        }

        void Refresh()
        {
            float night = Atmosphere.NightFactor;
            bool on = qualityOk && night > 0.15f;
            if (poolRenderer != null) poolRenderer.enabled = on;
            if (windowRenderer != null) windowRenderer.enabled = on;
        }

        void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.25f;
            Refresh();
            float night = qualityOk ? Atmosphere.NightFactor : 0f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, night));
            Color warm = new Color(1f, 0.62f, 0.32f).linear * (0.9f * k);
            Shader.SetGlobalColor(IdPoolColor, warm);
            // nearest pools for other shaders (wet road glints)
            var cam = Camera.main;
            int count = 0;
            if (cam != null && k > 0.001f)
            {
                Vector3 cp = cam.transform.position;
                for (int i = 0; i < nearestD.Length; i++) nearestD[i] = float.MaxValue;
                for (int p = 0; p < pools.Count; p++)
                {
                    float d = (pools[p].ground - cp).sqrMagnitude;
                    if (d > 160f * 160f || d >= nearestD[nearestD.Length - 1]) continue;
                    int at = nearestD.Length - 1;
                    while (at > 0 && nearestD[at - 1] > d) { nearestD[at] = nearestD[at - 1]; nearest[at] = nearest[at - 1]; at--; }
                    nearestD[at] = d;
                    var g = pools[p].ground;
                    nearest[at] = new Vector4(g.x, g.y, g.z, pools[p].radius);
                }
                for (int i = 0; i < nearestD.Length; i++) if (nearestD[i] < float.MaxValue) count++;
            }
            Shader.SetGlobalVectorArray(IdPools, nearest);
            Shader.SetGlobalFloat(IdPoolCount, count);
        }
    }
}
