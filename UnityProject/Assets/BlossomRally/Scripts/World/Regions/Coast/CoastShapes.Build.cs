// CoastShapes.Build.cs - Lotus Coast models, part 1: nature and beach. Coconut palms with swaying fronds,
// umbrella-crowned coastal pines, mangrove clumps on arching stilt roots, granite boulders and sea stacks,
// beach umbrellas and loungers, a thatched snack hut, the lifeguard tower, the volleyball net, kites,
// tide-pool rocks, starfish and shells. Local space: y up, origin on the ground, front = +Z.
// Vertex colour sRGB; alpha = wind weight (VertexColorWind / PropGlow).
using UnityEngine;

namespace BlossomRally
{
    internal static partial class CoastShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 A(Color32 c, int a) { return PG.A(c, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }
        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        public static readonly Color32 GraniteA = C(196, 178, 168), GraniteB = C(164, 150, 144), GraniteC = C(128, 120, 118);
        public static readonly Color32 Bamboo = C(198, 176, 112), BambooDark = C(150, 128, 76), Thatch = C(206, 176, 112), ThatchDark = C(160, 128, 76);
        public static readonly Color32 Sail = C(186, 92, 56), SailDark = C(146, 66, 44);

        // ------------------------------------------------------------------ coconut palm
        public static PropModel Palm(System.Random rng, float H)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            float a0 = R(rng, 0f, Mathf.PI * 2f);
            Vector3 lean = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * R(rng, 0.6f, 2.6f);
            Color32 barkA = C(150, 128, 104), barkB = C(118, 100, 84), barkC = C(170, 150, 122);
            const int NS = 8;
            Vector3 prev = Vector3.zero - Up * 0.3f;
            for (int i = 1; i <= NS; i++)
            {
                float t = i / (float)NS;
                Vector3 p = Up * (H * t) + lean * (2f * t - t * t);
                float r0 = Mathf.Lerp(0.27f, 0.15f, (i - 1) / (float)NS), r1 = Mathf.Lerp(0.27f, 0.15f, t);
                Color32 c0 = A((i & 1) == 0 ? barkA : barkB, (int)(40f * t * t)), c1 = A(i % 3 == 0 ? barkC : barkA, (int)(40f * t * t));
                g.Frustum(prev, p, r0, r0, r1, r1, 6, i * 0.4f, c0, c1, false, i == NS);
                // ring scars
                if (i < NS && (i & 1) == 0) g.Frustum(p - Up * 0.05f, p + Up * 0.05f, r1 + 0.03f, r1 + 0.03f, r1 + 0.03f, r1 + 0.03f, 6, i * 0.4f, Sh(c0, 0.8f), Sh(c0, 0.8f), false, false);
                prev = p;
            }
            Vector3 top = prev;
            Color32 shaft = C(112, 140, 66, 50);
            g.Frustum(top - Up * 0.1f, top + Up * 0.55f, 0.2f, 0.2f, 0.12f, 0.12f, 6, 0f, shaft, Sh(shaft, 1.15f), false, true);
            Color32 nutA = C(120, 132, 52, 50), nutB = C(150, 112, 56, 50);
            int nuts = 3 + rng.Next(4);
            for (int i = 0; i < nuts; i++)
            {
                float a = i * 2.4f + R(rng, 0f, 0.5f);
                Vector3 c = top + new Vector3(Mathf.Cos(a) * 0.24f, R(rng, -0.25f, 0f), Mathf.Sin(a) * 0.24f);
                g.Ellipsoid(c, new Vector3(0.14f, 0.16f, 0.14f), 4, 2, i % 2 == 0 ? nutA : nutB, Sh(nutA, 0.8f));
            }
            // fronds: rising then drooping arcs of leaflets
            int fronds = 9 + rng.Next(3);
            Color32 fBase = C(70, 116, 52), fMid = C(98, 150, 62), fTip = C(150, 176, 84), fUnder = C(84, 112, 56);
            for (int f = 0; f < fronds; f++)
            {
                float az = f * Mathf.PI * 2f / fronds + R(rng, -0.2f, 0.2f);
                Vector3 d = new Vector3(Mathf.Cos(az), 0f, Mathf.Sin(az));
                Vector3 side = new Vector3(-d.z, 0f, d.x);
                float L = R(rng, 3.4f, 4.6f) * Mathf.Clamp(H / 9f, 0.75f, 1.15f);
                float rise = R(rng, 0.25f, 0.55f), droop = R(rng, 0.85f, 1.25f);
                if (f % 4 == 0) { rise = R(rng, 0.7f, 0.95f); droop *= 0.7f; }      // a few young upright fronds
                const int NF = 4;
                Vector3 pp = top + Up * 0.35f;
                for (int j = 1; j <= NF; j++)
                {
                    float u0 = (j - 1) / (float)NF, u1 = j / (float)NF;
                    Vector3 q0 = Frond(top, d, L, rise, droop, u0), q1 = Frond(top, d, L, rise, droop, u1);
                    float w0 = 0.95f * Mathf.Sin(Mathf.PI * (0.12f + 0.84f * u0)) + 0.08f, w1 = 0.95f * Mathf.Sin(Mathf.PI * (0.12f + 0.84f * u1)) + 0.08f;
                    Color32 c0 = A(Color32.Lerp(fBase, j < 3 ? fMid : fTip, u0), 40 + (int)(200f * u0)), c1 = A(Color32.Lerp(fBase, fTip, u1), 40 + (int)(200f * u1));
                    Vector3 dn0 = Up * (w0 * 0.42f), dn1 = Up * (w1 * 0.42f);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 e0 = q0 + side * (s * w0) - dn0, e1 = q1 + side * (s * w1) - dn1;
                        PG.Quad2(st, q0, q1, e1, e0, c0, c1, Sh(c1, 0.9f), Sh(c0, 0.9f));
                    }
                    PG.Rod(st, q0, q1, 0.03f, A(fUnder, 40 + (int)(200f * u0)), 3);
                }
            }
            return m;
        }

        static Vector3 Frond(Vector3 top, Vector3 d, float L, float rise, float droop, float u)
        {
            return top + Up * 0.35f + d * (L * u) + Up * (L * (rise * u - droop * u * u));
        }

        // ------------------------------------------------------------------ coastal (Masson) pine
        public static PropModel CoastPine(System.Random rng, float H)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 bark = C(124, 84, 62), barkD = C(92, 62, 48), barkL = C(150, 104, 74);
            Vector3 lean = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * R(rng, 0.3f, 1.2f);
            var pts = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                pts[i] = Up * (H * t - 0.3f) + lean * Mathf.Sin(t * 1.9f) + (i > 0 ? new Vector3(R(rng, -0.2f, 0.2f), 0f, R(rng, -0.2f, 0.2f)) : Vector3.zero);
            }
            for (int i = 0; i < 4; i++)
                g.Frustum(pts[i], pts[i + 1], 0.34f - i * 0.06f, 0.34f - i * 0.06f, 0.28f - i * 0.06f, 0.28f - i * 0.06f, 5, i * 0.5f, i % 2 == 0 ? barkD : bark, barkL, false, i == 3);
            // a full, irregular crown of needle clumps over the top 45 %, flat-topped like a coastal pine
            Color32 top = C(62, 110, 74, 30), bottom = C(34, 64, 48, 20), rim = C(90, 134, 84, 35);
            float s = H / 11f;
            int clumps = 6 + rng.Next(3);
            for (int i = 0; i < clumps; i++)
            {
                float t = i == 0 ? 1f : R(rng, 0.58f, 0.96f);
                Vector3 axis = Vector3.Lerp(pts[2], pts[4], (t - 0.5f) / 0.5f);
                float a = i * 2.39f + R(rng, -0.4f, 0.4f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = i == 0 ? 0f : R(rng, 0.9f, 2.1f) * s * (1.25f - t * 0.5f);
                Vector3 c = axis + d * reach + Up * (i == 0 ? 0.4f * s : R(rng, 0f, 0.4f));
                if (i > 0 && reach > 1.2f) PG.Rod(g, axis, c, 0.09f * s, barkD, 4);
                Vector3 pr = (i == 0 ? new Vector3(2.4f, 0.95f, 2.1f) : new Vector3(R(rng, 1.3f, 1.9f), R(rng, 0.65f, 0.95f), R(rng, 1.2f, 1.8f))) * s;
                int st = g.v.Count;
                g.Ellipsoid(c, pr, 7, 4, t > 0.85f ? rim : top, bottom);
                PG.Lumpy(g, st, c, 0.16f, i * 7.3f + H);
            }
            return m;
        }

        // ------------------------------------------------------------------ mangrove clump (origin = water level)
        public static PropModel Mangrove(System.Random rng, float size)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 root = C(108, 92, 76), rootD = C(80, 70, 60);
            Vector3 trunkTop = Up * (2.2f * size);
            int roots = 9 + rng.Next(5);
            for (int i = 0; i < roots; i++)
            {
                float a = i * Mathf.PI * 2f / roots + R(rng, -0.2f, 0.2f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = R(rng, 1.2f, 2.4f) * size;
                Vector3 foot = d * r - Up * 0.8f, knee = d * (r * 0.75f) + Up * (R(rng, 0.9f, 1.4f) * size), hub = d * 0.25f + Up * (1.3f * size);
                PG.Rod(g, foot, knee, 0.06f * size, rootD, 4);
                PG.Rod(g, knee, hub, 0.07f * size, root, 4);
            }
            g.Frustum(Up * (1.2f * size), trunkTop, 0.22f * size, 0.22f * size, 0.14f * size, 0.14f * size, 6, 0f, root, rootD, false, false);
            Color32 lt = C(78, 128, 66, 25), dk = C(40, 82, 50, 18), gl = C(104, 150, 74, 30);
            int lumps = 4 + rng.Next(3);
            for (int i = 0; i < lumps; i++)
            {
                float a = i * 2.2f + R(rng, 0f, 0.6f);
                Vector3 c = trunkTop + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * R(rng, 0.6f, 1.6f) * size + Up * R(rng, 0.3f, 1.1f) * size;
                int s = g.v.Count;
                g.Ellipsoid(c, new Vector3(R(rng, 1.3f, 2f), R(rng, 0.8f, 1.2f), R(rng, 1.3f, 2f)) * size, 7, 4, i == 0 ? gl : lt, dk);
                PG.Lumpy(g, s, c, 0.16f, i * 5.7f + size);
            }
            return m;
        }

        // ------------------------------------------------------------------ granite
        public static void GraniteBoulder(LifeGeo g, Vector3 c, Vector3 r, float seed)
        {
            int s = g.v.Count;
            g.Ellipsoid(c, r, 8, 5, GraniteA, GraniteC);
            PG.Lumpy(g, s, c, 0.2f, seed);
            // flatten the tops a little (weathered granite) and colour by height
            for (int i = s; i < g.v.Count; i++)
            {
                Vector3 p = g.v[i];
                if (p.y > c.y + r.y * 0.55f) p.y = Mathf.Lerp(p.y, c.y + r.y * 0.6f, 0.6f);
                g.v[i] = p;
                float k = Mathf.Clamp01((p.y - (c.y - r.y)) / (2f * r.y));
                Color32 col = Color32.Lerp(GraniteC, ((i / 3) % 4 == 0) ? GraniteB : GraniteA, k);
                g.c[i] = col;
            }
            for (int i = s; i + 2 < g.v.Count; i += 3)
            {
                Vector3 nn = Vector3.Cross(g.v[i + 1] - g.v[i], g.v[i + 2] - g.v[i]).normalized;
                g.n[i] = nn; g.n[i + 1] = nn; g.n[i + 2] = nn;
            }
        }

        /// <summary>Granite sea stack (world space): stacked eroded rings, a guano-white crown, scrub on top.</summary>
        public static void SeaStack(LifeGeo g, Vector3 basePos, float H, float Rb, System.Random rng)
        {
            const int NR = 8, NSIDE = 8;
            var ring = new Vector3[NR, NSIDE];
            Vector3 leanDir = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized;
            float lean = R(rng, 0.02f, 0.1f) * H;
            for (int i = 0; i < NR; i++)
            {
                float t = i / (float)(NR - 1);
                float r = Rb * (1f - 0.35f * t + 0.12f * Mathf.Sin(Mathf.PI * t * 2f)) * R(rng, 0.86f, 1.1f);
                if (i == NR - 1) r *= 0.8f;
                Vector3 c = basePos + Up * (H * t) + leanDir * (lean * t * t);
                float rot = R(rng, 0f, 0.5f);
                for (int k = 0; k < NSIDE; k++)
                {
                    float a = rot + k * Mathf.PI * 2f / NSIDE;
                    ring[i, k] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * R(rng, 0.78f, 1.15f) + Up * R(rng, -0.05f, 0.05f) * H / NR;
                }
            }
            Color32 white = C(236, 232, 222), scrub = C(96, 128, 76), scrubD = C(70, 100, 62);
            for (int i = 0; i < NR - 1; i++)
                for (int k = 0; k < NSIDE; k++)
                {
                    int k1 = (k + 1) % NSIDE;
                    Vector3 a = ring[i, k], b = ring[i, k1], d = ring[i + 1, k1], e = ring[i + 1, k];
                    Vector3 mid = (a + b + d + e) * 0.25f;
                    Vector3 outward = mid - (basePos + Up * (mid.y - basePos.y));
                    float t = (mid.y - basePos.y) / H;
                    Color32 col = (k * 3 + i) % 3 == 0 ? GraniteA : ((k + i * 2) % 3 == 1 ? GraniteB : GraniteC);
                    if (t < 0.18f) col = Sh(C(120, 112, 100), 0.9f);                // wet, weedy foot
                    if (i == NR - 2 && (k & 1) == 0) col = white;
                    g.QuadOut(a, b, d, e, outward, col, col, col, col);
                }
            Vector3 topC = Vector3.zero;
            for (int k = 0; k < NSIDE; k++) topC += ring[NR - 1, k];
            topC /= NSIDE;
            Vector3 apex = topC + Up * Rb * 0.2f;
            for (int k = 0; k < NSIDE; k++)
                g.TriOut(ring[NR - 1, k], ring[NR - 1, (k + 1) % NSIDE], apex, Up, scrub, scrubD, scrub);
            int s = g.v.Count;
            g.Ellipsoid(apex + Up * 0.2f, new Vector3(Rb * 0.45f, Rb * 0.18f, Rb * 0.4f), 6, 3, scrub, scrubD);
            PG.Lumpy(g, s, apex, 0.2f, H);
        }

        // ------------------------------------------------------------------ beach umbrella + loungers / towels
        public static readonly Color32[] Cloth =
        {
            C(232, 76, 62), C(246, 196, 64), C(66, 150, 196), C(240, 128, 150), C(84, 178, 152), C(250, 250, 244), C(246, 146, 60),
        };

        public static PropModel BeachSet(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 ca = Cloth[rng.Next(Cloth.Length)], cb = rng.NextDouble() < 0.6 ? Cloth[5] : Cloth[rng.Next(Cloth.Length)];
            Vector3 tilt = new Vector3(R(rng, -0.25f, 0.25f), 1f, R(rng, -0.25f, 0.25f)).normalized;
            Vector3 foot = Vector3.zero - Up * 0.35f, head = tilt * 2.55f;
            PG.Rod(g, foot, head, 0.035f, C(236, 232, 224), 5);
            Vector3 side1 = Vector3.Cross(tilt, Vector3.right).normalized, side2 = Vector3.Cross(tilt, side1).normalized;
            const int N = 10;
            Vector3 apex = head + tilt * 0.12f;
            for (int k = 0; k < N; k++)
            {
                float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                Vector3 e0 = head - tilt * 0.48f + (side1 * Mathf.Cos(a0) + side2 * Mathf.Sin(a0)) * 1.65f;
                Vector3 e1 = head - tilt * 0.48f + (side1 * Mathf.Cos(a1) + side2 * Mathf.Sin(a1)) * 1.65f;
                Color32 col = A((k & 1) == 0 ? ca : cb, 18);
                g.TriOut(apex, e0, e1, tilt, col, col, col);
                g.TriOut(apex - tilt * 0.04f, e0 - tilt * 0.04f, e1 - tilt * 0.04f, -tilt, Sh(col, 0.75f), Sh(col, 0.75f), Sh(col, 0.75f));
                // scalloped valance
                Vector3 v0 = e0 - tilt * 0.2f, v1 = e1 - tilt * 0.2f;
                PG.Quad2(g, e0, e1, v1, v0, col, col, Sh(col, 0.85f), Sh(col, 0.85f));
            }
            g.Ellipsoid(apex + tilt * 0.06f, new Vector3(0.06f, 0.06f, 0.06f), 4, 3, Pal.Gold, Pal.GoldDark);
            // two loungers or towels under it
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Color32 tc = Cloth[rng.Next(Cloth.Length)];
                Vector3 c = new Vector3(side * R(rng, 0.9f, 1.3f), 0f, R(rng, -0.5f, 0.5f));
                float yaw = R(rng, -20f, 20f);
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                if (rng.NextDouble() < 0.5)
                {
                    PG.OBox(g, c + Up * 0.02f, new Vector3(0.45f, 0.012f, 0.95f), q, tc);
                    PG.OBox(g, c + Up * 0.03f + q * new Vector3(0f, 0f, 0.6f), new Vector3(0.46f, 0.012f, 0.08f), q, Cloth[5]);
                }
                else
                {
                    Color32 frame = C(244, 240, 232);
                    PG.OBox(g, c + Up * 0.32f + q * new Vector3(0f, 0f, -0.25f), new Vector3(0.32f, 0.04f, 0.7f), q, tc);
                    Quaternion back = q * Quaternion.Euler(-48f, 0f, 0f);
                    PG.OBox(g, c + Up * 0.62f + q * new Vector3(0f, 0f, 0.62f), new Vector3(0.32f, 0.04f, 0.38f), back, tc);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        PG.Rod(g, c + q * new Vector3(s * 0.28f, -0.2f, -0.85f), c + q * new Vector3(s * 0.28f, 0.3f, -0.85f), 0.025f, frame, 4);
                        PG.Rod(g, c + q * new Vector3(s * 0.28f, -0.2f, 0.4f), c + q * new Vector3(s * 0.28f, 0.3f, 0.4f), 0.025f, frame, 4);
                    }
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ thatched beach snack hut (front +Z)
        public static PropModel BeachHut(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            float hx = 2.2f, hz = 1.7f, fy = 0.45f, wy = 2.6f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    PG.Column(g, new Vector3(x * hx, -0.4f, z * hz), new Vector3(x * hx, wy, z * hz), 0.1f, BambooDark, 6);
            PG.Box2(g, new Vector3(0f, fy - 0.06f, 0.4f), new Vector3(hx + 0.3f, 0.07f, hz + 0.7f), Pal.Wood, Pal.WoodLight);
            // bamboo walls (back and sides): vertical poles
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                Vector3 b = new Vector3(-hx + 2f * hx * t, fy, -hz);
                PG.Column(g, b, b + Up * (wy - fy), 0.07f, (i & 1) == 0 ? Bamboo : BambooDark, 4);
            }
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    Vector3 b = new Vector3(s * hx, fy, -hz + 2f * hz * t * 0.7f);
                    PG.Column(g, b, b + Up * (wy - fy), 0.07f, (i & 1) == 0 ? Bamboo : BambooDark, 4);
                }
            // counter with drinks
            PG.Box2(g, new Vector3(0f, fy + 0.55f, hz - 0.1f), new Vector3(hx - 0.2f, 0.55f, 0.18f), Bamboo, Pal.WoodLight);
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-hx + 0.6f + i * 0.62f, fy + 1.16f, hz - 0.1f);
                Color32 cc = i % 3 == 0 ? C(250, 186, 70) : (i % 3 == 1 ? C(232, 98, 96) : C(120, 196, 160));
                g.Frustum(p, p + Up * 0.22f, 0.06f, 0.06f, 0.05f, 0.05f, 5, 0f, cc, cc, true, true);
            }
            // coconuts on the counter, hanging lamps
            for (int i = 0; i < 3; i++) g.Ellipsoid(new Vector3(0.8f + i * 0.25f, fy + 1.22f, hz - 0.15f), Vector3.one * 0.12f, 5, 3, C(120, 132, 52), C(90, 100, 40));
            for (int s = -1; s <= 1; s += 2) PG.Lantern(gl, new Vector3(s * 1.2f, wy + 0.1f, hz + 0.4f), 0.18f, 0.3f, C(255, 200, 120), 80);
            // thatched hip roof with fringe
            float ox = hx + 0.9f, oz = hz + 1.1f, ry = wy + 1.7f;
            Vector3 r0 = new Vector3(-0.6f, ry, 0f), r1 = new Vector3(0.6f, ry, 0f);
            Vector3 c00 = new Vector3(-ox, wy, -oz), c10 = new Vector3(ox, wy, -oz), c11 = new Vector3(ox, wy, oz), c01 = new Vector3(-ox, wy, oz);
            g.QuadOut(c00, c10, r1, r0, new Vector3(0f, 1f, -1f), Thatch, Thatch, ThatchDark, ThatchDark);
            g.QuadOut(c01, c11, r1, r0, new Vector3(0f, 1f, 1f), Thatch, Thatch, ThatchDark, ThatchDark);
            g.TriOut(c00, c01, r0, new Vector3(-1f, 1f, 0f), Thatch, Thatch, ThatchDark);
            g.TriOut(c10, c11, r1, new Vector3(1f, 1f, 0f), Thatch, Thatch, ThatchDark);
            Color32 under = Sh(ThatchDark, 0.7f);
            g.QuadOut(c00, c10, c11, c01, Vector3.down, under, under, under, under);
            Vector3 dn = Up * 0.35f;
            g.QuadOut(c00, c10, c10 - dn, c00 - dn, Vector3.back, ThatchDark, ThatchDark, Thatch, Thatch);
            g.QuadOut(c01, c11, c11 - dn, c01 - dn, Vector3.forward, ThatchDark, ThatchDark, Thatch, Thatch);
            g.QuadOut(c00, c01, c01 - dn, c00 - dn, Vector3.left, ThatchDark, ThatchDark, Thatch, Thatch);
            g.QuadOut(c10, c11, c11 - dn, c10 - dn, Vector3.right, ThatchDark, ThatchDark, Thatch, Thatch);
            PG.Beam(g, r0 + Up * 0.05f, r1 + Up * 0.05f, 0.12f, 0.1f, BambooDark);
            // sign board
            PG.Box2(g, new Vector3(0f, wy - 0.25f, oz - 0.05f), new Vector3(1.1f, 0.24f, 0.04f), C(64, 150, 170), C(64, 150, 170));
            for (int i = 0; i < 4; i++) g.Box(new Vector3(-0.66f + i * 0.44f, wy - 0.25f, oz), new Vector3(0.14f, 0.12f, 0.02f), C(250, 240, 214));
            // stools
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(-1.2f + i * 1.2f, 0f, hz + 1.2f);
                g.Frustum(p, p + Up * 0.65f, 0.05f, 0.05f, 0.05f, 0.05f, 4, 0f, BambooDark, BambooDark, false, false);
                g.Frustum(p + Up * 0.65f, p + Up * 0.72f, 0.22f, 0.22f, 0.22f, 0.22f, 7, 0f, C(232, 76, 62), C(232, 76, 62), true, true);
            }
            return m;
        }

        // ------------------------------------------------------------------ lifeguard tower (faces +Z, the sea)
        public static PropModel LifeguardTower()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            Color32 wood = C(236, 232, 222), red = C(214, 60, 48), yel = C(248, 204, 64);
            float py = 3.2f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    PG.Beam(g, new Vector3(x * 1.25f, -0.4f, z * 1.25f), new Vector3(x * 0.85f, py, z * 0.85f), 0.08f, 0.08f, wood);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Beam(g, new Vector3(s * 1.15f, 0.6f, -1.15f), new Vector3(-s * 0.95f, 2.6f, -0.95f), 0.04f, 0.04f, wood);
                PG.Beam(g, new Vector3(-1.15f, 0.6f, s * 1.15f), new Vector3(-0.95f, 2.6f, -s * 0.95f), 0.04f, 0.04f, wood);
            }
            PG.Box2(g, new Vector3(0f, py, 0.2f), new Vector3(1.25f, 0.08f, 1.45f), Sh(wood, 0.85f), C(196, 160, 112));
            PG.Box2(g, new Vector3(0f, py + 0.95f, -0.25f), new Vector3(0.95f, 0.9f, 0.85f), wood, wood);
            PG.BoxMM(g, new Vector3(-0.96f, py + 1.1f, -1.12f), new Vector3(0.96f, py + 1.35f, 0.61f), red);
            PG.BoxMM(g, new Vector3(-0.7f, py + 0.5f, 0.6f), new Vector3(0.7f, py + 1.3f, 0.62f), C(80, 120, 150));
            // slanted roof
            PG.OBox(g, new Vector3(0f, py + 2.05f, -0.15f), new Vector3(1.3f, 0.06f, 1.25f), Quaternion.Euler(-10f, 0f, 0f), red);
            // railing
            for (int i = 0; i <= 5; i++)
            {
                float x = -1.2f + i * 0.48f;
                PG.Rod(g, new Vector3(x, py, 1.6f), new Vector3(x, py + 0.9f, 1.6f), 0.025f, wood, 4);
            }
            PG.Beam(g, new Vector3(-1.25f, py + 0.9f, 1.6f), new Vector3(1.25f, py + 0.9f, 1.6f), 0.04f, 0.04f, yel);
            // ladder
            for (int s = -1; s <= 1; s += 2) PG.Beam(g, new Vector3(s * 0.32f, -0.2f, 3.0f), new Vector3(s * 0.32f, py, 1.65f), 0.04f, 0.04f, wood);
            for (int i = 1; i < 8; i++)
            {
                float t = i / 8f;
                Vector3 p = Vector3.Lerp(new Vector3(0f, -0.2f, 3.0f), new Vector3(0f, py, 1.65f), t);
                PG.Rod(g, p - Vector3.right * 0.32f, p + Vector3.right * 0.32f, 0.025f, wood, 4);
            }
            // lifebuoy
            Vector3 lb = new Vector3(0.95f, py + 0.6f, 1.65f);
            for (int k = 0; k < 10; k++)
            {
                float a0 = k * Mathf.PI * 2f / 10f, a1 = (k + 1) * Mathf.PI * 2f / 10f;
                PG.Rod(g, lb + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 0.3f, lb + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 0.3f, 0.07f, (k & 1) == 0 ? red : wood, 5);
            }
            // flag
            Vector3 fp = new Vector3(-1.1f, py + 2f, -1.1f);
            PG.Rod(g, fp, fp + Up * 2.4f, 0.03f, wood, 4);
            Vector3 f0 = fp + Up * 2.35f, f1 = fp + Up * 1.65f;
            for (int i = 0; i < 3; i++)
            {
                float u0 = i / 3f, u1 = (i + 1) / 3f;
                Vector3 o0 = new Vector3(1f, 0f, 0.3f) * (1.2f * u0), o1 = new Vector3(1f, 0f, 0.3f) * (1.2f * u1);
                Color32 c0 = A(i < 2 ? red : yel, 60 + (int)(190 * u0)), c1 = A(i < 1 ? red : yel, 60 + (int)(190 * u1));
                Vector3 mid = Vector3.Lerp(f0, f1, 0.5f);
                PG.Quad2(st, f0 + o0, f0 + o1, mid + o1, mid + o0, c0, c1, c1, c0);
                PG.Quad2(st, mid + o0, mid + o1, f1 + o1, f1 + o0, A(yel, c0.a), A(yel, c1.a), A(yel, c1.a), A(yel, c0.a));
            }
            return m;
        }

        // ------------------------------------------------------------------ volleyball court (net along X)
        public static PropModel VolleyCourt()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 post = C(232, 228, 220), net = C(52, 52, 58), band = C(248, 248, 244), line = C(70, 120, 200);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Column(g, new Vector3(s * 4.9f, -0.3f, 0f), new Vector3(s * 4.9f, 2.55f, 0f), 0.06f, post, 6);
                PG.Rod(g, new Vector3(s * 4.9f, 2.5f, 0f), new Vector3(s * 6.4f, 0f, 0f), 0.012f, post, 3);
            }
            PG.Beam(g, new Vector3(-4.9f, 2.43f, 0f), new Vector3(4.9f, 2.43f, 0f), 0.006f, 0.04f, band);
            PG.Beam(g, new Vector3(-4.9f, 1.47f, 0f), new Vector3(4.9f, 1.47f, 0f), 0.006f, 0.02f, band);
            for (int i = 0; i <= 32; i++)
            {
                float x = -4.8f + i * 0.3f;
                PG.Beam(g, new Vector3(x, 1.47f, 0f), new Vector3(x, 2.43f, 0f), 0.006f, 0.008f, net);
            }
            for (int j = 1; j < 4; j++)
                PG.Beam(g, new Vector3(-4.85f, 1.47f + j * 0.24f, 0f), new Vector3(4.85f, 1.47f + j * 0.24f, 0f), 0.006f, 0.008f, net);
            // court boundary tapes (9 x 18)
            PG.BoxMM(g, new Vector3(-4.5f, 0.0f, -9f), new Vector3(4.5f, 0.03f, -8.92f), line);
            PG.BoxMM(g, new Vector3(-4.5f, 0.0f, 8.92f), new Vector3(4.5f, 0.03f, 9f), line);
            PG.BoxMM(g, new Vector3(-4.5f, 0.0f, -9f), new Vector3(-4.42f, 0.03f, 9f), line);
            PG.BoxMM(g, new Vector3(4.42f, 0.0f, -9f), new Vector3(4.5f, 0.03f, 9f), line);
            g.Ellipsoid(new Vector3(1.6f, 0.11f, -3.2f), Vector3.one * 0.11f, 6, 4, C(250, 214, 70), C(60, 110, 200));
            return m;
        }

        // ------------------------------------------------------------------ kites (LifeBatch templates: kite + string to the origin)
        public static LifeGeo KiteOnString(int type, Color32 a, Color32 b, float height, float back)
        {
            var g = new LifeGeo();
            var k = type == 0 ? LifeShapes.KiteDiamond(a, b) : LifeShapes.KiteSwallow(a, b);
            Vector3 at = new Vector3(0f, height, back);
            PG.Append(g, k, Matrix4x4.TRS(at, Quaternion.Euler(-25f, 180f, 0f), Vector3.one * 1.5f));
            if (type == 0)
            {
                // ribbon tail
                Color32 t = C(250, 246, 236);
                Vector3 p = at - Up * 1.3f;
                for (int i = 0; i < 6; i++)
                {
                    Vector3 q = p - Up * 0.6f + new Vector3(Mathf.Sin(i * 1.3f) * 0.25f, 0f, 0.2f);
                    PG.Quad2(g, p - Vector3.right * 0.06f, p + Vector3.right * 0.06f, q + Vector3.right * 0.05f, q - Vector3.right * 0.05f, i % 2 == 0 ? t : a, t, t, t);
                    p = q;
                }
            }
            Color32 sc = C(240, 236, 226);
            PG.Rod(g, new Vector3(0f, 1.1f, 0f), at - Up * 0.2f, 0.012f, sc, 3);
            return g;
        }

        // ------------------------------------------------------------------ tide-pool rocks with starfish and shells (world space into g)
        public static void TidePool(LifeGeo g, LifeGeo water, Vector3 c, float r, System.Random rng, float waterY)
        {
            int n = 6 + rng.Next(4);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n + R(rng, -0.2f, 0.2f);
                Vector3 p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * R(rng, 0.9f, 1.15f);
                GraniteBoulder(g, p, new Vector3(R(rng, 0.7f, 1.3f), R(rng, 0.35f, 0.6f), R(rng, 0.6f, 1.1f)) * (r * 0.42f), R(rng, 0f, 50f));
            }
            Color32 star = C(240, 110, 60), star2 = C(232, 74, 92), shell = C(246, 230, 214);
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = c + new Vector3(R(rng, -0.6f, 0.6f), 0f, R(rng, -0.6f, 0.6f)) * r;
                p.y = waterY - 0.15f;
                float rot = R(rng, 0f, 6.28f);
                Color32 sc = i == 1 ? star2 : star;
                for (int k = 0; k < 5; k++)
                {
                    float a = rot + k * Mathf.PI * 0.4f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), s = new Vector3(-d.z, 0f, d.x);
                    g.TriOut(p + s * 0.07f, p + d * 0.32f, p - s * 0.07f, Up, sc, Sh(sc, 0.8f), sc);
                }
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = c + new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)) * r * 1.2f;
                p.y = c.y + 0.1f;
                g.Ellipsoid(p, new Vector3(0.09f, 0.05f, 0.11f), 5, 3, shell, Sh(shell, 0.85f));
            }
            water.Disc(new Vector3(c.x, waterY, c.z), r * 0.95f, 10, 0f, Up, C(255, 255, 255, 255), C(255, 255, 255, 255));
        }
    }
}
