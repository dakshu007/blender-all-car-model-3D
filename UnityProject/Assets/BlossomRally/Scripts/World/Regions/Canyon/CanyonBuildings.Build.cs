// CanyonBuildings.Build.cs - built things of the Red Canyon: the Silk Road beacon tower and the caravanserai ruins
// in rammed earth, the Crescent Spring tea-house, well and pavilion, Han-dynasty wall remnants, the gorge trestle
// bridge, a wrecked old truck, camels (static and walking parts), telegraph poles, signposts and marker stones.
// All models are in local space (origin on the ground, +z forward); colliders are added by the caller.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class CanyonBuildings
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        public static readonly Color32 Earth = C(206, 162, 116), EarthLight = C(224, 186, 140), EarthDark = C(168, 124, 88), Reed = C(196, 176, 120);

        /// <summary>Rammed-earth block with horizontal ramming layers (darker reed courses every ~0.9 m).</summary>
        public static void Rammed(LifeGeo g, Vector3 min, Vector3 max, System.Random rng)
        {
            float y = min.y;
            int layer = 0;
            while (y < max.y - 0.01f)
            {
                float y1 = Mathf.Min(max.y, y + 0.9f);
                Color32 c = (layer & 1) == 0 ? Earth : C(214, 172, 126);
                c = PG.Sh(c, R(rng, 0.95f, 1.04f));
                PG.Box2(g, new Vector3((min.x + max.x) * 0.5f, (y + y1) * 0.5f, (min.z + max.z) * 0.5f),
                        new Vector3((max.x - min.x) * 0.5f, (y1 - y) * 0.5f, (max.z - min.z) * 0.5f), c, EarthLight);
                if (y1 < max.y - 0.05f)
                    PG.BoxMM(g, new Vector3(min.x - 0.03f, y1 - 0.05f, min.z - 0.03f), new Vector3(max.x + 0.03f, y1 + 0.03f, max.z + 0.03f), Reed);
                y = y1;
                layer++;
            }
        }

        /// <summary>Row of merlons along x from x0 to x1 on top of a wall (top y, centred on z, thickness th).</summary>
        static void Merlons(LifeGeo g, float x0, float x1, float y, float z, float th, System.Random rng, float missing)
        {
            for (float x = x0 + 0.4f; x < x1 - 0.6f; x += 1.6f)
            {
                if (rng.NextDouble() < missing) continue;
                float h = R(rng, 0.7f, 1.0f);
                PG.Box2(g, new Vector3(x + 0.45f, y + h * 0.5f, z), new Vector3(0.45f, h * 0.5f, th * 0.5f), PG.Sh(Earth, R(rng, 0.92f, 1.02f)), EarthLight);
            }
        }

        // ------------------------------------------------------------------ beacon tower (烽火台)
        /// <summary>Tapered rammed-earth beacon tower with a crenellated top, lookout hut, ladder, brazier and banner.</summary>
        public static PropModel Watchtower(System.Random rng, out Vector3 brazier)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var pr = m[PK.Props];
            const float H = 14f;
            int layers = 16;
            for (int i = 0; i < layers; i++)
            {
                float t0 = i / (float)layers, t1 = (i + 1) / (float)layers;
                float h0 = 5.2f - 1.9f * t0, h1 = 5.2f - 1.9f * t1;
                float y0 = -1f + (H + 1f) * t0, y1 = -1f + (H + 1f) * t1;
                Color32 c = (i & 1) == 0 ? Earth : C(214, 172, 126);
                g.Frustum(new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f), h0 * 1.414f, h0 * 1.414f, h1 * 1.414f, h1 * 1.414f, 4, Mathf.PI * 0.25f, PG.Sh(c, 0.96f), c, false, i == layers - 1);
                if (i > 0) g.Frustum(new Vector3(0f, y0 - 0.04f, 0f), new Vector3(0f, y0 + 0.06f, 0f), h0 * 1.43f, h0 * 1.43f, h0 * 1.43f, h0 * 1.43f, 4, Mathf.PI * 0.25f, Reed, Reed, false, false);
            }
            // erosion bites at the corners
            for (int i = 0; i < 5; i++)
            {
                float y = R(rng, 1f, H - 2f), side = 5.2f - 1.9f * y / H;
                int s0 = g.v.Count;
                Vector3 p = new Vector3((rng.Next(2) * 2 - 1) * side, y, (rng.Next(2) * 2 - 1) * side);
                g.Ellipsoid(p, new Vector3(0.9f, 0.7f, 0.9f), 6, 3, EarthDark, PG.Sh(EarthDark, 0.85f));
                PG.Lumpy(g, s0, p, 0.2f, i);
            }
            // parapet
            float s = 3.3f;
            for (int k = 0; k < 4; k++)
            {
                var wall = new LifeGeo();
                PG.BoxMM(wall, new Vector3(-s, H, -0.35f), new Vector3(s, H + 0.9f, 0.35f), Earth);
                Merlons(wall, -s, s, H + 0.9f, 0f, 0.7f, rng, 0.15f);
                PropShapes.Place(g, wall, new Vector3(Mathf.Sin(k * Mathf.PI * 0.5f) * (s - 0.35f), 0f, Mathf.Cos(k * Mathf.PI * 0.5f) * (s - 0.35f)), k * 90f);
            }
            // lookout hut with a little tiled roof
            PG.BoxMM(g, new Vector3(-1.4f, H, -1.6f), new Vector3(1.4f, H + 2.4f, 1.0f), Pal.Wood);
            PG.BoxMM(g, new Vector3(-0.5f, H, 0.98f), new Vector3(0.5f, H + 1.9f, 1.04f), Pal.WoodDark);
            PG.Roof(g, new Vector3(0f, H + 2.4f, -0.3f), 1.5f, 1.4f, 0.8f, 0.5f, 0.25f, Pal.Tile, Pal.Tile2, Pal.TileUnder, PG.Sh(Pal.Tile, 0.75f), false);
            // brazier on the open corner + brushwood stack
            brazier = new Vector3(2.2f, H + 1.2f, 2.2f);
            pr.Frustum(new Vector3(2.2f, H, 2.2f), brazier, 0.5f, 0.5f, 0.7f, 0.7f, 8, 0f, C(70, 60, 54), C(96, 82, 70), false, false);
            var gl = m[PK.Glow];
            gl.Ellipsoid(brazier + Up * 0.15f, new Vector3(0.55f, 0.3f, 0.55f), 6, 3, C(255, 170, 70, 40), C(255, 120, 40, 40));
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-2.3f + (i % 3) * 0.6f, H + 0.25f + (i / 3) * 0.4f, 2.0f);
                PG.Beam(pr, p + new Vector3(0f, 0f, -0.8f), p + new Vector3(0.1f, 0.05f, 0.8f), 0.18f, 0.18f, C(150, 120, 80));
            }
            // ladder up the side
            for (int sd = -1; sd <= 1; sd += 2) PG.Beam(pr, new Vector3(sd * 0.35f, 0f, -5.4f), new Vector3(sd * 0.35f, H + 0.8f, -3.6f), 0.05f, 0.05f, Pal.WoodDark);
            for (int i = 1; i < 26; i++)
            {
                float t = i / 26f;
                PG.Beam(pr, new Vector3(-0.35f, t * (H + 0.8f), -5.4f + 1.8f * t), new Vector3(0.35f, t * (H + 0.8f), -5.4f + 1.8f * t), 0.03f, 0.03f, Pal.Wood);
            }
            // banner pole
            Vector3 fp = new Vector3(-2.6f, H, 2.6f);
            PG.Column(pr, fp, fp + Up * 6f, 0.07f, Pal.WoodDark, 5);
            var st = m[PK.Strong];
            for (int k = 0; k < 4; k++)
            {
                float x0 = k * 0.6f, x1 = x0 + 0.6f;
                Color32 c0 = PG.A(Pal.RedBright, k * 60), c1 = PG.A(Pal.RedBright, k * 60 + 60);
                PG.Quad2(st, fp + new Vector3(0f, 5.9f, 0f) + new Vector3(x0, 0f, -x0 * 0.6f), fp + new Vector3(0f, 5.9f, 0f) + new Vector3(x1, 0f, -x1 * 0.6f),
                         fp + new Vector3(0f, 4.6f, 0f) + new Vector3(x1, 0f, -x1 * 0.6f), fp + new Vector3(0f, 4.6f, 0f) + new Vector3(x0, 0f, -x0 * 0.6f), c0, c1, c1, c0);
            }
            // rubble at the foot
            for (int i = 0; i < 9; i++)
            {
                float a = R(rng, 0f, 6.28f), r = R(rng, 5.6f, 8f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0.1f, Mathf.Sin(a) * r);
                int s0 = pr.v.Count;
                pr.Ellipsoid(p, new Vector3(R(rng, 0.4f, 0.9f), R(rng, 0.25f, 0.45f), R(rng, 0.4f, 0.9f)), 5, 3, Earth, EarthDark);
                PG.Lumpy(pr, s0, p, 0.2f, i);
            }
            return m;
        }

        // ------------------------------------------------------------------ caravanserai ruins
        /// <summary>
        /// 44 x 44 m walled compound (centre at origin), gate in the +z wall. Returns wall boxes (centre, half size)
        /// in local space for colliders, and spots for resting camels / stalls.
        /// </summary>
        public static PropModel Caravanserai(System.Random rng, List<Vector4> walls, List<Vector3> camelSpots)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var pr = m[PK.Props];
            var st = m[PK.Strong];
            const float S = 22f, T = 1.6f, WH = 5.2f;
            for (int side = 0; side < 4; side++)
            {
                var wl = new LifeGeo();
                // wall along local x at z = S (then rotated); gaps: gate on side 0, a collapsed breach on side 2
                List<Vector2> parts = new List<Vector2>();
                if (side == 0) { parts.Add(new Vector2(-S, -2.6f)); parts.Add(new Vector2(2.6f, S)); }
                else if (side == 2) { parts.Add(new Vector2(-S, 4f)); parts.Add(new Vector2(11f, S)); }
                else parts.Add(new Vector2(-S, S));
                foreach (var p in parts)
                {
                    float x0 = p.x, x1 = p.y;
                    // uneven ruined top: split into chunks of varying height
                    for (float x = x0; x < x1 - 0.01f;)
                    {
                        float xe = Mathf.Min(x1, x + R(rng, 4f, 8f));
                        float h = WH * (rng.NextDouble() < 0.25 ? R(rng, 0.55f, 0.8f) : R(rng, 0.92f, 1.02f));
                        Rammed(wl, new Vector3(x, -0.6f, S - T), new Vector3(xe, h, S), rng);
                        if (h > WH * 0.9f) Merlons(wl, x, xe, h, S - T * 0.25f, T * 0.4f, rng, 0.3f);
                        x = xe;
                    }
                    Vector3 c = new Vector3((x0 + x1) * 0.5f, WH * 0.5f, S - T * 0.5f);
                    Vector3 hs = new Vector3((x1 - x0) * 0.5f, WH * 0.5f, T * 0.5f);
                    float a = side * 90f * Mathf.Deg2Rad;
                    Vector3 rc = new Vector3(c.x * Mathf.Cos(a) + c.z * Mathf.Sin(a), c.y, -c.x * Mathf.Sin(a) + c.z * Mathf.Cos(a));
                    walls.Add(new Vector4(rc.x, rc.z, side % 2 == 0 ? hs.x : hs.z, side % 2 == 0 ? hs.z : hs.x));
                }
                PropShapes.Place(g, wl, Vector3.zero, side * 90f);
            }
            // breach rubble on side 2 (z = -S)
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = new Vector3(R(rng, -1f, 6f), 0.2f, -S + R(rng, -3f, 2f));
                int s0 = g.v.Count;
                g.Ellipsoid(p, new Vector3(R(rng, 0.5f, 1.3f), R(rng, 0.3f, 0.7f), R(rng, 0.5f, 1.1f)), 6, 3, Earth, EarthDark);
                PG.Lumpy(g, s0, p, 0.2f, i);
            }
            // corner bastions
            for (int k = 0; k < 4; k++)
            {
                float x = (k & 1) == 0 ? -S : S, z = (k & 2) == 0 ? -S : S;
                float h = WH + R(rng, 0.8f, 2.4f);
                g.Frustum(new Vector3(x, -0.6f, z), new Vector3(x, h, z), 3.4f, 3.4f, 2.8f, 2.8f, 4, Mathf.PI * 0.25f, Earth, EarthLight, false, true);
                for (float y = 0.9f; y < h; y += 0.9f)
                    g.Frustum(new Vector3(x, y - 0.04f, z), new Vector3(x, y + 0.04f, z), 3.42f - y * 0.06f, 3.42f - y * 0.06f, 3.42f - y * 0.06f, 3.42f - y * 0.06f, 4, Mathf.PI * 0.25f, Reed, Reed, false, false);
                walls.Add(new Vector4(x, z, 2.4f, 2.4f));
            }
            // gate: tall pylons, a timber lintel with protruding beams, one leaf hanging open
            for (int s = -1; s <= 1; s += 2)
            {
                Rammed(g, new Vector3(s * 2.6f - (s > 0 ? 0f : 2.2f), -0.6f, S - T - 0.6f), new Vector3(s * 2.6f + (s > 0 ? 2.2f : 0f), WH + 1.8f, S + 0.6f), rng);
            }
            PG.BoxMM(g, new Vector3(-2.8f, WH - 0.2f, S - T - 0.3f), new Vector3(2.8f, WH + 0.35f, S + 0.3f), Pal.WoodDark);
            for (int i = 0; i < 5; i++) PG.Beam(g, new Vector3(-2.4f + i * 1.2f, WH + 0.6f, S - 1f), new Vector3(-2.4f + i * 1.2f, WH + 0.6f, S + 1.2f), 0.12f, 0.12f, Pal.Wood);
            // a pair of red lanterns under the gate lintel (lit at night)
            var gl = m[PK.Glow];
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Rod(gl, new Vector3(s * 1.6f, WH - 0.2f, S + 0.5f), new Vector3(s * 1.6f, WH - 0.7f, S + 0.5f), 0.01f, PG.A(Pal.Ink, 50), 3);
                PG.Lantern(gl, new Vector3(s * 1.6f, WH - 0.7f, S + 0.5f), 0.3f, 0.5f, Pal.RedBright, 110);
            }
            PG.BoxMM(g, new Vector3(-2.5f, 0f, S + 0.1f), new Vector3(-0.2f, 4.6f, S + 0.3f), Pal.Wood);
            var leaf = new LifeGeo();
            PG.BoxMM(leaf, new Vector3(0f, 0f, 0f), new Vector3(2.3f, 4.6f, 0.2f), Pal.Wood);
            for (int i = 0; i < 4; i++) PG.BoxMM(leaf, new Vector3(0.1f, 0.5f + i * 1.1f, 0.2f), new Vector3(2.2f, 0.62f + i * 1.1f, 0.26f), Pal.BronzeDark);
            PropShapes.Place(g, leaf, new Vector3(2.5f, 0f, S - 0.2f), -110f);
            // inner rooms along the west and east walls (low ruined walls), a stable lean-to on the south
            for (int s = -1; s <= 1; s += 2)
                for (int r = 0; r < 4; r++)
                {
                    float z0 = -S + T + 1f + r * 9.5f;
                    float h = R(rng, 1.2f, 3.4f);
                    Rammed(g, new Vector3(s > 0 ? S - T - 7f : -S + T, -0.3f, z0), new Vector3(s > 0 ? S - T : -S + T + 7f, h, z0 + 0.8f), rng);
                    Rammed(g, new Vector3(s > 0 ? S - T - 7.6f : -S + T + 6.8f, -0.3f, z0), new Vector3(s > 0 ? S - T - 6.8f : -S + T + 7.6f, h * R(rng, 0.5f, 1f), z0 + 5f), rng);
                    walls.Add(new Vector4(s * (S - T - 3.5f), z0 + 0.4f, 3.5f, 0.4f));
                }
            // awning stalls in the courtyard (cloth sways)
            Color32[] cloth = { C(200, 70, 56), C(64, 112, 168), C(232, 196, 100), C(226, 222, 206) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = new Vector3(-8f + i * 7.5f, 0f, -8f + (i % 2) * 5f);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = c + new Vector3((k & 1) == 0 ? -1.6f : 1.6f, 0f, (k & 2) == 0 ? -1.2f : 1.2f);
                    PG.Column(pr, p, p + Up * ((k & 2) == 0 ? 2.1f : 2.5f), 0.06f, Pal.WoodDark, 5);
                }
                Color32 cc = cloth[i % cloth.Length];
                PG.Quad2(st, c + new Vector3(-1.8f, 2.1f, -1.4f), c + new Vector3(1.8f, 2.1f, -1.4f), c + new Vector3(1.8f, 2.5f, 1.4f), c + new Vector3(-1.8f, 2.5f, 1.4f),
                         PG.A(cc, 140), PG.A(cc, 140), PG.A(cc, 40), PG.A(cc, 40));
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = c + new Vector3(R(rng, -1.2f, 1.2f), 0f, R(rng, -0.9f, 0.9f));
                    if (k % 2 == 0) PG.Box2(pr, p + Up * 0.3f, new Vector3(0.35f, 0.3f, 0.3f), C(150, 112, 74), C(176, 136, 92));
                    else { pr.Frustum(p, p + Up * 0.7f, 0.25f, 0.25f, 0.32f, 0.32f, 7, 0f, C(170, 104, 70), C(196, 128, 86), true, false); pr.Frustum(p + Up * 0.7f, p + Up * 0.85f, 0.32f, 0.32f, 0.16f, 0.16f, 7, 0f, C(196, 128, 86), C(170, 104, 70), false, true); }
                }
                walls.Add(new Vector4(c.x, c.z, 1.8f, 1.4f));
            }
            // courtyard well
            pr.Frustum(new Vector3(6f, 0f, 6f), new Vector3(6f, 0.8f, 6f), 1.1f, 1.1f, 1.1f, 1.1f, 10, 0f, Pal.StoneDark, Pal.Stone, false, false);
            pr.Disc(new Vector3(6f, 0.75f, 6f), 0.85f, 10, 0f, Up, C(40, 34, 30), C(60, 50, 44));
            walls.Add(new Vector4(6f, 6f, 1.1f, 1.1f));
            camelSpots.Add(new Vector3(-4f, 0f, 9f)); camelSpots.Add(new Vector3(1f, 0f, 12f)); camelSpots.Add(new Vector3(12f, 0f, -6f));
            // fallen beams and scattered jars outside
            for (int i = 0; i < 6; i++)
            {
                float a = R(rng, 0f, 6.28f);
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * R(rng, 24f, 28f);
                PG.Beam(pr, p + Up * 0.12f, p + new Vector3(R(rng, -2f, 2f), 0.12f, R(rng, -2f, 2f)), 0.12f, 0.12f, C(110, 86, 62));
            }
            return m;
        }

        // ------------------------------------------------------------------ Han wall remnant (汉长城)
        /// <summary>Ruined segment of rammed earth and reed wall, length L along +x, centred.</summary>
        public static PropModel HanWall(System.Random rng, float L)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            float x = -L * 0.5f;
            while (x < L * 0.5f)
            {
                float xe = Mathf.Min(L * 0.5f, x + R(rng, 2.5f, 5f));
                float h = R(rng, 1.4f, 3.6f);
                if (rng.NextDouble() < 0.2) h *= 0.4f;
                Rammed(g, new Vector3(x, -0.5f, -1.3f + R(rng, 0f, 0.3f)), new Vector3(xe, h, 1.3f - R(rng, 0f, 0.3f)), rng);
                x = xe;
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(R(rng, -L * 0.5f, L * 0.5f), 0.15f, R(rng, -2.5f, 2.5f));
                int s0 = g.v.Count;
                g.Ellipsoid(p, new Vector3(R(rng, 0.4f, 0.9f), 0.3f, R(rng, 0.4f, 0.9f)), 5, 3, Earth, EarthDark);
                PG.Lumpy(g, s0, p, 0.2f, i);
            }
            return m;
        }

        // ------------------------------------------------------------------ Crescent Spring tea-house
        /// <summary>Small adobe tea-house with a grey tiled roof, a shaded terrace towards +z, lanterns and a tea flag.</summary>
        public static PropModel TeaHouse(System.Random rng, List<Vector3> steam)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            var pr = m[PK.Props];
            var st = m[PK.Strong];
            PG.Box2(g, new Vector3(0f, -0.2f, 1.5f), new Vector3(6.2f, 0.45f, 6.2f), Pal.StoneDark, C(206, 176, 136));
            Color32 adobe = C(222, 186, 146), adobeD = C(196, 156, 116);
            // hall
            PG.BoxMM(g, new Vector3(-4.8f, 0.25f, -4.2f), new Vector3(4.8f, 3.6f, -3.9f), adobe);
            PG.BoxMM(g, new Vector3(-4.8f, 0.25f, -4.2f), new Vector3(-4.5f, 3.6f, 0.6f), adobe);
            PG.BoxMM(g, new Vector3(4.5f, 0.25f, -4.2f), new Vector3(4.8f, 3.6f, 0.6f), adobe);
            PG.BoxMM(g, new Vector3(-4.8f, 0.25f, 0.4f), new Vector3(-1.1f, 3.6f, 0.6f), adobeD);
            PG.BoxMM(g, new Vector3(1.1f, 0.25f, 0.4f), new Vector3(4.8f, 3.6f, 0.6f), adobeD);
            PG.BoxMM(g, new Vector3(-1.1f, 2.7f, 0.4f), new Vector3(1.1f, 3.6f, 0.6f), adobeD);
            PG.BoxMM(g, new Vector3(-1.0f, 0.25f, 0.42f), new Vector3(1.0f, 2.7f, 0.5f), C(70, 46, 34));
            for (int s = -1; s <= 1; s += 2)
            {
                PG.BoxMM(gl, new Vector3(s * 3.4f - 0.9f, 1.3f, 0.62f), new Vector3(s * 3.4f + 0.9f, 2.4f, 0.66f), Pal.WindowWarm);
                for (int k = 0; k < 4; k++) PG.BoxMM(g, new Vector3(s * 3.4f - 0.9f + k * 0.6f, 1.3f, 0.66f), new Vector3(s * 3.4f - 0.85f + k * 0.6f, 2.4f, 0.7f), Pal.Lattice);
                PG.BoxMM(g, new Vector3(s * 3.4f - 0.95f, 1.84f, 0.66f), new Vector3(s * 3.4f + 0.95f, 1.88f, 0.7f), Pal.Lattice);
            }
            PG.Roof(g, new Vector3(0f, 3.6f, -1.8f), 4.8f, 2.4f, 1.3f, 0.9f, 0.45f, Pal.Tile, Pal.Tile2, Pal.TileUnder, PG.Sh(Pal.Tile, 0.75f), true);
            // terrace pergola with reed mat shade
            for (int i = 0; i < 4; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 p = new Vector3(-4.5f + i * 3f, 0.25f, s > 0 ? 7f : 0.9f);
                    PG.Column(g, p, p + Up * 2.9f, 0.11f, Pal.Red, 6);
                }
            for (int i = 0; i < 4; i++) PG.Beam(g, new Vector3(-4.5f + i * 3f, 3.0f, 0.9f), new Vector3(-4.5f + i * 3f, 3.0f, 7f), 0.08f, 0.1f, Pal.WoodDark);
            for (int k = 0; k < 14; k++)
            {
                float z = 1f + k * 0.44f;
                PG.BoxMM(pr, new Vector3(-4.8f, 3.1f, z), new Vector3(4.8f, 3.16f, z + 0.36f), (k & 1) == 0 ? Reed : C(178, 158, 104));
            }
            Vector3[] tables = { new Vector3(-2.6f, 0.25f, 2.6f), new Vector3(2.6f, 0.25f, 2.6f), new Vector3(0f, 0.25f, 5.2f) };
            foreach (var tp in tables) steam.Add(PropShapes.TeaTable(m, tp, rng));
            for (int i = 0; i < 3; i++)
            {
                float x = -3f + i * 3f;
                PG.Rod(gl, new Vector3(x, 3.0f, 7.0f), new Vector3(x, 2.6f, 7.0f), 0.01f, PG.A(Pal.Ink, 50), 3);
                PG.Lantern(gl, new Vector3(x, 2.6f, 7.0f), 0.22f, 0.4f, Pal.RedBright, 110);
            }
            // tea flag
            Vector3 fp = new Vector3(5.6f, 0.25f, 7.4f);
            PG.Column(g, fp, fp + Up * 6.4f, 0.07f, Pal.WoodDark, 5);
            PG.Beam(g, fp + Up * 6.1f, fp + new Vector3(-1.2f, 6.1f, 0f), 0.03f, 0.03f, Pal.WoodDark);
            for (int k = 0; k < 4; k++)
            {
                float y0 = 6.05f - k * 0.55f, y1 = y0 - 0.55f;
                Color32 cc = k == 3 ? Pal.RedBright : C(242, 236, 214);
                PG.Quad2(st, fp + new Vector3(-1.15f, y0, 0f), fp + new Vector3(-0.3f, y0, 0f), fp + new Vector3(-0.3f, y1, 0f), fp + new Vector3(-1.15f, y1, 0f),
                         PG.A(cc, k * 60), PG.A(cc, k * 60), PG.A(cc, k * 60 + 60), PG.A(cc, k * 60 + 60));
            }
            // water jars and a hitching rail
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(-5.6f, 0.25f, -3f + i * 1.1f);
                pr.Frustum(p, p + Up * 0.8f, 0.28f, 0.28f, 0.42f, 0.42f, 8, 0f, C(150, 92, 62), C(180, 118, 80), true, false);
                pr.Frustum(p + Up * 0.8f, p + Up * 1.0f, 0.42f, 0.42f, 0.2f, 0.2f, 8, 0f, C(180, 118, 80), C(150, 92, 62), false, true);
            }
            for (int s = -1; s <= 1; s += 2) PG.Column(pr, new Vector3(s * 2.5f, 0.25f, 9.6f), new Vector3(s * 2.5f, 1.3f, 9.6f), 0.08f, Pal.Wood, 5);
            PG.Beam(pr, new Vector3(-2.6f, 1.2f, 9.6f), new Vector3(2.6f, 1.2f, 9.6f), 0.05f, 0.05f, Pal.Wood);
            return m;
        }

        /// <summary>Stone well with a timber windlass frame, bucket and a trough.</summary>
        public static PropModel Well(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            g.Frustum(Vector3.down * 0.2f, Up * 0.85f, 1.15f, 1.15f, 1.05f, 1.05f, 12, 0f, Pal.StoneDark, Pal.Stone, false, false);
            g.Frustum(Up * 0.85f, Up * 0.95f, 1.12f, 1.12f, 1.12f, 1.12f, 12, 0f, Pal.StoneLight, Pal.StoneLight, false, false);
            g.Disc(Up * 0.6f, 0.95f, 12, 0f, Up, C(30, 52, 50), C(40, 70, 64));
            for (int s = -1; s <= 1; s += 2) PG.Beam(g, new Vector3(s * 1.2f, 0f, 0f), new Vector3(s * 1.2f, 2.4f, 0f), 0.09f, 0.09f, Pal.Wood);
            PG.Beam(g, new Vector3(-1.4f, 2.4f, 0f), new Vector3(1.4f, 2.4f, 0f), 0.1f, 0.1f, Pal.WoodDark);
            g.Frustum(new Vector3(-0.7f, 1.9f, 0f), new Vector3(0.7f, 1.9f, 0f), 0.16f, 0.16f, 0.16f, 0.16f, 8, 0f, Pal.WoodLight, Pal.Wood, true, true);
            PG.Beam(g, new Vector3(1.25f, 1.9f, 0f), new Vector3(1.55f, 1.9f, 0f), 0.04f, 0.04f, Pal.WoodDark);
            PG.Beam(g, new Vector3(1.55f, 1.9f, 0f), new Vector3(1.55f, 1.5f, 0f), 0.04f, 0.04f, Pal.WoodDark);
            PG.Rod(g, new Vector3(0.1f, 1.85f, 0f), new Vector3(0.1f, 1.15f, 0f), 0.015f, C(150, 130, 90), 3);
            g.Frustum(new Vector3(0.1f, 0.85f, 0f), new Vector3(0.1f, 1.15f, 0f), 0.16f, 0.16f, 0.19f, 0.19f, 7, 0f, Pal.WoodDark, Pal.Wood, true, false);
            PG.Box2(g, new Vector3(0f, 0.3f, 2f), new Vector3(1.4f, 0.3f, 0.4f), Pal.StoneDark, Pal.Stone);
            PG.BoxMM(g, new Vector3(-1.25f, 0.45f, 1.7f), new Vector3(1.25f, 0.62f, 2.3f), C(70, 120, 120));
            return m;
        }

        /// <summary>Square pavilion with red columns and a curved hip roof (the spring's viewing pavilion).</summary>
        public static PropModel Pavilion(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            PG.Box2(g, new Vector3(0f, 0.05f, 0f), new Vector3(3.4f, 0.45f, 3.4f), Pal.StoneDark, Pal.Stone);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3((i & 1) == 0 ? -2.5f : 2.5f, 0.5f, (i & 2) == 0 ? -2.5f : 2.5f);
                PG.Column(g, p, p + Up * 3.1f, 0.16f, Pal.Red, 8);
            }
            for (int s = 0; s < 4; s++)
            {
                var b = new LifeGeo();
                PG.Beam(b, new Vector3(-2.7f, 3.45f, 2.5f), new Vector3(2.7f, 3.45f, 2.5f), 0.14f, 0.16f, C(56, 118, 122));
                PG.BoxMM(b, new Vector3(-2.3f, 3.0f, 2.47f), new Vector3(2.3f, 3.2f, 2.53f), Pal.Gold);
                PG.BoxMM(b, new Vector3(-2.5f, 0.9f, 2.45f), new Vector3(2.5f, 1.0f, 2.55f), Pal.Red);
                PropShapes.Place(g, b, Vector3.zero, s * 90f);
            }
            PG.Roof(g, new Vector3(0f, 3.6f, 0f), 2.7f, 2.7f, 1.6f, 1.1f, 0.6f, Pal.GreenTile, Pal.GreenTile2, Pal.TileUnder, PG.Sh(Pal.GreenTile, 0.75f), true);
            return m;
        }

        // ------------------------------------------------------------------ the wreck
        /// <summary>Old truck rusted into the playa: one wheel off, nose down, flat tyre beside it.</summary>
        public static PropModel Wreck(System.Random rng)
        {
            var src = PropShapes.Truck(0, rng);
            var m = new PropModel();
            var q = Quaternion.Euler(4f, 0f, -7f);
            m.Append(src, Matrix4x4.TRS(new Vector3(0f, -0.35f, 0f), q, Vector3.one));
            var g = m[PK.Landmark];
            for (int i = 0; i < g.c.Count; i++)
            {
                Color32 c = g.c[i];
                float l = (c.r + c.g + c.b) / 765f;
                float rust = 0.5f + 0.5f * Mathf.Sin(g.v[i].x * 3.1f + g.v[i].z * 2.3f + g.v[i].y * 4.1f);
                Color32 rc = Color32.Lerp(C(150, 84, 52), C(96, 108, 98), rust * 0.6f);
                g.c[i] = Color32.Lerp(rc, C(70, 60, 52), Mathf.Clamp01(0.4f - l));
                g.c[i] = new Color32(g.c[i].r, g.c[i].g, g.c[i].b, 0);
            }
            var pr = m[PK.Props];
            pr.Frustum(new Vector3(2.4f, 0.12f, -1.4f), new Vector3(2.4f, 0.4f, -1.4f), 0.5f, 0.5f, 0.5f, 0.5f, 10, 0f, C(40, 38, 38), C(52, 50, 48), false, true);
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(R(rng, -2.5f, 2.5f), 0.05f, R(rng, -4f, 4f));
                PG.Box2(pr, p, new Vector3(R(rng, 0.1f, 0.4f), 0.03f, R(rng, 0.1f, 0.5f)), C(130, 76, 50), C(150, 90, 60));
            }
            return m;
        }

        // ------------------------------------------------------------------ camels
        static readonly Color32[] CamelCoat = { C(196, 150, 100), C(176, 128, 82), C(214, 176, 126), C(160, 112, 72) };
        static readonly Color32[] Blanket = { C(190, 52, 46), C(48, 96, 160), C(232, 186, 70), C(126, 60, 120), C(40, 130, 110) };

        /// <summary>Camel body without legs (walking: legs are separate parts). Facing +z, back at ~2.1 m. kneel = resting pose.</summary>
        public static LifeGeo CamelBody(System.Random rng, int variant, bool kneel, bool loaded)
        {
            var g = new LifeGeo();
            Color32 coat = CamelCoat[variant % CamelCoat.Length], dark = PG.Sh(coat, 0.78f);
            float y = kneel ? 0.75f : 1.65f;
            g.Ellipsoid(new Vector3(0f, y, 0f), new Vector3(0.48f, 0.5f, 1.05f), 8, 5, coat, dark);
            g.Ellipsoid(new Vector3(0f, y + 0.5f, -0.35f), new Vector3(0.32f, 0.38f, 0.36f), 7, 4, coat, dark);
            g.Ellipsoid(new Vector3(0f, y + 0.5f, 0.38f), new Vector3(0.3f, 0.36f, 0.34f), 7, 4, coat, dark);
            Vector3 n0 = new Vector3(0f, y + 0.1f, 0.9f), n1 = new Vector3(0f, y + 0.25f, 1.55f), hd = new Vector3(0f, y + 0.85f, 1.85f);
            g.Frustum(n0, n1, 0.24f, 0.3f, 0.16f, 0.2f, 6, 0f, coat, coat, false, false);
            g.Frustum(n1, hd, 0.16f, 0.2f, 0.13f, 0.15f, 6, 0f, coat, coat, false, false);
            g.Ellipsoid(hd + new Vector3(0f, 0.04f, 0.15f), new Vector3(0.14f, 0.15f, 0.32f), 6, 4, coat, dark);
            for (int s = -1; s <= 1; s += 2)
                g.Frustum(hd + new Vector3(s * 0.1f, 0.12f, -0.05f), hd + new Vector3(s * 0.16f, 0.24f, -0.1f), 0.04f, 0.04f, 0.02f, 0.02f, 4, 0f, dark, dark, false, true);
            g.Frustum(new Vector3(0f, y + 0.1f, -1.0f), new Vector3(0f, y - 0.5f, -1.15f), 0.06f, 0.06f, 0.03f, 0.03f, 4, 0f, dark, PG.Sh(dark, 0.7f), false, true);
            // saddle blanket and bags
            Color32 bl = Blanket[rng.Next(Blanket.Length)], tr = Blanket[rng.Next(Blanket.Length)];
            g.Frustum(new Vector3(0f, y + 0.3f, -0.05f), new Vector3(0f, y + 0.55f, -0.05f), 0.6f, 0.75f, 0.42f, 0.6f, 8, 0f, bl, bl, false, false);
            PG.BoxMM(g, new Vector3(-0.62f, y + 0.27f, -0.78f), new Vector3(0.62f, y + 0.33f, 0.68f), tr);
            if (loaded)
                for (int s = -1; s <= 1; s += 2)
                {
                    PG.Box2(g, new Vector3(s * 0.62f, y + 0.1f, 0f), new Vector3(0.18f, 0.32f, 0.38f), C(150, 112, 74), C(176, 136, 92));
                    g.Ellipsoid(new Vector3(s * 0.55f, y + 0.6f, -0.55f), new Vector3(0.22f, 0.22f, 0.3f), 6, 3, C(230, 220, 196), C(200, 188, 160));
                }
            if (kneel)
                for (int s = -1; s <= 1; s += 2)
                {
                    PG.Beam(g, new Vector3(s * 0.32f, 0.2f, 0.6f), new Vector3(s * 0.32f, 0.2f, 1.25f), 0.09f, 0.12f, dark);
                    PG.Beam(g, new Vector3(s * 0.34f, 0.2f, -0.9f), new Vector3(s * 0.34f, 0.2f, -0.25f), 0.1f, 0.13f, dark);
                }
            return g;
        }

        /// <summary>One camel leg hanging down from its hip pivot (origin), length ~1.45 m.</summary>
        public static LifeGeo CamelLeg(int variant)
        {
            var g = new LifeGeo();
            Color32 coat = CamelCoat[variant % CamelCoat.Length], dark = PG.Sh(coat, 0.72f);
            g.Frustum(Vector3.zero, new Vector3(0f, -0.75f, 0.04f), 0.13f, 0.15f, 0.08f, 0.08f, 5, 0f, coat, coat, false, false);
            g.Frustum(new Vector3(0f, -0.75f, 0.04f), new Vector3(0f, -1.45f, 0f), 0.08f, 0.08f, 0.06f, 0.06f, 5, 0f, coat, dark, false, false);
            g.Ellipsoid(new Vector3(0f, -1.47f, 0.05f), new Vector3(0.12f, 0.05f, 0.15f), 5, 2, dark, dark);
            return g;
        }

        /// <summary>Caravan driver in a long robe and a wide hat (walking: two leg parts separately).</summary>
        public static LifeGeo Driver(System.Random rng)
        {
            var g = new LifeGeo();
            Color32 robe = rng.NextDouble() < 0.5 ? C(90, 110, 150) : C(150, 86, 60), skin = C(206, 156, 116);
            g.Frustum(new Vector3(0f, 0.75f, 0f), new Vector3(0f, 1.5f, 0f), 0.3f, 0.26f, 0.22f, 0.18f, 7, 0f, robe, PG.Sh(robe, 1.1f), true, true);
            g.Ellipsoid(new Vector3(0f, 1.66f, 0f), new Vector3(0.11f, 0.13f, 0.11f), 6, 4, skin, PG.Sh(skin, 0.85f));
            g.Frustum(new Vector3(0f, 1.74f, 0f), new Vector3(0f, 1.82f, 0f), 0.3f, 0.3f, 0.1f, 0.1f, 8, 0f, C(196, 172, 120), C(170, 146, 100), true, true);
            g.Frustum(new Vector3(0.25f, 1.4f, 0.05f), new Vector3(0.3f, 1.0f, 0.3f), 0.06f, 0.06f, 0.05f, 0.05f, 4, 0f, robe, robe, false, true);
            g.Frustum(new Vector3(-0.25f, 1.4f, 0f), new Vector3(-0.28f, 0.95f, -0.05f), 0.06f, 0.06f, 0.05f, 0.05f, 4, 0f, robe, robe, false, true);
            return g;
        }

        public static LifeGeo DriverLeg()
        {
            var g = new LifeGeo();
            g.Frustum(Vector3.zero, new Vector3(0f, -0.75f, 0f), 0.08f, 0.08f, 0.06f, 0.06f, 5, 0f, C(70, 60, 54), C(70, 60, 54), false, false);
            g.Ellipsoid(new Vector3(0f, -0.77f, 0.06f), new Vector3(0.07f, 0.04f, 0.12f), 5, 2, C(50, 40, 34), C(50, 40, 34));
            return g;
        }

        // ------------------------------------------------------------------ roadside furniture
        /// <summary>Wooden telegraph pole (height ~7 m) with a cross arm; wire attach points returned in local space.</summary>
        public static PropModel TelegraphPole(out Vector3 wireL, out Vector3 wireR)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Column(g, Vector3.down * 0.5f, Up * 7f, 0.13f, C(120, 92, 66), 6);
            PG.Beam(g, new Vector3(-1.0f, 6.5f, 0f), new Vector3(1.0f, 6.5f, 0f), 0.06f, 0.07f, C(104, 80, 58));
            for (int s = -1; s <= 1; s += 2) g.Frustum(new Vector3(s * 0.8f, 6.56f, 0f), new Vector3(s * 0.8f, 6.75f, 0f), 0.05f, 0.05f, 0.04f, 0.04f, 5, 0f, C(220, 226, 230), C(220, 226, 230), false, true);
            wireL = new Vector3(-0.8f, 6.75f, 0f); wireR = new Vector3(0.8f, 6.75f, 0f);
            return m;
        }

        /// <summary>Squat carved stone marker (km stone) with a red painted band.</summary>
        public static PropModel MarkerStone()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Box2(g, new Vector3(0f, 0.35f, 0f), new Vector3(0.22f, 0.55f, 0.14f), C(214, 200, 176), C(230, 218, 196));
            PG.BoxMM(g, new Vector3(-0.23f, 0.55f, -0.15f), new Vector3(0.23f, 0.7f, 0.15f), C(196, 52, 44));
            g.Ellipsoid(new Vector3(0f, 0.9f, 0f), new Vector3(0.22f, 0.08f, 0.14f), 6, 2, C(230, 218, 196), C(214, 200, 176));
            return m;
        }

        /// <summary>Rally marker post: white with a red reflector band (flexible, no collider).</summary>
        public static PropModel MarkerPost()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Box2(g, new Vector3(0f, 0.5f, 0f), new Vector3(0.06f, 0.6f, 0.06f), C(240, 238, 230), C(240, 238, 230));
            PG.BoxMM(g, new Vector3(-0.065f, 0.82f, -0.065f), new Vector3(0.065f, 1.0f, 0.065f), C(220, 50, 40));
            return m;
        }

        /// <summary>Wooden direction signpost with three painted boards.</summary>
        public static PropModel Signpost(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Column(g, Vector3.down * 0.3f, Up * 3f, 0.08f, Pal.WoodDark, 6);
            Color32[] boards = { C(196, 56, 46), C(232, 214, 170), C(60, 104, 150) };
            for (int i = 0; i < 3; i++)
            {
                float y = 2.6f - i * 0.45f, dir = (i & 1) == 0 ? 1f : -1f;
                var b = new LifeGeo();
                PG.BoxMM(b, new Vector3(0f, -0.15f, -0.03f), new Vector3(1.2f, 0.15f, 0.03f), boards[i]);
                b.TriOut(new Vector3(1.2f, -0.15f, 0.03f), new Vector3(1.2f, 0.15f, 0.03f), new Vector3(1.45f, 0f, 0.03f), Vector3.forward, boards[i], boards[i], boards[i]);
                b.TriOut(new Vector3(1.2f, -0.15f, -0.03f), new Vector3(1.2f, 0.15f, -0.03f), new Vector3(1.45f, 0f, -0.03f), Vector3.back, boards[i], boards[i], boards[i]);
                for (int k = 0; k < 3; k++) PG.BoxMM(b, new Vector3(0.2f + k * 0.32f, -0.07f, 0.03f), new Vector3(0.38f + k * 0.32f, 0.07f, 0.045f), Pal.Ink);
                PropShapes.Place(g, b, new Vector3(0f, y, 0f), dir > 0 ? R(rng, -30f, 30f) : 180f + R(rng, -30f, 30f));
            }
            return m;
        }

        /// <summary>Spectator group: a camp of a 4x4, a sun shade, flags and a few standing fans.</summary>
        public static PropModel SpectatorCamp(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            Color32 car = rng.NextDouble() < 0.5 ? C(230, 226, 214) : C(70, 92, 76);
            PG.Box2(g, new Vector3(-3f, 0.95f, 0f), new Vector3(0.95f, 0.5f, 2.2f), car, car);
            PG.Box2(g, new Vector3(-3f, 1.75f, -0.3f), new Vector3(0.9f, 0.35f, 1.4f), PG.Sh(car, 0.95f), C(60, 60, 64));
            PG.BoxMM(g, new Vector3(-3.92f, 1.5f, 0.6f), new Vector3(-2.08f, 1.98f, 1.1f), C(70, 92, 104));
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = new Vector3(-3f + ((i & 1) == 0 ? -0.95f : 0.95f), 0.42f, (i & 2) == 0 ? -1.4f : 1.4f);
                g.Frustum(c - Vector3.right * 0.16f, c + Vector3.right * 0.16f, 0.42f, 0.42f, 0.42f, 0.42f, 8, 0f, C(34, 34, 36), C(34, 34, 36), true, true);
            }
            PG.Box2(g, new Vector3(-3f, 2.2f, -0.3f), new Vector3(0.7f, 0.12f, 1.1f), C(120, 96, 70), C(150, 120, 86));
            // sun shade
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(0.5f + ((i & 1) == 0 ? -1.5f : 1.5f), 0f, (i & 2) == 0 ? -1.5f : 1.5f);
                PG.Column(g, p, p + Up * 2.3f, 0.04f, C(180, 180, 184), 4);
            }
            Color32 sh = Blanket[rng.Next(Blanket.Length)];
            PG.Quad2(st, new Vector3(-1f, 2.3f, -1.5f), new Vector3(2f, 2.3f, -1.5f), new Vector3(2f, 2.4f, 1.5f), new Vector3(-1f, 2.4f, 1.5f), PG.A(sh, 60), PG.A(sh, 60), PG.A(sh, 60), PG.A(sh, 60));
            // fans
            for (int i = 0; i < 4; i++)
            {
                var f = Driver(rng);
                var l1 = DriverLeg();
                Vector3 p = new Vector3(R(rng, -0.5f, 2f), 0.78f, R(rng, -1.2f, 1.6f));
                PropShapes.Place(g, f, p + Vector3.down * 0.78f, R(rng, -40f, 40f));
                PropShapes.Place(g, l1, p + new Vector3(0.1f, 0f, 0f), 0f);
                PropShapes.Place(g, l1, p + new Vector3(-0.1f, 0f, 0f), 0f);
            }
            // flags
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = new Vector3(3.2f, 0f, -1.5f + i * 3f);
                PG.Column(g, p, p + Up * 3.4f, 0.03f, C(200, 200, 200), 4);
                Color32 fc = Blanket[rng.Next(Blanket.Length)];
                PG.Quad2(st, p + Up * 3.35f, p + new Vector3(0f, 3.35f, 1.1f), p + new Vector3(0f, 2.65f, 1.1f), p + Up * 2.65f, PG.A(fc, 20), PG.A(fc, 200), PG.A(fc, 200), PG.A(fc, 20));
            }
            return m;
        }
    }
}
