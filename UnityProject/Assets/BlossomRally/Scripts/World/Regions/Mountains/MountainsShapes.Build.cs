// MountainsShapes.Build.cs - low-poly models of the Cloud Peak Mountains: granite pillars, pines (forest, spruce,
// Huangshan cliff pines), red / gold maples, road furniture (km stones, chevrons, delineators, stone lanterns),
// lookout pavilions, monastery buildings (golden halls, gate, pagoda), prayer-ribbon trees, cable car parts and
// eagles. Local space: origin on the ground, +Z = front. Vertex colours sRGB, alpha = wind weight.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class MtnShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 A(Color32 c, int a) { return PG.A(c, a); }
        static Color32 Sh(Color32 c, float k) { return PG.Sh(c, k); }
        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        public static readonly Color32 GraniteL = C(206, 202, 190), GraniteM = C(176, 174, 166), GraniteD = C(128, 128, 124), GraniteW = C(214, 198, 172);
        public static readonly Color32 MossC = C(98, 134, 82), MossL = C(126, 156, 96);
        public static readonly Color32 Bark = C(104, 76, 58), BarkD = C(76, 56, 44);

        // ------------------------------------------------------------------ granite pillar (world space)
        /// <summary>
        /// Tall faceted granite pillar from basePos (already sunk into the ground) up H metres, base radius Rb.
        /// Stacked joint blocks with slight offsets, vertical crack streaks, moss on ledges and a green crown.
        /// Returns the top centre (for pines / shrubs).
        /// </summary>
        public static Vector3 Pillar(LifeGeo g, Vector3 basePos, float H, float Rb, System.Random rng, float warm, out float taperOut)
        {
            int NR = Mathf.Clamp(5 + Mathf.RoundToInt(H / 9f), 6, 14);
            int NS = H > 40f ? 8 : 7;
            var prof = new float[NS];
            for (int k = 0; k < NS; k++) prof[k] = R(rng, 0.72f, 1.12f);
            var ring = new Vector3[NR + 1, NS];
            Vector3 lean = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * R(rng, 0.02f, 0.07f) * H;
            float rot = R(rng, 0f, 6.28f);
            Vector3 off = Vector3.zero;
            float taper = R(rng, 0.55f, 0.8f);
            taperOut = taper;
            for (int i = 0; i <= NR; i++)
            {
                float t = i / (float)NR;
                if (i > 0 && i < NR && (i % 2 == 0)) off += new Vector3(R(rng, -0.06f, 0.06f), 0f, R(rng, -0.06f, 0.06f)) * Rb;
                float r = Rb * Mathf.Lerp(1f, taper, t) * (i == 0 ? 1.18f : 1f) * (1f + 0.05f * Mathf.Sin(t * 9f + rot));
                if (i == NR) r *= 0.82f;
                Vector3 c = basePos + Up * (H * t) + lean * (t * t) + off;
                for (int k = 0; k < NS; k++)
                {
                    float a = rot + k * Mathf.PI * 2f / NS;
                    float pr = prof[k] * R(rng, 0.95f, 1.05f);
                    ring[i, k] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * pr;
                }
            }
            Color32 lite = Color32.Lerp(GraniteL, GraniteW, warm), mid = Color32.Lerp(GraniteM, GraniteW, warm * 0.6f), dark = GraniteD;
            // per-column tone (vertical streaks), per-ring tone (horizontal joints), cracks, moss on ledges
            var colTone = new Color32[NS];
            for (int k = 0; k < NS; k++)
            {
                float r = R(rng, 0f, 1f);
                colTone[k] = r < 0.45f ? lite : r < 0.8f ? Color32.Lerp(lite, mid, 0.5f) : mid;
            }
            var ringK = new float[NR];
            for (int i = 0; i < NR; i++) ringK[i] = R(rng, 0.93f, 1.04f);
            int crackA = rng.Next(NS), crackB = (crackA + 2 + rng.Next(Mathf.Max(1, NS - 3))) % NS;
            int ledge = 2 + rng.Next(Mathf.Max(1, NR - 4));
            for (int i = 0; i < NR; i++)
                for (int k = 0; k < NS; k++)
                {
                    int k1 = (k + 1) % NS;
                    Vector3 a = ring[i, k], b = ring[i, k1], d = ring[i + 1, k1], e = ring[i + 1, k];
                    Vector3 mid3 = (a + b + d + e) * 0.25f;
                    float tt = (mid3.y - basePos.y) / H;
                    Vector3 outward = mid3 - (basePos + lean * (tt * tt) + Up * (mid3.y - basePos.y));
                    float t = (i + 0.5f) / NR;
                    Color32 col = Sh(colTone[k], ringK[i] * Mathf.Lerp(0.8f, 1.05f, t));
                    if (k == crackA || k == crackB) col = Color32.Lerp(col, dark, 0.45f);
                    Color32 bot = Sh(col, 0.9f), top = col;
                    if (i == ledge && (k % 3) == 0) top = Color32.Lerp(col, MossC, 0.7f);
                    if (i >= NR - 2 && ((k + i) % 3) == 0) top = Color32.Lerp(col, MossC, 0.5f);
                    g.QuadOut(a, b, d, e, outward, bot, bot, top, top);
                }
            // crown: slanted cap with moss
            Vector3 topC = Vector3.zero;
            for (int k = 0; k < NS; k++) topC += ring[NR, k];
            topC /= NS;
            Vector3 apex = topC + Up * Rb * R(rng, 0.15f, 0.4f);
            for (int k = 0; k < NS; k++)
                g.TriOut(ring[NR, k], ring[NR, (k + 1) % NS], apex, Up, (k & 1) == 0 ? MossC : MossL, MossC, MossL);
            return apex;
        }

        /// <summary>A rounded granite boulder / outcrop (world space).</summary>
        public static void Outcrop(LifeGeo g, Vector3 c, Vector3 r, System.Random rng, float warm)
        {
            Color32 top = Color32.Lerp(GraniteL, GraniteW, warm), bot = GraniteD;
            int s = g.v.Count;
            g.Ellipsoid(c, r, 7, 4, top, bot);
            PG.Lumpy(g, s, c, 0.16f, R(rng, 0f, 50f));
        }

        // ------------------------------------------------------------------ pines
        /// <summary>Layered Chinese pine (flat tiers) - cheap forest tree. scale ~1 = 9-12 m tall.</summary>
        public static PropModel ForestPine(System.Random rng, int variant)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            float H = R(rng, 9f, 12.5f);
            Vector3 lean = new Vector3(R(rng, -0.6f, 0.6f), 0f, R(rng, -0.6f, 0.6f));
            Color32 dk = variant == 1 ? C(44, 78, 62, 22) : C(48, 86, 58, 22), md = variant == 1 ? C(62, 102, 76, 28) : C(70, 112, 70, 28);
            Color32 lt = variant == 1 ? C(88, 128, 92, 32) : C(102, 140, 82, 32);
            g.Frustum(-Up * 0.4f, Up * (H * 0.8f) + lean, 0.24f, 0.24f, 0.07f, 0.07f, 5, 0.3f, BarkD, Bark, false, false);
            if (variant == 1)
            {
                // spruce-like stacked cones
                int tiers = 5;
                for (int i = 0; i < tiers; i++)
                {
                    float t = i / (float)(tiers - 1);
                    float y = Mathf.Lerp(H * 0.22f, H * 0.82f, t);
                    float r = Mathf.Lerp(2.6f, 0.9f, t) * R(rng, 0.9f, 1.1f);
                    Vector3 c = Up * y + lean * (y / H);
                    g.Frustum(c, c + Up * Mathf.Lerp(2.6f, 1.8f, t), r, r, 0.05f, 0.05f, 7, R(rng, 0f, 1f), i == 0 ? dk : md, lt, true, false);
                }
            }
            else
            {
                // flat-topped tiers, wider at the bottom, offset like an old pine
                int tiers = variant == 2 ? 3 : 4;
                for (int i = 0; i < tiers; i++)
                {
                    float t = i / (float)(tiers - 1);
                    float y = Mathf.Lerp(H * 0.42f, H * 0.95f, t);
                    float r = Mathf.Lerp(variant == 2 ? 3.2f : 2.7f, 1.3f, t) * R(rng, 0.85f, 1.12f);
                    Vector3 c = Up * y + lean * (y / H) + new Vector3(R(rng, -0.5f, 0.5f), 0f, R(rng, -0.5f, 0.5f));
                    float th = R(rng, 0.7f, 1.0f);
                    g.Frustum(c - Up * th * 0.5f, c + Up * th * 0.5f, r, r * 0.82f, r * 0.62f, r * 0.55f, 7, R(rng, 0f, 1f), dk, md, true, false);
                    g.Frustum(c + Up * th * 0.5f, c + Up * (th * 0.5f + 0.35f), r * 0.62f, r * 0.55f, r * 0.2f, r * 0.18f, 7, R(rng, 0f, 1f), md, lt, false, true);
                }
            }
            return m;
        }

        /// <summary>Huangshan "welcoming pine" growing from rock: leaning trunk, flat cloud-like pads (world-space append).</summary>
        public static PropModel CliffPine(System.Random rng, float scale)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            float H = R(rng, 4.5f, 7f) * scale;
            Vector3 dir = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized;
            var pts = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                pts[i] = Up * (H * t - 0.3f) + dir * (Mathf.Sin(t * 2.4f) * 1.4f * scale) + new Vector3(R(rng, -0.2f, 0.2f), 0f, R(rng, -0.2f, 0.2f)) * scale * (i > 0 ? 1f : 0f);
            }
            for (int i = 0; i < 3; i++)
                g.Frustum(pts[i], pts[i + 1], (0.3f - i * 0.07f) * scale, (0.3f - i * 0.07f) * scale, (0.23f - i * 0.07f) * scale, (0.23f - i * 0.07f) * scale, 5, 0.4f, i % 2 == 0 ? BarkD : Bark, Bark, false, true);
            Color32 top = C(70, 118, 78, 30), bottom = C(38, 68, 52, 20), rim = C(98, 140, 88, 30);
            int pads = 3 + rng.Next(2);
            for (int i = 0; i < pads; i++)
            {
                float t = pads == 1 ? 1f : i / (float)(pads - 1);
                Vector3 from = Vector3.Lerp(pts[1], pts[3], 0.3f + 0.7f * t);
                float a = R(rng, 0f, Mathf.PI * 2f);
                Vector3 d = Vector3.Lerp(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), dir, 0.45f).normalized;
                float reach = R(rng, 1.2f, 2.6f) * scale * (1.15f - t * 0.5f);
                Vector3 end = from + d * reach + Up * R(rng, -0.2f, 0.4f) * scale;
                g.Frustum(from, end, 0.1f * scale, 0.1f * scale, 0.05f * scale, 0.05f * scale, 4, 0f, Bark, BarkD, false, false);
                Vector3 pr = new Vector3(R(rng, 1.4f, 2.1f), R(rng, 0.32f, 0.45f), R(rng, 1.1f, 1.6f)) * scale * (1.1f - t * 0.35f);
                int s = g.v.Count;
                g.Ellipsoid(end + Up * 0.12f * scale, pr, 7, 3, rim, bottom);
                PG.Lumpy(g, s, end, 0.12f, i * 7f + R(rng, 0f, 9f));
                int s2 = g.v.Count;
                g.Ellipsoid(end + Up * (0.12f * scale + pr.y * 0.5f), new Vector3(pr.x * 0.65f, pr.y * 0.55f, pr.z * 0.65f), 6, 2, top, rim);
                PG.Lumpy(g, s2, end, 0.1f, i * 5f);
            }
            return m;
        }

        // ------------------------------------------------------------------ maple (red / orange / gold)
        public static PropModel Maple(System.Random rng, int tone)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 a, b, c2;
            switch (tone)
            {
                case 0: a = C(206, 58, 40, 45); b = C(160, 34, 32, 30); c2 = C(232, 92, 50, 45); break;     // crimson
                case 1: a = C(236, 118, 42, 45); b = C(196, 74, 34, 30); c2 = C(248, 150, 60, 45); break;   // orange
                default: a = C(240, 186, 64, 45); b = C(206, 140, 42, 30); c2 = C(250, 210, 96, 45); break; // gold
            }
            float H = R(rng, 4.2f, 6.2f);
            Vector3 lean = new Vector3(R(rng, -0.4f, 0.4f), 0f, R(rng, -0.4f, 0.4f));
            Vector3 t0 = -Up * 0.3f, t1 = Up * (H * 0.45f) + lean * 0.5f, t2 = Up * (H * 0.7f) + lean;
            g.Frustum(t0, t1, 0.2f, 0.2f, 0.15f, 0.15f, 5, 0f, BarkD, Bark, false, false);
            g.Frustum(t1, t2, 0.15f, 0.15f, 0.08f, 0.08f, 5, 0f, Bark, BarkD, false, true);
            int clumps = 4 + rng.Next(2);
            for (int i = 0; i < clumps; i++)
            {
                float ang = i * Mathf.PI * 2f / clumps + R(rng, -0.4f, 0.4f);
                Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 c = (i == clumps - 1 ? t2 + Up * 0.8f : Vector3.Lerp(t1, t2, R(rng, 0.3f, 1f)) + d * R(rng, 1.0f, 1.8f) + Up * R(rng, 0.2f, 0.9f));
                if (i < clumps - 1) g.Frustum(Vector3.Lerp(t1, t2, 0.5f), c, 0.07f, 0.07f, 0.03f, 0.03f, 4, 0f, Bark, BarkD, false, false);
                float r = R(rng, 1.1f, 1.6f);
                int s = g.v.Count;
                g.Ellipsoid(c, new Vector3(r, r * 0.72f, r), 6, 3, rng.NextDouble() < 0.4 ? c2 : a, b);
                PG.Lumpy(g, s, c, 0.2f, i * 3.7f + R(rng, 0f, 9f));
            }
            // a scatter of fallen leaves at the foot
            Color32 leaf = Sh(a, 1.05f); leaf.a = 0;
            for (int i = 0; i < 18; i++)
            {
                float an = R(rng, 0f, 6.28f), rr = R(rng, 0.5f, 2.8f);
                Vector3 p = new Vector3(Mathf.Cos(an) * rr, 0.05f, Mathf.Sin(an) * rr);
                float rot = R(rng, 0f, 6.28f);
                Vector3 u = new Vector3(Mathf.Cos(rot), 0f, Mathf.Sin(rot)) * 0.09f, w = new Vector3(-u.z, 0f, u.x);
                g.TriOut(p - u, p + w, p + u, Up, leaf, leaf, leaf);
            }
            return m;
        }

        /// <summary>Old broad tree hung with red prayer ribbons and wooden wish tablets.</summary>
        public static PropModel PrayerTree(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var st = m[PK.Strong];
            Color32 bark = C(96, 78, 62), barkL = C(122, 98, 76);
            Vector3 p0 = -Up * 0.4f, p1 = Up * 2.6f + new Vector3(0.3f, 0f, 0.2f), p2 = Up * 4.6f + new Vector3(-0.2f, 0f, 0.4f);
            g.Frustum(p0, p1, 0.75f, 0.75f, 0.55f, 0.55f, 7, 0f, bark, barkL, false, false);
            g.Frustum(p1, p2, 0.55f, 0.55f, 0.38f, 0.38f, 7, 0f, barkL, bark, false, true);
            var tips = new List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + R(rng, -0.3f, 0.3f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 from = Vector3.Lerp(p1, p2, R(rng, 0.2f, 1f));
                Vector3 to = from + d * R(rng, 3.2f, 4.6f) + Up * R(rng, 0.8f, 1.8f);
                g.Frustum(from, to, 0.24f, 0.24f, 0.1f, 0.1f, 5, 0f, bark, barkL, false, true);
                tips.Add(to);
                Vector3 cc = to + Up * 0.7f;
                int s = g.v.Count;
                g.Ellipsoid(cc, new Vector3(R(rng, 2.0f, 2.6f), R(rng, 1.0f, 1.4f), R(rng, 2.0f, 2.6f)), 7, 3, C(96, 142, 76, 35), C(62, 104, 58, 25));
                PG.Lumpy(g, s, cc, 0.16f, i * 2.3f);
            }
            int sc = g.v.Count;
            g.Ellipsoid(p2 + Up * 1.6f, new Vector3(3f, 1.6f, 3f), 8, 3, C(104, 150, 82, 35), C(66, 108, 60, 25));
            PG.Lumpy(g, sc, p2 + Up * 1.6f, 0.14f, 41f);
            // ribbons + tablets hanging from the limbs
            foreach (var tp in tips)
                for (int k = 0; k < 7; k++)
                {
                    Vector3 top = Vector3.Lerp(Vector3.Lerp(p1, p2, 0.6f), tp, R(rng, 0.35f, 0.95f)) - Up * 0.1f;
                    float len = R(rng, 0.7f, 1.5f);
                    Vector3 side = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * 0.06f;
                    Color32 rc = (k % 5 == 0) ? C(240, 200, 70) : C(214, 40, 40);
                    PG.Quad2(st, top - side, top + side, top + side - Up * len, top - side - Up * len, A(rc, 20), A(rc, 20), A(rc, 230), A(rc, 230));
                    if (k % 3 == 0) st.Box(top - Up * (len + 0.12f), new Vector3(0.09f, 0.12f, 0.02f), A(C(196, 156, 100), 240));
                }
            // stone fence ring around the trunk
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 2.2f, 0f, Mathf.Sin(a) * 2.2f);
                g.Box(p + Up * 0.35f, new Vector3(0.12f, 0.35f, 0.12f), Pal.Stone);
                Vector3 q = new Vector3(Mathf.Cos(a + Mathf.PI / 6f) * 2.2f, 0f, Mathf.Sin(a + Mathf.PI / 6f) * 2.2f);
                PG.Beam(g, p + Up * 0.55f, q + Up * 0.55f, 0.05f, 0.07f, Pal.StoneLight);
            }
            return m;
        }

        // ------------------------------------------------------------------ road furniture
        /// <summary>Kilometre stone (white with a red cap, as on Chinese mountain roads). big = km, small = 100 m.</summary>
        public static PropModel KmStone(bool big)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            float h = big ? 0.85f : 0.45f, w = big ? 0.22f : 0.12f, d = big ? 0.12f : 0.1f;
            PG.Box2(g, new Vector3(0f, h * 0.5f - 0.1f, 0f), new Vector3(w, h * 0.5f + 0.1f, d), C(236, 234, 226), C(236, 234, 226));
            g.Frustum(Up * h, Up * (h + w * 0.9f), w, d, w * 0.15f, d * 0.6f, 4, Mathf.PI * 0.25f, C(196, 44, 40), C(206, 54, 46), false, true);
            if (big)
            {
                g.Box(new Vector3(0f, h * 0.55f, d + 0.005f), new Vector3(0.12f, 0.08f, 0.004f), C(40, 40, 46));
                g.Box(new Vector3(0f, h * 0.55f, -d - 0.005f), new Vector3(0.12f, 0.08f, 0.004f), C(40, 40, 46));
            }
            return m;
        }

        /// <summary>Chevron board on two posts (blue / white, arrows pointing towards local +X * dir).</summary>
        public static PropModel Chevron(float dir)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 post = C(220, 220, 214), board = C(36, 74, 160), white = C(246, 246, 240);
            for (int s = -1; s <= 1; s += 2) g.Frustum(new Vector3(s * 0.45f, -0.3f, 0f), new Vector3(s * 0.45f, 1.55f, 0f), 0.04f, 0.04f, 0.04f, 0.04f, 4, 0.8f, post, post, false, true);
            PG.Box2(g, new Vector3(0f, 1.25f, 0.04f), new Vector3(0.62f, 0.38f, 0.025f), board, board);
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.32f;
                Vector3 a = new Vector3(x - 0.12f * dir, 1.52f, 0.07f), b = new Vector3(x + 0.1f * dir, 1.25f, 0.07f), c = new Vector3(x - 0.12f * dir, 0.98f, 0.07f);
                Vector3 a2 = a + new Vector3(0.1f * dir, 0f, 0f), b2 = b + new Vector3(0.1f * dir, 0f, 0f), c2 = c + new Vector3(0.1f * dir, 0f, 0f);
                g.QuadOut(a, a2, b2, b, Vector3.forward, white, white, white, white);
                g.QuadOut(b, b2, c2, c, Vector3.forward, white, white, white, white);
            }
            return m;
        }

        /// <summary>White roadside delineator post with a red reflector band.</summary>
        public static PropModel Delineator()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Box2(g, new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.65f, 0.06f), C(238, 238, 232), C(238, 238, 232));
            g.Box(new Vector3(0f, 0.88f, 0f), new Vector3(0.065f, 0.06f, 0.065f), C(214, 40, 36));
            return m;
        }

        /// <summary>Stone lantern (granite, lit window boxes at night).</summary>
        public static PropModel StoneLantern()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            PG.Box2(g, new Vector3(0f, 0.12f, 0f), new Vector3(0.42f, 0.14f, 0.42f), Pal.StoneDark, Pal.Stone);
            g.Frustum(Up * 0.24f, Up * 1.05f, 0.14f, 0.14f, 0.12f, 0.12f, 6, 0f, Pal.Stone, Pal.StoneLight, false, false);
            PG.Box2(g, new Vector3(0f, 1.12f, 0f), new Vector3(0.34f, 0.08f, 0.34f), Pal.Stone, Pal.StoneLight);
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                g.Box(q * new Vector3(0.24f, 1.42f, 0.24f), new Vector3(0.06f, 0.22f, 0.06f), Pal.Stone);
            }
            gl.Box(Up * 1.42f, new Vector3(0.18f, 0.18f, 0.18f), C(255, 214, 140, 0));
            g.Frustum(Up * 1.62f, Up * 1.95f, 0.5f, 0.5f, 0.08f, 0.08f, 6, Mathf.PI / 6f, Pal.StoneDark, Pal.Stone, true, false);
            g.Ellipsoid(Up * 2.0f, new Vector3(0.09f, 0.1f, 0.09f), 5, 3, Pal.StoneLight, Pal.Stone);
            return m;
        }

        /// <summary>Red lantern post (wooden pole with a hanging lantern, lit at night).</summary>
        public static PropModel LanternPost()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            g.Frustum(-Up * 0.3f, Up * 3.2f, 0.09f, 0.09f, 0.07f, 0.07f, 6, 0f, Pal.RedDeep, Pal.Red, false, true);
            PG.Beam(g, Up * 3.0f, new Vector3(0f, 3.0f, 0.75f), 0.04f, 0.05f, Pal.WoodDark);
            g.Box(new Vector3(0f, 3.05f, 0.85f), new Vector3(0.08f, 0.04f, 0.1f), Pal.Gold);
            PG.Rod(gl, new Vector3(0f, 2.98f, 0.75f), new Vector3(0f, 2.8f, 0.75f), 0.01f, A(Pal.Ink, 60), 3);
            PG.Lantern(gl, new Vector3(0f, 2.8f, 0.75f), 0.24f, 0.42f, Pal.RedBright, 120);
            PG.Box2(g, new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.1f, 0.22f), Pal.StoneDark, Pal.Stone);
            return m;
        }

        // ------------------------------------------------------------------ lookout pavilion (square, double eave)
        public static PropModel Pavilion(System.Random rng, bool golden)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 tileA = golden ? C(222, 170, 64) : C(64, 128, 104), tileB = golden ? C(238, 192, 86) : C(82, 146, 116);
            PG.Box2(g, new Vector3(0f, 0.15f, 0f), new Vector3(3.3f, 0.35f, 3.3f), Pal.StoneDark, Pal.StoneLight);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -2.5f : 2.5f, z = (i & 2) == 0 ? -2.5f : 2.5f;
                PG.Column(g, new Vector3(x, 0.5f, z), new Vector3(x, 3.4f, z), 0.15f, Pal.Red, 6);
                g.Frustum(new Vector3(x, 0.5f, z), new Vector3(x, 0.72f, z), 0.24f, 0.24f, 0.2f, 0.2f, 6, 0f, Pal.Stone, Pal.Stone, false, true);
            }
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                PG.Beam(g, q * new Vector3(-2.6f, 3.3f, 2.5f), q * new Vector3(2.6f, 3.3f, 2.5f), 0.12f, 0.16f, C(56, 118, 122));
                PG.Beam(g, q * new Vector3(-2.5f, 2.9f, 2.5f), q * new Vector3(2.5f, 2.9f, 2.5f), 0.06f, 0.05f, Pal.Gold);
                // bench-railing (美人靠) on three sides
                if (k != 0)
                {
                    PG.Beam(g, q * new Vector3(-2.45f, 0.95f, 2.5f), q * new Vector3(2.45f, 0.95f, 2.5f), 0.22f, 0.05f, Pal.WoodDark);
                    for (int b = 0; b < 6; b++)
                    {
                        float x = -2.1f + b * 0.84f;
                        PG.Beam(g, q * new Vector3(x, 0.55f, 2.5f), q * new Vector3(x, 0.95f, 2.5f), 0.03f, 0.03f, Pal.Wood);
                    }
                }
            }
            PG.Roof(g, new Vector3(0f, 3.45f, 0f), 2.7f, 2.7f, 1.3f, 1.0f, 0.55f, tileA, tileB, Pal.TileUnder, C(62, 122, 98), false);
            PG.BoxMM(g, new Vector3(-1.4f, 4.2f, -1.4f), new Vector3(1.4f, 4.75f, 1.4f), Pal.Red);
            PG.Roof(g, new Vector3(0f, 4.75f, 0f), 1.5f, 1.5f, 1.4f, 0.7f, 0.45f, tileA, tileB, Pal.TileUnder, C(62, 122, 98), true);
            // stone table inside
            g.Frustum(Vector3.up * 0.5f, Vector3.up * 1.15f, 0.25f, 0.25f, 0.2f, 0.2f, 6, 0f, Pal.Stone, Pal.Stone, false, false);
            g.Frustum(Vector3.up * 1.15f, Vector3.up * 1.25f, 0.65f, 0.65f, 0.65f, 0.65f, 8, 0f, Pal.StoneLight, Pal.StoneLight, true, true);
            return m;
        }

        // ------------------------------------------------------------------ monastery
        /// <summary>Great hall with a golden double-eave roof on a stone platform (front = +Z). hx, hz = wall half sizes.</summary>
        public static PropModel GoldenHall(float hx, float hz, float wallH, bool doubleEave)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 wall = C(176, 52, 42), wallD = C(140, 40, 34), gold = C(224, 170, 58), gold2 = C(242, 198, 90), teal = C(56, 120, 124);
            float plat = 1.1f;
            PG.Box2(g, new Vector3(0f, plat * 0.5f - 0.6f, 0f), new Vector3(hx + 2.6f, plat * 0.5f + 0.6f, hz + 2.6f), Pal.StoneDark, Pal.StoneLight);
            // white marble balustrade around the platform
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                float len = (k & 1) == 0 ? hx + 2.5f : hz + 2.5f, off = (k & 1) == 0 ? hz + 2.5f : hx + 2.5f;
                int posts = Mathf.Max(3, Mathf.RoundToInt(len / 1.2f));
                for (int p = 0; p <= posts * 2; p++)
                {
                    float x = -len + p * len / posts;
                    if (k == 0 && Mathf.Abs(x) < 2.6f) continue;
                    PG.Beam(g, q * new Vector3(x, plat, off), q * new Vector3(x, plat + 0.75f, off), 0.07f, 0.07f, Pal.PlasterShade);
                }
                if (k != 0) PG.Beam(g, q * new Vector3(-len, plat + 0.6f, off), q * new Vector3(len, plat + 0.6f, off), 0.05f, 0.05f, Pal.Plaster);
            }
            // front steps
            for (int i = 0; i < 6; i++)
            {
                float y = plat * (6 - i) / 6f;
                PG.Box2(g, new Vector3(0f, (y - 0.6f) * 0.5f, hz + 2.6f + (i + 0.5f) * 0.36f), new Vector3(2.4f, (y + 0.6f) * 0.5f, 0.18f), Pal.Stone, Pal.StoneLight);
            }
            float b = plat, t = plat + wallH;
            // walls (back + sides), front of lattice doors under a colonnade
            PG.BoxMM(g, new Vector3(-hx, b, -hz), new Vector3(hx, t, -hz + 0.35f), wall);
            PG.BoxMM(g, new Vector3(-hx, b, -hz), new Vector3(-hx + 0.35f, t, hz - 0.8f), wall);
            PG.BoxMM(g, new Vector3(hx - 0.35f, b, -hz), new Vector3(hx, t, hz - 0.8f), wall);
            PG.BoxMM(g, new Vector3(-hx, b, hz - 1.0f), new Vector3(hx, b + 0.6f, hz - 0.8f), wallD);
            int bays = Mathf.Max(3, Mathf.RoundToInt(hx * 2f / 3.2f));
            for (int i = 0; i < bays; i++)
            {
                float x0 = -hx + i * hx * 2f / bays + 0.25f, x1 = -hx + (i + 1) * hx * 2f / bays - 0.25f;
                // lattice door panel (glows warm at night)
                PG.BoxMM(g, new Vector3(x0, b + 0.6f, hz - 1.0f), new Vector3(x1, t - 0.6f, hz - 0.9f), Pal.Lattice);
                gl.Box(new Vector3((x0 + x1) * 0.5f, (b + t) * 0.5f + 0.2f, hz - 0.86f), new Vector3((x1 - x0) * 0.42f, (t - b) * 0.3f, 0.02f), C(255, 206, 130));
                for (int l = 1; l < 4; l++)
                    PG.BoxMM(g, new Vector3(Mathf.Lerp(x0, x1, l / 4f) - 0.03f, b + 0.6f, hz - 0.85f), new Vector3(Mathf.Lerp(x0, x1, l / 4f) + 0.03f, t - 0.6f, hz - 0.8f), Pal.WoodDark);
            }
            PG.BoxMM(g, new Vector3(-hx, t - 0.6f, hz - 1.0f), new Vector3(hx, t, hz - 0.8f), wallD);
            // red colonnade + painted beams
            for (int i = 0; i <= bays; i++)
            {
                float x = -hx + i * hx * 2f / bays;
                PG.Column(g, new Vector3(x, b, hz + 0.6f), new Vector3(x, t, hz + 0.6f), 0.24f, Pal.Red, 8);
                g.Frustum(new Vector3(x, b, hz + 0.6f), new Vector3(x, b + 0.3f, hz + 0.6f), 0.36f, 0.36f, 0.3f, 0.3f, 8, 0f, Pal.Stone, Pal.StoneLight, false, true);
            }
            PG.Beam(g, new Vector3(-hx - 0.4f, t - 0.25f, hz + 0.6f), new Vector3(hx + 0.4f, t - 0.25f, hz + 0.6f), 0.2f, 0.25f, teal);
            PG.Beam(g, new Vector3(-hx - 0.4f, t - 0.65f, hz + 0.62f), new Vector3(hx + 0.4f, t - 0.65f, hz + 0.62f), 0.12f, 0.12f, Pal.Gold);
            // name plaque
            PG.BoxMM(g, new Vector3(-1.4f, t + 0.1f, hz + 0.82f), new Vector3(1.4f, t + 1.0f, hz + 0.9f), C(36, 56, 110));
            PG.BoxMM(g, new Vector3(-1.5f, t + 0.02f, hz + 0.8f), new Vector3(1.5f, t + 1.08f, hz + 0.82f), Pal.Gold);
            for (int i = 0; i < 3; i++) g.Box(new Vector3(-0.8f + i * 0.8f, t + 0.55f, hz + 0.92f), new Vector3(0.22f, 0.26f, 0.01f), Pal.Gold);
            if (doubleEave)
            {
                PG.Roof(g, new Vector3(0f, t, 0f), hx + 0.2f, hz + 0.9f, 1.6f, 1.8f, 0.7f, gold, gold2, Pal.TileUnder, gold2, false);
                float t2 = t + 1.1f;
                PG.BoxMM(g, new Vector3(-hx + 1.6f, t2, -hz + 1.4f), new Vector3(hx - 1.6f, t2 + 2.2f, hz - 1.0f), wall);
                for (int i = 0; i < bays; i++)
                {
                    float x = Mathf.Lerp(-hx + 2.4f, hx - 2.4f, (i + 0.5f) / bays);
                    gl.Box(new Vector3(x, t2 + 1.1f, hz - 0.98f), new Vector3(0.5f, 0.45f, 0.03f), C(255, 206, 130));
                }
                PG.Beam(g, new Vector3(-hx + 1.4f, t2 + 2.1f, hz - 0.9f), new Vector3(hx - 1.4f, t2 + 2.1f, hz - 0.9f), 0.14f, 0.16f, teal);
                PG.Roof(g, new Vector3(0f, t2 + 2.2f, 0.2f), hx - 1.2f, hz - 0.9f, 2.6f, 1.7f, 0.85f, gold, gold2, Pal.TileUnder, gold2, true);
            }
            else PG.Roof(g, new Vector3(0f, t, 0f), hx + 0.2f, hz + 0.9f, 2.2f, 1.6f, 0.75f, gold, gold2, Pal.TileUnder, gold2, true);
            // hanging lanterns at the front corners
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 h = new Vector3(s * (hx - 1.2f), t - 0.35f, hz + 1.4f);
                PG.Rod(gl, h, h - Up * 0.4f, 0.01f, A(Pal.Ink, 50), 3);
                PG.Lantern(gl, h - Up * 0.4f, 0.32f, 0.55f, Pal.RedBright, 110);
            }
            return m;
        }

        /// <summary>Stone paifang gate (three bays, golden tiled roofs) spanning width w (local X), front = +Z.</summary>
        public static PropModel Paifang(float w, float h)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 st = C(196, 190, 176), stD = C(150, 146, 136), gold = C(226, 172, 62), gold2 = C(244, 200, 92), teal = C(58, 122, 126);
            float[] xs = { -w * 0.5f, -w * 0.19f, w * 0.19f, w * 0.5f };
            foreach (float x in xs)
            {
                bool outer = Mathf.Abs(x) > w * 0.3f;
                float ph = outer ? h * 0.78f : h;
                PG.Box2(g, new Vector3(x, ph * 0.5f - 0.3f, 0f), new Vector3(0.38f, ph * 0.5f + 0.3f, 0.38f), stD, st);
                PG.Box2(g, new Vector3(x, 0.45f, 0f), new Vector3(0.62f, 0.55f, 0.95f), stD, st);   // drum-stone base
                g.Ellipsoid(new Vector3(x, 0.85f, 0.95f), new Vector3(0.35f, 0.35f, 0.15f), 7, 3, st, stD);
                g.Ellipsoid(new Vector3(x, 0.85f, -0.95f), new Vector3(0.35f, 0.35f, 0.15f), 7, 3, st, stD);
            }
            for (int i = 0; i < 3; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1];
                bool mid = i == 1;
                float bt = mid ? h : h * 0.78f;
                PG.BoxMM(g, new Vector3(x0, bt - 1.3f, -0.3f), new Vector3(x1, bt - 1.0f, 0.3f), teal);
                PG.BoxMM(g, new Vector3(x0, bt - 0.5f, -0.3f), new Vector3(x1, bt - 0.2f, 0.3f), teal);
                PG.BoxMM(g, new Vector3(x0 + 0.3f, bt - 1.0f, -0.22f), new Vector3(x1 - 0.3f, bt - 0.5f, 0.22f), mid ? C(36, 56, 110) : stD);
                if (mid) for (int k = 0; k < 3; k++) g.Box(new Vector3(-0.7f + k * 0.7f, bt - 0.75f, 0.235f), new Vector3(0.2f, 0.18f, 0.01f), Pal.Gold);
                PG.Roof(g, new Vector3((x0 + x1) * 0.5f, bt - 0.2f, 0f), (x1 - x0) * 0.5f + 0.2f, 0.45f, 0.9f, 0.7f, 0.4f, gold, gold2, Pal.TileUnder, gold2, mid);
                if (mid)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 hp = new Vector3(s * (x1 - x0) * 0.32f, bt - 1.35f, 0.4f);
                        PG.Rod(gl, hp, hp - Up * 0.25f, 0.01f, A(Pal.Ink, 50), 3);
                        PG.Lantern(gl, hp - Up * 0.25f, 0.3f, 0.5f, Pal.RedBright, 110);
                    }
            }
            return m;
        }

        /// <summary>Five-tier pagoda with golden eaves and a gilded finial (front = +Z), about 24 m tall.</summary>
        public static PropModel Pagoda(int tiers)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 wall = C(232, 226, 210), wallD = C(186, 60, 46), gold = C(226, 172, 62), gold2 = C(244, 200, 92);
            PG.Box2(g, new Vector3(0f, 0.3f, 0f), new Vector3(5.2f, 0.9f, 5.2f), Pal.StoneDark, Pal.StoneLight);
            float y = 1.2f, hw = 3.6f;
            for (int i = 0; i < tiers; i++)
            {
                float th = i == 0 ? 3.6f : 2.6f;
                PG.BoxMM(g, new Vector3(-hw, y, -hw), new Vector3(hw, y + th, hw), i == 0 ? wallD : wall);
                for (int k = 0; k < 4; k++)
                {
                    Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                    g.Box(q * new Vector3(0f, y + th * 0.5f, hw + 0.02f), new Vector3(hw * 0.28f, th * 0.3f, 0.02f), C(60, 44, 40));
                    gl.Box(q * new Vector3(0f, y + th * 0.5f, hw + 0.05f), new Vector3(hw * 0.16f, th * 0.18f, 0.01f), C(255, 206, 130));
                    for (int c = -1; c <= 1; c += 2) PG.Column(g, q * new Vector3(c * hw, y, hw), q * new Vector3(c * hw, y + th, hw), 0.14f, Pal.Red, 6);
                }
                y += th;
                PG.Roof(g, new Vector3(0f, y, 0f), hw + 0.1f, hw + 0.1f, 0.9f, 1.4f, 0.55f, gold, gold2, Pal.TileUnder, gold2, false);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 cnr = Quaternion.Euler(0f, 45f + k * 90f, 0f) * new Vector3(0f, 0f, (hw + 1.4f) * 1.38f);
                    PG.Rod(gl, new Vector3(cnr.x, y + 0.2f, cnr.z), new Vector3(cnr.x, y - 0.2f, cnr.z), 0.01f, A(Pal.Ink, 60), 3);
                    PG.Lantern(gl, new Vector3(cnr.x, y - 0.2f, cnr.z), 0.2f, 0.34f, Pal.RedBright, 120);
                }
                y += 0.55f;
                hw *= 0.86f;
            }
            // gilded spire (wheels + pearl)
            g.Frustum(Up * y, Up * (y + 1.2f), 1.1f, 1.1f, 0.4f, 0.4f, 8, 0f, gold, gold2, true, false);
            for (int i = 0; i < 5; i++)
                g.Frustum(Up * (y + 1.4f + i * 0.55f), Up * (y + 1.6f + i * 0.55f), 0.55f - i * 0.06f, 0.55f - i * 0.06f, 0.55f - i * 0.06f, 0.55f - i * 0.06f, 8, 0f, gold, gold2, true, true);
            PG.Rod(g, Up * y, Up * (y + 5.6f), 0.08f, gold, 5);
            g.Ellipsoid(Up * (y + 5.0f), new Vector3(0.38f, 0.45f, 0.38f), 7, 4, gold2, gold);
            return m;
        }

        /// <summary>Low monastery wall segment along local X (ochre plaster, grey tile coping), length 2*hl.</summary>
        public static void MonasteryWall(LifeGeo g, Vector3 a, Vector3 b, float h)
        {
            Vector3 d = b - a;
            float len = new Vector2(d.x, d.z).magnitude;
            if (len < 0.1f) return;
            Vector3 f = new Vector3(d.x, 0f, d.z) / len;
            Vector3 s = new Vector3(-f.z, 0f, f.x);
            Color32 wall = C(214, 168, 92), wallB = C(160, 120, 70), tile = Pal.Tile2;
            float th = 0.32f;
            Vector3 a0 = a - s * th, a1 = a + s * th, b0 = b - s * th, b1 = b + s * th;
            float by = Mathf.Min(a.y, b.y) - 0.8f;
            Vector3 la = new Vector3(0f, by - a.y, 0f), lb = new Vector3(0f, by - b.y, 0f);
            g.QuadOut(a0 + la, a0 + Up * h, b0 + Up * h, b0 + lb, -s, wallB, wall, wall, wallB);
            g.QuadOut(a1 + la, a1 + Up * h, b1 + Up * h, b1 + lb, s, wallB, wall, wall, wallB);
            // tile coping
            Vector3 o = s * (th + 0.22f);
            g.QuadOut(a - o + Up * (h - 0.05f), a + Up * (h + 0.35f), b + Up * (h + 0.35f), b - o + Up * (h - 0.05f), -s + Up, tile, Pal.Tile, Pal.Tile, tile);
            g.QuadOut(a + o + Up * (h - 0.05f), a + Up * (h + 0.35f), b + Up * (h + 0.35f), b + o + Up * (h - 0.05f), s + Up, tile, Pal.Tile, Pal.Tile, tile);
            PG.Beam(g, a + Up * (h + 0.38f), b + Up * (h + 0.38f), 0.06f, 0.06f, Pal.Tile);
        }

        // ------------------------------------------------------------------ cable car
        public static PropModel CableTower(float h)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 steel = C(196, 200, 206), steelD = C(140, 146, 154), red = C(196, 56, 44);
            PG.Box2(g, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.5f, 1.4f), Pal.StoneDark, Pal.Stone);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1f : 1f, z = (i & 2) == 0 ? -0.8f : 0.8f;
                PG.Beam(g, new Vector3(x, 0.4f, z), new Vector3(x * 0.35f, h, z * 0.5f), 0.1f, 0.1f, steel);
            }
            for (int k = 1; k < 6; k++)
            {
                float y = h * k / 6f, sx = Mathf.Lerp(1f, 0.35f, k / 6f), sz = Mathf.Lerp(0.8f, 0.4f, k / 6f);
                for (int s = -1; s <= 1; s += 2)
                {
                    PG.Beam(g, new Vector3(-sx, y, s * sz), new Vector3(sx, y, s * sz), 0.05f, 0.05f, steelD);
                    PG.Beam(g, new Vector3(s * sx, y, -sz), new Vector3(s * sx, y, sz), 0.05f, 0.05f, steelD);
                }
            }
            PG.BoxMM(g, new Vector3(-3.4f, h, -0.35f), new Vector3(3.4f, h + 0.5f, 0.35f), red);
            for (int s = -1; s <= 1; s += 2)
                g.Frustum(new Vector3(s * 3f, h + 0.5f, -0.6f), new Vector3(s * 3f, h + 0.5f, 0.6f), 0.35f, 0.35f, 0.35f, 0.35f, 8, 0f, steelD, steelD, true, true);
            return m;
        }

        public static PropModel CableStation(bool upper)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            PG.Box2(g, new Vector3(0f, 0.2f, 0f), new Vector3(8.4f, 0.8f, 6.4f), Pal.StoneDark, Pal.StoneLight);
            PG.BoxMM(g, new Vector3(-7.5f, 1f, -5.5f), new Vector3(7.5f, 5.4f, 5.5f), Pal.Plaster);
            PG.BoxMM(g, new Vector3(-7.6f, 1f, -5.6f), new Vector3(7.6f, 1.8f, 5.6f), Pal.StoneDark);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.BoxMM(g, new Vector3(-3.4f, 1.2f, s * 5.52f), new Vector3(3.4f, 4.6f, s * 5.62f), C(60, 70, 84));
                gl.Box(new Vector3(0f, 3.0f, s * 5.66f), new Vector3(3.0f, 1.4f, 0.02f), C(255, 214, 150));
            }
            PG.Roof(g, new Vector3(0f, 5.4f, 0f), 7.6f, 5.6f, 2.0f, 1.2f, 0.6f, Pal.Tile, Pal.Tile2, Pal.TileUnder, C(62, 122, 98), false);
            // bull-wheel housing towards local +Z (where the cable leaves)
            PG.BoxMM(g, new Vector3(-3.6f, 4.0f, 5.6f), new Vector3(3.6f, 5.2f, 8.5f), C(196, 56, 44));
            return m;
        }

        public static LifeGeo Cabin(Color32 body)
        {
            var g = new LifeGeo();
            Color32 glass = C(70, 96, 120), roof = C(236, 236, 230), steel = C(170, 176, 184);
            PG.Rod(g, Vector3.zero, -Up * 1.6f, 0.05f, steel, 4);
            g.Frustum(Vector3.zero, new Vector3(0f, 0.15f, 0f), 0.22f, 0.22f, 0.22f, 0.22f, 6, 0f, steel, steel, true, true);
            PG.Box2(g, new Vector3(0f, -2.75f, 0f), new Vector3(0.95f, 1.15f, 1.15f), body, roof);
            g.Box(new Vector3(0f, -2.45f, 1.16f), new Vector3(0.8f, 0.55f, 0.02f), glass);
            g.Box(new Vector3(0f, -2.45f, -1.16f), new Vector3(0.8f, 0.55f, 0.02f), glass);
            g.Box(new Vector3(0.96f, -2.45f, 0f), new Vector3(0.02f, 0.55f, 0.95f), glass);
            g.Box(new Vector3(-0.96f, -2.45f, 0f), new Vector3(0.02f, 0.55f, 0.95f), glass);
            PG.Box2(g, new Vector3(0f, -1.55f, 0f), new Vector3(0.7f, 0.08f, 0.85f), roof, roof);
            return g;
        }

        // ------------------------------------------------------------------ eagle (body + two wings, 4 m span)
        public static LifeGeo EagleBody()
        {
            var g = new LifeGeo();
            Color32 back = C(84, 62, 46), belly = C(120, 92, 66), head = C(236, 230, 214), beak = C(236, 190, 60);
            g.Frustum(new Vector3(0f, 0f, -0.55f), new Vector3(0f, 0.02f, 0.35f), 0.12f, 0.1f, 0.2f, 0.17f, 6, 0f, back, belly, true, false);
            g.Ellipsoid(new Vector3(0f, 0.05f, 0.45f), new Vector3(0.12f, 0.11f, 0.14f), 6, 3, head, head);
            g.Frustum(new Vector3(0f, 0.03f, 0.56f), new Vector3(0f, 0f, 0.7f), 0.04f, 0.04f, 0.01f, 0.01f, 4, 0f, beak, beak, true, false);
            Vector3 t0 = new Vector3(-0.18f, 0f, -0.5f), t1 = new Vector3(0.18f, 0f, -0.5f), t2 = new Vector3(0.24f, 0f, -0.95f), t3 = new Vector3(-0.24f, 0f, -0.95f);
            g.Tri2(t0, t1, t2, back, back, back);
            g.Tri2(t0, t2, t3, back, back, back);
            return g;
        }

        public static LifeGeo EagleWing(int side)
        {
            var g = new LifeGeo();
            Color32 inner = C(96, 70, 50), outer = C(58, 44, 36), tip = C(40, 32, 28);
            float s = side;
            Vector3 rf = new Vector3(0f, 0f, 0.2f), rb = new Vector3(0f, 0f, -0.25f);
            Vector3 mf = new Vector3(0.9f * s, 0.06f, 0.18f), mb = new Vector3(0.85f * s, 0.04f, -0.32f);
            Vector3 tf = new Vector3(1.9f * s, 0.12f, 0.05f), tb = new Vector3(1.75f * s, 0.1f, -0.38f);
            g.Tri2(rf, mf, mb, inner, inner, inner);
            g.Tri2(rf, mb, rb, inner, inner, inner);
            g.Tri2(mf, tf, tb, outer, tip, tip);
            g.Tri2(mf, tb, mb, outer, tip, outer);
            // fingered primaries
            for (int k = 0; k < 3; k++)
            {
                Vector3 a = Vector3.Lerp(tf, tb, k / 3f), b = Vector3.Lerp(tf, tb, (k + 0.6f) / 3f);
                g.Tri2(a, b, a + new Vector3(0.3f * s, 0.02f, -0.05f), tip, tip, tip);
            }
            return g;
        }
    }
}
