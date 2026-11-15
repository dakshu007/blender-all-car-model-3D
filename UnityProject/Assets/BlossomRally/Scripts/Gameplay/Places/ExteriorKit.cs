// ExteriorKit.cs - what the Places module adds outside: entrance dressing for existing buildings (lantern posts and
// a standing sign with a glowing hanzi panel), painted parking bays with a "P" post, the glass-fronted car
// showroom and the coast scooter shop (complete low-poly buildings), plus small reusable models (scooter, car
// silhouette). Geometry is built in WORLD space into a PGeo (root at the origin), lit by the sun like the regions.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class ExteriorKit
    {
        static readonly Vector3 Up = Vector3.up;
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }

        // ================================================================== entrance dressing
        /// <summary>Lantern posts either side of the door and a standing glyph sign (world space, door on the ground).</summary>
        public static void Entrance(PGeo pg, PlaceSites sites, WorldBuilder w, Vector3 door, float outYaw, char glyph, Color32 board, bool posts = true)
        {
            Quaternion q = Quaternion.Euler(0f, outYaw, 0f);
            Vector3 f = q * Vector3.forward, r = q * Vector3.right;
            if (posts)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 p = door + r * (s * 2.05f) + f * 0.35f;
                    p.y = w.TerrainHeight(p.x, p.z);
                    if (sites != null && !sites.FootprintFree(new Vector2(p.x, p.z), 0.25f, 0.25f, outYaw, p.y, p.y + 2.5f)) continue;
                    LanternPost(pg, p, outYaw, s);
                    pg.Col(p + Up * 1.2f, new Vector3(0.3f, 2.4f, 0.3f), outYaw);
                }
            Vector3 sp = door + r * 3.1f + f * 1.1f;
            sp.y = w.TerrainHeight(sp.x, sp.z);
            if (sites == null || sites.FootprintFree(new Vector2(sp.x, sp.z), 0.5f, 0.3f, outYaw, sp.y, sp.y + 2f))
            {
                StandingSign(pg, sp, outYaw, glyph, board);
                pg.Col(sp + Up * 0.9f, new Vector3(0.9f, 1.8f, 0.3f), outYaw);
            }
        }

        public static void LanternPost(PGeo pg, Vector3 p, float yaw, int side)
        {
            var g = pg.shell;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 r = q * Vector3.right;
            PG.Box2(g, p + Up * 0.12f, new Vector3(0.22f, 0.12f, 0.22f), Pal.StoneDark, Pal.Stone);
            g.Frustum(p, p + Up * 2.35f, 0.08f, 0.08f, 0.07f, 0.07f, 6, 0f, Pal.RedDeep, Pal.Red, false, true);
            Vector3 arm0 = p + Up * 2.2f, arm1 = arm0 - r * (side * 0.55f);
            PG.Beam(g, arm0, arm1, 0.035f, 0.035f, Pal.WoodDark);
            g.Ellipsoid(p + Up * 2.42f, new Vector3(0.09f, 0.1f, 0.09f), 6, 3, Pal.Gold, Pal.GoldDark);
            PG.Rod(pg.glow, arm1, arm1 - Up * 0.18f, 0.01f, PG.A(Pal.Ink, 60), 3);
            PG.Lantern(pg.glow, arm1 - Up * 0.18f, 0.22f, 0.38f, Pal.RedBright, 120);
        }

        /// <summary>Wooden sign on two legs with a glowing glyph panel facing yaw.</summary>
        public static void StandingSign(PGeo pg, Vector3 p, float yaw, char glyph, Color32 board)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = q * Vector3.forward, r = q * Vector3.right;
            var g = pg.shell;
            for (int s = -1; s <= 1; s += 2)
                PG.Box2(g, p + r * (s * 0.36f) + Up * 0.8f, new Vector3(0.04f, 0.8f, 0.04f), Pal.WoodDark, Pal.WoodDark);
            PlaceKit.SignPanel(pg, p + Up * 1.15f + f * 0.02f, f, 0.62f, 0.8f, glyph, board, Pal.Gold);
            PG.OBox(g, p + Up * 1.62f, new Vector3(0.5f, 0.05f, 0.12f), q, Pal.Tile);
        }

        // ================================================================== parking
        /// <summary>White bay lines (2.7 x 5.4) following the ground, plus a small blue "P" post at the head.</summary>
        public static void ParkingBay(PGeo pg, WorldBuilder w, Vector3 c, float yaw, bool post)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = q * Vector3.forward, r = q * Vector3.right;
            float hw = 1.35f, hl = 2.7f;
            Color32 paint = C(240, 240, 236);
            System.Action<Vector3, Vector3> line = (a, b) =>
            {
                a.y = w.TerrainHeight(a.x, a.z) + 0.035f;
                b.y = w.TerrainHeight(b.x, b.z) + 0.035f;
                Vector3 d = b - a;
                Vector3 side = Vector3.Cross(Up, d.normalized) * 0.06f;
                pg.shell.QuadOut(a - side, b - side, b + side, a + side, Up, paint, paint, paint, paint);
            };
            for (int s = -1; s <= 1; s += 2) line(c + r * (s * hw) - f * hl, c + r * (s * hw) + f * hl);
            line(c - r * hw + f * hl, c + r * hw + f * hl);
            if (!post) return;
            Vector3 pp = c + r * (hw + 0.5f) + f * (hl - 0.3f);
            pp.y = w.TerrainHeight(pp.x, pp.z);
            pg.shell.Frustum(pp, pp + Up * 2f, 0.04f, 0.04f, 0.04f, 0.04f, 6, 0f, C(150, 150, 156), C(180, 180, 186), false, true);
            PG.OBox(pg.shell, pp + Up * 2.05f, new Vector3(0.24f, 0.24f, 0.02f), q, C(40, 90, 180));
            PlaceKit.Glyph(pg.shell, pp + Up * 2.05f + f * 0.025f, r, Up, 0.32f, 0.03f, 'P', C(250, 250, 250));
            PlaceKit.Glyph(pg.shell, pp + Up * 2.05f - f * 0.025f, -r, Up, 0.32f, 0.03f, 'P', C(250, 250, 250));
        }

        // ================================================================== small models
        /// <summary>Low-poly scooter (local, origin at the ground between the wheels, facing +Z).</summary>
        public static PropModel Scooter(Color32 body, Color32 seat)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 tyre = C(30, 30, 32), rim = C(190, 190, 196), dark = C(50, 50, 56);
            foreach (float z in new[] { -0.62f, 0.66f })
            {
                g.Frustum(new Vector3(-0.06f, 0.26f, z), new Vector3(0.06f, 0.26f, z), 0.26f, 0.26f, 0.26f, 0.26f, 10, 0f, tyre, tyre, true, true);
                g.Frustum(new Vector3(-0.07f, 0.26f, z), new Vector3(0.07f, 0.26f, z), 0.13f, 0.13f, 0.13f, 0.13f, 8, 0f, rim, rim, true, true);
            }
            PG.Box2(g, new Vector3(0f, 0.36f, 0.05f), new Vector3(0.16f, 0.05f, 0.42f), dark, body);
            PG.OBox(g, new Vector3(0f, 0.72f, 0.5f), new Vector3(0.2f, 0.36f, 0.05f), Quaternion.Euler(-14f, 0f, 0f), body);
            g.Ellipsoid(new Vector3(0f, 0.6f, -0.45f), new Vector3(0.2f, 0.22f, 0.4f), 8, 4, body, Sh(body, 0.8f));
            PG.Box2(g, new Vector3(0f, 0.86f, -0.4f), new Vector3(0.15f, 0.05f, 0.3f), seat, seat);
            PG.Beam(g, new Vector3(0f, 1.0f, 0.56f), new Vector3(0f, 0.3f, 0.68f), 0.03f, 0.03f, dark);
            PG.Beam(g, new Vector3(-0.32f, 1.08f, 0.56f), new Vector3(0.32f, 1.08f, 0.56f), 0.02f, 0.02f, dark);
            g.Ellipsoid(new Vector3(0f, 1.0f, 0.62f), new Vector3(0.12f, 0.1f, 0.08f), 6, 3, body, body);
            m[PK.Glow].Ellipsoid(new Vector3(0f, 1.0f, 0.69f), new Vector3(0.06f, 0.05f, 0.02f), 6, 3, C(255, 246, 220), C(255, 236, 200));
            for (int s = -1; s <= 1; s += 2) g.Ellipsoid(new Vector3(s * 0.34f, 1.08f, 0.56f), new Vector3(0.04f, 0.03f, 0.05f), 5, 2, dark, dark);
            return m;
        }

        /// <summary>Simple low-poly car silhouette (seen through glass), local, facing +Z.</summary>
        public static void CarShape(LifeGeo g, Vector3 p, float yaw, Color32 body)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Color32 glass = C(40, 50, 60), tyre = C(30, 30, 32);
            PG.OBox(g, p + q * new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.3f, 2.1f), q, body);
            PG.OBox(g, p + q * new Vector3(0f, 1.0f, -0.25f), new Vector3(0.78f, 0.22f, 1.05f), q, glass);
            PG.OBox(g, p + q * new Vector3(0f, 1.24f, -0.3f), new Vector3(0.72f, 0.03f, 0.8f), q, body);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 wc = p + q * new Vector3(sx * 0.82f, 0.33f, sz * 1.35f);
                    g.Frustum(wc - q * Vector3.right * 0.12f, wc + q * Vector3.right * 0.12f, 0.33f, 0.33f, 0.33f, 0.33f, 10, 0f, tyre, tyre, true, true);
                }
        }

        // ================================================================== car showroom
        /// <summary>
        /// Glass-fronted showroom. c = footprint centre (ground), yaw = front facing (+Z), floorY / minY = floor level
        /// and lowest ground under the footprint, hx / hz = half width / depth, front = metres of forecourt to the road.
        /// Returns the door point (world, on the floor in front of the glass doors).
        /// </summary>
        public static Vector3 Showroom(PGeo pg, Vector2 c2, float yaw, float floorY, float minY, float hx, float hz, float front)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 o = new Vector3(c2.x, floorY, c2.y);
            System.Func<float, float, float, Vector3> L = (x, y, z) => o + q * new Vector3(x, y, z);
            var g = pg.shell;
            Color32 panel = C(232, 236, 240), panelD = C(196, 202, 210), dark = C(56, 60, 68), stone = C(176, 176, 172), jade = C(40, 170, 140);
            float H = 7.2f, base0 = minY - floorY - 0.6f;
            // plinth + apron in front
            PG.OBox(g, L(0f, (base0) * 0.5f, 0.6f), new Vector3(hx + 0.6f, -base0 * 0.5f + 0.001f, hz + 1.2f), q, stone);
            PG.OBox(g, L(0f, 0.01f, hz + 1.0f), new Vector3(hx + 0.6f, 0.02f, 1.0f), q, C(200, 198, 192));
            pg.Col(L(0f, (base0 - 0.0f) * 0.5f, 0.6f), new Vector3(2f * hx + 1.2f, -base0, 2f * hz + 2.4f), yaw);
            // walls: back + sides (panelled)
            for (int side = 0; side < 3; side++)
            {
                for (int i = 0; i < 6; i++)
                {
                    float t0 = i / 6f, t1 = (i + 1) / 6f;
                    Color32 pc = (i & 1) == 0 ? panel : Sh(panel, 0.97f);
                    if (side == 0)
                        PG.OBox(g, L(Mathf.Lerp(-hx, hx, (t0 + t1) * 0.5f), H * 0.5f, -hz + 0.15f), new Vector3(hx / 6f - 0.02f, H * 0.5f, 0.15f), q, pc);
                    else
                    {
                        float sx = side == 1 ? -1f : 1f;
                        PG.OBox(g, L(sx * (hx - 0.15f), H * 0.5f, Mathf.Lerp(-hz, hz - 1.2f, (t0 + t1) * 0.5f)), new Vector3(0.15f, H * 0.5f, (2f * hz - 1.2f) / 12f - 0.02f), q, pc);
                    }
                }
                // dark base strip
                if (side == 0) PG.OBox(g, L(0f, 0.3f, -hz + 0.12f), new Vector3(hx, 0.3f, 0.18f), q, dark);
                else PG.OBox(g, L((side == 1 ? -1f : 1f) * (hx - 0.12f), 0.3f, -0.6f), new Vector3(0.18f, 0.3f, hz - 0.6f), q, dark);
            }
            // front corner piers
            for (int s = -1; s <= 1; s += 2) PG.OBox(g, L(s * (hx - 0.6f), H * 0.5f, hz - 0.6f), new Vector3(0.6f, H * 0.5f, 0.6f), q, panelD);
            // glass front with mullions and a central double door
            float gz = hz - 0.35f, gx = hx - 1.2f;
            {
                Vector3 n = q * Vector3.forward;
                Vector3 a = L(-gx, 0.15f, gz), b = L(gx, 0.15f, gz), cc = L(gx, H - 0.9f, gz), d = L(-gx, H - 0.9f, gz);
                Color32 gc = new Color32(255, 255, 255, 255);
                pg.glass.QuadOut(a, b, cc, d, n, gc, gc, gc, gc);
                pg.glass.QuadOut(a, b, cc, d, -n, gc, gc, gc, gc);
                int nm = 8;
                for (int i = 0; i <= nm; i++)
                {
                    float x = Mathf.Lerp(-gx, gx, i / (float)nm);
                    PG.OBox(g, L(x, (H - 0.75f) * 0.5f, gz + 0.04f), new Vector3(0.05f, (H - 0.75f) * 0.5f, 0.07f), q, dark);
                }
                PG.OBox(g, L(0f, 3.2f, gz + 0.04f), new Vector3(gx, 0.05f, 0.07f), q, dark);
                PG.OBox(g, L(0f, 0.1f, gz + 0.04f), new Vector3(gx, 0.1f, 0.08f), q, dark);
                // door frame + handles
                PG.OBox(g, L(0f, 2.55f, gz + 0.09f), new Vector3(1.25f, 0.06f, 0.06f), q, C(200, 200, 206));
                for (int s = -1; s <= 1; s += 2)
                {
                    PG.OBox(g, L(s * 1.25f, 1.27f, gz + 0.09f), new Vector3(0.05f, 1.27f, 0.06f), q, C(200, 200, 206));
                    PG.OBox(g, L(s * 0.14f, 1.1f, gz + 0.14f), new Vector3(0.02f, 0.4f, 0.02f), q, C(220, 220, 226));
                }
            }
            // dummy interior seen through the glass: pale floor, back wall, ceiling light strips, podiums + car shapes
            PG.OBox(g, L(0f, 0.02f, 0f), new Vector3(hx - 0.3f, 0.02f, hz - 0.5f), q, C(214, 216, 220));
            PG.OBox(g, L(0f, H - 0.95f, 0f), new Vector3(hx - 0.3f, 0.05f, hz - 0.5f), q, C(236, 238, 240));
            for (int k = 0; k < 4; k++)
                PG.OBox(pg.glow, L(0f, H - 1.02f, -hz + 2f + k * (2f * hz - 3f) / 3f), new Vector3(hx - 1.5f, 0.03f, 0.12f), q, C(255, 250, 236));
            PG.OBox(pg.glow, L(0f, 3.4f, -hz + 0.32f), new Vector3(hx * 0.5f, 0.12f, 0.02f), q, jade);
            Color32[] carCols = { C(200, 40, 50), C(240, 240, 244), C(40, 90, 170), C(30, 30, 34) };
            for (int k = 0; k < 4; k++)
            {
                float x = (k % 2 == 0 ? -1f : 1f) * hx * 0.45f, z = k < 2 ? hz * 0.3f : -hz * 0.35f;
                Vector3 pc = L(x, 0f, z);
                g.Frustum(pc, pc + Up * 0.22f, 2.3f, 2.3f, 2.3f, 2.3f, 16, 0f, C(220, 222, 226), C(240, 242, 244), false, true);
                pg.glow.Frustum(pc + Up * 0.05f, pc + Up * 0.09f, 2.32f, 2.32f, 2.32f, 2.32f, 16, 0f, jade, jade, false, false);
                CarShape(g, pc + Up * 0.22f, yaw + (k % 2 == 0 ? 35f : -35f), carCols[k]);
            }
            // roof slab with a deep canopy over the forecourt + LED soffit line
            PG.OBox(g, L(0f, H + 0.25f, 0.8f), new Vector3(hx + 0.5f, 0.3f, hz + 1.3f), q, panelD);
            PG.OBox(g, L(0f, H + 0.58f, 0.8f), new Vector3(hx + 0.55f, 0.05f, hz + 1.35f), q, dark);
            PG.OBox(pg.glow, L(0f, H - 0.07f, hz + 1.9f), new Vector3(hx + 0.3f, 0.04f, 0.06f), q, C(255, 250, 236));
            for (int s = -1; s <= 1; s += 2) PG.OBox(g, L(s * (hx + 0.2f), H * 0.5f, hz + 1.7f), new Vector3(0.18f, H * 0.5f, 0.18f), q, dark);
            // fascia: jade glowing stripe + big glyph panel
            PG.OBox(g, L(0f, H - 0.45f, hz + 2.12f), new Vector3(hx + 0.5f, 0.45f, 0.06f), q, dark);
            PG.OBox(pg.glow, L(0f, H - 0.7f, hz + 2.2f), new Vector3(hx + 0.3f, 0.06f, 0.02f), q, jade);
            PlaceKit.SignPanel(pg, L(0f, H - 0.25f, hz + 2.25f), q * Vector3.forward, 1.6f, 1.2f, '车', C(30, 34, 44), C(120, 255, 220), false);
            // pylon at the front corner facing the road
            {
                Vector3 pb = L(hx + 1.6f, 0f, hz + 3.2f);
                pb.y = Mathf.Min(pb.y, floorY);
                PG.OBox(g, pb + Up * 4.5f, new Vector3(0.6f, 4.5f, 0.35f), q, dark);
                PG.OBox(pg.glow, pb + Up * 4.5f + q * new Vector3(0f, 0f, 0.36f), new Vector3(0.06f, 4f, 0.01f), q, jade);
                PlaceKit.SignPanel(pg, pb + Up * 7.8f + q * new Vector3(0f, 0f, 0.4f), q * Vector3.forward, 1.0f, 1.0f, '车', C(250, 250, 250), C(20, 140, 110), true);
                PlaceKit.SignPanel(pg, pb + Up * 7.8f - q * new Vector3(0f, 0f, 0.4f), q * Vector3.back, 1.0f, 1.0f, '车', C(250, 250, 250), C(20, 140, 110), true);
                pg.Col(pb + Up * 4.5f, new Vector3(1.2f, 9f, 0.7f), yaw);
            }
            // building collider (glass included) + canopy posts
            pg.Col(L(0f, H * 0.5f + 0.2f, -0.2f), new Vector3(2f * hx, H + 0.4f, 2f * hz - 0.3f), yaw);
            for (int s = -1; s <= 1; s += 2) pg.Col(L(s * (hx + 0.2f), H * 0.5f, hz + 1.7f), new Vector3(0.4f, H, 0.4f), yaw);
            // planters along the front
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 pp = L(s * (hx - 3.2f), 0f, hz + 1.2f);
                PG.OBox(g, pp + Up * 0.3f, new Vector3(1.4f, 0.3f, 0.4f), q, dark);
                int st = g.v.Count;
                g.Ellipsoid(pp + Up * 0.75f, new Vector3(1.35f, 0.32f, 0.38f), 8, 4, C(96, 150, 80), C(70, 120, 60));
                PG.Lumpy(g, st, pp + Up * 0.75f, 0.15f, s * 2f);
                pg.Col(pp + Up * 0.45f, new Vector3(2.8f, 0.9f, 0.8f), yaw);
            }
            return L(0f, 0f, hz + 0.9f);
        }

        // ================================================================== scooter shop
        public static Vector3 BikeShop(PGeo pg, Vector2 c2, float yaw, float floorY, float minY, float hx, float hz, System.Random rng)
        {
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 o = new Vector3(c2.x, floorY, c2.y);
            System.Func<float, float, float, Vector3> L = (x, y, z) => o + q * new Vector3(x, y, z);
            var g = pg.shell;
            Color32 wall = C(246, 242, 232), trim = C(60, 140, 170), base0c = C(150, 150, 146), shutter = C(176, 180, 186), dark = C(54, 56, 62);
            float H = 6.4f, b0 = minY - floorY - 0.5f;
            PG.OBox(g, L(0f, b0 * 0.5f, 0.5f), new Vector3(hx + 0.4f, -b0 * 0.5f + 0.001f, hz + 1f), q, base0c);
            pg.Col(L(0f, b0 * 0.5f, 0.5f), new Vector3(2f * hx + 0.8f, -b0, 2f * hz + 2f), yaw);
            // body
            PG.OBox(g, L(0f, H * 0.5f, -0.3f), new Vector3(hx, H * 0.5f, hz - 0.3f), q, wall);
            PG.OBox(g, L(0f, 0.25f, -0.3f), new Vector3(hx + 0.02f, 0.25f, hz - 0.28f), q, base0c);
            PG.OBox(g, L(0f, H + 0.15f, -0.3f), new Vector3(hx + 0.15f, 0.15f, hz - 0.15f), q, trim);
            PG.OBox(g, L(0f, 3.25f, hz - 0.58f), new Vector3(hx + 0.05f, 0.1f, 0.04f), q, trim);
            // shop front: half-open roller shutter and a warm lit interior behind it
            float fz = hz - 0.58f;
            PG.OBox(pg.glow, L(0f, 1.3f, fz - 0.04f), new Vector3(hx - 1.4f, 1.15f, 0.02f), q, C(255, 226, 170));
            for (int i = 0; i < 9; i++)
                PG.OBox(g, L(0f, 2.55f + i * 0.07f, fz + 0.02f), new Vector3(hx - 1.3f, 0.032f, 0.03f), q, i % 2 == 0 ? shutter : Sh(shutter, 0.9f));
            for (int s = -1; s <= 1; s += 2) PG.OBox(g, L(s * (hx - 0.7f), 1.5f, fz + 0.02f), new Vector3(0.6f, 1.5f, 0.04f), q, wall);
            // silhouettes of scooters inside
            for (int k = 0; k < 3; k++)
            {
                var sc = Scooter(PropShapes.Silks[(k * 2 + 1) % PropShapes.Silks.Length], C(40, 40, 44));
                pg.Add(sc, L(-2.4f + k * 2.4f, 0f, fz - 1.2f), yaw + 60f);
            }
            // upper floor windows with shutters
            for (int k = 0; k < 3; k++)
            {
                Vector3 wc = L(-hx * 0.62f + k * hx * 0.62f, 4.6f, fz + 0.01f);
                PG.OBox(pg.glow, wc, new Vector3(0.55f, 0.65f, 0.02f), q, C(255, 222, 160));
                PG.OBox(g, wc + q * new Vector3(0f, 0f, 0.03f), new Vector3(0.62f, 0.05f, 0.03f), q, trim);
                for (int s = -1; s <= 1; s += 2) PG.OBox(g, wc + q * new Vector3(s * 0.85f, 0f, 0.05f), new Vector3(0.24f, 0.7f, 0.03f), q, trim);
                PG.OBox(g, wc + q * new Vector3(0f, -0.72f, 0.12f), new Vector3(0.7f, 0.04f, 0.14f), q, wall);
            }
            // striped awning (gentle wind sway in the vertex alpha)
            {
                float ay = 3.0f, az0 = fz + 0.05f, az1 = fz + 1.5f, ay1 = 2.55f;
                int ns = 10;
                for (int i = 0; i < ns; i++)
                {
                    float x0 = Mathf.Lerp(-hx + 0.3f, hx - 0.3f, i / (float)ns), x1 = Mathf.Lerp(-hx + 0.3f, hx - 0.3f, (i + 1) / (float)ns);
                    Color32 sc = PG.A(i % 2 == 0 ? trim : C(250, 250, 246), 30);
                    Color32 sc2 = PG.A(sc, 80);
                    PG.Quad2(g, L(x0, ay, az0), L(x1, ay, az0), L(x1, ay1, az1), L(x0, ay1, az1), sc, sc, sc2, sc2);
                    PG.Quad2(g, L(x0, ay1, az1), L(x1, ay1, az1), L(x1, ay1 - 0.25f, az1 + 0.02f), L(x0, ay1 - 0.25f, az1 + 0.02f), sc2, sc2, sc2, sc2);
                }
            }
            // sign over the awning
            PlaceKit.SignPanel(pg, L(0f, 3.75f, fz + 0.12f), q * Vector3.forward, 1.1f, 0.75f, '车', C(250, 250, 246), C(30, 120, 160), true);
            // a tyre stack + a parked scooter out front
            for (int k = 0; k < 3; k++)
            {
                Vector3 tp = L(hx - 0.7f, 0.12f + k * 0.2f, hz + 0.3f);
                g.Frustum(tp - Up * 0.1f, tp + Up * 0.1f, 0.34f, 0.34f, 0.34f, 0.34f, 10, 0f, dark, dark, true, true);
            }
            pg.Col(L(hx - 0.7f, 0.35f, hz + 0.3f), new Vector3(0.7f, 0.7f, 0.7f), yaw);
            pg.Add(Scooter(C(240, 120, 80), C(40, 40, 44)), L(-hx + 1f, 0f, hz + 0.6f), yaw + 100f);
            pg.Col(L(-hx + 1f, 0.5f, hz + 0.6f), new Vector3(0.5f, 1f, 1.5f), yaw + 100f);
            pg.Col(L(0f, H * 0.5f, -0.3f), new Vector3(2f * hx, H, 2f * hz - 0.6f), yaw);
            return L(0f, 0f, hz + 0.3f);
        }

        // ================================================================== display car (real model, no physics)
        /// <summary>A car model from Resources (CarFactory's model + materials, no Rigidbody / controls / audio).</summary>
        public static GameObject DisplayCar(string id, Vector3 pos, float yaw, Transform parent, bool collider)
        {
            var prefab = Resources.Load<GameObject>("Cars/" + id + "/" + id + "_Export");
            if (prefab == null) return null;
            var root = new GameObject("DisplayCar_" + id);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var model = Object.Instantiate(prefab, root.transform, false);
            model.name = id + "_Model";
            AxisFix.Calibrate();
            AxisFix.Apply(model.transform);
            foreach (var an in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(an);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            MaterialLibrary.Remap(model, id);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.gameObject.layer = CarExtras.CarLayer;
            }
            if (collider)
            {
                var box = new GameObject("Col");
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                box.AddComponent<BoxCollider>().size = new Vector3(2f, 1.5f, 4.5f);
            }
            return root;
        }
    }
}
