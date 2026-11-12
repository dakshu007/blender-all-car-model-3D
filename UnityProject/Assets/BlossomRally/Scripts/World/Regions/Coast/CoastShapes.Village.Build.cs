// CoastShapes.Village.Build.cs - Lotus Coast models, part 2: the stilt fishing village and the headland.
// Stilt houses and piers, junk boats with battened sails and sampans with rattan canopies (LifeBatch
// templates: they bob in Tick), drying nets and fish racks, arcaded "qilou" shophouses in pastel stucco,
// seafood stalls with tanks, promenade lamps, the lighthouse and its keeper's house, a lotus pavilion and
// a five-storey pagoda. Local space: y up, origin on the ground (or the waterline for boats), front = +Z.
using UnityEngine;

namespace BlossomRally
{
    internal static partial class CoastShapes
    {
        public static readonly Color32 PlankA = C(150, 118, 86), PlankB = C(124, 96, 72), PlankGrey = C(146, 140, 128), Pile = C(92, 78, 64);
        public static readonly Color32 TileGrey = C(86, 92, 102), TileGrey2 = C(104, 110, 120), RidgeGrey = C(66, 70, 78);

        // ------------------------------------------------------------------ stilt house (origin = water level, deck at deckY)
        public static PropModel StiltHouse(System.Random rng, float deckY, float pileDepth)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            var st = m[PK.Strong];
            float hx = R(rng, 2.4f, 3.2f), hz = R(rng, 2.6f, 3.4f), wallH = R(rng, 2.5f, 2.9f);
            float porch = 1.8f;
            Color32[] walls = { C(150, 112, 80), C(120, 140, 150), C(176, 150, 112), C(138, 116, 98), C(110, 132, 120) };
            Color32 wall = walls[rng.Next(walls.Length)], wallD = Sh(wall, 0.82f);
            // piles
            for (int ix = -1; ix <= 1; ix++)
                for (int iz = -1; iz <= 2; iz++)
                {
                    float z = iz < 2 ? iz * hz : hz + porch;
                    Vector3 b = new Vector3(ix * hx + R(rng, -0.1f, 0.1f), -pileDepth, z);
                    g.Frustum(b, new Vector3(ix * hx, deckY, z), 0.15f, 0.15f, 0.13f, 0.13f, 5, 0f, Sh(Pile, 0.7f), Pile, false, false);
                }
            for (int ix = -1; ix <= 1; ix += 2)
                PG.Beam(g, new Vector3(ix * hx, 0.2f, -hz), new Vector3(ix * hx, deckY - 0.3f, hz), 0.04f, 0.04f, Pile);
            // deck
            PG.Box2(g, new Vector3(0f, deckY - 0.08f, porch * 0.5f), new Vector3(hx + 0.25f, 0.09f, hz + porch * 0.5f + 0.25f), Sh(PlankB, 0.8f), PlankA);
            // walls: plank stripes
            float y0 = deckY, y1 = deckY + wallH;
            PG.Box2(g, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(hx - 0.1f, wallH * 0.5f, hz - 0.1f), wall, wall);
            for (int i = 0; i < 5; i++)
            {
                float y = y0 + 0.3f + i * wallH / 5f;
                PG.BoxMM(g, new Vector3(-hx + 0.05f, y, -hz + 0.05f), new Vector3(hx - 0.05f, y + 0.05f, hz - 0.05f), wallD);
            }
            // door + windows (glow at night)
            PG.BoxMM(g, new Vector3(-0.5f, y0, hz - 0.1f), new Vector3(0.5f, y0 + 2f, hz - 0.04f), Pal.WoodDark);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.BoxMM(gl, new Vector3(s * 1.0f - 0.45f, y0 + 1.0f, hz - 0.09f), new Vector3(s * 1.0f + 0.45f, y0 + 1.8f, hz - 0.03f), Pal.WindowWarm);
                PG.BoxMM(gl, new Vector3(s * (hx - 0.09f), y0 + 1.0f, -0.6f), new Vector3(s * (hx - 0.03f), y0 + 1.8f, 0.6f), Pal.WindowWarm);
                PG.BoxMM(g, new Vector3(s * 1.0f - 0.5f, y0 + 0.95f, hz - 0.03f), new Vector3(s * 1.0f + 0.5f, y0 + 1.0f, hz + 0.02f), Pal.WoodDark);
                PG.Lantern(gl, new Vector3(s * 0.8f, y1 - 0.15f, hz + 0.25f), 0.2f, 0.34f, Pal.RedBright, 90);
            }
            // porch railing
            float pz = hz + porch + 0.2f;
            for (int i = 0; i <= 8; i++)
            {
                float x = -hx - 0.2f + i * (2f * hx + 0.4f) / 8f;
                PG.Rod(g, new Vector3(x, deckY, pz), new Vector3(x, deckY + 0.9f, pz), 0.035f, Pal.WoodDark, 4);
            }
            PG.Beam(g, new Vector3(-hx - 0.2f, deckY + 0.9f, pz), new Vector3(hx + 0.2f, deckY + 0.9f, pz), 0.05f, 0.04f, Pal.Wood);
            // roof: grey tiled Chinese hip roof
            PG.Roof(g, new Vector3(0f, y1, 0.15f), hx + 0.05f, hz + 0.2f, R(rng, 1.3f, 1.7f), 0.7f, 0.3f, TileGrey, TileGrey2, Pal.TileUnder, RidgeGrey, false);
            // drying fish / laundry line across the porch
            Vector3 la = new Vector3(-hx, y1 - 0.4f, hz + porch), lb = new Vector3(hx, y1 - 0.6f, hz + porch);
            PG.Rod(g, la, lb, 0.01f, Pal.Ink, 3);
            int items = 5 + rng.Next(4);
            bool fish = rng.NextDouble() < 0.5;
            for (int i = 0; i < items; i++)
            {
                Vector3 p = Vector3.Lerp(la, lb, (i + 0.5f) / items);
                if (fish)
                {
                    Color32 fc = A(C(196, 168, 120), 160);
                    st.Tri2(p, p + new Vector3(0.12f, -0.35f, 0f), p + new Vector3(-0.12f, -0.35f, 0f), A(fc, 60), fc, fc);
                    st.Tri2(p + new Vector3(0.12f, -0.35f, 0f), p + new Vector3(0f, -0.55f, 0f), p + new Vector3(-0.12f, -0.35f, 0f), fc, A(fc, 220), fc);
                }
                else
                {
                    Color32 cc = Cloth[rng.Next(Cloth.Length)];
                    PG.Quad2(st, p + new Vector3(-0.22f, 0f, 0f), p + new Vector3(0.22f, 0f, 0f), p + new Vector3(0.2f, -0.6f, 0f), p + new Vector3(-0.2f, -0.6f, 0f),
                             A(cc, 40), A(cc, 40), A(cc, 230), A(cc, 230));
                }
            }
            // a couple of baskets and a fish trap on the porch
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = new Vector3(R(rng, -hx + 0.5f, hx - 0.5f), deckY, hz + R(rng, 0.5f, porch - 0.3f));
                g.Frustum(p, p + Up * 0.45f, 0.26f, 0.26f, 0.32f, 0.32f, 7, 0f, BambooDark, Bamboo, true, false);
            }
            return m;
        }

        // ------------------------------------------------------------------ pier along +Z (origin = water level at the root)
        public static PropModel Pier(float len, float deckY, float pileDepth, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            float hw = 1.4f;
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 1.2f));
            for (int i = 0; i < n; i++)
            {
                float z0 = i * len / n, z1 = (i + 1) * len / n - 0.05f;
                Color32 c = i % 3 == 0 ? PlankB : (i % 3 == 1 ? PlankA : PlankGrey);
                PG.Box2(g, new Vector3(0f, deckY - 0.06f, (z0 + z1) * 0.5f), new Vector3(hw, 0.07f, (z1 - z0) * 0.5f), Sh(c, 0.8f), c);
            }
            for (float z = 0.4f; z <= len; z += 2.6f)
                for (int s = -1; s <= 1; s += 2)
                {
                    g.Frustum(new Vector3(s * (hw - 0.1f), -pileDepth, z), new Vector3(s * (hw - 0.1f), deckY + 0.35f, z), 0.14f, 0.14f, 0.12f, 0.12f, 5, 0f, Sh(Pile, 0.7f), Pile, false, true);
                }
            for (float z = 3f; z < len; z += 7f)
                g.Frustum(new Vector3(hw - 0.3f, deckY, z), new Vector3(hw - 0.3f, deckY + 0.45f, z), 0.13f, 0.13f, 0.16f, 0.16f, 6, 0f, Pal.Ink, Pal.Black, false, true);
            // lamp post at the end
            Vector3 lp = new Vector3(-hw + 0.25f, deckY, len - 0.6f);
            PG.Column(g, lp, lp + Up * 3f, 0.06f, Pal.Ink, 5);
            PG.Lantern(gl, lp + Up * 3f + new Vector3(0.3f, 0f, 0f), 0.2f, 0.34f, Pal.RedBright, 80);
            PG.Rod(g, lp + Up * 2.95f, lp + Up * 3f + new Vector3(0.32f, 0f, 0f), 0.03f, Pal.Ink, 3);
            // coiled rope, crates
            g.Frustum(new Vector3(0.4f, deckY, len * 0.5f), new Vector3(0.4f, deckY + 0.18f, len * 0.5f), 0.4f, 0.4f, 0.36f, 0.36f, 8, 0f, C(196, 170, 120), C(176, 150, 104), false, true);
            if (len > 10f) PG.Box2(g, new Vector3(-0.6f, deckY + 0.3f, len * 0.3f), new Vector3(0.4f, 0.3f, 0.35f), C(140, 110, 76), C(170, 136, 96));
            return m;
        }

        // ------------------------------------------------------------------ boat hull (rings along Z, origin = waterline)
        static void Hull(LifeGeo g, float len, float beam, float sheerMid, float sheerBow, float sheerStern, float keel, Color32 bottom, Color32 wale, Color32 top, Color32 deck)
        {
            const int NS = 8;
            var P = new Vector3[NS + 1, 7];
            for (int i = 0; i <= NS; i++)
            {
                float t = i / (float)NS;                 // 0 stern .. 1 bow
                float z = (t - 0.5f) * len;
                float b = beam * 0.5f * (t < 0.5f ? Mathf.Lerp(0.78f, 1f, Mathf.Sin(t * Mathf.PI)) : Mathf.Sin(Mathf.Lerp(0.5f, 1f, t) * Mathf.PI) * 0.95f + 0.05f);
                float sh = t < 0.5f ? Mathf.Lerp(sheerStern, sheerMid, Mathf.Sin(t * Mathf.PI)) : Mathf.Lerp(sheerBow, sheerMid, Mathf.Sin(t * Mathf.PI));
                float k = keel * Mathf.Sin(Mathf.Lerp(0.15f, 0.85f, t) * Mathf.PI);
                P[i, 0] = new Vector3(-b, sh, z);
                P[i, 1] = new Vector3(-b * 0.97f, sh * 0.45f, z);
                P[i, 2] = new Vector3(-b * 0.75f, -k * 0.55f, z);
                P[i, 3] = new Vector3(0f, -k, z);
                P[i, 4] = new Vector3(b * 0.75f, -k * 0.55f, z);
                P[i, 5] = new Vector3(b * 0.97f, sh * 0.45f, z);
                P[i, 6] = new Vector3(b, sh, z);
            }
            for (int i = 0; i < NS; i++)
                for (int k = 0; k < 6; k++)
                {
                    Vector3 a = P[i, k], b = P[i + 1, k], d = P[i + 1, k + 1], e = P[i, k + 1];
                    Vector3 mid = (a + b + d + e) * 0.25f;
                    Vector3 outw = new Vector3(mid.x, Mathf.Min(0f, mid.y) * 2f, 0f);
                    if (outw.sqrMagnitude < 1e-4f) outw = Vector3.down;
                    Color32 col = (k == 0 || k == 5) ? top : bottom;
                    g.QuadOut(a, b, d, e, outw, col, col, col, col);
                }
            // wale stripes
            for (int i = 0; i < NS; i++)
                for (int s = 0; s < 2; s++)
                {
                    int k = s == 0 ? 0 : 6;
                    float sx = s == 0 ? -1f : 1f;
                    Vector3 a = P[i, k] + new Vector3(sx * 0.03f, -0.12f, 0f), b = P[i + 1, k] + new Vector3(sx * 0.03f, -0.12f, 0f);
                    g.QuadOut(a, b, b - Up * 0.22f, a - Up * 0.22f, new Vector3(sx, 0f, 0f), wale, wale, wale, wale);
                }
            // transom and stem
            g.QuadOut(P[0, 0], P[0, 6], P[0, 4], P[0, 2], Vector3.back, top, top, bottom, bottom);
            g.TriOut(P[0, 2], P[0, 4], P[0, 3], Vector3.back, bottom, bottom, bottom);
            // deck
            for (int i = 0; i < NS; i++)
                g.QuadOut(P[i, 0] + Up * -0.05f, P[i + 1, 0] + Up * -0.05f, P[i + 1, 6] + Up * -0.05f, P[i, 6] + Up * -0.05f, Vector3.up, deck, deck, deck, deck);
        }

        public static LifeGeo Junk(System.Random rng)
        {
            var g = new LifeGeo();
            float len = R(rng, 12f, 15f), beam = len * 0.3f;
            Color32 hullB = C(70, 52, 42), hullT = C(140, 96, 60), wale = C(186, 54, 44), deck = C(176, 140, 96);
            Hull(g, len, beam, 1.4f, 2.4f, 2.8f, 1.1f, hullB, wale, hullT, deck);
            // bow eyes
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 e = new Vector3(s * beam * 0.36f, 1.55f, len * 0.4f);
                g.Disc(e, 0.28f, 8, 0f, new Vector3(s, 0f, 0.35f).normalized, C(250, 248, 240), C(250, 248, 240));
                g.Disc(e + new Vector3(s * 0.02f, 0f, 0.02f), 0.13f, 6, 0f, new Vector3(s, 0f, 0.35f).normalized, Pal.Black, Pal.Black);
            }
            // stern cabin with a curved roof
            float cz0 = -len * 0.5f + 0.6f, cz1 = -len * 0.12f;
            PG.Box2(g, new Vector3(0f, 2.2f, (cz0 + cz1) * 0.5f), new Vector3(beam * 0.36f, 0.85f, (cz1 - cz0) * 0.5f), C(150, 104, 66), C(150, 104, 66));
            PG.Roof(g, new Vector3(0f, 3.05f, (cz0 + cz1) * 0.5f), beam * 0.36f, (cz1 - cz0) * 0.5f, 0.6f, 0.35f, 0.18f, C(92, 72, 56), C(108, 84, 62), Pal.TileUnder, Pal.WoodDark, false);
            for (int s = -1; s <= 1; s += 2)
                PG.BoxMM(g, new Vector3(s * beam * 0.365f - 0.02f, 2.1f, cz0 + 0.8f), new Vector3(s * beam * 0.365f + 0.02f, 2.6f, cz1 - 0.8f), Pal.WindowWarm);
            // masts and battened sails
            float[] mz = { len * 0.3f, len * 0.02f, -len * 0.42f };
            float[] mh = { 8f, 11f, 6f };
            float trim = R(rng, -18f, 18f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 b = new Vector3(0f, 1.2f, mz[i]), t = b + Up * mh[i];
                g.Frustum(b, t, 0.13f, 0.13f, 0.08f, 0.08f, 5, 0f, Pal.WoodDark, Pal.Wood, false, true);
                Quaternion q = Quaternion.Euler(0f, trim + (i - 1) * 6f, 0f);
                float top = mh[i] - 0.3f, bot = 1.6f + (i == 2 ? 1.5f : 0.4f);
                float fwd = 0.6f, aft = mh[i] * 0.48f;
                Vector3 p0 = b + q * new Vector3(0f, top - 1.2f, fwd * 0.4f), p1 = b + q * new Vector3(0f, top, -aft * 0.75f);
                Vector3 p2 = b + q * new Vector3(0f, bot, -aft), p3 = b + q * new Vector3(0f, bot, fwd);
                Color32 sc = i == 1 ? Sail : Sh(Sail, 1.08f);
                PG.Quad2(g, p0, p1, p2, p3, sc, sc, Sh(sc, 0.9f), Sh(sc, 0.9f));
                int bat = Mathf.RoundToInt(mh[i] / 1.4f);
                for (int k = 0; k <= bat; k++)
                {
                    float u = k / (float)bat;
                    Vector3 a = Vector3.Lerp(p0, p3, u), c = Vector3.Lerp(p1, p2, u);
                    PG.Beam(g, a + q * new Vector3(0.05f, 0f, 0f), c + q * new Vector3(0.05f, 0f, 0f), 0.03f, 0.03f, SailDark);
                    PG.Beam(g, a - q * new Vector3(0.05f, 0f, 0f), c - q * new Vector3(0.05f, 0f, 0f), 0.03f, 0.03f, SailDark);
                }
            }
            // pennant on the main mast
            Vector3 pt = new Vector3(0f, 1.2f + mh[1], mz[1]);
            g.Tri2(pt, pt + new Vector3(0f, -0.4f, -1.6f), pt + new Vector3(0f, -0.8f, 0f), Pal.RedBright, Pal.Gold, Pal.RedBright);
            // nets and baskets on deck
            g.Ellipsoid(new Vector3(0.3f, 1.45f, len * 0.18f), new Vector3(0.9f, 0.25f, 0.7f), 6, 3, C(84, 96, 82), C(64, 74, 64));
            for (int i = 0; i < 3; i++)
                g.Frustum(new Vector3(-0.6f + i * 0.5f, 1.35f, len * 0.3f), new Vector3(-0.6f + i * 0.5f, 1.75f, len * 0.3f), 0.2f, 0.2f, 0.24f, 0.24f, 6, 0f, BambooDark, Bamboo, true, false);
            return g;
        }

        public static LifeGeo Sampan(System.Random rng)
        {
            var g = new LifeGeo();
            float len = R(rng, 5.6f, 7f), beam = 1.6f;
            Color32[] tops = { C(132, 100, 70), C(96, 132, 150), C(150, 120, 84), C(170, 74, 56) };
            Hull(g, len, beam, 0.55f, 0.9f, 0.8f, 0.45f, C(76, 60, 50), C(190, 160, 70), tops[rng.Next(tops.Length)], C(170, 140, 100));
            // rattan canopy (half barrel)
            float z0 = -len * 0.18f, z1 = len * 0.16f, r = 0.78f;
            Color32 rat = C(180, 146, 92), ratD = C(140, 110, 70);
            const int N = 7;
            for (int k = 0; k < N; k++)
            {
                float a0 = Mathf.PI * k / N, a1 = Mathf.PI * (k + 1) / N;
                Vector3 o0 = new Vector3(Mathf.Cos(a0) * r, 0.5f + Mathf.Sin(a0) * r, 0f), o1 = new Vector3(Mathf.Cos(a1) * r, 0.5f + Mathf.Sin(a1) * r, 0f);
                Color32 c = (k & 1) == 0 ? rat : ratD;
                PG.Quad2(g, o0 + Vector3.forward * z0, o1 + Vector3.forward * z0, o1 + Vector3.forward * z1, o0 + Vector3.forward * z1, c, c, c, c);
            }
            for (int i = 0; i <= 3; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / 3f);
                for (int k = 0; k < N; k++)
                {
                    float a0 = Mathf.PI * k / N, a1 = Mathf.PI * (k + 1) / N;
                    PG.Rod(g, new Vector3(Mathf.Cos(a0) * (r + 0.02f), 0.5f + Mathf.Sin(a0) * (r + 0.02f), z), new Vector3(Mathf.Cos(a1) * (r + 0.02f), 0.5f + Mathf.Sin(a1) * (r + 0.02f), z), 0.025f, Pal.WoodDark, 3);
                }
            }
            // stern sculling oar
            g.Frustum(new Vector3(0.2f, 0.8f, -len * 0.45f), new Vector3(0.5f, -0.3f, -len * 0.5f - 2.2f), 0.04f, 0.04f, 0.035f, 0.035f, 4, 0f, Pal.Wood, Pal.WoodLight, false, false);
            g.Ellipsoid(new Vector3(0.1f, 0.62f, len * 0.36f), new Vector3(0.3f, 0.12f, 0.25f), 6, 3, C(84, 96, 82), C(64, 74, 64));
            return g;
        }

        // ------------------------------------------------------------------ drying nets on bamboo frames (along X)
        public static PropModel NetFrame(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            float hw = R(rng, 2.6f, 3.6f), h = 2.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Rod(g, new Vector3(s * hw, -0.3f, -0.6f), new Vector3(s * hw, h + 0.1f, 0f), 0.05f, BambooDark, 4);
                PG.Rod(g, new Vector3(s * hw, -0.3f, 0.6f), new Vector3(s * hw, h + 0.1f, 0f), 0.05f, BambooDark, 4);
            }
            PG.Rod(g, new Vector3(-hw - 0.3f, h, 0f), new Vector3(hw + 0.3f, h, 0f), 0.05f, Bamboo, 5);
            Color32[] nets = { C(70, 100, 92), C(150, 80, 64), C(80, 96, 130), C(120, 116, 80) };
            Color32 nc = nets[rng.Next(nets.Length)];
            const int NS = 8;
            for (int i = 0; i < NS; i++)
            {
                float x0 = -hw + 2f * hw * i / NS, x1 = -hw + 2f * hw * (i + 1) / NS;
                float d0 = 0.08f * Mathf.Sin(Mathf.PI * i / NS), d1 = 0.08f * Mathf.Sin(Mathf.PI * (i + 1) / NS);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = new Vector3(x0, h - d0, 0f), b = new Vector3(x1, h - d1, 0f);
                    Vector3 a2 = new Vector3(x0, 0.35f, s * 0.35f), b2 = new Vector3(x1, 0.35f, s * 0.35f);
                    Color32 c0 = A(nc, 20), c1 = A(Sh(nc, 0.85f), 160);
                    PG.Quad2(st, a, b, b2, a2, c0, c0, c1, c1);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = new Vector3(-hw + 0.4f + i * (2f * hw - 0.8f) / 6f, h - 0.15f, 0.06f);
                g.Ellipsoid(p, new Vector3(0.1f, 0.1f, 0.1f), 5, 3, i % 2 == 0 ? C(246, 140, 50) : C(244, 240, 230), C(200, 100, 40));
            }
            return m;
        }

        public static PropModel FishRack(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var st = m[PK.Strong];
            float hw = 2.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Rod(g, new Vector3(s * hw, -0.3f, -0.5f), new Vector3(s * hw, 1.9f, 0f), 0.045f, BambooDark, 4);
                PG.Rod(g, new Vector3(s * hw, -0.3f, 0.5f), new Vector3(s * hw, 1.9f, 0f), 0.045f, BambooDark, 4);
            }
            Color32 fA = C(204, 176, 128), fB = C(176, 150, 112), fC = C(220, 200, 170);
            for (int r = 0; r < 3; r++)
            {
                float y = 1.85f - r * 0.55f, z = (r - 1) * 0.12f;
                PG.Rod(g, new Vector3(-hw - 0.2f, y, z), new Vector3(hw + 0.2f, y, z), 0.03f, Bamboo, 4);
                for (int i = 0; i < 11; i++)
                {
                    Vector3 p = new Vector3(-hw + 0.2f + i * (2f * hw - 0.4f) / 10f, y - 0.02f, z);
                    Color32 fc = (i + r) % 3 == 0 ? fA : ((i + r) % 3 == 1 ? fB : fC);
                    float L = R(rng, 0.3f, 0.42f);
                    st.Tri2(p, p + new Vector3(0.07f, -L * 0.6f, 0f), p + new Vector3(-0.07f, -L * 0.6f, 0f), A(fc, 30), A(fc, 120), A(fc, 120));
                    st.Tri2(p + new Vector3(0.07f, -L * 0.6f, 0f), p + new Vector3(0f, -L, 0f), p + new Vector3(-0.07f, -L * 0.6f, 0f), A(fc, 120), A(Sh(fc, 0.8f), 200), A(fc, 120));
                }
            }
            return m;
        }

        // ------------------------------------------------------------------ qilou arcaded shophouse (street front at +Z)
        public static readonly Color32[] Stucco =
        {
            C(240, 198, 182), C(236, 216, 160), C(190, 218, 198), C(212, 200, 230), C(244, 230, 210), C(178, 210, 222), C(246, 186, 150),
        };

        public static PropModel Qilou(System.Random rng, float W, int floors, int colour, float D = 11f)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            var st = m[PK.Strong];
            Color32 wall = Stucco[colour % Stucco.Length], wallD = Sh(wall, 0.86f), trim = C(250, 248, 240);
            Color32 shutter = rng.NextDouble() < 0.5 ? C(70, 132, 112) : C(76, 108, 150);
            float hw = W * 0.5f, arcH = 3.8f, flH = 3.2f;
            float top = arcH + floors * flH;
            // body (upper floors span the arcade)
            PG.Box2(g, new Vector3(0f, (arcH + top) * 0.5f, -D * 0.5f), new Vector3(hw, (top - arcH) * 0.5f, D * 0.5f), wall, Pal.Tile);
            // ground floor shop (recessed behind the arcade)
            PG.Box2(g, new Vector3(0f, arcH * 0.5f, -D * 0.5f - 1.3f), new Vector3(hw - 0.05f, arcH * 0.5f, D * 0.5f - 1.3f), wallD, wallD);
            PG.BoxMM(gl, new Vector3(-hw + 0.5f, 0.3f, -2.62f), new Vector3(hw - 0.5f, 2.9f, -2.58f), C(255, 214, 150));
            PG.BoxMM(g, new Vector3(-hw + 0.4f, 2.9f, -2.6f), new Vector3(hw - 0.4f, 3.2f, -2.55f), Pal.WoodDark);
            // arcade floor, pillars and arches
            PG.Box2(g, new Vector3(0f, 0.08f, -1.3f), new Vector3(hw, 0.1f, 1.3f), Pal.Stone, Pal.StoneLight);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(g, new Vector3(s * (hw - 0.3f), arcH * 0.5f, -0.3f), new Vector3(0.3f, arcH * 0.5f, 0.3f), trim, trim);
                PG.Box2(g, new Vector3(s * (hw - 0.3f), 0.3f, -0.3f), new Vector3(0.36f, 0.3f, 0.36f), wallD, wallD);
            }
            for (int k = 0; k < 6; k++)
            {
                float a0 = Mathf.PI * k / 6f, a1 = Mathf.PI * (k + 1) / 6f;
                float r = hw - 0.6f;
                Vector3 c = new Vector3(0f, arcH - r * 0.45f, 0.02f);
                Vector3 q0 = c + new Vector3(-Mathf.Cos(a0) * r, Mathf.Sin(a0) * r * 0.45f, 0f), q1 = c + new Vector3(-Mathf.Cos(a1) * r, Mathf.Sin(a1) * r * 0.45f, 0f);
                g.QuadOut(q0, q1, new Vector3(q1.x, arcH, 0.02f), new Vector3(q0.x, arcH, 0.02f), Vector3.forward, trim, trim, trim, trim);
            }
            // cornices
            PG.Box2(g, new Vector3(0f, arcH + 0.12f, 0.1f), new Vector3(hw + 0.1f, 0.14f, 0.12f), trim, trim);
            for (int f = 1; f < floors; f++) PG.Box2(g, new Vector3(0f, arcH + f * flH, 0.06f), new Vector3(hw + 0.05f, 0.08f, 0.08f), trim, trim);
            // upper windows with shutters (glow panes)
            int nw = W > 6.5f ? 3 : 2;
            for (int f = 0; f < floors; f++)
            {
                float y0 = arcH + f * flH + 0.8f, y1 = y0 + 1.7f;
                for (int i = 0; i < nw; i++)
                {
                    float cx = -hw + (i + 0.5f) * W / nw, ww = 0.42f;
                    PG.BoxMM(g, new Vector3(cx - ww - 0.12f, y0 - 0.12f, 0f), new Vector3(cx + ww + 0.12f, y1 + 0.14f, 0.06f), trim);
                    PG.BoxMM(gl, new Vector3(cx - ww, y0, 0.06f), new Vector3(cx + ww, y1, 0.08f), (f + i + colour) % 3 == 0 ? C(255, 206, 130) : C(120, 150, 170, 255));
                    PG.BoxMM(g, new Vector3(cx - 0.03f, y0, 0.08f), new Vector3(cx + 0.03f, y1, 0.1f), trim);
                    PG.BoxMM(g, new Vector3(cx - ww - 0.42f, y0, 0.07f), new Vector3(cx - ww - 0.13f, y1, 0.11f), shutter);
                    PG.BoxMM(g, new Vector3(cx + ww + 0.13f, y0, 0.07f), new Vector3(cx + ww + 0.42f, y1, 0.11f), shutter);
                    PG.BoxMM(g, new Vector3(cx - ww - 0.1f, y1 + 0.14f, 0f), new Vector3(cx + ww + 0.1f, y1 + 0.3f, 0.2f), wallD);
                }
                if (f == 0 && rng.NextDouble() < 0.6)
                {
                    // small balcony with a balustrade and flower pots
                    PG.Box2(g, new Vector3(0f, y0 - 0.15f, 0.45f), new Vector3(hw - 0.6f, 0.08f, 0.45f), trim, trim);
                    for (int i = 0; i <= 8; i++) PG.Rod(g, new Vector3(-hw + 0.7f + i * (W - 1.4f) / 8f, y0 - 0.1f, 0.85f), new Vector3(-hw + 0.7f + i * (W - 1.4f) / 8f, y0 + 0.65f, 0.85f), 0.04f, trim, 4);
                    PG.Beam(g, new Vector3(-hw + 0.6f, y0 + 0.68f, 0.85f), new Vector3(hw - 0.6f, y0 + 0.68f, 0.85f), 0.06f, 0.05f, trim);
                    for (int i = 0; i < 3; i++) g.Ellipsoid(new Vector3(-hw + 1.2f + i * (W - 2.4f) / 2f, y0 + 0.85f, 0.75f), new Vector3(0.25f, 0.2f, 0.18f), 6, 3, C(232, 90, 110, 30), C(80, 130, 70, 20));
                }
            }
            // back windows (the sea-side rows show their backs to the harbour)
            for (int f = 0; f < floors; f++)
                for (int i = 0; i < nw; i++)
                {
                    float cx = -hw + (i + 0.5f) * W / nw, y0 = arcH + f * flH + 0.9f;
                    PG.BoxMM(gl, new Vector3(cx - 0.4f, y0, -D - 0.05f), new Vector3(cx + 0.4f, y0 + 1.4f, -D + 0.02f), (f + i) % 2 == 0 ? Pal.WindowWarm : C(120, 150, 170, 255));
                    PG.BoxMM(g, new Vector3(cx - 0.5f, y0 + 1.4f, -D - 0.12f), new Vector3(cx + 0.5f, y0 + 1.55f, -D + 0.02f), trim);
                }
            // Nanyang parapet gable
            float py = top;
            PG.Box2(g, new Vector3(0f, py + 0.35f, 0f), new Vector3(hw, 0.35f, 0.15f), wall, trim);
            float gh = R(rng, 1.0f, 1.6f);
            Vector3 gl0 = new Vector3(-hw * 0.55f, py + 0.7f, 0.05f), gr0 = new Vector3(hw * 0.55f, py + 0.7f, 0.05f), gt = new Vector3(0f, py + 0.7f + gh, 0.05f);
            g.QuadOut(gl0, gr0, gr0 + Up * gh * 0.55f, gl0 + Up * gh * 0.55f, Vector3.forward, wall, wall, wall, wall);
            g.TriOut(gl0 + Up * gh * 0.55f, gr0 + Up * gh * 0.55f, gt, Vector3.forward, wall, wall, wall);
            g.QuadOut(gl0 - Vector3.forward * 0.25f, gr0 - Vector3.forward * 0.25f, gr0 + Up * gh * 0.55f - Vector3.forward * 0.25f, gl0 + Up * gh * 0.55f - Vector3.forward * 0.25f, Vector3.back, wallD, wallD, wallD, wallD);
            g.Disc(new Vector3(0f, py + 0.7f + gh * 0.42f, 0.08f), 0.32f, 10, 0f, Vector3.forward, trim, Sh(wall, 0.75f));
            PG.Beam(g, gl0 + Up * gh * 0.55f + Vector3.forward * 0.02f, gt + Vector3.forward * 0.02f, 0.07f, 0.06f, trim);
            PG.Beam(g, gr0 + Up * gh * 0.55f + Vector3.forward * 0.02f, gt + Vector3.forward * 0.02f, 0.07f, 0.06f, trim);
            // tiled roof behind the parapet
            PG.Roof(g, new Vector3(0f, top, -D * 0.5f - 0.3f), hw - 0.2f, D * 0.5f - 0.6f, 1.4f, 0.1f, 0.1f, TileGrey, TileGrey2, Pal.TileUnder, RidgeGrey, false);
            // vertical shop sign and a horizontal name board
            Color32 sign = rng.NextDouble() < 0.6 ? Pal.RedDeep : C(40, 70, 110);
            float sx = (rng.NextDouble() < 0.5 ? -1f : 1f) * (hw - 0.3f);
            PG.Box2(g, new Vector3(sx, arcH - 1.4f, 0.42f), new Vector3(0.24f, 1.1f, 0.06f), sign, sign);
            for (int i = 0; i < 4; i++) g.Box(new Vector3(sx, arcH - 0.65f - i * 0.5f, 0.49f), new Vector3(0.13f, 0.16f, 0.01f), Pal.Gold);
            PG.Box2(g, new Vector3(0f, arcH + 0.55f, 0.18f), new Vector3(hw * 0.6f, 0.28f, 0.05f), Pal.Black, Pal.Black);
            for (int i = 0; i < 4; i++) g.Box(new Vector3(-hw * 0.45f + i * hw * 0.3f, arcH + 0.55f, 0.24f), new Vector3(0.18f, 0.18f, 0.01f), Pal.Gold);
            // awning over the arcade mouth for some shops
            if (rng.NextDouble() < 0.45)
            {
                Color32 aw = Cloth[rng.Next(Cloth.Length)];
                PG.Quad2(st, new Vector3(-hw + 0.6f, arcH - 0.3f, 0.3f), new Vector3(hw - 0.6f, arcH - 0.3f, 0.3f), new Vector3(hw - 0.6f, arcH - 1.0f, 1.5f), new Vector3(-hw + 0.6f, arcH - 1.0f, 1.5f),
                         A(aw, 20), A(aw, 20), A(Sh(aw, 0.9f), 120), A(Sh(aw, 0.9f), 120));
            }
            // hanging lanterns in the arcade
            for (int s = -1; s <= 1; s += 2) PG.Lantern(gl, new Vector3(s * hw * 0.4f, arcH - 0.05f, -0.6f), 0.24f, 0.4f, Pal.RedBright, 70);
            return m;
        }

        // ------------------------------------------------------------------ seafood stall with tanks (front +Z)
        public static PropModel SeafoodStall(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            var st = m[PK.Strong];
            Color32 wood = Pal.Wood, dark = Pal.WoodDark;
            PG.Box2(g, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.45f, 0.6f), Sh(wood, 0.9f), Pal.WoodLight);
            // tanks (glassy blue boxes with darker water tops)
            for (int i = 0; i < 2; i++)
            {
                Vector3 c = new Vector3(-0.7f + i * 1.4f, 1.15f, -0.1f);
                PG.Box2(g, c, new Vector3(0.6f, 0.25f, 0.4f), C(150, 206, 214), C(70, 150, 168));
                for (int f = 0; f < 3; f++)
                    g.Ellipsoid(c + new Vector3(R(rng, -0.4f, 0.4f), R(rng, -0.1f, 0.1f), 0.41f), new Vector3(0.12f, 0.05f, 0.01f), 5, 2, C(240, 120, 70), C(200, 90, 60));
            }
            // ice bed with fish, baskets of crabs and shellfish
            PG.Box2(g, new Vector3(0f, 0.95f, 0.45f), new Vector3(1.35f, 0.05f, 0.18f), C(236, 244, 248), C(236, 244, 248));
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-1.1f + i * 0.44f, 1.02f, 0.45f);
                g.Ellipsoid(p, new Vector3(0.18f, 0.05f, 0.07f), 5, 3, C(196, 206, 214), C(140, 150, 160));
            }
            Color32[] catch_ = { C(222, 92, 56), C(70, 60, 56), C(232, 196, 160), C(196, 112, 140) };
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(-1.1f + i * 1.1f, 0f, 1.05f);
                g.Frustum(p, p + Up * 0.32f, 0.26f, 0.26f, 0.32f, 0.32f, 7, 0f, BambooDark, Bamboo, true, false);
                Color32 cc = catch_[rng.Next(catch_.Length)];
                for (int k = 0; k < 5; k++)
                    g.Ellipsoid(p + new Vector3(R(rng, -0.18f, 0.18f), 0.33f, R(rng, -0.18f, 0.18f)), new Vector3(0.09f, 0.05f, 0.08f), 5, 2, cc, Sh(cc, 0.8f));
            }
            // poles and striped awning
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Beam(g, new Vector3(s * 1.5f, -0.3f, 0.75f), new Vector3(s * 1.5f, 2.3f, 0.75f), 0.045f, 0.045f, dark);
                PG.Beam(g, new Vector3(s * 1.5f, -0.3f, -0.7f), new Vector3(s * 1.5f, 2.6f, -0.7f), 0.045f, 0.045f, dark);
            }
            Color32 ca = Cloth[rng.Next(Cloth.Length)], cb = Cloth[5];
            for (int i = 0; i < 6; i++)
            {
                float x0 = -1.6f + i * 3.2f / 6f, x1 = x0 + 3.2f / 6f;
                Color32 c = (i & 1) == 0 ? ca : cb;
                PG.Quad2(st, new Vector3(x0, 2.62f, -0.75f), new Vector3(x1, 2.62f, -0.75f), new Vector3(x1, 2.25f, 0.95f), new Vector3(x0, 2.25f, 0.95f), A(c, 10), A(c, 10), A(c, 110), A(c, 110));
            }
            PG.Lantern(gl, new Vector3(0f, 2.3f, 0.5f), 0.2f, 0.32f, C(255, 196, 110), 90);
            // price board
            PG.Box2(g, new Vector3(1.2f, 1.6f, 0.62f), new Vector3(0.25f, 0.3f, 0.02f), Pal.Ink, Pal.Ink);
            for (int i = 0; i < 3; i++) g.Box(new Vector3(1.2f, 1.75f - i * 0.15f, 0.645f), new Vector3(0.16f, 0.025f, 0.005f), C(240, 240, 230));
            return m;
        }

        // ------------------------------------------------------------------ promenade lamp (two lanterns)
        public static PropModel PromenadeLamp()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            Color32 iron = C(42, 56, 60), ironL = C(70, 90, 92);
            g.Frustum(Vector3.zero - Up * 0.2f, Up * 0.6f, 0.2f, 0.2f, 0.12f, 0.12f, 8, 0f, iron, ironL, false, true);
            g.Frustum(Up * 0.6f, Up * 4.2f, 0.07f, 0.07f, 0.05f, 0.05f, 6, 0f, iron, iron, false, true);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 a = Up * 4.0f, b = Up * 4.15f + Vector3.right * (s * 0.7f);
                PG.Rod(g, a, b, 0.035f, iron, 4);
                Vector3 lc = b - Up * 0.05f;
                g.Frustum(lc + Up * 0.05f, lc + Up * 0.15f, 0.2f, 0.2f, 0.08f, 0.08f, 6, 0f, iron, iron, false, true);
                g.Frustum(lc - Up * 0.45f, lc + Up * 0.05f, 0.14f, 0.14f, 0.19f, 0.19f, 6, 0f, iron, iron, true, false);
                gl.Ellipsoid(lc - Up * 0.2f, new Vector3(0.16f, 0.22f, 0.16f), 6, 4, C(255, 236, 190), C(255, 214, 150));
            }
            g.Ellipsoid(Up * 4.35f, new Vector3(0.09f, 0.12f, 0.09f), 5, 3, ironL, iron);
            return m;
        }

        // ------------------------------------------------------------------ lighthouse (skyline) + keeper's house
        public const float LightTowerH = 21f;

        public static PropModel Lighthouse()
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 white = C(246, 244, 238), whiteS = C(226, 222, 214), red = C(206, 56, 46), stone = C(176, 166, 156);
            float H = LightTowerH;
            // granite plinth
            g.Frustum(Up * -1f, Up * 1.4f, 4.4f, 4.4f, 4.0f, 4.0f, 8, Mathf.PI / 8f, Sh(stone, 0.85f), stone, false, true);
            // tapered octagonal tower with two red bands
            const int seg = 6;
            for (int i = 0; i < seg; i++)
            {
                float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
                float y0 = 1.4f + t0 * (H - 1.4f), y1 = 1.4f + t1 * (H - 1.4f);
                float r0 = Mathf.Lerp(3.1f, 1.95f, t0), r1 = Mathf.Lerp(3.1f, 1.95f, t1);
                Color32 c = (i == 2 || i == 4) ? red : white;
                g.Frustum(Up * y0, Up * y1, r0, r0, r1, r1, 8, Mathf.PI / 8f, c, (i == 2 || i == 4) ? red : whiteS, false, false);
            }
            // door, small windows up the tower
            PG.BoxMM(g, new Vector3(-0.6f, 1.4f, 2.95f), new Vector3(0.6f, 3.6f, 3.1f), C(60, 90, 110));
            for (int i = 0; i < 4; i++)
            {
                float y = 5.5f + i * 3.7f, r = Mathf.Lerp(3.1f, 1.95f, (y - 1.4f) / (H - 1.4f));
                Quaternion q = Quaternion.Euler(0f, i * 90f + 45f, 0f);
                PG.OBox(gl, q * new Vector3(0f, 0f, r - 0.02f) + Up * y, new Vector3(0.28f, 0.45f, 0.06f), q, Pal.WindowWarm);
            }
            // gallery and railing
            g.Frustum(Up * H, Up * (H + 0.35f), 2.0f, 2.0f, 3.0f, 3.0f, 8, Mathf.PI / 8f, red, red, true, true);
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 2.85f, H + 0.35f, Mathf.Sin(a) * 2.85f);
                PG.Rod(g, p, p + Up * 1.0f, 0.035f, Pal.Ink, 4);
            }
            g.Frustum(Up * (H + 1.3f), Up * (H + 1.38f), 2.88f, 2.88f, 2.88f, 2.88f, 16, 0f, Pal.Ink, Pal.Ink, false, false);
            // lantern room (glow) with mullions, red dome, vent ball and rod
            g.Frustum(Up * (H + 0.35f), Up * (H + 1.0f), 1.6f, 1.6f, 1.6f, 1.6f, 8, Mathf.PI / 8f, white, white, false, false);
            gl.Frustum(Up * (H + 1.0f), Up * (H + 3.4f), 1.45f, 1.45f, 1.45f, 1.45f, 8, Mathf.PI / 8f, C(255, 246, 214), C(255, 236, 190), false, false);
            for (int k = 0; k < 8; k++)
            {
                float a = Mathf.PI / 8f + k * Mathf.PI / 4f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 1.5f, H + 1.0f, Mathf.Sin(a) * 1.5f);
                PG.Rod(g, p, p + Up * 2.4f, 0.05f, Pal.Ink, 4);
            }
            g.Frustum(Up * (H + 3.4f), Up * (H + 4.6f), 1.8f, 1.8f, 0.35f, 0.35f, 8, Mathf.PI / 8f, red, Sh(red, 0.85f), true, false);
            g.Ellipsoid(Up * (H + 4.85f), new Vector3(0.32f, 0.32f, 0.32f), 6, 4, Pal.Ink, Pal.Ink);
            PG.Rod(g, Up * (H + 5f), Up * (H + 6.2f), 0.04f, Pal.Ink, 4);
            return m;
        }

        public static PropModel KeeperHouse()
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 white = C(244, 240, 230), base_ = C(170, 160, 150);
            PG.Box2(g, new Vector3(0f, 0.25f, 0f), new Vector3(4.6f, 0.45f, 3.4f), base_, Pal.StoneLight);
            PG.Box2(g, new Vector3(0f, 2.0f, 0f), new Vector3(4.2f, 1.5f, 3.0f), white, white);
            PG.BoxMM(g, new Vector3(-0.55f, 0.7f, 2.98f), new Vector3(0.55f, 2.9f, 3.05f), C(64, 100, 120));
            for (int s = -1; s <= 1; s += 2)
            {
                PG.BoxMM(gl, new Vector3(s * 2.4f - 0.55f, 1.6f, 2.98f), new Vector3(s * 2.4f + 0.55f, 2.7f, 3.04f), Pal.WindowWarm);
                PG.BoxMM(g, new Vector3(s * 2.4f - 0.65f, 1.5f, 3.0f), new Vector3(s * 2.4f + 0.65f, 1.6f, 3.12f), C(64, 100, 120));
            }
            PG.Roof(g, new Vector3(0f, 3.5f, 0f), 4.2f, 3.0f, 1.8f, 0.8f, 0.35f, C(196, 82, 62), C(214, 98, 72), Pal.TileUnder, C(150, 60, 50), false);
            // garden wall, flagpole
            for (int i = 0; i < 9; i++)
            {
                float x = -4.6f + i * 1.15f;
                PG.Box2(g, new Vector3(x, 0.5f, 5.2f), new Vector3(0.52f, 0.5f, 0.2f), white, Pal.StoneLight);
            }
            PG.Column(g, new Vector3(5.6f, 0f, 4.8f), new Vector3(5.6f, 7f, 4.8f), 0.06f, white, 5);
            return m;
        }

        // ------------------------------------------------------------------ pavilion (ting) and pagoda
        public static PropModel Pavilion(float half, float baseH, Color32 tileA, Color32 tileB)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            PG.Box2(g, new Vector3(0f, baseH * 0.5f - 0.3f, 0f), new Vector3(half + 0.6f, baseH * 0.5f + 0.3f, half + 0.6f), Pal.StoneDark, Pal.StoneLight);
            float y0 = baseH, colH = 3.0f;
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    PG.Column(g, new Vector3(x * half, y0, z * half), new Vector3(x * half, y0 + colH, z * half), 0.18f, Pal.Red, 8);
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                PG.Beam(g, q * new Vector3(-half, y0 + colH - 0.2f, half), q * new Vector3(half, y0 + colH - 0.2f, half), 0.12f, 0.18f, C(56, 118, 122));
                PG.Beam(g, q * new Vector3(-half, y0 + 0.45f, half), q * new Vector3(half, y0 + 0.45f, half), 0.1f, 0.05f, Pal.RedDeep);
            }
            PG.Roof(g, new Vector3(0f, y0 + colH, 0f), half + 0.1f, half + 0.1f, 1.9f, 1.0f, 0.55f, tileA, tileB, Pal.TileUnder, C(52, 100, 84), true);
            for (int s = -1; s <= 1; s += 2) PG.Lantern(gl, new Vector3(s * half, y0 + colH - 0.35f, half + 0.05f), 0.22f, 0.38f, Pal.RedBright, 90);
            return m;
        }

        public static PropModel Pagoda(int storeys)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 wall = C(240, 232, 214), wallD = C(222, 210, 190);
            PG.Box2(g, new Vector3(0f, 0.4f, 0f), new Vector3(5.2f, 0.8f, 5.2f), Pal.StoneDark, Pal.StoneLight);
            float y = 1.2f, hw = 3.4f;
            for (int i = 0; i < storeys; i++)
            {
                float h = i == 0 ? 3.4f : 2.6f;
                PG.Box2(g, new Vector3(0f, y + h * 0.5f, 0f), new Vector3(hw, h * 0.5f, hw), i % 2 == 0 ? wall : wallD, wall);
                for (int k = 0; k < 4; k++)
                {
                    Quaternion q = Quaternion.Euler(0f, k * 90f, 0f);
                    PG.OBox(gl, q * new Vector3(0f, y + h * 0.5f, hw + 0.01f), new Vector3(0.45f, h * 0.3f, 0.04f), q, Pal.WindowWarm);
                    PG.OBox(g, q * new Vector3(0f, y + h * 0.5f + h * 0.32f, hw + 0.03f), new Vector3(0.6f, 0.08f, 0.05f), q, Pal.Red);
                    for (int s = -1; s <= 1; s += 2) PG.OBox(g, q * new Vector3(s * (hw - 0.15f), y + h * 0.5f, hw + 0.02f), new Vector3(0.15f, h * 0.5f, 0.06f), q, Pal.Red);
                }
                y += h;
                PG.Roof(g, new Vector3(0f, y, 0f), hw + 0.1f, hw + 0.1f, 0.9f, 1.1f, 0.6f, Pal.GreenTile, Pal.GreenTile2, Pal.TileUnder, C(52, 100, 84), false);
                for (int k = 0; k < 4; k++)
                {
                    float a = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                    Vector3 corner = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (hw + 1.0f) * 1.38f + Up * (y + 0.1f);
                    PG.Lantern(gl, corner, 0.14f, 0.24f, Pal.RedBright, 60);
                }
                y += 0.5f;
                hw *= 0.86f;
            }
            // spire
            g.Frustum(Up * y, Up * (y + 0.6f), 0.5f, 0.5f, 0.35f, 0.35f, 8, 0f, Pal.GoldDark, Pal.Gold, true, false);
            for (int i = 0; i < 5; i++) g.Ellipsoid(Up * (y + 0.85f + i * 0.45f), new Vector3(0.32f - i * 0.04f, 0.1f, 0.32f - i * 0.04f), 8, 3, Pal.Gold, Pal.GoldDark);
            g.Frustum(Up * (y + 0.6f), Up * (y + 3.6f), 0.08f, 0.08f, 0.02f, 0.02f, 4, 0f, Pal.Gold, Pal.Gold, false, false);
            return m;
        }

        // ------------------------------------------------------------------ roadside
        /// <summary>Granite guard-wall block between a and b (ground points), height h.</summary>
        public static void GuardWall(LifeGeo g, Vector3 a, Vector3 b, float h, float th, int idx)
        {
            Vector3 d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 0.2f) return;
            d /= len;
            Vector3 side = new Vector3(-d.z, 0f, d.x) * (th * 0.5f);
            Color32 c = idx % 3 == 0 ? GraniteA : (idx % 3 == 1 ? GraniteB : Sh(GraniteA, 0.94f));
            Color32 top = C(214, 204, 194);
            Vector3 a0 = a - Up * 0.3f, b0 = b - Up * 0.3f, a1 = a + Up * h, b1 = b + Up * h;
            g.QuadOut(a0 + side, b0 + side, b1 + side, a1 + side, side, c, c, c, c);
            g.QuadOut(a0 - side, b0 - side, b1 - side, a1 - side, -side, c, c, c, c);
            g.QuadOut(a1 + side * 1.15f + Up * 0.12f, b1 + side * 1.15f + Up * 0.12f, b1 - side * 1.15f + Up * 0.12f, a1 - side * 1.15f + Up * 0.12f, Up, top, top, top, top);
            g.QuadOut(a1 + side * 1.15f, b1 + side * 1.15f, b1 + side * 1.15f + Up * 0.12f, a1 + side * 1.15f + Up * 0.12f, side, top, top, top, top);
            g.QuadOut(a1 - side * 1.15f, b1 - side * 1.15f, b1 - side * 1.15f + Up * 0.12f, a1 - side * 1.15f + Up * 0.12f, -side, top, top, top, top);
            g.QuadOut(a0 - side, a0 + side, a1 + side, a1 - side, -d, c, c, c, c);
            g.QuadOut(b0 - side, b0 + side, b1 + side, b1 - side, d, c, c, c, c);
        }

        /// <summary>Black / yellow chevron board on two posts (faces +Z).</summary>
        public static PropModel Chevron()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 yel = C(250, 204, 60), blk = C(36, 36, 40), post = C(200, 200, 196);
            for (int s = -1; s <= 1; s += 2) PG.Column(g, new Vector3(s * 0.7f, -0.3f, 0f), new Vector3(s * 0.7f, 1.7f, 0f), 0.05f, post, 5);
            PG.Box2(g, new Vector3(0f, 1.35f, 0.05f), new Vector3(0.95f, 0.38f, 0.03f), yel, yel);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.5f + i * 0.5f;
                g.QuadOut(new Vector3(x - 0.18f, 1.05f, 0.09f), new Vector3(x + 0.02f, 1.35f, 0.09f), new Vector3(x + 0.2f, 1.35f, 0.09f), new Vector3(x, 1.05f, 0.09f), Vector3.forward, blk, blk, blk, blk);
                g.QuadOut(new Vector3(x + 0.02f, 1.35f, 0.09f), new Vector3(x - 0.18f, 1.65f, 0.09f), new Vector3(x, 1.65f, 0.09f), new Vector3(x + 0.2f, 1.35f, 0.09f), Vector3.forward, blk, blk, blk, blk);
            }
            return m;
        }

        /// <summary>Blue direction sign with white text bars (faces +Z).</summary>
        public static PropModel RoadSign(int lines)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 blue = C(40, 92, 168), white = C(240, 244, 248), post = C(196, 198, 200);
            PG.Column(g, new Vector3(-1.0f, -0.3f, 0f), new Vector3(-1.0f, 3.2f, 0f), 0.07f, post, 6);
            PG.Column(g, new Vector3(1.0f, -0.3f, 0f), new Vector3(1.0f, 3.2f, 0f), 0.07f, post, 6);
            float h = 0.35f + lines * 0.32f;
            PG.Box2(g, new Vector3(0f, 3.1f - h * 0.5f, 0.1f), new Vector3(1.25f, h * 0.5f, 0.04f), blue, blue);
            for (int i = 0; i < lines; i++)
            {
                float y = 3.1f - 0.3f - i * 0.32f;
                g.Box(new Vector3(-0.3f, y, 0.15f), new Vector3(0.62f, 0.07f, 0.01f), white);
                g.TriOut(new Vector3(0.7f, y + 0.12f, 0.15f), new Vector3(1.0f, y, 0.15f), new Vector3(0.7f, y - 0.12f, 0.15f), Vector3.forward, white, white, white);
            }
            return m;
        }

        /// <summary>A villager standing (static spectator / beachgoer), one arm waving.</summary>
        public static PropModel Person(System.Random rng, bool wave)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32[] tunics = { C(214, 70, 60), C(70, 120, 190), C(246, 232, 210), C(90, 160, 120), C(240, 170, 60), C(200, 110, 160), C(60, 70, 90) };
            Color32 tunic = tunics[rng.Next(tunics.Length)], trim = tunics[rng.Next(tunics.Length)], trousers = rng.NextDouble() < 0.5 ? C(60, 64, 80) : C(120, 100, 80);
            int head = rng.Next(3);
            var body = LifeShapes.VillagerBody(tunic, trim, trousers, head, C(214, 186, 120));
            PG.Append(g, body, Matrix4x4.identity);
            var arm = LifeShapes.VillagerArm(tunic, trim);
            for (int s = -1; s <= 1; s += 2)
            {
                float lift = (wave && s > 0) ? 150f : R(rng, 5f, 25f);
                PG.Append(g, arm, Matrix4x4.TRS(new Vector3(s * 0.2f, 1.32f, 0f), Quaternion.Euler(0f, 0f, s * lift), Vector3.one));
            }
            return m;
        }
    }
}
