// WorldField.cs - the world's height field, roads and ground (pure logic, no engine objects).
//
//   BaseHeight  = blend of the six region heights (WorldLayout.Weights) + border ridges + outer rim,
//                 then each region's Shape() pass. Roads are sampled on BaseHeight.
//   HeightAt    = BaseHeight + valley river / lake carve + flat pads + road cut-and-fill.
//   GroundColor = blend of the region ground colours (+ worn verges beside roads).
//
// WorldBuilder turns this into meshes; the offline terrain harness evaluates the very same code.
using System.Collections.Generic;
using UnityEngine;

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

    public sealed class BridgeInfo { public int road; public List<int> idx; public float sHit, span, arch, total; }

    public sealed class WorldField
    {
        /// <summary>The field of the world being played (set by WorldBuilder).</summary>
        public static WorldField Active { get; set; }

        public readonly List<RoadDef> roads;
        public readonly List<List<RoadSample>> roadSamples = new List<List<RoadSample>>();
        public readonly List<RoadSample> allSamples = new List<RoadSample>();
        public readonly List<BridgeInfo> bridges = new List<BridgeInfo>();
        /// <summary>Bridge centres for other systems: x, deck height, z, road yaw (deg).</summary>
        public readonly List<Vector4> BridgeMarks = new List<Vector4>();
        /// <summary>Valley blossom tree spots: x, z, radius (petal carpets in the ground colour).</summary>
        public readonly List<Vector3> blossomSpots = new List<Vector3>();
        public readonly List<Vector2> riverPts = new List<Vector2>();
        public readonly Region[] regions = new Region[6];
        public bool RoadsReady { get; private set; }

        readonly Dictionary<long, List<int>> roadGrid = new Dictionary<long, List<int>>();
        readonly Dictionary<long, List<int>> riverGrid = new Dictionary<long, List<int>>();
        readonly float[] wbuf = new float[6];
        readonly float[] hbuf = new float[6];
        readonly float[] cbuf = new float[6];

        // flat pads (circles) and rectangles, in a 64 m grid
        struct Pad { public Vector2 c; public float r, h; }
        struct FlatRect { public Vector2 c, half; public float cos, sin, h, blend; }
        readonly List<Pad> pads = new List<Pad>();
        readonly List<FlatRect> rects = new List<FlatRect>();
        readonly Dictionary<long, List<int>> padGrid = new Dictionary<long, List<int>>();
        readonly Dictionary<long, List<int>> rectGrid = new Dictionary<long, List<int>>();
        const float PadCell = 64f;

        public static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public WorldField(IList<Region> list)
        {
            foreach (var r in list)
            {
                regions[(int)r.Id] = r;
                r.Field = this;
            }
            roads = MapData.Roads();
            foreach (var c in WorldLayout.Connectors)
                roads.Add(new RoadDef(c.name, c.surface, c.width, false, c.points) { cutWidth = 30f, maxGrade = 0.115f, region = c.a, connector = true });
            foreach (var id in WorldLayout.All)
            {
                var r = regions[(int)id];
                if (r == null || id == RegionId.Valley) continue;
                int before = roads.Count;
                r.AddRoads(roads);
                for (int i = before; i < roads.Count; i++) roads[i].region = id;
            }
            Active = this;
        }

        public Region RegionOf(RegionId id) { return regions[(int)id]; }

        public int RoadIndex(string name)
        {
            for (int i = 0; i < roads.Count; i++) if (roads[i].name == name) return i;
            return -1;
        }

        // ------------------------------------------------------------------ river (valley)
        public void TraceRiver()
        {
            riverPts.Clear();
            riverPts.AddRange(Spline.Sample(MapData.River, false, 6f));
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

        // ------------------------------------------------------------------ heights
        /// <summary>Region blend weights at p (index = (int)RegionId). The array is shared: copy if you keep it.</summary>
        public float[] Weights(Vector2 p)
        {
            WorldLayout.Weights(p, wbuf);
            return wbuf;
        }

        /// <summary>Region with the largest weight at p.</summary>
        public Region Dominant(Vector2 p)
        {
            return regions[(int)WorldLayout.At(p)] ?? regions[0];
        }

        public float BaseHeight(float x, float z)
        {
            Vector2 p = new Vector2(x, z);
            var w = hbuf;
            WorldLayout.Weights(p, w);
            float h = 0f, ws = 0f;
            for (int i = 0; i < 6; i++)
            {
                if (w[i] < 1e-4f || regions[i] == null) continue;
                h += w[i] * regions[i].Height(x, z);
                ws += w[i];
            }
            if (ws > 0f) h /= ws;
            h += WorldLayout.RidgeHeight(p) + WorldLayout.RimHeight(p);
            for (int i = 0; i < 6; i++)
            {
                if (w[i] < 1e-4f || regions[i] == null) continue;
                h = regions[i].Shape(x, z, h, w[i]);
            }
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
            h = ApplyPads(p, h);
            RoadSample rs;
            float dist = NearestRoad(p, out rs, 44f);
            if (dist < 44f && !rs.bridge)
            {
                float inner = rs.halfWidth + 1.0f;
                float cut = roads[rs.road].cutWidth;
                if (dist < inner + cut)
                {
                    float target = rs.height - 0.08f;
                    h = Mathf.Lerp(target, h, Spline.SmoothStep(inner, inner + cut, dist));
                }
            }
            return h;
        }

        // ------------------------------------------------------------------ pads
        /// <summary>Flat circular pad of radius r at height h (blends back to the terrain by 1.8 r).</summary>
        public void AddPad(Vector2 c, float r, float h)
        {
            int idx = pads.Count;
            pads.Add(new Pad { c = c, r = r, h = h });
            Insert(padGrid, idx, c, r * 1.8f);
        }

        public void AddPad(Vector4 xzrh) { AddPad(new Vector2(xzrh.x, xzrh.y), xzrh.z, xzrh.w); }

        /// <summary>Flat rectangle (centre, half size, yaw in degrees around +y) at height h, blending out over blend metres.</summary>
        public void AddFlatRect(Vector2 c, Vector2 half, float yawDeg, float h, float blend)
        {
            float a = yawDeg * Mathf.Deg2Rad;
            int idx = rects.Count;
            rects.Add(new FlatRect { c = c, half = half, cos = Mathf.Cos(a), sin = Mathf.Sin(a), h = h, blend = Mathf.Max(0.5f, blend) });
            Insert(rectGrid, idx, c, half.magnitude + blend);
        }

        static void Insert(Dictionary<long, List<int>> grid, int idx, Vector2 c, float reach)
        {
            int x0 = Mathf.FloorToInt((c.x - reach) / PadCell), x1 = Mathf.FloorToInt((c.x + reach) / PadCell);
            int z0 = Mathf.FloorToInt((c.y - reach) / PadCell), z1 = Mathf.FloorToInt((c.y + reach) / PadCell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<int> l;
                    if (!grid.TryGetValue(Key(x, z), out l)) { l = new List<int>(); grid[Key(x, z)] = l; }
                    l.Add(idx);
                }
        }

        public bool OnPad(Vector2 p)
        {
            long key = Key(Mathf.FloorToInt(p.x / PadCell), Mathf.FloorToInt(p.y / PadCell));
            List<int> l;
            if (padGrid.TryGetValue(key, out l))
                foreach (int i in l) if ((pads[i].c - p).sqrMagnitude < pads[i].r * pads[i].r) return true;
            if (rectGrid.TryGetValue(key, out l))
                foreach (int i in l) if (RectOutside(rects[i], p) <= 0f) return true;
            return false;
        }

        static float RectOutside(FlatRect r, Vector2 p)
        {
            Vector2 d = p - r.c;
            float lx = d.x * r.cos - d.y * r.sin, lz = d.x * r.sin + d.y * r.cos;
            float ox = Mathf.Max(0f, Mathf.Abs(lx) - r.half.x), oz = Mathf.Max(0f, Mathf.Abs(lz) - r.half.y);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        float ApplyPads(Vector2 p, float h)
        {
            long key = Key(Mathf.FloorToInt(p.x / PadCell), Mathf.FloorToInt(p.y / PadCell));
            List<int> l;
            if (padGrid.TryGetValue(key, out l))
                foreach (int i in l)
                {
                    var pad = pads[i];
                    float d = Vector2.Distance(p, pad.c);
                    if (d < pad.r * 1.8f) h = Mathf.Lerp(pad.h, h, Spline.SmoothStep(pad.r, pad.r * 1.8f, d));
                }
            if (rectGrid.TryGetValue(key, out l))
                foreach (int i in l)
                {
                    var r = rects[i];
                    float o = RectOutside(r, p);
                    if (o < r.blend) h = Mathf.Lerp(r.h, h, Spline.SmoothStep(0f, r.blend, o));
                }
            return h;
        }

        // ------------------------------------------------------------------ roads
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

        /// <summary>Distance from p to the nearest road edge (99 when no road within 40 m).</summary>
        public float EdgeDistance(Vector2 p)
        {
            RoadSample rs;
            float d = NearestRoad(p, out rs, 40f);
            return d >= 40f ? 99f : d - rs.halfWidth;
        }

        public void BuildRoadSamples()
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
                    list.Add(new RoadSample
                    {
                        pos = pts[i], tangent = (next - prev).normalized, halfWidth = def.width * 0.5f, road = r,
                        height = def.fixedHeight ? def.heightOffset : BaseHeight(pts[i].x, pts[i].y) + def.heightOffset, distance = dist
                    });
                }
                int n = list.Count;
                var hs = new float[n];
                int smoothPasses = def.fixedHeight ? 0 : 3;
                for (int pass = 0; pass < smoothPasses; pass++)
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
                // bridge spans: the deck runs straight from abutment to abutment (it never dips into the gorge)
                if (def.bridgeSpans != null)
                    foreach (var sp in def.bridgeSpans)
                    {
                        int ia = -1, ib = -1;
                        for (int i = 0; i < n; i++)
                        {
                            if (ia < 0 && list[i].distance >= sp.x) ia = i;
                            if (list[i].distance <= sp.y) ib = i;
                        }
                        if (ia < 0 || ib <= ia) continue;
                        float ha = list[ia].height, hb = list[ib].height, da = list[ia].distance, db = list[ib].distance;
                        for (int i = ia; i <= ib; i++)
                        {
                            var si = list[i];
                            si.height = Mathf.Lerp(ha, hb, (si.distance - da) / Mathf.Max(0.01f, db - da));
                            list[i] = si;
                        }
                    }
                // limit the gradient so every road stays drivable (terrain is cut / filled to match)
                float maxGrade = def.maxGrade;
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
            // junctions: where a road ends on another road it takes that road's height (2 cm below, so the
            // through road stays on top) and blends back over 45 m - no step for the car's body to hit.
            // Roads are processed in order, so a later road follows an earlier one it meets end to end.
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
            for (int r = 0; r < roadSamples.Count; r++)
            {
                if (roads[r].fixedHeight) continue;
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
            // region bridges (harbour, gorge, frozen lake crossings): flag the spans so the terrain is not cut to them
            for (int r = 0; r < roadSamples.Count; r++)
            {
                var spans = roads[r].bridgeSpans;
                if (spans == null || spans.Count == 0) continue;
                var list = roadSamples[r];
                for (int j = 0; j < list.Count; j++)
                    foreach (var sp in spans)
                        if (list[j].distance >= sp.x && list[j].distance <= sp.y) { var sj = list[j]; sj.bridge = true; list[j] = sj; break; }
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
            RoadsReady = true;
        }

        public static float AlongRoad(List<RoadSample> list, int j, float sHit, bool closed, float total)
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

        public const float WaterY = -1.15f;

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

        /// <summary>Relaxes points whose local turn radius is below rMin (open roads keep their end points).</summary>
        public static List<Vector2> EaseTightBends(List<Vector2> pts, bool closed, float rMin)
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
        public static List<Vector2> Resample(List<Vector2> pts, bool closed, float step)
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

        // ------------------------------------------------------------------ ground
        public void PlanRegions()
        {
            foreach (var r in regions) if (r != null) r.Plan();
        }

        /// <summary>Ground colour at (x,z) with final height h and terrain normal n. Alpha = 0 (terrain shader convention).</summary>
        public Color GroundColor(float x, float z, float h, Vector3 n)
        {
            Vector2 p = new Vector2(x, z);
            var w = cbuf;
            WorldLayout.Weights(p, w);
            Color c = new Color(0f, 0f, 0f, 0f);
            float ws = 0f;
            float edge = -1f;
            RoadSample rs = default(RoadSample);
            for (int i = 0; i < 6; i++)
            {
                if (w[i] < 1e-4f || regions[i] == null) continue;
                var reg = regions[i];
                Color rc = reg.GroundColor(x, z, h, n);
                if (reg.AutoVerge)
                {
                    if (edge < 0f)
                    {
                        float d = NearestRoad(p, out rs, 20f);
                        edge = d < 20f ? d : 99f;
                    }
                    if (edge < rs.halfWidth + 4f)
                        rc = Color.Lerp(rc, reg.VergeColor(x, z), (1f - Spline.SmoothStep(rs.halfWidth, rs.halfWidth + 4f, edge)) * 0.6f);
                }
                c += rc * w[i];
                ws += w[i];
            }
            if (ws > 0f) c /= ws;
            c.a = 0f;
            return c;
        }

        /// <summary>Driving surface of bare terrain at a world position.</summary>
        public Surface GroundSurface(float x, float z)
        {
            var r = Dominant(new Vector2(x, z));
            return r != null ? r.GroundSurface(x, z) : Surface.Grass;
        }
    }
}
