// CityTowers.Build.cs - procedural towers of Jade Harbour City. Every lot of CityPlan becomes one model in
// local space (origin = lot centre on its pad): a podium with a glowing lobby, glass shafts whose window rows
// are vertex-coloured glow bands (glass tints by day, lit offices at night), setbacks with rooftop gardens,
// pagoda eaves, bamboo nodes, crowns and spires (aircraft beacons are returned for the blinking light mesh),
// plus the anchor points of the neon signs hung on them.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    /// <summary>Where a neon sign goes (local to the lot): centre, outward normal, size, kind (0 blade, 1 board, 2 roof).</summary>
    internal struct CitySignSpot
    {
        public Vector3 pos, n;
        public float w, h;
        public int kind;
    }

    internal static class CityTowers
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }
        static float R(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        internal struct Pal
        {
            public Color32 glassLo, glassHi, frame, trim, accent, roof;
            public Pal(Color32 gl, Color32 gh, Color32 f, Color32 t, Color32 a, Color32 r) { glassLo = gl; glassHi = gh; frame = f; trim = t; accent = a; roof = r; }
        }

        internal static readonly Pal[] Pals =
        {
            new Pal(C(36, 104, 96), C(128, 206, 190), C(226, 232, 228), C(170, 196, 188), C(70, 240, 180), C(186, 196, 192)),    // jade
            new Pal(C(30, 82, 110), C(118, 190, 222), C(78, 94, 108), C(120, 140, 156), C(70, 220, 255), C(96, 108, 120)),       // teal
            new Pal(C(120, 96, 60), C(232, 204, 150), C(240, 228, 200), C(214, 176, 104), C(255, 196, 80), C(214, 200, 172)),    // champagne gold
            new Pal(C(86, 112, 132), C(200, 222, 236), C(246, 246, 240), C(206, 214, 222), C(255, 110, 186), C(220, 222, 224)),  // pearl white
            new Pal(C(28, 70, 84), C(104, 168, 176), C(196, 164, 116), C(150, 118, 78), C(255, 168, 80), C(150, 132, 104)),      // bronze & deep teal
        };

        static readonly Color32 LitWarm = C(255, 214, 150), LitCool = C(214, 234, 255), LitGold = C(255, 188, 104);
        static readonly Color32[] Pastels = { C(170, 220, 200), C(244, 196, 196), C(246, 230, 186), C(184, 208, 232), C(236, 236, 226), C(222, 200, 230) };

        // ------------------------------------------------------------------ window bands
        static Color32 WindowCell(Pal p, float t, float lit, System.Random r)
        {
            double q = r.NextDouble();
            if (q < lit) return r.NextDouble() < 0.7 ? LitWarm : (r.NextDouble() < 0.5 ? LitCool : LitGold);
            Color32 g = Color32.Lerp(p.glassLo, p.glassHi, Mathf.Clamp01(t));
            if (q > 0.94) g = Sh(g, 0.72f);
            return g;
        }

        /// <summary>One floor's window band along a face: cells of ~4.5 m merged when they share a colour.</summary>
        static void Band(LifeGeo gl, Vector3 start, Vector3 u, float len, Vector3 n, float ya, float yb, Pal p, float t, float lit, System.Random r)
        {
            int cells = Mathf.Max(1, Mathf.RoundToInt(len / 4.5f));
            float cw = len / cells;
            Vector3 off = n * 0.06f;
            int i = 0;
            while (i < cells)
            {
                Color32 col = WindowCell(p, t, lit, r);
                int j = i + 1;
                while (j < cells && r.NextDouble() < 0.55) j++;
                Vector3 a = start + u * (i * cw + 0.15f) + off, b = start + u * (j * cw - 0.15f) + off;
                gl.QuadOut(new Vector3(a.x, ya, a.z), new Vector3(b.x, ya, b.z), new Vector3(b.x, yb, b.z), new Vector3(a.x, yb, a.z), n, col, col, col, col);
                i = j;
            }
        }

        static float FloorLit(System.Random r)
        {
            double q = r.NextDouble();
            return q < 0.5 ? 0.04f : q < 0.88 ? 0.16f : 0.45f;
        }

        /// <summary>Rectangular glass shaft with banded windows and corner posts.</summary>
        static void RectShaft(PropModel m, Vector3 c, float hx, float hz, float y0, float y1, Pal p, System.Random r, float Htot, float floorH, bool posts = true)
        {
            var core = m[PK.Landmark];
            var gl = m[PK.Glow];
            PG.Box2(core, new Vector3(c.x, (y0 + y1) * 0.5f, c.z), new Vector3(hx, (y1 - y0) * 0.5f, hz), p.frame, p.roof);
            int floors = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / floorH));
            float fh = (y1 - y0) / floors;
            for (int f = 0; f < floors; f++)
            {
                float ya = y0 + f * fh + fh * 0.2f, yb = y0 + (f + 1) * fh - fh * 0.12f;
                float t = Mathf.Pow((ya + c.y) / Mathf.Max(20f, Htot), 0.8f);
                float lit = FloorLit(r);
                Band(gl, new Vector3(c.x - hx, 0f, c.z + hz), Vector3.right, 2f * hx, Vector3.forward, ya, yb, p, t, lit, r);
                Band(gl, new Vector3(c.x + hx, 0f, c.z - hz), Vector3.left, 2f * hx, Vector3.back, ya, yb, p, t, lit, r);
                Band(gl, new Vector3(c.x + hx, 0f, c.z + hz), Vector3.back, 2f * hz, Vector3.right, ya, yb, p, t, lit, r);
                Band(gl, new Vector3(c.x - hx, 0f, c.z - hz), Vector3.forward, 2f * hz, Vector3.left, ya, yb, p, t, lit, r);
            }
            if (posts)
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        PG.Box2(core, new Vector3(c.x + sx * hx, (y0 + y1) * 0.5f, c.z + sz * hz), new Vector3(0.45f, (y1 - y0) * 0.5f, 0.45f), p.trim, p.trim);
        }

        /// <summary>Polygonal (round) glass shaft; rTop / rBot radii, banded per side.</summary>
        static void PolyShaft(PropModel m, Vector3 c, float rBot, float rTop, int sides, float y0, float y1, Pal p, System.Random r, float Htot, float floorH)
        {
            var core = m[PK.Landmark];
            var gl = m[PK.Glow];
            float rot = Mathf.PI / sides;
            core.Frustum(new Vector3(c.x, y0, c.z), new Vector3(c.x, y1, c.z), rBot, rBot, rTop, rTop, sides, rot, p.frame, p.frame, false, true);
            int floors = Mathf.Max(1, Mathf.RoundToInt((y1 - y0) / floorH));
            float fh = (y1 - y0) / floors;
            float apo = Mathf.Cos(Mathf.PI / sides);
            for (int f = 0; f < floors; f++)
            {
                float ya = y0 + f * fh + fh * 0.2f, yb = y0 + (f + 1) * fh - fh * 0.12f;
                float ra = Mathf.Lerp(rBot, rTop, (ya - y0) / (y1 - y0)), rb = Mathf.Lerp(rBot, rTop, (yb - y0) / (y1 - y0));
                float t = Mathf.Pow((ya + c.y) / Mathf.Max(20f, Htot), 0.8f);
                float lit = FloorLit(r);
                for (int s = 0; s < sides; s++)
                {
                    float a0 = rot + s * Mathf.PI * 2f / sides, a1 = a0 + Mathf.PI * 2f / sides;
                    float am = (a0 + a1) * 0.5f;
                    Vector3 n = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    float k = 0.06f / apo;
                    Vector3 pa0 = c + d0 * (ra + k), pa1 = c + d1 * (ra + k), pb0 = c + d0 * (rb + k), pb1 = c + d1 * (rb + k);
                    // inset the cell a little from the edges so the frame shows as mullions
                    Vector3 ia0 = Vector3.Lerp(pa0, pa1, 0.06f), ia1 = Vector3.Lerp(pa0, pa1, 0.94f), ib0 = Vector3.Lerp(pb0, pb1, 0.06f), ib1 = Vector3.Lerp(pb0, pb1, 0.94f);
                    Color32 col = WindowCell(p, t, lit, r);
                    gl.QuadOut(new Vector3(ia0.x, ya, ia0.z), new Vector3(ia1.x, ya, ia1.z), new Vector3(ib1.x, yb, ib1.z), new Vector3(ib0.x, yb, ib0.z), n, col, col, col, col);
                }
            }
        }

        // ------------------------------------------------------------------ parts
        static void Podium(PropModel m, float hx, float hz, float h, Pal p, Vector2 face, System.Random r, List<CitySignSpot> signs, bool neon)
        {
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            PG.Box2(g, new Vector3(0f, h * 0.5f, 0f), new Vector3(hx, h * 0.5f, hz), Sh(p.frame, 0.92f), C(120, 150, 126));
            // glowing lobby band all around at street level, darker upper band
            Color32 lobby = Color32.Lerp(LitWarm, p.glassHi, 0.35f);
            Vector3[] st = { new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, -hz) };
            Vector3[] us = { Vector3.right, Vector3.left, Vector3.back, Vector3.forward };
            Vector3[] ns = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            float[] ls = { 2f * hx, 2f * hx, 2f * hz, 2f * hz };
            for (int k = 0; k < 4; k++)
            {
                Vector3 o = ns[k] * 0.07f;
                Vector3 a = st[k] + us[k] * 0.8f + o, b = st[k] + us[k] * (ls[k] - 0.8f) + o;
                gl.QuadOut(new Vector3(a.x, 0.3f, a.z), new Vector3(b.x, 0.3f, b.z), new Vector3(b.x, 3.6f, b.z), new Vector3(a.x, 3.6f, a.z), ns[k], lobby, lobby, Sh(lobby, 0.9f), Sh(lobby, 0.9f));
                if (h > 7f) Band(gl, st[k], us[k], ls[k], ns[k], 5.0f, h - 1.2f, p, 0.15f, 0.5f, r);
            }
            // entrance canopy on the street face
            Vector3 f3 = new Vector3(face.x, 0f, face.y);
            Vector3 side = new Vector3(-face.y, 0f, face.x);
            float reach = Mathf.Abs(face.x) > 0.5f ? hx : hz;
            Vector3 cc = f3 * (reach + 1.8f) + Up * 4.2f;
            float cw = Mathf.Min(7f, (Mathf.Abs(face.x) > 0.5f ? hz : hx) * 0.6f);
            PG.Box2(g, cc, Abs(side * cw + f3 * 1.8f) + Up * 0.15f, p.trim, p.trim);
            PG.Box2(gl, cc - Up * 0.17f, Abs(side * (cw - 0.3f) + f3 * 1.5f) + Up * 0.02f, LampWarm, LampWarm);
            for (int s = -1; s <= 1; s += 2) PG.Box2(g, f3 * (reach + 3.3f) + side * s * (cw - 0.3f) + Up * 2.05f, new Vector3(0.12f, 2.05f, 0.12f), p.trim, p.trim);
            if (neon && signs != null)
            {
                signs.Add(new CitySignSpot { pos = f3 * (reach + 0.12f) + side * (cw + 2.5f) * (r.NextDouble() < 0.5 ? -1f : 1f) + Up * (h - 2.6f), n = f3, w = R(r, 7f, 12f), h = 2.2f, kind = 1 });
            }
        }

        static readonly Color32 LampWarm = C(255, 230, 180);

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        static void RoofGarden(PropModel m, float hx, float hz, float y, System.Random r, int trees)
        {
            var g = m[PK.Props];
            PG.Box2(g, new Vector3(0f, y + 0.15f, 0f), new Vector3(hx * 0.8f, 0.15f, hz * 0.8f), C(126, 104, 82), C(108, 160, 88));
            Color32 top = C(110, 172, 92), bot = C(62, 120, 70);
            for (int i = 0; i < trees; i++)
            {
                Vector3 c = new Vector3(R(r, -hx * 0.65f, hx * 0.65f), y + 0.3f, R(r, -hz * 0.65f, hz * 0.65f));
                float s = R(r, 0.8f, 1.5f);
                g.Frustum(c, c + Up * 1.6f * s, 0.1f, 0.1f, 0.07f, 0.07f, 5, 0f, CityKit.Bark, CityKit.Bark, false, false);
                g.Ellipsoid(c + Up * 2.2f * s, new Vector3(1.1f, 0.9f, 1.1f) * s, 6, 3, top, bot);
            }
            // pergola with a jade frame
            float px = hx * 0.5f;
            for (int k = -1; k <= 1; k += 2) PG.Beam(g, new Vector3(k * px, y + 2.6f, -hz * 0.5f), new Vector3(k * px, y + 2.6f, hz * 0.5f), 0.08f, 0.08f, CityKit.Jade);
            for (int k = -1; k <= 1; k += 2)
                for (int l = -1; l <= 1; l += 2) PG.Beam(g, new Vector3(k * px, y + 0.3f, l * hz * 0.5f), new Vector3(k * px, y + 2.6f, l * hz * 0.5f), 0.07f, 0.07f, CityKit.Pearl);
        }

        static void RoofKit(PropModel m, float hx, float hz, float y, System.Random r)
        {
            var g = m[PK.Landmark];
            Color32 mech = C(176, 180, 182);
            PG.Box2(g, new Vector3(R(r, -hx, hx) * 0.3f, y + 1.8f, R(r, -hz, hz) * 0.3f), new Vector3(hx * 0.35f, 1.8f, hz * 0.3f), mech, Sh(mech, 1.06f));
            for (int i = 0; i < 4; i++)
                PG.Box2(g, new Vector3(R(r, -hx, hx) * 0.7f, y + 0.5f, R(r, -hz, hz) * 0.7f), new Vector3(0.9f, 0.5f, 0.7f), C(200, 202, 204), C(214, 216, 218));
            // parapet
            Color32 par = C(214, 214, 210);
            PG.Box2(g, new Vector3(0f, y + 0.5f, hz - 0.15f), new Vector3(hx, 0.5f, 0.15f), par, par);
            PG.Box2(g, new Vector3(0f, y + 0.5f, -hz + 0.15f), new Vector3(hx, 0.5f, 0.15f), par, par);
            PG.Box2(g, new Vector3(hx - 0.15f, y + 0.5f, 0f), new Vector3(0.15f, 0.5f, hz), par, par);
            PG.Box2(g, new Vector3(-hx + 0.15f, y + 0.5f, 0f), new Vector3(0.15f, 0.5f, hz), par, par);
        }

        static void WaterTank(PropModel m, Vector3 c)
        {
            var g = m[PK.Landmark];
            Color32 wood = C(150, 106, 70);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.78f;
                PG.Beam(g, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.3f, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.1f + Up * 3f, 0.08f, 0.08f, CityKit.SteelDark);
            }
            g.Frustum(c + Up * 3f, c + Up * 5.6f, 1.5f, 1.5f, 1.5f, 1.5f, 10, 0f, wood, Sh(wood, 1.1f), true, false);
            g.Frustum(c + Up * 5.6f, c + Up * 6.4f, 1.6f, 1.6f, 0.1f, 0.1f, 10, 0f, CityKit.SteelDark, CityKit.Steel, true, false);
        }

        /// <summary>Spire with a beacon at the tip.</summary>
        static void Spire(PropModel m, Vector3 b, float h, float r0, Color32 col, List<Vector3> beacons)
        {
            var g = m[PK.Landmark];
            g.Frustum(b, b + Up * h, r0, r0, r0 * 0.12f, r0 * 0.12f, 6, 0f, col, Sh(col, 1.15f), true, false);
            g.Frustum(b + Up * (h * 0.35f), b + Up * (h * 0.38f), r0 * 1.6f, r0 * 1.6f, r0 * 1.4f, r0 * 1.4f, 6, 0f, col, col, true, true);
            if (beacons != null) beacons.Add(b + Up * (h + 0.4f));
        }

        static void GlowRing(LifeGeo gl, Vector3 c, float rx, float rz, float y0, float y1, int sides, float rot, Color32 col)
        {
            gl.Frustum(new Vector3(c.x, y0, c.z), new Vector3(c.x, y1, c.z), rx, rz, rx, rz, sides, rot, col, Sh(col, 0.9f), false, false);
        }

        // ------------------------------------------------------------------ styles
        public static PropModel Build(CityLot lot, List<Vector3> beacons, List<CitySignSpot> signs)
        {
            var rng = new System.Random(lot.seed);
            var p = Pals[lot.palette % Pals.Length];
            var m = new PropModel();
            float hx = lot.half.x, hz = lot.half.y, H = lot.height;
            switch (lot.style)
            {
                case CityStyle.Setback: Setback(m, hx, hz, H, p, rng, lot, beacons, signs); break;
                case CityStyle.Round: Round(m, hx, hz, H, p, rng, lot, beacons, signs); break;
                case CityStyle.Bamboo: Bamboo(m, hx, hz, H, p, rng, lot, beacons, signs); break;
                case CityStyle.Pagoda: Pagoda(m, hx, hz, H, p, rng, lot, beacons, signs); break;
                case CityStyle.Twin: Twin(m, hx, hz, H, p, rng, lot, beacons, signs); break;
                case CityStyle.Slab: Slab(m, hx, hz, H, p, rng, lot, beacons, signs); break;
                case CityStyle.Shophouse: Shophouse(m, hx, hz, H, rng, lot, signs); break;
                default: Midrise(m, hx, hz, H, p, rng, lot, beacons, signs); break;
            }
            return m;
        }

        static void Setback(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 11f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            float[] ys = { pod, H * 0.56f, H * 0.82f, H };
            float[] ks = { 0.8f, 0.66f, 0.52f };
            for (int t = 0; t < 3; t++)
            {
                float sx = hx * ks[t], sz = hz * ks[t];
                RectShaft(m, Vector3.zero, sx, sz, ys[t], ys[t + 1], p, r, H, 3.9f);
                // terrace slab with a glowing edge and a small garden on the setback
                PG.Box2(m[PK.Landmark], new Vector3(0f, ys[t + 1] + 0.35f, 0f), new Vector3(sx + 0.6f, 0.35f, sz + 0.6f), p.trim, p.roof);
                m[PK.Glow].Frustum(new Vector3(0f, ys[t + 1] - 0.05f, 0f), new Vector3(0f, ys[t + 1] + 0.12f, 0f), (sx + 0.65f) * 1.4142f, (sz + 0.65f) * 1.4142f, (sx + 0.65f) * 1.4142f, (sz + 0.65f) * 1.4142f, 4, Mathf.PI * 0.25f, p.accent, p.accent, false, false);
                if (t < 2)
                {
                    float nx = hx * ks[t + 1];
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 c = new Vector3(s * (sx + nx) * 0.5f, ys[t + 1] + 0.7f, 0f);
                        m[PK.Props].Ellipsoid(c + Up * 0.9f, new Vector3((sx - nx) * 0.35f, 0.9f, sz * 0.6f), 6, 3, C(110, 172, 92), C(62, 120, 70));
                    }
                }
            }
            // crown: four fins, glow crown band, spire
            float cx = hx * 0.52f, cz = hz * 0.52f, top = H + 0.7f;
            var g = m[PK.Landmark];
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.Box2(g, new Vector3(sx * cx * 0.8f, top + 6f, sz * cz * 0.8f), new Vector3(0.6f, 6f, 0.6f), p.trim, p.trim);
            GlowRing(m[PK.Glow], Vector3.zero, cx * 1.2f, cz * 1.2f, top + 8f, top + 9.2f, 4, Mathf.PI * 0.25f, p.accent);
            GlowRing(m[PK.Glow], Vector3.zero, cx * 1.15f, cz * 1.15f, top + 3f, top + 3.6f, 4, Mathf.PI * 0.25f, p.accent);
            Spire(m, new Vector3(0f, top, 0f), H > 150f ? 34f : 16f, 0.9f, p.trim, bc);
        }

        static void Round(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 10f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            float r0 = Mathf.Min(hx, hz) * 0.86f;
            float y1 = H * 0.62f, y2 = H;
            PolyShaft(m, Vector3.zero, r0, r0 * 0.92f, 12, pod, y1, p, r, H, 3.9f);
            m[PK.Glow].Frustum(new Vector3(0f, y1 - 0.2f, 0f), new Vector3(0f, y1 + 0.6f, 0f), r0 * 0.96f, r0 * 0.96f, r0 * 0.96f, r0 * 0.96f, 12, 0f, p.accent, p.accent, false, false);
            PolyShaft(m, Vector3.zero, r0 * 0.86f, r0 * 0.66f, 12, y1 + 0.6f, y2, p, r, H, 3.9f);
            // crown: glowing halo ring on struts and a dome
            var g = m[PK.Landmark];
            float rc = r0 * 0.66f;
            g.Ellipsoid(new Vector3(0f, y2, 0f), new Vector3(rc, rc * 0.45f, rc), 12, 3, p.trim, p.frame);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PG.Beam(g, d * rc * 0.8f + Up * y2, d * rc * 1.25f + Up * (y2 + 7f), 0.25f, 0.25f, p.trim);
            }
            GlowRing(m[PK.Glow], Vector3.zero, rc * 1.3f, rc * 1.3f, y2 + 6.6f, y2 + 7.6f, 16, 0f, p.accent);
            Spire(m, new Vector3(0f, y2 + rc * 0.4f, 0f), 22f, 0.8f, p.trim, bc);
        }

        static void Bamboo(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 9f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            float r0 = Mathf.Min(hx, hz) * 0.9f;
            int segs = Mathf.Max(3, Mathf.RoundToInt((H - pod) / 24f));
            float sh = (H - pod) / segs;
            var g = m[PK.Landmark];
            Color32 node = CityKit.JadeLight;
            for (int s = 0; s < segs; s++)
            {
                float ra = r0 * (1f - 0.05f * s), rb = r0 * (1f - 0.05f * s - 0.03f);
                float y0 = pod + s * sh, y1 = y0 + sh;
                PolyShaft(m, Vector3.zero, ra, rb, 8, y0, y1 - 1.2f, p, r, H, 3.8f);
                g.Frustum(new Vector3(0f, y1 - 1.3f, 0f), new Vector3(0f, y1 + 0.2f, 0f), rb * 1.08f, rb * 1.08f, rb * 1.02f, rb * 1.02f, 8, Mathf.PI / 8f, node, Sh(node, 1.1f), true, true);
                GlowRing(m[PK.Glow], Vector3.zero, rb * 1.09f, rb * 1.09f, y1 - 0.9f, y1 - 0.5f, 8, Mathf.PI / 8f, p.accent);
            }
            // leaf crown: four jade blades leaning out
            float rt = r0 * (1f - 0.05f * segs);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), sd = new Vector3(-d.z, 0f, d.x);
                Vector3 b0 = d * rt * 0.4f + Up * H, tip = d * rt * 1.25f + Up * (H + 22f);
                PG.Quad2(m[PK.Landmark], b0 - sd * 3f, b0 + sd * 3f, tip + sd * 0.4f, tip - sd * 0.4f, CityKit.JadeDeep, CityKit.JadeDeep, CityKit.JadeLight, CityKit.JadeLight);
                PG.Beam(m[PK.Glow], b0 + d * 0.15f, tip + d * 0.15f, 0.12f, 0.12f, p.accent);
            }
            Spire(m, new Vector3(0f, H, 0f), 28f, 0.7f, node, bc);
        }

        static void Pagoda(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 12f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            int tiers = Mathf.Max(3, Mathf.RoundToInt((H - pod) / 30f));
            float th = (H - pod) / tiers;
            var g = m[PK.Landmark];
            for (int t = 0; t < tiers; t++)
            {
                float k = 0.84f - 0.07f * t;
                float sx = hx * k, sz = hz * k;
                float y0 = pod + t * th, y1 = y0 + th;
                RectShaft(m, Vector3.zero, sx, sz, y0, y1 - 1.6f, p, r, H, 3.9f);
                // flared eave with upturned corners and a glowing gold underline
                float ex = (sx + 3.2f) * 1.4142f, ez = (sz + 3.2f) * 1.4142f, ix = (sx + 0.4f) * 1.4142f, iz = (sz + 0.4f) * 1.4142f;
                g.Frustum(new Vector3(0f, y1 - 1.8f, 0f), new Vector3(0f, y1 - 0.2f, 0f), ex, ez, ix, iz, 4, Mathf.PI * 0.25f, p.trim, p.roof, true, true);
                m[PK.Glow].Frustum(new Vector3(0f, y1 - 2.0f, 0f), new Vector3(0f, y1 - 1.75f, 0f), ex * 1.01f, ez * 1.01f, ex * 1.01f, ez * 1.01f, 4, Mathf.PI * 0.25f, p.accent, p.accent, false, false);
                for (int sx2 = -1; sx2 <= 1; sx2 += 2)
                    for (int sz2 = -1; sz2 <= 1; sz2 += 2)
                    {
                        Vector3 cpt = new Vector3(sx2 * (sx + 3.2f), y1 - 1.8f, sz2 * (sz + 3.2f));
                        PG.Beam(g, cpt, cpt + new Vector3(sx2 * 1.4f, 2.0f, sz2 * 1.4f), 0.25f, 0.25f, p.trim);
                        m[PK.Glow].Ellipsoid(cpt + new Vector3(sx2 * 1.5f, 2.2f, sz2 * 1.5f), new Vector3(0.45f, 0.45f, 0.45f), 6, 3, p.accent, p.accent);
                    }
                PG.Box2(g, new Vector3(0f, y1 - 0.9f, 0f), new Vector3(sx * 0.98f, 0.7f, sz * 0.98f), p.frame, p.roof);
            }
            // crown: three small stacked roofs and a gold finial spire
            float s0 = hx * (0.84f - 0.07f * tiers);
            float y = H;
            for (int i = 0; i < 3; i++)
            {
                float w = s0 * (0.8f - i * 0.22f);
                g.Frustum(new Vector3(0f, y, 0f), new Vector3(0f, y + 4f, 0f), w * 1.2f, w * 1.2f, w * 0.85f, w * 0.85f, 4, Mathf.PI * 0.25f, p.frame, p.frame, true, false);
                g.Frustum(new Vector3(0f, y + 4f, 0f), new Vector3(0f, y + 5.2f, 0f), (w + 2.2f) * 1.4142f, (w + 2.2f) * 1.4142f, (w * 0.5f) * 1.4142f, (w * 0.5f) * 1.4142f, 4, Mathf.PI * 0.25f, p.trim, p.roof, true, true);
                m[PK.Glow].Frustum(new Vector3(0f, y + 3.8f, 0f), new Vector3(0f, y + 4.05f, 0f), (w + 2.25f) * 1.4142f, (w + 2.25f) * 1.4142f, (w + 2.25f) * 1.4142f, (w + 2.25f) * 1.4142f, 4, Mathf.PI * 0.25f, p.accent, p.accent, false, false);
                y += 5.2f;
            }
            Color32 gold = CityKit.Gold;
            for (int i = 0; i < 4; i++) g.Ellipsoid(new Vector3(0f, y + 3f + i * 4.5f, 0f), new Vector3(1.3f - i * 0.2f, 0.9f, 1.3f - i * 0.2f), 8, 4, gold, Sh(gold, 0.8f));
            Spire(m, new Vector3(0f, y, 0f), H > 180f ? 40f : 22f, 0.8f, gold, bc);
        }

        static void Twin(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 10f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            bool alongZ = hz >= hx;
            float half = (alongZ ? hz : hx);
            float tw = half * 0.4f, other = (alongZ ? hx : hz) * 0.78f;
            float[] hs = { H, H * 0.86f };
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = alongZ ? new Vector3(0f, 0f, s * half * 0.52f) : new Vector3(s * half * 0.52f, 0f, 0f);
                float h = hs[s < 0 ? 0 : 1];
                float sx = alongZ ? other : tw, sz = alongZ ? tw : other;
                RectShaft(m, c, sx, sz, pod, h, p, r, H, 3.9f);
                PG.Box2(m[PK.Landmark], c + Up * (h + 1.2f), new Vector3(sx * 0.8f, 1.2f, sz * 0.8f), p.trim, p.roof);
                GlowRing(m[PK.Glow], c, sx * 0.85f * 1.4142f, sz * 0.85f * 1.4142f, h + 2.4f, h + 3.2f, 4, Mathf.PI * 0.25f, p.accent);
                Spire(m, c + Up * (h + 2.4f), 14f, 0.6f, p.trim, bc);
            }
            // sky bridge
            float yb = H * 0.62f;
            Vector3 bh = alongZ ? new Vector3(other * 0.5f, 2.6f, half * 0.6f) : new Vector3(half * 0.6f, 2.6f, other * 0.5f);
            PG.Box2(m[PK.Landmark], Up * yb, bh, p.trim, p.roof);
            PG.Box2(m[PK.Glow], Up * yb, new Vector3(bh.x * (alongZ ? 1.02f : 0.98f), 1.4f, bh.z * (alongZ ? 0.98f : 1.02f)), p.accent, p.accent);
        }

        static void Slab(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 6f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            RectShaft(m, Vector3.zero, hx * 0.92f, hz * 0.88f, pod, H, p, r, H, 3.6f, false);
            var g = m[PK.Landmark];
            // vertical fins on the two long faces
            bool longX = hx >= hz;
            int fins = Mathf.Max(3, Mathf.RoundToInt((longX ? hx : hz) / 4f));
            for (int i = 0; i <= fins; i++)
            {
                float t = -1f + 2f * i / fins;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 c = longX ? new Vector3(t * hx * 0.92f, (pod + H) * 0.5f, s * (hz * 0.88f + 0.35f)) : new Vector3(s * (hx * 0.92f + 0.35f), (pod + H) * 0.5f, t * hz * 0.88f);
                    PG.Box2(g, c, longX ? new Vector3(0.18f, (H - pod) * 0.5f, 0.35f) : new Vector3(0.35f, (H - pod) * 0.5f, 0.18f), p.trim, p.trim);
                }
            }
            RoofKit(m, hx * 0.92f, hz * 0.88f, H, r);
            if (lot.neon && signs != null && r.NextDouble() < 0.7)
            {
                Vector3 f = new Vector3(lot.face.x, 0f, lot.face.y);
                signs.Add(new CitySignSpot { pos = Up * (H + 4.5f) + f * ((Mathf.Abs(f.x) > 0.5f ? hx : hz) * 0.6f), n = f, w = Mathf.Min(22f, (Mathf.Abs(f.x) > 0.5f ? hz : hx) * 1.5f), h = 5.5f, kind = 2 });
            }
            else bc.Add(Up * (H + 4f));
        }

        static void Midrise(PropModel m, float hx, float hz, float H, Pal p, System.Random r, CityLot lot, List<Vector3> bc, List<CitySignSpot> signs)
        {
            float pod = 7f;
            Podium(m, hx, hz, pod, p, lot.face, r, signs, lot.neon);
            float sx = hx * 0.9f, sz = hz * 0.9f;
            RectShaft(m, Vector3.zero, sx, sz, pod, H, p, r, H, 3.4f);
            // horizontal balcony slabs every other floor
            var g = m[PK.Landmark];
            for (float y = pod + 3.4f; y < H - 2f; y += 6.8f)
                PG.Box2(g, Up * y, new Vector3(sx + 0.5f, 0.12f, sz + 0.5f), p.trim, p.trim);
            if (r.NextDouble() < 0.55) RoofGarden(m, sx, sz, H, r, 3 + r.Next(4));
            else
            {
                RoofKit(m, sx, sz, H, r);
                WaterTank(m, new Vector3(sx * 0.45f, H, -sz * 0.4f));
            }
            if (lot.neon && signs != null)
            {
                Vector3 f = new Vector3(lot.face.x, 0f, lot.face.y), side = new Vector3(-f.z, 0f, f.x);
                float reach = Mathf.Abs(f.x) > 0.5f ? hx : hz, across = Mathf.Abs(f.x) > 0.5f ? hz : hx;
                // vertical blade sign at a front corner
                signs.Add(new CitySignSpot { pos = f * (reach * 0.9f + 1.6f) + side * (across * 0.8f) + Up * (pod + 7f), n = side, w = 1.6f, h = Mathf.Min(14f, H * 0.35f), kind = 0 });
            }
            else bc.Add(Up * (H + 1f));
        }

        static void Shophouse(PropModel m, float hx, float hz, float H, System.Random r, CityLot lot, List<CitySignSpot> signs)
        {
            // qilou arcade shophouse facing -z: arcade columns in front, pastel upper floors over the walkway
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 wall = Pastels[lot.palette % Pastels.Length], trim = C(246, 244, 236), shutter = C(70, 120, 110);
            float front = -hz, arcade = 3.2f, groundH = 4.4f;
            PG.Box2(g, new Vector3(0f, groundH * 0.5f, arcade * 0.5f), new Vector3(hx, groundH * 0.5f, hz - arcade * 0.5f), Sh(wall, 0.9f), wall);
            PG.Box2(g, new Vector3(0f, (groundH + H) * 0.5f, 0f), new Vector3(hx, (H - groundH) * 0.5f, hz), wall, C(160, 150, 140));
            // shop front glow and rolling shutter
            Color32 shop = r.NextDouble() < 0.7 ? C(255, 214, 160) : C(220, 236, 255);
            gl.QuadOut(new Vector3(-hx + 0.6f, 0.2f, front + arcade - 0.06f), new Vector3(hx - 0.6f, 0.2f, front + arcade - 0.06f), new Vector3(hx - 0.6f, 3.2f, front + arcade - 0.06f), new Vector3(-hx + 0.6f, 3.2f, front + arcade - 0.06f), Vector3.back, shop, shop, Sh(shop, 0.85f), Sh(shop, 0.85f));
            // arcade columns and beam
            int cols = Mathf.Max(2, Mathf.RoundToInt(hx / 2.6f));
            for (int i = 0; i <= cols; i++)
            {
                float x = -hx + 0.3f + (2f * hx - 0.6f) * i / cols;
                PG.Box2(g, new Vector3(x, groundH * 0.5f, front + 0.3f), new Vector3(0.3f, groundH * 0.5f, 0.3f), trim, trim);
            }
            PG.Box2(g, new Vector3(0f, groundH - 0.3f, front + 0.25f), new Vector3(hx, 0.35f, 0.3f), trim, trim);
            // upper floors: tall shuttered windows with glowing panes
            int floors = Mathf.Max(1, Mathf.RoundToInt((H - groundH) / 3.6f));
            float fh = (H - groundH) / floors;
            int wins = Mathf.Max(1, Mathf.RoundToInt(hx / 2.2f));
            for (int f = 0; f < floors; f++)
            {
                float y0 = groundH + f * fh + 0.7f, y1 = y0 + fh - 1.3f;
                for (int i = 0; i < wins; i++)
                {
                    float x = -hx + (2f * hx) * (i + 0.5f) / wins;
                    Color32 pane = r.NextDouble() < 0.45 ? C(255, 210, 140) : C(110, 150, 160);
                    gl.QuadOut(new Vector3(x - 0.55f, y0, front - 0.05f), new Vector3(x + 0.55f, y0, front - 0.05f), new Vector3(x + 0.55f, y1, front - 0.05f), new Vector3(x - 0.55f, y1, front - 0.05f), Vector3.back, pane, pane, pane, pane);
                    for (int s = -1; s <= 1; s += 2)
                        PG.Box2(g, new Vector3(x + s * 0.85f, (y0 + y1) * 0.5f, front - 0.1f), new Vector3(0.28f, (y1 - y0) * 0.5f, 0.05f), shutter, shutter);
                    PG.Box2(g, new Vector3(x, y0 - 0.15f, front - 0.25f), new Vector3(0.8f, 0.08f, 0.25f), trim, trim);
                }
                PG.Box2(g, new Vector3(0f, groundH + f * fh, front - 0.08f), new Vector3(hx, 0.14f, 0.1f), trim, trim);
            }
            // decorative parapet: stepped or arched gable
            if (r.NextDouble() < 0.5)
            {
                PG.Box2(g, new Vector3(0f, H + 0.6f, front + 0.2f), new Vector3(hx, 0.6f, 0.2f), trim, trim);
                PG.Box2(g, new Vector3(0f, H + 1.7f, front + 0.2f), new Vector3(hx * 0.45f, 0.6f, 0.2f), wall, wall);
            }
            else
            {
                g.Frustum(new Vector3(0f, H, front + 0.05f), new Vector3(0f, H, front + 0.4f), hx * 0.75f, 2.2f, hx * 0.75f, 2.2f, 10, 0f, trim, trim, true, true);
                PG.Box2(g, new Vector3(0f, H + 0.3f, front + 0.2f), new Vector3(hx, 0.3f, 0.2f), trim, trim);
            }
            if (signs != null && r.NextDouble() < 0.75)
            {
                if (r.NextDouble() < 0.5) signs.Add(new CitySignSpot { pos = new Vector3(0f, groundH + 0.2f, front - 0.3f), n = Vector3.back, w = hx * 1.6f, h = 1.3f, kind = 1 });
                else signs.Add(new CitySignSpot { pos = new Vector3((r.NextDouble() < 0.5 ? -1f : 1f) * (hx - 0.6f), groundH + 3.6f, front - 1.1f), n = Vector3.right, w = 1.2f, h = Mathf.Min(7f, H - groundH - 1f), kind = 0 });
            }
        }
    }
}
