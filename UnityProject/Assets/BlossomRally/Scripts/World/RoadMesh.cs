// RoadMesh.cs - road mesh geometry (pure: lists of vertices / triangles, no engine objects, so the offline terrain
// harness can build and check exactly what the game builds). WorldBuilder.Roads.cs turns it into meshes.
//
//   * Ribbon: left edge, crowned centre, right edge (+ the sample's bank).
//   * Skirt: a strip from each edge 0.35 m out and down into the ground, so no gap ever shows under the road edge
//     on curves and slopes (the 4 m terrain grid is coarser than the road).
//   * Junctions (WorldField.junctions): the joining road is clipped exactly at the through road's edge (its
//     triangles minus the through road's footprint); the cut edge sits 1.2 cm under the through road's surface and
//     the crown fades out towards the mouth - no overlapping surfaces, so no z-fighting. Where a road starts at
//     another road's end, the earlier road gets a round cap that fills the corner, and a joining road that stops
//     short of the through road is extended to meet it.
//   * Lane lines stop at the mouth; the through road's edge line breaks across the mouth.
//   * The collider is the top surface only (no skirt). Every triangle faces up.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed class RoadMeshData
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
        /// <summary>Collider: top surface triangles only.</summary>
        public readonly List<Vector3> cv = new List<Vector3>();
        public readonly List<int> ct = new List<int>();
        /// <summary>Lane lines.</summary>
        public readonly List<Vector3> lv = new List<Vector3>();
        public readonly List<int> lt = new List<int>();
        public int clipped;
    }

    public sealed class RoadMeshBuilder
    {
        public const float SkirtOut = 0.35f;

        struct CVert
        {
            public Vector2 p;
            public float y, u, v;
            public int cutter;          // -1 = original vertex, else the cutter whose edge made it
        }

        sealed class Cutter
        {
            public Vector2[] poly;      // convex, counter-clockwise (seen from above, x right / z up)
            public Vector2 min, max;
            public int master;
        }

        readonly WorldField field;
        readonly Func<float, float, float> ground;
        readonly List<Cutter>[] cutters;
        public readonly List<RoadJunction>[] asSlave, asMaster;

        /// <summary>ground(x, z) = height of the terrain mesh (skirts reach just below it).</summary>
        public RoadMeshBuilder(WorldField f, Func<float, float, float> groundHeight)
        {
            field = f;
            ground = groundHeight;
            int n = f.roads.Count;
            cutters = new List<Cutter>[n];
            asSlave = new List<RoadJunction>[n];
            asMaster = new List<RoadJunction>[n];
            for (int r = 0; r < n; r++) { cutters[r] = new List<Cutter>(); asSlave[r] = new List<RoadJunction>(); asMaster[r] = new List<RoadJunction>(); }
            var edgeCache = new Dictionary<int, Vector2[][]>();
            foreach (var j in f.junctions)
            {
                asSlave[j.slave].Add(j);
                asMaster[j.master].Add(j);
                var M = f.roadSamples[j.master];
                bool closed = f.roads[j.master].closed;
                // the master's footprint exactly as its mesh is built (same fold-guarded edge points)
                Vector2[][] ed;
                if (!edgeCache.TryGetValue(j.master, out ed))
                {
                    ed = new[] { EdgePoints(M, -1f, 0f), EdgePoints(M, 1f, 0f) };
                    edgeCache[j.master] = ed;
                }
                float reach = f.roads[j.slave].width + f.roads[j.master].width * 0.5f + 10f;
                for (int k = 0; k < M.Count; k++)
                {
                    int k1 = k + 1;
                    if (k1 >= M.Count) { if (!closed) break; k1 = 0; }
                    if ((M[k].pos - j.point).sqrMagnitude > reach * reach && (M[k1].pos - j.point).sqrMagnitude > reach * reach) continue;
                    AddCutter(j.slave, j.master, new[] { ed[0][k], ed[0][k1], ed[1][k1], ed[1][k] });
                }
                if (j.endToEnd) AddCutter(j.slave, j.master, CapOutline(j));
            }
        }

        /// <summary>Top of the master's surface at p; past its open end the surface carries on at its last grade (caps).</summary>
        public float MasterSurface(int master, Vector2 p)
        {
            RoadSample s;
            if (field.NearestOnRoad(master, p, out s) == float.MaxValue) return float.MinValue;
            float lat = Mathf.Clamp(Vector2.Dot(p - s.pos, s.Right), -s.halfWidth, s.halfWidth);
            float h = s.SurfaceAt(lat);
            var M = field.roadSamples[master];
            if (field.roads[master].closed || M.Count < 3) return h;
            int end = s.distance < 0.05f ? 0 : s.distance > M[M.Count - 1].distance - 0.05f ? 1 : -1;
            if (end < 0) return h;
            var a = end == 0 ? M[0] : M[M.Count - 1];
            var b = end == 0 ? M[2] : M[M.Count - 3];
            Vector2 o = end == 0 ? -a.tangent : a.tangent;
            float beyond = Vector2.Dot(p - a.pos, o);
            if (beyond <= 0f) return h;
            float run = Vector2.Distance(a.pos, b.pos);
            float grade = run > 0.1f ? (a.height - b.height) / run : 0f;
            return h + grade * beyond;
        }

        void AddCutter(int slave, int master, Vector2[] poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Length; i++) { Vector2 a = poly[i], b = poly[(i + 1) % poly.Length]; area += a.x * b.y - a.y * b.x; }
            if (Mathf.Abs(area) < 1e-4f) return;
            if (area < 0f) Array.Reverse(poly);
            var c = new Cutter { poly = poly, master = master, min = poly[0], max = poly[0] };
            foreach (var p in poly) { c.min = Vector2.Min(c.min, p); c.max = Vector2.Max(c.max, p); }
            cutters[slave].Add(c);
        }

        /// <summary>Half disc beyond the master's end (end-to-end joints).</summary>
        public Vector2[] CapOutline(RoadJunction j)
        {
            var M = field.roadSamples[j.master];
            var e = M[j.masterEnd == 0 ? 0 : M.Count - 1];
            Vector2 o = j.masterEnd == 0 ? -e.tangent : e.tangent;
            Vector2 rt = e.Right;
            // sharp corner (an L): a square cap gives the square outer corner; otherwise a half disc
            var S = field.roadSamples[j.slave];
            var se = S[j.slaveEnd == 0 ? 0 : S.Count - 1];
            Vector2 sd = j.slaveEnd == 0 ? se.tangent : -se.tangent;
            if (Mathf.Abs(Vector2.Dot(sd, o)) < 0.5f)
                return new[] { e.pos + rt * e.halfWidth, e.pos + rt * e.halfWidth + o * e.halfWidth, e.pos - rt * e.halfWidth + o * e.halfWidth, e.pos - rt * e.halfWidth };
            var pts = new Vector2[9];
            for (int k = 0; k <= 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                pts[k] = e.pos + (rt * Mathf.Cos(a) + o * Mathf.Sin(a)) * e.halfWidth;
            }
            return pts;
        }

        /// <summary>The road's samples, plus a few extra rows where a joining road stops short of the through road.</summary>
        public List<RoadSample> WithExtensions(int r)
        {
            var src = field.roadSamples[r];
            var list = new List<RoadSample>(src);
            foreach (var j in asSlave[r])
            {
                if (j.endToEnd) continue;
                var E = j.slaveEnd == 0 ? src[0] : src[src.Count - 1];
                RoadSample m;
                float d = field.NearestOnRoad(j.master, E.pos, out m);
                if (d == float.MaxValue || d < m.halfWidth - 0.4f) continue;
                Vector2 o = j.slaveEnd == 0 ? -E.tangent : E.tangent;
                var extra = new List<RoadSample>();
                float gap = Mathf.Max(1f, d - m.halfWidth + 1f);
                for (int k = 1; k <= 14; k++)
                {
                    var s = E;
                    s.pos = E.pos + o * k;
                    s.distance = E.distance + (j.slaveEnd == 0 ? -k : k);
                    float dk = field.NearestOnRoad(j.master, s.pos, out m);
                    float target = field.RoadSurface(j.master, s.pos) - WorldField.RoadLift - 0.012f;
                    if (target > -1e6f) s.height = Mathf.Lerp(E.height, target, Mathf.Clamp01(k / gap));
                    extra.Add(s);
                    if (dk < m.halfWidth - 0.6f) break;
                }
                if (j.slaveEnd == 0) { extra.Reverse(); list.InsertRange(0, extra); }
                else list.AddRange(extra);
            }
            return list;
        }

        /// <summary>Crown factor 0..1: the crown fades out over the last metres before a mouth.</summary>
        public float CrownAt(int r, RoadSample s)
        {
            float c = 1f;
            var L = field.roadSamples[r];
            foreach (var j in asSlave[r])
            {
                if (j.mouth < 0) continue;
                float md = L[j.mouth].distance;
                float along = j.slaveEnd == 0 ? s.distance - md : md - s.distance;
                c *= Spline.SmoothStep(-1f, 6f, along);
            }
            return c;
        }

        /// <summary>
        /// Points offset sideways from the centreline (positive = right). Where a bend is still tighter than the offset,
        /// the edge would run backwards and fold; such points are held so the edge stays clean.
        /// </summary>
        public static Vector2[] EdgePoints(List<RoadSample> run, float offsetFraction, float extra)
        {
            int n = run.Count;
            var e = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var s = run[i];
                Vector2 p = s.pos + s.Right * (s.halfWidth * offsetFraction + extra);
                if (i > 0 && Vector2.Dot(p - e[i - 1], s.tangent) < 0.02f) p = e[i - 1] + s.tangent * 0.02f;
                e[i] = p;
            }
            return e;
        }

        // ------------------------------------------------------------------ ribbon
        static readonly float[] Us = { 0f, 0f, 0f, 0.5f, 1f, 1f, 1f };
        static readonly int[,] Quads = { { 0, 1 }, { 2, 3 }, { 3, 4 }, { 5, 6 } };

        public RoadMeshData Piece(int r, List<RoadSample> run, float tile, bool lines)
        {
            var d = new RoadMeshData();
            int n = run.Count;
            var leftE = EdgePoints(run, -1f, 0f);
            var rightE = EdgePoints(run, 1f, 0f);
            var leftS = EdgePoints(run, -1f, -SkirtOut);
            var rightS = EdgePoints(run, 1f, SkirtOut);
            // 7 columns per row: skirt L, edge L (skirt copy), edge L, centre, edge R, edge R (skirt copy), skirt R
            var P = new Vector2[n * 7];
            var Y = new float[n * 7];
            var V = new float[n];
            float startDist = run[0].distance;
            for (int i = 0; i < n; i++)
            {
                var s = run[i];
                float yl = s.height + WorldField.RoadLift - s.bank * s.halfWidth, yr = s.height + WorldField.RoadLift + s.bank * s.halfWidth;
                float yc = s.height + WorldField.RoadLift + WorldField.RoadCrown * CrownAt(r, s);
                float sl, sr;
                if (s.bridge) { sl = yl - 0.3f; sr = yr - 0.3f; }
                else
                {
                    sl = Mathf.Clamp(ground(leftS[i].x, leftS[i].y) - 0.1f, yl - 4f, yl - 0.3f);
                    sr = Mathf.Clamp(ground(rightS[i].x, rightS[i].y) - 0.1f, yr - 4f, yr - 0.3f);
                }
                int o = i * 7;
                P[o] = leftS[i]; P[o + 1] = leftE[i]; P[o + 2] = leftE[i]; P[o + 3] = s.pos; P[o + 4] = rightE[i]; P[o + 5] = rightE[i]; P[o + 6] = rightS[i];
                Y[o] = sl; Y[o + 1] = yl; Y[o + 2] = yl; Y[o + 3] = yc; Y[o + 4] = yr; Y[o + 5] = yr; Y[o + 6] = sr;
                float vv = (s.distance - startDist) / tile;
                V[i] = vv < 0f ? vv + 1000f : vv;
            }
            var cl = cutters[r];
            var rowBase = new int[n];
            for (int i = 0; i < n; i++)
            {
                rowBase[i] = d.v.Count;
                for (int k = 0; k < 7; k++)
                {
                    int o = i * 7 + k;
                    d.v.Add(new Vector3(P[o].x, Y[o], P[o].y));
                    d.uv.Add(new Vector2(Us[k], V[i]));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int q = 0; q < 4; q++)
                {
                    int ka = Quads[q, 0], kb = Quads[q, 1];
                    bool top = q == 1 || q == 2;
                    int a0 = i * 7 + ka, b0 = i * 7 + kb, a1 = (i + 1) * 7 + ka, b1 = (i + 1) * 7 + kb;
                    if (cl.Count > 0 && Touches(cl, P[a0], P[b0], P[a1], P[b1]))
                    {
                        var A0 = new CVert { p = P[a0], y = Y[a0], u = Us[ka], v = V[i], cutter = -1 };
                        var A1 = new CVert { p = P[a1], y = Y[a1], u = Us[ka], v = V[i + 1], cutter = -1 };
                        var B0 = new CVert { p = P[b0], y = Y[b0], u = Us[kb], v = V[i], cutter = -1 };
                        var B1 = new CVert { p = P[b1], y = Y[b1], u = Us[kb], v = V[i + 1], cutter = -1 };
                        d.clipped += EmitClipped(d, cl, top, A0, A1, B1);
                        d.clipped += EmitClipped(d, cl, top, A0, B1, B0);
                        continue;
                    }
                    int va0 = rowBase[i] + ka, vb0 = rowBase[i] + kb, va1 = rowBase[i + 1] + ka, vb1 = rowBase[i + 1] + kb;
                    d.t.Add(va0); d.t.Add(va1); d.t.Add(vb1);
                    d.t.Add(va0); d.t.Add(vb1); d.t.Add(vb0);
                    if (top)
                    {
                        AddColTri(d, d.v[va0], d.v[va1], d.v[vb1]);
                        AddColTri(d, d.v[va0], d.v[vb1], d.v[vb0]);
                    }
                }
            FaceUp(d.v, d.t);
            FaceUp(d.cv, d.ct);
            if (lines) Lines(r, run, d);
            return d;
        }

        static void AddColTri(RoadMeshData b, Vector3 a, Vector3 c, Vector3 e)
        {
            int i = b.cv.Count;
            b.cv.Add(a); b.cv.Add(c); b.cv.Add(e);
            b.ct.Add(i); b.ct.Add(i + 1); b.ct.Add(i + 2);
        }

        /// <summary>Makes every triangle face up (+Y) regardless of how it was wound.</summary>
        public static void FaceUp(List<Vector3> v, List<int> t)
        {
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                float ny = (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z);
                if (ny < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
        }

        static bool Touches(List<Cutter> cl, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 mn = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d)), mx = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
            foreach (var k in cl)
                if (mn.x <= k.max.x && mx.x >= k.min.x && mn.y <= k.max.y && mx.y >= k.min.y) return true;
            return false;
        }

        // ------------------------------------------------------------------ clipping (triangle minus convex polygons)
        int EmitClipped(RoadMeshData d, List<Cutter> cl, bool top, CVert a, CVert b, CVert c)
        {
            var pieces = new List<List<CVert>> { new List<CVert> { a, b, c } };
            for (int ci = 0; ci < cl.Count && pieces.Count > 0; ci++)
            {
                var k = cl[ci];
                var next = new List<List<CVert>>();
                foreach (var poly in pieces)
                {
                    if (!Overlaps(poly, k)) { next.Add(poly); continue; }
                    var cur = poly;
                    for (int e = 0; e < k.poly.Length && cur.Count >= 3; e++)
                    {
                        Vector2 p0 = k.poly[e], p1 = k.poly[(e + 1) % k.poly.Length];
                        var outside = ClipHalf(cur, p0, p1, false, ci);
                        if (outside.Count >= 3 && Area(outside) > 1e-4f) next.Add(outside);
                        cur = ClipHalf(cur, p0, p1, true, ci);
                    }
                    // what is left of cur lies inside the cutter: dropped
                }
                pieces = next;
            }
            int tris = 0;
            foreach (var poly in pieces)
            {
                int i0 = d.v.Count;
                foreach (var cv in poly)
                {
                    float y = cv.y;
                    if (cv.cutter >= 0)
                    {
                        // the cut edge sits just under the through road's surface (a seamless, flicker-free joint)
                        float ms = MasterSurface(cl[cv.cutter].master, cv.p);
                        if (ms > -1e6f && Mathf.Abs(ms - y) < 0.4f) y = top ? ms - 0.012f : Mathf.Min(y, ms - 0.3f);
                    }
                    d.v.Add(new Vector3(cv.p.x, y, cv.p.y));
                    d.uv.Add(new Vector2(cv.u, cv.v));
                }
                for (int k = 1; k < poly.Count - 1; k++)
                {
                    d.t.Add(i0); d.t.Add(i0 + k); d.t.Add(i0 + k + 1);
                    if (top) AddColTri(d, d.v[i0], d.v[i0 + k], d.v[i0 + k + 1]);
                    tris++;
                }
            }
            return tris;
        }

        static bool Overlaps(List<CVert> poly, Cutter k)
        {
            Vector2 mn = poly[0].p, mx = poly[0].p;
            foreach (var v in poly) { mn = Vector2.Min(mn, v.p); mx = Vector2.Max(mx, v.p); }
            return mn.x <= k.max.x && mx.x >= k.min.x && mn.y <= k.max.y && mx.y >= k.min.y;
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) { return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x); }

        /// <summary>Sutherland-Hodgman against the line a->b: keepInside keeps the left side (inside a CCW polygon).</summary>
        static List<CVert> ClipHalf(List<CVert> poly, Vector2 a, Vector2 b, bool keepInside, int cutter)
        {
            var o = new List<CVert>(poly.Count + 2);
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                CVert P = poly[i], Q = poly[(i + 1) % n];
                float sp = Side(a, b, P.p), sq = Side(a, b, Q.p);
                if (!keepInside) { sp = -sp; sq = -sq; }
                bool pin = sp >= 0f, qin = sq >= 0f;
                if (pin) o.Add(P);
                if (pin != qin)
                {
                    float t = sp / (sp - sq);
                    o.Add(new CVert
                    {
                        p = Vector2.LerpUnclamped(P.p, Q.p, t), y = P.y + (Q.y - P.y) * t, u = P.u + (Q.u - P.u) * t,
                        v = P.v + (Q.v - P.v) * t, cutter = cutter
                    });
                }
            }
            return o;
        }

        static float Area(List<CVert> poly)
        {
            float a = 0f;
            for (int i = 0; i < poly.Count; i++) { Vector2 p = poly[i].p, q = poly[(i + 1) % poly.Count].p; a += p.x * q.y - p.y * q.x; }
            return Mathf.Abs(a) * 0.5f;
        }

        // ------------------------------------------------------------------ caps (end-to-end joints)
        /// <summary>Round cap filling the corner where a road starts at the master's end (null on a bridge).</summary>
        public RoadMeshData Cap(RoadJunction j)
        {
            var M = field.roadSamples[j.master];
            var e = M[j.masterEnd == 0 ? 0 : M.Count - 1];
            if (e.bridge) return null;
            var outline = CapOutline(j);
            var d = new RoadMeshData();
            // a square cap gets its centre on the far half so the fan stays inside it
            Vector2 centre = outline.Length == 4 ? (outline[0] + outline[1] + outline[2] + outline[3]) * 0.25f : e.pos;
            d.v.Add(new Vector3(centre.x, MasterSurface(j.master, centre), centre.y));
            d.uv.Add(new Vector2(0.5f, 0f));
            for (int k = 0; k < outline.Length; k++)
            {
                Vector2 p = outline[k];
                float lat = Vector2.Dot(p - e.pos, e.Right);
                d.v.Add(new Vector3(p.x, MasterSurface(j.master, p), p.y));
                d.uv.Add(new Vector2(0.5f + 0.5f * lat / e.halfWidth, Vector2.Distance(p, e.pos) / 7f));
                if (k > 0) { d.t.Add(0); d.t.Add(k); d.t.Add(k + 1); AddColTri(d, d.v[0], d.v[k], d.v[k + 1]); }
                if (k == outline.Length - 1 && outline.Length == 4) { d.t.Add(0); d.t.Add(k + 1); d.t.Add(1); AddColTri(d, d.v[0], d.v[k + 1], d.v[1]); }
            }
            int s0 = d.v.Count;
            for (int k = 0; k < outline.Length; k++)
            {
                Vector2 dir = (outline[k] - centre).normalized;
                Vector2 q = outline[k] + dir * SkirtOut;
                d.v.Add(new Vector3(q.x, Mathf.Min(d.v[k + 1].y - 0.3f, ground(q.x, q.y) - 0.1f), q.y));
                d.uv.Add(d.uv[k + 1]);
                if (k > 0) { d.t.Add(k); d.t.Add(s0 + k - 1); d.t.Add(s0 + k); d.t.Add(k); d.t.Add(s0 + k); d.t.Add(k + 1); }
            }
            FaceUp(d.v, d.t);
            FaceUp(d.cv, d.ct);
            return d;
        }

        // ------------------------------------------------------------------ lane lines
        /// <summary>True where a lane line must not be painted: inside a through road (joining road) or across a mouth.</summary>
        public bool LineBlocked(int r, Vector2 p, bool edgeLine)
        {
            foreach (var j in asSlave[r])
            {
                if ((p - j.point).sqrMagnitude > 900f) continue;
                RoadSample m;
                float d = field.NearestOnRoad(j.master, p, out m);
                if (d < m.halfWidth + 0.5f) return true;
            }
            if (!edgeLine) return false;
            foreach (var j in asMaster[r])
            {
                float reach = field.roads[j.slave].width + field.roads[r].width + 6f;
                if ((p - j.point).sqrMagnitude > reach * reach) continue;
                RoadSample s;
                if (field.NearestOnRoad(j.slave, p, out s) == float.MaxValue) continue;
                var SL = field.roadSamples[j.slave];
                bool isEnd = !field.roads[j.slave].closed && (s.distance < 0.05f || s.distance > SL[SL.Count - 1].distance - 0.05f);
                if (isEnd)
                {
                    Vector2 o = s.distance < 0.05f ? -s.tangent : s.tangent;
                    if (Vector2.Dot(p - s.pos, o) > 0f) continue;
                }
                if (Mathf.Abs(Vector2.Dot(p - s.pos, s.Right)) < s.halfWidth + 0.8f) return true;
            }
            return false;
        }

        void Lines(int r, List<RoadSample> run, RoadMeshData d)
        {
            int n = run.Count;
            var lineL = EdgePoints(run, -1f, 0.45f);
            var lineR = EdgePoints(run, 1f, -0.45f);
            bool junctionNear = asSlave[r].Count > 0 || asMaster[r].Count > 0;
            for (int i = 0; i < n - 1; i++)
            {
                var s0 = run[i]; var s1 = run[i + 1];
                Vector2 n0 = s0.Right, n1 = s1.Right;
                for (int k = 0; k < 3; k++)
                {
                    if (k == 2 && (((s0.distance % 7f) + 7f) % 7f) > 3.5f) continue;
                    float w = k == 2 ? 0.08f : 0.1f;
                    Vector2 c0 = k == 0 ? lineL[i] : k == 1 ? lineR[i] : s0.pos;
                    Vector2 c1 = k == 0 ? lineL[i + 1] : k == 1 ? lineR[i + 1] : s1.pos;
                    if ((c1 - c0).sqrMagnitude < 0.0004f) continue;          // held (folded) stretch: no line
                    if (junctionNear && (LineBlocked(r, c0, k < 2) || LineBlocked(r, c1, k < 2))) continue;
                    float lat0 = Vector2.Dot(c0 - s0.pos, n0), lat1 = Vector2.Dot(c1 - s1.pos, n1);
                    float c = WorldField.RoadCrown;
                    float y0 = s0.height + WorldField.RoadLift + s0.bank * lat0 + c * CrownAt(r, s0) * Mathf.Max(0f, 1f - Mathf.Abs(lat0) / s0.halfWidth) + 0.014f;
                    float y1 = s1.height + WorldField.RoadLift + s1.bank * lat1 + c * CrownAt(r, s1) * Mathf.Max(0f, 1f - Mathf.Abs(lat1) / s1.halfWidth) + 0.014f;
                    int b = d.lv.Count;
                    d.lv.Add(new Vector3(c0.x - n0.x * w, y0, c0.y - n0.y * w));
                    d.lv.Add(new Vector3(c0.x + n0.x * w, y0, c0.y + n0.y * w));
                    d.lv.Add(new Vector3(c1.x - n1.x * w, y1, c1.y - n1.y * w));
                    d.lv.Add(new Vector3(c1.x + n1.x * w, y1, c1.y + n1.y * w));
                    d.lt.Add(b); d.lt.Add(b + 2); d.lt.Add(b + 1); d.lt.Add(b + 1); d.lt.Add(b + 2); d.lt.Add(b + 3);
                }
            }
            FaceUp(d.lv, d.lt);
        }
    }
}
