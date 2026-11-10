// PropShapes.cs - procedural low-poly village & culture props for WorldProps (local space: origin on the
// ground at the prop's centre / front, +Z = front). Vertex colours are sRGB, alpha = wind sway weight.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class PropShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 A(Color32 c, int a) { return PG.A(c, a); }
        static Color32 Sh(Color32 c, float k) { return PG.Sh(c, k); }
        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        public static readonly Color32[] Silks =
        {
            C(200, 40, 52), C(232, 184, 70), C(60, 150, 120), C(90, 140, 210), C(236, 140, 180), C(140, 90, 180), C(240, 236, 220)
        };

        // ------------------------------------------------------------------ guardian lion
        public static PropModel StoneLion(int variant)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 st = C(182, 178, 168), stD = C(140, 136, 128), dark = C(60, 56, 54);
            PG.Box2(g, new Vector3(0f, 0.32f, 0f), new Vector3(0.46f, 0.32f, 0.62f), stD, st);
            PG.Box2(g, new Vector3(0f, 0.67f, 0f), new Vector3(0.52f, 0.04f, 0.68f), st, Pal.StoneLight);
            g.Ellipsoid(new Vector3(0f, 0.98f, -0.18f), new Vector3(0.36f, 0.3f, 0.4f), 7, 4, st, stD);
            g.Ellipsoid(new Vector3(0f, 1.28f, 0.1f), new Vector3(0.31f, 0.44f, 0.28f), 7, 4, st, stD);
            Vector3 head = new Vector3(0f, 1.8f, 0.2f);
            g.Ellipsoid(head, new Vector3(0.31f, 0.29f, 0.28f), 7, 5, st, stD);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.PI * (i / 8f) - Mathf.PI * 0.0f;
                Vector3 p = head + new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.27f - 0.02f, -0.12f);
                g.Ellipsoid(p, new Vector3(0.12f, 0.12f, 0.12f), 5, 3, Sh(st, 0.95f), stD);
            }
            for (int i = 0; i < 5; i++)
                g.Ellipsoid(new Vector3((i - 2) * 0.11f, 1.48f, 0.3f), new Vector3(0.07f, 0.1f, 0.06f), 5, 3, st, stD);   // mane curls on the chest
            g.Ellipsoid(head + new Vector3(0f, -0.08f, 0.25f), new Vector3(0.16f, 0.12f, 0.1f), 6, 3, st, stD);           // snout
            g.Ellipsoid(head + new Vector3(0f, -0.16f, 0.24f), new Vector3(0.12f, 0.04f, 0.06f), 5, 2, C(150, 70, 60), dark); // open mouth
            for (int s = -1; s <= 1; s += 2)
            {
                g.Box(head + new Vector3(s * 0.11f, 0.06f, 0.27f), new Vector3(0.045f, 0.035f, 0.02f), dark);                  // eyes
                g.Ellipsoid(head + new Vector3(s * 0.25f, 0.18f, 0.02f), new Vector3(0.07f, 0.09f, 0.05f), 5, 3, st, stD);    // ears
                g.Frustum(new Vector3(s * 0.17f, 0.7f, 0.32f), new Vector3(s * 0.17f, 1.22f, 0.22f), 0.09f, 0.09f, 0.08f, 0.08f, 6, 0f, stD, st, false, false);
                g.Ellipsoid(new Vector3(s * 0.17f, 0.75f, 0.38f), new Vector3(0.1f, 0.06f, 0.12f), 5, 3, st, stD);
                g.Ellipsoid(new Vector3(s * 0.28f, 0.82f, -0.12f), new Vector3(0.1f, 0.14f, 0.24f), 5, 3, st, stD);           // haunch
            }
            g.Ellipsoid(new Vector3(0f, 0.95f, -0.58f), new Vector3(0.12f, 0.16f, 0.1f), 5, 3, st, stD);                       // tail curl
            float sx = variant == 0 ? 0.2f : -0.2f;
            if (variant == 0) g.Ellipsoid(new Vector3(sx, 0.86f, 0.44f), new Vector3(0.14f, 0.14f, 0.14f), 7, 4, Pal.StoneLight, stD);  // embroidered ball
            else
            {
                g.Ellipsoid(new Vector3(sx, 0.84f, 0.44f), new Vector3(0.12f, 0.1f, 0.15f), 5, 3, st, stD);                  // cub
                g.Ellipsoid(new Vector3(sx, 0.97f, 0.52f), new Vector3(0.09f, 0.08f, 0.08f), 5, 3, st, stD);
            }
            return m;
        }

        // ------------------------------------------------------------------ incense burner (ding)
        public static PropModel IncenseBurner(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            g.Frustum(Vector3.zero, Up * 0.32f, 0.95f, 0.95f, 0.85f, 0.85f, 8, 0.2f, Pal.StoneDark, Pal.Stone, false, true);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.42f, 0f, Mathf.Sin(a) * 0.42f);
                g.Frustum(p + Up * 0.32f, p * 0.85f + Up * 0.85f, 0.09f, 0.09f, 0.11f, 0.11f, 5, 0f, Pal.BronzeDark, Pal.Bronze, false, false);
            }
            g.Frustum(Up * 0.72f, Up * 1.0f, 0.4f, 0.4f, 0.62f, 0.62f, 10, 0f, Pal.BronzeDark, Pal.Bronze, true, false);
            g.Frustum(Up * 1.0f, Up * 1.36f, 0.62f, 0.62f, 0.66f, 0.66f, 10, 0f, Pal.Bronze, Sh(Pal.Bronze, 1.1f), false, false);
            g.Frustum(Up * 1.36f, Up * 1.44f, 0.74f, 0.74f, 0.74f, 0.74f, 10, 0f, Pal.GoldDark, Pal.GoldDark, true, true);
            g.Disc(Up * 1.445f, 0.66f, 10, 0f, Vector3.up, C(110, 100, 90), C(96, 86, 76));                  // ash / sand
            for (int s = -1; s <= 1; s += 2)
            {
                PG.BoxMM(g, new Vector3(s * 0.62f - 0.04f, 1.36f, -0.2f), new Vector3(s * 0.62f + 0.04f, 1.86f, -0.12f), Pal.Bronze);
                PG.BoxMM(g, new Vector3(s * 0.62f - 0.04f, 1.36f, 0.12f), new Vector3(s * 0.62f + 0.04f, 1.86f, 0.2f), Pal.Bronze);
                PG.BoxMM(g, new Vector3(s * 0.62f - 0.04f, 1.78f, -0.2f), new Vector3(s * 0.62f + 0.04f, 1.88f, 0.2f), Pal.GoldDark);
            }
            for (int i = 0; i < 4; i++)
                g.Box(new Vector3(0f, 1.18f, 0f) + Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 0.645f), new Vector3(0.12f, 0.08f, 0.02f), Pal.GoldDark);
            var gl = m[PK.Glow];
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(R(rng, -0.35f, 0.35f), 1.44f, R(rng, -0.35f, 0.35f));
                Vector3 tip = p + new Vector3(R(rng, -0.06f, 0.06f), R(rng, 0.38f, 0.55f), R(rng, -0.06f, 0.06f));
                PG.Rod(g, p, tip, 0.012f, C(196, 60, 46), 3);
                gl.Box(tip, new Vector3(0.018f, 0.025f, 0.018f), C(255, 140, 60));
            }
            return m;
        }

        // ------------------------------------------------------------------ tea table set
        /// <summary>Round table with stools, a teapot and cups; returns the local steam point.</summary>
        public static Vector3 TeaTable(PropModel m, Vector3 at, System.Random rng)
        {
            var g = m[PK.Props];
            Color32 top = Pal.WoodLight, leg = Pal.WoodDark;
            g.Frustum(at, at + Up * 0.05f, 0.32f, 0.32f, 0.28f, 0.28f, 8, 0f, leg, leg, false, true);
            g.Frustum(at, at + Up * 0.72f, 0.07f, 0.07f, 0.07f, 0.07f, 6, 0f, leg, leg, false, false);
            g.Frustum(at + Up * 0.72f, at + Up * 0.78f, 0.58f, 0.58f, 0.58f, 0.58f, 12, 0f, Sh(top, 0.85f), top, true, true);
            bool porcelain = rng.NextDouble() < 0.5;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Vector3 p = at + new Vector3(Mathf.Cos(a) * 0.9f, 0f, Mathf.Sin(a) * 0.9f);
                Color32 sc = porcelain ? Pal.Porcelain : Pal.Wood;
                g.Frustum(p, p + Up * 0.23f, 0.17f, 0.17f, 0.21f, 0.21f, 8, 0f, sc, sc, false, false);
                g.Frustum(p + Up * 0.23f, p + Up * 0.46f, 0.21f, 0.21f, 0.18f, 0.18f, 8, 0f, sc, porcelain ? Pal.PorcelainBlue : Pal.WoodLight, false, true);
                if (porcelain) g.Frustum(p + Up * 0.2f, p + Up * 0.27f, 0.215f, 0.215f, 0.215f, 0.215f, 8, 0f, Pal.PorcelainBlue, Pal.PorcelainBlue, false, false);
            }
            Color32 clay = rng.NextDouble() < 0.6 ? C(128, 76, 62) : C(150, 196, 176);
            Vector3 pot = at + new Vector3(0.12f, 0.78f, -0.05f);
            g.Ellipsoid(pot + Up * 0.075f, new Vector3(0.1f, 0.075f, 0.1f), 7, 4, clay, Sh(clay, 0.8f));
            g.Ellipsoid(pot + Up * 0.16f, new Vector3(0.03f, 0.025f, 0.03f), 5, 3, clay, clay);
            g.Frustum(pot + new Vector3(0.08f, 0.06f, 0f), pot + new Vector3(0.19f, 0.15f, 0f), 0.024f, 0.024f, 0.012f, 0.012f, 4, 0f, clay, clay, false, true);
            PG.Beam(g, pot + new Vector3(-0.09f, 0.13f, 0f), pot + new Vector3(-0.16f, 0.06f, 0f), 0.015f, 0.02f, clay);
            int cups = 2 + rng.Next(3);
            for (int i = 0; i < cups; i++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f);
                Vector3 p = at + new Vector3(Mathf.Cos(a) * 0.36f, 0.78f, Mathf.Sin(a) * 0.36f);
                g.Frustum(p, p + Up * 0.05f, 0.03f, 0.03f, 0.042f, 0.042f, 6, 0f, Pal.Porcelain, Pal.Porcelain, true, false);
                g.Disc(p + Up * 0.045f, 0.036f, 6, 0f, Vector3.up, C(170, 140, 60), C(170, 140, 60));
            }
            if (rng.NextDouble() < 0.6)
            {
                Vector3 pl = at + new Vector3(-0.25f, 0.785f, 0.18f);
                g.Disc(pl, 0.13f, 8, 0f, Vector3.up, Pal.Porcelain, Pal.PorcelainBlue);
                for (int i = 0; i < 3; i++)
                    g.Frustum(pl + new Vector3(Mathf.Cos(i * 2.1f) * 0.05f, 0f, Mathf.Sin(i * 2.1f) * 0.05f), pl + new Vector3(Mathf.Cos(i * 2.1f) * 0.05f, 0.035f, Mathf.Sin(i * 2.1f) * 0.05f),
                              0.04f, 0.04f, 0.04f, 0.04f, 6, 0f, C(196, 140, 70), C(220, 168, 90), false, true);
            }
            return pot + new Vector3(0.19f, 0.17f, 0f);
        }

        // ------------------------------------------------------------------ market stall
        /// <summary>type: 0 fruit, 1 silk, 2 lanterns, 3 noodles, 4 porcelain. Origin = front of the counter.</summary>
        public static PropModel Stall(int type, System.Random rng, List<Vector3> steam)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            var st = m[PK.Strong];
            Color32 wood = Pal.Wood, dark = Pal.WoodDark;
            PG.Box2(g, new Vector3(0f, 0.45f, -0.5f), new Vector3(1.2f, 0.45f, 0.5f), Sh(wood, 0.9f), Pal.WoodLight);
            PG.BoxMM(g, new Vector3(-1.21f, 0.05f, -0.01f), new Vector3(1.21f, 0.12f, 0.01f), dark);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Beam(g, new Vector3(s * 1.25f, -0.3f, 0.05f), new Vector3(s * 1.25f, 2.25f, 0.05f), 0.05f, 0.05f, dark);
                PG.Beam(g, new Vector3(s * 1.25f, -0.3f, -1.65f), new Vector3(s * 1.25f, 2.62f, -1.65f), 0.05f, 0.05f, dark);
                PG.Beam(g, new Vector3(s * 1.25f, 2.25f, 0.05f), new Vector3(s * 1.25f, 2.62f, -1.65f), 0.04f, 0.04f, dark);
            }
            PG.Beam(g, new Vector3(-1.3f, 2.24f, 0.05f), new Vector3(1.3f, 2.24f, 0.05f), 0.045f, 0.045f, dark);
            PG.Beam(g, new Vector3(-1.3f, 2.6f, -1.65f), new Vector3(1.3f, 2.6f, -1.65f), 0.045f, 0.045f, dark);
            // awning (striped canvas, flaps more towards the free front edge)
            Color32[] awn = { Pal.RedBright, C(70, 110, 180), C(232, 190, 70), C(70, 150, 110), C(214, 96, 140) };
            Color32 ca = awn[rng.Next(awn.Length)], cb = rng.NextDouble() < 0.7 ? C(246, 240, 226) : Sh(ca, 0.8f);
            const int NX = 8, NZ = 3;
            for (int ix = 0; ix < NX; ix++)
                for (int iz = 0; iz < NZ; iz++)
                {
                    float x0 = -1.45f + 2.9f * ix / NX, x1 = -1.45f + 2.9f * (ix + 1) / NX;
                    float t0 = iz / (float)NZ, t1 = (iz + 1) / (float)NZ;
                    Vector3 p00 = AwnPt(x0, t0), p10 = AwnPt(x1, t0), p11 = AwnPt(x1, t1), p01 = AwnPt(x0, t1);
                    Color32 col = (ix & 1) == 0 ? ca : cb;
                    int a0 = (int)(t0 * t0 * 190f), a1 = (int)(t1 * t1 * 190f);
                    PG.Quad2(st, p00, p10, p11, p01, A(col, a0), A(col, a0), A(col, a1), A(col, a1));
                }
            for (int ix = 0; ix < NX; ix++)
            {
                float x0 = -1.45f + 2.9f * ix / NX, x1 = -1.45f + 2.9f * (ix + 1) / NX;
                Vector3 a = AwnPt(x0, 1f), b = AwnPt(x1, 1f), mid = (a + b) * 0.5f - Up * 0.26f;
                Color32 col = A((ix & 1) == 0 ? ca : cb, 200);
                st.TriOut(a, b, mid, Vector3.forward, col, col, A(col, 230));
                st.TriOut(a, b, mid, Vector3.back, col, col, A(col, 230));
            }
            Vector3 ct = new Vector3(0f, 0.9f, -0.5f);
            switch (type)
            {
                case 0:
                    {
                        Color32[] fruit = { C(244, 150, 40), C(250, 172, 160), C(200, 40, 44), C(150, 196, 70), C(236, 210, 80) };
                        for (int b = 0; b < 3; b++)
                        {
                            Vector3 bp = ct + new Vector3(-0.75f + b * 0.75f, 0f, 0f);
                            g.Frustum(bp, bp + Up * 0.14f, 0.26f, 0.26f, 0.34f, 0.34f, 8, 0f, C(176, 136, 80), C(206, 168, 104), true, false);
                            Color32 fc = fruit[rng.Next(fruit.Length)];
                            for (int k = 0; k < 9; k++)
                            {
                                float a = k * 2.4f, r = k < 6 ? 0.2f : 0.08f;
                                Vector3 fp = bp + new Vector3(Mathf.Cos(a) * r, k < 6 ? 0.17f : 0.26f, Mathf.Sin(a) * r);
                                g.Ellipsoid(fp, new Vector3(0.075f, 0.07f, 0.075f), 5, 3, fc, Sh(fc, 0.8f));
                            }
                        }
                        for (int k = 0; k < 2; k++)
                        {
                            Vector3 cp = new Vector3(-1.75f, 0f, -0.3f - k * 0.62f);
                            PG.Box2(g, cp + Up * 0.2f, new Vector3(0.28f, 0.2f, 0.28f), C(176, 140, 90), C(150, 116, 74));
                            Color32 fc = fruit[rng.Next(fruit.Length)];
                            for (int q = 0; q < 4; q++)
                                g.Ellipsoid(cp + new Vector3((q & 1) * 0.24f - 0.12f, 0.43f, (q >> 1) * 0.24f - 0.12f), new Vector3(0.1f, 0.08f, 0.1f), 5, 3, fc, Sh(fc, 0.8f));
                        }
                        break;
                    }
                case 1:
                    for (int i = 0; i < 6; i++)
                    {
                        Color32 sc = Silks[rng.Next(Silks.Length)];
                        Vector3 p = ct + new Vector3(-0.95f + i * 0.38f, 0.11f, 0f);
                        g.Frustum(p + Vector3.back * 0.36f, p + Vector3.forward * 0.36f, 0.11f, 0.11f, 0.11f, 0.11f, 7, 0f, sc, Sh(sc, 0.9f), true, true);
                        if (i < 5)
                        {
                            Color32 s2 = Silks[rng.Next(Silks.Length)];
                            Vector3 q = p + new Vector3(0.19f, 0.2f, 0f);
                            g.Frustum(q + Vector3.back * 0.33f, q + Vector3.forward * 0.33f, 0.1f, 0.1f, 0.1f, 0.1f, 7, 0f, s2, Sh(s2, 0.9f), true, true);
                        }
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        Color32 sc = Silks[rng.Next(Silks.Length)];
                        float x = -0.9f + i * 0.9f;
                        for (int k = 0; k < 4; k++)
                        {
                            float y0 = 2.2f - k * 0.3f, y1 = y0 - 0.3f;
                            PG.Quad2(st, new Vector3(x - 0.18f, y0, 0.12f), new Vector3(x + 0.18f, y0, 0.12f), new Vector3(x + 0.18f, y1, 0.12f), new Vector3(x - 0.18f, y1, 0.12f),
                                     A(sc, k * 55), A(sc, k * 55), A(sc, k * 55 + 55), A(sc, k * 55 + 55));
                        }
                    }
                    break;
                case 2:
                    for (int i = 0; i < 5; i++)
                    {
                        Color32 lc = (i % 2 == 0) ? Pal.RedBright : C(244, 186, 60);
                        float x = -1.0f + i * 0.5f;
                        PG.Rod(gl, new Vector3(x, 2.24f, 0.06f), new Vector3(x, 1.95f, 0.06f), 0.008f, A(Pal.Ink, 60), 3);
                        PG.Lantern(gl, new Vector3(x, 1.95f, 0.06f), 0.15f, 0.26f, lc, 150, i != 2);
                    }
                    for (int i = 0; i < 3; i++)
                        PG.Lantern(gl, ct + new Vector3(-0.7f + i * 0.7f, 0.42f, -0.1f), 0.16f, 0.3f, i == 1 ? C(244, 186, 60) : Pal.RedBright, 0);
                    for (int i = 0; i < 4; i++)
                        g.Frustum(ct + new Vector3(-1.0f + i * 0.12f, 0f, -0.35f), ct + new Vector3(-1.0f + i * 0.12f, 0.05f, -0.35f), 0.17f, 0.17f, 0.17f, 0.17f, 8, 0f, Pal.Red, Pal.Gold, false, true);
                    break;
                case 3:
                    {
                        Vector3 sp = ct + new Vector3(-0.72f, 0f, -0.1f);
                        PG.Box2(g, sp + Up * 0.12f, new Vector3(0.36f, 0.12f, 0.3f), C(60, 56, 54), C(80, 74, 70));
                        gl.Box(sp + new Vector3(0f, 0.1f, 0.305f), new Vector3(0.14f, 0.06f, 0.01f), C(255, 120, 40));
                        g.Frustum(sp + Up * 0.24f, sp + Up * 0.58f, 0.27f, 0.27f, 0.31f, 0.31f, 10, 0f, C(150, 150, 156), C(176, 176, 182), true, false);
                        g.Disc(sp + Up * 0.55f, 0.29f, 10, 0f, Vector3.up, C(236, 222, 190), C(214, 196, 160));
                        steam.Add(sp + Up * 0.7f);
                        for (int i = 0; i < 5; i++)
                        {
                            Vector3 bp = ct + new Vector3(-0.15f + i * 0.28f, 0f, (i & 1) * 0.22f - 0.05f);
                            g.Frustum(bp, bp + Up * 0.08f, 0.055f, 0.055f, 0.1f, 0.1f, 8, 0f, Pal.Porcelain, Pal.PorcelainBlue, true, false);
                            g.Disc(bp + Up * 0.07f, 0.09f, 8, 0f, Vector3.up, C(240, 218, 160), C(226, 196, 130));
                            g.Box(bp + new Vector3(0.03f, 0.08f, 0f), new Vector3(0.02f, 0.01f, 0.02f), C(90, 170, 70));
                        }
                        PG.BoxMM(g, new Vector3(-0.5f, 1.7f, -1.69f), new Vector3(0.5f, 2.3f, -1.62f), Pal.RedDeep);
                        for (int i = 0; i < 4; i++) g.Box(new Vector3(-0.33f + i * 0.22f, 2.0f, -1.6f), new Vector3(0.07f, 0.09f, 0.01f), Pal.Gold);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Vector3 sp2 = new Vector3(s * 1.8f, 0f, -0.6f);
                            g.Frustum(sp2, sp2 + Up * 0.45f, 0.15f, 0.15f, 0.17f, 0.17f, 7, 0f, Pal.Wood, Pal.WoodLight, false, true);
                        }
                        break;
                    }
                default:
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 vp = ct + new Vector3(-0.95f + i * 0.38f, 0f, (i & 1) * 0.2f - 0.1f);
                        float h = R(rng, 0.25f, 0.45f);
                        bool blue = (i % 3) != 1;
                        Color32 vc = blue ? Pal.Porcelain : C(150, 196, 176);
                        g.Frustum(vp, vp + Up * h * 0.6f, 0.07f, 0.07f, 0.12f, 0.12f, 8, 0f, vc, blue ? Pal.PorcelainBlue : vc, true, false);
                        g.Frustum(vp + Up * h * 0.6f, vp + Up * h, 0.12f, 0.12f, 0.05f, 0.05f, 8, 0f, blue ? Pal.PorcelainBlue : vc, vc, false, false);
                        g.Frustum(vp + Up * h, vp + Up * (h + 0.06f), 0.05f, 0.05f, 0.06f, 0.06f, 8, 0f, vc, vc, false, true);
                    }
                    break;
            }
            // small corner lantern on every stall
            PG.Rod(gl, new Vector3(1.22f, 2.24f, 0.08f), new Vector3(1.22f, 2.0f, 0.08f), 0.008f, A(Pal.Ink, 60), 3);
            PG.Lantern(gl, new Vector3(1.22f, 2.0f, 0.08f), 0.13f, 0.22f, Pal.RedBright, 150);
            return m;
        }

        static Vector3 AwnPt(float x, float t)
        {
            // from the back beam (t = 0) to the free front edge (t = 1), a gentle sag
            float z = Mathf.Lerp(-1.75f, 0.72f, t);
            float y = Mathf.Lerp(2.66f, 2.18f, t) - Mathf.Sin(t * Mathf.PI) * 0.07f;
            return new Vector3(x, y, z);
        }

        // ------------------------------------------------------------------ walls, moon gates, lattice windows
        /// <summary>Tile coping along X on top of a wall (y = wall top).</summary>
        public static void Coping(LifeGeo g, float x0, float x1, float y, float z, float th, Color32 tile)
        {
            float o = th * 0.5f + 0.22f;
            Vector3 r0 = new Vector3(x0 - 0.1f, y + 0.32f, z), r1 = new Vector3(x1 + 0.1f, y + 0.32f, z);
            Vector3 f0 = new Vector3(x0 - 0.15f, y + 0.02f, z + o), f1 = new Vector3(x1 + 0.15f, y + 0.02f, z + o);
            Vector3 b0 = new Vector3(x0 - 0.15f, y + 0.02f, z - o), b1 = new Vector3(x1 + 0.15f, y + 0.02f, z - o);
            g.QuadOut(r0, r1, f1, f0, new Vector3(0f, 1f, 1f), tile, tile, tile, tile);
            g.QuadOut(r0, r1, b1, b0, new Vector3(0f, 1f, -1f), tile, tile, tile, tile);
            Color32 u = Sh(tile, 0.7f);
            g.QuadOut(f0, f1, b1, b0, Vector3.down, u, u, u, u);
            g.TriOut(r0, f0, b0, Vector3.left, u, u, u);
            g.TriOut(r1, f1, b1, Vector3.right, u, u, u);
            PG.Beam(g, r0 + Up * 0.05f, r1 + Up * 0.05f, 0.08f, 0.07f, Sh(tile, 0.85f));
        }

        /// <summary>Wall along X (x0..x1, y0..y1, centred at z) with rectangular openings filled by lattice.</summary>
        public static void WallX(LifeGeo g, float x0, float x1, float y0, float y1, float z, float th, Color32 col, Color32 baseCol, List<Rect> holes, Color32 lattice)
        {
            var xs = new List<float> { x0, x1 };
            if (holes != null)
                foreach (var h in holes) { xs.Add(Mathf.Clamp(h.xMin, x0, x1)); xs.Add(Mathf.Clamp(h.xMax, x0, x1)); }
            xs.Sort();
            float hz = th * 0.5f;
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                float a = xs[i], b = xs[i + 1];
                if (b - a < 0.005f) continue;
                float mid = (a + b) * 0.5f;
                Rect hole = new Rect();
                bool inHole = false;
                if (holes != null)
                    foreach (var h in holes)
                        if (mid > h.xMin && mid < h.xMax) { hole = h; inHole = true; break; }
                if (!inHole)
                {
                    Seg(g, a, b, y0, y1, z, hz, col, baseCol);
                }
                else
                {
                    if (hole.yMin > y0) Seg(g, a, b, y0, hole.yMin, z, hz, col, baseCol);
                    if (hole.yMax < y1) Seg(g, a, b, hole.yMax, y1, z, hz, col, col);
                }
            }
            if (holes == null) return;
            foreach (var h in holes)
            {
                // frame + lattice (a square grid with a diamond)
                Color32 fr = Sh(lattice, 0.8f);
                float t = hz * 0.6f;
                PG.BoxMM(g, new Vector3(h.xMin, h.yMin, z - t), new Vector3(h.xMax, h.yMin + 0.06f, z + t), fr);
                PG.BoxMM(g, new Vector3(h.xMin, h.yMax - 0.06f, z - t), new Vector3(h.xMax, h.yMax, z + t), fr);
                PG.BoxMM(g, new Vector3(h.xMin, h.yMin, z - t), new Vector3(h.xMin + 0.06f, h.yMax, z + t), fr);
                PG.BoxMM(g, new Vector3(h.xMax - 0.06f, h.yMin, z - t), new Vector3(h.xMax, h.yMax, z + t), fr);
                for (int k = 1; k < 4; k++)
                {
                    float x = Mathf.Lerp(h.xMin, h.xMax, k / 4f), y = Mathf.Lerp(h.yMin, h.yMax, k / 4f);
                    PG.BoxMM(g, new Vector3(x - 0.02f, h.yMin, z - 0.03f), new Vector3(x + 0.02f, h.yMax, z + 0.03f), lattice);
                    PG.BoxMM(g, new Vector3(h.xMin, y - 0.02f, z - 0.03f), new Vector3(h.xMax, y + 0.02f, z + 0.03f), lattice);
                }
                Vector3 c = new Vector3(h.center.x, h.center.y, z);
                float rx = h.width * 0.32f, ry = h.height * 0.32f;
                Vector3 l = c + Vector3.left * rx, r = c + Vector3.right * rx, tp = c + Up * ry, bt = c - Up * ry;
                PG.Beam(g, l, tp, 0.025f, 0.03f, lattice); PG.Beam(g, tp, r, 0.025f, 0.03f, lattice);
                PG.Beam(g, r, bt, 0.025f, 0.03f, lattice); PG.Beam(g, bt, l, 0.025f, 0.03f, lattice);
            }
        }

        static void Seg(LifeGeo g, float a, float b, float y0, float y1, float z, float hz, Color32 col, Color32 baseCol)
        {
            if (y1 - y0 < 0.005f) return;
            float split = Mathf.Min(y1, y0 + 0.45f);
            if (baseCol.r != col.r || baseCol.g != col.g || baseCol.b != col.b)
            {
                if (split > y0) PG.BoxMM(g, new Vector3(a, y0, z - hz - 0.02f), new Vector3(b, split, z + hz + 0.02f), baseCol);
                if (y1 > split) PG.BoxMM(g, new Vector3(a, split, z - hz), new Vector3(b, y1, z + hz), col);
            }
            else PG.BoxMM(g, new Vector3(a, y0, z - hz), new Vector3(b, y1, z + hz), col);
        }

        /// <summary>Wall piece along X (-hw..hw) with a round moon gate of radius R; bottom at y0.</summary>
        public static void MoonWall(LifeGeo g, float hw, float y0, float H, float R, float th, Color32 col, Color32 trim)
        {
            float hz = th * 0.5f;
            Vector2 c = new Vector2(0f, y0 + R + 0.1f);
            var angs = new List<float>();
            const int N = 28;
            for (int k = 0; k < N; k++) angs.Add(k * Mathf.PI * 2f / N);
            Vector2[] corners = { new Vector2(hw, y0 + H), new Vector2(-hw, y0 + H), new Vector2(-hw, y0), new Vector2(hw, y0) };
            foreach (var q in corners)
            {
                float a = Mathf.Atan2(q.y - c.y, q.x - c.x);
                if (a < 0f) a += Mathf.PI * 2f;
                angs.Add(a);
            }
            angs.Sort();
            for (int i = 0; i < angs.Count; i++)
            {
                float a0 = angs[i], a1 = i + 1 < angs.Count ? angs[i + 1] : angs[0] + Mathf.PI * 2f;
                if (a1 - a0 < 1e-4f) continue;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Vector2 i0 = c + d0 * R, i1 = c + d1 * R;
                Vector2 o0 = RayRect(c, d0, hw, y0, y0 + H), o1 = RayRect(c, d1, hw, y0, y0 + H);
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * hz;
                    g.QuadOut(V(i0, z), V(i1, z), V(o1, z), V(o0, z), Vector3.forward * s, col, col, col, col);
                    Vector2 t0 = c + d0 * (R + 0.14f), t1 = c + d1 * (R + 0.14f);
                    float zt = s * (hz + 0.015f);
                    g.QuadOut(V(i0, zt), V(i1, zt), V(t1, zt), V(t0, zt), Vector3.forward * s, trim, trim, trim, trim);
                }
                Vector3 inward = -new Vector3((d0.x + d1.x), (d0.y + d1.y), 0f);
                g.QuadOut(V(i0, hz), V(i1, hz), V(i1, -hz), V(i0, -hz), inward, Sh(trim, 1.1f), Sh(trim, 1.1f), Sh(trim, 1.1f), Sh(trim, 1.1f));
            }
            // outer faces (top and ends)
            g.QuadOut(new Vector3(-hw, y0 + H, -hz), new Vector3(hw, y0 + H, -hz), new Vector3(hw, y0 + H, hz), new Vector3(-hw, y0 + H, hz), Vector3.up, col, col, col, col);
            for (int s = -1; s <= 1; s += 2)
                g.QuadOut(new Vector3(s * hw, y0, -hz), new Vector3(s * hw, y0 + H, -hz), new Vector3(s * hw, y0 + H, hz), new Vector3(s * hw, y0, hz), Vector3.right * s, col, col, col, col);
        }

        static Vector3 V(Vector2 p, float z) { return new Vector3(p.x, p.y, z); }

        static Vector2 RayRect(Vector2 c, Vector2 d, float hw, float yb, float yt)
        {
            float t = 1e6f;
            if (d.x > 1e-5f) t = Mathf.Min(t, (hw - c.x) / d.x);
            if (d.x < -1e-5f) t = Mathf.Min(t, (-hw - c.x) / d.x);
            if (d.y > 1e-5f) t = Mathf.Min(t, (yt - c.y) / d.y);
            if (d.y < -1e-5f) t = Mathf.Min(t, (yb - c.y) / d.y);
            return c + d * t;
        }

        /// <summary>Append 'src' built along X into 'dst' rotated by yaw and moved to pos.</summary>
        public static void Place(LifeGeo dst, LifeGeo src, Vector3 pos, float yaw)
        {
            PG.Append(dst, src, Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
        }

        /// <summary>Walled scholar garden: white walls with grey tile coping, lattice windows, a moon gate in the front (+Z) wall.</summary>
        public static PropModel GardenWalls(float hx, float hz, float baseY, float wallH)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 wall = Pal.Plaster, plinth = Pal.Brick, tile = Pal.Tile;
            float th = 0.38f, gateHw = 1.9f;
            // front wall: left + moon gate + right
            var left = new LifeGeo();
            WallX(left, -hx, -gateHw, baseY, wallH, 0f, th, wall, plinth, new List<Rect> { new Rect(-hx * 0.55f - 0.55f, 1.1f, 1.1f, 1.0f) }, C(110, 110, 114));
            Coping(left, -hx, -gateHw, wallH, 0f, th, tile);
            Place(g, left, new Vector3(0f, 0f, hz), 0f);
            var right = new LifeGeo();
            WallX(right, gateHw, hx, baseY, wallH, 0f, th, wall, plinth, new List<Rect> { new Rect(hx * 0.55f - 0.55f, 1.1f, 1.1f, 1.0f) }, C(110, 110, 114));
            Coping(right, gateHw, hx, wallH, 0f, th, tile);
            Place(g, right, new Vector3(0f, 0f, hz), 0f);
            var gate = new LifeGeo();
            MoonWall(gate, gateHw, baseY, wallH + 0.35f - baseY, 1.35f, th + 0.06f, wall, C(128, 128, 132));
            Coping(gate, -gateHw, gateHw, wallH + 0.35f, 0f, th + 0.06f, tile);
            Place(g, gate, new Vector3(0f, 0f, hz), 0f);
            // back and sides with lattice windows
            for (int side = 0; side < 3; side++)
            {
                float len = side == 0 ? hx : hz;
                var w = new LifeGeo();
                var holes = new List<Rect>();
                int nwin = Mathf.Max(1, Mathf.FloorToInt(len * 2f / 4.5f));
                for (int k = 0; k < nwin; k++)
                {
                    float x = -len + (k + 0.5f) * (2f * len / nwin);
                    holes.Add(new Rect(x - 0.5f, 1.05f, 1.0f, 1.0f));
                }
                WallX(w, -len, len, baseY, wallH, 0f, th, wall, plinth, holes, C(110, 110, 114));
                Coping(w, -len, len, wallH, 0f, th, tile);
                if (side == 0) Place(g, w, new Vector3(0f, 0f, -hz), 0f);
                else Place(g, w, new Vector3(side == 1 ? hx : -hx, 0f, 0f), 90f);
            }
            return m;
        }

        // ------------------------------------------------------------------ painted door gods & couplets
        static void DoorGod(LifeGeo g, Vector3 c, float s, bool left)
        {
            // c = centre of the leaf front face; painted in thin layers (each a little proud of the last)
            Color32 robe = left ? C(224, 172, 52) : C(60, 96, 176), sash = left ? C(60, 140, 90) : C(206, 50, 46);
            float z = c.z;
            g.Box(new Vector3(c.x, c.y - 0.05f * s, z + 0.012f), new Vector3(0.26f * s, 0.62f * s, 0.006f), C(236, 222, 190));
            g.Box(new Vector3(c.x, c.y - 0.12f * s, z + 0.022f), new Vector3(0.22f * s, 0.36f * s, 0.006f), robe);
            g.Box(new Vector3(c.x, c.y - 0.06f * s, z + 0.03f), new Vector3(0.23f * s, 0.04f * s, 0.006f), sash);
            g.Box(new Vector3(c.x - 0.1f * s, c.y - 0.52f * s, z + 0.022f), new Vector3(0.06f * s, 0.1f * s, 0.006f), Pal.Ink);
            g.Box(new Vector3(c.x + 0.1f * s, c.y - 0.52f * s, z + 0.022f), new Vector3(0.06f * s, 0.1f * s, 0.006f), Pal.Ink);
            g.Box(new Vector3(c.x, c.y + 0.33f * s, z + 0.022f), new Vector3(0.09f * s, 0.1f * s, 0.006f), C(240, 196, 160));
            g.Box(new Vector3(c.x, c.y + 0.25f * s, z + 0.03f), new Vector3(0.07f * s, 0.035f * s, 0.006f), Pal.Ink);
            g.Box(new Vector3(c.x, c.y + 0.47f * s, z + 0.022f), new Vector3(0.12f * s, 0.06f * s, 0.006f), left ? Pal.RedBright : C(40, 40, 52));
            g.Box(new Vector3(c.x + (left ? 0.2f : -0.2f) * s, c.y, z + 0.03f), new Vector3(0.02f * s, 0.5f * s, 0.006f), C(120, 120, 128));
        }

        static void Couplet(LifeGeo g, Vector3 c, float h, Color32 ink)
        {
            g.Box(c, new Vector3(0.15f, h * 0.5f, 0.01f), Pal.RedBright);
            int n = Mathf.Max(3, Mathf.FloorToInt(h / 0.24f));
            for (int i = 0; i < n; i++)
            {
                float y = c.y + h * 0.5f - (i + 0.5f) * h / n;
                g.Box(new Vector3(c.x, y, c.z + 0.012f), new Vector3(0.075f, 0.075f, 0.004f), ink);
            }
        }

        static void Fu(LifeGeo g, Vector3 c, float s)
        {
            g.TriOut(c + Up * s, c + Vector3.right * s, c - Up * s, Vector3.forward, Pal.RedBright, Pal.RedBright, Pal.RedBright);
            g.TriOut(c + Up * s, c - Up * s, c + Vector3.left * s, Vector3.forward, Pal.RedBright, Pal.RedBright, Pal.RedBright);
            g.Box(c + Vector3.forward * 0.006f, new Vector3(s * 0.32f, s * 0.36f, 0.004f), Pal.Ink);
        }

        static void Roof90(LifeGeo g, Vector3 c, float hLong, float hShort, float rise, float over, float curl, Color32 a, Color32 b, Color32 u, Color32 r)
        {
            var t = new LifeGeo();
            PG.Roof(t, Vector3.zero, hLong, hShort, rise, over, curl, a, b, u, r, false);
            Place(g, t, c, 90f);
        }

        /// <summary>Lattice window panel on a facade facing +Z (glowing paper behind wooden lattice).</summary>
        static void FacadeWindow(LifeGeo g, LifeGeo gl, float x0, float x1, float y0, float y1, float z, float dir)
        {
            gl.Box(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z), new Vector3((x1 - x0) * 0.5f, (y1 - y0) * 0.5f, 0.02f), Pal.WindowWarm);
            float zf = z + dir * 0.03f;
            Color32 lt = Pal.Lattice;
            PG.BoxMM(g, new Vector3(x0, y0, zf - 0.02f), new Vector3(x1, y0 + 0.06f, zf + 0.02f), lt);
            PG.BoxMM(g, new Vector3(x0, y1 - 0.06f, zf - 0.02f), new Vector3(x1, y1, zf + 0.02f), lt);
            int nx = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / 0.3f)), ny = Mathf.Max(2, Mathf.RoundToInt((y1 - y0) / 0.3f));
            for (int i = 0; i <= nx; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)nx);
                PG.BoxMM(g, new Vector3(x - 0.025f, y0, zf - 0.02f), new Vector3(x + 0.025f, y1, zf + 0.02f), lt);
            }
            for (int j = 1; j < ny; j++)
            {
                float y = Mathf.Lerp(y0, y1, j / (float)ny);
                PG.BoxMM(g, new Vector3(x0, y - 0.02f, zf - 0.02f), new Vector3(x1, y + 0.02f, zf + 0.02f), lt);
            }
        }

        // ------------------------------------------------------------------ courtyard house (siheyuan)
        public static PropModel CourtyardHouse(float bTop, float bBottom, int variant, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            var pr = m[PK.Props];
            float b = bTop;
            Color32 brick = variant == 1 ? C(168, 160, 150) : Pal.Brick, tile = Pal.Tile, tile2 = Pal.Tile2;
            PG.Box2(g, new Vector3(0f, (b + bBottom) * 0.5f, 0.4f), new Vector3(7.8f, (b - bBottom) * 0.5f, 7.2f), Pal.StoneDark, Pal.StoneLight);
            float wh = b + 2.7f, th = 0.4f;
            var w = new LifeGeo();
            WallX(w, -7.5f, 7.5f, b, wh, 0f, th, brick, Pal.BrickDark, null, Pal.Lattice); Coping(w, -7.5f, 7.5f, wh, 0f, th, tile);
            Place(g, w, new Vector3(0f, 0f, -6.5f), 0f);
            for (int s = -1; s <= 1; s += 2)
            {
                var sw = new LifeGeo();
                WallX(sw, -6.5f, 6.5f, b, wh, 0f, th, brick, Pal.BrickDark, null, Pal.Lattice); Coping(sw, -6.5f, 6.5f, wh, 0f, th, tile);
                Place(g, sw, new Vector3(s * 7.5f, 0f, 0f), 90f);
                var fw = new LifeGeo();
                WallX(fw, 1.7f, 7.5f, b, wh, 0f, th, brick, Pal.BrickDark, null, Pal.Lattice); Coping(fw, 1.7f, 7.5f, wh, 0f, th, tile);
                Place(g, fw, new Vector3(0f, 0f, 6.5f), s > 0 ? 0f : 180f);
            }
            // gatehouse
            float gz0 = 5.6f, gz1 = 7.3f, gh = b + 3.4f;
            PG.BoxMM(g, new Vector3(-1.7f, b, gz0), new Vector3(-0.85f, gh, gz1), brick);
            PG.BoxMM(g, new Vector3(0.85f, b, gz0), new Vector3(1.7f, gh, gz1), brick);
            PG.BoxMM(g, new Vector3(-0.85f, b + 2.45f, gz0), new Vector3(0.85f, gh, gz1), Pal.Red);
            PG.BoxMM(g, new Vector3(-0.85f, b, 6.9f), new Vector3(0f, b + 2.45f, 6.98f), Pal.RedDeep);
            PG.BoxMM(g, new Vector3(0f, b, 6.9f), new Vector3(0.85f, b + 2.45f, 6.98f), Pal.RedDeep);
            PG.BoxMM(g, new Vector3(-0.02f, b, 6.97f), new Vector3(0.02f, b + 2.45f, 7.0f), Pal.Ink);
            for (int s = -1; s <= 1; s += 2)
            {
                DoorGod(g, new Vector3(s * 0.42f, b + 1.3f, 6.98f), 0.95f, s < 0);
                g.Disc(new Vector3(s * 0.12f, b + 1.15f, 7.0f), 0.07f, 8, 0f, Vector3.forward, Pal.Gold, Pal.GoldDark);
                Couplet(g, new Vector3(s * 1.28f, b + 1.45f, 7.31f), 1.7f, variant == 2 ? Pal.Gold : Pal.Ink);
                g.Frustum(new Vector3(s * 0.98f, b + 0.36f, 7.32f), new Vector3(s * 0.98f, b + 0.36f, 7.62f), 0.3f, 0.3f, 0.3f, 0.3f, 8, 0f, Pal.Stone, Pal.StoneLight, true, true);
                PG.Rod(gl, new Vector3(s * 1.25f, b + 3.3f, 7.65f), new Vector3(s * 1.25f, b + 3.05f, 7.65f), 0.01f, A(Pal.Ink, 40), 3);
                PG.Lantern(gl, new Vector3(s * 1.25f, b + 3.05f, 7.65f), 0.2f, 0.34f, Pal.RedBright, 120);
            }
            g.Box(new Vector3(0f, b + 2.62f, 7.32f), new Vector3(0.62f, 0.13f, 0.01f), Pal.RedBright);
            for (int i = 0; i < 4; i++) g.Box(new Vector3(-0.39f + i * 0.26f, b + 2.62f, 7.335f), new Vector3(0.08f, 0.08f, 0.004f), variant == 2 ? Pal.Ink : Pal.Gold);
            PG.Roof(g, new Vector3(0f, gh, 6.45f), 1.7f, 0.85f, 0.85f, 0.55f, 0.28f, tile, tile2, Pal.TileUnder, Sh(tile, 0.8f), false);
            if (b > 0.12f)
            {
                PG.BoxMM(g, new Vector3(-1.3f, bBottom, 7.3f), new Vector3(1.3f, b * 0.5f, 8.1f), Pal.Stone);
                PG.BoxMM(g, new Vector3(-1.3f, bBottom, 7.3f), new Vector3(1.3f, b, 7.7f), Pal.Stone);
            }
            // spirit screen with a Fu diamond
            PG.Box2(g, new Vector3(0f, b + 1.2f, 4.6f), new Vector3(1.4f, 1.2f, 0.15f), Pal.Brick, Pal.Brick);
            Coping(g, -1.4f, 1.4f, b + 2.4f, 4.6f, 0.3f, tile);
            var fu = new LifeGeo();
            Fu(fu, Vector3.zero, 0.55f);
            PG.Append(g, fu, Matrix4x4.TRS(new Vector3(0f, b + 1.3f, 4.76f), Quaternion.identity, Vector3.one));
            // main hall
            float hh = b + 3.4f;
            PG.BoxMM(g, new Vector3(-5.6f, b, -6.2f), new Vector3(5.6f, hh, -6.0f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(-5.6f, b, -6.2f), new Vector3(-5.4f, hh, -1.6f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(5.4f, b, -6.2f), new Vector3(5.6f, hh, -1.6f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(-5.6f, b, -1.75f), new Vector3(5.6f, b + 0.9f, -1.6f), Pal.Wood);
            PG.BoxMM(g, new Vector3(-5.6f, b + 2.7f, -1.75f), new Vector3(5.6f, hh, -1.6f), Pal.Red);
            PG.BoxMM(g, new Vector3(-5.6f, b + 0.9f, -1.75f), new Vector3(5.6f, b + 2.7f, -1.7f), Pal.WoodDark);
            FacadeWindow(g, gl, -5.2f, -1.2f, b + 0.95f, b + 2.6f, -1.65f, 1f);
            FacadeWindow(g, gl, 1.2f, 5.2f, b + 0.95f, b + 2.6f, -1.65f, 1f);
            PG.BoxMM(g, new Vector3(-1.1f, b, -1.66f), new Vector3(1.1f, b + 2.6f, -1.6f), Pal.RedDeep);
            var fu2 = new LifeGeo();
            Fu(fu2, Vector3.zero, 0.28f);
            PG.Append(g, fu2, Matrix4x4.TRS(new Vector3(0f, b + 1.7f, -1.58f), Quaternion.identity, Vector3.one));
            for (int i = 0; i < 5; i++) PG.Column(g, new Vector3(-5.6f + i * 2.8f, b, -1.25f), new Vector3(-5.6f + i * 2.8f, hh, -1.25f), 0.15f, Pal.Red);
            PG.Beam(g, new Vector3(-5.8f, hh - 0.15f, -1.25f), new Vector3(5.8f, hh - 0.15f, -1.25f), 0.14f, 0.15f, C(60, 120, 120));
            PG.Roof(g, new Vector3(0f, hh, -3.75f), 5.7f, 2.5f, 1.7f, 1.0f, 0.45f, tile, tile2, Pal.TileUnder, Sh(tile, 0.75f), false);
            // side wings
            for (int s = -1; s <= 1; s += 2)
            {
                float xi = s * 4.1f, xo = s * 7.1f, wt = b + 2.9f;
                PG.BoxMM(g, new Vector3(Mathf.Min(xi, xo), b, -0.8f), new Vector3(Mathf.Max(xi, xo), wt, 4.4f), Pal.Plaster);
                var fw = new LifeGeo();
                var fwg = new LifeGeo();
                FacadeWindow(fw, fwg, -1.8f, 1.8f, b + 0.9f, b + 2.4f, 0f, 1f);
                Place(g, fw, new Vector3(xi + s * -0.02f, 0f, 1.8f), s > 0 ? -90f : 90f);
                Place(gl, fwg, new Vector3(xi + s * -0.02f, 0f, 1.8f), s > 0 ? -90f : 90f);
                Roof90(g, new Vector3(s * 5.6f, wt, 1.8f), 2.6f, 1.5f, 1.1f, 0.7f, 0.35f, tile, tile2, Pal.TileUnder, Sh(tile, 0.75f));
            }
            // courtyard details: fish bowl, potted pomegranates
            Vector3 bowl = new Vector3(2.4f, b, 2.2f);
            pr.Frustum(bowl, bowl + Up * 0.6f, 0.42f, 0.42f, 0.56f, 0.56f, 10, 0f, C(150, 190, 176), C(120, 166, 150), true, false);
            pr.Disc(bowl + Up * 0.55f, 0.53f, 10, 0f, Vector3.up, C(90, 140, 160), C(70, 120, 140));
            for (int i = 0; i < 3; i++) pr.Disc(bowl + new Vector3(Mathf.Cos(i * 2f) * 0.25f, 0.57f, Mathf.Sin(i * 2f) * 0.25f), 0.1f, 6, 0f, Vector3.up, C(90, 150, 70), C(70, 130, 60));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 pot = new Vector3(s * 3.0f, b, -0.7f);
                pr.Frustum(pot, pot + Up * 0.45f, 0.24f, 0.24f, 0.32f, 0.32f, 8, 0f, Pal.Porcelain, Pal.PorcelainBlue, true, false);
                pr.Frustum(pot + Up * 0.45f, pot + Up * 1.1f, 0.04f, 0.04f, 0.03f, 0.03f, 4, 0f, Pal.WoodDark, Pal.WoodDark, false, false);
                int st = pr.v.Count;
                pr.Ellipsoid(pot + Up * 1.4f, new Vector3(0.45f, 0.38f, 0.45f), 6, 4, C(96, 146, 72, 40), C(70, 116, 56, 30));
                PG.Lumpy(pr, st, pot + Up * 1.4f, 0.2f, s * 3f);
                for (int k = 0; k < 4; k++) pr.Box(pot + new Vector3(Mathf.Cos(k * 1.7f) * 0.38f, 1.35f + (k & 1) * 0.15f, Mathf.Sin(k * 1.7f) * 0.38f), new Vector3(0.05f, 0.05f, 0.05f), C(214, 50, 40, 40));
            }
            return m;
        }

        // ------------------------------------------------------------------ tea house
        public static PropModel TeaHouse(float dT, float dB, System.Random rng, List<Vector3> steam)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            var pr = m[PK.Props];
            var st = m[PK.Strong];
            PG.Box2(g, new Vector3(0f, (dT + dB) * 0.5f, 0f), new Vector3(6.6f, (dT - dB) * 0.5f, 7.6f), Pal.StoneDark, C(196, 168, 128));
            for (int i = 0; i < 14; i++) PG.BoxMM(g, new Vector3(-6.6f, dT, -7.6f + i * 1.0f + 0.48f), new Vector3(6.6f, dT + 0.01f, -7.6f + i * 1.0f + 0.52f), C(170, 140, 104));
            if (dT > 0.15f)
            {
                int steps = Mathf.Clamp(Mathf.CeilToInt(dT / 0.2f), 1, 6);
                for (int i = 0; i < steps; i++)
                {
                    float y = dT * (steps - i) / steps;
                    PG.Box2(g, new Vector3(0f, (y + dB) * 0.5f, 7.6f + (i + 0.5f) * 0.32f), new Vector3(1.5f, (y - dB) * 0.5f, 0.16f), Pal.StoneDark, Pal.Stone);
                }
            }
            // hall
            float wt = dT + 3.2f;
            PG.BoxMM(g, new Vector3(-5.5f, dT, -7.2f), new Vector3(5.5f, wt, -7.0f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(-5.5f, dT, -7.2f), new Vector3(-5.3f, wt, -2.0f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(5.3f, dT, -7.2f), new Vector3(5.5f, wt, -2.0f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(-5.5f, dT, -2.15f), new Vector3(5.5f, dT + 0.8f, -2.0f), Pal.Wood);
            PG.BoxMM(g, new Vector3(-5.5f, dT + 2.8f, -2.15f), new Vector3(5.5f, wt, -2.0f), Pal.Wood);
            PG.BoxMM(g, new Vector3(-5.5f, dT + 0.8f, -2.15f), new Vector3(5.5f, dT + 2.8f, -2.1f), Pal.WoodDark);
            FacadeWindow(g, gl, -5.1f, -2.95f, dT + 0.9f, dT + 2.7f, -2.05f, 1f);
            FacadeWindow(g, gl, 2.95f, 5.1f, dT + 0.9f, dT + 2.7f, -2.05f, 1f);
            FacadeWindow(g, gl, -2.55f, -0.1f, dT + 1.3f, dT + 2.7f, -2.05f, 1f);
            FacadeWindow(g, gl, 0.1f, 2.55f, dT + 1.3f, dT + 2.7f, -2.05f, 1f);
            for (int s = -1; s <= 1; s += 2)
            {
                var sw = new LifeGeo(); var swg = new LifeGeo();
                FacadeWindow(sw, swg, -1.6f, 1.6f, dT + 1.0f, dT + 2.5f, 0f, 1f);
                Place(g, sw, new Vector3(s * 5.52f, 0f, -4.6f), s > 0 ? 90f : -90f);
                Place(gl, swg, new Vector3(s * 5.52f, 0f, -4.6f), s > 0 ? 90f : -90f);
            }
            for (int i = 0; i < 5; i++) PG.Column(g, new Vector3(-5.5f + i * 2.75f, dT, -1.75f), new Vector3(-5.5f + i * 2.75f, wt, -1.75f), 0.16f, Pal.Red);
            PG.Beam(g, new Vector3(-5.7f, wt - 0.15f, -1.75f), new Vector3(5.7f, wt - 0.15f, -1.75f), 0.15f, 0.16f, C(56, 118, 122));
            for (int i = 0; i < 6; i++) g.Box(new Vector3(-4.6f + i * 1.84f, wt - 0.15f, -1.58f), new Vector3(0.35f, 0.05f, 0.01f), Pal.Gold);
            PG.Roof(g, new Vector3(0f, wt, -4.45f), 5.6f, 2.75f, 1.0f, 1.15f, 0.5f, Pal.Tile, Pal.Tile2, Pal.TileUnder, Sh(Pal.Tile, 0.75f), false);
            PG.BoxMM(g, new Vector3(-4.3f, wt, -6.3f), new Vector3(4.3f, wt + 1.2f, -2.6f), Pal.Plaster);
            FacadeWindow(g, gl, -3.6f, -0.3f, wt + 0.35f, wt + 1.0f, -2.58f, 1f);
            FacadeWindow(g, gl, 0.3f, 3.6f, wt + 0.35f, wt + 1.0f, -2.58f, 1f);
            PG.Roof(g, new Vector3(0f, wt + 1.2f, -4.45f), 4.3f, 1.85f, 1.5f, 0.95f, 0.55f, Pal.Tile, Pal.Tile2, Pal.TileUnder, Sh(Pal.Tile, 0.75f), false);
            // lanterns under the front eave
            for (int i = 0; i < 4; i++)
            {
                float x = -4.1f + i * 2.73f;
                PG.Rod(gl, new Vector3(x, wt - 0.05f, -0.9f), new Vector3(x, wt - 0.45f, -0.9f), 0.01f, A(Pal.Ink, 50), 3);
                PG.Lantern(gl, new Vector3(x, wt - 0.45f, -0.9f), 0.24f, 0.42f, Pal.RedBright, 110);
            }
            // tea flag on a pole at the front corner
            Vector3 fp = new Vector3(5.9f, dT, 6.9f);
            PG.Column(g, fp, fp + Up * 6.2f, 0.07f, Pal.WoodDark, 5);
            PG.Beam(g, fp + Up * 5.9f, fp + new Vector3(-1.2f, 5.9f, 0f), 0.03f, 0.03f, Pal.WoodDark);
            for (int k = 0; k < 4; k++)
            {
                float y0 = 5.85f - k * 0.55f, y1 = y0 - 0.55f;
                Color32 cc = k == 3 ? Pal.RedBright : C(242, 236, 214);
                PG.Quad2(st, fp + new Vector3(-1.15f, y0, 0f), fp + new Vector3(-0.3f, y0, 0f), fp + new Vector3(-0.3f, y1, 0f), fp + new Vector3(-1.15f, y1, 0f),
                         A(cc, k * 60), A(cc, k * 60), A(cc, k * 60 + 60), A(cc, k * 60 + 60));
            }
            g.Box(fp + new Vector3(-0.73f, 4.6f, 0.012f), new Vector3(0.24f, 0.24f, 0.004f), Pal.Ink);
            g.Box(fp + new Vector3(-0.73f, 4.6f, -0.012f), new Vector3(0.24f, 0.24f, 0.004f), Pal.Ink);
            // terrace: tables, railings, pots
            Vector3[] tables = { new Vector3(-3.3f, dT, 0.8f), new Vector3(3.3f, dT, 0.8f), new Vector3(-3.3f, dT, 4.4f), new Vector3(3.3f, dT, 4.4f), new Vector3(0f, dT, 2.6f) };
            foreach (var tp in tables) steam.Add(TeaTable(m, tp, rng));
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k <= 6; k++)
                {
                    float z = -1.4f + k * 1.45f;
                    PG.Beam(pr, new Vector3(s * 6.45f, dT, z), new Vector3(s * 6.45f, dT + 0.85f, z), 0.05f, 0.05f, Pal.Red);
                }
                PG.Beam(pr, new Vector3(s * 6.45f, dT + 0.82f, -1.4f), new Vector3(s * 6.45f, dT + 0.82f, 7.3f), 0.05f, 0.04f, Pal.Red);
                PG.Beam(pr, new Vector3(s * 6.45f, dT + 0.82f, 7.3f), new Vector3(s * 1.6f, dT + 0.82f, 7.3f), 0.05f, 0.04f, Pal.Red);
                PG.Beam(pr, new Vector3(s * 1.6f, dT, 7.3f), new Vector3(s * 1.6f, dT + 0.85f, 7.3f), 0.06f, 0.06f, Pal.Red);
                Vector3 pot = new Vector3(s * 2.1f, dT, 6.7f);
                pr.Frustum(pot, pot + Up * 0.5f, 0.3f, 0.3f, 0.38f, 0.38f, 8, 0f, Pal.Porcelain, Pal.PorcelainBlue, true, false);
                int sv = pr.v.Count;
                pr.Ellipsoid(pot + Up * 0.95f, new Vector3(0.5f, 0.45f, 0.5f), 6, 4, s > 0 ? C(244, 150, 186, 40) : C(250, 246, 240, 40), C(110, 150, 80, 30));
                PG.Lumpy(pr, sv, pot + Up * 0.95f, 0.2f, 4f + s);
            }
            return m;
        }

        // ------------------------------------------------------------------ drum / bell tower
        /// <summary>Two storey tower on a brick podium. hang = local bell hang point (bell tower only).</summary>
        public static PropModel Tower(bool bell, float bt, float bb, out Vector3 hang, out Vector3 logPivot)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 brick = C(150, 132, 124), red = Pal.Red;
            float top = bt + 3.8f;
            PG.Box2(g, new Vector3(0f, (top + bb) * 0.5f, 0f), new Vector3(3.6f, (top - bb) * 0.5f, 3.6f), brick, C(170, 150, 140));
            PG.Box2(g, new Vector3(0f, bt + 0.25f, 0f), new Vector3(3.75f, 0.25f, 3.75f), Pal.Stone, Pal.Stone);
            for (int s = -1; s <= 1; s += 2)
            {
                // painted arch doorway front and back
                g.Box(new Vector3(0f, bt + 0.5f + 1.0f, s * 3.61f), new Vector3(0.9f, 1.0f, 0.02f), C(54, 40, 40));
                g.Disc(new Vector3(0f, bt + 2.5f, s * 3.62f), 0.9f, 12, 0f, Vector3.forward * s, C(54, 40, 40), C(54, 40, 40));
                g.Box(new Vector3(0f, bt + 3.55f, s * 3.62f), new Vector3(0.9f, 0.22f, 0.01f), C(54, 40, 40));
            }
            // parapet
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                Vector3 a = q * new Vector3(-3.6f, top + 0.3f, 3.45f), b2 = q * new Vector3(3.6f, top + 0.3f, 3.45f);
                PG.Beam(g, a, b2, 0.15f, 0.3f, Sh(brick, 0.9f));
            }
            PG.BoxMM(g, new Vector3(-3.0f, top, -3.0f), new Vector3(3.0f, top + 0.18f, 3.0f), Pal.WoodDark);
            float ct = top + 3.0f;
            float[] cx = { -2.6f, -0.87f, 0.87f, 2.6f };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    if (i != 0 && i != 3 && j != 0 && j != 3) continue;
                    PG.Column(g, new Vector3(cx[i], top, cx[j]), new Vector3(cx[i], ct, cx[j]), 0.15f, red);
                }
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                PG.Beam(g, q * new Vector3(-2.75f, ct - 0.12f, 2.6f), q * new Vector3(2.75f, ct - 0.12f, 2.6f), 0.13f, 0.16f, C(56, 118, 122));
                PG.Beam(g, q * new Vector3(-2.6f, top + 0.85f, 2.6f), q * new Vector3(2.6f, top + 0.85f, 2.6f), 0.05f, 0.05f, red);
                for (int p = 0; p < 9; p++)
                {
                    float x = -2.6f + p * 0.65f;
                    PG.Beam(g, q * new Vector3(x, top + 0.18f, 2.6f), q * new Vector3(x, top + 0.85f, 2.6f), 0.03f, 0.03f, red);
                }
            }
            PG.Roof(g, new Vector3(0f, ct, 0f), 2.7f, 2.7f, 0.85f, 1.15f, 0.42f, Pal.Tile, Pal.Tile2, Pal.TileUnder, Sh(Pal.Tile, 0.75f), false);
            PG.Box2(g, new Vector3(0f, ct + 0.65f, 0f), new Vector3(2.0f, 0.65f, 2.0f), red, red);
            for (int k = 0; k < 4; k++)
            {
                var wv = new LifeGeo(); var wg = new LifeGeo();
                FacadeWindow(wv, wg, -1.5f, 1.5f, ct + 0.3f, ct + 1.05f, 0f, 1f);
                Place(g, wv, new Vector3(0f, 0f, 0f) + Quaternion.Euler(0f, k * 90f, 0f) * new Vector3(0f, 0f, 2.02f), k * 90f);
                Place(gl, wg, Quaternion.Euler(0f, k * 90f, 0f) * new Vector3(0f, 0f, 2.02f), k * 90f);
            }
            PG.Roof(g, new Vector3(0f, ct + 1.3f, 0f), 2.0f, 2.0f, 1.7f, 1.15f, 0.6f, Pal.Tile, Pal.Tile2, Pal.TileUnder, Sh(Pal.Tile, 0.75f), true);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Rod(gl, new Vector3(s * 2.6f, ct - 0.25f, 3.3f), new Vector3(s * 2.6f, ct - 0.6f, 3.3f), 0.01f, A(Pal.Ink, 50), 3);
                PG.Lantern(gl, new Vector3(s * 2.6f, ct - 0.6f, 3.3f), 0.24f, 0.42f, Pal.RedBright, 110);
            }
            hang = new Vector3(0f, ct - 0.25f, 0.3f);
            logPivot = new Vector3(0f, ct - 0.25f, -1.75f);
            if (bell)
            {
                PG.Beam(g, new Vector3(-2.6f, ct - 0.2f, 0.3f), new Vector3(2.6f, ct - 0.2f, 0.3f), 0.12f, 0.12f, Pal.WoodDark);
                PG.Beam(g, new Vector3(-2.6f, ct - 0.2f, -1.75f), new Vector3(2.6f, ct - 0.2f, -1.75f), 0.1f, 0.1f, Pal.WoodDark);
            }
            else
            {
                Vector3 dc = new Vector3(0f, top + 1.7f, 0f);
                Color32 drumRed = C(176, 34, 34), head = C(232, 210, 172);
                g.Frustum(dc + new Vector3(0f, 0f, -0.55f), dc, 1.0f, 1.0f, 1.12f, 1.12f, 14, 0f, drumRed, Sh(drumRed, 1.1f), false, false);
                g.Frustum(dc, dc + new Vector3(0f, 0f, 0.55f), 1.12f, 1.12f, 1.0f, 1.0f, 14, 0f, Sh(drumRed, 1.1f), drumRed, false, false);
                for (int s = -1; s <= 1; s += 2)
                {
                    g.Disc(dc + new Vector3(0f, 0f, s * 0.551f), 1.0f, 14, 0f, Vector3.forward * s, head, Sh(head, 0.9f));
                    g.Disc(dc + new Vector3(0f, 0f, s * 0.556f), 0.3f, 8, 0f, Vector3.forward * s, C(200, 60, 50), C(200, 60, 50));
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI / 8f;
                        g.Box(dc + new Vector3(Mathf.Cos(a) * 0.98f, Mathf.Sin(a) * 0.98f, s * 0.52f), new Vector3(0.04f, 0.04f, 0.04f), Pal.Gold);
                    }
                    PG.Beam(g, new Vector3(s * 0.9f, top + 0.18f, -0.4f), new Vector3(s * 0.75f, top + 1.1f, 0f), 0.06f, 0.06f, Pal.WoodDark);
                    PG.Beam(g, new Vector3(s * 0.9f, top + 0.18f, 0.4f), new Vector3(s * 0.75f, top + 1.1f, 0f), 0.06f, 0.06f, Pal.WoodDark);
                }
                PG.Beam(g, new Vector3(-0.5f, top + 1.0f, 1.0f), new Vector3(0.3f, top + 1.4f, 1.1f), 0.03f, 0.03f, Pal.RedDeep);
                PG.Beam(g, new Vector3(0.5f, top + 1.0f, 1.0f), new Vector3(-0.2f, top + 1.45f, 1.15f), 0.03f, 0.03f, Pal.RedDeep);
            }
            return m;
        }

        /// <summary>Bronze bell hanging down from the origin (movers).</summary>
        public static LifeGeo Bell()
        {
            var g = new LifeGeo();
            Color32 br = C(110, 92, 58), brD = C(76, 62, 42), brL = C(150, 126, 76);
            PG.Beam(g, Vector3.zero, new Vector3(0f, -0.25f, 0f), 0.06f, 0.06f, brD);
            g.Ellipsoid(new Vector3(0f, -0.45f, 0f), new Vector3(0.6f, 0.26f, 0.6f), 10, 4, brL, br);
            g.Frustum(new Vector3(0f, -0.45f, 0f), new Vector3(0f, -1.45f, 0f), 0.6f, 0.6f, 0.78f, 0.78f, 12, 0f, br, brD, false, false);
            g.Frustum(new Vector3(0f, -1.45f, 0f), new Vector3(0f, -1.6f, 0f), 0.78f, 0.78f, 0.84f, 0.84f, 12, 0f, brL, brD, false, false);
            g.Frustum(new Vector3(0f, -0.78f, 0f), new Vector3(0f, -0.84f, 0f), 0.66f, 0.66f, 0.67f, 0.67f, 12, 0f, brL, brL, false, false);
            g.Frustum(new Vector3(0f, -1.15f, 0f), new Vector3(0f, -1.21f, 0f), 0.73f, 0.73f, 0.74f, 0.74f, 12, 0f, brL, brL, false, false);
            g.Disc(new Vector3(0f, -1.58f, 0f), 0.82f, 12, 0f, Vector3.down, C(40, 34, 28), C(40, 34, 28));
            g.Ellipsoid(new Vector3(0f, -1.0f, -0.72f), new Vector3(0.14f, 0.14f, 0.06f), 6, 3, Pal.Gold, brL);
            return g;
        }

        /// <summary>Striker log hung by two ropes; origin = rope top (pivot), log points along +Z towards the bell.</summary>
        public static LifeGeo StrikerLog()
        {
            var g = new LifeGeo();
            for (int s = -1; s <= 1; s += 2)
                PG.Rod(g, new Vector3(s * 0.25f, 0f, 0f), new Vector3(s * 0.12f, -0.95f, 0f), 0.02f, C(200, 180, 130), 3);
            g.Frustum(new Vector3(0f, -0.98f, -0.75f), new Vector3(0f, -0.98f, 0.75f), 0.14f, 0.14f, 0.13f, 0.13f, 7, 0f, Pal.Wood, Pal.WoodLight, true, true);
            return g;
        }

        // ------------------------------------------------------------------ water wheel
        /// <summary>Wheel rotating about local X (origin = hub).</summary>
        public static LifeGeo WaterWheel(float R)
        {
            var g = new LifeGeo();
            Color32 w = C(140, 108, 76), wd = C(100, 76, 54);
            const int N = 16;
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.42f;
                for (int k = 0; k < N; k++)
                {
                    float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                    PG.Beam(g, new Vector3(x, Mathf.Cos(a0) * R, Mathf.Sin(a0) * R), new Vector3(x, Mathf.Cos(a1) * R, Mathf.Sin(a1) * R), 0.05f, 0.08f, wd);
                    PG.Beam(g, new Vector3(x, Mathf.Cos(a0) * R * 0.62f, Mathf.Sin(a0) * R * 0.62f), new Vector3(x, Mathf.Cos(a1) * R * 0.62f, Mathf.Sin(a1) * R * 0.62f), 0.035f, 0.05f, wd);
                    if ((k & 1) == 0) PG.Beam(g, new Vector3(x, 0f, 0f), new Vector3(x, Mathf.Cos(a0) * R, Mathf.Sin(a0) * R), 0.045f, 0.045f, w);
                }
            }
            for (int k = 0; k < N; k++)
            {
                float a = (k + 0.5f) * 360f / N;
                Quaternion q = Quaternion.AngleAxis(a, Vector3.right);
                Vector3 radial = q * Vector3.up;
                PG.OBox(g, radial * (R - 0.2f), new Vector3(0.48f, 0.32f, 0.03f), q, w);
                if ((k & 1) == 0) g.Frustum(radial * (R + 0.05f) + Vector3.left * 0.2f, radial * (R + 0.05f) + Vector3.right * 0.2f, 0.07f, 0.07f, 0.07f, 0.07f, 5, 0f, Pal.Bamboo, Pal.Bamboo, true, true);
            }
            g.Frustum(new Vector3(-0.6f, 0f, 0f), new Vector3(0.6f, 0f, 0f), 0.24f, 0.24f, 0.24f, 0.24f, 8, 0f, wd, wd, true, true);
            g.Frustum(new Vector3(-1.05f, 0f, 0f), new Vector3(1.05f, 0f, 0f), 0.07f, 0.07f, 0.07f, 0.07f, 6, 0f, Pal.BronzeDark, Pal.BronzeDark, true, true);
            return g;
        }

        /// <summary>Static frame for a water wheel: A-frames down to the river bed and a flume to the bank (+X).</summary>
        public static PropModel WheelFrame(float R, float down, float flumeLen, float flumeDrop)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 w = C(120, 92, 66), wd = C(90, 68, 50);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.85f;
                PG.Beam(g, new Vector3(x, 0.2f, 0f), new Vector3(x, -down, 1.6f), 0.08f, 0.08f, wd);
                PG.Beam(g, new Vector3(x, 0.2f, 0f), new Vector3(x, -down, -1.6f), 0.08f, 0.08f, wd);
                PG.Beam(g, new Vector3(x, -down * 0.55f, 1.0f), new Vector3(x, -down * 0.55f, -1.0f), 0.05f, 0.05f, w);
                PG.Box2(g, new Vector3(x, 0.05f, 0f), new Vector3(0.12f, 0.16f, 0.2f), wd, w);
            }
            // flume: catches water at the top of the wheel and carries it to the bank
            Vector3 f0 = new Vector3(0.5f, R + 0.25f, -0.5f), f1 = new Vector3(flumeLen, R + 0.25f - flumeDrop, -0.5f);
            Vector3 dir = (f1 - f0).normalized;
            Quaternion q = Quaternion.FromToRotation(Vector3.right, dir);
            Vector3 mid = (f0 + f1) * 0.5f;
            float hl = (f1 - f0).magnitude * 0.5f;
            PG.OBox(g, mid, new Vector3(hl, 0.04f, 0.28f), q, w);
            PG.OBox(g, mid + q * new Vector3(0f, 0.14f, 0.26f), new Vector3(hl, 0.14f, 0.03f), q, wd);
            PG.OBox(g, mid + q * new Vector3(0f, 0.14f, -0.26f), new Vector3(hl, 0.14f, 0.03f), q, wd);
            PG.OBox(g, mid + q * new Vector3(0f, 0.06f, 0f), new Vector3(hl, 0.01f, 0.23f), q, C(150, 200, 214));
            float postX = flumeLen - 0.3f;
            PG.Beam(g, new Vector3(postX, R + 0.2f - flumeDrop, -0.5f), new Vector3(postX, -down * 0.6f, -0.5f), 0.07f, 0.07f, wd);
            PG.Beam(g, new Vector3(flumeLen * 0.5f, R + 0.2f - flumeDrop * 0.5f, -0.5f), new Vector3(flumeLen * 0.5f, -down, -0.5f), 0.07f, 0.07f, wd);
            return m;
        }

        // ------------------------------------------------------------------ washing line
        public static PropModel WashingLine(System.Random rng, float len)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            float hx = len * 0.5f, y = 2.2f, sag = 0.22f;
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Rod(g, new Vector3(s * hx, -0.3f, 0f), new Vector3(s * hx, y + 0.1f, 0f), 0.05f, Pal.Bamboo, 5);
                PG.Beam(g, new Vector3(s * hx, y + 0.05f, -0.3f), new Vector3(s * hx, y + 0.05f, 0.3f), 0.03f, 0.03f, Pal.Bamboo);
            }
            const int NL = 8;
            for (int i = 0; i < NL; i++)
            {
                float t0 = i / (float)NL, t1 = (i + 1) / (float)NL;
                PG.Beam(g, LinePt(t0, hx, y, sag), LinePt(t1, hx, y, sag), 0.012f, 0.012f, C(60, 56, 52));
            }
            Color32[] cloth = { C(52, 74, 146), C(206, 56, 52), C(242, 238, 226), C(234, 200, 80), C(236, 150, 180), C(90, 150, 200), C(80, 140, 100) };
            float x = -hx + 0.35f;
            while (x < hx - 0.6f)
            {
                float w = R(rng, 0.45f, 0.95f), h = R(rng, 0.55f, 1.25f);
                if (x + w > hx - 0.25f) break;
                Color32 c0 = cloth[rng.Next(cloth.Length)], c1 = rng.NextDouble() < 0.5 ? c0 : cloth[rng.Next(cloth.Length)];
                const int NY = 3;
                for (int k = 0; k < NY; k++)
                    for (int j = 0; j < 2; j++)
                    {
                        float xa = x + w * j / 2f, xb = x + w * (j + 1) / 2f;
                        float ya0 = LinePt((xa + hx) / len, hx, y, sag).y - 0.02f, yb0 = LinePt((xb + hx) / len, hx, y, sag).y - 0.02f;
                        float f0 = k / (float)NY, f1 = (k + 1) / (float)NY;
                        Color32 col = ((k + j) & 1) == 0 ? c0 : c1;
                        int a0 = (int)(f0 * 230f), a1 = (int)(f1 * 230f);
                        PG.Quad2(st, new Vector3(xa, ya0 - h * f0, 0f), new Vector3(xb, yb0 - h * f0, 0f), new Vector3(xb, yb0 - h * f1, 0f), new Vector3(xa, ya0 - h * f1, 0f),
                                 A(col, a0), A(col, a0), A(col, a1), A(col, a1));
                    }
                x += w + R(rng, 0.12f, 0.3f);
            }
            return m;
        }

        static Vector3 LinePt(float t, float hx, float y, float sag)
        {
            return new Vector3(Mathf.Lerp(-hx, hx, t), y - 4f * sag * t * (1f - t), 0f);
        }

        // ------------------------------------------------------------------ vehicles
        static void Wheel(LifeGeo g, Vector3 c, float r, int segs, float rimW, Color32 rim, Color32 spoke, int spokes, float spokeR)
        {
            for (int k = 0; k < segs; k++)
            {
                float a0 = k * Mathf.PI * 2f / segs, a1 = (k + 1) * Mathf.PI * 2f / segs;
                PG.Beam(g, c + new Vector3(0f, Mathf.Cos(a0) * r, Mathf.Sin(a0) * r), c + new Vector3(0f, Mathf.Cos(a1) * r, Mathf.Sin(a1) * r), rimW, rimW * 1.4f, rim);
            }
            for (int k = 0; k < spokes; k++)
            {
                float a = k * Mathf.PI * 2f / spokes + 0.2f;
                PG.Beam(g, c, c + new Vector3(0f, Mathf.Cos(a) * r, Mathf.Sin(a) * r), spokeR, spokeR, spoke);
            }
            g.Frustum(c + Vector3.left * 0.06f, c + Vector3.right * 0.06f, rimW * 2f, rimW * 2f, rimW * 2f, rimW * 2f, 6, 0f, spoke, spoke, true, true);
        }

        public static LifeGeo Bicycle(Color32 frame, bool basket, System.Random rng)
        {
            var g = new LifeGeo();
            Color32 tyre = C(36, 36, 40), steel = C(176, 176, 182);
            Vector3 rh = new Vector3(0f, 0.34f, -0.52f), fh = new Vector3(0f, 0.34f, 0.52f);
            Wheel(g, rh, 0.33f, 12, 0.018f, tyre, steel, 4, 0.005f);
            Wheel(g, fh, 0.33f, 12, 0.018f, tyre, steel, 4, 0.005f);
            Vector3 bb = new Vector3(0f, 0.3f, -0.08f), seat = new Vector3(0f, 0.84f, -0.24f), head = new Vector3(0f, 0.86f, 0.34f), headL = new Vector3(0f, 0.66f, 0.38f);
            float t = 0.022f;
            PG.Beam(g, bb, seat, t, t, frame); PG.Beam(g, seat, head, t, t, frame); PG.Beam(g, bb, headL, t, t, frame);
            PG.Beam(g, headL, head, t, t, frame); PG.Beam(g, rh, bb, t * 0.8f, t * 0.8f, frame); PG.Beam(g, rh, seat, t * 0.8f, t * 0.8f, frame);
            PG.Beam(g, headL, fh, t * 0.8f, t * 0.8f, frame);
            PG.Beam(g, head, head + new Vector3(0f, 0.12f, -0.03f), t, t, steel);
            PG.Beam(g, head + new Vector3(-0.27f, 0.12f, -0.06f), head + new Vector3(0.27f, 0.12f, -0.06f), 0.014f, 0.014f, steel);
            PG.BoxMM(g, seat + new Vector3(-0.08f, 0.04f, -0.12f), seat + new Vector3(0.08f, 0.09f, 0.08f), C(80, 52, 40));
            PG.BoxMM(g, new Vector3(-0.12f, 0.6f, -0.78f), new Vector3(0.12f, 0.62f, -0.4f), steel);
            PG.Beam(g, new Vector3(0f, 0.6f, -0.76f), rh, 0.01f, 0.01f, steel);
            PG.Beam(g, new Vector3(-0.1f, 0.24f, -0.08f), new Vector3(0.1f, 0.24f, -0.08f), 0.03f, 0.015f, tyre);
            if (basket)
            {
                Vector3 bp = new Vector3(0f, 0.86f, 0.6f);
                g.Frustum(bp - Up * 0.1f, bp + Up * 0.1f, 0.14f, 0.12f, 0.18f, 0.15f, 6, 0f, C(176, 136, 80), C(206, 168, 104), true, false);
                if (rng.NextDouble() < 0.6)
                {
                    Color32 veg = rng.NextDouble() < 0.5 ? C(140, 190, 80) : C(240, 150, 50);
                    for (int i = 0; i < 3; i++) g.Ellipsoid(bp + new Vector3((i - 1) * 0.08f, 0.1f, 0f), new Vector3(0.07f, 0.06f, 0.07f), 5, 3, veg, Sh(veg, 0.8f));
                }
            }
            return g;
        }

        public static PropModel Rickshaw(int variant)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 lac = variant == 0 ? C(40, 36, 42) : C(150, 36, 36), cush = variant == 0 ? C(170, 40, 44) : C(232, 196, 90);
            for (int s = -1; s <= 1; s += 2)
                Wheel(g, new Vector3(s * 0.66f, 0.62f, 0f), 0.6f, 14, 0.025f, C(36, 36, 40), C(180, 180, 186), 8, 0.008f);
            PG.Beam(g, new Vector3(-0.7f, 0.62f, 0f), new Vector3(0.7f, 0.62f, 0f), 0.03f, 0.03f, C(60, 60, 64));
            PG.Box2(g, new Vector3(0f, 0.85f, -0.2f), new Vector3(0.5f, 0.1f, 0.32f), lac, cush);
            PG.BoxMM(g, new Vector3(-0.5f, 0.85f, -0.56f), new Vector3(0.5f, 1.5f, -0.46f), lac);
            PG.BoxMM(g, new Vector3(-0.44f, 0.95f, -0.46f), new Vector3(0.44f, 1.42f, -0.42f), cush);
            for (int s = -1; s <= 1; s += 2) PG.BoxMM(g, new Vector3(s * 0.5f - 0.03f, 0.75f, -0.5f), new Vector3(s * 0.5f + 0.03f, 1.1f, 0.12f), lac);
            PG.BoxMM(g, new Vector3(-0.42f, 0.36f, 0.15f), new Vector3(0.42f, 0.42f, 0.8f), C(50, 44, 40));
            PG.Beam(g, new Vector3(-0.42f, 0.42f, 0.15f), new Vector3(-0.42f, 0.75f, 0.12f), 0.025f, 0.025f, lac);
            PG.Beam(g, new Vector3(0.42f, 0.42f, 0.15f), new Vector3(0.42f, 0.75f, 0.12f), 0.025f, 0.025f, lac);
            // folding hood
            Vector3 hc = new Vector3(0f, 1.05f, -0.55f);
            Color32 hoodO = C(46, 42, 48), hoodI = C(130, 44, 44);
            const int NH = 6;
            for (int i = 0; i < NH; i++)
            {
                float a0 = Mathf.Deg2Rad * (i * 140f / NH), a1 = Mathf.Deg2Rad * ((i + 1) * 140f / NH);
                Vector3 d0 = new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * 0.72f, d1 = new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * 0.72f;
                Vector3 l0 = hc + d0 + Vector3.left * 0.58f, r0 = hc + d0 + Vector3.right * 0.58f, l1 = hc + d1 + Vector3.left * 0.58f, r1 = hc + d1 + Vector3.right * 0.58f;
                Vector3 outward = (d0 + d1);
                g.QuadOut(l0, r0, r1, l1, outward, hoodO, hoodO, hoodO, hoodO);
                g.QuadOut(l0, r0, r1, l1, -outward, hoodI, hoodI, hoodI, hoodI);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 cc = hc + Vector3.right * s * 0.58f;
                    g.TriOut(cc, cc + d0, cc + d1, Vector3.right * s, hoodO, hoodO, hoodO);
                    g.TriOut(cc, cc + d0, cc + d1, -Vector3.right * s, hoodI, hoodI, hoodI);
                }
            }
            for (int s = -1; s <= 1; s += 2)
                PG.Beam(g, new Vector3(s * 0.42f, 0.82f, 0.15f), new Vector3(s * 0.42f, 0.08f, 2.3f), 0.035f, 0.035f, lac);
            PG.Beam(g, new Vector3(-0.42f, 0.12f, 2.2f), new Vector3(0.42f, 0.12f, 2.2f), 0.03f, 0.03f, lac);
            return m;
        }

        public static PropModel Cart(int load, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 w = C(150, 112, 74), wd = C(104, 76, 52);
            for (int s = -1; s <= 1; s += 2) Wheel(g, new Vector3(s * 0.82f, 0.5f, 0f), 0.48f, 12, 0.045f, wd, w, 6, 0.03f);
            PG.Beam(g, new Vector3(-0.9f, 0.5f, 0f), new Vector3(0.9f, 0.5f, 0f), 0.04f, 0.04f, wd);
            PG.Box2(g, new Vector3(0f, 0.62f, 0.05f), new Vector3(0.72f, 0.05f, 1.15f), wd, w);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Beam(g, new Vector3(s * 0.7f, 0.9f, -1.1f), new Vector3(s * 0.7f, 0.9f, 1.2f), 0.03f, 0.04f, w);
                for (int k = 0; k < 4; k++)
                {
                    float z = -1.1f + k * 0.77f;
                    PG.Beam(g, new Vector3(s * 0.7f, 0.66f, z), new Vector3(s * 0.7f, 0.92f, z), 0.025f, 0.025f, wd);
                }
                PG.Beam(g, new Vector3(s * 0.45f, 0.62f, 1.15f), new Vector3(s * 0.45f, 0.06f, 2.7f), 0.04f, 0.045f, wd);
            }
            PG.Beam(g, new Vector3(-0.45f, 0.1f, 2.6f), new Vector3(0.45f, 0.1f, 2.6f), 0.03f, 0.03f, wd);
            Vector3 bed = new Vector3(0f, 0.67f, 0.05f);
            switch (load)
            {
                case 0:
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = bed + new Vector3((i & 1) * 0.6f - 0.3f, 0.2f + (i >> 1) * 0.0f, (i >> 1) * 0.9f - 0.45f);
                        PG.Box2(g, p, new Vector3(0.28f, 0.2f, 0.42f), C(214, 184, 100), C(232, 204, 120));
                    }
                    PG.Box2(g, bed + new Vector3(0f, 0.6f, 0f), new Vector3(0.28f, 0.2f, 0.42f), C(214, 184, 100), C(232, 204, 120));
                    break;
                case 1:
                    for (int i = 0; i < 10; i++)
                    {
                        Vector3 p = bed + new Vector3(R(rng, -0.5f, 0.5f), 0.16f + (i > 6 ? 0.18f : 0f), R(rng, -0.9f, 0.9f));
                        int s0 = g.v.Count;
                        g.Ellipsoid(p, new Vector3(0.17f, 0.15f, 0.17f), 6, 3, C(156, 206, 104), C(110, 160, 80));
                        PG.Lumpy(g, s0, p, 0.15f, i);
                    }
                    break;
                case 2:
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = bed + new Vector3((i & 1) * 0.6f - 0.3f, 0f, (i >> 1) * 0.9f - 0.45f);
                        g.Frustum(p, p + Up * 0.55f, 0.2f, 0.2f, 0.27f, 0.27f, 8, 0f, C(176, 136, 80), C(206, 168, 104), true, false);
                        int s0 = g.v.Count;
                        g.Ellipsoid(p + Up * 0.55f, new Vector3(0.25f, 0.12f, 0.25f), 6, 3, C(110, 170, 80), C(80, 140, 60));
                        PG.Lumpy(g, s0, p + Up * 0.55f, 0.2f, i * 2f);
                    }
                    break;
                default:
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = bed + new Vector3((i & 1) * 0.6f - 0.3f, 0f, (i >> 1) * 0.9f - 0.45f);
                        g.Frustum(p, p + Up * 0.3f, 0.16f, 0.16f, 0.26f, 0.26f, 8, 0f, C(120, 70, 46), C(140, 84, 54), true, false);
                        g.Frustum(p + Up * 0.3f, p + Up * 0.55f, 0.26f, 0.26f, 0.12f, 0.12f, 8, 0f, C(140, 84, 54), C(96, 60, 40), false, true);
                    }
                    break;
            }
            return m;
        }

        public static PropModel Truck(int variant, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 body = variant == 0 ? C(92, 116, 82) : C(108, 148, 178), bodyD = Sh(body, 0.78f);
            Color32 tyre = C(36, 36, 38), hub = C(150, 150, 150), glass = C(70, 92, 104), wood = C(156, 116, 74);
            float[] axz = { 2.45f, -1.75f };
            foreach (float z in axz)
                for (int s = -1; s <= 1; s += 2)
                {
                    float wx = z > 0f ? 0.95f : 1.0f, ww = z > 0f ? 0.14f : 0.22f;
                    Vector3 c = new Vector3(s * wx, 0.5f, z);
                    g.Frustum(c - Vector3.right * ww, c + Vector3.right * ww, 0.5f, 0.5f, 0.5f, 0.5f, 10, 0f, tyre, tyre, true, true);
                    g.Disc(c + Vector3.right * s * (ww + 0.005f), 0.24f, 8, 0f, Vector3.right * s, hub, Sh(hub, 0.8f));
                }
            for (int s = -1; s <= 1; s += 2) PG.Beam(g, new Vector3(s * 0.45f, 0.62f, -3.2f), new Vector3(s * 0.45f, 0.62f, 3.4f), 0.07f, 0.1f, C(40, 40, 42));
            PG.Box2(g, new Vector3(0f, 1.15f, 2.5f), new Vector3(0.62f, 0.4f, 0.75f), body, body);
            g.Frustum(new Vector3(0f, 1.55f, 1.75f), new Vector3(0f, 1.55f, 3.25f), 0.62f, 0.18f, 0.62f, 0.18f, 10, 0f, body, body, false, false);
            PG.BoxMM(g, new Vector3(-0.5f, 0.75f, 3.25f), new Vector3(0.5f, 1.55f, 3.31f), C(54, 56, 58));
            for (int i = 0; i < 6; i++) PG.BoxMM(g, new Vector3(-0.42f + i * 0.17f, 0.82f, 3.31f), new Vector3(-0.39f + i * 0.17f, 1.48f, 3.33f), C(150, 150, 152));
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 5; k++)
                {
                    float a0 = Mathf.Deg2Rad * (15f + k * 30f), a1 = Mathf.Deg2Rad * (15f + (k + 1) * 30f);
                    Vector3 c = new Vector3(s * 0.95f, 0.5f, 2.45f);
                    PG.Beam(g, c + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * 0.66f, c + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * 0.66f, 0.2f, 0.03f, bodyD);
                }
                g.Frustum(new Vector3(s * 0.78f, 1.32f, 2.95f), new Vector3(s * 0.78f, 1.32f, 3.12f), 0.13f, 0.13f, 0.13f, 0.13f, 8, 0f, C(60, 60, 60), C(246, 236, 196), false, true);
                PG.BoxMM(g, new Vector3(s * 0.85f - 0.15f, 0.5f, 0.6f), new Vector3(s * 0.85f + 0.15f, 0.56f, 1.95f), C(50, 50, 52));
                PG.BoxMM(g, new Vector3(s * 1.0f - 0.01f, 1.6f, 0.75f), new Vector3(s * 1.0f + 0.01f, 2.15f, 1.5f), glass);
                PG.BoxMM(g, new Vector3(s * 1.08f - 0.02f, 1.75f, 1.65f), new Vector3(s * 1.08f + 0.02f, 1.95f, 1.75f), C(50, 50, 52));
            }
            PG.BoxMM(g, new Vector3(-1.05f, 0.45f, 3.35f), new Vector3(1.05f, 0.6f, 3.5f), C(44, 44, 46));
            PG.Box2(g, new Vector3(0f, 1.55f, 1.1f), new Vector3(1.0f, 0.8f, 0.65f), body, Sh(body, 1.08f));
            PG.BoxMM(g, new Vector3(-0.85f, 1.6f, 1.74f), new Vector3(0.85f, 2.2f, 1.77f), glass);
            PG.Box2(g, new Vector3(0f, 0.92f, -1.48f), new Vector3(1.15f, 0.08f, 1.85f), C(80, 70, 60), wood);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.BoxMM(g, new Vector3(s * 1.15f - 0.04f, 1.0f, -3.33f), new Vector3(s * 1.15f + 0.04f, 1.6f, 0.37f), wood);
                for (int k = 0; k < 3; k++) PG.BoxMM(g, new Vector3(s * 1.19f - 0.01f, 1.17f + k * 0.18f, -3.33f), new Vector3(s * 1.19f + 0.01f, 1.19f + k * 0.18f, 0.37f), Sh(wood, 0.7f));
            }
            PG.BoxMM(g, new Vector3(-1.15f, 1.0f, -3.37f), new Vector3(1.15f, 1.6f, -3.29f), wood);
            PG.BoxMM(g, new Vector3(-1.15f, 1.0f, 0.29f), new Vector3(1.15f, 1.6f, 0.37f), wood);
            int load = rng.Next(3);
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3((i & 1) * 1.0f - 0.5f, 1.0f, -2.8f + (i >> 1) * 1.1f);
                if (load == 0)
                {
                    int s0 = g.v.Count;
                    g.Ellipsoid(p + Up * 0.25f, new Vector3(0.45f, 0.25f, 0.35f), 6, 3, C(226, 210, 170), C(196, 178, 136));
                    PG.Lumpy(g, s0, p + Up * 0.25f, 0.15f, i);
                }
                else if (load == 1)
                {
                    g.Frustum(p, p + Up * 0.6f, 0.26f, 0.26f, 0.34f, 0.34f, 8, 0f, C(176, 136, 80), C(206, 168, 104), true, false);
                    g.Ellipsoid(p + Up * 0.6f, new Vector3(0.32f, 0.14f, 0.32f), 6, 3, C(110, 170, 80), C(80, 140, 60));
                }
                else PG.Box2(g, p + Up * 0.3f, new Vector3(0.42f, 0.3f, 0.45f), C(176, 140, 90), C(196, 160, 110));
            }
            return m;
        }

        // ------------------------------------------------------------------ temple hall
        public static PropModel TempleHall(float bt, float bb)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 wall = C(178, 62, 46), gold = C(222, 168, 62), gold2 = C(238, 192, 84);
            PG.Box2(g, new Vector3(0f, (bt + bb) * 0.5f, 0f), new Vector3(6.2f, (bt - bb) * 0.5f, 4.7f), Pal.StoneDark, Pal.StoneLight);
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                float len = (k & 1) == 0 ? 6.2f : 4.7f, off = (k & 1) == 0 ? 4.7f : 6.2f;
                for (int p = 0; p <= 8; p++)
                {
                    float x = -len + p * len / 4f;
                    if (k == 0 && Mathf.Abs(x) < 1.7f) continue;
                    PG.Beam(g, q * new Vector3(x, bt, off), q * new Vector3(x, bt + 0.7f, off), 0.07f, 0.07f, Pal.StoneLight);
                }
            }
            if (bt - bb > 0.15f)
            {
                int steps = Mathf.Clamp(Mathf.CeilToInt((bt - bb) / 0.22f), 1, 14);
                for (int i = 0; i < steps; i++)
                {
                    float y = bb + (bt - bb) * (steps - i) / steps;
                    PG.Box2(g, new Vector3(0f, (y + bb) * 0.5f, 4.7f + (i + 0.5f) * 0.32f), new Vector3(1.6f, (y - bb) * 0.5f, 0.16f), Pal.StoneDark, Pal.Stone);
                }
            }
            float wt = bt + 3.8f;
            PG.BoxMM(g, new Vector3(-4.6f, bt, -3.2f), new Vector3(4.6f, wt, -2.95f), wall);
            PG.BoxMM(g, new Vector3(-4.6f, bt, -3.2f), new Vector3(-4.35f, wt, 1.8f), wall);
            PG.BoxMM(g, new Vector3(4.35f, bt, -3.2f), new Vector3(4.6f, wt, 1.8f), wall);
            PG.BoxMM(g, new Vector3(-4.6f, bt, 1.6f), new Vector3(-2.4f, wt, 1.8f), wall);
            PG.BoxMM(g, new Vector3(2.4f, bt, 1.6f), new Vector3(4.6f, wt, 1.8f), wall);
            PG.BoxMM(g, new Vector3(-2.4f, bt + 3.0f, 1.6f), new Vector3(2.4f, wt, 1.8f), Pal.WoodDark);
            FacadeWindow(g, gl, -2.35f, -0.05f, bt + 0.4f, bt + 2.95f, 1.7f, 1f);
            FacadeWindow(g, gl, 0.05f, 2.35f, bt + 0.4f, bt + 2.95f, 1.7f, 1f);
            for (int i = 0; i < 6; i++) PG.Column(g, new Vector3(-4.5f + i * 1.8f, bt, 2.7f), new Vector3(-4.5f + i * 1.8f, wt, 2.7f), 0.17f, Pal.Red);
            PG.Beam(g, new Vector3(-4.8f, wt - 0.18f, 2.7f), new Vector3(4.8f, wt - 0.18f, 2.7f), 0.15f, 0.18f, C(56, 118, 122));
            PG.BoxMM(g, new Vector3(-0.9f, wt - 0.95f, 2.86f), new Vector3(0.9f, wt - 0.38f, 2.92f), C(40, 60, 110));
            PG.BoxMM(g, new Vector3(-0.95f, wt - 1.0f, 2.84f), new Vector3(0.95f, wt - 0.33f, 2.86f), Pal.Gold);
            for (int i = 0; i < 3; i++) g.Box(new Vector3(-0.5f + i * 0.5f, wt - 0.66f, 2.93f), new Vector3(0.14f, 0.16f, 0.005f), Pal.Gold);
            PG.Roof(g, new Vector3(0f, wt, -0.25f), 4.9f, 3.0f, 2.1f, 1.3f, 0.6f, gold, gold2, Pal.TileUnder, C(62, 122, 98), false);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Rod(gl, new Vector3(s * 2.7f, wt - 0.3f, 3.2f), new Vector3(s * 2.7f, wt - 0.7f, 3.2f), 0.01f, A(Pal.Ink, 50), 3);
                PG.Lantern(gl, new Vector3(s * 2.7f, wt - 0.7f, 3.2f), 0.26f, 0.46f, Pal.RedBright, 110);
            }
            return m;
        }

        // ------------------------------------------------------------------ lantern / firecracker span over the street
        /// <summary>Two tall poles at a and b (ground points, world) with a sagging rope of lanterns and firecracker strings.</summary>
        public static PropModel LanternSpan(Vector3 a, Vector3 b, System.Random rng, float topY)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            var st = m[PK.Strong];
            Vector3 ta = new Vector3(a.x, topY, a.z), tb = new Vector3(b.x, topY, b.z);
            foreach (var p in new[] { a, b })
            {
                float poleH = topY - p.y;
                g.Frustum(p - Up * 0.3f, p + Up * (poleH + 0.2f), 0.12f, 0.12f, 0.09f, 0.09f, 6, 0f, Pal.RedDeep, Pal.Red, false, true);
                g.Ellipsoid(p + Up * (poleH + 0.32f), new Vector3(0.12f, 0.14f, 0.12f), 6, 3, Pal.Gold, Pal.GoldDark);
                g.Frustum(p - Up * 0.1f, p + Up * 0.35f, 0.25f, 0.25f, 0.2f, 0.2f, 6, 0f, Pal.Stone, Pal.Stone, false, true);
            }
            float sag = 0.75f;
            const int NS = 14;
            var pts = new Vector3[NS + 1];
            for (int i = 0; i <= NS; i++)
            {
                float t = i / (float)NS;
                pts[i] = Vector3.Lerp(ta, tb, t) - Up * (4f * sag * t * (1f - t));
            }
            for (int i = 0; i < NS; i++) PG.Beam(g, pts[i], pts[i + 1], 0.015f, 0.015f, C(60, 40, 36));
            float span = (tb - ta).magnitude;
            int n = Mathf.Max(3, Mathf.FloorToInt((span - 3.2f) / 1.1f));
            for (int k = 0; k < n; k++)
            {
                float t = Mathf.Lerp(0.2f, 0.8f, n == 1 ? 0.5f : k / (float)(n - 1));
                Vector3 rp = Vector3.Lerp(ta, tb, t) - Up * (4f * sag * t * (1f - t));
                int sway = 70 + (int)(110f * Mathf.Sin(t * Mathf.PI));
                Color32 lc = (k % 4 == 2) ? C(246, 190, 60) : Pal.RedBright;
                PG.Rod(gl, rp, rp - Up * 0.22f, 0.008f, A(Pal.Ink, sway / 2), 3);
                PG.Lantern(gl, rp - Up * 0.22f, 0.2f, 0.34f, lc, sway, (k & 1) == 0);
            }
            // firecracker strings near both poles
            foreach (float t in new[] { 0.1f, 0.9f })
            {
                Vector3 rp = Vector3.Lerp(ta, tb, t) - Up * (4f * sag * t * (1f - t));
                st.Ellipsoid(rp - Up * 0.06f, new Vector3(0.06f, 0.06f, 0.06f), 5, 3, A(Pal.Gold, 0), A(Pal.GoldDark, 0));
                for (int c = 0; c < 9; c++)
                {
                    float y = 0.14f + c * 0.1f;
                    int al = 30 + c * 22;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 p0 = rp - Up * y, p1 = p0 + new Vector3(s * 0.11f, -0.03f, 0f);
                        st.Frustum(p0, p1, 0.022f, 0.022f, 0.022f, 0.022f, 4, 0f, A(Pal.RedBright, al), A(c == 8 ? Pal.Gold : Pal.Red, al), false, true);
                    }
                }
                st.Frustum(rp - Up * 1.05f, rp - Up * 1.3f, 0.04f, 0.04f, 0.01f, 0.01f, 4, 0f, A(Pal.Red, 230), A(Pal.Gold, 240), false, false);
            }
            return m;
        }

        // ------------------------------------------------------------------ garden furniture
        public static PropModel ScholarRock(System.Random rng, float h)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Box2(g, new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.12f, 0.45f), Pal.StoneDark, Pal.Stone);
            float y = 0.24f;
            int parts = 3 + rng.Next(2);
            float seed = R(rng, 0f, 50f);
            Color32 lite = C(212, 210, 202), dark = C(96, 98, 100);
            g.Paint = p => (Mathf.Sin(p.x * 13f + seed) * Mathf.Sin(p.y * 11f - seed) * Mathf.Sin(p.z * 12f + seed * 0.3f)) > 0.45f ? dark : (p.y > h * 0.6f ? lite : C(186, 186, 180));
            for (int i = 0; i < parts; i++)
            {
                float ph = h / parts;
                Vector3 c = new Vector3(R(rng, -0.18f, 0.18f), y + ph * 0.5f, R(rng, -0.15f, 0.15f));
                Vector3 r = new Vector3(R(rng, 0.25f, 0.42f), ph * 0.65f, R(rng, 0.2f, 0.32f));
                int s = g.v.Count;
                g.Ellipsoid(c, r, 7, 4, lite, dark);
                PG.Lumpy(g, s, c, 0.35f, seed + i);
                y += ph * 0.85f;
            }
            g.Paint = null;
            return m;
        }

        public static PropModel StoneTableSet()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            g.Frustum(Vector3.zero, Up * 0.7f, 0.25f, 0.25f, 0.18f, 0.18f, 8, 0f, Pal.StoneDark, Pal.Stone, false, false);
            g.Frustum(Up * 0.7f, Up * 0.8f, 0.6f, 0.6f, 0.6f, 0.6f, 10, 0f, Pal.Stone, Pal.StoneLight, true, true);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 1.0f, 0f, Mathf.Sin(a) * 1.0f);
                g.Frustum(p, p + Up * 0.45f, 0.2f, 0.2f, 0.17f, 0.17f, 8, 0f, Pal.StoneDark, Pal.Stone, false, true);
            }
            g.Box(Up * 0.82f, new Vector3(0.24f, 0.015f, 0.24f), C(220, 200, 150));
            return m;
        }

        public static PropModel Scarecrow(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            PG.Rod(g, new Vector3(0f, -0.3f, 0f), new Vector3(0f, 1.9f, 0f), 0.05f, Pal.WoodDark, 5);
            PG.Beam(g, new Vector3(-0.75f, 1.45f, 0f), new Vector3(0.75f, 1.45f, 0f), 0.03f, 0.03f, Pal.WoodDark);
            g.Ellipsoid(new Vector3(0f, 1.82f, 0f), new Vector3(0.16f, 0.18f, 0.16f), 6, 3, C(222, 196, 130), C(196, 170, 104));
            g.Frustum(new Vector3(0f, 1.9f, 0f), new Vector3(0f, 2.18f, 0f), 0.45f, 0.45f, 0.02f, 0.02f, 8, 0f, C(214, 184, 110), C(232, 204, 130), true, false);
            Color32 shirt = rng.NextDouble() < 0.5 ? C(60, 84, 150) : C(190, 60, 50);
            for (int k = 0; k < 3; k++)
            {
                float y0 = 1.55f - k * 0.25f, y1 = y0 - 0.25f;
                float w0 = k == 0 ? 0.75f : 0.28f, w1 = k == 0 ? 0.3f : 0.28f;
                PG.Quad2(st, new Vector3(-w0, y0, 0.02f), new Vector3(w0, y0, 0.02f), new Vector3(w1, y1, 0.02f), new Vector3(-w1, y1, 0.02f),
                         A(shirt, k * 60), A(shirt, k * 60), A(shirt, k * 60 + 60), A(shirt, k * 60 + 60));
            }
            return m;
        }
    }
}
