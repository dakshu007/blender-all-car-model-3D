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
        public const float WaterY = -1.15f;

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
        class BridgeInfo { public int road; public List<int> idx; public float sHit, span, arch, total; }
        readonly List<BridgeInfo> bridges = new List<BridgeInfo>();
        /// <summary>Bridge centres for other systems: x, deck height, z, road yaw (deg).</summary>
        public readonly List<Vector4> BridgeMarks = new List<Vector4>();
        readonly List<Vector4> pads = new List<Vector4>();               // x, z, radius, height
        readonly List<Vector3> blossomSpots = new List<Vector3>();       // x, z, radius
        readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        readonly System.Random rng = new System.Random(20261108);

        Transform staticRoot, dynamicRoot, grassRoot;
        float[] terrH;
        int terrN;
        public int GrassBlades { get; private set; }
        readonly List<KeyValuePair<string, GameObject>> treeObjects = new List<KeyValuePair<string, GameObject>>();

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

            Status = "Growing the meadows"; Progress = 0.40f;
            yield return BuildGrass();

            Status = "Paving roads"; Progress = 0.45f;
            BuildRoadMeshes();
            BuildWater();
            yield return null;

            Status = "Raising the village"; Progress = 0.55f;
            BuildLandmarks();
            yield return null;

            Status = "Planting cherry blossoms"; Progress = 0.62f;
            yield return SpawnVegetation(plan);

            Status = "Painting distant trees"; Progress = 0.93f;
            yield return TreeImpostors.Build(treeObjects);

            Status = "Finishing touches"; Progress = 0.96f;
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
                // ease every bend to at least 1.8 x the half width, so the inner edge of a hairpin never folds over
                for (int ease = 0; ease < 6; ease++)
                    pts = Resample(EaseTightBends(pts, def.closed, def.width * 0.5f * 1.8f), def.closed, 2f);
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
            // junctions: where a side road ends on another road, it takes that road's height (2 cm below, so the
            // through road stays on top) and blends back over 45 m - no step for the car's body to hit
            for (int r = 0; r < roadSamples.Count; r++)
            {
                if (roads[r].closed) continue;
                var list = roadSamples[r];
                int n = list.Count;
                for (int end = 0; end < 2; end++)
                {
                    int ei = end == 0 ? 0 : n - 1;
                    Vector2 ep = list[ei].pos;
                    float best = float.MaxValue, hOther = 0f;
                    for (int o = 0; o < roadSamples.Count; o++)
                    {
                        if (o == r) continue;
                        foreach (var so in roadSamples[o])
                        {
                            float dd = (so.pos - ep).sqrMagnitude;
                            if (dd < best) { best = dd; hOther = so.height; }
                        }
                    }
                    if (best > 15f * 15f) continue;
                    float delta = (hOther - 0.02f) - list[ei].height;
                    float endDist = list[ei].distance;
                    for (int j = 0; j < n; j++)
                    {
                        float along = Mathf.Abs(list[j].distance - endDist);
                        if (along > 45f) continue;
                        var sj = list[j];
                        sj.height += delta * (1f - Spline.SmoothStep(0f, 45f, along));
                        list[j] = sj;
                    }
                }
            }

            // bridges where any road crosses the river: the road itself rises into a gentle stone arch
            // (part of the road mesh, so there are no seams, steps or gaps), max ~10% grade on the ramps
            for (int r = 0; r < roadSamples.Count; r++)
            {
                var list = roadSamples[r];
                bool closed = roads[r].closed;
                int n = list.Count;
                float total = list[n - 1].distance + (closed ? Vector2.Distance(list[n - 1].pos, list[0].pos) : 0f);
                int segs = closed ? n : n - 1;
                for (int i = 0; i < segs; i++)
                {
                    int i1 = (i + 1) % n;
                    for (int k = 0; k < riverPts.Count - 1; k++)
                    {
                        Vector2 hit;
                        if (!Spline.SegmentIntersect(list[i].pos, list[i1].pos, riverPts[k], riverPts[k + 1], out hit)) continue;
                        Vector2 rt = (list[i1].pos - list[i].pos).normalized;
                        Vector2 rv = (riverPts[k + 1] - riverPts[k]).normalized;
                        float sinA = Mathf.Max(0.4f, Mathf.Abs(rt.x * rv.y - rt.y * rv.x));
                        float sHit = list[i].distance + Vector2.Distance(list[i].pos, hit);
                        LiftBridge(list, closed, total, sHit, 17f / sinA, r);
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

        float AlongRoad(List<RoadSample> list, int j, float sHit, bool closed, float total)
        {
            float d = list[j].distance - sHit;
            if (closed)
            {
                if (d > total * 0.5f) d -= total;
                else if (d < -total * 0.5f) d += total;
            }
            return d;
        }

        float HeightAlong(List<RoadSample> list, float sHit, float offset, bool closed, float total)
        {
            int best = 0; float bd = float.MaxValue;
            for (int j = 0; j < list.Count; j++)
            {
                float e = Mathf.Abs(AlongRoad(list, j, sHit, closed, total) - offset);
                if (e < bd) { bd = e; best = j; }
            }
            return list[best].height;
        }

        void LiftBridge(List<RoadSample> list, bool closed, float total, float sHit, float riverHalf, int road)
        {
            float span = riverHalf + 2.5f;                 // deck length either side of the river centre
            float R = span + 6f, lift = 1.1f, hA = 0f, hB = 0f;
            for (int it = 0; it < 6; it++)
            {
                hA = HeightAlong(list, sHit, -R, closed, total);
                hB = HeightAlong(list, sHit, R, closed, total);
                lift = Mathf.Max(0.6f, WaterY + 2.4f - 0.5f * (hA + hB));     // deck clears the water by ~2.4 m
                float lin = Mathf.Abs(hB - hA) / (2f * R);
                R = Mathf.Max(span + 6f, lift * Mathf.PI * 0.5f / Mathf.Max(0.03f, 0.095f - lin));
            }
            var info = new BridgeInfo { road = road, idx = new List<int>(), sHit = sHit, span = span, arch = riverHalf, total = total };
            var newH = new float[list.Count];
            for (int j = 0; j < list.Count; j++)
            {
                newH[j] = list[j].height;
                float d = AlongRoad(list, j, sHit, closed, total);
                if (Mathf.Abs(d) > R) continue;
                float lin = Mathf.Lerp(hA, hB, (d + R) / (2f * R));
                float w = Spline.SmoothStep(R, R * 0.6f, Mathf.Abs(d));
                float bump = 0.5f * (Mathf.Cos(Mathf.PI * d / R) + 1f);
                newH[j] = Mathf.Lerp(list[j].height, lin, w) + lift * bump;
                if (Mathf.Abs(d) <= span) info.idx.Add(j);
            }
            for (int j = 0; j < list.Count; j++)
            {
                var sj = list[j];
                sj.height = newH[j];
                if (info.idx.Contains(j)) sj.bridge = true;
                list[j] = sj;
            }
            info.idx.Sort((a, b) => AlongRoad(list, a, sHit, closed, total).CompareTo(AlongRoad(list, b, sHit, closed, total)));
            if (info.idx.Count > 1)
            {
                bridges.Add(info);
                var mid = list[info.idx[info.idx.Count / 2]];
                BridgeMarks.Add(new Vector4(mid.pos.x, mid.height, mid.pos.y, Mathf.Atan2(mid.tangent.x, mid.tangent.y) * Mathf.Rad2Deg));
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
            Vector2 tt = Garage.Spot;
            pads.Add(new Vector4(tt.x, tt.y, 6f, BaseHeight(g.x, g.y)));
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
                if (Vector2.Distance(p, Garage.Spot) < 9f + margin * 0.3f) return false;
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
                for (int i = 0; i < list.Count; i += 11)
                {
                    var s = list[i];
                    if (s.bridge) continue;
                    Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 p = s.pos + nrm * side * (s.halfWidth + Rand(3f, 9f)) + s.tangent * Rand(-2f, 2f);
                        if (RiverDistance(p) < 14f || MapData.LakeK(p) < 1.25f) continue;
                        plants.Add(new Plant { res = "ENV_GrassClump", pos = new Vector3(p.x, 0, p.y), yaw = Rand(0, 360), scale = Rand(0.9f, 1.6f), kind = 5 });
                    }
                }
            for (int i = 0; i < 450; i++)
            {
                Vector2 p = new Vector2(Rand(-460f, 460f), Rand(-460f, 440f));
                if (!clearOfFeatures(p, 2f) || MapData.InTeaTerraces(p)) continue;
                plants.Add(new Plant { res = "ENV_GrassClump", pos = new Vector3(p.x, 0, p.y), yaw = Rand(0, 360), scale = Rand(0.9f, 1.7f), kind = 5 });
            }
            // share what is taken with later modules (props, life, crowds)
            PlacementRegistry.Clear();
            foreach (var t in taken) PlacementRegistry.Reserve(new Vector2(t.x, t.z), t.y);
            foreach (var hs in houseSpots) PlacementRegistry.Reserve(new Vector2(hs.x, hs.y), 9f);
            foreach (var pd in pads) PlacementRegistry.Reserve(new Vector2(pd.x, pd.y), pd.z);
            PlacementRegistry.Reserve(MapData.Gate, 9f);
            PlacementRegistry.Reserve(MapData.PagodaTop, 14f);
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
                if (j % 24 == 0) { Progress = 0.15f + 0.23f * j / n; yield return null; }
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
            terrN = n;
            terrH = new float[n * n];
            for (int i = 0; i < verts.Length; i++) terrH[i] = verts[i].y;
            Progress = 0.38f;
        }

        /// <summary>Height of the actual terrain mesh (same triangulation), so grass sits exactly on it.</summary>
        public float TerrainHeight(float x, float z)
        {
            if (terrH == null) return HeightAt(x, z);
            float fx = (x + TerrainHalf) / TerrainCell, fz = (z + TerrainHalf) / TerrainCell;
            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, terrN - 2), j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, terrN - 2);
            float u = Mathf.Clamp01(fx - i), w = Mathf.Clamp01(fz - j);
            float ha = terrH[j * terrN + i], hb = terrH[j * terrN + i + 1], hc = terrH[(j + 1) * terrN + i], hd = terrH[(j + 1) * terrN + i + 1];
            if (u + w <= 1f) return ha + (hb - ha) * u + (hc - ha) * w;
            return hd + (hc - hd) * (1f - u) + (hb - hd) * (1f - w);
        }

        // ------------------------------------------------------------------ grass
        static readonly Color[] GrassTips =
        {
            new Color(0.56f, 0.78f, 0.30f), new Color(0.68f, 0.82f, 0.36f), new Color(0.42f, 0.66f, 0.24f), new Color(0.62f, 0.74f, 0.32f)
        };

        IEnumerator BuildGrass()
        {
            var root = new GameObject("World_GrassBlades").transform;
            var mat = MaterialLibrary.Grass;
            var grng = new System.Random(4242);
            System.Func<float> R01 = () => (float)grng.NextDouble();
            const float chunk = 40f, lo = -470f;
            int chunks = Mathf.CeilToInt(-2f * lo / chunk);
            var verts = new List<Vector3>(); var cols = new List<Color32>(); var uvs = new List<Vector4>();
            var nrms = new List<Vector3>(); var tris = new List<int>();
            var spots = new List<Vector3>();
            int done = 0, total = 0;
            for (int cz = 0; cz < chunks; cz++)
                for (int cx = 0; cx < chunks; cx++)
                {
                    float x0 = lo + cx * chunk, z0 = lo + cz * chunk;
                    Vector2 cc = new Vector2(x0 + chunk * 0.5f, z0 + chunk * 0.5f);
                    RoadSample crs;
                    bool roadNear = NearestRoad(cc, out crs, 64f) < 64f;
                    spots.Clear();
                    foreach (var bs in blossomSpots)
                        if (Mathf.Abs(bs.x - cc.x) < chunk * 0.5f + bs.z * 1.3f && Mathf.Abs(bs.y - cc.y) < chunk * 0.5f + bs.z * 1.3f) spots.Add(bs);
                    verts.Clear(); cols.Clear(); uvs.Clear(); nrms.Clear(); tris.Clear();
                    for (float z = z0; z < z0 + chunk; z += 1f)
                        for (float x = x0; x < x0 + chunk; x += 1f)
                        {
                            float px = x + R01(), pz = z + R01();
                            Vector2 p = new Vector2(px, pz);
                            // soft meadow patches across the valley
                            float meadow = Mathf.Clamp01((Mathf.Sin(px * 0.021f + Mathf.Sin(pz * 0.017f) * 2.1f) * Mathf.Sin(pz * 0.024f + 0.7f) - 0.3f) * 1.8f);
                            float prob = meadow * 0.22f;
                            float spotK = 0f;
                            foreach (var bs in spots)
                            {
                                float db = Vector2.Distance(p, new Vector2(bs.x, bs.y));
                                if (db < bs.z * 1.3f) spotK = Mathf.Max(spotK, 1f - db / (bs.z * 1.3f));
                            }
                            prob = Mathf.Max(prob, spotK * 0.6f);
                            float edge = 99f;
                            if (roadNear)
                            {
                                RoadSample rs;
                                float d = NearestRoad(p, out rs, 26f);
                                if (d < 26f)
                                {
                                    edge = d - rs.halfWidth;
                                    if (edge < 0.35f) continue;                 // never on the road
                                    if (rs.bridge && edge < 5f) continue;       // bridge parapets
                                    float band = edge < 9f ? 0.62f : Mathf.Lerp(0.62f, 0.12f, (edge - 9f) / 13f);
                                    prob = Mathf.Max(prob, band);
                                }
                            }
                            if (prob < 0.02f || R01() > prob) continue;
                            if (MapData.InVillage(p) || MapData.InTeaTerraces(p)) continue;
                            if (MapData.LakeK(p) < 1.2f || RiverDistance(p) < 11.5f) continue;
                            if (MapData.InBambooForest(p) && R01() < 0.6f) continue;
                            bool onPad = false;
                            foreach (var pad in pads)
                                if ((new Vector2(pad.x, pad.y) - p).sqrMagnitude < pad.z * pad.z) { onPad = true; break; }
                            if (onPad) continue;
                            AddTuft(p, edge, spotK, meadow, R01, verts, cols, uvs, nrms, tris);
                        }
                    if (verts.Count > 0)
                    {
                        var m = new Mesh { name = "Grass_" + cx + "_" + cz };
                        if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                        m.SetVertices(verts);
                        m.SetNormals(nrms);
                        m.SetColors(cols);
                        m.SetUVs(0, uvs);
                        m.SetTriangles(tris, 0);
                        m.RecalculateBounds();
                        var bnd = m.bounds;
                        bnd.Expand(1.5f);              // room for wind / car displacement
                        m.bounds = bnd;
                        var go = new GameObject(m.name);
                        go.transform.SetParent(root, false);
                        go.layer = LayerGrass;
                        go.AddComponent<MeshFilter>().sharedMesh = m;
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = mat;
                        mr.shadowCastingMode = ShadowCastingMode.Off;
                        mr.receiveShadows = true;
                        total += verts.Count / 3;
                    }
                    if (++done % 8 == 0)
                    {
                        Progress = 0.38f + 0.07f * done / (chunks * chunks);
                        yield return null;
                    }
                }
            GrassBlades = total;
            Debug.Log("[BlossomRally] grass blades: " + total);
        }

        void AddTuft(Vector2 p, float edge, float spotK, float meadow, System.Func<float> R01,
                     List<Vector3> verts, List<Color32> cols, List<Vector4> uvs, List<Vector3> nrms, List<int> tris)
        {
            int blades = 2 + Mathf.FloorToInt(R01() * 1.99f);
            float tall = edge < 2.5f ? 0.65f : (edge > 9f ? Mathf.Lerp(1f, 1.35f, meadow) : 1f);   // verge is trimmed short
            // patch colour so the field isn't uniform
            float patch = 0.5f + 0.5f * Mathf.Sin(p.x * 0.11f + Mathf.Sin(p.y * 0.07f) * 1.7f) * Mathf.Sin(p.y * 0.09f + 0.4f);
            for (int b = 0; b < blades; b++)
            {
                Vector2 o = new Vector2(R01() - 0.5f, R01() - 0.5f) * 0.36f;
                Vector2 rp = p + o;
                float y = TerrainHeight(rp.x, rp.y) - 0.02f;
                float yaw = R01() * Mathf.PI * 2f;
                Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 lean = new Vector3(o.x, 0f, o.y).normalized * 0.5f + new Vector3(R01() - 0.5f, 0f, R01() - 0.5f) * 0.6f;
                float H = (0.30f + R01() * 0.34f) * tall;
                float W = 0.045f + R01() * 0.045f;
                Vector3 c = new Vector3(rp.x, y, rp.y);
                Vector3 tip = c + lean * H * 0.45f + Vector3.up * H;
                Color rootC = Color.Lerp(new Color(0.17f, 0.30f, 0.10f), new Color(0.26f, 0.40f, 0.14f), R01());
                Color tipC = GrassTips[Mathf.FloorToInt(R01() * GrassTips.Length) % GrassTips.Length];
                tipC = Color.Lerp(tipC, new Color(0.76f, 0.84f, 0.42f), patch * 0.35f);
                float roll = R01();
                if (roll < 0.05f) tipC = new Color(0.84f, 0.79f, 0.52f);                                    // dry blade
                else if (spotK > 0.1f && roll < 0.05f + 0.07f * spotK) tipC = new Color(0.99f, 0.77f, 0.86f); // fallen petals / pink flowers
                else if (roll > 0.985f) tipC = new Color(0.98f, 0.97f, 0.92f);                              // white daisy
                else if (roll > 0.972f) tipC = new Color(0.99f, 0.86f, 0.34f);                              // buttercup
                Vector3 nrm = (Vector3.up + lean * 0.35f).normalized;
                Vector4 uv = new Vector4(c.x, c.y, c.z, R01());
                int i0 = verts.Count;
                verts.Add(c - side * W * 0.5f);
                verts.Add(tip);
                verts.Add(c + side * W * 0.5f);
                Color32 r32 = rootC; r32.a = 0;
                Color32 t32 = tipC; t32.a = 255;
                cols.Add(r32); cols.Add(t32); cols.Add(r32);
                nrms.Add(nrm); nrms.Add(nrm); nrms.Add(nrm);
                uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
            }
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
                var mat = MaterialLibrary.Road("Road_" + def.surface, Resources.Load<Texture2D>("Env/" + tex), def.surface == Surface.Asphalt ? 0.22f : 0.08f);
                if (def.surface == Surface.Gravel || def.surface == Surface.Dirt) mat.SetFloat("_PuddleAmount", 1f);
                else if (def.surface == Surface.Paving) mat.SetFloat("_PuddleAmount", 0.6f);
                else mat.SetFloat("_PuddleAmount", 0.8f);
                float tile = def.surface == Surface.Paving ? 4f : 7f;
                int count = list.Count + (def.closed ? 1 : 0);
                // split into runs of <= 240 samples (bridges are part of the road surface)
                var run = new List<RoadSample>();
                int part = 0;
                for (int i = 0; i < count; i++)
                {
                    var s = list[i % list.Count];
                    if (run.Count >= 240)
                    {
                        MakeRoadPiece(def, run, mat, lineMat, tile, part++);
                        var last = run[run.Count - 1];
                        run.Clear();
                        run.Add(last);
                    }
                    run.Add(s);
                }
                if (run.Count > 1) MakeRoadPiece(def, run, mat, lineMat, tile, part++);
            }
        }

        /// <summary>Relaxes points whose local turn radius is below rMin (open roads keep their end points).</summary>
        static List<Vector2> EaseTightBends(List<Vector2> pts, bool closed, float rMin)
        {
            var p = new List<Vector2>(pts);
            int n = p.Count;
            if (n < 5) return p;
            for (int pass = 0; pass < 400; pass++)
            {
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (!closed && (i < 2 || i > n - 3)) continue;
                    Vector2 a = p[(i - 1 + n) % n], b = p[i], c = p[(i + 1) % n];
                    float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
                    float area2 = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
                    if (area2 < 1e-5f) continue;
                    float r = ab * bc * ca / (2f * area2);
                    if (r >= rMin) continue;
                    p[i] = Vector2.Lerp(b, (a + c) * 0.5f, 0.5f);
                    changed = true;
                }
                if (!changed) break;
            }
            return p;
        }

        /// <summary>Re-spaces a polyline to an even step.</summary>
        static List<Vector2> Resample(List<Vector2> pts, bool closed, float step)
        {
            var src = new List<Vector2>(pts);
            if (closed) src.Add(pts[0]);
            var outp = new List<Vector2> { src[0] };
            float carry = 0f;
            for (int i = 1; i < src.Count; i++)
            {
                Vector2 a = src[i - 1], b = src[i];
                float seg = Vector2.Distance(a, b);
                float t = step - carry;
                while (t <= seg)
                {
                    outp.Add(Vector2.Lerp(a, b, t / seg));
                    t += step;
                }
                carry = seg - (t - step);
            }
            if (closed)
            {
                if (outp.Count > 1 && Vector2.Distance(outp[outp.Count - 1], outp[0]) < step * 0.5f) outp.RemoveAt(outp.Count - 1);
            }
            else if (Vector2.Distance(outp[outp.Count - 1], src[src.Count - 1]) > step * 0.25f) outp.Add(src[src.Count - 1]);
            return outp;
        }

        /// <summary>
        /// Points offset sideways from the centreline (positive = right). Where a bend is still tighter than the offset,
        /// the edge would run backwards and fold; such points are held so the edge stays clean.
        /// </summary>
        static Vector2[] EdgePoints(List<RoadSample> run, float offsetFraction, float extra)
        {
            int n = run.Count;
            var e = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var s = run[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                Vector2 p = s.pos + nrm * (s.halfWidth * offsetFraction + extra);
                if (i > 0 && Vector2.Dot(p - e[i - 1], s.tangent) < 0.02f)
                {
                    // fold: keep the edge on the previous point, nudged forwards along the road
                    p = e[i - 1] + s.tangent * 0.02f;
                }
                e[i] = p;
            }
            return e;
        }

        void MakeRoadPiece(RoadDef def, List<RoadSample> run, Material mat, Material lineMat, float tile, int part)
        {
            int n = run.Count;
            var leftE = EdgePoints(run, -1f, 0f);
            var rightE = EdgePoints(run, 1f, 0f);
            var v = new Vector3[n * 3];
            var uv = new Vector2[n * 3];
            var tri = new List<int>();
            float startDist = run[0].distance;
            for (int i = 0; i < n; i++)
            {
                var s = run[i];
                Vector2 nrm = new Vector2(s.tangent.y, -s.tangent.x);
                float y = s.height + RoadLift;
                Vector2 l = leftE[i], rr = rightE[i];
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
            var lineL = EdgePoints(run, -1f, 0.45f);
            var lineR = EdgePoints(run, 1f, -0.45f);
            for (int i = 0; i < n - 1; i++)
            {
                var s0 = run[i]; var s1 = run[i + 1];
                Vector2 n0 = new Vector2(s0.tangent.y, -s0.tangent.x), n1 = new Vector2(s1.tangent.y, -s1.tangent.x);
                for (int k = 0; k < 3; k++)
                {
                    if (k == 2 && (s0.distance % 7f) > 3.5f) continue;
                    float w = k == 2 ? 0.08f : 0.1f;
                    float lift = k == 2 ? 0.045f : 0.012f;
                    float y0 = s0.height + RoadLift + lift + 0.012f, y1 = s1.height + RoadLift + lift + 0.012f;
                    int b = lv.Count;
                    Vector2 c0 = k == 0 ? lineL[i] : k == 1 ? lineR[i] : s0.pos;
                    Vector2 c1 = k == 0 ? lineL[i + 1] : k == 1 ? lineR[i + 1] : s1.pos;
                    if ((c1 - c0).sqrMagnitude < 0.0004f) continue;          // held (folded) stretch: no line
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
            const float y = WaterY;
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
            // Place a clean holder (Unity axes) and keep the model's own imported root transform:
            // Unity collapses the FBX root into the prefab root, and that root carries the
            // Blender->Unity axis rotation. Overwriting it stands every model on its end.
            var go = new GameObject(res);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            var model = Instantiate(prefab, go.transform, false);
            model.name = res + "_Model";
            AxisFix.Calibrate();
            AxisFix.Apply(model.transform);
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
            // stone arch bridges (procedural, built around the lifted road deck)
            foreach (var b in bridges) BuildBridge(b);
        }

        // ------------------------------------------------------------------ bridges
        class MeshAcc
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Color32> c = new List<Color32>();
            public readonly List<int> t = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 outward, Color32 ca, Color32 cb)
            {
                // a,d = "bottom" edge (colour ca), b,c = "top" edge (colour cb)
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
                c.Add(ca); c.Add(cb); c.Add(cb); c.Add(ca);
                Vector3 nrm = Vector3.Cross(b - a, cc - a);
                if (Vector3.Dot(nrm, outward) >= 0f) t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            public void Box(Vector3 centre, Vector3 fwd, Vector3 size, Color32 col, Color32 top)
            {
                Vector3 f = fwd.normalized * size.z * 0.5f;
                Vector3 r = Vector3.Cross(Vector3.up, fwd).normalized * size.x * 0.5f;
                Vector3 u = Vector3.up * size.y;
                Vector3 b0 = centre - f - r, b1 = centre - f + r, b2 = centre + f + r, b3 = centre + f - r;
                Quad(b0, b0 + u, b1 + u, b1, -f, col, top);
                Quad(b1, b1 + u, b2 + u, b2, r, col, top);
                Quad(b2, b2 + u, b3 + u, b3, f, col, top);
                Quad(b3, b3 + u, b0 + u, b0, -r, col, top);
                Quad(b0 + u, b1 + u, b2 + u, b3 + u, Vector3.up, top, top);
            }
        }

        static Color32 Stone(float shade, float hash)
        {
            float k = shade * (0.94f + 0.08f * hash);
            return new Color32((byte)(Mathf.Clamp01(0.82f * k) * 255), (byte)(Mathf.Clamp01(0.78f * k) * 255), (byte)(Mathf.Clamp01(0.72f * k) * 255), 0);
        }

        static float Hash(int a, int b)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        void BuildBridge(BridgeInfo b)
        {
            var list = roadSamples[b.road];
            bool closed = roads[b.road].closed;
            int n = list.Count;
            // extend two samples into the abutments at each end
            var ext = new List<int>(b.idx);
            for (int e = 1; e <= 2; e++)
            {
                int lo = b.idx[0] - e, hi = b.idx[b.idx.Count - 1] + e;
                if (closed) { lo = (lo + n) % n; hi %= n; }
                if (lo >= 0 && lo < n) ext.Insert(0, lo);
                if (hi >= 0 && hi < n) ext.Add(hi);
            }
            var acc = new MeshAcc();
            var go = new GameObject("StoneBridge_" + bridges.IndexOf(b));
            go.transform.SetParent(staticRoot, false);
            const float wallIn = 0.05f, wallOut = 0.6f, wallH = 0.72f, cope = 0.12f, over = 0.06f;
            Color32 moss = new Color32(128, 138, 112, 0);
            Color32 copeCol = Stone(1.12f, 0.5f);
            Color32 soffit = Stone(0.62f, 0.3f);
            float springY = WaterY - 0.45f;
            float postAcc = 0f;

            System.Func<int, Vector3> P3 = j => new Vector3(list[j].pos.x, list[j].height + RoadLift, list[j].pos.y);
            System.Func<int, Vector3> N3 = j => new Vector3(list[j].tangent.y, 0f, -list[j].tangent.x);
            System.Func<int, float, float> Bottom = (j, side) =>
            {
                float d = AlongRoad(list, j, b.sHit, closed, b.total);
                float y = list[j].height + RoadLift;
                float crown = y - 0.75f;
                if (Mathf.Abs(d) < b.arch)
                {
                    float q = d / b.arch;
                    return springY + (crown - springY) * Mathf.Pow(1f - q * q, 0.42f);
                }
                Vector3 e = P3(j) + N3(j) * side * (list[j].halfWidth + wallOut);
                return Mathf.Min(HeightAt(e.x, e.z) - 0.6f, crown);
            };

            for (int q = 0; q < ext.Count - 1; q++)
            {
                int j0 = ext[q], j1 = ext[q + 1];
                Vector3 p0 = P3(j0), p1 = P3(j1), n0 = N3(j0), n1 = N3(j1);
                float hw0 = list[j0].halfWidth, hw1 = list[j1].halfWidth;
                Vector3 fwd = (p1 - p0);
                float segLen = new Vector2(fwd.x, fwd.z).magnitude;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 out0 = n0 * side, out1 = n1 * side;
                    Vector3 i0 = p0 + out0 * (hw0 + wallIn), i1 = p1 + out1 * (hw1 + wallIn);
                    Vector3 o0 = p0 + out0 * (hw0 + wallOut), o1 = p1 + out1 * (hw1 + wallOut);
                    float t0 = p0.y + wallH, t1 = p1.y + wallH;
                    float b0 = Bottom(j0, side), b1 = Bottom(j1, side);
                    Color32 wallCol = Stone(1f, Hash(q, side));
                    Color32 low = (b0 < 0.2f || b1 < 0.2f) ? moss : Stone(0.9f, Hash(q, side + 5));
                    // outer spandrel wall, inner parapet face
                    acc.Quad(new Vector3(o0.x, b0, o0.z), new Vector3(o0.x, t0, o0.z), new Vector3(o1.x, t1, o1.z), new Vector3(o1.x, b1, o1.z), out0, low, wallCol);
                    acc.Quad(new Vector3(i0.x, p0.y - 0.05f, i0.z), new Vector3(i0.x, t0, i0.z), new Vector3(i1.x, t1, i1.z), new Vector3(i1.x, p1.y - 0.05f, i1.z), -out0, Stone(0.85f, 0.5f), wallCol);
                    // coping slab on top of the parapet
                    Vector3 ci0 = p0 + out0 * (hw0 + wallIn - over), ci1 = p1 + out1 * (hw1 + wallIn - over);
                    Vector3 co0 = p0 + out0 * (hw0 + wallOut + over), co1 = p1 + out1 * (hw1 + wallOut + over);
                    acc.Quad(new Vector3(ci0.x, t0 + cope, ci0.z), new Vector3(co0.x, t0 + cope, co0.z), new Vector3(co1.x, t1 + cope, co1.z), new Vector3(ci1.x, t1 + cope, ci1.z), Vector3.up, copeCol, copeCol);
                    acc.Quad(new Vector3(co0.x, t0, co0.z), new Vector3(co0.x, t0 + cope, co0.z), new Vector3(co1.x, t1 + cope, co1.z), new Vector3(co1.x, t1, co1.z), out0, copeCol, copeCol);
                    acc.Quad(new Vector3(ci0.x, t0, ci0.z), new Vector3(ci0.x, t0 + cope, ci0.z), new Vector3(ci1.x, t1 + cope, ci1.z), new Vector3(ci1.x, t1, ci1.z), -out0, copeCol, copeCol);
                    acc.Quad(new Vector3(ci0.x, t0, ci0.z), new Vector3(co0.x, t0, co0.z), new Vector3(co1.x, t1, co1.z), new Vector3(ci1.x, t1, ci1.z), Vector3.down, soffit, soffit);
                    // end caps of the parapet at both ends of the bridge
                    if (q == 0 || q == ext.Count - 2)
                    {
                        bool first = q == 0;
                        Vector3 ii = first ? i0 : i1, oo = first ? o0 : o1;
                        float yb = first ? p0.y - 0.05f : p1.y - 0.05f, yt = (first ? t0 : t1) + cope;
                        Vector3 capOut = first ? -fwd : fwd;
                        acc.Quad(new Vector3(ii.x, yb, ii.z), new Vector3(ii.x, yt, ii.z), new Vector3(oo.x, yt, oo.z), new Vector3(oo.x, yb, oo.z), capOut, Stone(0.95f, 0.4f), copeCol);
                        // big end post with a lion-head style cap
                        Vector3 post = (ii + oo) * 0.5f;
                        post.y = yt;
                        acc.Box(post, fwd, new Vector3(0.78f, 0.55f, 0.78f), copeCol, Stone(1.18f, 0.2f));
                        acc.Box(post + Vector3.up * 0.55f, fwd, new Vector3(0.46f, 0.32f, 0.46f), Stone(1.1f, 0.8f), Stone(1.2f, 0.6f));
                    }
                    // invisible wall so cars bounce off the parapet instead of climbing it
                    var wall = new GameObject("BridgeWall");
                    wall.transform.SetParent(go.transform, false);
                    Vector3 mid = (i0 + i1 + o0 + o1) * 0.25f;
                    mid.y = (p0.y + p1.y) * 0.5f + 0.7f;
                    wall.transform.position = mid;
                    wall.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                    wall.AddComponent<BoxCollider>().size = new Vector3(wallOut - wallIn, 1.6f, segLen + 0.08f);
                }
                // arch soffit / underside of the deck
                {
                    Vector3 l0 = p0 - n0 * (hw0 + wallOut), r0 = p0 + n0 * (hw0 + wallOut);
                    Vector3 l1 = p1 - n1 * (hw1 + wallOut), r1 = p1 + n1 * (hw1 + wallOut);
                    float bl0 = Bottom(j0, -1), br0 = Bottom(j0, 1), bl1 = Bottom(j1, -1), br1 = Bottom(j1, 1);
                    acc.Quad(new Vector3(l0.x, bl0, l0.z), new Vector3(r0.x, br0, r0.z), new Vector3(r1.x, br1, r1.z), new Vector3(l1.x, bl1, l1.z), Vector3.down, soffit, soffit);
                }
                // small balusters along the coping
                postAcc += segLen;
                if (postAcc >= 2.4f)
                {
                    postAcc = 0f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 c = p1 + n1 * side * (hw1 + (wallIn + wallOut) * 0.5f);
                        c.y = p1.y + wallH + cope;
                        acc.Box(c, fwd, new Vector3(0.34f, 0.32f, 0.34f), copeCol, Stone(1.2f, 0.7f));
                    }
                }
            }

            var mesh = new Mesh { name = go.name };
            mesh.SetVertices(acc.v);
            mesh.SetColors(acc.c);
            mesh.SetTriangles(acc.t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Terrain;

            // red lanterns at the four corners of the bridge
            int[] ends = { ext[0], ext[ext.Count - 1] };
            foreach (int j in ends)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = P3(j) + N3(j) * side * (list[j].halfWidth + 1.5f);
                    Vector3 arm = -N3(j) * side;
                    float yaw = Mathf.Atan2(-arm.z, arm.x) * Mathf.Rad2Deg;
                    var lp = Spawn("ENV_LanternPost", new Vector3(p.x, HeightAt(p.x, p.z), p.z), yaw, Vector3.one, staticRoot);
                    if (lp != null) AddBox(lp, new Vector3(0f, 1.2f, 0f), new Vector3(0.45f, 2.4f, 0.45f));
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
                        if (go != null) { var cc = go.AddComponent<CapsuleCollider>(); cc.radius = 1.1f; cc.height = 6f; cc.center = new Vector3(0, 3f, 0); treeObjects.Add(new KeyValuePair<string, GameObject>(pl.res, go)); }
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
                            treeObjects.Add(new KeyValuePair<string, GameObject>(pl.res, go));
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
