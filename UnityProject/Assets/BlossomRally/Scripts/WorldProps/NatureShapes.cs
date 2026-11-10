// NatureShapes.cs - low-poly painterly trees, rocks and flowers for WorldProps: weeping willows,
// Huangshan-style pines, plum / peach blossom trees, karst pillars, wisteria arches and big pond lotuses.
// Vertex colours sRGB; alpha = wind weight (willow strands / racemes go into the strong-wind kind).
using UnityEngine;

namespace BlossomRally
{
    internal static class NatureShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 A(Color32 c, int a) { return PG.A(c, a); }
        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        static void Limb(LifeGeo g, Vector3 a, Vector3 b, float r0, float r1, Color32 c0, Color32 c1)
        {
            g.Frustum(a, b, r0, r0, r1, r1, 6, 0.4f, c0, c1, false, true);
        }

        // ------------------------------------------------------------------ weeping willow
        public static PropModel Willow(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            Color32 bark = C(92, 74, 58), barkL = C(118, 96, 74);
            Vector3 lean = new Vector3(R(rng, -0.5f, 0.5f), 0f, R(rng, -0.5f, 0.5f));
            Vector3 p0 = Vector3.zero - Up * 0.3f, p1 = Up * 1.6f + lean * 0.4f, p2 = Up * 3.2f + lean, p3 = Up * 4.4f + lean * 1.2f;
            Limb(g, p0, p1, 0.42f, 0.34f, bark, barkL);
            Limb(g, p1, p2, 0.34f, 0.26f, barkL, bark);
            Limb(g, p2, p3, 0.26f, 0.18f, bark, barkL);
            Vector3 crown = p3 + Up * 0.8f;
            int limbs = 5;
            var tips = new Vector3[limbs];
            for (int i = 0; i < limbs; i++)
            {
                float a = i * Mathf.PI * 2f / limbs + R(rng, -0.3f, 0.3f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 from = Vector3.Lerp(p2, p3, R(rng, 0.2f, 0.9f));
                tips[i] = from + d * R(rng, 1.8f, 2.6f) + Up * R(rng, 1.0f, 1.8f);
                Limb(g, from, tips[i], 0.15f, 0.06f, bark, barkL);
            }
            Color32 lt = C(160, 196, 98, 30), dk = C(118, 160, 74, 20);
            for (int i = 0; i < 6; i++)
            {
                Vector3 c = i < limbs ? tips[i] + Up * 0.1f : crown + Up * 0.4f;
                int s = g.v.Count;
                g.Ellipsoid(c, new Vector3(R(rng, 1.2f, 1.7f), R(rng, 0.6f, 0.9f), R(rng, 1.2f, 1.7f)), 7, 4, lt, dk);
                PG.Lumpy(g, s, c, 0.18f, i * 3.1f);
            }
            // long hanging strands
            int strands = 70;
            Color32 sTop = C(132, 172, 80), sTip = C(186, 214, 112);
            for (int i = 0; i < strands; i++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float rad = R(rng, 1.4f, 3.4f);
                Vector3 top = crown + lean * 0.2f + d * rad + Up * R(rng, -0.9f, 0.3f);
                float len = Mathf.Min(top.y - 0.55f, R(rng, 2.6f, 4.6f));
                if (len < 1f) continue;
                Vector3 side = new Vector3(-d.z, 0f, d.x) * R(rng, 0.09f, 0.14f);
                Vector3 bow = d * R(rng, 0.2f, 0.5f);
                const int NSEG = 4;
                for (int k = 0; k < NSEG; k++)
                {
                    float t0 = k / (float)NSEG, t1 = (k + 1) / (float)NSEG;
                    Vector3 q0 = top - Up * len * t0 + bow * Mathf.Sin(t0 * Mathf.PI * 0.6f);
                    Vector3 q1 = top - Up * len * t1 + bow * Mathf.Sin(t1 * Mathf.PI * 0.6f);
                    float w0 = 1f - t0 * 0.6f, w1 = 1f - t1 * 0.6f;
                    Color32 c0 = A(Color32.Lerp(sTop, sTip, t0), (int)(255f * t0 * t0)), c1 = A(Color32.Lerp(sTop, sTip, t1), (int)(255f * t1 * t1));
                    PG.Quad2(st, q0 - side * w0, q0 + side * w0, q1 + side * w1, q1 - side * w1, c0, c0, c1, c1);
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ Huangshan pine on a rock
        public static PropModel Pine(System.Random rng, float scale)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 bark = C(112, 80, 62), barkD = C(84, 60, 48);
            Color32 rock = C(156, 154, 148), rockD = C(116, 116, 112);
            int rocks = 2 + rng.Next(3);
            for (int i = 0; i < rocks; i++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f);
                Vector3 c = new Vector3(Mathf.Cos(a) * R(rng, 0.4f, 1.3f), R(rng, 0.0f, 0.3f), Mathf.Sin(a) * R(rng, 0.4f, 1.3f));
                PG.Boulder(g, c, new Vector3(R(rng, 0.7f, 1.3f), R(rng, 0.5f, 0.9f), R(rng, 0.6f, 1.2f)) * scale, rock, rockD, R(rng, 0f, 40f));
            }
            float H = R(rng, 5.5f, 8f) * scale;
            Vector3 lean = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized * R(rng, 0.6f, 1.6f) * scale;
            var pts = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                pts[i] = Up * (H * t - 0.3f) + lean * Mathf.Sin(t * 2.2f) + new Vector3(R(rng, -0.25f, 0.25f), 0f, R(rng, -0.25f, 0.25f)) * (i > 0 ? 1f : 0f);
            }
            for (int i = 0; i < 4; i++) Limb(g, pts[i], pts[i + 1], (0.36f - i * 0.07f) * scale, (0.29f - i * 0.07f) * scale, i % 2 == 0 ? barkD : bark, bark);
            Color32 top = C(64, 112, 78, 30), bottom = C(36, 64, 50, 20), rim = C(92, 136, 86, 30);
            int pads = 3 + rng.Next(3);
            for (int i = 0; i < pads; i++)
            {
                float t = Mathf.Lerp(0.45f, 0.92f, pads == 1 ? 1f : i / (float)(pads - 1));
                Vector3 from = Vector3.Lerp(pts[2], pts[4], (t - 0.45f) / 0.47f);
                float a = R(rng, 0f, Mathf.PI * 2f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = R(rng, 1.2f, 2.8f) * scale * (1.1f - t * 0.6f);
                Vector3 end = from + d * reach + Up * R(rng, -0.2f, 0.5f);
                Limb(g, from, end, 0.12f * scale, 0.06f * scale, bark, barkD);
                Vector3 pr = new Vector3(R(rng, 1.3f, 2.1f), R(rng, 0.3f, 0.42f), R(rng, 1.0f, 1.5f)) * scale * (1.15f - t * 0.4f);
                int s = g.v.Count;
                g.Ellipsoid(end + Up * 0.15f, pr, 8, 3, top, bottom);
                PG.Lumpy(g, s, end + Up * 0.15f, 0.12f, i * 7f);
                int s2 = g.v.Count;
                g.Ellipsoid(end + Up * (0.15f + pr.y * 0.55f), new Vector3(pr.x * 0.7f, pr.y * 0.5f, pr.z * 0.7f), 7, 3, rim, top);
                PG.Lumpy(g, s2, end + Up * 0.3f, 0.12f, i * 5f);
            }
            int st = g.v.Count;
            Vector3 cap = pts[4] + Up * 0.2f;
            g.Ellipsoid(cap, new Vector3(1.6f, 0.4f, 1.3f) * scale, 8, 3, rim, bottom);
            PG.Lumpy(g, st, cap, 0.12f, 99f);
            return m;
        }

        // ------------------------------------------------------------------ plum (white) / peach (deep pink) blossom
        public static PropModel BlossomTree(bool peach, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 bark = peach ? C(92, 64, 56) : C(64, 50, 48), barkL = Pal.WoodDark;
            Color32 bTop, bBot, bAlt;
            if (peach) { bTop = C(246, 132, 176, 60); bBot = C(214, 76, 130, 40); bAlt = C(250, 166, 198, 60); }
            else { bTop = C(252, 250, 246, 60); bBot = C(232, 218, 224, 40); bAlt = C(246, 226, 234, 60); }
            Vector3 lean = new Vector3(R(rng, -0.4f, 0.4f), 0f, R(rng, -0.4f, 0.4f));
            Vector3 t0 = -Up * 0.2f, t1 = Up * 1.3f + lean * 0.5f, t2 = Up * 2.2f + lean;
            Limb(g, t0, t1, 0.24f, 0.18f, bark, barkL);
            Limb(g, t1, t2, 0.18f, 0.12f, barkL, bark);
            int branches = 5 + rng.Next(2);
            for (int i = 0; i < branches; i++)
            {
                float a = i * Mathf.PI * 2f / branches + R(rng, -0.4f, 0.4f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 from = Vector3.Lerp(t1, t2, R(rng, 0.1f, 1f));
                Vector3 mid = from + d * R(rng, 0.7f, 1.1f) + Up * R(rng, 0.5f, 0.9f);
                Vector3 end = mid + d * R(rng, 0.5f, 1.0f) + Up * R(rng, 0.2f, 0.8f) + new Vector3(R(rng, -0.3f, 0.3f), 0f, R(rng, -0.3f, 0.3f));
                Limb(g, from, mid, 0.09f, 0.06f, bark, barkL);
                Limb(g, mid, end, 0.06f, 0.03f, barkL, bark);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 c = k == 0 ? end : Vector3.Lerp(mid, end, 0.3f) + Up * 0.2f;
                    float r = R(rng, 0.55f, 0.9f) * (k == 0 ? 1f : 0.75f);
                    bool alt = rng.NextDouble() < 0.35;
                    int s = g.v.Count;
                    g.Ellipsoid(c, new Vector3(r, r * 0.7f, r), 6, 4, alt ? bAlt : bTop, bBot);
                    PG.Lumpy(g, s, c, 0.22f, i * 4.3f + k);
                }
                if (peach && rng.NextDouble() < 0.5)
                    g.Ellipsoid(mid + Up * 0.15f, new Vector3(0.35f, 0.22f, 0.35f), 5, 3, C(134, 180, 84, 50), C(100, 150, 70, 40));
            }
            int st = g.v.Count;
            g.Ellipsoid(t2 + Up * 0.5f, new Vector3(0.9f, 0.6f, 0.9f), 6, 4, bTop, bBot);
            PG.Lumpy(g, st, t2 + Up * 0.5f, 0.2f, 17f);
            // a sprinkle of fallen petals at the foot
            Color32 pet = peach ? C(240, 130, 172) : C(250, 244, 244);
            for (int i = 0; i < 26; i++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f), r = R(rng, 0.4f, 2.4f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0.04f, Mathf.Sin(a) * r);
                float rot = R(rng, 0f, 6.28f);
                Vector3 u = new Vector3(Mathf.Cos(rot), 0f, Mathf.Sin(rot)) * 0.06f, w = new Vector3(-u.z, 0f, u.x);
                g.TriOut(p - u, p + w, p + u, Vector3.up, pet, pet, pet);
            }
            return m;
        }

        // ------------------------------------------------------------------ karst limestone pillar (world space)
        public static void Karst(LifeGeo g, Vector3 basePos, float H, float Rb, System.Random rng)
        {
            const int NR = 9, NSIDE = 9;
            var ring = new Vector3[NR, NSIDE];
            Vector3 leanDir = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized;
            float lean = R(rng, 0.03f, 0.12f) * H;
            for (int i = 0; i < NR; i++)
            {
                float t = i / (float)(NR - 1);
                float r = Rb * (1f - 0.55f * t + 0.22f * Mathf.Sin(Mathf.PI * t)) * R(rng, 0.88f, 1.08f);
                if (i == NR - 1) r *= 0.75f;
                Vector3 c = basePos + Up * (H * t) + leanDir * (lean * t * t) + new Vector3(R(rng, -0.04f, 0.04f), 0f, R(rng, -0.04f, 0.04f)) * Rb;
                float rot = R(rng, 0f, 0.4f);
                for (int k = 0; k < NSIDE; k++)
                {
                    float a = rot + k * Mathf.PI * 2f / NSIDE;
                    float f = R(rng, 0.8f, 1.15f);
                    ring[i, k] = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * f + Up * R(rng, -0.04f, 0.04f) * H / NR;
                }
            }
            Color32 rockA = C(184, 184, 172), rockB = C(156, 158, 150), rockC = C(132, 134, 130);
            Color32 green = C(92, 132, 84), greenL = C(118, 156, 96);
            int ledge = 2 + rng.Next(NR - 4);
            for (int i = 0; i < NR - 1; i++)
                for (int k = 0; k < NSIDE; k++)
                {
                    int k1 = (k + 1) % NSIDE;
                    Vector3 a = ring[i, k], b = ring[i, k1], d = ring[i + 1, k1], e = ring[i + 1, k];
                    Vector3 nrm = Vector3.Cross(d - a, b - a).normalized;
                    Vector3 mid = (a + b + d + e) * 0.25f;
                    Vector3 outward = mid - (basePos + Up * mid.y);
                    if (Vector3.Dot(nrm, outward) < 0f) nrm = -nrm;
                    Color32 col = ((k * 7 + i * 3) % 3 == 0) ? rockA : (((k * 5 + i) % 3 == 1) ? rockB : rockC);
                    if (nrm.y > 0.35f || i >= NR - 2 || (i == ledge && (k & 1) == 0)) col = ((k + i) & 1) == 0 ? green : greenL;
                    g.QuadOut(a, b, d, e, outward, col, col, col, col);
                }
            Vector3 topC = Vector3.zero;
            for (int k = 0; k < NSIDE; k++) topC += ring[NR - 1, k];
            topC /= NSIDE;
            Vector3 apex = topC + Up * Rb * 0.35f;
            for (int k = 0; k < NSIDE; k++)
                g.TriOut(ring[NR - 1, k], ring[NR - 1, (k + 1) % NSIDE], apex, Up + (ring[NR - 1, k] - topC).normalized * 0.3f, green, green, greenL);
            int shrubs = 3 + rng.Next(3);
            for (int i = 0; i < shrubs; i++)
            {
                int k = rng.Next(NSIDE);
                Vector3 p = Vector3.Lerp(ring[NR - 1, k], apex, R(rng, 0f, 0.6f));
                int s = g.v.Count;
                g.Ellipsoid(p + Up * Rb * 0.08f, new Vector3(Rb * 0.18f, Rb * 0.14f, Rb * 0.18f), 6, 3, C(70, 110, 72), C(46, 80, 56));
                PG.Lumpy(g, s, p, 0.2f, i);
            }
        }

        // ------------------------------------------------------------------ wisteria pergola (tunnel along Z)
        public static PropModel WisteriaArch(System.Random rng, float halfLen)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            Color32 wood = C(110, 80, 58);
            float hw = 1.4f, ph = 2.5f, peak = 3.1f;
            int bays = Mathf.Max(2, Mathf.RoundToInt(halfLen));
            for (int i = 0; i <= bays; i++)
            {
                float z = -halfLen + i * 2f * halfLen / bays;
                for (int s = -1; s <= 1; s += 2) PG.Beam(g, new Vector3(s * hw, -0.3f, z), new Vector3(s * hw, ph, z), 0.07f, 0.07f, wood);
                for (int k = 0; k < 6; k++)
                {
                    Vector3 a = ArcPt(k / 6f, hw, ph, peak, z), b = ArcPt((k + 1) / 6f, hw, ph, peak, z);
                    PG.Beam(g, a, b, 0.05f, 0.06f, wood);
                }
            }
            for (int k = 0; k <= 4; k++)
            {
                float u = k / 4f;
                PG.Beam(g, ArcPt(u, hw, ph, peak, -halfLen - 0.2f), ArcPt(u, hw, ph, peak, halfLen + 0.2f), 0.04f, 0.04f, wood);
            }
            Color32 leaf = C(104, 148, 76, 40), leafD = C(76, 118, 60, 30);
            int lumps = bays * 5;
            for (int i = 0; i < lumps; i++)
            {
                Vector3 c = ArcPt(R(rng, 0.05f, 0.95f), hw, ph, peak, R(rng, -halfLen, halfLen)) + Up * 0.15f;
                int s = g.v.Count;
                g.Ellipsoid(c, new Vector3(R(rng, 0.5f, 0.75f), 0.28f, R(rng, 0.5f, 0.8f)), 6, 3, leaf, leafD);
                PG.Lumpy(g, s, c, 0.2f, i);
            }
            Color32 lav = C(208, 182, 238), deep = C(140, 96, 200), white = C(240, 232, 248);
            int racemes = bays * 16;
            for (int i = 0; i < racemes; i++)
            {
                float u = R(rng, 0f, 1f);
                if (u > 0.3f && u < 0.7f && rng.NextDouble() < 0.5) u = rng.NextDouble() < 0.5 ? R(rng, 0f, 0.3f) : R(rng, 0.7f, 1f);
                Vector3 top = ArcPt(u, hw, ph, peak, R(rng, -halfLen, halfLen)) - Up * 0.05f;
                float len = R(rng, 0.45f, 0.95f);
                Vector3 tip = top - Up * len + new Vector3(R(rng, -0.08f, 0.08f), 0f, R(rng, -0.08f, 0.08f));
                Color32 c0 = rng.NextDouble() < 0.15 ? white : lav;
                st.Frustum(top, tip, 0.13f, 0.13f, 0.025f, 0.025f, 5, R(rng, 0f, 1f), A(c0, 30), A(deep, 220), true, false);
            }
            return m;
        }

        static Vector3 ArcPt(float u, float hw, float ph, float peak, float z)
        {
            float x = Mathf.Lerp(-hw, hw, u);
            return new Vector3(x, ph + (peak - ph) * Mathf.Sin(u * Mathf.PI), z);
        }

        // ------------------------------------------------------------------ big pond lotus
        public static void LotusFlower(LifeGeo g, Vector3 at, float size, float rot, bool bud)
        {
            Color32 stem = C(96, 140, 70, 40);
            Vector3 head = at + Up * (0.45f * size);
            PG.Rod(g, at - Up * 0.3f, head, 0.025f * size, stem, 4);
            Color32 baseC = C(252, 236, 242, 50), tipC = C(234, 98, 154, 50);
            if (bud)
            {
                g.Ellipsoid(head + Up * 0.18f * size, new Vector3(0.11f, 0.2f, 0.11f) * size, 6, 4, tipC, baseC);
                return;
            }
            for (int ringI = 0; ringI < 2; ringI++)
            {
                int n = ringI == 0 ? 8 : 7;
                float tilt = ringI == 0 ? 0.55f : 0.95f, len = (ringI == 0 ? 0.36f : 0.3f) * size;
                for (int k = 0; k < n; k++)
                {
                    float a = rot + k * Mathf.PI * 2f / n + ringI * 0.22f;
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 dir = (d * Mathf.Cos(tilt) + Up * Mathf.Sin(tilt)).normalized;
                    Vector3 tip = head + dir * len;
                    PG.Petal(g, head + d * 0.03f * size, tip, new Vector3(-d.z, 0f, d.x), 0.1f * size, baseC, tipC);
                }
            }
            g.Frustum(head, head + Up * 0.08f * size, 0.06f * size, 0.06f * size, 0.08f * size, 0.08f * size, 7, 0f, C(220, 200, 80, 50), C(236, 220, 90, 50), false, true);
        }

        public static void LilyPad(LifeGeo g, Vector3 c, float r, float rot)
        {
            Color32 cc = C(96, 150, 78, 25), ce = C(70, 124, 62, 25);
            const int N = 10;
            for (int k = 0; k < N; k++)
            {
                if (k == 0) continue;   // the notch
                float a0 = rot + k * Mathf.PI * 2f / N, a1 = rot + (k + 1) * Mathf.PI * 2f / N;
                g.TriOut(c, c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r, Vector3.up, cc, ce, ce);
            }
        }
    }
}
