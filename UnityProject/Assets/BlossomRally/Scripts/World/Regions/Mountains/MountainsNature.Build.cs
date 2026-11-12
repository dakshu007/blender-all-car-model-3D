// MountainsNature.Build.cs - the Cloud Peak landscape dressing: granite pillars and spires (the tallest on the
// skyline) with pines growing from their cracks, pine forests with red / gold maples and boulders, the gorge
// stream with its cascades, the valley falls and plunge pool, small cliff waterfalls, the jade lake, plank
// walkways, rope bridges and stone steps, and the cloud sea that fills the deep valleys.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class MountainsRegion
    {
        struct PillarInfo { public Vector3 basePos, top; public float H, R, taper; public bool sky; }
        readonly List<PillarInfo> pillarList = new List<PillarInfo>();
        readonly List<Vector3> sprayPts = new List<Vector3>();      // white-water spray sources (FX)
        readonly List<Vector3> mapleTrees = new List<Vector3>();     // falling-leaf sources (FX)
        PropModel[] cliffPines;
        Material lakeMat;

        float SlopeAround(Vector2 p, float r)
        {
            float gx = Gnd(p.x + r, p.y) - Gnd(p.x - r, p.y), gz = Gnd(p.x, p.y + r) - Gnd(p.x, p.y - r);
            return Mathf.Sqrt(gx * gx + gz * gz) / (2f * r);
        }

        static float LakeK(Vector2 p)
        {
            float lx = (p.x - MountainsMap.LakeC.x) / MountainsMap.LakeR.x, lz = (p.y - MountainsMap.LakeC.y) / MountainsMap.LakeR.y;
            return Mathf.Sqrt(lx * lx + lz * lz);
        }

        static float StreamD(Vector2 p)
        {
            float h, t;
            return MountainsMap.StreamDistance(p.x, p.y, out h, out t);
        }

        bool NearMonastery(Vector2 p, float r) { return (p - MountainsMap.Monastery - new Vector2(10f, 30f)).sqrMagnitude < r * r; }

        // ------------------------------------------------------------------ water
        IEnumerator BuildWater()
        {
            // ---- jade lake (own material: jade shallows, deep teal, sky reflections from the probe)
            {
                const int seg = 36;
                float[] ks = { 0f, 0.4f, 0.75f, 1.06f };
                var v = new List<Vector3>();
                var t = new List<int>();
                for (int r = 0; r < ks.Length; r++)
                {
                    if (r == 0) { v.Add(new Vector3(MountainsMap.LakeC.x, MountainsMap.LakeY, MountainsMap.LakeC.y)); continue; }
                    for (int k = 0; k < seg; k++)
                    {
                        float a = k * Mathf.PI * 2f / seg;
                        v.Add(new Vector3(MountainsMap.LakeC.x + Mathf.Cos(a) * MountainsMap.LakeR.x * ks[r], MountainsMap.LakeY, MountainsMap.LakeC.y + Mathf.Sin(a) * MountainsMap.LakeR.y * ks[r]));
                    }
                }
                for (int k = 0; k < seg; k++) { t.Add(0); t.Add(1 + (k + 1) % seg); t.Add(1 + k); }
                for (int r = 1; r < ks.Length - 1; r++)
                {
                    int b0 = 1 + (r - 1) * seg, b1 = 1 + r * seg;
                    for (int k = 0; k < seg; k++)
                    {
                        int k1 = (k + 1) % seg;
                        t.Add(b0 + k); t.Add(b0 + k1); t.Add(b1 + k1);
                        t.Add(b0 + k); t.Add(b1 + k1); t.Add(b1 + k);
                    }
                }
                var mesh = new Mesh { name = "Mtn_JadeLake" };
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                var nrm = new Vector3[v.Count];
                for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
                mesh.normals = nrm;
                mesh.RecalculateBounds();
                lakeMat = new Material(MaterialLibrary.Water) { name = "BR_MtnJadeLake" };
                lakeMat.SetColor("_Shallow", new Color(0.42f, 0.80f, 0.70f, 0.74f));
                lakeMat.SetColor("_Deep", new Color(0.07f, 0.40f, 0.38f, 0.93f));
                lakeMat.SetFloat("_RippleScale", 0.45f);
                lakeMat.SetFloat("_RippleSpeed", 0.3f);
                ctx.AddMesh("Mtn_JadeLake", mesh, lakeMat, 0, false);
                ctx.Reserve(MountainsMap.LakeC, MountainsMap.LakeR.y);
                // shore boulders and reeds
                var rocks = new PropModel();
                var rg = rocks[PK.Props];
                var bb = ctx.Meshes.Get(PK.Blades, V3(MountainsMap.LakeC, MountainsMap.LakeY));
                for (int k = 0; k < 44; k++)
                {
                    float a = k * Mathf.PI * 2f / 44f + Rand(-0.05f, 0.05f);
                    float kk = Rand(1.02f, 1.16f);
                    Vector2 p = MountainsMap.LakeC + new Vector2(Mathf.Cos(a) * MountainsMap.LakeR.x * kk, Mathf.Sin(a) * MountainsMap.LakeR.y * kk);
                    if (ctx.EdgeDist(p) < 2f) continue;
                    if (k % 3 == 0)
                        MtnShapes.Outcrop(rg, V3(p, Gnd(p) + 0.1f), new Vector3(Rand(0.6f, 1.4f), Rand(0.4f, 0.8f), Rand(0.6f, 1.2f)), rng, Rand(0f, 0.5f));
                    else if (k % 3 == 1)
                        for (int q = 0; q < 22; q++)
                        {
                            Vector2 rp = p + new Vector2(Rand(-1.6f, 1.6f), Rand(-1.6f, 1.6f));
                            AddBlade(bb, V3(rp, Mathf.Min(Gnd(rp), MountainsMap.LakeY + 0.2f) - 0.1f), Rand(0.9f, 1.6f), 0.07f, PG.C(80, 112, 60), q % 5 == 0 ? PG.C(170, 140, 90) : PG.C(150, 186, 96));
                        }
                }
                StampWorld(rocks, V3(MountainsMap.LakeC, MountainsMap.LakeY));
            }
            yield return null;

            // ---- gorge stream: lake outlet -> cascades under the bridge -> gorge -> valley falls
            {
                var pts = new List<Vector2>();
                int n0 = MountainsMap.Stream.Length;
                for (int i = 0; i < n0 - 1; i++)
                {
                    Vector3 a3 = MountainsMap.Stream[Mathf.Max(0, i - 1)], b3 = MountainsMap.Stream[i], c3 = MountainsMap.Stream[i + 1], d3 = MountainsMap.Stream[Mathf.Min(n0 - 1, i + 2)];
                    Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), c = new Vector2(c3.x, c3.z), d = new Vector2(d3.x, d3.z);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(b, c) / 1.5f));
                    for (int k = 0; k < steps; k++) pts.Add(Spline.CatmullRom(a, b, c, d, k / (float)steps));
                }
                pts.Add(new Vector2(MountainsMap.Stream[n0 - 1].x, MountainsMap.Stream[n0 - 1].z));
                Ribbon(pts, 0, pts.Count, 1.3f, 2.6f, true, rocks: true);
            }
            yield return null;

            // ---- plunge pool under the valley falls + big flanking boulders
            {
                Vector2 pc = MountainsMap.PoolC + new Vector2(0f, 1.5f);
                var wb = ctx.Meshes.Get(PK.Water, V3(pc, 0f));
                const int seg = 20;
                float y = WorldField.WaterY + 0.015f;
                int c0 = wb.Vert(V3(pc, y), Vector3.up, new Color32(255, 255, 255, 255), new Vector4(pc.x * 0.05f, pc.y * 0.05f, 0f, 0f));
                for (int k = 0; k < seg; k++)
                {
                    float a = k * Mathf.PI * 2f / seg;
                    Vector2 q = pc + new Vector2(Mathf.Cos(a) * 9.5f, Mathf.Sin(a) * 8f);
                    wb.Vert(V3(q, y), Vector3.up, new Color32(255, 255, 255, 255), new Vector4(q.x * 0.05f, q.y * 0.05f, 0f, 0f));
                }
                for (int k = 0; k < seg; k++) wb.Tri(c0, c0 + 1 + (k + 1) % seg, c0 + 1 + k);
                var rocks = new PropModel();
                var rg = rocks[PK.Props];
                Vector2[] big = { new Vector2(-40f, 620f), new Vector2(-21f, 624f), new Vector2(-42f, 606f), new Vector2(-16f, 609f), new Vector2(-38f, 588f), new Vector2(-14f, 592f) };
                foreach (var b in big)
                {
                    if (ctx.EdgeDist(b) < 3f) continue;
                    MtnShapes.Outcrop(rg, V3(b, Gnd(b) + 0.4f), new Vector3(Rand(2.2f, 3.6f), Rand(1.6f, 2.8f), Rand(2f, 3.2f)), rng, Rand(0f, 0.4f));
                }
                StampWorld(rocks, V3(pc, 0f));
                sprayPts.Add(new Vector3(-29f, 0.5f, 605f));
                sprayPts.Add(new Vector3(-30.5f, 6.5f, 617f));
            }
            yield return null;

            // ---- small cliff waterfalls feeding the gorge, the lake and the basin
            Vector2[] seeds =
            {
                new Vector2(22f, 690f), new Vector2(28f, 770f), new Vector2(40f, 836f), new Vector2(-82f, 790f),
                new Vector2(176f, 1150f), new Vector2(-440f, 1210f), new Vector2(205f, 880f), new Vector2(360f, 760f)
            };
            int falls = 0;
            foreach (var sd in seeds)
            {
                // start from the highest point near the seed
                Vector2 best = sd;
                float bh = Gnd(sd);
                for (int k = 0; k < 16; k++)
                {
                    Vector2 q = sd + new Vector2(Mathf.Cos(k * 0.39f), Mathf.Sin(k * 0.39f)) * (6f + (k & 3) * 3f);
                    float h = Gnd(q);
                    if (h > bh && ctx.EdgeDist(q) > 10f) { bh = h; best = q; }
                }
                var path = TraceDownhill(best);
                if (path == null) continue;
                Ribbon(path, 0, path.Count, 0.55f, 1.1f, false, rocks: false);
                sprayPts.Add(V3(path[path.Count - 1], Gnd(path[path.Count - 1]) + 0.3f));
                falls++;
                if (Slice()) yield return null;
            }
        }

        /// <summary>Steepest-descent path from p until the slope flattens, the stream / lake is reached, or a road is near.</summary>
        List<Vector2> TraceDownhill(Vector2 p)
        {
            var pts = new List<Vector2>();
            float startH = Gnd(p);
            int flat = 0;
            Vector2 dir = Vector2.zero;
            for (int step = 0; step < 90; step++)
            {
                if (ctx.EdgeDist(p) < 7f || !ctx.InRegion(p, 4f)) break;
                pts.Add(p);
                if (StreamD(p) < 3f || LakeK(p) < 0.98f) break;
                float gx = Gnd(p.x + 1.2f, p.y) - Gnd(p.x - 1.2f, p.y), gz = Gnd(p.x, p.y + 1.2f) - Gnd(p.x, p.y - 1.2f);
                Vector2 grad = new Vector2(gx, gz) / 2.4f;
                float gm = grad.magnitude;
                if (gm < 0.18f) { if (++flat > 4) break; } else flat = 0;
                Vector2 down = gm > 1e-4f ? -grad / gm : dir;
                dir = dir == Vector2.zero ? down : (dir * 0.45f + down * 0.55f).normalized;
                p += dir * 1.5f;
            }
            if (pts.Count < 8) return null;
            if (startH - Gnd(pts[pts.Count - 1]) < 9f) return null;
            return pts;
        }

        /// <summary>FlowFoam ribbon along a polyline: water colour on gentle stretches, white foam where it drops.</summary>
        void Ribbon(List<Vector2> path, int from, int to, float hw0, float hw1, bool main, bool rocks)
        {
            int n = to - from;
            if (n < 2) return;
            var sb = ctx.Meshes.Get(PK.Stream, V3(path[from], 0f));
            var rm = new PropModel();
            var rg = rm[PK.Props];
            var ys = new float[n];
            var nr = new Vector2[n];
            var hws = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = path[from + i];
                Vector2 t = (path[from + Mathf.Min(n - 1, i + 1)] - path[from + Mathf.Max(0, i - 1)]).normalized;
                nr[i] = new Vector2(t.y, -t.x);
                float k = i / (float)(n - 1);
                hws[i] = Mathf.Lerp(hw0, hw1, k) * (1f + 0.15f * Mathf.Sin(i * 0.37f));
                Vector2 l = p - nr[i] * hws[i], r = p + nr[i] * hws[i];
                ys[i] = Mathf.Max(Gnd(p), Mathf.Max(Gnd(l), Gnd(r)) - 0.15f) + 0.08f;
            }
            // monotonic downstream (no water flowing uphill over a bump)
            for (int i = 1; i < n; i++) ys[i] = Mathf.Min(ys[i], ys[i - 1] + 0.02f);
            float dist = 0f;
            int prev = -1;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = path[from + i];
                if (i > 0) dist += Vector2.Distance(p, path[from + i - 1]);
                float drop = 0f;
                for (int k = -2; k <= 2; k++)
                {
                    int a = Mathf.Clamp(i + k, 0, n - 1), b = Mathf.Clamp(i + k + 1, 0, n - 1);
                    drop = Mathf.Max(drop, ys[a] - ys[b]);
                }
                float foam = Mathf.Clamp01((drop - 0.25f) / 0.5f);
                byte ea = (byte)Mathf.Lerp(90f, 225f, foam), ma = (byte)Mathf.Lerp(205f, 250f, foam);
                Color32 edge = new Color32(255, 255, 255, ea), mid = new Color32(255, 255, 255, ma);
                Vector2 l = p - nr[i] * hws[i], r = p + nr[i] * hws[i];
                float v = dist * 0.5f;
                float lift = foam * 0.18f;
                int a0 = sb.Vert(V3(l, ys[i] + lift * 0.5f), Vector3.up, edge, new Vector4(0f, v, 0f, 0f));
                sb.Vert(V3(p, ys[i] + lift), Vector3.up, mid, new Vector4(0.5f, v, 0f, 0f));
                sb.Vert(V3(r, ys[i] + lift * 0.5f), Vector3.up, edge, new Vector4(1f, v, 0f, 0f));
                if (prev >= 0)
                {
                    sb.Tri(prev, a0, a0 + 1); sb.Tri(prev, a0 + 1, prev + 1);
                    sb.Tri(prev + 1, a0 + 1, a0 + 2); sb.Tri(prev + 1, a0 + 2, prev + 2);
                }
                prev = a0;
                if (rocks && i % 4 == 2)
                {
                    float side = (i & 4) == 0 ? 1f : -1f;
                    Vector2 bp = p + nr[i] * side * (hws[i] + Rand(0.2f, 0.9f));
                    float sc = foam > 0.4f ? Rand(0.6f, 1.2f) : Rand(0.25f, 0.6f);
                    PG.Boulder(rg, V3(bp, Gnd(bp) + sc * 0.2f), new Vector3(sc * Rand(0.9f, 1.4f), sc * 0.7f, sc), (i & 1) == 0 ? MtnShapes.GraniteL : Pal.Stone, MtnShapes.GraniteD, i);
                }
                if (main && foam > 0.6f && i % 6 == 0) sprayPts.Add(V3(p, ys[i] + 0.4f));
            }
            if (rocks) StampWorld(rm, V3(path[from + n / 2], ys[n / 2]));
        }

        void AddBlade(PropBucket b, Vector3 root, float h, float w, Color32 cRoot, Color32 cTip)
        {
            float yaw = Rand(0f, Mathf.PI * 2f);
            Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (w * 0.5f);
            Vector3 lean = new Vector3(Rand(-0.25f, 0.25f), 0f, Rand(-0.25f, 0.25f)) * h;
            Vector3 nrm = (Vector3.up + lean * 0.4f).normalized;
            Vector4 uv = new Vector4(root.x, root.y, root.z, (float)rng.NextDouble());
            cRoot.a = 0; cTip.a = 255;
            int i0 = b.Vert(root - side, nrm, cRoot, uv);
            b.Vert(root + lean + Vector3.up * h, nrm, cTip, uv);
            b.Vert(root + side, nrm, cRoot, uv);
            b.Tri(i0, i0 + 1, i0 + 2);
        }

        // ------------------------------------------------------------------ granite pillars
        IEnumerator BuildPillars()
        {
            cliffPines = new PropModel[8];
            for (int i = 0; i < cliffPines.Length; i++) cliffPines[i] = MtnShapes.CliffPine(rng, 1f);
            var near = new PropModel();
            var ng = near[PK.Karst];
            var sky = new PropModel();
            var sg = sky[PK.Karst];
            // skyline giants: the spires everyone sees from the other regions
            Vector2[] giants =
            {
                new Vector2(292f, 1452f), new Vector2(150f, 1462f), new Vector2(330f, 1330f), new Vector2(-330f, 1360f), new Vector2(-452f, 1270f),
                new Vector2(-390f, 1440f), new Vector2(400f, 720f), new Vector2(210f, 600f), new Vector2(120f, 760f), new Vector2(430f, 1000f),
                new Vector2(-240f, 1400f), new Vector2(40f, 1460f)
            };
            foreach (var gp in giants)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    Vector2 p = gp + (tries == 0 ? Vector2.zero : new Vector2(Rand(-30f, 30f), Rand(-30f, 30f)));
                    if (TryPillar(sg, p, Rand(72f, 110f), Rand(7.5f, 10.5f), true, 3)) break;
                }
            }
            yield return null;
            // granite towers on the cellular bumps of the terrain, each with a few satellites
            for (int cz = 3; cz <= 17; cz++)
            {
                for (int cx = -7; cx <= 6; cx++)
                {
                    Vector2 p = MountainsMap.TowerPoint(cx, cz);
                    float rug = MountainsMap.Rugged(p.x, p.y);
                    if (rug < 0.5f) continue;
                    float H = Mathf.Lerp(26f, 70f, (rug - 0.5f) * 2f) * Rand(0.7f, 1.35f);
                    float R = H * Rand(0.075f, 0.11f) + 2.2f;
                    if (!TryPillar(ng, p, H, R, false, 2)) continue;
                    int sats = 1 + rng.Next(3);
                    for (int k = 0; k < sats; k++)
                    {
                        float a = Rand(0f, Mathf.PI * 2f);
                        Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * Rand(1.5f, 2.6f);
                        float h2 = H * Rand(0.4f, 0.75f);
                        TryPillar(ng, q, h2, h2 * Rand(0.09f, 0.13f) + 2f, false, 1);
                    }
                }
                if (Slice()) yield return null;
            }
            // pillars on steep ground everywhere (cliffs read as granite)
            for (float z = 512f; z < 1500f; z += 22f)
            {
                for (float x = -490f; x < 500f; x += 22f)
                {
                    Vector2 p = new Vector2(x + Rand(-8f, 8f), z + Rand(-8f, 8f));
                    float rug = MountainsMap.Rugged(p.x, p.y);
                    if (rug < 0.28f) continue;
                    float s = SlopeAround(p, 4f);
                    if (s < 0.85f || !Chance(0.45f)) continue;
                    float H = Rand(12f, 22f) + rug * Rand(4f, 16f);
                    TryPillar(ng, p, H, H * Rand(0.1f, 0.15f) + 1.8f, false, 1);
                }
                if (Slice()) yield return null;
            }
            // the gorge walls: tall granite on both sides of the stream (mostly the east wall)
            for (int i = 0; i < MountainsMap.Stream.Length - 1; i++)
            {
                Vector3 a = MountainsMap.Stream[i], b = MountainsMap.Stream[i + 1];
                Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z);
                float len = Vector2.Distance(a2, b2);
                Vector2 t = (b2 - a2) / Mathf.Max(0.01f, len);
                Vector2 nn = new Vector2(t.y, -t.x);
                for (float d = 0f; d < len; d += 16f)
                {
                    Vector2 c = a2 + t * d;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 east = new Vector2(1f, 0f);
                        bool eastSide = Vector2.Dot(nn * side, east) > 0f;
                        if (!Chance(eastSide ? 0.7f : 0.3f)) continue;
                        Vector2 p = c + nn * side * Rand(17f, 32f);
                        float H = Rand(16f, 38f) * (eastSide ? 1.2f : 0.8f);
                        TryPillar(ng, p, H, H * Rand(0.09f, 0.13f) + 2f, false, 1);
                    }
                }
            }
            StampWorld(near, new Vector3(0f, 80f, 1000f));
            StampWorldSkyline(sky, new Vector3(0f, 80f, 1000f));
            yield return null;
        }

        bool TryPillar(LifeGeo g, Vector2 p, float H, float R, bool skyline, int pines)
        {
            if (!ctx.InRegion(p, -40f)) return false;
            if (WorldLayout.OutsideDistance(p) > 30f) return false;
            float roadH;
            float e = RoadEdge(p, out roadH);
            if (e < R * 1.25f + 5f) return false;
            if (StreamD(p) < R + 6f || LakeK(p) < 1.6f) return false;
            if (Vector2.Distance(p, MountainsMap.PoolC) < R + 18f) return false;
            if (NearMonastery(p, 70f + R)) return false;
            if (ctx.Field.OnPad(p)) return false;
            if (!ctx.Free(p, R * 0.75f)) return false;
            float gmin = Gnd(p);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                gmin = Mathf.Min(gmin, Gnd(p.x + Mathf.Cos(a) * R, p.y + Mathf.Sin(a) * R));
            }
            // never let a pillar foot hang over a road below (it would block the view / look odd)
            if (e < 40f && gmin < roadH + 2f && e < R * 2f + 8f) return false;
            float lift = Gnd(p) - gmin;
            Vector3 b = V3(p, gmin - 2.5f);
            float taper;
            Vector3 top = MtnShapes.Pillar(g, b, H + lift + 2.5f, R, rng, Rand(0f, 0.6f), out taper);
            ctx.Reserve(p, R);
            pillarList.Add(new PillarInfo { basePos = b, top = top, H = H + lift + 2.5f, R = R, taper = taper, sky = skyline });
            // pines growing from the crown and from cracks in the walls
            for (int k = 0; k < pines; k++)
            {
                var pm = cliffPines[rng.Next(cliffPines.Length)];
                Vector3 pos;
                float sc;
                if (k == 0) { pos = top - Vector3.up * 0.4f + new Vector3(Rand(-0.3f, 0.3f), 0f, Rand(-0.3f, 0.3f)) * R; sc = Rand(0.9f, 1.35f); }
                else
                {
                    float t = Rand(0.45f, 0.85f), a = Rand(0f, Mathf.PI * 2f);
                    float rr = R * Mathf.Lerp(1f, taper, t) * 0.8f;
                    pos = b + Vector3.up * ((H + lift) * t) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr;
                    sc = Rand(0.55f, 0.85f);
                }
                if (skyline) ctx.StampSkyline(pm, pos, Rand(0f, 360f), sc);
                else ctx.Stamp(pm, pos, Rand(0f, 360f), sc);
            }
            if (e < 30f) ctx.Capsule(V3(p, gmin - 1f), R * 0.85f, Mathf.Min(H, 14f));
            return true;
        }

        // ------------------------------------------------------------------ forests
        IEnumerator BuildForest()
        {
            var pines = new PropModel[12];
            for (int i = 0; i < pines.Length; i++) pines[i] = MtnShapes.ForestPine(rng, i % 3);
            var maples = new PropModel[9];
            for (int i = 0; i < maples.Length; i++) maples[i] = MtnShapes.Maple(rng, i % 3);
            var stones = new PropModel[5];
            for (int i = 0; i < stones.Length; i++)
            {
                stones[i] = new PropModel();
                var g = stones[i][PK.Props];
                int k = 1 + rng.Next(3);
                for (int q = 0; q < k; q++)
                    MtnShapes.Outcrop(g, new Vector3(Rand(-1.2f, 1.2f), Rand(0f, 0.3f), Rand(-1.2f, 1.2f)), new Vector3(Rand(0.6f, 1.5f), Rand(0.45f, 1f), Rand(0.6f, 1.4f)), rng, Rand(0f, 0.5f));
            }
            int nPine = 0, nMaple = 0, nStone = 0, nCliff = 0;
            const float cell = 9.5f;
            for (float z = 500f + cell * 0.5f; z < 1500f; z += cell)
            {
                for (float x = -500f + cell * 0.5f; x < 500f; x += cell)
                {
                    Vector2 p = new Vector2(x + Rand(-4f, 4f), z + Rand(-4f, 4f));
                    if (!ctx.InRegion(p, 1.5f)) continue;
                    float e = ctx.EdgeDist(p);
                    if (e < 2.6f) continue;
                    if (StreamD(p) < 7f || LakeK(p) < 1.22f || Vector2.Distance(p, MountainsMap.PoolC) < 18f) continue;
                    if (ctx.Field.OnPad(p)) continue;
                    if (PathDistance(p.x, p.y) < 2.2f) continue;
                    if (!ctx.Free(p, 1.6f)) continue;
                    float s = SlopeAround(p, 2.5f);
                    float forest = MountainsMap.Forest(p.x, p.y);
                    float maple = MapleK(p.x, p.y);
                    float rug = MountainsMap.Rugged(p.x, p.y);
                    float y = Gnd(p);
                    bool mona = NearMonastery(p, 85f);
                    if (s > 1.25f)
                    {
                        // cliff faces: an occasional pine clinging to a crack
                        if (Chance(0.07f) && !mona)
                        {
                            ctx.Stamp(cliffPines[rng.Next(cliffPines.Length)], V3(p, y - 0.4f), Rand(0f, 360f), Rand(0.6f, 1f));
                            nCliff++;
                        }
                        continue;
                    }
                    if (maple > 0.12f && Chance(0.25f + 0.6f * maple))
                    {
                        ctx.Stamp(maples[rng.Next(maples.Length)], V3(p, y - 0.1f), Rand(0f, 360f), Rand(0.85f, 1.25f));
                        mapleTrees.Add(V3(p, y + 4.5f));
                        if (e < 14f) ctx.Capsule(V3(p, y), 0.22f, 3.5f);
                        ctx.Reserve(p, 2.2f);
                        nMaple++;
                        continue;
                    }
                    float prob = 0.035f + 0.8f * Mathf.Pow(forest, 1.7f);
                    if (s > 0.7f) prob *= 0.55f;
                    if (e < 9f) prob *= 0.55f;
                    if (mona) prob *= 0.25f;
                    if (y > 175f) prob *= 0.5f;
                    if (Chance(prob))
                    {
                        float sc = Rand(0.78f, 1.3f) * (forest > 0.6f ? 1.08f : 0.95f);
                        ctx.Stamp(pines[rng.Next(pines.Length)], V3(p, y - 0.15f), Rand(0f, 360f), sc);
                        if (e < 15f) ctx.Capsule(V3(p, y), 0.26f * sc, 4f);
                        ctx.Reserve(p, 1.8f);
                        nPine++;
                    }
                    else if (Chance(0.025f + rug * 0.05f) && e > 3.5f)
                    {
                        float sc = Rand(0.7f, 1.6f);
                        ctx.Stamp(stones[rng.Next(stones.Length)], V3(p, y - 0.2f * sc), Rand(0f, 360f), sc);
                        if (e < 10f) ctx.Box(V3(p, y + 0.5f), new Vector3(2.2f * sc, 1.2f * sc, 2.2f * sc), 0f);
                        nStone++;
                    }
                }
                if (Slice()) yield return null;
            }
            Debug.Log("[BlossomRally] Mountains forest: " + nPine + " pines, " + nMaple + " maples, " + nCliff + " cliff pines, " + nStone + " boulder groups, " + pillarList.Count + " granite pillars");
        }

        // ------------------------------------------------------------------ plank walkways, rope bridges, stone steps
        IEnumerator BuildWalkways()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 plank = PG.C(150, 112, 74), plankD = PG.C(118, 86, 58), rope = PG.C(176, 150, 104), postC = PG.C(96, 70, 50), chain = PG.C(70, 70, 74);
            // ---- plank walkways (栈道) clinging to big pillars that can be seen from a road
            int walks = 0;
            foreach (var pi in pillarList)
            {
                if (walks >= 4 || pi.sky || pi.H < 30f) continue;
                Vector2 p = new Vector2(pi.basePos.x, pi.basePos.z);
                float e = ctx.EdgeDist(p);
                if (e > 90f || e < 14f) continue;
                float t0 = Rand(0.3f, 0.45f);
                float a0 = Rand(0f, Mathf.PI * 2f), sweep = Rand(3.2f, 4.4f);
                const int N = 46;
                Vector3 prevOut = Vector3.zero, prevIn = Vector3.zero;
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N;
                    float t = t0 + u * 0.12f;
                    float a = a0 + u * sweep;
                    float rr = pi.R * Mathf.Lerp(1f, pi.taper, t) * 1.13f;
                    Vector3 c = pi.basePos + Vector3.up * (pi.H * t);
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 pin = c + d * rr, pout = c + d * (rr + 1.15f);
                    Vector3 tang = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    PG.OBox(g, (pin + pout) * 0.5f, new Vector3(0.6f, 0.05f, 0.16f), Quaternion.LookRotation(tang, Vector3.up) * Quaternion.Euler(0f, 90f, 0f), (i & 1) == 0 ? plank : plankD);
                    if (i % 3 == 0)
                    {
                        PG.Beam(g, pin - Vector3.up * 0.05f, pin - d * 0.6f - Vector3.up * 0.8f, 0.05f, 0.05f, postC);    // bracket into the rock
                        PG.Rod(g, pout, pout + Vector3.up * 1.0f, 0.035f, postC, 4);
                        if (i > 0) PG.Rod(g, prevOut + Vector3.up * 0.95f, pout + Vector3.up * 0.95f, 0.02f, chain, 3);
                        prevOut = pout;
                    }
                }
                walks++;
            }
            yield return null;
            // ---- rope bridges between neighbouring pillar crowns (never over a road)
            int bridges = 0;
            for (int i = 0; i < pillarList.Count && bridges < 3; i++)
                for (int j = i + 1; j < pillarList.Count && bridges < 3; j++)
                {
                    var A = pillarList[i]; var B = pillarList[j];
                    if (A.sky || B.sky) continue;
                    Vector3 ta = A.top - Vector3.up * 1.2f, tb = B.top - Vector3.up * 1.2f;
                    float span = Vector2.Distance(new Vector2(ta.x, ta.z), new Vector2(tb.x, tb.z));
                    if (span < 16f || span > 42f || Mathf.Abs(ta.y - tb.y) > 7f || A.H < 22f || B.H < 22f) continue;
                    bool clear = true;
                    for (int k = 1; k < 8 && clear; k++)
                    {
                        Vector3 q = Vector3.Lerp(ta, tb, k / 8f);
                        float rh;
                        if (RoadEdge(new Vector2(q.x, q.z), out rh) < 18f) clear = false;
                    }
                    if (!clear) continue;
                    // shorten to the crown edges
                    Vector3 dir = (tb - ta); dir.y = 0f; dir.Normalize();
                    ta += dir * A.R * A.taper * 0.7f; tb -= dir * B.R * B.taper * 0.7f;
                    Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                    float L = Vector2.Distance(new Vector2(ta.x, ta.z), new Vector2(tb.x, tb.z));
                    int planks = Mathf.CeilToInt(L / 0.45f);
                    float sag = L * 0.07f;
                    for (int k = 0; k <= planks; k++)
                    {
                        float u = k / (float)planks;
                        Vector3 c = Vector3.Lerp(ta, tb, u) - Vector3.up * (4f * sag * u * (1f - u));
                        PG.OBox(g, c, new Vector3(0.55f, 0.04f, 0.13f), Quaternion.LookRotation(dir, Vector3.up), (k & 1) == 0 ? plank : plankD);
                        if (k % 4 == 0)
                            for (int s = -1; s <= 1; s += 2) PG.Rod(g, c + side * s * 0.55f, c + side * s * 0.6f + Vector3.up * 0.95f, 0.015f, rope, 3);
                    }
                    for (int s = -1; s <= 1; s += 2)
                        for (int k = 0; k < 10; k++)
                        {
                            float u0 = k / 10f, u1 = (k + 1) / 10f;
                            Vector3 c0 = Vector3.Lerp(ta, tb, u0) - Vector3.up * (4f * sag * 0.8f * u0 * (1f - u0)) + side * s * 0.62f + Vector3.up * 1.0f;
                            Vector3 c1 = Vector3.Lerp(ta, tb, u1) - Vector3.up * (4f * sag * 0.8f * u1 * (1f - u1)) + side * s * 0.62f + Vector3.up * 1.0f;
                            PG.Rod(g, c0, c1, 0.03f, rope, 3);
                        }
                    for (int s = -1; s <= 1; s += 2)
                    {
                        PG.Rod(g, ta + side * s * 0.65f - Vector3.up * 0.3f, ta + side * s * 0.65f + Vector3.up * 1.2f, 0.07f, postC, 4);
                        PG.Rod(g, tb + side * s * 0.65f - Vector3.up * 0.3f, tb + side * s * 0.65f + Vector3.up * 1.2f, 0.07f, postC, 4);
                    }
                    bridges++;
                }
            // ---- stone steps on the foot paths
            foreach (var path in Paths)
            {
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a = path[i], b = path[i + 1];
                    float len = Vector2.Distance(a, b);
                    Vector2 t = (b - a) / Mathf.Max(0.01f, len);
                    Quaternion q = Quaternion.LookRotation(new Vector3(t.x, 0f, t.y), Vector3.up);
                    for (float d = 0.4f; d < len; d += 0.85f)
                    {
                        Vector2 c = a + t * d;
                        if (ctx.EdgeDist(c) < 0.6f) continue;
                        float y = Gnd(c);
                        Color32 sc = ((int)(d / 0.85f) & 1) == 0 ? Pal.StoneLight : Pal.Stone;
                        PG.OBox(g, V3(c, y - 0.05f), new Vector3(0.85f, 0.14f, 0.4f), q, sc);
                    }
                }
            }
            StampWorld(m, new Vector3(0f, 60f, 900f));
        }

        // ------------------------------------------------------------------ cloud sea
        GameObject[] cloudLayers;
        Vector3[] cloudBase;
        Material cloudMat;

        IEnumerator BuildClouds()
        {
            // x, z, radius, layer height
            Vector4[] zones =
            {
                new Vector4(200f, 820f, 125f, 88f), new Vector4(385f, 850f, 95f, 92f), new Vector4(150f, 650f, 110f, 62f),
                new Vector4(-300f, 1220f, 125f, 70f), new Vector4(-12f, 760f, 85f, 22f), new Vector4(176f, 1106f, 62f, 78.4f),
                new Vector4(-450f, 760f, 70f, 50f), new Vector4(330f, 1280f, 80f, 142f), new Vector4(80f, 890f, 60f, 55f),
                new Vector4(420f, 1000f, 80f, 120f), new Vector4(-420f, 1380f, 80f, 112f)
            };
            cloudMat = new Material(MaterialLibrary.FindShader("BlossomRally/Particle")) { name = "BR_MtnCloudSea" };
            cloudMat.SetTexture("_MainTex", ProceduralTextures.Soft);
            cloudMat.SetColor("_Tint", Color.white);
            cloudMat.SetFloat("_Lit", 0.5f);
            cloudMat.renderQueue = 3010;
            const int layers = 2;
            cloudLayers = new GameObject[layers];
            cloudBase = new Vector3[layers];
            for (int L = 0; L < layers; L++)
            {
                var v = new List<Vector3>();
                var uv = new List<Vector2>();
                var col = new List<Color32>();
                var tri = new List<int>();
                foreach (var z in zones)
                {
                    float y = z.w + L * 2.4f;
                    float step = L == 0 ? 17f : 23f;
                    for (float dz = -z.z; dz <= z.z; dz += step)
                        for (float dx = -z.z; dx <= z.z; dx += step)
                        {
                            Vector2 c = new Vector2(z.x + dx + Rand(-6f, 6f), z.y + dz + Rand(-6f, 6f));
                            float r01 = Mathf.Sqrt(dx * dx + dz * dz) / z.z;
                            if (r01 > 1f) continue;
                            float g = Gnd(c);
                            if (g > y - 1.2f) continue;
                            float rh;
                            float e = RoadEdge(c, out rh, 40f);
                            if (e < 30f && rh < y + 7f) continue;
                            float size = Rand(28f, 50f) * (L == 0 ? 1f : 1.3f) * Mathf.Lerp(1f, 0.75f, r01);
                            float yy = y + Rand(-0.8f, 0.8f);
                            float rot = Rand(0f, 6.28f);
                            Vector3 ax = new Vector3(Mathf.Cos(rot), 0f, Mathf.Sin(rot)) * size * 0.5f, az = new Vector3(-ax.z, 0f, ax.x);
                            Vector3 cc = V3(c, yy);
                            float depth = Mathf.Clamp01((y - g) / 10f);
                            byte a = (byte)(255f * (L == 0 ? 0.72f : 0.45f) * Mathf.Lerp(0.55f, 1f, depth) * (1f - 0.45f * r01 * r01));
                            Color32 cl = L == 0 ? new Color32(246, 250, 252, a) : new Color32(236, 244, 250, a);
                            int i0 = v.Count;
                            v.Add(cc - ax - az); v.Add(cc + ax - az); v.Add(cc + ax + az); v.Add(cc - ax + az);
                            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                            for (int k = 0; k < 4; k++) col.Add(cl);
                            tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 1);
                            tri.Add(i0); tri.Add(i0 + 3); tri.Add(i0 + 2);
                        }
                }
                var mesh = new Mesh { name = "Mtn_CloudSea_" + L };
                mesh.SetVertices(v);
                mesh.SetUVs(0, uv);
                mesh.SetColors(col);
                mesh.SetTriangles(tri, 0);
                var nrm = new Vector3[v.Count];
                for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up;
                mesh.normals = nrm;
                mesh.RecalculateBounds();
                var b = mesh.bounds; b.Expand(new Vector3(16f, 2f, 16f)); mesh.bounds = b;
                cloudLayers[L] = ctx.AddMesh("Mtn_CloudSea_" + L, mesh, cloudMat, 0, false);
                cloudBase[L] = cloudLayers[L].transform.localPosition;
                yield return null;
            }
        }
    }
}
