// MountainsRegion.Build.cs - builds the Cloud Peak Mountains: orchestration, shared helpers, the colour grade,
// the stone arch bridge over the lake-outlet ravine and the roadside (granite guard walls, rockfall nets,
// delineators, kilometre stones, chevrons on the hairpins, stone lanterns up to the monastery).
// Nature (pillars, forests, water, cloud sea) is in MountainsNature.Build.cs, the monastery / pavilions /
// cable car in MountainsLandmarks.Build.cs, effects and per-frame life in MountainsLife.Build.cs.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class MountainsRegion
    {
        RegionContext ctx;
        System.Random rng;
        float sliceT;

        sealed class RoadRun
        {
            public int index;
            public RoadDef def;
            public List<RoadSample> s;
            public bool mine;
        }
        readonly List<RoadRun> runs = new List<RoadRun>();

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        bool Chance(float p) { return rng.NextDouble() < p; }
        static Vector3 V3(Vector2 p, float y) { return new Vector3(p.x, y, p.y); }
        float Gnd(Vector2 p) { return ctx.Ground(p.x, p.y); }
        float Gnd(float x, float z) { return ctx.Ground(x, z); }
        static float YawOf(Vector2 dir) { return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; }

        /// <summary>True when the current slice of work has used its time budget (then the caller yields).</summary>
        bool Slice()
        {
            float now = Time.realtimeSinceStartup;
            if (now - sliceT < 0.006f) return false;
            sliceT = now;
            return true;
        }

        /// <summary>Stamp world-space geometry (model built in world coordinates) into the chunk around 'at'.</summary>
        void StampWorld(PropModel m, Vector3 at) { ctx.Meshes.Stamp(m, Matrix4x4.identity, at); }
        void StampWorldSkyline(PropModel m, Vector3 at) { ctx.SkylineMeshes.Stamp(m, Matrix4x4.identity, at); }

        /// <summary>Distance from p to the nearest road edge, also reporting that road's height there.</summary>
        float RoadEdge(Vector2 p, out float roadH, float max = 40f)
        {
            RoadSample rs;
            float d = ctx.Field.NearestRoad(p, out rs, max);
            roadH = rs.height;
            return d >= max ? 99f : d - rs.halfWidth;
        }

        // ------------------------------------------------------------------ grade: fresh, cool, misty jade
        public override Grade Grade
        {
            get
            {
                return new Grade
                {
                    tint = new Vector3(0.97f, 1.02f, 1.01f), sat = 1.02f, con = 0.98f,
                    shadow = new Vector3(0.86f, 0.98f, 1.04f), shadowAmt = 0.09f,
                    lift = new Vector3(0.82f, 0.95f, 0.93f), liftAmt = 0.045f
                };
            }
        }

        // ------------------------------------------------------------------ build
        public override IEnumerator Build(RegionContext c)
        {
            ctx = c;
            rng = c.Rng;
            sliceT = Time.realtimeSinceStartup;
            CollectRoads();
            IEnumerator[] steps =
            {
                BuildMonastery(), BuildPavilions(), BuildBridge(), BuildRoadside(), BuildWater(), BuildPillars(),
                BuildForest(), BuildWalkways(), BuildCableCar(), BuildClouds(), BuildLife()
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var e = steps[i];
                while (e.MoveNext()) yield return e.Current;
                ctx.Progress = (i + 1) / (float)steps.Length;
                yield return null;
                sliceT = Time.realtimeSinceStartup;
            }
        }

        void CollectRoads()
        {
            runs.Clear();
            var f = ctx.Field;
            for (int r = 0; r < f.roads.Count; r++)
            {
                var def = f.roads[r];
                bool mine = def.region == RegionId.Mountains && !def.connector;
                bool touches = false;
                if (!mine && def.connector)
                    foreach (var s in f.roadSamples[r]) if (ctx.InRegion(s.pos, 0f)) { touches = true; break; }
                if (!mine && !touches) continue;
                runs.Add(new RoadRun { index = r, def = def, s = f.roadSamples[r], mine = mine });
            }
        }

        RoadRun Run(string name)
        {
            foreach (var r in runs) if (r.def.name == name) return r;
            return null;
        }

        // ------------------------------------------------------------------ stone arch bridge (east pass)
        IEnumerator BuildBridge()
        {
            var run = Run(MountainsMap.RoadEast);
            if (run == null) yield break;
            var s = run.s;
            int i0 = -1, i1 = -1;
            for (int i = 0; i < s.Count; i++)
                if (s[i].bridge) { if (i0 < 0) i0 = i; i1 = i; }
            if (i0 < 0) yield break;
            int lo = Mathf.Max(0, i0 - 2), hi = Mathf.Min(s.Count - 1, i1 + 2);
            var m = new PropModel();
            var g = m[PK.Landmark];
            // arch: centred on the ravine, springing from the rock walls
            Vector2 bc = MountainsMap.BridgeAt;
            int ic = i0;
            float bestD = 1e9f;
            for (int i = i0; i <= i1; i++) { float d = (s[i].pos - bc).sqrMagnitude; if (d < bestD) { bestD = d; ic = i; } }
            float sC = s[ic].distance;
            float archHalf = Mathf.Min(17f, (s[i1].distance - s[i0].distance) * 0.5f - 3f);
            float crown = s[ic].height + 0.05f - 1.35f;
            float springY = Mathf.Max(Gnd(s[ic].pos) + 1.5f, crown - archHalf * 0.95f);
            const float wallIn = 0.05f, wallOut = 0.65f, wallH = 0.8f, cope = 0.14f;
            Color32 copeC = PG.C(222, 218, 206), soffit = PG.C(120, 116, 108), moss = PG.C(118, 134, 100);
            System.Func<int, float, float> Bottom = (j, side) =>
            {
                float d = s[j].distance - sC;
                float y = s[j].height + 0.05f;
                if (Mathf.Abs(d) < archHalf)
                {
                    float q = d / archHalf;
                    return springY + (crown - springY) * Mathf.Pow(1f - q * q, 0.5f);
                }
                Vector2 n = new Vector2(s[j].tangent.y, -s[j].tangent.x) * side;
                Vector2 e = s[j].pos + n * (s[j].halfWidth + wallOut);
                return Mathf.Min(Gnd(e) - 1.5f, y - 1.2f);
            };
            for (int j = lo; j < hi; j++)
            {
                var a = s[j]; var b = s[j + 1];
                Vector3 p0 = new Vector3(a.pos.x, a.height + 0.05f, a.pos.y), p1 = new Vector3(b.pos.x, b.height + 0.05f, b.pos.y);
                Vector3 n0 = new Vector3(a.tangent.y, 0f, -a.tangent.x), n1 = new Vector3(b.tangent.y, 0f, -b.tangent.x);
                float segLen = Vector2.Distance(a.pos, b.pos);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 o0 = p0 + n0 * side * (a.halfWidth + wallOut), o1 = p1 + n1 * side * (b.halfWidth + wallOut);
                    Vector3 i0v = p0 + n0 * side * (a.halfWidth + wallIn), i1v = p1 + n1 * side * (b.halfWidth + wallIn);
                    float b0 = Bottom(j, side), b1 = Bottom(j + 1, side);
                    int blk = Mathf.FloorToInt(a.distance / 1.6f);
                    Color32 wc = (blk & 1) == 0 ? MtnShapes.GraniteL : MtnShapes.GraniteM;
                    Color32 low = Color32.Lerp(wc, moss, 0.5f);
                    float t0 = p0.y + wallH, t1 = p1.y + wallH;
                    g.QuadOut(new Vector3(o0.x, b0, o0.z), new Vector3(o0.x, t0, o0.z), new Vector3(o1.x, t1, o1.z), new Vector3(o1.x, b1, o1.z), n0 * side, low, wc, wc, low);
                    g.QuadOut(new Vector3(i0v.x, p0.y - 0.05f, i0v.z), new Vector3(i0v.x, t0, i0v.z), new Vector3(i1v.x, t1, i1v.z), new Vector3(i1v.x, p1.y - 0.05f, i1v.z), -n0 * side, wc, wc, wc, wc);
                    // coping
                    Vector3 ci0 = i0v - n0 * side * 0.06f, ci1 = i1v - n1 * side * 0.06f, co0 = o0 + n0 * side * 0.06f, co1 = o1 + n1 * side * 0.06f;
                    g.QuadOut(ci0 + Vector3.up * (wallH + cope), co0 + Vector3.up * (wallH + cope), co1 + Vector3.up * (wallH + cope), ci1 + Vector3.up * (wallH + cope), Vector3.up, copeC, copeC, copeC, copeC);
                    g.QuadOut(co0 + Vector3.up * wallH, co0 + Vector3.up * (wallH + cope), co1 + Vector3.up * (wallH + cope), co1 + Vector3.up * wallH, n0 * side, copeC, copeC, copeC, copeC);
                    g.QuadOut(ci0 + Vector3.up * wallH, ci0 + Vector3.up * (wallH + cope), ci1 + Vector3.up * (wallH + cope), ci1 + Vector3.up * wallH, -n0 * side, copeC, copeC, copeC, copeC);
                    // voussoir ring around the arch on the outer face
                    float d0 = a.distance - sC, d1 = b.distance - sC;
                    if (Mathf.Abs(d0) < archHalf && Mathf.Abs(d1) < archHalf)
                    {
                        Color32 vc = (Mathf.FloorToInt(a.distance / 1.2f) & 1) == 0 ? PG.C(228, 222, 206) : PG.C(196, 190, 178);
                        Vector3 r0 = o0 + n0 * side * 0.12f, r1 = o1 + n1 * side * 0.12f;
                        g.QuadOut(new Vector3(r0.x, b0 - 0.1f, r0.z), new Vector3(r0.x, b0 + 1.1f, r0.z), new Vector3(r1.x, b1 + 1.1f, r1.z), new Vector3(r1.x, b1 - 0.1f, r1.z), n0 * side, vc, vc, vc, vc);
                    }
                    if (j == lo || j == hi - 1)
                    {
                        Vector3 pp = j == lo ? (i0v + o0) * 0.5f : (i1v + o1) * 0.5f;
                        pp.y = (j == lo ? t0 : t1) + cope;
                        PG.Box2(g, pp + Vector3.up * 0.35f, new Vector3(0.45f, 0.35f, 0.45f), copeC, MtnShapes.GraniteL);
                        g.Ellipsoid(pp + Vector3.up * 0.95f, new Vector3(0.28f, 0.28f, 0.28f), 6, 3, MtnShapes.GraniteL, MtnShapes.GraniteM);
                    }
                    else if ((j - lo) % 2 == 0)
                    {
                        Vector3 pp = (i1v + o1) * 0.5f;
                        pp.y = t1 + cope;
                        PG.Box2(g, pp + Vector3.up * 0.16f, new Vector3(0.22f, 0.16f, 0.22f), copeC, MtnShapes.GraniteL);
                    }
                    // parapet collider
                    Vector3 mid = (i0v + i1v + o0 + o1) * 0.25f;
                    mid.y = (p0.y + p1.y) * 0.5f + 0.75f;
                    ctx.Box(mid, new Vector3(wallOut - wallIn + 0.1f, 1.7f, segLen + 0.1f), YawOf(b.pos - a.pos));
                }
                // arch soffit / deck underside
                {
                    Vector3 l0 = p0 - n0 * (a.halfWidth + wallOut), r0 = p0 + n0 * (a.halfWidth + wallOut);
                    Vector3 l1 = p1 - n1 * (b.halfWidth + wallOut), r1 = p1 + n1 * (b.halfWidth + wallOut);
                    float bl0 = Bottom(j, -1), br0 = Bottom(j, 1), bl1 = Bottom(j + 1, -1), br1 = Bottom(j + 1, 1);
                    g.QuadOut(new Vector3(l0.x, bl0, l0.z), new Vector3(r0.x, br0, r0.z), new Vector3(r1.x, br1, r1.z), new Vector3(l1.x, bl1, l1.z), Vector3.down, soffit, soffit, soffit, soffit);
                }
            }
            // lantern posts and guardian lions at both ends
            foreach (int j in new[] { lo, hi })
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 n = new Vector2(s[j].tangent.y, -s[j].tangent.x) * side;
                    Vector2 off = s[j].tangent * (j == lo ? -2.5f : 2.5f);
                    Vector2 p = s[j].pos + n * (s[j].halfWidth + 1.6f) + off;
                    ctx.Stamp(MtnShapes.LanternPost(), V3(p, Gnd(p) - 0.05f), YawOf(-n));
                    ctx.Capsule(V3(p, Gnd(p)), 0.18f, 3f);
                    Vector2 lp = p + off * 1.4f;
                    ctx.Stamp(PropShapes.StoneLion(side > 0 ? 0 : 1), V3(lp, Gnd(lp) - 0.05f), YawOf(s[j].tangent * (j == lo ? -1f : 1f)), 1.2f);
                    ctx.Box(V3(lp, Gnd(lp) + 1f), new Vector3(1.2f, 2f, 1.6f), YawOf(s[j].tangent));
                }
            Vector3 at = new Vector3(s[ic].pos.x, s[ic].height, s[ic].pos.y);
            StampWorld(m, at);
            ctx.Reserve(s[ic].pos, 26f);
            yield return null;
        }

        // ------------------------------------------------------------------ roadside
        static readonly int[] KmEvery = { 100, 500 };

        IEnumerator BuildRoadside()
        {
            var post = MtnShapes.Delineator();
            var kmBig = MtnShapes.KmStone(true);
            var kmSmall = MtnShapes.KmStone(false);
            var chevL = MtnShapes.Chevron(1f);
            var chevR = MtnShapes.Chevron(-1f);
            var lantern = MtnShapes.StoneLantern();
            foreach (var run in runs)
            {
                var s = run.s;
                int n = s.Count;
                if (n < 8) continue;
                bool asphalt = run.def.surface == Surface.Asphalt;
                // ---- guard walls on the drop side, rockfall nets on the cut side
                for (int side = -1; side <= 1; side += 2)
                {
                    int wallStart = -1, netStart = -1;
                    for (int i = 0; i <= n; i++)
                    {
                        bool wantWall = false, wantNet = false;
                        if (i < n && !s[i].bridge && ctx.InRegion(s[i].pos, 2f) && i > 4 && i < n - 5)
                        {
                            Vector2 nn = new Vector2(s[i].tangent.y, -s[i].tangent.x) * side;
                            float hw = s[i].halfWidth;
                            float h = s[i].height;
                            float drop = h - Gnd(s[i].pos + nn * (hw + 5.5f));
                            float drop2 = h - Gnd(s[i].pos + nn * (hw + 9f));
                            bool open = OtherRoadNear(run.index, s[i].pos + nn * (hw + 2f), 7f) || NearBridgeEnd(run, i);
                            wantWall = !open && (drop > (wallStart >= 0 ? 1.3f : 2.4f) || drop2 > (wallStart >= 0 ? 3f : 5f));
                            float rise = Gnd(s[i].pos + nn * (hw + 7f)) - h;
                            float rise2 = Gnd(s[i].pos + nn * (hw + 3.5f)) - h;
                            wantNet = !open && !wantWall && rise > (netStart >= 0 ? 3.2f : 4.5f) && rise2 > 0.9f;
                        }
                        if (wantWall && wallStart < 0) wallStart = i;
                        if (!wantWall && wallStart >= 0)
                        {
                            if (i - wallStart >= 5) GuardWall(run, wallStart, i - 1, side);
                            wallStart = -1;
                        }
                        if (wantNet && netStart < 0) netStart = i;
                        if (!wantNet && netStart >= 0)
                        {
                            if (i - netStart >= 6) RockNet(run, netStart, i - 1, side);
                            netStart = -1;
                        }
                        if (Slice()) yield return null;
                    }
                }
                // ---- delineators, km stones, chevrons
                float nextPost = 12f;
                int lastChev = -99;
                for (int i = 2; i < n - 2; i++)
                {
                    var a = s[i];
                    if (!ctx.InRegion(a.pos, 3f) || a.bridge) continue;
                    Vector2 right = new Vector2(a.tangent.y, -a.tangent.x);
                    // kilometre / hectometre stones on the right
                    if (run.mine)
                    {
                        int dm = Mathf.FloorToInt(a.distance), dp = Mathf.FloorToInt(s[i - 1].distance);
                        if (dm / 100 != dp / 100 && dm >= 90)
                        {
                            bool big = dm / 500 != dp / 500;
                            Vector2 p = a.pos + right * (a.halfWidth + 1.4f);
                            if (!OtherRoadNear(run.index, p, 4f) && !WallAt(p))
                                ctx.Stamp(big ? kmBig : kmSmall, V3(p, Gnd(p) - 0.05f), YawOf(a.tangent) + 90f);
                        }
                    }
                    // chevrons on the outside of hairpins
                    if (i > 14 && i < n - 15 && i - lastChev >= 5)
                    {
                        Vector2 t0 = s[i - 12].tangent, t1 = s[i + 12].tangent;
                        float turn = Vector2.SignedAngle(t0, t1);
                        if (Mathf.Abs(turn) > 75f)
                        {
                            float outSide = turn > 0f ? 1f : -1f;      // turning left -> chevrons on the right
                            Vector2 nn = right * outSide;
                            Vector2 p = a.pos + nn * (a.halfWidth + 2.2f);
                            if (!OtherRoadNear(run.index, p, 5f))
                            {
                                ctx.Stamp(turn > 0f ? chevL : chevR, V3(p, Gnd(p) - 0.05f), YawOf(-nn));
                                ctx.Capsule(V3(p, Gnd(p)), 0.12f, 1.6f);
                                lastChev = i;
                            }
                        }
                    }
                    // delineators where there is no wall
                    if (a.distance >= nextPost)
                    {
                        nextPost = a.distance + (asphalt ? 24f : 30f);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = a.pos + right * side * (a.halfWidth + 1.25f);
                            if (WallAt(p) || OtherRoadNear(run.index, p, 4f)) continue;
                            if (Mathf.Abs(Gnd(p) - a.height) > 1.2f) continue;
                            ctx.Stamp(post, V3(p, Gnd(p) - 0.05f), YawOf(a.tangent));
                        }
                    }
                    if (Slice()) yield return null;
                }
            }
            // stone lanterns up the last stretch to the monastery, lantern posts round the col junction
            var main = Run(MountainsMap.RoadMain);
            if (main != null)
            {
                var s = main.s;
                float end = s[s.Count - 1].distance;
                int k = 0;
                for (int i = 0; i < s.Count; i++)
                {
                    if (s[i].distance < end - 280f || s[i].distance > end - 30f) continue;
                    if (Mathf.FloorToInt(s[i].distance / 32f) == Mathf.FloorToInt(s[Mathf.Max(0, i - 1)].distance / 32f)) continue;
                    float side = (k++ & 1) == 0 ? 1f : -1f;
                    Vector2 nn = new Vector2(s[i].tangent.y, -s[i].tangent.x) * side;
                    Vector2 p = s[i].pos + nn * (s[i].halfWidth + 1.8f);
                    if (WallAt(p)) p += nn * 0.9f;
                    ctx.Stamp(lantern, V3(p, Gnd(p) - 0.05f), YawOf(-nn));
                    ctx.Box(V3(p, Gnd(p) + 1f), new Vector3(0.7f, 2f, 0.7f), YawOf(nn));
                }
            }
            var lp = MtnShapes.LanternPost();
            for (int q = 0; q < 4; q++)
            {
                float a = q * Mathf.PI * 0.5f + 0.785f;
                Vector2 p = MountainsMap.Col + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f;
                float e = ctx.EdgeDist(p);
                if (e < 1.2f) p += (p - MountainsMap.Col).normalized * (1.6f - e);
                ctx.Stamp(lp, V3(p, Gnd(p) - 0.05f), YawOf(MountainsMap.Col - p));
                ctx.Capsule(V3(p, Gnd(p)), 0.15f, 3f);
            }
        }

        readonly List<Vector2> wallPts = new List<Vector2>();

        bool WallAt(Vector2 p)
        {
            for (int i = 0; i < wallPts.Count; i++) if ((wallPts[i] - p).sqrMagnitude < 9f) return true;
            return false;
        }

        bool OtherRoadNear(int road, Vector2 p, float within)
        {
            var f = ctx.Field;
            RoadSample rs;
            // check a few directions so a junction mouth is found even when this road is the nearest
            float d = f.NearestRoad(p, out rs, 30f);
            if (d < 30f && rs.road != road && d < rs.halfWidth + within) return true;
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * within;
                d = f.NearestRoad(q, out rs, 20f);
                if (d < 20f && rs.road != road && d < rs.halfWidth + 0.5f) return true;
            }
            return false;
        }

        bool NearBridgeEnd(RoadRun run, int i)
        {
            for (int k = -6; k <= 6; k++)
            {
                int j = i + k;
                if (j >= 0 && j < run.s.Count && run.s[j].bridge) return true;
            }
            return false;
        }

        /// <summary>Granite guard wall (dressed blocks, light coping, taller piers) along samples i0..i1 on one side.</summary>
        void GuardWall(RoadRun run, int i0, int i1, int side)
        {
            var s = run.s;
            var m = new PropModel();
            var g = m[PK.Props];
            const float inOff = 0.55f, outOff = 1.1f, H = 0.82f;
            Color32 copeC = PG.C(226, 222, 210), blockA = MtnShapes.GraniteL, blockB = PG.C(186, 182, 172), base0 = PG.C(140, 138, 130);
            int colStart = i0;
            for (int i = i0; i < i1; i++)
            {
                var a = s[i]; var b = s[i + 1];
                Vector3 na = new Vector3(a.tangent.y, 0f, -a.tangent.x) * side, nb = new Vector3(b.tangent.y, 0f, -b.tangent.x) * side;
                Vector3 pa = new Vector3(a.pos.x, a.height + 0.05f, a.pos.y), pb = new Vector3(b.pos.x, b.height + 0.05f, b.pos.y);
                Vector3 ia = pa + na * (a.halfWidth + inOff), ib = pb + nb * (b.halfWidth + inOff);
                Vector3 oa = pa + na * (a.halfWidth + outOff), ob = pb + nb * (b.halfWidth + outOff);
                float ga = Mathf.Min(Gnd(oa.x, oa.z), pa.y) - 0.6f, gb = Mathf.Min(Gnd(ob.x, ob.z), pb.y) - 0.6f;
                int blk = Mathf.FloorToInt(a.distance / 2.2f);
                Color32 c = (blk & 1) == 0 ? blockA : blockB;
                float ta = pa.y + H, tb = pb.y + H;
                g.QuadOut(new Vector3(ia.x, pa.y - 0.15f, ia.z), new Vector3(ia.x, ta, ia.z), new Vector3(ib.x, tb, ib.z), new Vector3(ib.x, pb.y - 0.15f, ib.z), -na, c, c, c, c);
                g.QuadOut(new Vector3(oa.x, ga, oa.z), new Vector3(oa.x, ta, oa.z), new Vector3(ob.x, tb, ob.z), new Vector3(ob.x, gb, ob.z), na, base0, c, c, base0);
                Vector3 ci = -na * 0.07f, co = na * 0.07f;
                g.QuadOut(new Vector3(ia.x, ta + 0.12f, ia.z) + ci, new Vector3(oa.x, ta + 0.12f, oa.z) + co, new Vector3(ob.x, tb + 0.12f, ob.z) + co, new Vector3(ib.x, tb + 0.12f, ib.z) + ci, Vector3.up, copeC, copeC, copeC, copeC);
                g.QuadOut(new Vector3(ia.x, ta, ia.z) + ci, new Vector3(ia.x, ta + 0.12f, ia.z) + ci, new Vector3(ib.x, tb + 0.12f, ib.z) + ci, new Vector3(ib.x, tb, ib.z) + ci, -na, copeC, copeC, copeC, copeC);
                g.QuadOut(new Vector3(oa.x, ta, oa.z) + co, new Vector3(oa.x, ta + 0.12f, oa.z) + co, new Vector3(ob.x, tb + 0.12f, ob.z) + co, new Vector3(ob.x, tb, ob.z) + co, na, copeC, copeC, copeC, copeC);
                // a taller pier every ~12 m and a mortar joint line on the road face
                if (Mathf.FloorToInt(a.distance / 12f) != Mathf.FloorToInt(b.distance / 12f) || i == i0)
                {
                    Vector3 pc = (ia + oa) * 0.5f;
                    PG.Box2(g, new Vector3(pc.x, ta + 0.08f, pc.z), new Vector3(0.36f, 0.24f, 0.36f), blockB, copeC);
                }
                wallPts.Add(new Vector2((ia.x + oa.x) * 0.5f, (ia.z + oa.z) * 0.5f));
                // collider every ~8 m
                if (i - colStart >= 4 || i == i1 - 1)
                {
                    var sa = s[colStart]; var sb = b;
                    Vector3 nA = new Vector3(sa.tangent.y, 0f, -sa.tangent.x) * side;
                    Vector3 cA = new Vector3(sa.pos.x, sa.height, sa.pos.y) + nA * (sa.halfWidth + (inOff + outOff) * 0.5f);
                    Vector3 cB = pb + nb * (b.halfWidth + (inOff + outOff) * 0.5f);
                    Vector3 mid = (cA + cB) * 0.5f;
                    mid.y += 0.5f;
                    float len = new Vector2(cB.x - cA.x, cB.z - cA.z).magnitude;
                    ctx.Box(mid, new Vector3(outOff - inOff + 0.1f, 1.8f, len + 0.3f), YawOf(new Vector2(cB.x - cA.x, cB.z - cA.z)));
                    colStart = i + 1;
                }
            }
            var mid2 = s[(i0 + i1) / 2];
            StampWorld(m, new Vector3(mid2.pos.x, mid2.height, mid2.pos.y));
        }

        /// <summary>Rockfall net draped on the cut slope above the road (samples i0..i1, one side).</summary>
        void RockNet(RoadRun run, int i0, int i1, int side)
        {
            var s = run.s;
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 net = PG.C(112, 118, 112), netD = PG.C(84, 92, 88), wire = PG.C(170, 172, 168), postC = PG.C(70, 74, 78);
            const float near = 2.6f, depth = 7.5f;
            for (int i = i0; i < i1; i += 2)
            {
                int j = Mathf.Min(i + 2, i1);
                var a = s[i]; var b = s[j];
                Vector2 na = new Vector2(a.tangent.y, -a.tangent.x) * side, nb = new Vector2(b.tangent.y, -b.tangent.x) * side;
                Vector2 a0 = a.pos + na * (a.halfWidth + near), b0 = b.pos + nb * (b.halfWidth + near);
                Vector2 a1 = a0 + na * depth, b1 = b0 + nb * depth;
                const int rows = 3;
                for (int r = 0; r < rows; r++)
                {
                    float t0 = r / (float)rows, t1 = (r + 1) / (float)rows;
                    Vector2 qa0 = Vector2.Lerp(a0, a1, t0), qb0 = Vector2.Lerp(b0, b1, t0), qa1 = Vector2.Lerp(a0, a1, t1), qb1 = Vector2.Lerp(b0, b1, t1);
                    Color32 cc = ((i / 2 + r) & 1) == 0 ? net : netD;
                    g.QuadOut(V3(qa0, Gnd(qa0) + 0.18f), V3(qb0, Gnd(qb0) + 0.18f), V3(qb1, Gnd(qb1) + 0.18f), V3(qa1, Gnd(qa1) + 0.18f), Vector3.up, cc, cc, cc, cc);
                }
                // diagonal wires + top cable + post
                PG.Rod(g, V3(a0, Gnd(a0) + 0.25f), V3(b1, Gnd(b1) + 0.25f), 0.03f, wire, 3);
                PG.Rod(g, V3(b0, Gnd(b0) + 0.25f), V3(a1, Gnd(a1) + 0.25f), 0.03f, wire, 3);
                Vector3 topA = V3(a1, Gnd(a1) + 1.6f), topB = V3(b1, Gnd(b1) + 1.6f);
                PG.Rod(g, topA, topB, 0.035f, wire, 3);
                g.Frustum(V3(a1, Gnd(a1) - 0.3f), topA + Vector3.up * 0.1f, 0.07f, 0.07f, 0.06f, 0.06f, 5, 0f, postC, postC, false, true);
                PG.Rod(g, V3(a0, Gnd(a0) + 0.2f), V3(a1, Gnd(a1) + 0.2f), 0.03f, wire, 3);
            }
            var mid = s[(i0 + i1) / 2];
            StampWorld(m, new Vector3(mid.pos.x, mid.height, mid.pos.y));
        }
    }
}
