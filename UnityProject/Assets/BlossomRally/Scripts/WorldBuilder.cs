// WorldBuilder.cs - builds the Blossom Valley at runtime from MapData + the Blender environment kit:
// terrain (with river / lake carving and road flattening), roads with markings, water, bridges,
// village, temple pieces, cherry blossom avenues, bamboo forest, tea terraces and grass.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlossomRally
{
    public struct RoadSample
    {
        public Vector2 pos;
        public Vector2 tangent;
        public float height;
        public float halfWidth;
        public int road;
        public bool bridge;
        public float distance;
    }

    public class WorldBuilder : MonoBehaviour
    {
        public const int LayerGrass = 8;
        public const int LayerTrees = 9;
        const float TerrainHalf = 660f;
        const float TerrainCell = 4f;
        const float RoadLift = 0.05f;

        public readonly List<RoadDef> roads = MapData.Roads();
        public readonly List<List<RoadSample>> roadSamples = new List<List<RoadSample>>();
        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }
        public bool Done { get; private set; }
        public string Status { get; private set; } = "Preparing";
        public float Progress { get; private set; }

        readonly Dictionary<long, List<int>> roadGrid = new Dictionary<long, List<int>>();
        readonly List<RoadSample> allSamples = new List<RoadSample>();
        readonly List<Vector2> riverPts = new List<Vector2>();
        readonly Dictionary<long, List<int>> riverGrid = new Dictionary<long, List<int>>();
        readonly List<Vector4> bridges = new List<Vector4>();            // x, z, yaw(deg), deck height
        readonly List<Vector4> pads = new List<Vector4>();               // x, z, radius, height
        readonly List<Vector3> blossomSpots = new List<Vector3>();       // x, z, radius
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        readonly System.Random rng = new System.Random(20261108);

        Transform staticRoot, dynamicRoot, grassRoot;

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        // ------------------------------------------------------------------ height field
        public float BaseHeight(float x, float z)
        {
            float h = 0.9f * Mathf.Sin(x * 0.013f) * Mathf.Sin(z * 0.011f) + 0.6f * Mathf.Sin(x * 0.031f + z * 0.017f + 1.3f)
                      + 0.3f * Mathf.Sin(x * 0.07f - z * 0.05f);
            Vector2 mc = MapData.MountainCenter;
            float dm2 = (x - mc.x) * (x - mc.x) + (z - mc.y) * (z - mc.y);
            h += MapData.MountainHeight * Mathf.Exp(-dm2 / (2f * MapData.MountainSigma * MapData.MountainSigma));
            float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            h += Spline.SmoothStep(450f, 640f, m) * (36f + 12f * Mathf.Sin(x * 0.011f + z * 0.009f));
            if (x > 270f)
            {
                float band = Spline.SmoothStep(-380f, -310f, z) * (1f - Spline.SmoothStep(370f, 430f, z));
                h += (x - 270f) * 0.09f * band;
            }
            if (MapData.InBambooForest(new Vector2(x, z))) h += 1.6f * Mathf.Sin(x * 0.05f) * Mathf.Sin(z * 0.045f);
            return h;
        }

        public float HeightAt(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            float h = BaseHeight(x, z);
            float dr = RiverDistance(p);
            if (dr < 17f) h = Mathf.Lerp(-2.8f, h, Spline.SmoothStep(7.5f, 17f, dr));
            float k = MapData.LakeK(p);
            if (k < 1.4f) h = Mathf.Lerp(-2.8f, h, Spline.SmoothStep(0.92f, 1.4f, k));
            foreach (var pad in pads)
            {
                float d = Vector2.Distance(p, new Vector2(pad.x, pad.y));
                if (d < pad.z * 1.8f) h = Mathf.Lerp(pad.w, h, Spline.SmoothStep(pad.z, pad.z * 1.8f, d));
            }
            RoadSample rs;
            float dist = NearestRoad(p, out rs, 30f);
            if (dist < 30f && !rs.bridge)
            {
                float inner = rs.halfWidth + 1.0f;
                float target = rs.height - 0.08f;
                h = Mathf.Lerp(target, h, Spline.SmoothStep(inner, inner + 9f, dist));
            }
            return h;
        }

        public float RiverDistance(Vector2 p)
        {
            int cx = Mathf.FloorToInt(p.x / 24f), cz = Mathf.FloorToInt(p.y / 24f);
            float best = 999f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    List<int> list;
                    if (!riverGrid.TryGetValue(Key(cx + dx, cz + dz), out list)) continue;
                    foreach (int i in list)
                    {
                        float t;
                        float d = Spline.DistToSegment(p, riverPts[i], riverPts[i + 1], out t);
                        if (d < best) best = d;
                    }
                }
            return best;
        }

        public float NearestRoad(Vector2 p, out RoadSample sample, float maxDist = 40f)
        {
            sample = default(RoadSample);
            int r = Mathf.CeilToInt(maxDist / 16f);
            int cx = Mathf.FloorToInt(p.x / 16f), cz = Mathf.FloorToInt(p.y / 16f);
            float best = float.MaxValue;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    List<int> list;
                    if (!roadGrid.TryGetValue(Key(cx + dx, cz + dz), out list)) continue;
                    foreach (int i in list)
                    {
                        var s = allSamples[i];
                        float d = (s.pos - p).sqrMagnitude;
                        if (d < best) { best = d; sample = s; }
                    }
                }
            return best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
        }

        // ------------------------------------------------------------------ build
        public IEnumerator Build()
        {
            staticRoot = new GameObject("World_Static").transform;
            dynamicRoot = new GameObject("World_Dynamic").transform;
            grassRoot = new GameObject("World_Grass").transform;

            Status = "Tracing the river"; Progress = 0.02f;
            var river = Spline.Sample(MapData.River, false, 6f);
            riverPts.AddRange(river);
            for (int i = 0; i < riverPts.Count - 1; i++)
            {
                Vector2 a = riverPts[i], b = riverPts[i + 1];
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / 24f) - 1, x1 = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / 24f) + 1;
                int z0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) / 24f) - 1, z1 = Mathf.FloorToInt(Mathf.Max(a.y, b.y) / 24f) + 1;
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        List<int> l;
                        if (!riverGrid.TryGetValue(Key(x, z), out l)) { l = new List<int>(); riverGrid[Key(x, z)] = l; }
                        l.Add(i);
                    }
            }
            yield return null;

            Status = "Laying out the roads"; Progress = 0.06f;
            BuildRoadSamples();
            yield return null;

            Status = "Planning the village and gardens"; Progress = 0.1f;
            PlanPads();
            var plan = PlanVegetation();
            yield return null;

            Status = "Shaping the valley"; Progress = 0.15f;
            yield return BuildTerrain();

            Status = "Paving roads"; Progress = 0.45f;
            BuildRoadMeshes();
            BuildWater();
            yield return null;

            Status = "Raising the village"; Progress = 0.55f;
            BuildLandmarks();
            yield return null;

            Status = "Planting cherry blossoms"; Progress = 0.62f;
            yield return SpawnVegetation(plan);

            Status = "Finishing touches"; Progress = 0.94f;
            BuildBoundary();
            StaticBatchingUtility.Combine(staticRoot.gameObject);
            StaticBatchingUtility.Combine(grassRoot.gameObject);
            Progress = 1f;
            Status = "Ready";
            Done = true;
        }

        void BuildRoadSamples()
        {
            for (int r = 0; r < roads.Count; r++)
            {
                var def = roads[r];
                var pts = Spline.Sample(def.points, def.closed, 2f);
                var list = new List<RoadSample>(pts.Count);
                float dist = 0f;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector2 prev = pts[def.closed ? (i - 1 + pts.Count) % pts.Count : Mathf.Max(0, i - 1)];
                    Vector2 next = pts[def.closed ? (i + 1) % pts.Count : Mathf.Min(pts.Count - 1, i + 1)];
                    if (i > 0) dist += Vector2.Distance(pts[i], pts[i - 1]);
                    var s = new RoadSample
                    {
                        pos = pts[i], tangent = (next - prev).normalized, halfWidth = def.width * 0.5f, road = r,
                        height = BaseHeight(pts[i].x, pts[i].y), distance = dist
                    };
                    list.Add(s);
                }
                // smooth heights along the road for a drivable profile
                int n = list.Count;
                var hs = new float[n];
                for (int pass = 0; pass < 3; pass++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        float sum = 0f; int cnt = 0;
                        for (int k = -10; k <= 10; k++)
                        {
                            int j = i + k;
                            if (def.closed) j = (j % n + n) % n; else if (j < 0 || j >= n) continue;
                            sum += list[j].height; cnt++;
                        }
                        hs[i] = sum / cnt;
                    }
                    for (int i = 0; i < n; i++) { var s = list[i]; s.height = hs[i]; list[i] = s; }
                }
                // limit the gradient to a drivable 10% (terrain is cut / filled to match)
                const float maxGrade = 0.10f;
                for (int pass = 0; pass < 4; pass++)
                {
                    for (int i = 1; i < n + (def.closed ? 1 : 0); i++)
                    {
                        int a = (i - 1) % n, b = i % n;
                        float ds = Vector2.Distance(list[a].pos, list[b].pos) * maxGrade;
                        var sb = list[b]; sb.height = Mathf.Clamp(sb.height, list[a].height - ds, list[a].height + ds); list[b] = sb;
                    }
                    for (int i = n - 2 + (def.closed ? 1 : 0); i >= 0; i--)
                    {
                        int a = (i + 1) % n, b = i % n;
                        float ds = Vector2.Distance(list[a].pos, list[b].pos) * maxGrade;
                        var sb = list[b]; sb.height = Mathf.Clamp(sb.height, list[a].height - ds, list[a].height + ds); list[b] = sb;
                    }
                }
                roadSamples.Add(list);
            }
            // bridges where any road crosses the river
            for (int r = 0; r < roadSamples.Count; r++)
            {
                var list = roadSamples[r];
                for (int i = 0; i < list.Count - 1; i++)
                {
                    for (int k = 0; k < riverPts.Count - 1; k++)
                    {
                        Vector2 hit;
                        if (!Spline.SegmentIntersect(list[i].pos, list[i + 1].pos, riverPts[k], riverPts[k + 1], out hit)) continue;
                        float halfLen = 15f;
                        float hEnds = 0f; int cnt = 0;
                        for (int j = 0; j < list.Count; j++)
                        {
                            float d = Vector2.Distance(list[j].pos, hit);
                            if (d < halfLen + 1.5f)
                            {
                                var s = list[j]; s.bridge = true; list[j] = s;
                                if (d > halfLen - 1.5f) { hEnds += s.height; cnt++; }
                            }
                        }
                        Vector2 t = list[i].tangent;
                        float yaw = Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg;
                        bridges.Add(new Vector4(hit.x, hit.y, yaw, cnt > 0 ? hEnds / cnt : 0f));
                    }
                }
            }
            for (int r = 0; r < roadSamples.Count; r++)
                foreach (var s in roadSamples[r])
                {
                    int idx = allSamples.Count;
                    allSamples.Add(s);
                    long key = Key(Mathf.FloorToInt(s.pos.x / 16f), Mathf.FloorToInt(s.pos.y / 16f));
                    List<int> l;
                    if (!roadGrid.TryGetValue(key, out l)) { l = new List<int>(); roadGrid[key] = l; }
                    l.Add(idx);
                }
        }

        readonly List<Vector4> houseSpots = new List<Vector4>();   // x, z, yaw, type

        void PlanPads()
        {
            // Blossom Village: houses line Lantern Street on both sides, facing the street
            var street = roadSamples[4];
            int idx = 0;
            for (int i = 14; i < street.Count - 8; i += 13)
            {
                var s = street[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 p = s.pos + nrm * side * (s.halfWidth + 10.5f);
                    if (Vector2.Distance(p, MapData.Garage) < 22f || Vector2.Distance(p, MapData.Gate) < 14f) continue;
                    RoadSample other;
                    if (NearestRoad(p, out other, 30f) < other.halfWidth + 7f) continue;
                    Vector2 face = -nrm * side;                        // front (-Z) must point at the street
                    float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;
                    houseSpots.Add(new Vector4(p.x, p.y, yaw, idx++ % 3 == 1 ? 1 : 0));
                    pads.Add(new Vector4(p.x, p.y, 9f, s.height));
                }
            }
            Vector2 g = MapData.Garage;
            pads.Add(new Vector4(g.x, g.y, 10f, BaseHeight(g.x, g.y)));
            Vector2 pg = MapData.PagodaTop;
            pads.Add(new Vector4(pg.x, pg.y, 12f, BaseHeight(pg.x, pg.y) - 0.5f));
        }

        struct Plant { public string res; public Vector3 pos; public float yaw; public float scale; public int kind; }

        List<Plant> PlanVegetation()
        {
            var plants = new List<Plant>();
            var taken = new List<Vector3>();
            System.Func<Vector2, float, bool> free = (p, r) =>
            {
                foreach (var t in taken) if ((new Vector2(t.x, t.z) - p).sqrMagnitude < (r + t.y) * (r + t.y)) return false;
                return true;
            };
            System.Func<Vector2, float, bool> clearOfFeatures = (p, margin) =>
            {
                RoadSample rs;
                if (NearestRoad(p, out rs, 40f) < rs.halfWidth + margin) return false;
                if (RiverDistance(p) < 18f) return false;
                if (MapData.LakeK(p) < 1.45f) return false;
                if (MapData.InVillage(p) && margin < 30f) return false;
                if (Mathf.Abs(p.x) > 470f || Mathf.Abs(p.y) > 470f) return false;
                return true;
            };

            // blossom avenues along the main loop and lantern street
            var loop = roadSamples[0];
            for (int i = 0; i < loop.Count; i += 11)
            {
                var s = loop[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    Vector2 p = s.pos + nrm * side * (s.halfWidth + Rand(6.5f, 10f));
                    if (!clearOfFeatures(p, 5.5f) || MapData.InBambooForest(p) || MapData.InTeaTerraces(p) || !free(p, 7f)) continue;
                    int kind = rng.NextDouble() < 0.12 ? 0 : (rng.NextDouble() < 0.6 ? 1 : 2);
                    AddTree(plants, taken, p, kind);
                }
            }
            // scattered blossoms across the valley
            for (int tries = 0; tries < 2600 && plants.Count < 175; tries++)
            {
                Vector2 p = new Vector2(Rand(-460f, 460f), Rand(-440f, 440f));
                if (!clearOfFeatures(p, 7f) || MapData.InBambooForest(p) || MapData.InTeaTerraces(p) || !free(p, 9f)) continue;
                float dm = Vector2.Distance(p, MapData.MountainCenter);
                if (dm < 150f && rng.NextDouble() < 0.75) continue;
                int kind = rng.NextDouble() < 0.07 ? 0 : (rng.NextDouble() < 0.55 ? 1 : 2);
                AddTree(plants, taken, p, kind);
            }
            // bamboo forest
            Vector2 a = MapData.P(62, 310), b = MapData.P(350, 1270);
            for (float x = a.x + 10f; x < b.x - 6f; x += 36f)
                for (float z = b.y + 10f; z < a.y - 6f; z += 36f)
                {
                    Vector2 p = new Vector2(x + Rand(-9f, 9f), z + Rand(-9f, 9f));
                    if (!clearOfFeatures(p, 4f) || !free(p, 4f)) continue;
                    plants.Add(new Plant { res = "ENV_Bamboo", pos = new Vector3(p.x, 0, p.y), yaw = Rand(0, 360), scale = Rand(0.9f, 1.3f), kind = 3 });
                    taken.Add(new Vector3(p.x, 4f, p.y));
                }
            // tea terraces (modules step up eastwards)
            Vector2 ta = MapData.P(1190, 340), tb = MapData.P(1466, 1310);
            for (float x = ta.x + 2f; x < 440f; x += 10.4f)
                for (float z = tb.y + 8f; z < ta.y - 8f; z += 16f)
                {
                    Vector2 p = new Vector2(x, z);
                    RoadSample rs;
                    if (NearestRoad(p + new Vector2(5f, 0f), out rs, 30f) < rs.halfWidth + 9f) continue;
                    plants.Add(new Plant { res = "ENV_TeaTerrace", pos = new Vector3(x, 0, z), yaw = 90f, scale = 1f, kind = 4 });
                }
            // grass verges along every road + meadow tufts
            foreach (var list in roadSamples)
                for (int i = 0; i < list.Count; i += 3)
                {
                    var s = list[i];
                    if (s.bridge) continue;
                    Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 p = s.pos + nrm * side * (s.halfWidth + Rand(1.2f, 7f)) + s.tangent * Rand(-2f, 2f);
                        if (RiverDistance(p) < 14f || MapData.LakeK(p) < 1.25f) continue;
                        plants.Add(new Plant { res = "ENV_GrassClump", pos = new Vector3(p.x, 0, p.y), yaw = Rand(0, 360), scale = Rand(0.9f, 1.6f), kind = 5 });
                    }
                }
            for (int i = 0; i < 1400; i++)
            {
                Vector2 p = new Vector2(Rand(-460f, 460f), Rand(-460f, 440f));
                if (!clearOfFeatures(p, 2f) || MapData.InTeaTerraces(p)) continue;
                plants.Add(new Plant { res = "ENV_GrassClump", pos = new Vector3(p.x, 0, p.y), yaw = Rand(0, 360), scale = Rand(0.9f, 1.7f), kind = 5 });
            }
            return plants;
        }

        void AddTree(List<Plant> plants, List<Vector3> taken, Vector2 p, int kind)
        {
            string res = kind == 0 ? "ENV_CherryKing" : (kind == 1 ? "ENV_CherryTree" : "ENV_CherryYoung");
            float r = kind == 0 ? 7.5f : (kind == 1 ? 5.5f : 3.8f);
            plants.Add(new Plant { res = res, pos = new Vector3(p.x, 0, p.y), yaw = Rand(0, 360), scale = Rand(0.9f, 1.15f), kind = kind });
            taken.Add(new Vector3(p.x, r, p.y));
            blossomSpots.Add(new Vector3(p.x, p.y, r * 1.25f));
        }

        // ------------------------------------------------------------------ terrain
        IEnumerator BuildTerrain()
        {
            int n = Mathf.RoundToInt(TerrainHalf * 2f / TerrainCell) + 1;
            var verts = new Vector3[n * n];
            var cols = new Color32[n * n];
            for (int j = 0; j < n; j++)
            {
                float z = -TerrainHalf + j * TerrainCell;
                for (int i = 0; i < n; i++)
                {
                    float x = -TerrainHalf + i * TerrainCell;
                    float h = HeightAt(x, z);
                    verts[j * n + i] = new Vector3(x, h, z);
                    cols[j * n + i] = GroundColor(x, z, h);
                }
                if (j % 24 == 0) { Progress = 0.15f + 0.25f * j / n; yield return null; }
            }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { name = "BlossomValley_Terrain", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.colors32 = cols;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Terrain_Valley");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Terrain;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            Progress = 0.44f;
        }

        Color32 GroundColor(float x, float z, float h)
        {
            Vector2 p = new Vector2(x, z);
            float n1 = 0.5f + 0.5f * Mathf.Sin(x * 0.043f + Mathf.Sin(z * 0.031f) * 2f) * Mathf.Sin(z * 0.037f);
            float n2 = 0.5f + 0.5f * Mathf.Sin(x * 0.17f + z * 0.13f) * Mathf.Sin(x * 0.11f - z * 0.19f);
            Color c = Color.Lerp(new Color(0.47f, 0.70f, 0.31f), new Color(0.60f, 0.79f, 0.39f), n1);
            c = Color.Lerp(c, new Color(0.67f, 0.83f, 0.46f), Mathf.Max(0f, n2 - 0.7f) * 1.5f);
            if (MapData.InBambooForest(p)) c = Color.Lerp(c, new Color(0.45f, 0.58f, 0.27f), 0.6f);
            if (MapData.InTeaTerraces(p)) c = Color.Lerp(c, new Color(0.38f, 0.60f, 0.27f), 0.5f);
            if (MapData.InVillage(p)) c = Color.Lerp(c, new Color(0.84f, 0.77f, 0.62f), 0.85f);
            if (h > 26f) c = Color.Lerp(c, new Color(0.60f, 0.62f, 0.55f), Mathf.Clamp01((h - 26f) / 22f) * 0.65f);
            float dr = RiverDistance(p);
            float k = MapData.LakeK(p);
            if (dr < 17f || k < 1.4f)
            {
                float bank = Mathf.Max(1f - Spline.SmoothStep(9f, 17f, dr), 1f - Spline.SmoothStep(1.0f, 1.4f, k));
                c = Color.Lerp(c, new Color(0.84f, 0.78f, 0.62f), bank * 0.9f);
                if (h < -1.0f) c = Color.Lerp(c, new Color(0.45f, 0.52f, 0.46f), 0.6f);
            }
            RoadSample rs;
            float d = NearestRoad(p, out rs, 20f);
            if (d < rs.halfWidth + 4f)
                c = Color.Lerp(c, new Color(0.73f, 0.66f, 0.52f), (1f - Spline.SmoothStep(rs.halfWidth, rs.halfWidth + 4f, d)) * 0.6f);
            foreach (var bs in blossomSpots)
            {
                float db = Vector2.Distance(p, new Vector2(bs.x, bs.y));
                if (db < bs.z)
                {
                    float speck = 0.5f + 0.5f * Mathf.Sin(x * 3.7f + z * 2.9f) * Mathf.Sin(x * 1.3f - z * 4.1f);
                    Color pk = speck > 0.65f ? new Color(0.99f, 0.88f, 0.92f) : new Color(0.95f, 0.74f, 0.81f);
                    c = Color.Lerp(c, pk, Mathf.Pow(1f - db / bs.z, 0.7f) * 0.8f);
                }
            }
            float m = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            c = Color.Lerp(c, new Color(0.62f, 0.72f, 0.66f), Spline.SmoothStep(520f, 660f, m) * 0.5f);
            Color32 o = c;
            o.a = 0;
            return o;
        }

        // ------------------------------------------------------------------ roads + water
        void BuildRoadMeshes()
        {
            var lineMat = MaterialLibrary.Standard("RoadLine", new Color(0.95f, 0.94f, 0.9f), 0.35f, 0f);
            for (int r = 0; r < roads.Count; r++)
            {
                var def = roads[r];
                var list = roadSamples[r];
                string tex = def.surface == Surface.Asphalt ? "ENV_Road_Asphalt" : def.surface == Surface.Gravel ? "ENV_Road_Gravel"
                           : def.surface == Surface.Dirt ? "ENV_Road_Dirt" : "ENV_Road_Paving";
                var mat = MaterialLibrary.Standard("Road_" + def.surface, Color.white, def.surface == Surface.Asphalt ? 0.22f : 0.08f, 0f,
                                                   Resources.Load<Texture2D>("Env/" + tex));
                float tile = def.surface == Surface.Paving ? 4f : 7f;
                int count = list.Count + (def.closed ? 1 : 0);
                // split into runs that skip bridge spans
                var run = new List<RoadSample>();
                int part = 0;
                for (int i = 0; i < count; i++)
                {
                    var s = list[i % list.Count];
                    if (s.bridge || run.Count >= 240)
                    {
                        if (run.Count > 1) MakeRoadPiece(def, run, mat, lineMat, tile, part++);
                        bool keepLast = !s.bridge && run.Count > 0;
                        var last = keepLast ? run[run.Count - 1] : default(RoadSample);
                        run.Clear();
                        if (keepLast) run.Add(last);
                        if (s.bridge) continue;
                    }
                    run.Add(s);
                }
                if (run.Count > 1) MakeRoadPiece(def, run, mat, lineMat, tile, part++);
            }
        }

        void MakeRoadPiece(RoadDef def, List<RoadSample> run, Material mat, Material lineMat, float tile, int part)
        {
            int n = run.Count;
            var v = new Vector3[n * 3];
            var uv = new Vector2[n * 3];
            var tri = new List<int>();
            float startDist = run[0].distance;
            for (int i = 0; i < n; i++)
            {
                var s = run[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                float y = s.height + RoadLift;
                Vector2 l = s.pos - nrm * s.halfWidth, rr = s.pos + nrm * s.halfWidth;
                v[i * 3] = new Vector3(l.x, y, l.y);
                v[i * 3 + 1] = new Vector3(s.pos.x, y + 0.04f, s.pos.y);
                v[i * 3 + 2] = new Vector3(rr.x, y, rr.y);
                float vv = (s.distance - startDist) / tile;
                if (vv < 0) vv += 1000f;
                uv[i * 3] = new Vector2(0f, vv); uv[i * 3 + 1] = new Vector2(0.5f, vv); uv[i * 3 + 2] = new Vector2(1f, vv);
                if (i < n - 1)
                {
                    int a = i * 3;
                    tri.AddRange(new[] { a, a + 3, a + 1, a + 1, a + 3, a + 4, a + 1, a + 4, a + 2, a + 2, a + 4, a + 5 });
                }
            }
            var mesh = new Mesh { name = def.name + "_" + part, vertices = v, uv = uv };
            mesh.SetTriangles(tri, 0);
            FixWinding(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(def.name + "_" + part);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<SurfaceTag>().surface = def.surface;
            if (def.surface != Surface.Asphalt) return;
            // lane markings: solid edges + dashed centre line
            var lv = new List<Vector3>();
            var lt = new List<int>();
            for (int i = 0; i < n - 1; i++)
            {
                var s0 = run[i]; var s1 = run[i + 1];
                Vector2 n0 = new Vector2(s0.tangent.y, -s0.tangent.x), n1 = new Vector2(s1.tangent.y, -s1.tangent.x);
                float[] offs = { -(s0.halfWidth - 0.45f), s0.halfWidth - 0.45f, 0f };
                for (int k = 0; k < 3; k++)
                {
                    if (k == 2 && (s0.distance % 7f) > 3.5f) continue;
                    float w = k == 2 ? 0.08f : 0.1f;
                    float lift = k == 2 ? 0.045f : 0.012f;
                    float y0 = s0.height + RoadLift + lift + 0.012f, y1 = s1.height + RoadLift + lift + 0.012f;
                    int b = lv.Count;
                    Vector2 c0 = s0.pos + n0 * offs[k], c1 = s1.pos + n1 * offs[k];
                    lv.Add(new Vector3(c0.x - n0.x * w, y0, c0.y - n0.y * w));
                    lv.Add(new Vector3(c0.x + n0.x * w, y0, c0.y + n0.y * w));
                    lv.Add(new Vector3(c1.x - n1.x * w, y1, c1.y - n1.y * w));
                    lv.Add(new Vector3(c1.x + n1.x * w, y1, c1.y + n1.y * w));
                    lt.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            if (lv.Count == 0) return;
            var lm = new Mesh { name = def.name + "_Lines_" + part, indexFormat = IndexFormat.UInt32 };
            lm.SetVertices(lv);
            lm.SetTriangles(lt, 0);
            FixWinding(lm);
            lm.RecalculateNormals();
            var lgo = new GameObject(def.name + "_Lines_" + part);
            lgo.AddComponent<MeshFilter>().sharedMesh = lm;
            var lr = lgo.AddComponent<MeshRenderer>();
            lr.sharedMaterial = lineMat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lgo.transform.SetParent(go.transform, true);
        }

        /// <summary>Make every triangle face up (+Y) regardless of how the strip was wound.</summary>
        static void FixWinding(Mesh m)
        {
            var v = m.vertices;
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 nrm = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (nrm.y < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            m.triangles = t;
        }

        void BuildWater()
        {
            const float y = -1.15f;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < riverPts.Count; i++)
            {
                Vector2 a = riverPts[Mathf.Max(0, i - 1)], b = riverPts[Mathf.Min(riverPts.Count - 1, i + 1)];
                Vector2 d = (b - a).normalized;
                Vector2 nrm = new Vector2(d.y, -d.x);
                Vector2 l = riverPts[i] - nrm * 13f, r = riverPts[i] + nrm * 13f;
                v.Add(new Vector3(l.x, y, l.y));
                v.Add(new Vector3(r.x, y, r.y));
                if (i > 0) { int k = (i - 1) * 2; t.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            MakeWater("Water_River", v, t);
            v = new List<Vector3> { new Vector3(MapData.LakeCenter.x, y, MapData.LakeCenter.y) };
            t = new List<int>();
            const int seg = 64;
            for (int i = 0; i <= seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2f;
                v.Add(new Vector3(MapData.LakeCenter.x + Mathf.Cos(ang) * MapData.LakeRadii.x * 1.22f, y,
                                  MapData.LakeCenter.y + Mathf.Sin(ang) * MapData.LakeRadii.y * 1.22f));
                if (i > 0) t.AddRange(new[] { 0, i, i + 1 });
            }
            MakeWater("Water_Lake", v, t);
        }

        void MakeWater(string name, List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            FixWinding(m);
            var uvs = new List<Vector2>();
            foreach (var p in v) uvs.Add(new Vector2(p.x * 0.05f, p.z * 0.05f));
            m.SetUVs(0, uvs);
            m.RecalculateNormals();
            m.RecalculateTangents();
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Water;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ instancing helpers
        GameObject Prefab(string res)
        {
            GameObject p;
            if (!prefabs.TryGetValue(res, out p))
            {
                p = Resources.Load<GameObject>("Env/" + res);
                if (p == null) Debug.LogWarning("[BlossomRally] missing environment asset Env/" + res);
                prefabs[res] = p;
            }
            return p;
        }

        GameObject Spawn(string res, Vector3 pos, float yaw, Vector3 scale, Transform parent, int layer = 0, bool shadows = true)
        {
            var prefab = Prefab(res);
            if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), parent);
            go.transform.localScale = scale;
            go.name = res;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name.Contains("_Petal_")) { Destroy(tr.gameObject); continue; }
                tr.gameObject.layer = layer;
            }
            foreach (var an in go.GetComponentsInChildren<Animator>(true)) Destroy(an);
            foreach (var an in go.GetComponentsInChildren<Animation>(true)) Destroy(an);
            MaterialLibrary.Remap(go);
            if (!shadows)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            // lanterns and flags keep moving: move their pivots out of the static batch
            var movers = new List<Transform>();
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                if (tr.name.EndsWith("_Pivot") || tr.name.Contains("_Flag_")) movers.Add(tr);
            foreach (var tr in movers)
            {
                if (tr == null) continue;
                tr.SetParent(dynamicRoot, true);
                var sw = tr.gameObject.AddComponent<LanternSwing>();
                sw.amplitude = tr.name.Contains("_Flag_") ? 10f : 6f;
            }
            return go;
        }

        void AddBox(GameObject go, Vector3 localCenter, Vector3 size)
        {
            var c = new GameObject("Collider");
            c.transform.SetParent(go.transform, false);
            c.transform.localPosition = localCenter;
            var bc = c.AddComponent<BoxCollider>();
            bc.size = size;
        }

        void AddBoundsCollider(GameObject go, float shrink = 0.9f)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var c = new GameObject("Collider");
            c.transform.SetParent(go.transform, true);
            c.transform.position = b.center;
            c.transform.rotation = Quaternion.identity;
            var bc = c.AddComponent<BoxCollider>();
            Vector3 ls = go.transform.lossyScale;
            bc.size = new Vector3(b.size.x / ls.x, b.size.y / ls.y, b.size.z / ls.z) * shrink;
        }

        // ------------------------------------------------------------------ landmarks
        void BuildLandmarks()
        {
            // village houses along Lantern Street
            foreach (var hs in houseSpots)
            {
                string res = hs.w > 0.5f ? "ENV_HouseShop" : "ENV_HouseTwoStorey";
                var go = Spawn(res, new Vector3(hs.x, HeightAt(hs.x, hs.y), hs.y), hs.z, Vector3.one, staticRoot);
                if (go != null) AddBoundsCollider(go, 0.85f);
            }
            // garage + start
            Vector2 g = MapData.Garage;
            float gh = HeightAt(g.x, g.y);
            var garage = Spawn("ENV_Garage", new Vector3(g.x, gh, g.y), -90f, Vector3.one, staticRoot);
            if (garage != null) AddBoundsCollider(garage, 0.85f);
            Vector3 spawn = new Vector3(g.x + 14f, 0f, g.y);
            spawn.y = HeightAt(spawn.x, spawn.z) + 0.6f;
            SpawnPosition = spawn;
            SpawnRotation = Quaternion.Euler(0f, 90f, 0f);
            // paifang gate over lantern street (front faces south, towards the village)
            Vector2 gt = MapData.Gate;
            var gate = Spawn("ENV_PaifangGate", new Vector3(gt.x, HeightAt(gt.x, gt.y), gt.y), 0f, Vector3.one * 1.15f, staticRoot);
            if (gate != null)
                foreach (float px in new[] { -4.2f, 4.2f, -7.0f, 7.0f })
                    AddBox(gate, new Vector3(px, 3f, 0f), new Vector3(0.9f, 6f, 0.9f));
            // lantern posts along lantern street
            var ls = roadSamples[4];
            for (int i = 4; i < ls.Count - 2; i += 7)
            {
                var s = ls[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 p = s.pos + nrm * side * (s.halfWidth + 1.3f);
                    // the post's lantern arm is its local +X; yaw it so the arm reaches over the street
                    Vector2 arm = -nrm * side;
                    float yaw = Mathf.Atan2(-arm.y, arm.x) * Mathf.Rad2Deg;
                    var lp = Spawn("ENV_LanternPost", new Vector3(p.x, HeightAt(p.x, p.y), p.y), yaw, Vector3.one, staticRoot);
                    if (lp != null) AddBox(lp, new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 2.4f, 0.45f));
                }
            }
            // lookout spots: lantern pairs
            foreach (var lk in MapData.Lookouts)
                for (int k = 0; k < 2; k++)
                {
                    Vector2 p = lk + new Vector2(k == 0 ? -3f : 3f, 0f);
                    var lp = Spawn("ENV_LanternPost", new Vector3(p.x, HeightAt(p.x, p.y), p.y), k * 180f, Vector3.one, staticRoot);
                    if (lp != null) AddBox(lp, new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 2.4f, 0.45f));
                }
            // mountain pagoda
            Vector2 pt = MapData.PagodaTop;
            var pagoda = Spawn("ENV_Pagoda", new Vector3(pt.x, HeightAt(pt.x, pt.y) - 0.3f, pt.y), 180f, Vector3.one * 1.4f, staticRoot);
            if (pagoda != null) AddBoundsCollider(pagoda, 0.6f);
            // lake pavilion (walkway towards the south shore)
            Vector2 lc = MapData.LakeCenter;
            var pav = Spawn("ENV_LakePavilion", new Vector3(lc.x, -0.85f, lc.y - (MapData.LakeRadii.y - 16f)), 0f, Vector3.one * 1.3f, staticRoot);
            if (pav != null) AddBox(pav, new Vector3(0f, 1.8f, 0f), new Vector3(7f, 3.6f, 7f));
            // stone bridges
            foreach (var b in bridges)
            {
                var br = Spawn("ENV_StoneBridge", new Vector3(b.x, b.w + RoadLift - 0.27f, b.y), b.z, new Vector3(1.25f, 1f, 30f / 18f), staticRoot);
                if (br == null) continue;
                foreach (var mf in br.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    if (mf.name.Contains("_Road")) mf.gameObject.AddComponent<SurfaceTag>().surface = Surface.Asphalt;
                }
            }
        }

        IEnumerator SpawnVegetation(List<Plant> plan)
        {
            int count = 0;
            foreach (var pl in plan)
            {
                float y = HeightAt(pl.pos.x, pl.pos.z);
                Vector3 pos = new Vector3(pl.pos.x, y - 0.05f, pl.pos.z);
                GameObject go;
                switch (pl.kind)
                {
                    case 3:
                        go = Spawn(pl.res, pos, pl.yaw, Vector3.one * pl.scale, staticRoot, LayerTrees);
                        if (go != null) { var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 1.1f; cc.height = 6f; cc.center = new Vector3(0, 3f, 0); }
                        break;
                    case 4:
                        go = Spawn(pl.res, new Vector3(pos.x, BaseHeight(pos.x, pos.z) - 0.15f, pos.z), pl.yaw, new Vector3(1f, 0.34f, 1f), staticRoot, LayerTrees);
                        if (go != null) AddBox(go, new Vector3(0f, 0.9f, 5.2f), new Vector3(16f, 1.8f, 10.4f));
                        break;
                    case 5:
                        go = Spawn(pl.res, pos, pl.yaw, Vector3.one * pl.scale, grassRoot, LayerGrass, false);
                        break;
                    default:
                        go = Spawn(pl.res, pos, pl.yaw, Vector3.one * pl.scale, staticRoot, LayerTrees);
                        if (go != null)
                        {
                            var cc = go.AddComponent<CapsuleCollider>();
                            cc.radius = pl.kind == 0 ? 0.55f : 0.38f;
                            cc.height = 5f;
                            cc.center = new Vector3(0, 2.5f, 0);
                        }
                        break;
                }
                if (++count % 150 == 0)
                {
                    Progress = 0.62f + 0.32f * count / plan.Count;
                    yield return null;
                }
            }
        }

        void BuildBoundary()
        {
            float L = 640f;
            for (int i = 0; i < 4; i++)
            {
                var w = new GameObject("Boundary_" + i);
                var bc = w.AddComponent<BoxCollider>();
                bool alongX = i < 2;
                float s = (i % 2 == 0) ? 1f : -1f;
                w.transform.position = alongX ? new Vector3(0f, 30f, s * L) : new Vector3(s * L, 30f, 0f);
                bc.size = alongX ? new Vector3(2f * L, 140f, 4f) : new Vector3(4f, 140f, 2f * L);
            }
        }

        /// <summary>Nearest road point and heading for respawning.</summary>
        public bool RespawnPoint(Vector3 from, Vector3 forward, out Vector3 pos, out Quaternion rot)
        {
            RoadSample s;
            float d = NearestRoad(new Vector2(from.x, from.z), out s, 600f);
            pos = SpawnPosition; rot = SpawnRotation;
            if (d == float.MaxValue) return false;
            if (s.bridge)
            {
                // step along the road until we are off the bridge
                var list = roadSamples[s.road];
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].bridge) continue;
                    float dd = (list[i].pos - s.pos).sqrMagnitude;
                    if (dd < bd) { bd = dd; best = i; }
                }
                s = list[best];
            }
            Vector3 t = new Vector3(s.tangent.x, 0f, s.tangent.y);
            if (Vector3.Dot(t, forward) < 0f) t = -t;
            pos = new Vector3(s.pos.x, s.height + 1.0f, s.pos.y);
            rot = Quaternion.LookRotation(t, Vector3.up);
            return true;
        }
    }

    /// <summary>Marks a collider with its driving surface (grip, smoke colour).</summary>
    public class SurfaceTag : MonoBehaviour
    {
        public Surface surface = Surface.Asphalt;
    }

    /// <summary>Gentle seamless swing for lanterns and flags (they live outside the static batch).</summary>
    public class LanternSwing : MonoBehaviour
    {
        public float amplitude = 6f;
        Quaternion rest;
        float phase, speed;

        void Start()
        {
            rest = transform.localRotation;
            phase = Random.value * 10f;
            speed = Random.Range(0.8f, 1.3f);
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            float gust = 1f + Atmosphere.Gust;
            transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 1.3f) * amplitude * gust,
                                                             Mathf.Sin(t * 0.7f + 1f) * amplitude * 0.4f,
                                                             Mathf.Sin(t * 1.1f + 2f) * amplitude * 0.7f * gust);
        }
    }
}
