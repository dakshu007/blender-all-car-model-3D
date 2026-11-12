// CityRegion.Landmarks.Build.cs - the landmarks of Jade Harbour City: the Lantern Tower (TV tower with a giant
// red lantern observation pod) in the Neon Circus, the jade Gateway Arch over Jade Avenue, the Old Town with
// its lantern alley, red pagoda temple and the moon gate framing the towers, the Lotus Arena stadium, metro
// entrances, Jade Plaza with the "bi" disc fountain and the seafront promenade.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CityRegion
    {
        readonly List<Vector3> fountainJets = new List<Vector3>();

        // ------------------------------------------------------------------ Lantern Tower
        void BuildLanternTower(RegionContext ctx)
        {
            Vector2 c2 = CityPlan.TvTower;
            float gy = CityPlan.Plat;
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 white = C(240, 242, 238), jade = CityKit.Jade, gold = CityKit.Gold, red = C(226, 52, 42), redD = C(170, 30, 30);
            // podium
            g.Frustum(Vector3.zero, Up * 1.2f, 24f, 24f, 22f, 22f, 24, 0f, C(196, 192, 184), C(214, 210, 202), false, true);
            gl.Frustum(Up * 1.21f, Up * 1.26f, 22.2f, 22.2f, 22.2f, 22.2f, 24, 0f, C(120, 255, 210), C(120, 255, 210), false, false);
            g.Frustum(Up * 1.2f, Up * 7f, 9f, 9f, 8f, 8f, 12, 0f, white, white, false, true);
            gl.Frustum(Up * 2f, Up * 5.5f, 9.05f, 9.05f, 8.35f, 8.35f, 12, 0f, C(255, 226, 170), C(255, 236, 196), false, false);
            // tripod legs
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f + 0.5f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 foot = d * 17f + Up * 1.2f, head = d * 3.4f + Up * 52f;
                PG.Beam(g, foot, head, 1.3f, 1.6f, white);
                PG.Beam(gl, foot + d * 1.4f + Up * 0.5f, head + d * 1.35f, 0.12f, 0.12f, C(120, 255, 210));
                Vector3 mid = Vector3.Lerp(foot, head, 0.18f);
                ctx.Box(new Vector3(c2.x + mid.x, gy + mid.y, c2.y + mid.z), new Vector3(3.2f, mid.y * 2f, 3.2f), a * Mathf.Rad2Deg);
                g.Frustum(foot - Up * 1.2f, foot + Up * 0.6f, 2.6f, 2.6f, 2.2f, 2.2f, 8, 0f, C(170, 166, 158), C(186, 182, 174), false, true);
            }
            // shaft
            g.Frustum(Up * 7f, Up * 104f, 4.6f, 4.6f, 3.0f, 3.0f, 10, 0f, white, white, false, false);
            g.Frustum(Up * 128f, Up * 176f, 2.9f, 2.9f, 2.2f, 2.2f, 10, 0f, white, white, false, true);
            for (float y = 20f; y < 100f; y += 16f) gl.Frustum(Up * y, Up * (y + 0.8f), 4.6f - y * 0.0165f + 0.08f, 4.6f - y * 0.0165f + 0.08f, 4.6f - y * 0.0165f + 0.07f, 4.6f - y * 0.0165f + 0.07f, 10, 0f, jade, jade, false, false);
            ctx.Capsule(new Vector3(c2.x, gy, c2.y), 5.2f, 60f);
            // the lantern pod: red glowing panels, gold ribs, caps, an observation ring and a tassel
            Vector3 lc = Up * 116f;
            Vector3 lr = new Vector3(15f, 14f, 15f);
            gl.Ellipsoid(lc, lr, 16, 8, red, redD);
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j < 8; j++)
                {
                    float t0 = -Mathf.PI * 0.5f + Mathf.PI * j / 8f, t1 = t0 + Mathf.PI / 8f;
                    Vector3 p0 = lc + d * Mathf.Cos(t0) * lr.x * 1.02f + Up * Mathf.Sin(t0) * lr.y * 1.02f;
                    Vector3 p1 = lc + d * Mathf.Cos(t1) * lr.x * 1.02f + Up * Mathf.Sin(t1) * lr.y * 1.02f;
                    PG.Beam(g, p0, p1, 0.32f, 0.32f, gold);
                }
            }
            g.Frustum(lc + Up * 12.6f, lc + Up * 15.2f, 8.5f, 8.5f, 6f, 6f, 16, 0f, gold, Sh(gold, 1.1f), false, true);
            g.Frustum(lc - Up * 12.6f, lc - Up * 15.2f, 8.5f, 8.5f, 6f, 6f, 16, 0f, gold, Sh(gold, 0.9f), false, true);
            g.Frustum(lc - Up * 1.2f, lc + Up * 1.2f, 16.8f, 16.8f, 16.8f, 16.8f, 24, 0f, white, white, true, true);
            gl.Frustum(lc + Up * 1.2f, lc + Up * 4.2f, 15.6f, 15.6f, 14.8f, 14.8f, 24, 0f, C(200, 240, 255), C(230, 250, 255), false, false);
            for (int k = 0; k < 24; k++)
            {
                float a = k * Mathf.PI / 12f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PG.Rod(g, lc - Up * 15.2f + d * 4.5f, lc - Up * 27f + d * 5.5f, 0.18f, k % 2 == 0 ? red : gold, 4);
            }
            g.Frustum(lc - Up * 27f, lc - Up * 28.2f, 6f, 6f, 5f, 5f, 12, 0f, gold, gold, true, true);
            // upper pearl pod, mast rings and the beacon
            gl.Ellipsoid(Up * 158f, new Vector3(6.5f, 5f, 6.5f), 12, 6, C(236, 246, 250), C(200, 226, 236));
            g.Frustum(Up * 157.6f, Up * 158.4f, 7f, 7f, 7f, 7f, 16, 0f, gold, gold, true, true);
            g.Frustum(Up * 176f, Up * 236f, 1.2f, 1.2f, 0.25f, 0.25f, 8, 0f, white, Sh(white, 0.9f), true, false);
            for (int k = 0; k < 4; k++)
            {
                float y = 182f + k * 12f;
                gl.Frustum(Up * y, Up * (y + 1f), 1.6f - k * 0.25f, 1.6f - k * 0.25f, 1.5f - k * 0.25f, 1.5f - k * 0.25f, 8, 0f, k % 2 == 0 ? red : jade, k % 2 == 0 ? red : jade, true, true);
            }
            neon.AddBeacon(new Vector3(c2.x, gy + 237f, c2.y));
            neon.AddBeacon(new Vector3(c2.x, gy + 131.6f, c2.y + 15.5f));
            neon.AddBeacon(new Vector3(c2.x, gy + 131.6f, c2.y - 15.5f));
            ctx.StampSkyline(m, new Vector3(c2.x, gy, c2.y), 0f);

            // fountains either side of the tower inside the circus
            foreach (int s in new[] { -1, 1 })
                Fountain(ctx, new Vector2(c2.x + s * 62f, c2.y), 7.5f, false);
            // plaza lamps around the tower
            var lamp = CityKit.LanternPost();
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI / 5f + 0.3f;
                Vector2 p = c2 + new Vector2(Mathf.Cos(a) * 34f, Mathf.Sin(a) * 26f);
                if (Mathf.Abs(p.x - c2.x) > 50f) continue;
                ctx.Stamp(lamp, new Vector3(p.x, gy, p.y), a * Mathf.Rad2Deg);
                ctx.Capsule(new Vector3(p.x, gy, p.y), 0.25f, 4.4f);
            }
            var tree = CityKit.StreetTree(new System.Random(55), 3, true);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.2f;
                Vector2 p = c2 + new Vector2(Mathf.Cos(a) * 88f, Mathf.Sin(a) * 30f);
                ctx.Stamp(tree, new Vector3(p.x, gy, p.y), k * 45f);
                ctx.Capsule(new Vector3(p.x, gy, p.y), 1.3f, 1.2f);
            }
        }

        // ------------------------------------------------------------------ fountain
        void Fountain(RegionContext ctx, Vector2 c, float r, bool jadeDisc)
        {
            float gy = ctx.Ground(c.x, c.y);
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 rim = C(214, 210, 200), inner = C(70, 120, 120);
            g.Frustum(Up * 0f, Up * 0.7f, r + 0.6f, r + 0.6f, r + 0.6f, r + 0.6f, 24, 0f, rim, rim, false, false);
            g.Frustum(Up * 0.7f, Up * 0.85f, r + 0.8f, r + 0.8f, r + 0.6f, r + 0.6f, 24, 0f, Sh(rim, 1.05f), rim, false, false);
            g.Frustum(Up * 0.85f, Up * 0.85f, r + 0.6f, r + 0.6f, r, r, 24, 0f, rim, rim, false, false);
            g.Frustum(Up * 0.85f, Up * -0.2f, r, r, r, r, 24, 0f, inner, inner, false, true);
            m[PK.Water].Disc(Up * 0.55f, r, 24, 0f, Vector3.up, C(255, 255, 255), C(255, 255, 255));
            // tiered bowls
            g.Frustum(Up * 0f, Up * 2.4f, 1.2f, 1.2f, 0.6f, 0.6f, 10, 0f, rim, rim, false, true);
            g.Frustum(Up * 2.4f, Up * 2.9f, 0.6f, 0.6f, 2.4f, 2.4f, 12, 0f, rim, Sh(rim, 1.1f), true, true);
            if (jadeDisc)
            {
                // the jade "bi" disc: a standing ring with a gold rim
                Color32 jade = C(80, 178, 146), jd = C(46, 124, 102);
                float R0 = 5.4f, R1 = 1.9f, th = 0.7f, yc = 3.2f + R0;
                int seg = 28;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    Vector3 o0 = new Vector3(Mathf.Cos(a0) * R0, yc + Mathf.Sin(a0) * R0, 0f), o1 = new Vector3(Mathf.Cos(a1) * R0, yc + Mathf.Sin(a1) * R0, 0f);
                    Vector3 i0 = new Vector3(Mathf.Cos(a0) * R1, yc + Mathf.Sin(a0) * R1, 0f), i1 = new Vector3(Mathf.Cos(a1) * R1, yc + Mathf.Sin(a1) * R1, 0f);
                    Vector3 z = new Vector3(0f, 0f, th);
                    Color32 cc = (k % 2 == 0) ? jade : Sh(jade, 1.06f);
                    g.QuadOut(i0 + z, i1 + z, o1 + z, o0 + z, Vector3.forward, cc, cc, cc, cc);
                    g.QuadOut(i0 - z, i1 - z, o1 - z, o0 - z, Vector3.back, cc, cc, cc, cc);
                    Vector3 on = (o0 + o1) * 0.5f - Up * yc;
                    g.QuadOut(o0 - z, o1 - z, o1 + z, o0 + z, on, CityKit.Gold, CityKit.Gold, CityKit.Gold, CityKit.Gold);
                    g.QuadOut(i0 - z, i1 - z, i1 + z, i0 + z, -on, jd, jd, jd, jd);
                }
                PG.Box2(g, Up * 1.6f, new Vector3(1.6f, 1.6f, 1.2f), C(60, 64, 70), C(80, 84, 90));
            }
            ctx.Stamp(m, new Vector3(c.x, gy, c.y), jadeDisc ? 90f : 0f);
            ctx.Box(new Vector3(c.x, gy + 0.5f, c.y), new Vector3((r + 0.6f) * 1.6f, 1.2f, (r + 0.6f) * 1.6f), 0f);
            ctx.Box(new Vector3(c.x, gy + 0.5f, c.y), new Vector3((r + 0.6f) * 1.6f, 1.2f, (r + 0.6f) * 1.6f), 45f);
            fountainJets.Add(new Vector3(c.x, gy + 3.0f, c.y));
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                fountainJets.Add(new Vector3(c.x + Mathf.Cos(a) * r * 0.7f, gy + 0.6f, c.y + Mathf.Sin(a) * r * 0.7f));
            }
        }

        // ------------------------------------------------------------------ Gateway Arch over Jade Avenue
        void BuildGatewayArch(RegionContext ctx)
        {
            Vector2 p = CityPlan.GatewayArch;
            float gy = CityPlan.Plat;
            MarkNeon();
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 jade = CityKit.Jade, jadeL = CityKit.JadeLight, gold = CityKit.Gold, white = CityKit.Pearl;
            float half = 9.5f, H = 15f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 b = new Vector3(0f, 0f, s * half);
                PG.Box2(g, b + Up * 0.6f, new Vector3(1.9f, 0.6f, 1.9f), C(186, 182, 172), C(206, 202, 194));
                g.Frustum(b + Up * 1.2f, b + Up * H, 1.25f, 1.25f, 1.0f, 1.0f, 8, Mathf.PI / 8f, jade, jadeL, false, true);
                PG.Beam(gl, b + new Vector3(1.3f, 1.4f, 0f), b + new Vector3(1.05f, H - 1f, 0f), 0.08f, 0.08f, C(140, 255, 220));
                PG.Beam(gl, b + new Vector3(-1.3f, 1.4f, 0f), b + new Vector3(-1.05f, H - 1f, 0f), 0.08f, 0.08f, C(140, 255, 220));
                g.Frustum(b + Up * H, b + Up * (H + 1.2f), 1.5f, 1.5f, 0.6f, 0.6f, 8, 0f, gold, gold, true, false);
                g.Ellipsoid(b + Up * (H + 1.8f), new Vector3(0.6f, 0.6f, 0.6f), 8, 4, gold, Sh(gold, 0.8f));
                ctx.Box(new Vector3(p.x, gy + H * 0.5f, p.y + s * half), new Vector3(3f, H, 3f), 0f);
            }
            // arched lintel (segments), a flat sign beam below with neon glyphs on both faces
            int seg = 12;
            for (int k = 0; k < seg; k++)
            {
                float t0 = k / (float)seg, t1 = (k + 1f) / seg;
                Vector3 a = new Vector3(0f, H + 1.5f + 2.2f * Mathf.Sin(t0 * Mathf.PI), -half - 2.5f + (2f * half + 5f) * t0);
                Vector3 b = new Vector3(0f, H + 1.5f + 2.2f * Mathf.Sin(t1 * Mathf.PI), -half - 2.5f + (2f * half + 5f) * t1);
                PG.Beam(g, a, b, 0.9f, 0.7f, white);
                PG.Beam(gl, a + Up * 0.75f, b + Up * 0.75f, 0.3f, 0.06f, C(255, 200, 90));
            }
            PG.Box2(g, Up * (H - 1.6f), new Vector3(0.5f, 1.4f, half - 1f), C(30, 34, 44), C(30, 34, 44));
            PG.Box2(g, Up * (H + 0.2f), new Vector3(0.7f, 0.25f, half + 1f), gold, gold);
            var rr = new System.Random(88);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 n = new Vector3(side, 0f, 0f), right = new Vector3(0f, 0f, -side);
                CityNeon.Outline(neon.Group(3), Up * (H - 1.6f) + n * 0.5f, right, Up, n, 2f * half - 2.4f, 2.4f, 0.12f, C(255, 196, 80));
                for (int k = 0; k < 4; k++)
                    CityKit.Glyph(neon.Group(k % 2 == 0 ? 0 : 1), Up * (H - 1.6f) + n * 0.5f + right * (-5.4f + k * 3.6f), right, Up, n, 1.9f, 0.2f, k % 2 == 0 ? C(255, 90, 170) : C(80, 240, 255), rr);
            }
            // lanterns hanging from the lintel
            for (int k = -2; k <= 2; k++)
            {
                if (k == 0) continue;
                Vector3 top = new Vector3(0f, H - 3.1f, k * 3.4f);
                PG.Rod(gl, top, top - Up * 0.5f, 0.02f, C(60, 40, 30), 3);
                PG.Lantern(gl, top - Up * 0.5f, 0.55f, 0.9f, C(232, 58, 46), 90);
            }
            // the glyph tubes above were authored around the arch origin: move them into place
            ShiftLast(neon, new Vector3(p.x, gy, p.y));
            ctx.StampSkyline(m, new Vector3(p.x, gy, p.y), 0f);
        }

        /// <summary>The gateway glyphs were added in local space; offset every neon vertex added since the last mark.</summary>
        readonly int[] neonMark = new int[CityNeon.Groups];
        void MarkNeon() { for (int i = 0; i < CityNeon.Groups; i++) neonMark[i] = neon.Group(i).v.Count; }
        void ShiftLast(CityNeon n, Vector3 off)
        {
            for (int i = 0; i < CityNeon.Groups; i++)
            {
                var g = n.Group(i);
                for (int k = neonMark[i]; k < g.v.Count; k++) g.v[k] += off;
                neonMark[i] = g.v.Count;
            }
        }

        // ------------------------------------------------------------------ Old Town
        static PropModel OldHouse(float hw, float hd, System.Random r, out Vector3 sign)
        {
            // two storeys, front at -z, grey tile roof, red columns, glowing shop and lattice windows
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 wall = r.NextDouble() < 0.5 ? Pal.Plaster : C(214, 200, 180), wood = Pal.WoodDark, red = Pal.Red;
            float h1 = 3.6f, h2 = 3.2f;
            PG.Box2(g, new Vector3(0f, 0.2f, 0f), new Vector3(hw, 0.2f, hd), Pal.StoneDark, Pal.Stone);
            PG.Box2(g, new Vector3(0f, 0.4f + h1 * 0.5f, 0.8f), new Vector3(hw - 0.2f, h1 * 0.5f, hd - 0.8f), wall, wall);
            Color32 shop = r.NextDouble() < 0.7 ? C(255, 206, 140) : C(255, 170, 120);
            gl.QuadOut(new Vector3(-hw + 0.8f, 0.5f, -hd + 1.55f), new Vector3(hw - 0.8f, 0.5f, -hd + 1.55f), new Vector3(hw - 0.8f, 3.4f, -hd + 1.55f), new Vector3(-hw + 0.8f, 3.4f, -hd + 1.55f), Vector3.back, shop, shop, Sh(shop, 0.8f), Sh(shop, 0.8f));
            for (int i = 0; i <= 2; i++)
            {
                float x = -hw + 0.4f + (2f * hw - 0.8f) * i / 2f;
                PG.Column(g, new Vector3(x, 0.4f, -hd + 0.4f), new Vector3(x, 0.4f + h1 + h2, -hd + 0.4f), 0.16f, red, 6);
            }
            // upper storey with a balcony and lattice windows
            float y2 = 0.4f + h1;
            PG.Box2(g, new Vector3(0f, y2 + 0.1f, -0.2f), new Vector3(hw, 0.12f, hd - 0.2f), wood, wood);
            PG.Box2(g, new Vector3(0f, y2 + h2 * 0.5f, 0.6f), new Vector3(hw - 0.3f, h2 * 0.5f, hd - 0.9f), wall, wall);
            int wins = Mathf.Max(1, Mathf.RoundToInt(hw / 1.6f));
            for (int i = 0; i < wins; i++)
            {
                float x = -hw + (2f * hw) * (i + 0.5f) / wins;
                gl.QuadOut(new Vector3(x - 0.6f, y2 + 0.8f, -hd + 1.27f), new Vector3(x + 0.6f, y2 + 0.8f, -hd + 1.27f), new Vector3(x + 0.6f, y2 + 2.4f, -hd + 1.27f), new Vector3(x - 0.6f, y2 + 2.4f, -hd + 1.27f), Vector3.back, C(255, 200, 130), C(255, 200, 130), C(255, 214, 150), C(255, 214, 150));
                for (int k = -1; k <= 1; k++) PG.Box2(g, new Vector3(x + k * 0.3f, y2 + 1.6f, -hd + 1.22f), new Vector3(0.03f, 0.8f, 0.03f), Pal.Lattice, Pal.Lattice);
                PG.Box2(g, new Vector3(x, y2 + 1.6f, -hd + 1.22f), new Vector3(0.6f, 0.03f, 0.03f), Pal.Lattice, Pal.Lattice);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -hw + 0.3f + (2f * hw - 0.6f) * i / 8f;
                PG.Box2(g, new Vector3(x, y2 + 0.65f, -hd + 0.25f), new Vector3(0.04f, 0.42f, 0.04f), red, red);
            }
            PG.Box2(g, new Vector3(0f, y2 + 1.08f, -hd + 0.25f), new Vector3(hw, 0.05f, 0.06f), red, red);
            PG.Roof(g, new Vector3(0f, y2 + h2, 0f), hw, hd, 1.9f, 0.9f, 0.45f, Pal.Tile, Pal.Tile2, Pal.TileUnder, Pal.Tile, false);
            for (int s = -1; s <= 1; s += 2)
                PG.Lantern(gl, new Vector3(s * (hw - 1.2f), 0.4f + h1 - 0.2f, -hd + 0.2f), 0.32f, 0.5f, Pal.RedBright, 90);
            sign = new Vector3(0f, 0.4f + h1 - 0.4f, -hd + 0.15f);
            return m;
        }

        static PropModel Pagoda(int tiers, float baseW)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 red = C(196, 52, 42), green = Pal.GreenTile, green2 = Pal.GreenTile2;
            PG.Box2(g, Up * 0.6f, new Vector3(baseW + 2f, 0.6f, baseW + 2f), Pal.StoneDark, Pal.StoneLight);
            float y = 1.2f;
            for (int t = 0; t < tiers; t++)
            {
                float w = baseW * (1f - t * 0.13f);
                float h = t == 0 ? 5f : 3.8f;
                PG.Box2(g, Up * (y + h * 0.5f), new Vector3(w, h * 0.5f, w), red, red);
                for (int k = 0; k < 4; k++)
                {
                    Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                    gl.QuadOut(q * new Vector3(-w * 0.35f, y + 0.8f, -w - 0.03f), q * new Vector3(w * 0.35f, y + 0.8f, -w - 0.03f), q * new Vector3(w * 0.35f, y + h - 1.0f, -w - 0.03f), q * new Vector3(-w * 0.35f, y + h - 1.0f, -w - 0.03f), q * Vector3.back, C(255, 200, 120), C(255, 200, 120), C(255, 214, 150), C(255, 214, 150));
                    for (int c = -1; c <= 1; c += 2) PG.Column(g, q * new Vector3(c * w * 0.85f, y, -w - 0.2f), q * new Vector3(c * w * 0.85f, y + h, -w - 0.2f), 0.18f, Pal.RedDeep, 6);
                }
                y += h;
                PG.Roof(g, Up * y, w, w, 1.6f, 1.6f, 0.7f, green, green2, Pal.TileUnder, CityKit.Gold, false);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        PG.Lantern(gl, new Vector3(sx * (w + 1.3f), y - 0.2f, sz * (w + 1.3f)), 0.28f, 0.42f, Pal.RedBright, 120);
                y += 0.9f;
            }
            g.Frustum(Up * y, Up * (y + 1.2f), 0.7f, 0.7f, 0.5f, 0.5f, 8, 0f, CityKit.Gold, CityKit.Gold, true, true);
            for (int i = 0; i < 5; i++) g.Ellipsoid(Up * (y + 1.8f + i * 0.9f), new Vector3(0.55f - i * 0.07f, 0.28f, 0.55f - i * 0.07f), 8, 3, CityKit.Gold, Sh(CityKit.Gold, 0.8f));
            g.Frustum(Up * (y + 6f), Up * (y + 8.5f), 0.12f, 0.12f, 0.02f, 0.02f, 6, 0f, CityKit.Gold, CityKit.Gold, false, false);
            return m;
        }

        void BuildOldTown(RegionContext ctx)
        {
            var r = new System.Random(2626);
            float gy = CityPlan.Plat, az = CityPlan.LanternAlley[0].y;
            // shophouses on both sides of the alley
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 752f;
                while (x < 838f)
                {
                    float hw = CityKit.R(r, 3.6f, 5.0f), hd = 5.2f;
                    float cx = x + hw;
                    if (cx + hw > 842f) break;
                    float cz = az + side * (3.5f + 1.6f + hd);
                    Vector3 sp;
                    var house = OldHouse(hw, hd, r, out sp);
                    float yaw = side > 0 ? 0f : 180f;
                    ctx.Stamp(house, new Vector3(cx, gy, cz), yaw);
                    ctx.Box(new Vector3(cx, gy + 3.5f, cz + side * 0.6f), new Vector3(2f * hw, 7f, 2f * hd - 1.2f), 0f);
                    // red signboards with gold glyph tubes
                    if (r.NextDouble() < 0.8)
                    {
                        Vector3 n = new Vector3(0f, 0f, -side);
                        Vector3 sw = new Vector3(cx, gy + sp.y, cz) + n * (hd - 0.15f + 0.05f);
                        neon.AddSign(geo, new CitySignSpot { pos = sw, n = n, w = Mathf.Min(5f, hw * 1.2f), h = 1.0f, kind = 1 }, r);
                    }
                    x += 2f * hw + 0.3f;
                }
            }
            // lantern spans across the alley
            for (float x = 760f; x < 840f; x += 13f)
            {
                Vector3 a = new Vector3(x, gy, az - 4.3f), b = new Vector3(x + 2f, gy, az + 4.3f);
                Vector3 o = (a + b) * 0.5f;
                ctx.Stamp(PropShapes.LanternSpan(a - o, b - o, r, 7.2f), o, 0f);
                ctx.Capsule(a, 0.2f, 7f);
                ctx.Capsule(b, 0.2f, 7f);
            }
            // the moon gate over the east end of the alley: the towers of the CBD are framed through it
            {
                Vector2 mg = CityPlan.MoonGate;
                float R = 6.4f, H = 13f, hw = 15f, th = 1.4f;
                float y0 = gy - 0.4f * R - 0.1f;
                var m = new PropModel();
                var g = new LifeGeo();
                PropShapes.MoonWall(g, hw, y0 - gy, H, R, th, Pal.Plaster, C(120, 120, 124));
                PG.Append(m[PK.Landmark], g, Matrix4x4.identity);
                var gm = m[PK.Landmark];
                float topY = y0 - gy + H;
                PG.Box2(gm, new Vector3(0f, topY + 0.25f, 0f), new Vector3(hw + 0.5f, 0.25f, th * 0.5f + 0.5f), Pal.Tile, Pal.Tile2);
                PG.Roof(gm, new Vector3(0f, topY + 0.5f, 0f), hw, th * 0.5f, 1.2f, 0.7f, 0.4f, Pal.Tile, Pal.Tile2, Pal.TileUnder, Pal.Tile, false);
                // gold-lettered plaque and two lanterns
                PG.Box2(gm, new Vector3(0f, topY - 1.6f, -th * 0.5f - 0.08f), new Vector3(2.4f, 0.7f, 0.08f), C(40, 60, 110), C(40, 60, 110));
                var pr = new System.Random(4);
                for (int k = 0; k < 2; k++) CityKit.Glyph(m[PK.Glow], new Vector3(-1.1f + k * 2.2f, topY - 1.6f, -th * 0.5f - 0.16f), Vector3.right, Up, Vector3.back, 1.1f, 0.12f, CityKit.Gold, pr);
                for (int s = -1; s <= 1; s += 2) PG.Lantern(m[PK.Glow], new Vector3(s * (R + 1.4f), topY - 1.4f, -th * 0.5f - 0.6f), 0.5f, 0.8f, Pal.RedBright, 80);
                ctx.Stamp(m, new Vector3(mg.x, gy, mg.y), 90f);
                for (int s = -1; s <= 1; s += 2)
                    ctx.Box(new Vector3(mg.x, gy + 4f, mg.y + s * (hw + 5.2f) * 0.5f), new Vector3(th, 9f, hw - 5.2f), 0f);
            }
            // temple compound: hall facing the alley, a red five-tier pagoda, walls, lions and incense
            {
                Vector2 tp = CityPlan.Temple;
                ctx.Stamp(PropShapes.TempleHall(0.9f, 0f), new Vector3(tp.x - 12f, gy, tp.y + 2f), 180f, 1.35f);
                ctx.Box(new Vector3(tp.x - 12f, gy + 4f, tp.y + 2f), new Vector3(17f, 8f, 13f), 0f);
                var pag = Pagoda(5, 4.6f);
                ctx.StampSkyline(pag, new Vector3(tp.x + 22f, gy, tp.y + 6f), 0f);
                ctx.Box(new Vector3(tp.x + 22f, gy + 6f, tp.y + 6f), new Vector3(11f, 12f, 11f), 0f);
                ctx.Stamp(PropShapes.IncenseBurner(r), new Vector3(tp.x - 12f, gy, tp.y - 14f), 0f);
                ctx.Box(new Vector3(tp.x - 12f, gy + 0.6f, tp.y - 14f), new Vector3(2f, 1.2f, 2f), 0f);
                for (int s = -1; s <= 1; s += 2)
                {
                    ctx.Stamp(PropShapes.StoneLion(s < 0 ? 0 : 1), new Vector3(tp.x - 12f + s * 4f, gy, tp.y - 22f), 180f);
                    ctx.Box(new Vector3(tp.x - 12f + s * 4f, gy + 0.9f, tp.y - 22f), new Vector3(1.1f, 1.8f, 1.4f), 0f);
                }
                // compound walls (red with tile coping) with a gate opening on the alley side
                Color32 wall = C(176, 58, 46);
                System.Action<Vector3, Vector3> wallSeg = (a, b) =>
                {
                    Vector3 d = b - a;
                    float len = d.magnitude;
                    Vector3 mid = (a + b) * 0.5f;
                    float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    geo.Box(PK.Landmark, mid + Up * 1.5f, new Vector3(0.35f, 1.5f, len * 0.5f), yaw, wall);
                    geo.Box(PK.Landmark, mid + Up * 3.1f, new Vector3(0.6f, 0.15f, len * 0.5f + 0.3f), yaw, Pal.Tile);
                    ctx.Box(mid + Up * 1.5f, new Vector3(0.7f, 3f, len), yaw);
                };
                float wx0 = 748f, wx1 = 872f, wz0 = az + 3.5f + 1.6f + 10.4f + 2f, wz1 = 222f;
                wallSeg(new Vector3(wx0, gy, wz1), new Vector3(wx1, gy, wz1));
                wallSeg(new Vector3(wx0, gy, wz0), new Vector3(wx0, gy, wz1));
                wallSeg(new Vector3(wx1, gy, wz0), new Vector3(wx1, gy, wz1));
                wallSeg(new Vector3(wx0, gy, wz0), new Vector3(tp.x - 18f, gy, wz0));
                wallSeg(new Vector3(tp.x - 6f, gy, wz0), new Vector3(wx1, gy, wz0));
                var tree = NatureShapes.Pine(new System.Random(7), 1.2f);
                ctx.Stamp(tree, new Vector3(tp.x - 34f, gy, tp.y + 14f), 30f);
                ctx.Stamp(NatureShapes.BlossomTree(true, new System.Random(8)), new Vector3(tp.x + 4f, gy, tp.y - 10f), 0f);
                ctx.Stamp(NatureShapes.BlossomTree(false, new System.Random(9)), new Vector3(tp.x - 30f, gy, tp.y - 8f), 0f);
                ctx.Capsule(new Vector3(tp.x - 34f, gy, tp.y + 14f), 0.6f, 4f);
                ctx.Capsule(new Vector3(tp.x + 4f, gy, tp.y - 10f), 0.4f, 3f);
                ctx.Capsule(new Vector3(tp.x - 30f, gy, tp.y - 8f), 0.4f, 3f);
            }
            // courtyard houses south of the alley
            for (int k = 0; k < 2; k++)
            {
                Vector3 p = new Vector3(772f + k * 42f, gy, 90f);
                ctx.Stamp(PropShapes.CourtyardHouse(0.5f, 0f, k, r), p, 0f);
                ctx.Box(p + Up * 2f, new Vector3(16f, 4f, 15f), 0f);
            }
        }

        // ------------------------------------------------------------------ Lotus Arena
        void BuildStadium(RegionContext ctx)
        {
            Vector2 c = CityPlan.Stadium;
            Vector2 R = CityPlan.StadiumR;
            float gy = CityPlan.Plat;
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            int seg = 48;
            Color32 white = C(242, 242, 238), seat = C(46, 150, 128), seat2 = C(36, 112, 150), glass = C(150, 210, 226);
            System.Func<float, float, float, Vector3> E = (a, k, y) => new Vector3(Mathf.Cos(a) * R.x * k, y, Mathf.Sin(a) * R.y * k);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 n = new Vector3(Mathf.Cos((a0 + a1) * 0.5f) / R.x, 0f, Mathf.Sin((a0 + a1) * 0.5f) / R.y).normalized;
                // outer facade: white base, glass band, white crown
                g.QuadOut(E(a0, 1f, 0f), E(a1, 1f, 0f), E(a1, 1f, 4f), E(a0, 1f, 4f), n, white, white, white, white);
                gl.QuadOut(E(a0, 1.002f, 4f), E(a1, 1.002f, 4f), E(a1, 1.002f, 11f), E(a0, 1.002f, 11f), n, glass, glass, Sh(glass, 1.1f), Sh(glass, 1.1f));
                g.QuadOut(E(a0, 1f, 11f), E(a1, 1f, 11f), E(a1, 1f, 16f), E(a0, 1f, 16f), n, white, white, white, white);
                PG.Beam(g, E(a0, 1.01f, 0f), E(a0, 1.01f, 16f), 0.3f, 0.3f, white);
                // seating bowl (stepped ring inwards)
                for (int t = 0; t < 4; t++)
                {
                    float k0 = 0.95f - t * 0.08f, k1 = k0 - 0.08f, y0 = 15f - t * 3.4f, y1 = y0 - 3.4f;
                    Color32 sc = (t + i) % 2 == 0 ? seat : seat2;
                    g.QuadOut(E(a0, k0, y0), E(a1, k0, y0), E(a1, k1, y1), E(a0, k1, y1), Up - n * 0.8f, sc, sc, Sh(sc, 0.9f), Sh(sc, 0.9f));
                }
                g.QuadOut(E(a0, 1f, 16f), E(a1, 1f, 16f), E(a1, 0.95f, 15f), E(a0, 0.95f, 15f), Up, white, white, white, white);
                // lotus petal roof: a cantilevered petal per two segments
                if (i % 2 == 0)
                {
                    float am = a1;
                    Vector3 b0 = E(a0, 1.0f, 16f), b1 = E(a0 + 2f * (a1 - a0), 1.0f, 16f);
                    Vector3 tip = E(am, 0.72f, 27f), mid0 = E(a0 + 0.3f * (a1 - a0), 0.88f, 23f), mid1 = E(a0 + 1.7f * (a1 - a0), 0.88f, 23f);
                    Color32 pa = C(250, 246, 248), pb = C(246, 200, 214);
                    PG.Quad2(g, b0, b1, mid1, mid0, pa, pa, pb, pb);
                    g.Tri2(mid0, mid1, tip, pb, pb, C(240, 168, 190));
                    PG.Beam(gl, (b0 + b1) * 0.5f + Up * 0.3f, tip, 0.1f, 0.1f, C(255, 190, 220));
                }
            }
            // pitch
            m[PK.Props].Disc(Up * 0.4f, 1f, 32, 0f, Vector3.up, C(84, 156, 76), C(110, 176, 92));
            var pitch = m[PK.Props];
            for (int i = 0; i < pitch.v.Count; i++) { var v = pitch.v[i]; pitch.v[i] = new Vector3(v.x * R.x * 0.6f, v.y, v.z * R.y * 0.58f); }
            // floodlight masts
            for (int k = 0; k < 4; k++)
            {
                float a = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                Vector3 b = E(a, 1.12f, 0f);
                g.Frustum(b, b + Up * 46f, 0.9f, 0.9f, 0.5f, 0.5f, 6, 0f, CityKit.Steel, CityKit.SteelLight, false, true);
                Vector3 h = b + Up * 46f - new Vector3(b.x, 0f, b.z).normalized * 1.2f;
                PG.Box2(g, h, new Vector3(3f, 1.6f, 0.5f), CityKit.SteelDark, CityKit.SteelDark);
                PG.Box2(gl, h - new Vector3(b.x, 0f, b.z).normalized * 0.55f, new Vector3(2.7f, 1.4f, 0.1f), CityKit.LampCool, CityKit.LampCool);
                ctx.Capsule(new Vector3(c.x + b.x, gy, c.y + b.z), 1f, 46f);
            }
            ctx.StampSkyline(m, new Vector3(c.x, gy, c.y), 0f);
            for (int i = 0; i < 16; i++)
            {
                float a0 = i * Mathf.PI / 8f, a1 = (i + 1) * Mathf.PI / 8f;
                Vector3 p0 = E(a0, 1f, 0f), p1 = E(a1, 1f, 0f);
                Vector3 mid = (p0 + p1) * 0.5f;
                Vector3 d = p1 - p0;
                ctx.Box(new Vector3(c.x + mid.x, gy + 8f, c.y + mid.z), new Vector3(2f, 16f, d.magnitude + 1f), Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            }
        }

        // ------------------------------------------------------------------ metro entrances, plazas, promenade
        static PropModel MetroEntrance()
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 jade = CityKit.Jade, glass = C(170, 214, 220), dark = C(40, 44, 50);
            // stair pit (dark) with steps, glass side walls and a curved jade-framed canopy, opening towards -z
            PG.Box2(g, new Vector3(0f, 0.05f, 0f), new Vector3(2.4f, 0.05f, 5f), dark, dark);
            for (int i = 0; i < 8; i++) PG.Box2(g, new Vector3(0f, 0.08f, -4f + i * 1.1f), new Vector3(2.1f, 0.03f, 0.3f), C(120, 124, 128), C(150, 154, 158));
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(g, new Vector3(s * 2.45f, 0.6f, 0f), new Vector3(0.12f, 0.6f, 5f), C(200, 200, 196), C(214, 214, 210));
                PG.Box2(g, new Vector3(s * 2.45f, 1.9f, 0.6f), new Vector3(0.05f, 0.7f, 4.4f), glass, glass);
            }
            for (int k = 0; k < 8; k++)
            {
                float a0 = k * Mathf.PI / 8f, a1 = (k + 1) * Mathf.PI / 8f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 2.6f, 2.6f + Mathf.Sin(a0) * 1.2f, 0f), p1 = new Vector3(Mathf.Cos(a1) * 2.6f, 2.6f + Mathf.Sin(a1) * 1.2f, 0f);
                Vector3 z = new Vector3(0f, 0f, 5f);
                PG.Quad2(g, p0 - z, p1 - z, p1 + z + new Vector3(0f, 0f, 0.5f), p0 + z + new Vector3(0f, 0f, 0.5f), glass, glass, glass, glass);
                PG.Beam(g, p0 - z, p1 - z, 0.12f, 0.12f, jade);
                PG.Beam(g, p0 + z, p1 + z, 0.12f, 0.12f, jade);
            }
            // round sign on a post: abstract wave logo
            g.Frustum(new Vector3(3.4f, 0f, -5f), new Vector3(3.4f, 4.4f, -5f), 0.08f, 0.08f, 0.08f, 0.08f, 6, 0f, CityKit.Steel, CityKit.Steel, false, true);
            gl.Disc(new Vector3(3.4f, 4.6f, -5.08f), 0.75f, 16, 0f, Vector3.back, C(240, 250, 248), C(40, 190, 150));
            gl.Disc(new Vector3(3.4f, 4.6f, -4.92f), 0.75f, 16, 0f, Vector3.forward, C(240, 250, 248), C(40, 190, 150));
            PG.Box2(gl, new Vector3(3.4f, 4.6f, -5.12f), new Vector3(0.45f, 0.08f, 0.01f), C(30, 120, 100), C(30, 120, 100));
            return m;
        }

        void BuildPlazas(RegionContext ctx)
        {
            var r = new System.Random(1717);
            float gy = CityPlan.Plat;
            // Jade Plaza: bi disc fountain, trees, benches, metro
            Vector2 jp = CityPlan.JadePlaza;
            Fountain(ctx, jp, 10f, true);
            var tree = CityKit.StreetTree(r, 0, true);
            var tree2 = CityKit.StreetTree(r, 2, true);
            var bench = CityKit.Bench();
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f + 0.39f;
                Vector2 p = jp + new Vector2(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 19f);
                ctx.Stamp(k % 2 == 0 ? tree : tree2, new Vector3(p.x, gy, p.y), k * 30f);
                ctx.Capsule(new Vector3(p.x, gy, p.y), 1.35f, 1.2f);
                Vector2 bp = jp + new Vector2(Mathf.Cos(a + 0.39f) * 18f, Mathf.Sin(a + 0.39f) * 13f);
                ctx.Stamp(bench, new Vector3(bp.x, gy, bp.y), Mathf.Atan2(-(bp.x - jp.x), -(bp.y - jp.y)) * Mathf.Rad2Deg + 180f);
            }
            var metro = MetroEntrance();
            foreach (var mp in new[] { new Vector3(858f, gy, -100f), new Vector3(956f, gy, 34f), new Vector3(1196f, gy, 34f) })
            {
                ctx.Stamp(metro, mp, 0f);
                ctx.Box(mp + Up * 1.8f, new Vector3(5.4f, 3.6f, 10.4f), 0f);
            }
            // bollards along the plaza edges
            var bol = CityKit.Bollard();
            for (float x = jp.x - 52f; x <= jp.x + 52f; x += 4f)
                ctx.Stamp(bol, new Vector3(x, gy, -118.5f), 0f);
            ctx.Box(new Vector3(jp.x, gy + 0.45f, -118.5f), new Vector3(106f, 0.9f, 0.4f), 0f);

            // seafront promenade: lantern posts, benches facing the sea, trees, ferry terminal canopy, a fountain row
            float pz0 = -417.5f, pz1 = -436f;
            var lp = CityKit.LanternPost();
            var palm = CityKit.StreetTree(r, 1, true);
            for (float x = 712f; x < 1118f; x += 14f)
            {
                if (Mathf.Abs(x - CityPlan.FerryPier.x) < 22f) continue;
                if (Mathf.Abs(x - 880f) < 9f) continue;
                ctx.Stamp(lp, new Vector3(x, CityPlan.Low, pz0), 180f);
                ctx.Capsule(new Vector3(x, CityPlan.Low, pz0), 0.24f, 4.4f);
                if (((int)(x / 14f)) % 2 == 0)
                {
                    ctx.Stamp(palm, new Vector3(x + 7f, CityPlan.Low, -427f), x);
                    ctx.Capsule(new Vector3(x + 7f, CityPlan.Low, -427f), 1.35f, 1.2f);
                }
                else ctx.Stamp(bench, new Vector3(x + 7f, CityPlan.Low, pz1), 180f);
            }
            // ferry terminal canopy (jade wave roof) and the dancing fountain row in front of it
            {
                Vector2 fp = CityPlan.FerryPier;
                var m = new PropModel();
                var g = m[PK.Landmark];
                var gl = m[PK.Glow];
                for (int s = -1; s <= 1; s += 2)
                    for (int k = -2; k <= 2; k++)
                        PG.Column(g, new Vector3(k * 7f, 0f, s * 5f), new Vector3(k * 7f, 6f, s * 5f), 0.3f, CityKit.Pearl, 8);
                int seg = 16;
                for (int i = 0; i < seg; i++)
                {
                    float x0 = -18f + 36f * i / seg, x1 = -18f + 36f * (i + 1) / seg;
                    float y0 = 6.4f + 1.2f * Mathf.Sin(x0 * 0.35f), y1 = 6.4f + 1.2f * Mathf.Sin(x1 * 0.35f);
                    PG.Quad2(g, new Vector3(x0, y0, -7f), new Vector3(x1, y1, -7f), new Vector3(x1, y1, 7f), new Vector3(x0, y0, 7f), CityKit.Jade, CityKit.Jade, CityKit.JadeLight, CityKit.JadeLight);
                    PG.Beam(gl, new Vector3(x0, y0 - 0.1f, -7.05f), new Vector3(x1, y1 - 0.1f, -7.05f), 0.06f, 0.06f, C(150, 255, 220));
                }
                PG.Box2(g, new Vector3(0f, 1.6f, 3.5f), new Vector3(9f, 1.6f, 1.2f), C(200, 226, 230), CityKit.Pearl);
                PG.Box2(gl, new Vector3(0f, 1.6f, 2.25f), new Vector3(8.6f, 1.2f, 0.05f), C(255, 226, 170), C(255, 226, 170));
                var sr = new System.Random(21);
                CityKit.Glyph(gl, new Vector3(-3f, 5.2f, -5.2f), Vector3.right, Up, Vector3.back, 1.6f, 0.18f, CityKit.Gold, sr);
                CityKit.Glyph(gl, new Vector3(-1f, 5.2f, -5.2f), Vector3.right, Up, Vector3.back, 1.6f, 0.18f, CityKit.Gold, sr);
                CityKit.Glyph(gl, new Vector3(1f, 5.2f, -5.2f), Vector3.right, Up, Vector3.back, 1.6f, 0.18f, CityKit.Gold, sr);
                ctx.Stamp(m, new Vector3(fp.x, CityPlan.Low, -428f), 0f);
                for (int s = -1; s <= 1; s += 2)
                    for (int k = -2; k <= 2; k++) ctx.Capsule(new Vector3(fp.x + k * 7f, CityPlan.Low, -428f + s * 5f), 0.35f, 6f);
                ctx.Box(new Vector3(fp.x, CityPlan.Low + 1.6f, -424.5f), new Vector3(18f, 3.2f, 2.4f), 0f);
                for (int k = 0; k < 9; k++) fountainJets.Add(new Vector3(fp.x - 28f + k * 7f, CityPlan.Low + 0.1f, -440f));
                var grate = new PropModel();
                PG.Box2(grate[PK.Props], Vector3.zero, new Vector3(30f, 0.04f, 1.2f), C(60, 70, 74), C(70, 84, 90));
                ctx.Stamp(grate, new Vector3(fp.x, CityPlan.Low + 0.03f, -440f), 0f);
            }
            // harbour view terrace along Jade Avenue: benches and coin telescopes looking over the harbour
            var scope = new PropModel();
            scope[PK.Props].Frustum(Vector3.zero, Up * 1.1f, 0.08f, 0.08f, 0.06f, 0.06f, 6, 0f, CityKit.Steel, CityKit.Steel, false, true);
            scope[PK.Props].Frustum(new Vector3(0f, 1.25f, -0.3f), new Vector3(0f, 1.35f, 0.4f), 0.12f, 0.12f, 0.16f, 0.16f, 8, 0f, CityKit.Jade, CityKit.Gold, true, true);
            for (float x = 900f; x < 1030f; x += 18f)
            {
                ctx.Stamp(scope, new Vector3(x, gy, -150f), 180f);
                ctx.Stamp(bench, new Vector3(x + 9f, gy, -152f), 180f);
            }
        }
    }
}
