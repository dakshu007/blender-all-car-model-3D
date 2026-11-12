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
        /// <summary>Cross slope (m per m, + = the right side is higher; right = (tangent.y, -tangent.x)).</summary>
        public float bank;

        public Vector2 Right { get { return new Vector2(tangent.y, -tangent.x); } }
        /// <summary>Top of the road mesh at a lateral offset (+ = right) from the centreline.</summary>
        public float SurfaceAt(float lateral)
        {
            float a = Mathf.Abs(lateral);
            return height + WorldField.RoadLift + bank * lateral + WorldField.RoadCrown * Mathf.Max(0f, 1f - a / Mathf.Max(0.5f, halfWidth));
        }
    }

    /// <summary>A road (slave) ending on another road (master): a T junction, or an end-to-end joint.</summary>
    public sealed class RoadJunction
    {
        public int slave, master;
        /// <summary>0 = the slave's first sample touches the master, 1 = its last.</summary>
        public int slaveEnd;
        /// <summary>The master ends here too (masterEnd 0 / 1), e.g. a connector running into a region road.</summary>
        public bool endToEnd;
        public int masterEnd = -1;
        public Vector2 point;
        /// <summary>First slave sample outside the master's footprint.</summary>
        public int mouth = -1;
    }

    public sealed class BridgeInfo { public int road; public List<int> idx; public float sHit, span, arch, total; }

    public sealed class WorldField
    {
        /// <summary>The field of the world being played (set by WorldBuilder).</summary>
        public static WorldField Active { get; set; }

        /// <summary>The road mesh sits this far above the sample heights (edges); the crown adds RoadCrown at the centre.</summary>
        public const float RoadLift = 0.05f, RoadCrown = 0.04f;

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

        public float BaseHeight(float x, float z) { return PassCorridor(new Vector2(x, z), NaturalHeight(x, z)); }

        /// <summary>Blended region terrain + ridges + rim + region shaping, before the pass corridors.</summary>
        public float NaturalHeight(float x, float z)
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

        // ------------------------------------------------------------------ connector corridors
        // Each cross-border road runs through a smooth valley floor that ramps evenly between the two regions'
        // natural heights at its ends, so passes read as saddles instead of trenches cut into a ridge.
        float[] connLen, connH0, connH1;

        void PrepareCorridors()
        {
            var cs = WorldLayout.Connectors;
            connLen = new float[cs.Length]; connH0 = new float[cs.Length]; connH1 = new float[cs.Length];
            for (int c = 0; c < cs.Length; c++)
            {
                var pts = cs[c].points;
                float L = 0f;
                for (int i = 0; i < pts.Length - 1; i++) L += Vector2.Distance(pts[i], pts[i + 1]);
                connLen[c] = L;
                // ends: the full natural ground (regions often shape their gateways in Shape())
                Vector2 p0 = pts[0], p1 = pts[pts.Length - 1];
                connH0[c] = NaturalHeight(p0.x, p0.y);
                connH1[c] = NaturalHeight(p1.x, p1.y);
            }
        }

        float PassCorridor(Vector2 p, float h)
        {
            if (connLen == null) PrepareCorridors();
            var cs = WorldLayout.Connectors;
            for (int c = 0; c < cs.Length; c++)
            {
                var pts = cs[c].points;
                float best = 1e9f, along = 0f, acc = 0f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    float t;
                    float seg = Vector2.Distance(pts[i], pts[i + 1]);
                    float d = Spline.DistToSegment(p, pts[i], pts[i + 1], out t);
                    if (d < best) { best = d; along = acc + t * seg; }
                    acc += seg;
                }
                if (best >= 80f) continue;
                float u = Mathf.Clamp01(along / Mathf.Max(1f, connLen[c]));
                // an even ramp (the gentlest possible grade); near the gateways each region's own ground takes over
                float floorH = Mathf.Lerp(connH0[c], connH1[c], u);
                // the corridor only flattens in the middle of the link: near the gateways each region rules
                float mid = WorldNoise.Smooth(0f, 0.18f, u) * (1f - WorldNoise.Smooth(0.82f, 1f, u));
                float wgt = (1f - WorldNoise.Smooth(26f, 80f, best)) * mid;
                if (wgt > 0f) h = Mathf.Lerp(h, floorH, wgt);
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
            if (!RoadsReady) return h;
            RoadSample a, b;
            float da, db;
            int found = NearestTwo(p, SearchReach, out a, out da, out b, out db);
            if (found == 0 || a.bridge) return h;
            da = SquareEnds(a, da, p);
            if (found == 2) db = SquareEnds(b, db, p);
            float ea;
            float ha = RoadProfile(a, da, p, h, out ea);
            if (found == 1 || b.bridge || db >= ProfileReach(b)) return ha;
            // two stretches of road (a junction, two hairpin legs, parallel roads): blend their profiles by edge
            // distance, so the ground between them is a continuous ramp instead of a step at the half-way line
            float eb;
            float hb = RoadProfile(b, db, p, h, out eb);
            float A = Mathf.Max(0f, ea), B = Mathf.Max(0f, eb);
            if (A + B < 1e-3f) return Mathf.Min(ha, hb);
            float w = A * A / (A * A + B * B);
            return ha + (hb - ha) * w;
        }

        /// <summary>Past the open end of a road its shoulder wraps round square (not as a round cap reaching hw out).</summary>
        float SquareEnds(RoadSample s, float d, Vector2 p)
        {
            if (roads[s.road].closed) return d;
            Vector2 outward;
            if (s.distance < 0.01f) outward = -s.tangent;
            else if (s.distance > roadTotal[s.road] - 0.01f) outward = s.tangent;
            else return d;
            float beyond = Vector2.Dot(p - s.pos, outward);
            if (beyond <= 0f) return d;
            float lat = Mathf.Abs(Vector2.Dot(p - s.pos, s.Right));
            return s.halfWidth + Mathf.Max(lat - s.halfWidth, beyond);
        }

        float ProfileReach(RoadSample s)
        {
            var def = roads[s.road];
            return def.fixedHeight ? s.halfWidth + 1f + def.cutWidth : s.halfWidth + Shoulder + ConeReach;
        }

        /// <summary>
        /// Ground height beside one road. Under the road and on a short shoulder the ground sits just below the surface
        /// (following its bank); beyond the shoulder the cut / fill face rises (or falls) at most ConeSlope per metre
        /// (about 37 deg, gentler on the wide connector corridors) with a rounded toe, and meets the natural ground with
        /// a soft crease. Only far out (beyond ~12 m) may it steepen, so very deep cuts still close within ConeReach.
        /// </summary>
        float RoadProfile(RoadSample s, float d, Vector2 p, float h, out float edge)
        {
            var def = roads[s.road];
            float hw = s.halfWidth;
            edge = d - hw;
            if (def.fixedHeight)
            {
                // flat city streets / causeways: the classic blend (the regions build kerbs and quays to it)
                float inF = hw + 1f;
                if (d >= inF + def.cutWidth) return h;
                return Mathf.Lerp(s.height - 0.08f, h, Spline.SmoothStep(inF, inF + def.cutWidth, d));
            }
            float run = edge - Shoulder;
            if (run >= ConeReach) return h;
            float lat = Mathf.Clamp(Vector2.Dot(p - s.pos, s.Right), -hw, hw);
            float drop = 0.12f + 0.05f * Mathf.Clamp(edge, 0f, Shoulder);
            float T = s.height + s.bank * lat - drop;
            if (run <= 0f) return T;
            float m = Mathf.Min(ConeSlope, 9f / Mathf.Max(1f, def.cutWidth));
            float extra = run - 16f;
            float cone = m * run * run / (run + 1.5f) + (extra > 0f ? 0.02f * extra * extra : 0f);
            float dv = h - T;
            float ad = Mathf.Abs(dv);
            float soft = Mathf.Min(1f, 0.5f * cone);
            float lim = SMin(ad, cone, soft);
            float hc = T + (dv < 0f ? -lim : lim);
            // the face lets go at its reach; anything still steeper there is natural cliff
            float rel = Spline.SmoothStep(ConeReach - 6f, ConeReach, run);
            return hc + (h - hc) * rel;
        }

        static float SMin(float a, float b, float k)
        {
            if (k < 1e-4f) return a < b ? a : b;
            float d = a - b;
            return 0.5f * (a + b - Mathf.Sqrt(d * d + k * k));
        }

        static float SMax(float a, float b, float k)
        {
            if (k < 1e-4f) return a > b ? a : b;
            float d = a - b;
            return 0.5f * (a + b + Mathf.Sqrt(d * d + k * k));
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
        /// <summary>Ground beside roads: a flat shoulder this wide (m), then cut / fill faces no steeper than ConeSlope
        /// (rise per metre, ~37 deg) out to ConeReach metres beyond the shoulder.</summary>
        public const float Shoulder = 3f, ConeSlope = 0.75f, ConeReach = 32f, SearchReach = 44f;
        /// <summary>Steepest bank on fast asphalt bends (m per m).</summary>
        public const float BankMax = 0.045f;
        public static int FairIters = 20, FairIters2 = 14;

        /// <summary>Where a road (slave) ends on another road (master): T junctions and end-to-end joints.</summary>
        public readonly List<RoadJunction> junctions = new List<RoadJunction>();

        int[] roadStart, roadCount;
        float[] roadTotal;
        Vector2[] spos;
        const float RoadCell = 16f;
        int rgX0, rgZ0, rgW, rgH;
        int[] rgStart, rgItems;

        void BuildRoadGrid()
        {
            int n = allSamples.Count;
            spos = new Vector2[n];
            roadStart = new int[roads.Count];
            roadCount = new int[roads.Count];
            roadTotal = new float[roads.Count];
            for (int r = 0, k = 0; r < roads.Count; r++)
            {
                var l = roadSamples[r];
                roadStart[r] = k; roadCount[r] = l.Count; k += l.Count;
                roadTotal[r] = l.Count == 0 ? 0f : l[l.Count - 1].distance + (roads[r].closed ? Vector2.Distance(l[l.Count - 1].pos, l[0].pos) : 0f);
            }
            if (n == 0) { rgStart = null; return; }
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            var cx = new int[n]; var cz = new int[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = allSamples[i].pos;
                spos[i] = p;
                cx[i] = Mathf.FloorToInt(p.x / RoadCell); cz[i] = Mathf.FloorToInt(p.y / RoadCell);
                minX = Mathf.Min(minX, cx[i]); maxX = Mathf.Max(maxX, cx[i]);
                minZ = Mathf.Min(minZ, cz[i]); maxZ = Mathf.Max(maxZ, cz[i]);
            }
            rgX0 = minX; rgZ0 = minZ; rgW = maxX - minX + 1; rgH = maxZ - minZ + 1;
            rgStart = new int[rgW * rgH + 1];
            for (int i = 0; i < n; i++) rgStart[(cz[i] - rgZ0) * rgW + (cx[i] - rgX0) + 1]++;
            for (int c = 0; c < rgW * rgH; c++) rgStart[c + 1] += rgStart[c];
            var fill = (int[])rgStart.Clone();
            rgItems = new int[n];
            for (int i = 0; i < n; i++) rgItems[fill[(cz[i] - rgZ0) * rgW + (cx[i] - rgX0)]++] = i;
        }

        bool CellRange(Vector2 p, float maxDist, out int x0, out int x1, out int z0, out int z1)
        {
            int r = Mathf.CeilToInt(maxDist / RoadCell);
            int cx = Mathf.FloorToInt(p.x / RoadCell) - rgX0, cz = Mathf.FloorToInt(p.y / RoadCell) - rgZ0;
            x0 = Mathf.Max(0, cx - r); x1 = Mathf.Min(rgW - 1, cx + r);
            z0 = Mathf.Max(0, cz - r); z1 = Mathf.Min(rgH - 1, cz + r);
            return rgStart != null && x0 <= x1 && z0 <= z1;
        }

        int NearestIndex(Vector2 p, float maxDist, float limitSq, int onlyRoad = -1)
        {
            int x0, x1, z0, z1;
            if (!CellRange(p, maxDist, out x0, out x1, out z0, out z1)) return -1;
            int best = -1;
            float bestSq = limitSq;
            for (int z = z0; z <= z1; z++)
            {
                int row = z * rgW;
                for (int x = x0; x <= x1; x++)
                {
                    int c = row + x;
                    for (int k = rgStart[c], e = rgStart[c + 1]; k < e; k++)
                    {
                        int i = rgItems[k];
                        Vector2 q = spos[i];
                        float dx = q.x - p.x, dz = q.y - p.y, d = dx * dx + dz * dz;
                        if (d < bestSq && (onlyRoad < 0 || allSamples[i].road == onlyRoad)) { bestSq = d; best = i; }
                    }
                }
            }
            return best;
        }

        static RoadSample LerpSample(RoadSample a, RoadSample b, float t)
        {
            var s = a;
            float seg = Vector2.Distance(a.pos, b.pos);
            s.pos = Vector2.LerpUnclamped(a.pos, b.pos, t);
            s.height = a.height + (b.height - a.height) * t;
            s.bank = a.bank + (b.bank - a.bank) * t;
            s.halfWidth = a.halfWidth + (b.halfWidth - a.halfWidth) * t;
            s.distance = a.distance + seg * t;
            Vector2 tg = Vector2.LerpUnclamped(a.tangent, b.tangent, t);
            s.tangent = tg.sqrMagnitude > 1e-6f ? tg.normalized : a.tangent;
            s.bridge = t < 0.5f ? a.bridge : b.bridge;
            return s;
        }

        /// <summary>Projects p onto the road segments either side of sample idx: the exact centreline point.</summary>
        float Refine(int idx, Vector2 p, out RoadSample s)
        {
            s = allSamples[idx];
            int r = s.road, n = roadCount[r], li = idx - roadStart[r];
            bool closed = roads[r].closed;
            float best = Vector2.Distance(s.pos, p);
            if (n < 2) return best;
            for (int k = -1; k <= 0; k++)
            {
                int a = li + k, b = a + 1;
                if (closed) { a = (a + n) % n; b %= n; }
                else if (a < 0 || b >= n) continue;
                Vector2 A = spos[roadStart[r] + a], B = spos[roadStart[r] + b];
                float t;
                float d = Spline.DistToSegment(p, A, B, out t);
                if (d < best - 1e-5f)
                {
                    best = d;
                    s = LerpSample(allSamples[roadStart[r] + a], allSamples[roadStart[r] + b], t);
                }
            }
            return best;
        }

        /// <summary>Nearest point on any road centreline (interpolated between samples) and its distance.</summary>
        public float NearestRoad(Vector2 p, out RoadSample sample, float maxDist = 40f)
        {
            sample = default(RoadSample);
            int i = NearestIndex(p, maxDist, float.MaxValue);
            if (i < 0) return float.MaxValue;
            return Refine(i, p, out sample);
        }

        /// <summary>Nearest point on one road's centreline (float.MaxValue when that road is not within maxDist).</summary>
        public float NearestOnRoad(int road, Vector2 p, out RoadSample sample, float maxDist = 32f)
        {
            sample = default(RoadSample);
            int i = NearestIndex(p, maxDist, float.MaxValue, road);
            if (i < 0) return float.MaxValue;
            return Refine(i, p, out sample);
        }

        /// <summary>Distance from p to the nearest road edge (99 when no road within 40 m).</summary>
        public float EdgeDistance(Vector2 p)
        {
            RoadSample rs;
            float d = NearestRoad(p, out rs, 40f);
            return d >= 40f ? 99f : d - rs.halfWidth;
        }

        /// <summary>Top of the road mesh of one road at p (its nearest centreline point, lateral offset clamped to the road).</summary>
        public float RoadSurface(int road, Vector2 p)
        {
            RoadSample s;
            if (NearestOnRoad(road, p, out s) == float.MaxValue) return float.MinValue;
            return s.SurfaceAt(Mathf.Clamp(Vector2.Dot(p - s.pos, s.Right), -s.halfWidth, s.halfWidth));
        }

        bool SameStretch(int i, int j)
        {
            var a = allSamples[i]; var b = allSamples[j];
            if (a.road != b.road) return false;
            float d = Mathf.Abs(a.distance - b.distance);
            if (roads[a.road].closed) d = Mathf.Min(d, roadTotal[a.road] - d);
            return d < 10f;
        }

        bool LocalMin(int i, Vector2 p)
        {
            int r = allSamples[i].road, n = roadCount[r], li = i - roadStart[r];
            bool closed = roads[r].closed;
            float d = (spos[i] - p).sqrMagnitude;
            for (int k = -1; k <= 1; k += 2)
            {
                int j = li + k;
                if (closed) j = (j + n) % n; else if (j < 0 || j >= n) continue;
                if ((spos[roadStart[r] + j] - p).sqrMagnitude < d) return false;
            }
            return true;
        }

        /// <summary>The nearest road point and the nearest point on a different stretch (another road, or another leg
        /// of the same road). Returns how many were found (0..2).</summary>
        int NearestTwo(Vector2 p, float maxDist, out RoadSample a, out float da, out RoadSample b, out float db)
        {
            a = default(RoadSample); b = default(RoadSample); da = db = float.MaxValue;
            int x0, x1, z0, z1;
            if (!CellRange(p, maxDist, out x0, out x1, out z0, out z1)) return 0;
            float lim = maxDist * maxDist;
            int ia = -1;
            float best = lim;
            for (int z = z0; z <= z1; z++)
                for (int x = x0, c = z * rgW + x0; x <= x1; x++, c++)
                    for (int k = rgStart[c], e = rgStart[c + 1]; k < e; k++)
                    {
                        int i = rgItems[k];
                        float dx = spos[i].x - p.x, dz = spos[i].y - p.y, d = dx * dx + dz * dz;
                        if (d < best) { best = d; ia = i; }
                    }
            if (ia < 0) return 0;
            da = Refine(ia, p, out a);
            int ib = -1;
            best = lim;
            for (int z = z0; z <= z1; z++)
                for (int x = x0, c = z * rgW + x0; x <= x1; x++, c++)
                    for (int k = rgStart[c], e = rgStart[c + 1]; k < e; k++)
                    {
                        int i = rgItems[k];
                        float dx = spos[i].x - p.x, dz = spos[i].y - p.y, d = dx * dx + dz * dz;
                        if (d >= best || i == ia || SameStretch(i, ia) || !LocalMin(i, p)) continue;
                        best = d; ib = i;
                    }
            if (ib < 0) return 1;
            db = Refine(ib, p, out b);
            return 2;
        }

        public void BuildRoadSamples()
        {
            for (int r = 0; r < roads.Count; r++)
            {
                var def = roads[r];
                float rMin = def.width * 0.5f * 1.8f;
                var pts = Resample(Spline.Sample(def.points, def.closed, 1f), def.closed, 2f);
                // fair the line first (removes the curvature jumps Catmull-Rom leaves at control points), then ease
                // every bend to at least 1.8 x the half width so the inner edge of a hairpin never folds over, and
                // fair lightly again so the eased hairpins blend into their legs
                pts = Fair(pts, def.closed, FairIters);
                for (int ease = 0; ease < 6; ease++)
                    pts = Resample(EaseTightBends(pts, def.closed, rMin), def.closed, 2f);
                pts = Resample(Fair(pts, def.closed, FairIters2), def.closed, 2f);
                pts = Resample(EaseTightBends(pts, def.closed, rMin * 0.97f), def.closed, 2f);
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
                ClampGrade(list, def.closed, def.maxGrade);
                roadSamples.Add(list);
            }
            ComputeBanks();
            FindJunctions();
            MatchJunctions();
            RegradeFromMouths();

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
            FadeBankAtBridges();
            for (int r = 0; r < roadSamples.Count; r++)
                foreach (var s in roadSamples[r]) allSamples.Add(s);
            BuildRoadGrid();
            RoadsReady = true;
        }

        static void ClampGrade(List<RoadSample> list, bool closed, float maxGrade)
        {
            int n = list.Count;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 1; i < n + (closed ? 1 : 0); i++)
                {
                    int a = (i - 1) % n, b = i % n;
                    float ds = Vector2.Distance(list[a].pos, list[b].pos) * maxGrade;
                    var sb = list[b]; sb.height = Mathf.Clamp(sb.height, list[a].height - ds, list[a].height + ds); list[b] = sb;
                }
                for (int i = n - 2 + (closed ? 1 : 0); i >= 0; i--)
                {
                    int a = (i + 1) % n, b = i % n;
                    float ds = Vector2.Distance(list[a].pos, list[b].pos) * maxGrade;
                    var sb = list[b]; sb.height = Mathf.Clamp(sb.height, list[a].height - ds, list[a].height + ds); list[b] = sb;
                }
            }
        }

        /// <summary>After junction matching: keep each joining road within its grade limit, working outwards from its
        /// mouths (the mouth heights stay exactly on the through road).</summary>
        void RegradeFromMouths()
        {
            var lo = new Dictionary<int, int>(); var hi = new Dictionary<int, int>();
            foreach (var j in junctions)
            {
                if (j.mouth < 0) continue;
                if (j.slaveEnd == 0) lo[j.slave] = j.mouth; else hi[j.slave] = j.mouth;
            }
            for (int r = 0; r < roads.Count; r++)
            {
                int a, b;
                bool hasA = lo.TryGetValue(r, out a), hasB = hi.TryGetValue(r, out b);
                if (!hasA && !hasB) continue;
                var L = roadSamples[r];
                int n = L.Count;
                if (!hasA) a = 0;
                if (!hasB) b = n - 1;
                if (b - a < 2) continue;
                float g = roads[r].maxGrade * 1.05f;
                for (int pass = 0; pass < 2; pass++)
                {
                    if (hasA)
                        for (int i = a + 1; i <= b - (hasB ? 1 : 0); i++)
                        {
                            float ds = Vector2.Distance(L[i].pos, L[i - 1].pos) * g;
                            var si = L[i]; si.height = Mathf.Clamp(si.height, L[i - 1].height - ds, L[i - 1].height + ds); L[i] = si;
                        }
                    if (hasB)
                        for (int i = b - 1; i >= a + (hasA ? 1 : 0); i--)
                        {
                            float ds = Vector2.Distance(L[i].pos, L[i + 1].pos) * g;
                            var si = L[i]; si.height = Mathf.Clamp(si.height, L[i + 1].height - ds, L[i + 1].height + ds); L[i] = si;
                        }
                }
            }
        }

        /// <summary>Signed curvature (1/m, + = turning left) at every sample, box-smoothed over about 26 m.</summary>
        public static float[] Curvature(List<RoadSample> list, bool closed, int smooth = 6)
        {
            int n = list.Count;
            var k = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i == 0 || i == n - 1)) continue;
                Vector2 a = list[(i - 1 + n) % n].pos, b = list[i].pos, c = list[(i + 1) % n].pos;
                Vector2 d0 = b - a, d1 = c - b;
                float l0 = d0.magnitude, l1 = d1.magnitude;
                if (l0 < 1e-4f || l1 < 1e-4f) continue;
                float turn = Mathf.Atan2(d0.x * d1.y - d0.y * d1.x, Vector2.Dot(d0, d1));
                k[i] = turn / (0.5f * (l0 + l1));
            }
            return BoxSmooth(k, closed, smooth, 2);
        }

        static float[] BoxSmooth(float[] v, bool closed, int half, int passes)
        {
            int n = v.Length;
            var src = (float[])v.Clone();
            var dst = new float[n];
            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    float s = 0f; int c = 0;
                    for (int k = -half; k <= half; k++)
                    {
                        int j = i + k;
                        if (closed) j = (j % n + n) % n; else if (j < 0 || j >= n) continue;
                        s += src[j]; c++;
                    }
                    dst[i] = s / c;
                }
                var t = src; src = dst; dst = t;
            }
            return src;
        }

        /// <summary>Gentle banking on the valley's and the connectors' fast asphalt bends (hairpins stay flat).</summary>
        void ComputeBanks()
        {
            for (int r = 0; r < roads.Count; r++)
            {
                var def = roads[r];
                if (def.fixedHeight || def.surface != Surface.Asphalt || !(def.region == RegionId.Valley || def.connector)) continue;
                var list = roadSamples[r];
                int n = list.Count;
                if (n < 12) continue;
                var k = Curvature(list, def.closed);
                var bank = new float[n];
                float total = list[n - 1].distance;
                for (int i = 0; i < n; i++)
                {
                    float ak = Mathf.Abs(k[i]);
                    if (ak < 1e-5f) continue;
                    float R = 1f / ak;
                    float b = Mathf.Min(BankMax, 2.4f * ak) * Spline.SmoothStep(14f, 30f, R) * Mathf.Sign(k[i]);
                    if (!def.closed) b *= Spline.SmoothStep(0f, 25f, list[i].distance) * Spline.SmoothStep(0f, 25f, total - list[i].distance);
                    bank[i] = b;
                }
                bank = BoxSmooth(bank, def.closed, 5, 1);
                for (int i = 0; i < n; i++) { var s = list[i]; s.bank = bank[i]; list[i] = s; }
            }
        }

        void FadeBankAtBridges()
        {
            for (int r = 0; r < roads.Count; r++)
            {
                var list = roadSamples[r];
                int n = list.Count;
                bool any = false;
                foreach (var s in list) if (s.bridge) { any = true; break; }
                if (!any) continue;
                var near = new float[n];
                for (int i = 0; i < n; i++) near[i] = 1e9f;
                for (int i = 0; i < n; i++)
                    if (list[i].bridge)
                        for (int j = 0; j < n; j++) near[j] = Mathf.Min(near[j], Mathf.Abs(list[j].distance - list[i].distance));
                for (int j = 0; j < n; j++)
                {
                    float f = Spline.SmoothStep(4f, 24f, near[j]);
                    if (f >= 1f) continue;
                    var s = list[j]; s.bank *= f; list[j] = s;
                }
            }
        }

        /// <summary>Nearest point on road list (brute force, used while sampling before the grid exists).</summary>
        static float NearestOnList(List<RoadSample> L, Vector2 p, out RoadSample best, out int bi)
        {
            float bd = float.MaxValue; bi = 0;
            for (int j = 0; j < L.Count; j++) { float d = (L[j].pos - p).sqrMagnitude; if (d < bd) { bd = d; bi = j; } }
            best = L[bi];
            float dist = Mathf.Sqrt(bd);
            for (int k = -1; k <= 0; k++)
            {
                int a = bi + k, b = a + 1;
                if (a < 0 || b >= L.Count) continue;
                float t;
                float d = Spline.DistToSegment(p, L[a].pos, L[b].pos, out t);
                if (d < dist - 1e-5f) { dist = d; best = LerpSample(L[a], L[b], t); }
            }
            return dist;
        }

        /// <summary>Height of the master road's surface plane (grade + bank, no crown) extended to p, and its gradient.</summary>
        static float MasterPlane(List<RoadSample> M, bool closed, RoadSample m, int mi, Vector2 p, out Vector2 grad)
        {
            int n = M.Count;
            int a = closed ? (mi - 2 + n) % n : Mathf.Max(0, mi - 2), b = closed ? (mi + 2) % n : Mathf.Min(n - 1, mi + 2);
            float ds = Vector2.Distance(M[a].pos, M[b].pos);
            float grade = ds > 0.5f ? (M[b].height - M[a].height) / ds : 0f;
            Vector2 dir = ds > 0.5f ? (M[b].pos - M[a].pos) / ds : m.tangent;
            grad = dir * grade + m.Right * m.bank;
            return m.height + Vector2.Dot(grad, p - m.pos);
        }

        void FindJunctions()
        {
            junctions.Clear();
            for (int r = 0; r < roads.Count; r++)
            {
                if (roads[r].closed) continue;
                var L = roadSamples[r];
                if (L.Count < 4) continue;
                for (int end = 0; end < 2; end++)
                {
                    var E = L[end == 0 ? 0 : L.Count - 1];
                    int bestO = -1; float bestScore = 3.5f; bool bestE2E = false; RoadSample bestM = default(RoadSample);
                    for (int o = 0; o < roads.Count; o++)
                    {
                        if (o == r) continue;
                        var M = roadSamples[o];
                        if (M.Count < 2) continue;
                        RoadSample m; int mi;
                        float d = NearestOnList(M, E.pos, out m, out mi);
                        float score = d - m.halfWidth;
                        if (score >= 3.5f) continue;
                        float mTotal = M[M.Count - 1].distance;
                        bool e2e = !roads[o].closed && (m.distance < 3f || m.distance > mTotal - 3f);
                        if (e2e) score += 1f;          // prefer a through road over another road's end
                        if (score < bestScore) { bestScore = score; bestO = o; bestE2E = e2e; bestM = m; }
                    }
                    if (bestO < 0) continue;
                    if (bestE2E && r < bestO) continue;               // end-to-end: the later road follows the earlier one
                    if (bestM.bridge || E.bridge || Mathf.Abs(E.height - bestM.height) > 6f) continue;
                    junctions.Add(new RoadJunction
                    {
                        slave = r, master = bestO, slaveEnd = end, endToEnd = bestE2E, point = E.pos,
                        masterEnd = bestE2E ? (bestM.distance < 3f ? 0 : 1) : -1
                    });
                }
            }
        }

        /// <summary>
        /// Junction heights: the joining road meets the through road's surface exactly at the mouth (2 cm under it),
        /// with its cross slope tilted to match the through road's grade and bank there, then eases back to its own
        /// line over a distance long enough to keep its grade. Several passes so chains of junctions settle.
        /// </summary>
        void MatchJunctions()
        {
            if (junctions.Count == 0) return;
            var bank0 = new Dictionary<int, float[]>();
            foreach (var j in junctions)
                if (!bank0.ContainsKey(j.slave))
                {
                    var L = roadSamples[j.slave];
                    var b = new float[L.Count];
                    for (int i = 0; i < L.Count; i++) b[i] = L[i].bank;
                    bank0[j.slave] = b;
                }
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (var kv in bank0)
                {
                    var L = roadSamples[kv.Key];
                    for (int i = 0; i < L.Count; i++) { var s = L[i]; s.bank = kv.Value[i]; L[i] = s; }
                }
                foreach (var j in junctions)
                {
                    var L = roadSamples[j.slave];
                    var M = roadSamples[j.master];
                    bool mClosed = roads[j.master].closed;
                    int n = L.Count;
                    // the mouth: the first slave sample (walking in from its end) outside the master's footprint
                    int mouth = -1;
                    RoadSample m = default(RoadSample); int mi = 0;
                    for (int k = 0; k < Mathf.Min(n, 40); k++)
                    {
                        int idx = j.slaveEnd == 0 ? k : n - 1 - k;
                        float d = NearestOnList(M, L[idx].pos, out m, out mi);
                        bool beyondEnd = j.endToEnd && !mClosed
                                         && ((mi == 0 && Vector2.Dot(L[idx].pos - M[0].pos, M[0].tangent) < -0.05f)
                                          || (mi == M.Count - 1 && Vector2.Dot(L[idx].pos - M[M.Count - 1].pos, M[M.Count - 1].tangent) > 0.05f));
                        if (d > m.halfWidth + 0.3f || beyondEnd) { mouth = idx; break; }
                    }
                    if (mouth < 0) continue;
                    j.mouth = mouth;
                    Vector2 grad;
                    float plane = MasterPlane(M, mClosed, m, mi, L[mouth].pos, out grad);
                    float delta = plane - 0.02f - L[mouth].height;
                    float bJ = Mathf.Clamp(Vector2.Dot(grad, L[mouth].Right), -0.12f, 0.12f);
                    var def = roads[j.slave];
                    float total = L[n - 1].distance;
                    float len = Mathf.Min(Mathf.Max(45f, 3f * Mathf.Abs(delta) / Mathf.Max(0.03f, def.maxGrade)), Mathf.Max(20f, total));
                    float dm = L[mouth].distance;
                    for (int i = 0; i < n; i++)
                    {
                        float along = j.slaveEnd == 0 ? L[i].distance - dm : dm - L[i].distance;
                        var s = L[i];
                        if (along <= 0f)
                        {
                            // inside the master (hidden under it): sit on the master's plane
                            s.height = MasterPlane(M, mClosed, m, mi, s.pos, out grad) - 0.02f;
                            s.bank = bJ;
                        }
                        else
                        {
                            if (along < len) s.height += delta * (1f - Spline.SmoothStep(0f, len, along));
                            if (along < 30f) s.bank = Mathf.Lerp(bJ, s.bank, Spline.SmoothStep(0f, 30f, along));
                        }
                        L[i] = s;
                    }
                }
            }
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

        /// <summary>Taubin (lambda / mu) fairing: smooths curvature jumps shorter than ~25 m without shrinking bends.
        /// Open roads keep their first and last two points.</summary>
        public static List<Vector2> Fair(List<Vector2> pts, bool closed, int iters)
        {
            var p = new List<Vector2>(pts);
            int n = p.Count;
            if (n < 7) return p;
            var q = new Vector2[n];
            for (int it = 0; it < iters; it++)
                for (int pass = 0; pass < 2; pass++)
                {
                    float f = pass == 0 ? 0.5f : -0.53f;
                    for (int i = 0; i < n; i++)
                    {
                        if (!closed && (i < 2 || i > n - 3)) { q[i] = p[i]; continue; }
                        Vector2 a = p[(i - 1 + n) % n], c = p[(i + 1) % n];
                        q[i] = p[i] + f * ((a + c) * 0.5f - p[i]);
                    }
                    for (int i = 0; i < n; i++) p[i] = q[i];
                }
            return p;
        }

        /// <summary>Re-spaces a polyline to an even step (closed loops: exactly even, no short closing gap).</summary>
        public static List<Vector2> Resample(List<Vector2> pts, bool closed, float step)
        {
            if (closed && pts.Count > 2)
            {
                float L = 0f;
                for (int i = 0; i < pts.Count; i++) L += Vector2.Distance(pts[i], pts[(i + 1) % pts.Count]);
                int segs = Mathf.Max(3, Mathf.RoundToInt(L / step));
                step = L / segs;
                var o = new List<Vector2>(segs) { pts[0] };
                float t = step;
                float acc = 0f;
                for (int i = 0; i < pts.Count && o.Count < segs; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                    float seg = Vector2.Distance(a, b);
                    while (t <= acc + seg + 1e-4f && o.Count < segs)
                    {
                        o.Add(Vector2.Lerp(a, b, seg > 1e-6f ? (t - acc) / seg : 0f));
                        t += step;
                    }
                    acc += seg;
                }
                return o;
            }
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
