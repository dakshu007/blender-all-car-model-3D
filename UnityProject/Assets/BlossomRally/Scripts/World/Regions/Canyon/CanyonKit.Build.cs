// CanyonKit.Build.cs - low-poly geometry for the Red Canyon: banded Danxia rock (hoodoos, spires, cliff slabs,
// wind-carved arches, mesa cap-rock rings), boulders, desert plants (saxaul, tamarisk, Euphrates poplar, camelthorn,
// reeds), cairns, bleached bones and tumbleweeds. Banded rock is built with grey placeholder shades and then
// recoloured per face from CanyonRegion.StrataColor at its world position, so the stripes line up with the terrain.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class CanyonKit
    {
        public static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        public static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        /// <summary>Grey placeholder (shade 0..1 in every channel) for banded rock.</summary>
        public static Color32 Shade(float k) { byte b = (byte)Mathf.Clamp(k * 255f, 0f, 255f); return new Color32(b, b, b, 0); }

        /// <summary>Recolour vertices [start, end) per triangle from the strata at their world position (m = local to world).</summary>
        public static void Band(LifeGeo g, int start, int end, Matrix4x4 m, float sat = 1f)
        {
            for (int i = start; i + 2 < end; i += 3)
            {
                Vector3 w = m.MultiplyPoint3x4((g.v[i] + g.v[i + 1] + g.v[i + 2]) * (1f / 3f));
                float shade = g.c[i].r / 255f;
                Color c = CanyonRegion.StrataColor(w.x, w.z, w.y);
                if (sat != 1f) { float l = (c.r + c.g + c.b) / 3f; c = Color.Lerp(new Color(l, l, l), c, sat); }
                c *= shade;
                var c32 = new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), g.c[i].a);
                g.c[i] = c32; g.c[i + 1] = c32; g.c[i + 2] = c32;
            }
        }

        // ------------------------------------------------------------------ hoodoo / spire / rock tower
        /// <summary>
        /// Banded rock column, base at the origin (extends 2 m into the ground). kind 0 = hoodoo (bulging bands,
        /// dark cap stone), 1 = tapering spire, 2 = squat butte tower with a flat top. Grey placeholders: call Band.
        /// </summary>
        public static void Column(LifeGeo g, System.Random rng, float H, float Rb, int kind)
        {
            int NR = kind == 1 ? 10 : 8, NS = kind == 2 ? 11 : 9;
            var ring = new Vector3[NR, NS];
            Vector3 lean = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * R(rng, 0.02f, kind == 1 ? 0.1f : 0.05f) * H;
            float ph = R(rng, 0f, 6.28f), bulges = R(rng, 2.5f, 4.5f);
            for (int i = 0; i < NR; i++)
            {
                float t = i / (float)(NR - 1);
                float y = -2f + (H + 2f) * t;
                float r;
                if (kind == 0) r = Rb * (1f - 0.42f * t) * (1f + 0.16f * Mathf.Sin(t * Mathf.PI * bulges + ph));
                else if (kind == 1) r = Rb * Mathf.Lerp(1f, 0.16f, Mathf.Pow(t, 0.85f)) * (1f + 0.1f * Mathf.Sin(t * 9f + ph));
                else r = Rb * (1f - 0.18f * t) * (1f + 0.07f * Mathf.Sin(t * 7f + ph));
                Vector3 c = Up * y + lean * (t * t);
                float rot = R(rng, 0f, 0.5f);
                for (int k = 0; k < NS; k++)
                {
                    float a = rot + k * Mathf.PI * 2f / NS;
                    ring[i, k] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * R(rng, 0.82f, 1.14f) + Up * R(rng, -0.12f, 0.12f) * (H / NR);
                }
            }
            for (int i = 0; i < NR - 1; i++)
                for (int k = 0; k < NS; k++)
                {
                    int k1 = (k + 1) % NS;
                    Vector3 a = ring[i, k], b = ring[i, k1], d = ring[i + 1, k1], e = ring[i + 1, k];
                    Vector3 mid = (a + b + d + e) * 0.25f;
                    Vector3 axis = Up * mid.y + lean * Mathf.Pow(Mathf.Clamp01((mid.y + 2f) / (H + 2f)), 2f);
                    g.QuadOut(a, b, d, e, mid - axis, Shade(R(rng, 0.86f, 1f)), Shade(R(rng, 0.86f, 1f)), Shade(R(rng, 0.86f, 1f)), Shade(R(rng, 0.86f, 1f)));
                }
            Vector3 topC = Vector3.zero;
            for (int k = 0; k < NS; k++) topC += ring[NR - 1, k];
            topC /= NS;
            if (kind == 1)
            {
                Vector3 apex = topC + Up * Rb * 0.5f;
                for (int k = 0; k < NS; k++) g.TriOut(ring[NR - 1, k], ring[NR - 1, (k + 1) % NS], apex, Up + (ring[NR - 1, k] - topC).normalized, Shade(0.95f), Shade(0.95f), Shade(0.95f));
            }
            else
            {
                Vector3 cap = topC + Up * 0.15f;
                for (int k = 0; k < NS; k++) g.TriOut(ring[NR - 1, k], ring[NR - 1, (k + 1) % NS], cap, Up, Shade(0.98f), Shade(0.98f), Shade(0.98f));
            }
        }

        /// <summary>Dark varnished cap stone on top of a hoodoo (not banded: call after Band).</summary>
        public static void CapStone(LifeGeo g, System.Random rng, Vector3 top, float r)
        {
            int s = g.v.Count;
            g.Ellipsoid(top + Up * r * 0.25f, new Vector3(r * R(rng, 1.15f, 1.45f), r * 0.42f, r * R(rng, 1.1f, 1.4f)), 7, 3, C(150, 92, 70), C(96, 58, 48));
            PG.Lumpy(g, s, top + Up * r * 0.25f, 0.14f, R(rng, 0f, 9f));
        }

        // ------------------------------------------------------------------ cliff slab (canyon walls)
        /// <summary>
        /// A section of vertical canyon wall. Front face at z = 0 faces -z (towards the road), width w (x), height h,
        /// with horizontal strata ledges and vertical fluting; the top runs back to depth 'back' and the base sinks 2.5 m.
        /// </summary>
        public static void Slab(LifeGeo g, System.Random rng, float w, float h, float back)
        {
            const int NX = 6;
            int NY = Mathf.Clamp(Mathf.RoundToInt(h / 2.4f), 3, 9);
            var P = new Vector3[NX + 1, NY + 1];
            float[] rowIn = new float[NY + 1];
            for (int j = 0; j <= NY; j++) rowIn[j] = (j % 2 == 0 ? 0f : 0.55f) + R(rng, -0.25f, 0.35f);
            for (int i = 0; i <= NX; i++)
            {
                float u = i / (float)NX;
                float edge = Mathf.Sin(u * Mathf.PI);                 // the ends curl back into the slope
                float flute = 0.5f * Mathf.Sin(u * 13f + R(rng, 0f, 0.6f));
                for (int j = 0; j <= NY; j++)
                {
                    float v = j / (float)NY;
                    float y = -2.5f + (h + 2.5f) * v;
                    float z = rowIn[j] + flute * (0.4f + 0.6f * v) + (1f - edge) * (2.2f + 1.5f * v) + v * v * 0.8f;
                    if (j == NY) z += R(rng, 0f, 0.6f);
                    float x = (u - 0.5f) * w * (1f - 0.08f * v) + R(rng, -0.25f, 0.25f);
                    P[i, j] = new Vector3(x, y + (j > 0 && j < NY ? R(rng, -0.3f, 0.3f) : 0f), z);
                }
            }
            for (int i = 0; i < NX; i++)
                for (int j = 0; j < NY; j++)
                {
                    float sh = R(rng, 0.84f, 1f) * (rowIn[j] > 0.4f ? 0.9f : 1f);
                    Color32 c = Shade(sh);
                    g.QuadOut(P[i, j], P[i + 1, j], P[i + 1, j + 1], P[i, j + 1], new Vector3(0f, 0f, -1f), c, c, c, c);
                }
            // top ledge back into the slope
            for (int i = 0; i < NX; i++)
            {
                Vector3 a = P[i, NY], b = P[i + 1, NY];
                Vector3 a2 = new Vector3(a.x * 0.9f, h - 0.6f, back), b2 = new Vector3(b.x * 0.9f, h - 0.6f, back);
                Color32 c = Shade(R(rng, 0.92f, 1f));
                g.QuadOut(a, b, b2, a2, Up, c, c, c, c);
            }
            // side returns
            for (int s = 0; s <= 1; s++)
            {
                int i = s == 0 ? 0 : NX;
                Vector3 outward = new Vector3(s == 0 ? -1f : 1f, 0f, 0f);
                for (int j = 0; j < NY; j++)
                {
                    Vector3 a = P[i, j], b = P[i, j + 1];
                    Vector3 a2 = new Vector3(a.x * 0.85f, a.y, back), b2 = new Vector3(b.x * 0.85f, j + 1 == NY ? h - 0.6f : b.y, back);
                    Color32 c = Shade(R(rng, 0.8f, 0.92f));
                    g.QuadOut(a, b, b2, a2, outward, c, c, c, c);
                }
            }
        }

        // ------------------------------------------------------------------ wind-carved arch
        /// <summary>
        /// Natural arch in the local x-y plane: legs at x = +-S, crown centre height Hc, thickness tl (legs) / tc (crown)
        /// and depth d (z). Legs sink 2 m. Grey placeholders: call Band.
        /// </summary>
        public static void Arch(LifeGeo g, System.Random rng, float S, float Hc, float tl, float tc, float d)
        {
            const int N = 22, SIDES = 8;
            var ring = new Vector3[N + 1, SIDES];
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float phi = Mathf.PI * (1f - t);
                float cx = S * Mathf.Cos(phi);
                float cy = Hc * Mathf.Pow(Mathf.Sin(phi), 0.75f);
                // tangent direction
                float phi2 = Mathf.PI * (1f - Mathf.Min(1f, t + 0.01f)), phi1 = Mathf.PI * (1f - Mathf.Max(0f, t - 0.01f));
                Vector3 p1 = new Vector3(S * Mathf.Cos(phi1), Hc * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phi1)), 0.75f), 0f);
                Vector3 p2 = new Vector3(S * Mathf.Cos(phi2), Hc * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phi2)), 0.75f), 0f);
                Vector3 tan = (p2 - p1).normalized;
                if (i == 0 || i == N) tan = i == 0 ? Up : -Up;
                Vector3 nrm = Vector3.Cross(tan, Vector3.forward).normalized;   // in-plane normal
                float legness = Mathf.Abs(t - 0.5f) * 2f;
                float th = Mathf.Lerp(tc, tl, legness * legness) * R(rng, 0.92f, 1.08f);
                float dd = Mathf.Lerp(d * 0.75f, d, legness) * R(rng, 0.92f, 1.08f);
                Vector3 c = new Vector3(cx, cy, 0f);
                if (i == 0 || i == N) c.y = -2f;
                for (int k = 0; k < SIDES; k++)
                {
                    float a = k * Mathf.PI * 2f / SIDES + 0.3f;
                    ring[i, k] = c + nrm * (Mathf.Cos(a) * th * 0.5f) + Vector3.forward * (Mathf.Sin(a) * dd * 0.5f)
                                 + new Vector3(R(rng, -0.2f, 0.2f), R(rng, -0.2f, 0.2f), R(rng, -0.2f, 0.2f));
                }
            }
            for (int i = 0; i < N; i++)
                for (int k = 0; k < SIDES; k++)
                {
                    int k1 = (k + 1) % SIDES;
                    Vector3 a = ring[i, k], b = ring[i, k1], e = ring[i + 1, k1], f = ring[i + 1, k];
                    Vector3 mid = (a + b + e + f) * 0.25f;
                    Vector3 ctr = Vector3.zero;
                    for (int q = 0; q < SIDES; q++) ctr += ring[i, q] + ring[i + 1, q];
                    ctr /= SIDES * 2;
                    Color32 c = Shade(R(rng, 0.85f, 1f));
                    g.QuadOut(a, b, e, f, mid - ctr, c, c, c, c);
                }
        }

        // ------------------------------------------------------------------ boulders & pebbles
        public static readonly Color32[] RockTops = { C(178, 98, 72), C(196, 128, 92), C(150, 84, 66), C(206, 160, 118), C(122, 70, 58) };

        public static PropModel Boulder(System.Random rng, int variant, PK kind)
        {
            var m = new PropModel();
            var g = m[kind];
            Color32 top = RockTops[variant % RockTops.Length], bot = PG.Sh(top, 0.66f);
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float r = i == 0 ? 1f : R(rng, 0.35f, 0.6f);
                Vector3 c = i == 0 ? new Vector3(0f, 0.25f, 0f) : new Vector3(R(rng, -1.1f, 1.1f), 0.05f, R(rng, -1.1f, 1.1f));
                PG.Boulder(g, c, new Vector3(r * R(rng, 0.9f, 1.3f), r * R(rng, 0.55f, 0.85f), r * R(rng, 0.8f, 1.2f)), top, bot, R(rng, 0f, 9f));
            }
            return m;
        }

        public static PropModel Pebbles(System.Random rng, bool pale)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            int n = 5 + rng.Next(5);
            for (int i = 0; i < n; i++)
            {
                float r = R(rng, 0.12f, 0.32f);
                Color32 c = pale ? (rng.NextDouble() < 0.5 ? C(214, 204, 186) : C(184, 170, 150)) : RockTops[rng.Next(RockTops.Length)];
                Vector3 p = new Vector3(R(rng, -1.6f, 1.6f), 0f, R(rng, -1.6f, 1.6f));
                g.Ellipsoid(p, new Vector3(r, r * 0.55f, r * R(rng, 0.7f, 1.2f)), 4, 2, c, PG.Sh(c, 0.75f));
            }
            return m;
        }

        // ------------------------------------------------------------------ desert plants
        /// <summary>Saxaul (梭梭): crooked grey trunks and dark olive tufts.</summary>
        public static PropModel Saxaul(System.Random rng, float s)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 bark = C(132, 116, 100), barkD = C(92, 80, 70);
            int limbs = 3 + rng.Next(3);
            for (int i = 0; i < limbs; i++)
            {
                float a = R(rng, 0f, 6.28f);
                Vector3 mid = new Vector3(Mathf.Cos(a) * 0.5f, R(rng, 0.7f, 1.1f), Mathf.Sin(a) * 0.5f) * s;
                Vector3 tip = mid + new Vector3(Mathf.Cos(a + R(rng, -0.6f, 0.6f)) * 0.8f, R(rng, 0.6f, 1.2f), Mathf.Sin(a + R(rng, -0.6f, 0.6f)) * 0.8f) * s;
                g.Frustum(Vector3.zero, mid, 0.09f * s, 0.09f * s, 0.06f * s, 0.06f * s, 4, a, barkD, bark, false, false);
                g.Frustum(mid, tip, 0.06f * s, 0.06f * s, 0.025f * s, 0.025f * s, 4, a, bark, bark, false, true);
                for (int k = 0; k < 1; k++)
                {
                    Vector3 p = Vector3.Lerp(mid, tip, 0.85f) + new Vector3(R(rng, -0.2f, 0.2f), 0.1f, R(rng, -0.2f, 0.2f)) * s;
                    int s0 = g.v.Count;
                    Color32 leaf = rng.NextDouble() < 0.5 ? C(112, 124, 76, 90) : C(138, 142, 88, 90);
                    g.Ellipsoid(p, new Vector3(0.7f, 0.38f, 0.7f) * s * R(rng, 0.8f, 1.2f), 6, 3, leaf, PG.Sh(leaf, 0.7f));
                    PG.Lumpy(g, s0, p, 0.22f, R(rng, 0f, 9f));
                }
            }
            return m;
        }

        /// <summary>Tamarisk (红柳): a fountain of feathery plumes, grey-green below and pink at the tips.</summary>
        public static PropModel Tamarisk(System.Random rng, float s)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            int plumes = 8 + rng.Next(5);
            Color32 pink = rng.NextDouble() < 0.5 ? C(236, 132, 168, 160) : C(222, 108, 150, 160);
            for (int i = 0; i < plumes; i++)
            {
                float a = R(rng, 0f, 6.28f), out1 = R(rng, 0.25f, 0.8f);
                Vector3 b = new Vector3(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f) * s;
                Vector3 mid = new Vector3(Mathf.Cos(a) * out1, R(rng, 0.9f, 1.4f), Mathf.Sin(a) * out1) * s;
                Vector3 tip = mid + new Vector3(Mathf.Cos(a) * 0.5f, R(rng, 0.7f, 1.2f), Mathf.Sin(a) * 0.5f) * s;
                g.Frustum(b, mid, 0.18f * s, 0.18f * s, 0.22f * s, 0.22f * s, 4, a, C(112, 124, 96, 20), C(132, 142, 104, 90), false, false);
                g.Frustum(mid, tip, 0.22f * s, 0.22f * s, 0.02f * s, 0.02f * s, 4, a, C(160, 140, 130, 120), pink, false, false);
            }
            return m;
        }

        /// <summary>Camelthorn / desert tuft: a low grey-green cushion.</summary>
        public static PropModel Tuft(System.Random rng, float s, bool dry)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            int n = 2 + rng.Next(2);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = new Vector3(R(rng, -0.4f, 0.4f), 0.15f, R(rng, -0.4f, 0.4f)) * s;
                Color32 c = dry ? (rng.NextDouble() < 0.5 ? C(196, 170, 112, 70) : C(172, 150, 96, 70)) : (rng.NextDouble() < 0.5 ? C(128, 142, 90, 70) : C(150, 156, 98, 70));
                int s0 = g.v.Count;
                g.Ellipsoid(p, new Vector3(0.6f, 0.38f, 0.6f) * s * R(rng, 0.7f, 1.2f), 5, 3, c, PG.Sh(c, 0.62f));
                PG.Lumpy(g, s0, p, 0.25f, R(rng, 0f, 9f));
            }
            return m;
        }

        /// <summary>Euphrates poplar (胡杨): gnarled split trunk with golden or green crown clumps.</summary>
        public static PropModel Poplar(System.Random rng, float s, bool golden)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var f = m[PK.Props];
            Color32 bark = C(126, 102, 82), barkD = C(86, 68, 56);
            float h0 = R(rng, 2.2f, 3.2f) * s;
            Vector3 lean = new Vector3(R(rng, -0.6f, 0.6f), 0f, R(rng, -0.6f, 0.6f)) * s;
            Vector3 fork = Up * h0 + lean;
            g.Frustum(Vector3.down * 0.3f, fork, 0.42f * s, 0.42f * s, 0.3f * s, 0.3f * s, 7, 0f, barkD, bark, false, false);
            int limbs = 3 + rng.Next(2);
            Color32[] gold = { C(240, 186, 64, 120), C(250, 206, 90, 120), C(226, 160, 52, 120) };
            Color32[] green = { C(132, 158, 78, 120), C(150, 172, 90, 120), C(112, 140, 70, 120) };
            for (int i = 0; i < limbs; i++)
            {
                float a = i * 6.28f / limbs + R(rng, -0.4f, 0.4f);
                Vector3 d = new Vector3(Mathf.Cos(a), R(rng, 0.9f, 1.5f), Mathf.Sin(a)).normalized;
                Vector3 tip = fork + d * R(rng, 2.2f, 3.4f) * s;
                g.Frustum(fork, tip, 0.24f * s, 0.24f * s, 0.1f * s, 0.1f * s, 5, a, bark, PG.Sh(bark, 1.1f), false, true);
                int clumps = 2 + rng.Next(2);
                for (int k = 0; k < clumps; k++)
                {
                    Vector3 p = tip + new Vector3(R(rng, -1f, 1f), R(rng, -0.2f, 0.8f), R(rng, -1f, 1f)) * s;
                    Color32 c = golden ? gold[rng.Next(3)] : green[rng.Next(3)];
                    int s0 = f.v.Count;
                    f.Ellipsoid(p, new Vector3(1.5f, 1.0f, 1.5f) * s * R(rng, 0.8f, 1.2f), 7, 4, c, PG.Sh(c, 0.68f));
                    PG.Lumpy(f, s0, p, 0.2f, R(rng, 0f, 9f));
                }
            }
            return m;
        }

        /// <summary>Clump of reeds for the spring's edge (strong sway).</summary>
        public static PropModel Reeds(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Strong];
            int n = 12 + rng.Next(10);
            for (int i = 0; i < n; i++)
            {
                Vector3 b = new Vector3(R(rng, -0.8f, 0.8f), -0.1f, R(rng, -0.8f, 0.8f));
                float h = R(rng, 1.2f, 2.3f);
                Vector3 t = b + new Vector3(R(rng, -0.3f, 0.3f), h, R(rng, -0.3f, 0.3f));
                Vector3 side = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized;
                PG.Petal(g, b, t, side, 0.05f, C(96, 128, 70, 0), C(178, 176, 110, 255));
                if (rng.NextDouble() < 0.35)
                    g.Ellipsoid(t + Up * 0.12f, new Vector3(0.06f, 0.2f, 0.06f), 4, 3, C(170, 130, 90, 255), C(140, 100, 70, 255));
            }
            return m;
        }

        // ------------------------------------------------------------------ cairns, bones, tumbleweed
        /// <summary>Stone cairn (敖包): stacked flat stones, optionally with cloth strips on a stick.</summary>
        public static PropModel Cairn(System.Random rng, bool flags)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            int n = 4 + rng.Next(4);
            float y = 0f, r = R(rng, 0.55f, 0.75f);
            for (int i = 0; i < n; i++)
            {
                float hh = r * R(rng, 0.35f, 0.5f);
                Color32 c = RockTops[rng.Next(RockTops.Length)];
                int s0 = g.v.Count;
                Vector3 p = new Vector3(R(rng, -0.08f, 0.08f), y + hh * 0.5f, R(rng, -0.08f, 0.08f));
                g.Ellipsoid(p, new Vector3(r, hh * 0.6f, r * R(rng, 0.8f, 1f)), 6, 3, c, PG.Sh(c, 0.7f));
                PG.Lumpy(g, s0, p, 0.12f, i * 3f);
                y += hh * 0.95f;
                r *= R(rng, 0.7f, 0.85f);
            }
            if (flags)
            {
                Vector3 top = new Vector3(0f, y + 1.6f, 0f);
                PG.Rod(g, new Vector3(0f, y - 0.3f, 0f), top, 0.035f, C(110, 82, 60));
                var st = m[PK.Strong];
                Color32[] cl = { C(70, 130, 210), C(240, 236, 220), C(214, 60, 52), C(250, 200, 70), C(70, 160, 110) };
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.26f;
                    Vector3 p = top - Up * (0.15f + i * 0.12f);
                    Vector3 d = new Vector3(Mathf.Cos(a), -0.35f, Mathf.Sin(a)).normalized * R(rng, 0.7f, 1.1f);
                    Vector3 side = Vector3.Cross(d, Up).normalized * 0.08f;
                    Color32 c0 = PG.A(cl[i], 30), c1 = PG.A(cl[i], 255);
                    PG.Quad2(st, p - side, p + side, p + d + side, p + d - side, c0, c0, c1, c1);
                }
            }
            return m;
        }

        /// <summary>Bleached camel skeleton half sunk in the sand (along +z).</summary>
        public static PropModel Bones(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 bone = C(238, 230, 210), boneD = C(206, 196, 176);
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(Mathf.Sin(i * 0.5f) * 0.12f, 0.12f + Mathf.Sin(i * 0.35f) * 0.1f, -1.2f + i * 0.3f);
                g.Ellipsoid(p, new Vector3(0.1f, 0.08f, 0.12f), 4, 2, bone, boneD);
            }
            for (int i = 0; i < 7; i++)
            {
                float z = -0.8f + i * 0.24f, len = 0.75f + 0.2f * Mathf.Sin(i * 0.6f);
                for (int s = -1; s <= 1; s += 2)
                {
                    if (rng.NextDouble() < 0.2) continue;
                    Vector3 a = new Vector3(0f, 0.2f, z), b = new Vector3(s * len * 0.6f, 0.35f, z + 0.05f), c = new Vector3(s * len * 0.85f, -0.05f, z + 0.12f);
                    PG.Beam(g, a, b, 0.03f, 0.035f, bone);
                    PG.Beam(g, b, c, 0.03f, 0.035f, boneD);
                }
            }
            Vector3 sk = new Vector3(0.5f, 0.1f, 1.6f);
            g.Ellipsoid(sk, new Vector3(0.16f, 0.14f, 0.22f), 6, 4, bone, boneD);
            g.Frustum(sk + new Vector3(0f, 0f, 0.15f), sk + new Vector3(0.05f, -0.05f, 0.62f), 0.12f, 0.09f, 0.06f, 0.05f, 6, 0f, bone, boneD, false, true);
            PG.Beam(g, new Vector3(-0.6f, 0.04f, 0.4f), new Vector3(-1.4f, 0.04f, 0.9f), 0.05f, 0.05f, boneD);
            return m;
        }

        /// <summary>Tumbleweed: a ball of thin twig loops (radius 1, centre at the origin).</summary>
        public static LifeGeo Tumbleweed(System.Random rng)
        {
            var g = new LifeGeo();
            Color32 a = C(176, 148, 102), b = C(140, 112, 76), d = C(206, 180, 128);
            for (int l = 0; l < 5; l++)
            {
                Vector3 n = new Vector3(R(rng, -1f, 1f), R(rng, -1f, 1f), R(rng, -1f, 1f)).normalized;
                Vector3 u = Vector3.Cross(n, Up).sqrMagnitude > 0.01f ? Vector3.Cross(n, Up).normalized : Vector3.right;
                Vector3 w = Vector3.Cross(n, u);
                const int segs = 8;
                Vector3 prev = Vector3.zero;
                for (int k = 0; k <= segs; k++)
                {
                    float t = k * Mathf.PI * 2f / segs;
                    float rr = R(rng, 0.8f, 1.05f);
                    Vector3 p = (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * rr;
                    if (k > 0) PG.Rod(g, prev, p, 0.035f, (k & 1) == 0 ? a : (l % 2 == 0 ? b : d), 3);
                    prev = p;
                }
            }
            return g;
        }
    }
}
