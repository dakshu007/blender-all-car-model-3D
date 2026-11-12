// MountainsLandmarks.Build.cs - the golden-roofed summit monastery (paifang over the finish, bell and drum
// towers, main hall, side halls, five-tier pagoda, incense burners, prayer-ribbon trees, lanterns and terraces),
// the lookout pavilions on the spurs and the cable car from the jade lake up to the monastery.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class MountainsRegion
    {
        // monastery frame (matches the flat rectangles from Plan)
        const float MonaYaw = 12f;
        public const float MonaT2 = 4.5f, MonaT3 = 9f;
        static Vector2 MonaLocal(float lx, float lz)
        {
            Vector3 w = Quaternion.Euler(0f, MonaYaw, 0f) * new Vector3(lx, 0f, lz);
            return MountainsMap.Monastery + new Vector2(w.x, w.z);
        }

        readonly List<Vector3> incensePts = new List<Vector3>();
        Vector3 bellPos;
        // cable car
        static Vector2 CableLow { get { return MountainsMap.CableLow; } }
        static Vector2 CableHigh { get { return MountainsMap.CableHigh; } }
        Vector3[] cableA, cableB;     // support points per lane (stations + towers)

        IEnumerator BuildMonastery()
        {
            float y0 = MountainsMap.MonasteryH, y1 = y0 + MonaT2, y2 = y0 + MonaT3;
            Vector2 tT2 = MonaLocal(0.8f, 34.9f), tT3 = MonaLocal(2.8f, 64f);
            float yawF = MonaYaw + 180f;     // buildings face down the hill (towards the plaza / road)
            ctx.Reserve(MountainsMap.Monastery, 30f);
            ctx.Reserve(tT2, 26f);
            ctx.Reserve(tT3, 20f);

            // ---- paving over the plaza and the terraces (slab grid with a little colour play)
            {
                var m = new PropModel();
                var g = m[PK.Props];
                PaveRect(g, 0f, 0f, 29f, 17f, y0);
                PaveRect(g, 0.8f, 34.9f, 25f, 13f, y1);
                PaveRect(g, 2.8f, 64f, 19f, 11f, y2);
                StampWorld(m, V3(MountainsMap.Monastery, y0));
            }
            yield return null;

            // ---- retaining walls with balustrades at the terrace fronts, ochre walls round the sides
            {
                var m = new PropModel();
                var g = m[PK.Landmark];
                Retaining(g, -25f, 26.6f, 20.9f, y1, 0.8f, 4.8f);
                Retaining(g, -17f, 22.6f, 52f, y2, 2.8f, 4.4f);
                // stairs plaza -> T2 and T2 -> T3
                Stairs(g, 0.8f, 13.8f, 20.9f, y0, y1, 4.2f);
                Stairs(g, 2.8f, 46.6f, 52f, y1, y2, 3.2f);
                // side and back walls
                Vector3 W(float lx, float lz, float y) { Vector2 p = MonaLocal(lx, lz); return V3(p, y); }
                MtnShapes.MonasteryWall(g, W(-25.5f, 21.5f, y1), W(-25.5f, 48.5f, y1), 2.2f);
                MtnShapes.MonasteryWall(g, W(27f, 21.5f, y1), W(27f, 48.5f, y1), 2.2f);
                MtnShapes.MonasteryWall(g, W(-17.5f, 52.5f, y2), W(-17.5f, 75.5f, y2), 2.2f);
                MtnShapes.MonasteryWall(g, W(23f, 52.5f, y2), W(23f, 75.5f, y2), 2.2f);
                MtnShapes.MonasteryWall(g, W(-17.5f, 75.5f, y2), W(23f, 75.5f, y2), 2.2f);
                StampWorld(m, V3(tT2, y1));
            }
            yield return null;

            // ---- the paifang gate over the finish line, near the end of the summit road
            var main = Run(MountainsMap.RoadMain);
            if (main != null)
            {
                var s = main.s;
                float end = s[s.Count - 1].distance;
                int gi = s.Count - 1;
                while (gi > 0 && s[gi].distance > end - 24f) gi--;
                var gs = s[gi];
                float gw = gs.halfWidth * 2f + 4f;
                float yaw = YawOf(gs.tangent);   // local X across the road
                var gate = MtnShapes.Paifang(gw, 8.6f);
                Vector3 gp = V3(gs.pos, gs.height);
                ctx.Stamp(gate, gp, yaw);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                foreach (float x in new[] { -gw * 0.5f, -gw * 0.19f, gw * 0.19f, gw * 0.5f })
                {
                    if (Mathf.Abs(x) < gs.halfWidth + 0.2f) continue;   // the inner posts stand clear of the road
                    Vector3 c = gp + q * new Vector3(x, 0f, 0f);
                    ctx.Box(c + Vector3.up * 2.5f, new Vector3(1.3f, 5f, 1.9f), yaw);
                }
            }

            // ---- plaza: bell & drum towers, incense burner, prayer-ribbon trees, lions, lanterns
            {
                Vector3 hang, logPivot;
                var bell = PropShapes.Tower(true, 0.6f, -1.2f, out hang, out logPivot);
                var drum = PropShapes.Tower(false, 0.6f, -1.2f, out hang, out logPivot);
                Vector2 bp = MonaLocal(-21f, 7f), dp = MonaLocal(21f, 7f);
                ctx.Stamp(bell, V3(bp, y0), MonaYaw + 90f);
                ctx.Stamp(drum, V3(dp, y0), MonaYaw - 90f);
                ctx.Box(V3(bp, y0 + 4f), new Vector3(7.4f, 8f, 7.4f), MonaYaw);
                ctx.Box(V3(dp, y0 + 4f), new Vector3(7.4f, 8f, 7.4f), MonaYaw);
                bellPos = V3(bp, y0 + 6f);
                var burner = PropShapes.IncenseBurner(rng);
                foreach (var lp in new[] { new Vector2(0.8f, 8.5f), new Vector2(0.8f, 25f) })
                {
                    Vector2 p = MonaLocal(lp.x, lp.y);
                    float y = lp.y > 20f ? y1 : y0;
                    ctx.Stamp(burner, V3(p, y), yawF, 1.3f);
                    ctx.Capsule(V3(p, y), 1.1f, 2.4f);
                    incensePts.Add(V3(p, y + 2.1f));
                }
                var tree = MtnShapes.PrayerTree(rng);
                foreach (var lp in new[] { new Vector2(-10.5f, -1f), new Vector2(12f, -2f) })
                {
                    Vector2 p = MonaLocal(lp.x, lp.y);
                    ctx.Stamp(tree, V3(p, y0 - 0.1f), Rand(0f, 360f), Rand(0.95f, 1.1f));
                    ctx.Capsule(V3(p, y0), 0.8f, 4f);
                }
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    Vector2 p = MonaLocal(0.8f + sgn * 4.6f, 12.4f);
                    ctx.Stamp(PropShapes.StoneLion(sgn > 0 ? 0 : 1), V3(p, y0), yawF, 1.25f);
                    ctx.Box(V3(p, y0 + 1f), new Vector3(1.3f, 2f, 1.7f), MonaYaw);
                }
                var sl = MtnShapes.StoneLantern();
                foreach (var lp in new[] { new Vector2(-12f, 13f), new Vector2(13.6f, 13f), new Vector2(-8f, 26f), new Vector2(9.6f, 26f), new Vector2(-6f, 56f), new Vector2(11.6f, 56f), new Vector2(-14f, -12f), new Vector2(16f, -12f) })
                {
                    Vector2 p = MonaLocal(lp.x, lp.y);
                    float y = lp.y > 52f ? y2 : lp.y > 20f ? y1 : y0;
                    ctx.Stamp(sl, V3(p, y), yawF);
                    ctx.Box(V3(p, y + 1f), new Vector3(0.8f, 2f, 0.8f), MonaYaw);
                }
                // red lantern strings across the plaza between the towers and along the terrace front
                { var ls = PropShapes.LanternSpan(V3(MonaLocal(-16f, 14f), y0), V3(MonaLocal(-5f, 14f), y0), rng, y0 + 4.6f); StampWorld(ls, V3(MountainsMap.Monastery, MountainsMap.MonasteryH)); }
                { var ls = PropShapes.LanternSpan(V3(MonaLocal(6.6f, 14f), y0), V3(MonaLocal(17.6f, 14f), y0), rng, y0 + 4.6f); StampWorld(ls, V3(MountainsMap.Monastery, MountainsMap.MonasteryH)); }
                { var ls = PropShapes.LanternSpan(V3(MonaLocal(-22f, 30f), y1), V3(MonaLocal(-13f, 30f), y1), rng, y1 + 4f); StampWorld(ls, V3(MountainsMap.Monastery, MountainsMap.MonasteryH)); }
                { var ls = PropShapes.LanternSpan(V3(MonaLocal(14.6f, 30f), y1), V3(MonaLocal(23.6f, 30f), y1), rng, y1 + 4f); StampWorld(ls, V3(MountainsMap.Monastery, MountainsMap.MonasteryH)); }
            }
            yield return null;

            // ---- main hall, side halls, pagoda (skyline)
            {
                var hall = MtnShapes.GoldenHall(9f, 6f, 5.4f, true);
                Vector2 hp = MonaLocal(0.8f, 38f);
                ctx.StampSkyline(hall, V3(hp, y1), yawF);
                ctx.Box(V3(hp, y1 + 3f), new Vector3(23f, 7f, 17f), MonaYaw);
                var side = PropShapes.TempleHall(0.5f, -0.8f);
                Vector2 sl = MonaLocal(-18.5f, 35f), sr = MonaLocal(20.2f, 35f);
                ctx.Stamp(side, V3(sl, y1), MonaYaw + 90f);
                ctx.Stamp(side, V3(sr, y1), MonaYaw - 90f);
                ctx.Box(V3(sl, y1 + 2.5f), new Vector3(9.6f, 5f, 12.6f), MonaYaw);
                ctx.Box(V3(sr, y1 + 2.5f), new Vector3(9.6f, 5f, 12.6f), MonaYaw);
                var pagoda = MtnShapes.Pagoda(5);
                Vector2 pp = MonaLocal(2.8f, 66f);
                ctx.StampSkyline(pagoda, V3(pp, y2), yawF);
                ctx.Box(V3(pp, y2 + 3f), new Vector3(10.6f, 6f, 10.6f), MonaYaw);
                var small = MtnShapes.Pavilion(rng, true);
                foreach (var lp in new[] { new Vector2(-11f, 64f), new Vector2(17f, 64f) })
                {
                    Vector2 p = MonaLocal(lp.x, lp.y);
                    ctx.Stamp(small, V3(p, y2), yawF, 0.75f);
                    ctx.Box(V3(p, y2 + 1.5f), new Vector3(5f, 3f, 5f), MonaYaw);
                }
            }
            yield return null;
        }

        void PaveRect(LifeGeo g, float cx, float cz, float hx, float hz, float y)
        {
            const float S = 2.2f;
            Quaternion q = Quaternion.Euler(0f, MonaYaw, 0f);
            Vector3 o = V3(MountainsMap.Monastery, 0f);
            for (float z = -hz; z < hz; z += S)
                for (float x = -hx; x < hx; x += S)
                {
                    Vector3 a = o + q * new Vector3(cx + x + 0.05f, 0f, cz + z + 0.05f);
                    Vector3 b = o + q * new Vector3(cx + x + S - 0.05f, 0f, cz + z + 0.05f);
                    Vector3 c = o + q * new Vector3(cx + x + S - 0.05f, 0f, cz + z + S - 0.05f);
                    Vector3 d = o + q * new Vector3(cx + x + 0.05f, 0f, cz + z + S - 0.05f);
                    Vector2 mid = new Vector2((a.x + c.x) * 0.5f, (a.z + c.z) * 0.5f);
                    if (ctx.EdgeDist(mid) < 0.8f) continue;
                    float k = 0.5f + 0.5f * Mathf.Sin(x * 1.7f + z * 2.3f) * Mathf.Sin(x * 0.9f - z * 1.3f);
                    Color32 col = Color32.Lerp(PG.C(196, 190, 176), PG.C(222, 216, 200), k);
                    if (((int)((x + 100f) / S) + (int)((z + 100f) / S)) % 7 == 0) col = PG.C(176, 170, 160);
                    a.y = b.y = c.y = d.y = y + 0.04f;
                    g.QuadOut(a, b, c, d, Vector3.up, col, col, col, col);
                }
        }

        /// <summary>Stone retaining wall along local X at local z (lx0..lx1) up to height top, with a gap for stairs at gapX.</summary>
        void Retaining(LifeGeo g, float lx0, float lx1, float lz, float top, float gapX, float gapHalf)
        {
            Quaternion q = Quaternion.Euler(0f, MonaYaw, 0f);
            Vector3 fwd = q * Vector3.back;
            for (float x = lx0; x < lx1; x += 2f)
            {
                float x1 = Mathf.Min(lx1, x + 2f);
                if (x1 > gapX - gapHalf && x < gapX + gapHalf) continue;
                Vector2 a = MonaLocal(x, lz), b = MonaLocal(x1, lz);
                float ga = Mathf.Min(Gnd(MonaLocal(x, lz - 2.5f)), top) - 0.6f, gb = Mathf.Min(Gnd(MonaLocal(x1, lz - 2.5f)), top) - 0.6f;
                Color32 c = (((int)(x + 100f) / 2) & 1) == 0 ? MtnShapes.GraniteL : MtnShapes.GraniteM;
                g.QuadOut(V3(a, ga), V3(a, top + 0.9f), V3(b, top + 0.9f), V3(b, gb), fwd, PG.Sh(c, 0.8f), c, c, PG.Sh(c, 0.8f));
                Vector3 back = -fwd * 0.45f;
                g.QuadOut(V3(a, top + 0.9f), V3(a, top + 0.9f) + back, V3(b, top + 0.9f) + back, V3(b, top + 0.9f), Vector3.up, Pal.Plaster, Pal.Plaster, Pal.Plaster, Pal.Plaster);
                g.QuadOut(V3(a, top - 0.2f) + back, V3(a, top + 0.9f) + back, V3(b, top + 0.9f) + back, V3(b, top - 0.2f) + back, -fwd, c, c, c, c);
            }
        }

        void Stairs(LifeGeo g, float lx, float lz0, float lz1, float yA, float yB, float halfW)
        {
            int steps = Mathf.Max(3, Mathf.CeilToInt((yB - yA) / 0.18f));
            Quaternion q = Quaternion.Euler(0f, MonaYaw, 0f);
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 1) / (float)steps;
                float y = Mathf.Lerp(yA, yB, t);
                float z0 = Mathf.Lerp(lz0, lz1, i / (float)steps), z1 = lz1;
                Vector2 c = MonaLocal(lx, (z0 + z1) * 0.5f);
                PG.OBox(g, V3(c, (y + yA - 0.6f) * 0.5f), new Vector3(halfW, (y - yA + 0.6f) * 0.5f, (z1 - z0) * 0.5f), q, (i & 1) == 0 ? Pal.StoneLight : Pal.Stone);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 a = MonaLocal(lx + s * (halfW + 0.3f), lz0), b = MonaLocal(lx + s * (halfW + 0.3f), lz1);
                PG.Beam(g, V3(a, yA + 0.9f), V3(b, yB + 0.9f), 0.25f, 0.45f, Pal.PlasterShade);
            }
        }

        // ------------------------------------------------------------------ lookout pavilions
        IEnumerator BuildPavilions()
        {
            var pav = MtnShapes.Pavilion(rng, false);
            var bench = PropShapes.StoneTableSet();
            foreach (var pv in PavilionSpots)
            {
                Vector2 p = new Vector2(pv.x, pv.y);
                RoadSample rs;
                float d = ctx.Field.NearestRoad(p, out rs, 80f);
                float yaw = d < 80f ? YawOf(rs.pos - p) : 0f;
                float y = Gnd(p);
                ctx.Stamp(pav, V3(p, y - 0.1f), yaw);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 c = q * new Vector3((i & 1) == 0 ? -2.5f : 2.5f, 0f, (i & 2) == 0 ? -2.5f : 2.5f);
                    ctx.Capsule(V3(p, y) + c, 0.2f, 3.4f);
                }
                ctx.Box(V3(p, y + 0.15f), new Vector3(6.6f, 0.7f, 6.6f), yaw);
                ctx.Reserve(p, 6f);
                // a few stone lanterns and a viewing stone
                Vector2 side = new Vector2(Mathf.Cos(yaw * Mathf.Deg2Rad), -Mathf.Sin(yaw * Mathf.Deg2Rad));
                ctx.Stamp(MtnShapes.StoneLantern(), V3(p + side * 4.6f, Gnd(p + side * 4.6f)), yaw);
                ctx.Stamp(PropShapes.ScholarRock(rng, 1.6f), V3(p - side * 4.8f, Gnd(p - side * 4.8f)), yaw + 30f);
            }
            yield return null;
        }

        // ------------------------------------------------------------------ cable car
        IEnumerator BuildCableCar()
        {
            Vector2 d2 = (CableHigh - CableLow).normalized;
            Vector2 side2 = new Vector2(d2.y, -d2.x);
            float gl = Gnd(CableLow), gh = Gnd(CableHigh);
            ctx.Stamp(MtnShapes.CableStation(false), V3(CableLow, gl), YawOf(d2));
            ctx.Stamp(MtnShapes.CableStation(true), V3(CableHigh, gh), YawOf(-d2));
            ctx.Box(V3(CableLow, gl + 3f), new Vector3(15.5f, 6f, 11.5f), YawOf(d2));
            ctx.Box(V3(CableHigh, gh + 3f), new Vector3(15.5f, 6f, 11.5f), YawOf(d2));
            ctx.Reserve(CableLow, 10f);
            ctx.Reserve(CableHigh, 10f);
            Vector2 e0 = CableLow + d2 * 7.5f, e3 = CableHigh - d2 * 7.5f;
            float y0 = gl + 4.6f, y3 = gh + 4.6f;
            var sup = new List<Vector3> { V3(e0, y0) };
            var tower = new List<float>();
            for (int k = 1; k <= 2; k++)
            {
                float u = k / 3f;
                Vector2 tp = Vector2.Lerp(e0, e3, u);
                float g = Gnd(tp);
                float lin = Mathf.Lerp(y0, y3, u);
                // clear the terrain under the spans by at least 12 m
                float need = lin;
                for (int q = -10; q <= 10; q++)
                {
                    Vector2 sp = tp + d2 * (q * Vector2.Distance(e0, e3) / 60f);
                    need = Mathf.Max(need, Gnd(sp) + 12f);
                }
                float top = Mathf.Max(need, g + 16f);
                sup.Add(V3(tp, top));
                tower.Add(top - g);
                ctx.Stamp(MtnShapes.CableTower(top - g), V3(tp, g - 0.2f), YawOf(side2) + 90f);
                ctx.Box(V3(tp, g + 2f), new Vector3(3f, 4f, 3f), YawOf(d2));
                ctx.Reserve(tp, 4f);
            }
            sup.Add(V3(e3, y3));
            cableA = new Vector3[sup.Count];
            cableB = new Vector3[sup.Count];
            Vector3 off = new Vector3(side2.x, 0f, side2.y) * 3f;
            for (int i = 0; i < sup.Count; i++) { cableA[i] = sup[i] + off; cableB[i] = sup[i] - off; }
            // static cables
            var m = new PropModel();
            var gg = m[PK.Landmark];
            Color32 steel = PG.C(70, 72, 78);
            foreach (var lane in new[] { cableA, cableB })
                for (int i = 0; i < lane.Length - 1; i++)
                    for (int k = 0; k < 12; k++)
                        PG.Rod(gg, CablePoint(lane, i, k / 12f), CablePoint(lane, i, (k + 1) / 12f), 0.05f, steel, 3);
            StampWorld(m, V3(Vector2.Lerp(CableLow, CableHigh, 0.5f), (gl + gh) * 0.5f));
            yield return null;
        }

        static Vector3 CablePoint(Vector3[] lane, int span, float u)
        {
            Vector3 a = lane[span], b = lane[span + 1];
            float L = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            float sag = L * 0.018f;
            return Vector3.Lerp(a, b, u) - Vector3.up * (4f * sag * u * (1f - u));
        }

        /// <summary>Position along a whole lane (0..1), heading out of the span tangent.</summary>
        static Vector3 CableAt(Vector3[] lane, float u, out Vector3 dir)
        {
            int spans = lane.Length - 1;
            float f = Mathf.Clamp01(u) * spans;
            int i = Mathf.Min(spans - 1, (int)f);
            float t = f - i;
            Vector3 p = CablePoint(lane, i, t);
            dir = CablePoint(lane, i, Mathf.Min(1f, t + 0.02f)) - CablePoint(lane, i, Mathf.Max(0f, t - 0.02f));
            return p;
        }
    }
}
