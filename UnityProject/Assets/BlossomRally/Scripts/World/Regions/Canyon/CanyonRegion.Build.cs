// CanyonRegion.Build.cs - fills the Red Canyon: banded cap-rock cliffs on the mesas, a sculpted slot canyon,
// vertical strata walls along the road canyons and the gorge, hoodoo gardens and Danxia spires, wind-carved
// arches (one spans the main track), the timber trestle over the gorge, the Crescent Spring oasis (jade pool,
// poplars, tamarisk, reeds, tea-house, well, pavilion), the beacon tower and caravanserai ruins, Han wall
// remnants, the cracked-mud playa, saxaul / camelthorn scrub, cairns, bones, a wrecked truck, telegraph poles,
// marker posts, signposts and spectator camps. Animated life and effects live in CanyonLife.Build.cs.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CanyonRegion
    {
        RegionContext ctx;
        System.Random rng;
        float sliceT;
        readonly List<Vector3> steamSpots = new List<Vector3>();
        readonly List<Vector3> fireSpots = new List<Vector3>();
        int nRocks, nPlants, nCliffs, nHoodoos;

        public override Grade Grade
        {
            get
            {
                // warm, saturated golden hour: amber light, violet shadows
                return new Grade
                {
                    tint = new Vector3(1.07f, 0.99f, 0.88f), sat = 1.1f, con = 1.05f,
                    shadow = new Vector3(1.0f, 0.86f, 1.1f), shadowAmt = 0.08f,
                    lift = new Vector3(1.0f, 0.82f, 0.68f), liftAmt = 0.03f
                };
            }
        }

        bool Slice()
        {
            float t = Time.realtimeSinceStartup;
            if (t - sliceT < 0.007f) return false;
            sliceT = t;
            return true;
        }

        float Rand(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        bool Chance(float p) { return rng.NextDouble() < p; }
        float Gr(float x, float z) { return ctx.Ground(x, z); }
        Vector3 OnGround(Vector2 p, float dy = 0f) { return new Vector3(p.x, ctx.Ground(p.x, p.y) + dy, p.y); }
        static float YawOf(Vector2 d) { return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }

        /// <summary>Lowest ground under a footprint of radius r (so props never float on slopes).</summary>
        float GroundMin(Vector2 p, float r)
        {
            float h = Gr(p.x, p.y);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + 0.4f;
                h = Mathf.Min(h, Gr(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r));
            }
            return h;
        }

        List<RoadSample> Samples(string name)
        {
            int i = ctx.Field.RoadIndex(name);
            return i >= 0 ? ctx.Field.roadSamples[i] : null;
        }

        void StampBanded(PropModel m, Matrix4x4 M, bool skyline)
        {
            for (int k = 0; k < PropModel.Kinds; k++)
            {
                var g = m.geo[k];
                if (g == null || g.v.Count == 0 || (k != (int)PK.Landmark && k != (int)PK.Karst)) continue;
                CanyonKit.Band(g, 0, g.v.Count, M);
            }
            if (skyline) ctx.StampSkyline(m, M); else ctx.Stamp(m, M);
        }

        public override IEnumerator Build(RegionContext c)
        {
            ctx = c;
            rng = new System.Random(5150);
            sliceT = Time.realtimeSinceStartup;
            float t0 = sliceT;
            steamSpots.Clear(); fireSpots.Clear();

            for (var e = BuildMesaCrowns(); e.MoveNext();) yield return null;
            ctx.Progress = 0.08f;
            for (var e = BuildSlot(); e.MoveNext();) yield return null;
            for (var e = BuildBridge(); e.MoveNext();) yield return null;
            ctx.Progress = 0.14f;
            for (var e = BuildCanyonWalls(); e.MoveNext();) yield return null;
            ctx.Progress = 0.3f;
            for (var e = BuildArches(); e.MoveNext();) yield return null;
            for (var e = BuildHoodoos(); e.MoveNext();) yield return null;
            ctx.Progress = 0.42f;
            for (var e = BuildOasis(); e.MoveNext();) yield return null;
            ctx.Progress = 0.5f;
            for (var e = BuildRuins(); e.MoveNext();) yield return null;
            ctx.Progress = 0.58f;
            for (var e = BuildPlaya(); e.MoveNext();) yield return null;
            ctx.Progress = 0.64f;
            for (var e = BuildRoadside(); e.MoveNext();) yield return null;
            ctx.Progress = 0.72f;
            for (var e = BuildScatter(); e.MoveNext();) yield return null;
            ctx.Progress = 0.92f;
            BuildLife();
            ctx.Progress = 1f;
            Debug.Log("[BlossomRally] Red Canyon content: " + nCliffs + " cliff slabs, " + nHoodoos + " hoodoos, " + nRocks + " rocks, " + nPlants
                      + " plants, " + ctx.ColliderCount + " colliders in " + ((Time.realtimeSinceStartup - t0) * 1000f).ToString("0") + " ms");
        }

        // ------------------------------------------------------------------ mesa cap-rock cliffs
        IEnumerator BuildMesaCrowns()
        {
            foreach (var me in Mesas)
            {
                if (!me.crown) continue;
                Vector2 c = me.c;
                float top = -1e9f;
                for (int k = 0; k < 9; k++)
                {
                    float a = k * 0.7f, r = k == 0 ? 0f : Mathf.Min(me.rx, me.rz) * 0.25f;
                    top = Mathf.Max(top, Gr(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r));
                }
                float baseH = GroundMin(c, Mathf.Max(me.rx, me.rz) * 1.15f);
                float H = top - baseH;
                if (H < 8f) continue;
                float drop = Mathf.Min(H * 0.5f, 18f);
                float rmax = Mathf.Max(me.rx, me.rz) * 1.5f;
                int N = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * Mathf.Max(me.rx, me.rz) * 0.75f / 6f), 18, 56);
                var wallP = new Vector2[N];
                var edgeP = new Vector2[N];
                var wallG = new float[N];
                var ok = new bool[N];
                for (int j = 0; j < N; j++)
                {
                    float a = j * Mathf.PI * 2f / N;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float rEdge = -1f, rWall = -1f;
                    for (float r = 3f; r < rmax; r += 1.5f)
                    {
                        float h = Gr(c.x + d.x * r, c.y + d.y * r);
                        if (rEdge < 0f && h < top - 1.2f) rEdge = r;
                        if (h < top - drop) { rWall = r; break; }
                    }
                    if (rEdge < 0f || rWall < 0f) continue;
                    wallP[j] = c + d * rWall;
                    edgeP[j] = c + d * Mathf.Max(2f, rEdge - 2f);
                    wallG[j] = Gr(wallP[j].x, wallP[j].y);
                    ok[j] = ctx.EdgeDist(wallP[j]) > 5f;
                }
                var m = new PropModel();
                var g = m[PK.Landmark];
                const int NR = 6;
                Vector3 origin = new Vector3(c.x, 0f, c.y);
                var rows = new Vector3[N, NR + 2];
                for (int j = 0; j < N; j++)
                {
                    if (!ok[j]) continue;
                    Vector2 outw = (wallP[j] - c).normalized;
                    for (int r = 0; r <= NR; r++)
                    {
                        float t = r / (float)NR;
                        float y = Mathf.Lerp(wallG[j] - 2.5f, top + 0.6f, t);
                        float ledge = ((r & 1) == 0 ? 0f : -0.7f) + Rand(-0.35f, 0.35f) + (r == NR ? -0.4f : 0f);
                        Vector2 p = wallP[j] + outw * ledge;
                        rows[j, r] = new Vector3(p.x, y, p.y) - origin;
                    }
                    rows[j, NR + 1] = new Vector3(edgeP[j].x, top + 0.12f, edgeP[j].y) - origin;
                }
                for (int j = 0; j < N; j++)
                {
                    int j1 = (j + 1) % N;
                    if (!ok[j] || !ok[j1]) continue;
                    for (int r = 0; r <= NR; r++)
                    {
                        Vector3 a = rows[j, r], b = rows[j1, r], d = rows[j1, r + 1], e = rows[j, r + 1];
                        Vector3 mid = (a + b + d + e) * 0.25f;
                        Vector3 outw = new Vector3(mid.x, 0f, mid.z).normalized + (r == NR ? Vector3.up * 2f : Vector3.zero);
                        Color32 col = CanyonKit.Shade(r == NR ? Rand(0.9f, 1f) : Rand(0.82f, 1f));
                        g.QuadOut(a, b, d, e, outw, col, col, col, col);
                    }
                }
                StampBanded(m, Matrix4x4.Translate(origin), me.skyline);
                nCliffs++;
                if (Slice()) yield return null;
            }
        }

        // ------------------------------------------------------------------ the slot canyon
        IEnumerator BuildSlot()
        {
            var list = Samples(RoadMain);
            if (list == null) yield break;
            int ia = -1, ib = -1; float da = 1e9f, db = 1e9f;
            for (int i = 0; i < list.Count; i++)
            {
                float a = (list[i].pos - SlotA).sqrMagnitude, b = (list[i].pos - SlotB).sqrMagnitude;
                if (a < da) { da = a; ia = i; }
                if (b < db) { db = b; ib = i; }
            }
            if (ia < 0 || ib <= ia) yield break;
            ia = Mathf.Max(0, ia - 8); ib = Mathf.Min(list.Count - 1, ib + 8);
            const int NR = 9;
            for (int side = -1; side <= 1; side += 2)
            {
                var m = new PropModel();
                var g = m[PK.Landmark];
                Vector2 o2 = list[(ia + ib) / 2].pos;
                Vector3 origin = new Vector3(o2.x, 0f, o2.y);
                var cols = new List<Vector3[]>();
                float phase = side * 1.7f;
                for (int i = ia; i <= ib; i += 2)
                {
                    var s = list[i];
                    Vector2 n = new Vector2(-s.tangent.y, s.tangent.x) * side;
                    float u = (i - ia) / (float)(ib - ia);
                    float flare = 1f - Mathf.Sin(u * Mathf.PI);                       // walls open out at both ends
                    float H = 15f + 7f * Mathf.Sin(u * 7.3f + phase) + 4f * Mathf.Sin(u * 17f);
                    H *= 1f - 0.55f * flare * flare;
                    var col = new Vector3[NR + 2];
                    for (int r = 0; r <= NR; r++)
                    {
                        float t = r / (float)NR;
                        float off = 6.7f + 0.85f * Mathf.Sin(t * 4.1f + i * 0.19f + phase) + 0.55f * Mathf.Sin(t * 2.3f - i * 0.11f) + 1.6f * t * t + 9f * flare * flare;
                        Vector2 p = s.pos + n * off;
                        col[r] = new Vector3(p.x, s.height - 2.5f + (H + 2.5f) * t, p.y) - origin;
                    }
                    Vector2 back = s.pos + n * (17f + 6f * flare);
                    col[NR + 1] = new Vector3(back.x, s.height + H * 0.92f, back.y) - origin;
                    cols.Add(col);
                }
                for (int k = 0; k < cols.Count - 1; k++)
                    for (int r = 0; r <= NR; r++)
                    {
                        Vector3 a = cols[k][r], b = cols[k + 1][r], d = cols[k + 1][r + 1], e = cols[k][r + 1];
                        Vector3 mid = (a + b + d + e) * 0.25f;
                        int si = ia + k * 2;
                        Vector2 sp = list[Mathf.Min(si, list.Count - 1)].pos;
                        Vector3 toRoad = new Vector3(sp.x - origin.x - mid.x, 0f, sp.y - origin.z - mid.z);
                        Vector3 outw = r == NR ? Vector3.up : toRoad;
                        Color32 colr = CanyonKit.Shade(r == NR ? 0.95f : Rand(0.8f, 1f) * (0.86f + 0.14f * (r / (float)NR)));
                        g.QuadOut(a, b, d, e, outw, colr, colr, colr, colr);
                    }
                StampBanded(m, Matrix4x4.Translate(origin), false);
                // colliders: one thin box per ~8 m of wall
                for (int i = ia; i < ib; i += 4)
                {
                    var s = list[i];
                    var s2 = list[Mathf.Min(ib, i + 4)];
                    float u = (i + 2 - ia) / (float)(ib - ia);
                    float flare = 1f - Mathf.Sin(u * Mathf.PI);
                    Vector2 n = new Vector2(-s.tangent.y, s.tangent.x) * side;
                    Vector2 mid = (s.pos + s2.pos) * 0.5f + n * (6.7f - 0.8f + 0.6f + 9f * flare * flare);
                    float len = Vector2.Distance(s.pos, s2.pos) + 1f;
                    ctx.Box(new Vector3(mid.x, (s.height + s2.height) * 0.5f + 3f, mid.y), new Vector3(1.2f, 7f, len), YawOf(s.tangent));
                }
                nCliffs++;
                if (Slice()) yield return null;
            }
        }

        // ------------------------------------------------------------------ banded walls along canyons and the gorge
        IEnumerator BuildCanyonWalls()
        {
            string[] names = { RoadMain, RoadLoop, RoadWash, RoadHighway };
            foreach (var name in names)
            {
                var list = Samples(name);
                if (list == null) continue;
                float step = 11f, next = 0f;
                for (int i = 0; i < list.Count; i++)
                {
                    var s = list[i];
                    if (s.distance < next || s.bridge) continue;
                    next = s.distance + step * Rand(0.8f, 1.2f);
                    if (SegDist(s.pos.x, s.pos.y, SlotA, SlotB) < 40f) continue;
                    for (int side = -1; side <= 1; side += 2)
                        TryCliff(s.pos, new Vector2(-s.tangent.y, s.tangent.x) * side, s.height, s.halfWidth);
                    if (Slice()) yield return null;
                }
            }
            // gorge walls: slabs facing into the gorge from both rims
            float acc = 0f;
            for (int i = 0; i < Gorge.Length - 1; i++)
            {
                Vector2 a = Gorge[i], b = Gorge[i + 1];
                float len = Vector2.Distance(a, b);
                Vector2 t = (b - a) / len;
                for (float d = 0f; d < len; d += 9f)
                {
                    acc += 9f;
                    Vector2 p = a + t * d;
                    if ((p - GorgeCross).sqrMagnitude < 20f * 20f) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 n = new Vector2(-t.y, t.x) * side;
                        float floor = Gr(p.x, p.y);
                        // find the rim
                        float rimD = -1f;
                        for (float o = 3f; o < 26f; o += 1f)
                            if (Gr(p.x + n.x * o, p.y + n.y * o) > floor + 7f) { rimD = o; break; }
                        if (rimD < 0f) continue;
                        Vector2 q = p + n * (rimD - 2.5f);
                        if (!ctx.Free(q, 4f)) continue;
                        float rim = Gr(p.x + n.x * (rimD + 8f), p.y + n.y * (rimD + 8f));
                        float H = Mathf.Clamp(rim - floor + 0.5f, 6f, 20f);
                        var m = new PropModel();
                        CanyonKit.Slab(m[PK.Landmark], rng, Rand(9f, 12f), H, H * 0.8f + 2f);
                        StampBanded(m, Matrix4x4.TRS(new Vector3(q.x, floor - 0.3f, q.y), Quaternion.Euler(0f, YawOf(n), 0f), Vector3.one), false);
                        ctx.Reserve(q, 4f);
                        nCliffs++;
                    }
                }
                if (Slice()) yield return null;
            }
        }

        /// <summary>Looks sideways from a road sample for a canyon wall and dresses it with a vertical banded slab.</summary>
        void TryCliff(Vector2 p, Vector2 n, float roadH, float halfW)
        {
            float foot = -1f;
            for (float o = halfW + 2f; o < 42f; o += 2f)
                if (Gr(p.x + n.x * o, p.y + n.y * o) > roadH + 2.5f) { foot = o; break; }
            if (foot < 0f) return;
            float rise = Gr(p.x + n.x * (foot + 16f), p.y + n.y * (foot + 16f)) - roadH;
            if (rise < 9f) return;
            Vector2 q = p + n * (foot + 1.5f);
            if (!ctx.Free(q, 4.5f)) return;
            if (ctx.EdgeDist(q) < 2.2f) return;
            float baseY = Gr(q.x, q.y);
            float H = Mathf.Clamp(rise * Rand(0.55f, 0.8f), 6f, 19f);
            float w = Rand(9f, 14f);
            var m = new PropModel();
            CanyonKit.Slab(m[PK.Landmark], rng, w, H, H * 0.85f + 2f);
            float yaw = YawOf(n) + Rand(-8f, 8f);
            StampBanded(m, Matrix4x4.TRS(new Vector3(q.x, baseY - 0.4f, q.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one), false);
            ctx.Reserve(q, 5f);
            nCliffs++;
            if (ctx.EdgeDist(q) < 9f)
            {
                Vector2 bc = q + n * 1.5f;
                ctx.Box(new Vector3(bc.x, baseY + H * 0.5f - 1f, bc.y), new Vector3(w * 0.85f, H, 3f), yaw);
            }
        }

        // ------------------------------------------------------------------ arches
        IEnumerator BuildArches()
        {
            // 1) over the main track and 2) over the mesa loop, on flat canyon floor; 3) a free-standing landmark arch
            TryRoadArch(RoadMain, 1500f, 1650f, 11.5f, 14f);
            TryRoadArch(RoadLoop, 300f, 460f, 10.5f, 12.5f);
            Vector2 fa = new Vector2(-1172f, 930f);
            float y = GroundMin(fa, 12f);
            var m = new PropModel();
            CanyonKit.Arch(m[PK.Landmark], rng, 12f, 20f, 7f, 4.2f, 6f);
            float yaw = 18f;
            StampBanded(m, Matrix4x4.TRS(new Vector3(fa.x, y, fa.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one), true);
            ArchLegs(new Vector3(fa.x, y, fa.y), yaw, 12f, 7f, 6f);
            ctx.Reserve(fa, 16f);
            yield return null;
        }

        void TryRoadArch(string road, float from, float to, float S, float Hc)
        {
            var list = Samples(road);
            if (list == null) return;
            int best = -1; float bestScore = 1e9f;
            for (int i = 3; i < list.Count - 3; i += 3)
            {
                var s = list[i];
                if (s.distance < from || s.distance > to || s.bridge) continue;
                Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                float hl = Gr(s.pos.x + n.x * S, s.pos.y + n.y * S), hr = Gr(s.pos.x - n.x * S, s.pos.y - n.y * S);
                float turn = Vector2.Angle(list[i - 3].tangent, list[i + 3].tangent);
                float score = Mathf.Abs(hl - s.height) + Mathf.Abs(hr - s.height) + turn * 0.5f;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) return;
            var sb = list[best];
            Vector2 nn = new Vector2(-sb.tangent.y, sb.tangent.x);
            float yaw = YawOf(nn) - 90f;          // local x across the road
            Vector3 c = new Vector3(sb.pos.x, sb.height - 0.2f, sb.pos.y);
            var m = new PropModel();
            CanyonKit.Arch(m[PK.Landmark], rng, S, Hc, 5f, 3.4f, 5f);
            StampBanded(m, Matrix4x4.TRS(c, Quaternion.Euler(0f, yaw, 0f), Vector3.one), true);
            ArchLegs(c, yaw, S, 5f, 5f);
            ctx.Reserve(sb.pos + nn * S, 4f);
            ctx.Reserve(sb.pos - nn * S, 4f);
        }

        void ArchLegs(Vector3 c, float yaw, float S, float tl, float d)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 p = c + q * new Vector3(s * (S + 0.6f), 3f, 0f);
                ctx.Box(p, new Vector3(tl * 0.9f, 8f, d * 0.9f), yaw);
            }
        }

        // ------------------------------------------------------------------ hoodoos and Danxia spires
        IEnumerator BuildHoodoos()
        {
            // hoodoo gardens: (x, z, radius, count)
            Vector4[] gardens =
            {
                new Vector4(-880f, 690f, 40f, 9), new Vector4(-735f, 780f, 30f, 7), new Vector4(-985f, 770f, 28f, 6),
                new Vector4(-1150f, 870f, 34f, 8), new Vector4(-1260f, 900f, 26f, 6), new Vector4(-1005f, 1395f, 30f, 7),
                new Vector4(-1150f, 1300f, 32f, 7), new Vector4(-660f, 1240f, 30f, 6), new Vector4(-1040f, 870f, 18f, 4),
                new Vector4(-800f, 1180f, 26f, 6), new Vector4(-1350f, 1010f, 22f, 4), new Vector4(-1390f, 770f, 26f, 5),
            };
            foreach (var gd in gardens)
            {
                int n = (int)gd.w;
                for (int k = 0; k < n * 3 && n > 0; k++)
                {
                    float a = Rand(0f, 6.28f), r = Mathf.Sqrt(Rand(0f, 1f)) * gd.z;
                    Vector2 p = new Vector2(gd.x + Mathf.Cos(a) * r, gd.y + Mathf.Sin(a) * r);
                    float H = Rand(4.5f, 13f), Rb = H * Rand(0.16f, 0.24f);
                    if (PlaceColumn(p, H, Rb, Chance(0.75f) ? 0 : 1, false)) n--;
                }
                if (Slice()) yield return null;
            }
            // tall Danxia spires seen from afar
            Vector3[] spires =
            {
                new Vector3(-905f, 735f, 30f), new Vector3(-772f, 760f, 24f), new Vector3(-1010f, 1250f, 22f), new Vector3(-690f, 1140f, 26f),
                new Vector3(-1236f, 1370f, 20f), new Vector3(-1300f, 960f, 18f), new Vector3(-640f, 1320f, 28f), new Vector3(-1120f, 760f, 16f),
            };
            foreach (var sp in spires)
                for (int tries = 0; tries < 8; tries++)
                {
                    Vector2 p = new Vector2(sp.x + Rand(-12f, 12f), sp.y + Rand(-12f, 12f));
                    if (PlaceColumn(p, sp.z, sp.z * Rand(0.16f, 0.2f), Chance(0.5f) ? 1 : 2, true)) break;
                }
        }

        bool PlaceColumn(Vector2 p, float H, float Rb, int kind, bool skyline)
        {
            if (!ctx.InRegion(p, 30f)) return false;
            if (ctx.EdgeDist(p) < Rb + 3f) return false;
            if (!ctx.Free(p, Rb + 1f)) return false;
            if (OasisSdf(p.x, p.y) < 20f) return false;
            float y = GroundMin(p, Rb);
            if (Gr(p.x, p.y) - y > Rb * 1.2f) return false;
            var m = new PropModel();
            var g = m[PK.Landmark];
            CanyonKit.Column(g, rng, H, Rb, kind);
            float yaw = Rand(0f, 360f);
            var M = Matrix4x4.TRS(new Vector3(p.x, y, p.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            CanyonKit.Band(g, 0, g.v.Count, M);
            if (kind == 0)
            {
                // the cap stone sits on the column's top ring
                float topR = Rb * 0.58f;
                CanyonKit.CapStone(g, rng, new Vector3(0f, H, 0f), topR * 1.1f);
            }
            if (skyline) ctx.StampSkyline(m, M); else ctx.Stamp(m, M);
            ctx.Reserve(p, Rb + 1f);
            if (ctx.EdgeDist(p) < 18f) ctx.Capsule(new Vector3(p.x, y - 1f, p.y), Rb * 0.85f, H + 1f);
            nHoodoos++;
            return true;
        }

        // ------------------------------------------------------------------ the gorge bridge (timber trestle)
        IEnumerator BuildBridge()
        {
            var list = Samples(RoadMain);
            if (list == null) yield break;
            int a = -1, b = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].bridge) { if (a < 0) a = i; b = i; }
            if (a < 0) yield break;
            // extend onto the abutments
            int a0 = a, b0 = b;
            while (a0 > 0 && list[a].distance - list[a0].distance < 6f) a0--;
            while (b0 < list.Count - 1 && list[b0].distance - list[b].distance < 6f) b0++;
            var m = new PropModel();
            var g = m[PK.Landmark];
            var pr = m[PK.Props];
            var st = m[PK.Strong];
            Vector2 o2 = list[(a + b) / 2].pos;
            Vector3 origin = new Vector3(o2.x, 0f, o2.y);
            float hw = list[a].halfWidth;
            Color32 plank = LifeGeo.C(150, 112, 76), plankD = LifeGeo.C(120, 88, 60), timber = LifeGeo.C(110, 80, 56), timberD = LifeGeo.C(84, 60, 44);
            // deck planks, stringers, curbs and railings
            for (int i = a0; i <= b0; i++)
            {
                var s = list[i];
                Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                Vector3 c = new Vector3(s.pos.x, s.height + 0.035f, s.pos.y) - origin;
                Vector3 n3 = new Vector3(n.x, 0f, n.y), t3 = new Vector3(s.tangent.x, 0f, s.tangent.y);
                Quaternion q = Quaternion.LookRotation(t3, Vector3.up);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 pc = c + t3 * (k * 0.5f - 0.75f);
                    PG.OBox(pr, pc, new Vector3(hw + 0.7f, 0.045f, 0.21f), q, (k + i) % 3 == 0 ? plankD : plank);
                }
            }
            for (int i = a0; i < b0; i += 2)
            {
                var s = list[i]; var s2 = list[Mathf.Min(b0, i + 2)];
                Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                Vector3 p0 = new Vector3(s.pos.x, s.height, s.pos.y) - origin, p1 = new Vector3(s2.pos.x, s2.height, s2.pos.y) - origin;
                Vector3 n3 = new Vector3(n.x, 0f, n.y);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 o = n3 * side * (hw + 0.55f);
                    PG.Beam(g, p0 + o + Vector3.up * 0.18f, p1 + o + Vector3.up * 0.18f, 0.22f, 0.2f, timberD);                     // curb log
                    PG.Beam(g, p0 + o + Vector3.up * 1.15f, p1 + o + Vector3.up * 1.15f, 0.07f, 0.08f, timber);                     // hand rail
                    PG.Beam(g, p0 + o + Vector3.up * 0.65f, p1 + o + Vector3.up * 0.65f, 0.05f, 0.06f, timber);                     // mid rail
                    PG.Beam(g, p0 + n3 * side * (hw - 1.5f) - Vector3.up * 0.45f, p1 + n3 * side * (hw - 1.5f) - Vector3.up * 0.45f, 0.18f, 0.32f, timberD); // stringer
                    if ((i - a0) % 4 == 0) PG.Beam(g, p0 + o, p0 + o + Vector3.up * 1.25f, 0.1f, 0.1f, timberD);                      // post
                }
            }
            // trestle bents every ~4 m down to the gorge floor
            for (int i = a0; i <= b0; i += 2)
            {
                var s = list[i];
                if ((i - a0) % 2 != 0) continue;
                Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                Vector3 n3 = new Vector3(n.x, 0f, n.y);
                Vector3 top = new Vector3(s.pos.x, s.height - 0.7f, s.pos.y) - origin;
                float gy = Gr(s.pos.x, s.pos.y);
                if (top.y - gy < 1.2f) continue;
                PG.Beam(g, top - n3 * (hw + 0.8f), top + n3 * (hw + 0.8f), 0.18f, 0.2f, timberD);   // cap beam
                float[] offs = { -hw - 0.4f, -1.6f, 1.6f, hw + 0.4f };
                var feet = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    float batter = Mathf.Abs(offs[k]) > 2f ? Mathf.Sign(offs[k]) * (top.y - gy) * 0.12f : 0f;
                    Vector2 fp = s.pos + n * (offs[k] + batter);
                    feet[k] = new Vector3(fp.x, Gr(fp.x, fp.y) - 0.6f, fp.y) - origin;
                    PG.Beam(g, top + n3 * offs[k], feet[k], 0.14f, 0.14f, timber);
                }
                // X braces in 3 m storeys
                float hgt = top.y - Mathf.Max(feet[0].y, feet[3].y);
                int storeys = Mathf.Max(1, Mathf.FloorToInt(hgt / 3.2f));
                for (int k = 0; k < storeys; k++)
                {
                    float t0 = k / (float)storeys, t1 = (k + 1) / (float)storeys;
                    Vector3 l0 = Vector3.Lerp(top + n3 * offs[0], feet[0], t0), l1 = Vector3.Lerp(top + n3 * offs[0], feet[0], t1);
                    Vector3 r0 = Vector3.Lerp(top + n3 * offs[3], feet[3], t0), r1 = Vector3.Lerp(top + n3 * offs[3], feet[3], t1);
                    PG.Beam(g, l0, r1, 0.06f, 0.08f, timberD);
                    PG.Beam(g, r0, l1, 0.06f, 0.08f, timberD);
                    PG.Beam(g, l1, r1, 0.07f, 0.09f, timber);
                }
            }
            // stone abutments
            foreach (int e in new[] { a, b })
            {
                var s = list[e];
                Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                float gy = Gr(s.pos.x, s.pos.y);
                Quaternion q = Quaternion.LookRotation(new Vector3(s.tangent.x, 0f, s.tangent.y));
                Vector3 c = new Vector3(s.pos.x, (s.height - 0.5f + gy - 3f) * 0.5f, s.pos.y) - origin;
                PG.OBox(g, c, new Vector3(hw + 1.4f, (s.height - 0.5f - gy + 3f) * 0.5f, 1.6f), q, Pal.StoneDark);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 pp = new Vector3(s.pos.x + n.x * side * (hw + 1.2f), s.height, s.pos.y + n.y * side * (hw + 1.2f)) - origin;
                    PG.Beam(g, pp - Vector3.up * 0.5f, pp + Vector3.up * 1.7f, 0.22f, 0.22f, Pal.StoneLight);
                    PG.Rod(pr, pp + Vector3.up * 1.7f, pp + Vector3.up * 3.6f, 0.04f, timberD);
                    Color32 fc = side < 0 ? Pal.RedBright : LifeGeo.C(240, 200, 70);
                    Vector3 f0 = pp + Vector3.up * 3.55f, td = new Vector3(s.tangent.x, 0f, s.tangent.y) * 1.1f;
                    PG.Quad2(st, f0, f0 + td, f0 + td - Vector3.up * 0.7f, f0 - Vector3.up * 0.7f, PG.A(fc, 20), PG.A(fc, 220), PG.A(fc, 220), PG.A(fc, 20));
                }
            }
            ctx.Stamp(m, Matrix4x4.Translate(origin));
            // railing colliders (one per side, per few samples so they follow the deck)
            for (int i = a0; i < b0; i += 6)
            {
                var s = list[i]; var s2 = list[Mathf.Min(b0, i + 6)];
                Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 mid = (s.pos + s2.pos) * 0.5f + n * side * (hw + 0.6f);
                    ctx.Box(new Vector3(mid.x, (s.height + s2.height) * 0.5f + 0.6f, mid.y), new Vector3(0.35f, 1.3f, Vector2.Distance(s.pos, s2.pos) + 0.5f), YawOf(s.tangent));
                }
            }
            ctx.Reserve(o2, 18f);
            yield return null;
        }

        // ------------------------------------------------------------------ Crescent Spring oasis
        IEnumerator BuildOasis()
        {
            // jade pool (own material, built in BuildLife), reeds on the shore
            var reeds = new PropModel[4];
            for (int i = 0; i < reeds.Length; i++) reeds[i] = CanyonKit.Reeds(rng);
            for (int i = 0; i < 260; i++)
            {
                float a = Rand(0f, 6.28f), r = Rand(OasisRA - 6f, OasisRA + 4f);
                Vector2 p = OasisA + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                float so = OasisSdf(p.x, p.y);
                if (so < -1.5f || so > 2.5f) continue;
                if (Chance(0.45f)) continue;
                ctx.Stamp(reeds[rng.Next(reeds.Length)], OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.8f, 1.25f));
            }
            for (int i = 0; i < 160; i++)
            {
                float a = Rand(0f, 6.28f), r = Rand(OasisRB - 4f, OasisRB + 3f);
                Vector2 p = OasisB + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                float so = OasisSdf(p.x, p.y);
                if (so < -1.5f || so > 2.5f || Chance(0.4f)) continue;
                ctx.Stamp(reeds[rng.Next(reeds.Length)], OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.8f, 1.2f));
            }
            yield return null;
            // buildings
            float thYaw = -90f;
            Vector3 thPos = new Vector3(TeaHouse.x, PoolLevel + 1.0f, TeaHouse.y) + Quaternion.Euler(0f, thYaw, 0f) * new Vector3(0f, 0f, -2.7f);
            var th = CanyonBuildings.TeaHouse(rng, steamSpots);
            ctx.Stamp(th, thPos, thYaw);
            for (int i = 0; i < steamSpots.Count; i++) steamSpots[i] = thPos + Quaternion.Euler(0f, thYaw, 0f) * steamSpots[i];
            ctx.Box(thPos + Quaternion.Euler(0f, thYaw, 0f) * new Vector3(0f, 1.8f, -1.8f), new Vector3(9.8f, 3.6f, 5.2f), thYaw);
            for (int i = 0; i < 4; i++)
                for (int s = -1; s <= 1; s += 2)
                    ctx.Capsule(thPos + Quaternion.Euler(0f, thYaw, 0f) * new Vector3(-4.5f + i * 3f, 0f, s > 0 ? 7f : 0.9f), 0.15f, 3f);
            ctx.Reserve(TeaHouse, 10f);
            var well = CanyonBuildings.Well(rng);
            ctx.Stamp(well, new Vector3(Well.x, PoolLevel + 0.9f, Well.y), 30f);
            ctx.Box(new Vector3(Well.x, PoolLevel + 1.3f, Well.y), new Vector3(2.4f, 1.6f, 2.4f), 30f);
            ctx.Reserve(Well, 4f);
            float pavY = Gr(Pavilion.x, Pavilion.y);
            var pav = CanyonBuildings.Pavilion(rng);
            ctx.Stamp(pav, new Vector3(Pavilion.x, pavY, Pavilion.y), YawOf(OasisA - Pavilion));
            ctx.Box(new Vector3(Pavilion.x, pavY + 2f, Pavilion.y), new Vector3(5.4f, 4f, 5.4f), YawOf(OasisA - Pavilion));
            ctx.Reserve(Pavilion, 6f);
            fireSpots.Add(thPos + Quaternion.Euler(0f, thYaw, 0f) * new Vector3(0f, 2.4f, 7f));
            yield return null;
            // poplars (golden and green), tamarisk and camelthorn around the banks
            var poplars = new PropModel[6];
            for (int i = 0; i < poplars.Length; i++) poplars[i] = CanyonKit.Poplar(rng, Rand(0.9f, 1.25f), i % 3 != 0);
            var tam = new PropModel[4];
            for (int i = 0; i < tam.Length; i++) tam[i] = CanyonKit.Tamarisk(rng, Rand(0.9f, 1.3f));
            var greenTufts = new PropModel[4];
            for (int i = 0; i < greenTufts.Length; i++) greenTufts[i] = CanyonKit.Tuft(rng, Rand(0.7f, 1.2f), false);
            int placedP = 0;
            for (int i = 0; i < 220 && placedP < 30; i++)
            {
                Vector2 p = OasisA + new Vector2(Rand(-90f, 90f), Rand(-90f, 90f));
                float so = OasisSdf(p.x, p.y);
                if (so < 5f || so > 38f || ctx.EdgeDist(p) < 6f || !ctx.Free(p, 4f)) continue;
                ctx.Stamp(poplars[rng.Next(poplars.Length)], OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.85f, 1.2f));
                ctx.Reserve(p, 4f);
                if (ctx.EdgeDist(p) < 30f || so < 20f) ctx.Capsule(OnGround(p, -0.5f), 0.45f, 3f);
                placedP++; nPlants++;
            }
            for (int i = 0; i < 260; i++)
            {
                Vector2 p = OasisA + new Vector2(Rand(-110f, 110f), Rand(-110f, 110f));
                float so = OasisSdf(p.x, p.y);
                if (so < 2.5f || so > 60f || ctx.EdgeDist(p) < 2f || !ctx.Free(p, 1.6f)) continue;
                if (Chance(WorldNoise.Smooth(10f, 60f, so) * 0.7f)) continue;
                if (Chance(0.55f)) ctx.Stamp(tam[rng.Next(tam.Length)], OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.8f, 1.3f));
                else ctx.Stamp(greenTufts[rng.Next(greenTufts.Length)], OnGround(p, -0.05f), Rand(0f, 360f), Rand(0.8f, 1.2f));
                ctx.Reserve(p, 1.5f);
                nPlants++;
                if (Slice()) yield return null;
            }
        }

        // ------------------------------------------------------------------ beacon tower, caravanserai, Han wall
        IEnumerator BuildRuins()
        {
            Vector3 brazier;
            var wt = CanyonBuildings.Watchtower(rng, out brazier);
            float wy = GroundMin(Watchtower, 5f);
            float wyaw = 12f;
            ctx.StampSkyline(wt, new Vector3(Watchtower.x, wy, Watchtower.y), wyaw);
            ctx.Box(new Vector3(Watchtower.x, wy + 7f, Watchtower.y), new Vector3(9.4f, 15f, 9.4f), wyaw + 45f * 0f);
            fireSpots.Add(new Vector3(Watchtower.x, wy, Watchtower.y) + Quaternion.Euler(0f, wyaw, 0f) * brazier);
            ctx.Reserve(Watchtower, 9f);
            yield return null;

            // caravanserai: the gate faces the main track
            RoadSample rs;
            ctx.Field.NearestRoad(Caravanserai, out rs, 120f);
            float want = YawOf(rs.pos - Caravanserai);
            float yaw = CaravanseraiYaw;
            for (int k = 0; k < 4; k++)
            {
                float cand = CaravanseraiYaw + k * 90f;
                if (Mathf.Abs(Mathf.DeltaAngle(cand, want)) < 45f) yaw = cand;
            }
            var walls = new List<Vector4>();
            var camelSpots = new List<Vector3>();
            var cs = CanyonBuildings.Caravanserai(rng, walls, camelSpots);
            float cy = ctx.Field.BaseHeight(Caravanserai.x, Caravanserai.y);
            cy = Gr(Caravanserai.x, Caravanserai.y);
            Vector3 cpos = new Vector3(Caravanserai.x, cy, Caravanserai.y);
            var q = Quaternion.Euler(0f, yaw, 0f);
            ctx.Stamp(cs, cpos, yaw);
            foreach (var w in walls)
            {
                Vector3 c = cpos + q * new Vector3(w.x, 2.6f, w.y);
                ctx.Box(c, new Vector3(w.z * 2f, 5.6f, w.w * 2f), yaw);
            }
            for (int i = 0; i < camelSpots.Count; i++)
            {
                var cm = new PropModel();
                PG.Append(cm[PK.Props], CanyonBuildings.CamelBody(rng, i, true, i != 1), Matrix4x4.identity);
                ctx.Stamp(cm, cpos + q * camelSpots[i], yaw + Rand(-60f, 60f));
            }
            ctx.Reserve(Caravanserai, 30f);
            yield return null;

            // Han wall remnants marching across the southern desert, with a ruined beacon mound
            Vector2[] hanLine = { new Vector2(-1452f, 652f), new Vector2(-1360f, 640f), new Vector2(-1262f, 628f), new Vector2(-1190f, 618f) };
            for (int i = 0; i < hanLine.Length - 1; i++)
            {
                Vector2 a = hanLine[i], b = hanLine[i + 1];
                float len = Vector2.Distance(a, b);
                Vector2 t = (b - a) / len;
                for (float d = 0f; d < len - 8f; d += Rand(14f, 26f))
                {
                    float L = Rand(7f, 15f);
                    Vector2 p = a + t * (d + L * 0.5f);
                    if (ctx.EdgeDist(p) < L * 0.5f + 4f || !ctx.Free(p, L * 0.4f)) continue;
                    var hw = CanyonBuildings.HanWall(rng, L);
                    float y = GroundMin(p, L * 0.5f);
                    float yw = YawOf(t) - 90f;
                    ctx.Stamp(hw, new Vector3(p.x, y, p.y), yw);
                    ctx.Box(new Vector3(p.x, y + 1.2f, p.y), new Vector3(L, 2.6f, 2.4f), yw);
                    ctx.Reserve(p, L * 0.4f);
                }
            }
            var mound = CanyonBuildings.HanWall(rng, 6f);
            Vector2 mp = new Vector2(-1310f, 634f);
            if (ctx.Free(mp, 5f))
            {
                float y = GroundMin(mp, 4f);
                ctx.Stamp(mound, new Vector3(mp.x, y, mp.y), 0f, 1f);
                ctx.Stamp(mound, new Vector3(mp.x, y + 2.8f, mp.y), 90f, 0.7f);
                ctx.Box(new Vector3(mp.x, y + 2f, mp.y), new Vector3(6f, 4f, 6f), 0f);
            }
            yield return null;
        }

        // ------------------------------------------------------------------ cracked-mud playa
        IEnumerator BuildPlaya()
        {
            const float step = 1.75f;
            var m = new PropModel();
            var g = m[PK.Props];
            Vector3 origin = new Vector3(PlayaC.x, 0f, PlayaC.y);
            int plates = 0;
            for (float z = PlayaC.y - PlayaRZ; z <= PlayaC.y + PlayaRZ; z += step)
            {
                for (float x = PlayaC.x - PlayaRX; x <= PlayaC.x + PlayaRX; x += step)
                {
                    float jx = (WorldNoise.Hash01(Mathf.RoundToInt(x * 4f), Mathf.RoundToInt(z * 4f), 801) - 0.5f) * 0.6f;
                    float jz = (WorldNoise.Hash01(Mathf.RoundToInt(x * 4f), Mathf.RoundToInt(z * 4f), 803) - 0.5f) * 0.6f;
                    float px = x + jx + ((Mathf.RoundToInt(z / step) & 1) == 0 ? 0f : step * 0.5f), pz = z + jz;
                    Vector2 w = WorldNoise.Warp(px, pz, 170f, 26f, 611);
                    float pm = PlayaMask(w.x, w.y);
                    if (pm < 0.75f) continue;
                    Vector2 p = new Vector2(px, pz);
                    if (ctx.EdgeDist(p) < 1.2f) continue;
                    float y = Gr(px, pz) + 0.02f;
                    float r = step * 0.47f;
                    float sh = 0.9f + 0.1f * WorldNoise.Hash01(Mathf.RoundToInt(px * 3f), Mathf.RoundToInt(pz * 3f), 807);
                    Color32 cc = new Color32((byte)(236 * sh), (byte)(222 * sh), (byte)(198 * sh), 0);
                    Color32 ce = new Color32((byte)(222 * sh), (byte)(204 * sh), (byte)(178 * sh), 0);
                    Vector3 c = new Vector3(px, y, pz) - origin;
                    float rot = WorldNoise.Hash01(Mathf.RoundToInt(px * 5f), Mathf.RoundToInt(pz * 5f), 809) * 6.28f;
                    Vector3 prev = Vector3.zero, first = Vector3.zero;
                    for (int k = 0; k <= 6; k++)
                    {
                        float a = rot + (k % 6) * Mathf.PI / 3f;
                        float rr = r * (0.82f + 0.18f * WorldNoise.Hash01(Mathf.RoundToInt(px * 7f) + k, Mathf.RoundToInt(pz * 7f), 811));
                        Vector3 v = c + new Vector3(Mathf.Cos(a) * rr, 0.05f, Mathf.Sin(a) * rr);
                        if (k == 0) first = v;
                        if (k > 0) g.TriOut(c, prev, k == 6 ? first : v, Vector3.up, cc, ce, ce);
                        prev = v;
                    }
                    plates++;
                }
                if (Slice()) yield return null;
            }
            ctx.Stamp(m, Matrix4x4.Translate(origin));
            // the wreck rests on the playa's edge
            var wr = CanyonBuildings.Wreck(rng);
            Vector2 wp = Wreck;
            float wyaw = 128f;
            ctx.Stamp(wr, new Vector3(wp.x, Gr(wp.x, wp.y), wp.y), wyaw);
            ctx.Box(new Vector3(wp.x, Gr(wp.x, wp.y) + 1.1f, wp.y), new Vector3(2.4f, 2.2f, 6.8f), wyaw);
            ctx.Reserve(wp, 6f);
        }

        // ------------------------------------------------------------------ roadside: posts, stones, signs, poles, camps
        IEnumerator BuildRoadside()
        {
            var post = CanyonBuildings.MarkerPost();
            var stone = CanyonBuildings.MarkerStone();
            string[] rally = { RoadMain, RoadLoop, RoadWash, RoadDune };
            foreach (var name in rally)
            {
                var list = Samples(name);
                if (list == null) continue;
                float next = 20f, nextStone = 120f;
                for (int i = 4; i < list.Count - 4; i++)
                {
                    var s = list[i];
                    if (s.bridge) continue;
                    // posts on the outside of bends, every ~24 m
                    if (s.distance >= next)
                    {
                        next = s.distance + 24f;
                        float turn = list[i - 4].tangent.x * list[i + 4].tangent.y - list[i - 4].tangent.y * list[i + 4].tangent.x;
                        int side = turn > 0.02f ? -1 : turn < -0.02f ? 1 : (Chance(0.5f) ? 1 : -1);
                        Vector2 n = new Vector2(-s.tangent.y, s.tangent.x) * side;
                        Vector2 p = s.pos + n * (s.halfWidth + 1.3f);
                        if (ctx.EdgeDist(p) > 0.9f && ctx.Free(p, 0.5f)) ctx.Stamp(post, OnGround(p, -0.05f), YawOf(s.tangent));
                    }
                    if (name == RoadMain && s.distance >= nextStone)
                    {
                        nextStone = s.distance + 250f;
                        Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                        Vector2 p = s.pos + n * (s.halfWidth + 1.8f);
                        if (ctx.Free(p, 0.6f) && ctx.EdgeDist(p) > 1.2f)
                        {
                            ctx.Stamp(stone, OnGround(p, -0.1f), YawOf(s.tangent));
                            ctx.Box(OnGround(p, 0.4f), new Vector3(0.45f, 0.9f, 0.3f), YawOf(s.tangent));
                            ctx.Reserve(p, 0.6f);
                        }
                    }
                }
                if (Slice()) yield return null;
            }
            // highway: white posts both sides and a telegraph line on the west side
            var hw = Samples(RoadHighway);
            if (hw != null)
            {
                Vector3 wl, wr;
                var pole = CanyonBuildings.TelegraphPole(out wl, out wr);
                float next = 10f, nextPole = 15f;
                Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
                bool havePrev = false;
                var wires = new PropModel();
                var wg = wires[PK.Props];
                Vector3 wo = Vector3.zero;
                bool woSet = false;
                foreach (var s in hw)
                {
                    Vector2 n = new Vector2(-s.tangent.y, s.tangent.x);
                    if (s.distance >= next)
                    {
                        next = s.distance + 40f;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector2 p = s.pos + n * side * (s.halfWidth + 1.2f);
                            if (ctx.EdgeDist(p) > 0.8f) ctx.Stamp(post, OnGround(p, -0.05f), YawOf(s.tangent));
                        }
                    }
                    if (s.distance >= nextPole)
                    {
                        nextPole = s.distance + 46f;
                        Vector2 sideN = n.x > 0f ? -n : n;                // the west side
                        Vector2 p = s.pos + sideN * (s.halfWidth + 6f);
                        if (ctx.EdgeDist(p) < 4f) continue;
                        float yaw = YawOf(s.tangent) + 90f;
                        Vector3 bp = OnGround(p, 0f);
                        ctx.Stamp(pole, bp, yaw);
                        ctx.Capsule(bp - Vector3.up * 0.5f, 0.18f, 7.5f);
                        var q = Quaternion.Euler(0f, yaw, 0f);
                        Vector3 L = bp + q * wl, Rr = bp + q * wr;
                        if (!woSet) { wo = bp; woSet = true; }
                        if (havePrev)
                            for (int k = 0; k < 2; k++)
                            {
                                Vector3 a = k == 0 ? prevL : prevR, b = k == 0 ? L : Rr;
                                Vector3 last = a;
                                for (int j = 1; j <= 6; j++)
                                {
                                    float t = j / 6f;
                                    Vector3 pnt = Vector3.Lerp(a, b, t) - Vector3.up * (4f * t * (1f - t) * 0.9f);
                                    PG.Rod(wg, last - wo, pnt - wo, 0.018f, LifeGeo.C(50, 46, 44), 3);
                                    last = pnt;
                                }
                            }
                        prevL = L; prevR = Rr; havePrev = true;
                    }
                }
                if (woSet) ctx.Stamp(wires, Matrix4x4.Translate(wo));
            }
            yield return null;
            // signposts at the junctions
            Vector2[] signs =
            {
                WashPts[1] + new Vector2(14f, 8f), MainPts[MainPts.Length - 1] + new Vector2(12f, 14f), LoopPts[0] + new Vector2(-12f, 6f),
                LoopPts[LoopPts.Length - 1] + new Vector2(10f, -12f), HighwayPts[0] + new Vector2(6f, -14f), WashPts[WashPts.Length - 1] + new Vector2(12f, -8f),
                MainPts[1] + new Vector2(0f, 12f),
            };
            foreach (var sp in signs)
            {
                Vector2 p = sp;
                for (int k = 0; k < 6 && (ctx.EdgeDist(p) < 2.5f || !ctx.Free(p, 1f)); k++) p += (p - sp + new Vector2(1f, 1f)).normalized * 2.5f;
                if (ctx.EdgeDist(p) < 2.5f) continue;
                ctx.Stamp(CanyonBuildings.Signpost(rng), OnGround(p, 0f), Rand(0f, 360f));
                ctx.Capsule(OnGround(p, -0.3f), 0.12f, 3f);
                ctx.Reserve(p, 1f);
            }
            // spectator camps beside the fast sections (by road distance)
            Vector3[] camps = { new Vector3(0, 500f, 1), new Vector3(0, 1120f, -1), new Vector3(0, 1560f, 1), new Vector3(1, 400f, -1), new Vector3(2, 380f, 1), new Vector3(3, 260f, 1) };
            string[] campRoad = { RoadMain, RoadWash, RoadLoop, RoadDune };
            foreach (var cp in camps)
            {
                var list = Samples(campRoad[(int)cp.x]);
                if (list == null) continue;
                foreach (var s in list)
                {
                    if (s.distance < cp.y) continue;
                    Vector2 n = new Vector2(-s.tangent.y, s.tangent.x) * cp.z;
                    for (float o = s.halfWidth + 9f; o < s.halfWidth + 24f; o += 3f)
                    {
                        Vector2 p = s.pos + n * o;
                        if (ctx.EdgeDist(p) < 7f || !ctx.Free(p, 5f)) continue;
                        if (Mathf.Abs(Gr(p.x, p.y) - GroundMin(p, 4f)) > 0.8f) continue;
                        float yaw = YawOf(-n) + 90f;
                        ctx.Stamp(CanyonBuildings.SpectatorCamp(rng), OnGround(p, -0.05f), yaw);
                        var q = Quaternion.Euler(0f, yaw, 0f);
                        ctx.Box(OnGround(p, 1.2f) + q * new Vector3(-3f, 0f, 0f), new Vector3(2f, 2.4f, 4.6f), yaw);
                        ctx.Reserve(p, 6f);
                        break;
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ desert scatter: rocks, scrub, pebbles, cairns, bones
        IEnumerator BuildScatter()
        {
            var boulders = new PropModel[10];
            for (int i = 0; i < boulders.Length; i++) boulders[i] = CanyonKit.Boulder(rng, i, PK.Landmark);
            var small = new PropModel[6];
            for (int i = 0; i < small.Length; i++) small[i] = CanyonKit.Boulder(rng, i + 2, PK.Props);
            var saxaul = new PropModel[5];
            for (int i = 0; i < saxaul.Length; i++) saxaul[i] = CanyonKit.Saxaul(rng, Rand(0.9f, 1.4f));
            var tufts = new PropModel[6];
            for (int i = 0; i < tufts.Length; i++) tufts[i] = CanyonKit.Tuft(rng, Rand(0.6f, 1.1f), i % 2 == 0);
            var tam = new PropModel[3];
            for (int i = 0; i < tam.Length; i++) tam[i] = CanyonKit.Tamarisk(rng, Rand(0.8f, 1.1f));
            var pebbles = new PropModel[4];
            for (int i = 0; i < pebbles.Length; i++) pebbles[i] = CanyonKit.Pebbles(rng, i < 2);
            var cairnF = CanyonKit.Cairn(rng, true);
            var cairns = new PropModel[3];
            for (int i = 0; i < cairns.Length; i++) cairns[i] = CanyonKit.Cairn(rng, false);
            var bones = CanyonKit.Bones(rng);
            var poplarDead = CanyonKit.Poplar(rng, 1.1f, true);

            Rect rr = Rect;
            const float step = 8f;
            for (float z = rr.yMin + 30f; z < rr.yMax - 30f; z += step)
            {
                for (float x = rr.xMin + 30f; x < rr.xMax - 30f; x += step)
                {
                    Vector2 p = new Vector2(x + Rand(-3.5f, 3.5f), z + Rand(-3.5f, 3.5f));
                    float edge = ctx.EdgeDist(p);
                    if (edge < 1.6f) continue;
                    float so = OasisSdf(p.x, p.y);
                    if (so < 40f) continue;
                    float h = Gr(p.x, p.y);
                    float rel = h - Plane(p.x, p.y);
                    float dm = DuneMask(p.x, p.y);
                    float near = 1f - WorldNoise.Smooth(3f, 30f, edge);          // denser where it is seen
                    float slope = Mathf.Abs(Gr(p.x + 2f, p.y) - Gr(p.x - 2f, p.y)) + Mathf.Abs(Gr(p.x, p.y + 2f) - Gr(p.x, p.y - 2f));
                    float r01 = (float)rng.NextDouble();
                    bool wash = false;
                    if (x < -990f && x > -1300f && z > 670f && z < 1060f)
                    {
                        float al, tot;
                        wash = PolyDist(Wash, p.x, p.y, out al, out tot) < WashHalf * 0.8f;
                    }
                    if (dm > 0.5f)
                    {
                        if (r01 < 0.025f + 0.03f * near) { ctx.Stamp(tufts[rng.Next(tufts.Length)], OnGround(p, -0.15f), Rand(0f, 360f), Rand(0.7f, 1.2f)); nPlants++; }
                        else if (r01 < 0.03f + 0.03f * near && dm > 0.8f && Chance(0.05f)) { ctx.Stamp(bones, OnGround(p, -0.15f), Rand(0f, 360f)); }
                        continue;
                    }
                    Vector2 pw = WorldNoise.Warp(p.x, p.y, 170f, 26f, 611);
                    if (PlayaMask(pw.x, pw.y) > 0.6f)
                    {
                        if (r01 < 0.012f) { ctx.Stamp(small[rng.Next(small.Length)], OnGround(p, -0.2f), Rand(0f, 360f), Rand(0.3f, 0.6f)); nRocks++; }
                        continue;
                    }
                    if (wash)
                    {
                        if (r01 < 0.16f + 0.1f * near) { ctx.Stamp(pebbles[rng.Next(2)], OnGround(p, -0.08f), Rand(0f, 360f), Rand(0.8f, 1.5f)); }
                        else if (r01 < 0.21f + 0.06f * near) { ctx.Stamp(tam[rng.Next(tam.Length)], OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.6f, 1.1f)); nPlants++; }
                        else if (r01 < 0.24f) { ctx.Stamp(small[rng.Next(small.Length)], OnGround(p, -0.25f), Rand(0f, 360f), Rand(0.5f, 1.1f)); nRocks++; }
                        continue;
                    }
                    if (slope > 5f)
                    {
                        // talus: tumbled blocks at the foot of the cliffs
                        if (r01 < 0.05f && ctx.Free(p, 2f))
                        {
                            float sc = Rand(1.0f, 2.6f);
                            ctx.Stamp(boulders[rng.Next(boulders.Length)], new Vector3(p.x, GroundMin(p, sc) - 0.3f * sc, p.y), Rand(0f, 360f), sc);
                            ctx.Reserve(p, sc);
                            if (edge < 12f) ctx.Box(OnGround(p, 0.4f * sc), new Vector3(1.6f * sc, 1.1f * sc, 1.6f * sc), Rand(0f, 90f));
                            nRocks++;
                        }
                        continue;
                    }
                    bool top = rel > 10f;
                    float pSax = top ? 0.05f : 0.035f + 0.05f * near;
                    float pTuft = 0.06f + 0.1f * near;
                    float pRock = 0.025f + 0.05f * near;
                    float pPeb = 0.03f + 0.06f * near;
                    if (r01 < pSax)
                    {
                        if (!ctx.Free(p, 1.6f)) continue;
                        float sc = Rand(0.7f, 1.25f);
                        ctx.Stamp(saxaul[rng.Next(saxaul.Length)], OnGround(p, -0.1f), Rand(0f, 360f), sc);
                        ctx.Reserve(p, 1.4f);
                        if (edge < 6f && sc > 1f) ctx.Capsule(OnGround(p, -0.3f), 0.18f, 1.8f);
                        nPlants++;
                    }
                    else if (r01 < pSax + pTuft)
                    {
                        ctx.Stamp(tufts[rng.Next(tufts.Length)], OnGround(p, -0.12f), Rand(0f, 360f), Rand(0.6f, 1.2f));
                        nPlants++;
                    }
                    else if (r01 < pSax + pTuft + pRock)
                    {
                        if (!ctx.Free(p, 1.5f)) continue;
                        float sc = edge < 5f ? Rand(0.4f, 0.8f) : Rand(0.5f, 1.6f);
                        bool big = sc > 0.9f;
                        ctx.Stamp(big ? boulders[rng.Next(boulders.Length)] : small[rng.Next(small.Length)], new Vector3(p.x, GroundMin(p, sc) - 0.25f * sc, p.y), Rand(0f, 360f), sc);
                        ctx.Reserve(p, sc * 1.1f);
                        if (big && edge < 14f) ctx.Box(OnGround(p, 0.35f * sc), new Vector3(1.5f * sc, 1f * sc, 1.5f * sc), Rand(0f, 90f));
                        nRocks++;
                    }
                    else if (r01 < pSax + pTuft + pRock + pPeb)
                        ctx.Stamp(pebbles[2 + rng.Next(2)], OnGround(p, -0.06f), Rand(0f, 360f), Rand(0.7f, 1.3f));
                    else if (r01 < pSax + pTuft + pRock + pPeb + 0.004f + 0.01f * near && edge > 3f && ctx.Free(p, 1.2f))
                    {
                        ctx.Stamp(Chance(0.3f) ? cairnF : cairns[rng.Next(cairns.Length)], OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.8f, 1.2f));
                        ctx.Reserve(p, 1f);
                    }
                    else if (r01 < pSax + pTuft + pRock + pPeb + 0.016f + 0.01f * near && edge > 5f && Chance(0.08f))
                        ctx.Stamp(bones, OnGround(p, -0.15f), Rand(0f, 360f), Rand(0.9f, 1.1f));
                    else if (r01 > 0.9985f && edge > 8f && ctx.Free(p, 3f))
                    {
                        // a lone, half-dead poplar on the open plain
                        ctx.Stamp(poplarDead, OnGround(p, -0.1f), Rand(0f, 360f), Rand(0.7f, 0.9f));
                        ctx.Reserve(p, 3f);
                        if (edge < 30f) ctx.Capsule(OnGround(p, -0.4f), 0.35f, 2.5f);
                        nPlants++;
                    }
                }
                if (Slice()) yield return null;
            }
        }
    }
}
