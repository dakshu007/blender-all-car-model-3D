// SnowShapes.Build.cs - low-poly models of the Frost Summit region: snow-laden pines, frosted dead trees, snowy rocks
// and crags, alpine chalets with warm windows, snowmobiles, snow poles, fences, banks, the summit observatory, wind
// turbines, cable-car stations / pylons / gondolas, prayer flags, ice sculptures, a crashed snowplough, crevasses and
// a frozen waterfall. Vertex colours sRGB; alpha = wind weight (VertexColorWind / PropGlow).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal static class SnowShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }
        static float R(System.Random rng, float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        public static readonly Color32 Snow = C(238, 243, 250), SnowShade = C(204, 218, 238), SnowBlue = C(186, 206, 234);
        public static readonly Color32 IceC = C(176, 218, 242), IceDeep = C(108, 168, 214), IceWhite = C(226, 242, 252);
        public static readonly Color32 RockA = C(112, 114, 124), RockB = C(92, 94, 106), RockC = C(134, 132, 138);
        public static readonly Color32 Log = C(132, 86, 54), LogDark = C(96, 62, 42), LogLight = C(168, 118, 76);
        public static readonly Color32 Warm = C(255, 196, 112), WarmHot = C(255, 226, 160);
        public static readonly Color32 Orange = C(240, 112, 30), Black = C(34, 34, 38), Steel = C(176, 182, 192), SteelDark = C(110, 116, 126);
        static readonly Color32[] FlagCols = { C(40, 92, 196), C(244, 244, 238), C(206, 44, 40), C(46, 150, 82), C(246, 196, 40) };

        // ------------------------------------------------------------------ trees
        /// <summary>Snow-laden spruce: dark tiers, each carrying a white cap; snow = 0..1 how heavily loaded.</summary>
        public static PropModel Pine(System.Random rng, float snow)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            float H = R(rng, 7.5f, 12f);
            float baseR = H * R(rng, 0.26f, 0.32f);
            Color32 bark = C(92, 66, 50);
            g.Frustum(Vector3.down * 0.3f, Up * (H * 0.35f), 0.22f + H * 0.012f, 0.22f + H * 0.012f, 0.12f, 0.12f, 5, 0f, bark, Sh(bark, 0.85f), false, false);
            int tiers = H > 10f ? 5 : 4;
            Color32 g0 = C(30, 70, 58, 14), g1 = C(52, 98, 76, 26);
            float lean = R(rng, -0.25f, 0.25f);
            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)tiers;
                float y0 = H * (0.16f + 0.74f * t);
                float y1 = y0 + H * (0.34f - 0.08f * t);
                float r0 = baseR * (1f - 0.72f * t) * R(rng, 0.92f, 1.08f);
                Vector3 c0 = new Vector3(lean * t, y0, 0f), c1 = new Vector3(lean * t * 1.1f, y1, 0f);
                float rot = R(rng, 0f, 1f);
                // dark underside skirt + green tier
                g.Frustum(c0, c1, r0, r0, 0.05f, 0.05f, 7, rot, g0, g1, true, false);
                // snow blanket over the tier: starts a little above the rim, heavier trees are almost white
                float sk = Mathf.Lerp(0.62f, 0.95f, snow) * R(rng, 0.92f, 1.04f);
                Vector3 s0 = Vector3.Lerp(c0, c1, 1f - sk) + Up * 0.1f;
                float sr = r0 * sk * 1.03f + 0.06f;
                Color32 sw = PG.A(Snow, 10), ss = PG.A(SnowShade, 8);
                g.Frustum(s0, c1 + Up * 0.12f, sr, sr, 0.04f, 0.04f, 7, rot, ss, sw, false, false);
            }
            int st = g.v.Count;
            g.Ellipsoid(new Vector3(lean, H * 1.02f + 0.2f, 0f), new Vector3(0.28f, 0.34f, 0.28f), 5, 3, Snow, SnowShade);
            PG.Lumpy(g, st, new Vector3(lean, H, 0f), 0.15f, R(rng, 0f, 9f));
            return m;
        }

        /// <summary>Frosted dead tree: silver-grey trunk and crooked limbs, rime on the upper sides.</summary>
        public static PropModel DeadTree(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 wood = C(118, 108, 104), woodD = C(84, 76, 74), rime = C(232, 240, 250);
            float H = R(rng, 5f, 8.5f);
            Vector3 lean = new Vector3(R(rng, -0.6f, 0.6f), 0f, R(rng, -0.6f, 0.6f));
            Vector3 a = Vector3.down * 0.3f, b = Up * H * 0.5f + lean * 0.4f, c = Up * H + lean;
            g.Frustum(a, b, 0.32f, 0.32f, 0.22f, 0.22f, 6, 0f, woodD, wood, false, false);
            g.Frustum(b, c, 0.22f, 0.22f, 0.04f, 0.04f, 6, 0f, wood, rime, false, true);
            int limbs = 5 + rng.Next(4);
            for (int i = 0; i < limbs; i++)
            {
                float t = R(rng, 0.35f, 0.92f);
                Vector3 from = Vector3.Lerp(a, c, t);
                float ang = R(rng, 0f, Mathf.PI * 2f);
                Vector3 d = new Vector3(Mathf.Cos(ang), R(rng, 0.25f, 0.9f), Mathf.Sin(ang));
                float len = R(rng, 1.2f, 2.8f) * (1.2f - t * 0.5f);
                Vector3 mid = from + d * len * 0.55f;
                Vector3 end = mid + (d + new Vector3(R(rng, -0.4f, 0.4f), 0.3f, R(rng, -0.4f, 0.4f))).normalized * len * 0.5f;
                g.Frustum(from, mid, 0.09f, 0.09f, 0.055f, 0.055f, 4, 0f, wood, wood, false, false);
                g.Frustum(mid, end, 0.055f, 0.055f, 0.015f, 0.015f, 4, 0f, wood, rime, false, false);
                // rime crust on top of the limb
                g.Frustum(mid + Up * 0.05f, end + Up * 0.04f, 0.04f, 0.04f, 0.01f, 0.01f, 3, 0f, rime, rime, false, false);
            }
            return m;
        }

        // ------------------------------------------------------------------ rocks
        /// <summary>Snow-covered boulder cluster (radius ~size).</summary>
        public static PropModel SnowRock(System.Random rng, float size, bool landmark)
        {
            var m = new PropModel();
            var g = m[landmark ? PK.Landmark : PK.Props];
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float s = size * (i == 0 ? 1f : R(rng, 0.4f, 0.7f));
                Vector3 c = i == 0 ? Vector3.zero : new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)) * size * 0.9f;
                Vector3 r = new Vector3(s * R(rng, 0.9f, 1.3f), s * R(rng, 0.55f, 0.85f), s * R(rng, 0.8f, 1.2f));
                PG.Boulder(g, c + Up * r.y * 0.35f, r, i == 0 ? RockA : RockB, RockB, R(rng, 0f, 50f));
                int st = g.v.Count;
                Vector3 cap = c + Up * (r.y * 0.35f + r.y * 0.62f);
                g.Ellipsoid(cap, new Vector3(r.x * 0.82f, r.y * 0.42f, r.z * 0.82f), 7, 3, Snow, SnowShade);
                PG.Lumpy(g, st, cap, 0.16f, R(rng, 0f, 50f));
            }
            return m;
        }

        /// <summary>
        /// Snowy rock mass in world space (base on the ground, height H, base radius Rb). style 0 = jagged peak with
        /// sub-summits, 1 = cliff buttress (wide along 'along', flat-ish snowy top, stepped strata ledges).
        /// </summary>
        public static void Crag(LifeGeo g, Vector3 basePos, float H, float Rb, System.Random rng, int style = 0, float alongYaw = 0f)
        {
            const int NR = 10, NS = 11;
            var ring = new Vector3[NR, NS];
            Vector3 leanDir = new Vector3(R(rng, -1f, 1f), 0f, R(rng, -1f, 1f)).normalized;
            float lean = (style == 1 ? R(rng, 0.0f, 0.05f) : R(rng, 0.04f, 0.12f)) * H;
            Quaternion q = Quaternion.Euler(0f, alongYaw, 0f);
            float ax = style == 1 ? R(rng, 1.5f, 2.1f) : R(rng, 0.85f, 1.2f), az = style == 1 ? R(rng, 0.55f, 0.75f) : R(rng, 0.8f, 1.1f);
            var jag = new float[NS];
            for (int k = 0; k < NS; k++) jag[k] = R(rng, 0.78f, 1.22f);
            float ledgeA = R(rng, 0.3f, 0.45f), ledgeB = R(rng, 0.6f, 0.75f);
            for (int i = 0; i < NR; i++)
            {
                float t = i / (float)(NR - 1);
                float prof = style == 1
                    ? 1f - 0.28f * t - (t > ledgeA ? 0.12f : 0f) - (t > ledgeB ? 0.12f : 0f) - (t > 0.92f ? 0.2f : 0f)
                    : 1f - 0.82f * Mathf.Pow(t, 0.8f) + 0.08f * Mathf.Sin(Mathf.PI * t * 2.3f);
                float r = Rb * Mathf.Max(0.04f, prof) * R(rng, 0.92f, 1.06f);
                float y = H * t;
                if (style == 1 && i == NR - 1) y = H * 0.97f;
                Vector3 c = basePos + Up * y + leanDir * (lean * t * t) + new Vector3(R(rng, -0.06f, 0.06f), 0f, R(rng, -0.06f, 0.06f)) * Rb;
                float rot = R(rng, -0.12f, 0.12f);
                for (int k = 0; k < NS; k++)
                {
                    float a = rot + k * Mathf.PI * 2f / NS;
                    float f = Mathf.Lerp(jag[k], R(rng, 0.8f, 1.2f), 0.45f);
                    Vector3 off = q * new Vector3(Mathf.Cos(a) * ax, 0f, Mathf.Sin(a) * az) * r * f;
                    ring[i, k] = c + off + Up * R(rng, -0.35f, 0.35f) * H / NR;
                }
            }
            for (int i = 0; i < NR - 1; i++)
                for (int k = 0; k < NS; k++)
                {
                    int k1 = (k + 1) % NS;
                    Vector3 a = ring[i, k], b = ring[i, k1], d = ring[i + 1, k1], e = ring[i + 1, k];
                    Vector3 mid = (a + b + d + e) * 0.25f;
                    Vector3 outward = mid - (basePos + Up * (mid.y - basePos.y));
                    Vector3 nrm = Vector3.Cross(d - a, b - a).normalized;
                    if (Vector3.Dot(nrm, outward) < 0f) nrm = -nrm;
                    int hsh = (k * 7 + i * 13 + (int)(basePos.x * 0.3f)) % 5;
                    Color32 col = hsh == 0 ? RockA : (hsh == 1 ? RockB : (hsh == 2 ? RockC : (hsh == 3 ? C(100, 100, 112) : C(122, 120, 128))));
                    float snowY = style == 1 ? 0.62f : 0.42f;
                    if (nrm.y > snowY) col = nrm.y > snowY + 0.18f ? Snow : SnowShade;
                    else if (style == 0 && nrm.y > 0.25f && ((k + i) & 1) == 0) col = SnowShade;
                    // north faces hold more snow
                    else if (nrm.z > 0.55f && nrm.y > 0.05f && i > 1) col = SnowBlue;
                    g.QuadOut(a, b, d, e, outward, col, col, col, col);
                }
            Vector3 topC = Vector3.zero;
            for (int k = 0; k < NS; k++) topC += ring[NR - 1, k];
            topC /= NS;
            Vector3 apex = topC + Up * (style == 1 ? Rb * 0.05f : Rb * 0.25f);
            for (int k = 0; k < NS; k++)
                g.TriOut(ring[NR - 1, k], ring[NR - 1, (k + 1) % NS], apex, Up, Snow, Snow, SnowShade);
            // a couple of satellite rocks around the base
            if (style == 0)
                for (int i = 0; i < 2; i++)
                {
                    float a = R(rng, 0f, Mathf.PI * 2f);
                    Vector3 c = basePos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Rb * R(rng, 0.9f, 1.3f) + Up * Rb * 0.2f;
                    float rr = Rb * R(rng, 0.25f, 0.4f);
                    PG.Boulder(g, c, new Vector3(rr, rr * 0.8f, rr), RockA, RockB, R(rng, 0f, 40f));
                    g.Ellipsoid(c + Up * rr * 0.62f, new Vector3(rr * 0.75f, rr * 0.3f, rr * 0.75f), 6, 2, Snow, SnowShade);
                }
        }

        // ------------------------------------------------------------------ buildings
        /// <summary>
        /// Alpine chalet facing -Z (door side): stone plinth, log walls, steep snow-loaded gable roof with icicles,
        /// warm glowing windows, balcony, stone chimney. chimneyTop returns the smoke point (model space).
        /// </summary>
        public static PropModel Chalet(float hx, float hz, float wallH, int variant, System.Random rng, out Vector3 chimneyTop)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var glow = m[PK.Glow];
            Color32 wood = variant % 3 == 0 ? Log : (variant % 3 == 1 ? C(150, 98, 62) : C(116, 74, 50));
            Color32 trim = variant % 2 == 0 ? C(176, 44, 40) : C(46, 92, 74);
            Color32 stone = C(150, 148, 146), stoneD = C(118, 116, 116);
            float plinth = 0.9f;
            PG.Box2(g, new Vector3(0f, plinth * 0.5f - 0.4f, 0f), new Vector3(hx + 0.15f, plinth * 0.5f + 0.4f, hz + 0.15f), stone, stoneD);
            float y0 = plinth, y1 = plinth + wallH;
            // log walls with horizontal log lines
            PG.Box2(g, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(hx, wallH * 0.5f, hz), wood, wood);
            for (float y = y0 + 0.35f; y < y1; y += 0.42f)
            {
                PG.BoxMM(g, new Vector3(-hx - 0.04f, y, -hz - 0.04f), new Vector3(hx + 0.04f, y + 0.07f, -hz + 0.02f), LogDark);
                PG.BoxMM(g, new Vector3(-hx - 0.04f, y, hz - 0.02f), new Vector3(hx + 0.04f, y + 0.07f, hz + 0.04f), LogDark);
            }
            // corner log ends
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.BoxMM(g, new Vector3(sx * hx - 0.18f, y0, sz * hz - 0.18f), new Vector3(sx * hx + 0.18f, y1, sz * hz + 0.18f), LogLight);
            // gable roof along X (ridge along x), steep
            float rise = hz * 1.05f, over = 0.7f;
            float ridgeY = y1 + rise;
            Color32 roofC = C(64, 58, 60);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 eave = new Vector3(0f, y1 - over * rise / hz, s * (hz + over));
                Vector3 ridge = new Vector3(0f, ridgeY, 0f);
                // roof slab (dark underside) + thick snow blanket on top
                Vector3 n = new Vector3(0f, hz, s * rise).normalized;
                Vector3 hxv = new Vector3(hx + over, 0f, 0f);
                g.QuadOut(eave - hxv, eave + hxv, ridge + hxv, ridge - hxv, -n, roofC, roofC, roofC, roofC);
                Vector3 lift = n * 0.42f;
                Color32 sw = Snow, se = SnowShade;
                g.QuadOut(eave - hxv - hxv.normalized * 0.1f + lift, eave + hxv + hxv.normalized * 0.1f + lift, ridge + hxv + hxv.normalized * 0.1f + lift, ridge - hxv - hxv.normalized * 0.1f + lift, n, se, se, sw, sw);
                // rounded snow edge hanging over the eave
                g.Frustum(eave - hxv - Vector3.right * 0.1f + lift * 0.5f, eave + hxv + Vector3.right * 0.1f + lift * 0.5f, 0.26f, 0.26f, 0.26f, 0.26f, 6, 0f, se, se, true, true);
                // icicles
                for (float x = -hx - over + 0.3f; x < hx + over - 0.2f; x += R(rng, 0.35f, 0.8f))
                {
                    float L = R(rng, 0.25f, 0.9f);
                    Vector3 top = new Vector3(x, eave.y + lift.y * 0.2f - 0.15f, eave.z + s * 0.05f);
                    g.Frustum(top, top - Up * L, 0.05f, 0.05f, 0.005f, 0.005f, 3, 0f, IceWhite, IceC, false, false);
                }
                // fascia board in the trim colour
                PG.Beam(g, eave - hxv + Up * 0.02f, eave + hxv + Up * 0.02f, 0.06f, 0.16f, trim);
            }
            // gable triangles (wood) at both ends
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * hx;
                g.TriOut(new Vector3(x, y1, -hz), new Vector3(x, y1, hz), new Vector3(x, ridgeY - 0.1f, 0f), Vector3.right * s, wood, wood, LogLight);
                // gable window (glowing) + frame
                Vector3 wc = new Vector3(x + s * 0.06f, y1 + rise * 0.38f, 0f);
                GlowWindow(g, glow, wc, Vector3.right * s, 0.55f, 0.65f, trim);
            }
            // front (-Z) and back windows, door
            int nw = Mathf.Max(1, Mathf.FloorToInt(hx / 1.7f));
            for (int i = 0; i < nw; i++)
            {
                float x = -hx + (i + 0.5f) * (2f * hx / nw);
                if (Mathf.Abs(x) < 0.9f && nw > 1) x += 1.2f;
                GlowWindow(g, glow, new Vector3(x, y0 + wallH * 0.55f, -hz - 0.06f), Vector3.back, 0.5f, 0.6f, trim);
                GlowWindow(g, glow, new Vector3(x, y0 + wallH * 0.55f, hz + 0.06f), Vector3.forward, 0.5f, 0.6f, trim);
            }
            PG.BoxMM(g, new Vector3(-0.55f, y0, -hz - 0.1f), new Vector3(0.55f, y0 + 2.05f, -hz + 0.02f), LogDark);
            PG.BoxMM(g, new Vector3(-0.42f, y0, -hz - 0.13f), new Vector3(0.42f, y0 + 1.92f, -hz - 0.08f), trim);
            glow.QuadOut(new Vector3(-0.12f, y0 + 1.95f, -hz - 0.15f), new Vector3(0.12f, y0 + 1.95f, -hz - 0.15f),
                         new Vector3(0.12f, y0 + 2.2f, -hz - 0.15f), new Vector3(-0.12f, y0 + 2.2f, -hz - 0.15f), Vector3.back, WarmHot, WarmHot, WarmHot, WarmHot);
            // steps with snow
            PG.Box2(g, new Vector3(0f, y0 - 0.25f, -hz - 0.7f), new Vector3(0.8f, 0.25f, 0.5f), stoneD, Snow);
            // balcony on the -X gable
            if (variant != 2)
            {
                float by = y1 - 0.1f;
                PG.Box2(g, new Vector3(-hx - 0.8f, by, 0f), new Vector3(0.8f, 0.08f, hz * 0.8f), LogDark, Snow);
                for (float z = -hz * 0.8f; z <= hz * 0.8f + 0.01f; z += hz * 0.4f)
                    PG.Beam(g, new Vector3(-hx - 1.5f, by, z), new Vector3(-hx - 1.5f, by + 1f, z), 0.05f, 0.05f, wood);
                PG.Beam(g, new Vector3(-hx - 1.5f, by + 1f, -hz * 0.8f), new Vector3(-hx - 1.5f, by + 1f, hz * 0.8f), 0.07f, 0.07f, LogLight);
                PG.Beam(g, new Vector3(-hx - 1.5f, by + 1.12f, -hz * 0.8f), new Vector3(-hx - 1.5f, by + 1.12f, hz * 0.8f), 0.08f, 0.04f, Snow);
                PG.Beam(g, new Vector3(-hx - 1.45f, by - 1.5f, -hz * 0.75f), new Vector3(-hx - 0.2f, by - 0.05f, -hz * 0.75f), 0.06f, 0.06f, LogDark);
                PG.Beam(g, new Vector3(-hx - 1.45f, by - 1.5f, hz * 0.75f), new Vector3(-hx - 0.2f, by - 0.05f, hz * 0.75f), 0.06f, 0.06f, LogDark);
            }
            // stone chimney through the roof on the +X half
            float cx = hx * 0.45f, cz = hz * 0.35f;
            float roofAt = ridgeY - rise * (cz / hz);
            float cTop = ridgeY + 1.1f;
            PG.Box2(g, new Vector3(cx, (roofAt - 0.5f + cTop) * 0.5f, cz), new Vector3(0.42f, (cTop - roofAt + 0.5f) * 0.5f, 0.42f), stone, stoneD);
            PG.Box2(g, new Vector3(cx, cTop + 0.08f, cz), new Vector3(0.52f, 0.1f, 0.52f), stoneD, Snow);
            int sc = g.v.Count;
            g.Ellipsoid(new Vector3(cx, cTop + 0.22f, cz), new Vector3(0.45f, 0.12f, 0.45f), 6, 2, Snow, Snow);
            PG.Lumpy(g, sc, new Vector3(cx, cTop + 0.2f, cz), 0.1f, variant);
            chimneyTop = new Vector3(cx, cTop + 0.45f, cz);
            // firewood stack + snow drift against the back wall
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 5 - i; j++)
                    g.Frustum(new Vector3(hx * 0.3f + j * 0.28f + i * 0.14f, y0 - 0.6f + 0.15f + i * 0.24f, hz + 0.25f),
                              new Vector3(hx * 0.3f + j * 0.28f + i * 0.14f, y0 - 0.6f + 0.15f + i * 0.24f, hz + 0.85f), 0.12f, 0.12f, 0.12f, 0.12f, 5, 0f, LogLight, LogLight, true, true);
            int sd = g.v.Count;
            g.Ellipsoid(new Vector3(-hx * 0.3f, y0 - 0.6f, hz + 0.6f), new Vector3(hx * 0.6f, 0.7f, 0.9f), 7, 3, Snow, SnowShade);
            PG.Lumpy(g, sd, new Vector3(-hx * 0.3f, y0 - 0.6f, hz + 0.6f), 0.12f, variant * 3f);
            return m;
        }

        /// <summary>Warm window: glowing pane, dark mullion cross, frame + snowy sill. Faces 'outward'.</summary>
        public static void GlowWindow(LifeGeo g, LifeGeo glow, Vector3 c, Vector3 outward, float hw, float hh, Color32 frame)
        {
            Vector3 side = Vector3.Cross(Up, outward).normalized;
            Vector3 up = Up;
            Vector3 o = outward * 0.02f;
            glow.QuadOut(c - side * hw - up * hh + o, c + side * hw - up * hh + o, c + side * hw + up * hh + o, c - side * hw + up * hh + o, outward, Warm, Warm, WarmHot, WarmHot);
            Vector3 o2 = outward * 0.05f;
            PG.Beam(g, c - up * hh + o2, c + up * hh + o2, 0.03f, 0.03f, LogDark);
            PG.Beam(g, c - side * hw + o2, c + side * hw + o2, 0.03f, 0.03f, LogDark);
            PG.Beam(g, c - side * (hw + 0.1f) - up * (hh + 0.08f) + o2, c + side * (hw + 0.1f) - up * (hh + 0.08f) + o2, 0.08f, 0.06f, frame);
            PG.Beam(g, c - side * (hw + 0.1f) + up * (hh + 0.08f) + o2, c + side * (hw + 0.1f) + up * (hh + 0.08f) + o2, 0.08f, 0.06f, frame);
            PG.Beam(g, c - side * (hw + 0.1f) - up * (hh + 0.08f) + o2, c - side * (hw + 0.1f) + up * (hh + 0.08f) + o2, 0.06f, 0.06f, frame);
            PG.Beam(g, c + side * (hw + 0.1f) - up * (hh + 0.08f) + o2, c + side * (hw + 0.1f) + up * (hh + 0.08f) + o2, 0.06f, 0.06f, frame);
            PG.Beam(g, c - side * (hw + 0.12f) - up * (hh + 0.02f) + outward * 0.12f, c + side * (hw + 0.12f) - up * (hh + 0.02f) + outward * 0.12f, 0.12f, 0.05f, Snow);
        }

        /// <summary>Summit observatory: drum building, silver dome with an open slit and the telescope, annex, mast and dish.</summary>
        public static PropModel Observatory(out Vector3 beacon)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var glow = m[PK.Glow];
            Color32 wall = C(232, 230, 222), wallD = C(198, 196, 190), dome = C(214, 222, 234), domeD = C(168, 178, 194), band = C(44, 74, 120);
            const int N = 18;
            float R0 = 7.5f, H0 = 7f;
            g.Frustum(Vector3.down * 1.5f, Up * 0.6f, R0 + 0.8f, R0 + 0.8f, R0 + 0.5f, R0 + 0.5f, N, 0f, C(130, 130, 134), C(160, 160, 164), false, true);
            g.Frustum(Up * 0.6f, Up * H0, R0, R0, R0, R0, N, 0f, wallD, wall, false, false);
            g.Frustum(Up * (H0 - 0.6f), Up * H0, R0 + 0.12f, R0 + 0.12f, R0 + 0.12f, R0 + 0.12f, N, 0f, band, band, false, false);
            // windows ring
            for (int k = 0; k < N; k += 2)
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / N;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (Vector3.Dot(d, Vector3.back) > 0.94f) continue;
                GlowWindow(g, glow, d * (R0 + 0.02f) + Up * 3.4f, d, 0.45f, 0.75f, band);
            }
            // door (-Z)
            PG.BoxMM(g, new Vector3(-1f, 0.6f, -R0 - 0.3f), new Vector3(1f, 3f, -R0 + 0.3f), band);
            glow.QuadOut(new Vector3(-0.7f, 0.7f, -R0 - 0.33f), new Vector3(0.7f, 0.7f, -R0 - 0.33f), new Vector3(0.7f, 2.7f, -R0 - 0.33f), new Vector3(-0.7f, 2.7f, -R0 - 0.33f), Vector3.back, Warm, Warm, WarmHot, WarmHot);
            // dome: hemisphere rings with a dark observing slit facing south-east, snow on its shoulders
            int lat = 6;
            float slitA = -0.75f;
            for (int j = 0; j < lat; j++)
            {
                float t0 = Mathf.PI * 0.5f * j / lat, t1 = Mathf.PI * 0.5f * (j + 1) / lat;
                for (int k = 0; k < N; k++)
                {
                    float a0 = k * Mathf.PI * 2f / N, a1 = (k + 1) * Mathf.PI * 2f / N;
                    float am = (a0 + a1) * 0.5f;
                    float da = Mathf.Abs(Mathf.DeltaAngle(am * Mathf.Rad2Deg, slitA * Mathf.Rad2Deg));
                    Vector3 p00 = DomeP(R0 + 0.15f, H0, t0, a0), p01 = DomeP(R0 + 0.15f, H0, t0, a1), p10 = DomeP(R0 + 0.15f, H0, t1, a0), p11 = DomeP(R0 + 0.15f, H0, t1, a1);
                    Color32 col = (k & 1) == 0 ? dome : Sh(dome, 0.95f);
                    if (da < 12f && j >= 1) col = C(24, 30, 44);
                    else if (j >= 2 && Mathf.Sin(am * 3f) > 0.2f && j < 5) col = Snow;
                    g.QuadOut(p00, p01, p11, p10, (p00 + p11) * 0.5f - Up * H0, col, col, col, col);
                }
            }
            // telescope tube poking out of the slit
            Vector3 dir = new Vector3(Mathf.Cos(slitA), 0.9f, Mathf.Sin(slitA)).normalized;
            Vector3 tc = Up * (H0 + 2.5f);
            g.Frustum(tc - dir * 2f, tc + dir * 6.4f, 0.75f, 0.75f, 0.85f, 0.85f, 10, 0f, C(236, 236, 240), C(250, 250, 252), true, false);
            g.Frustum(tc + dir * 6.3f, tc + dir * 6.9f, 0.95f, 0.95f, 0.95f, 0.95f, 10, 0f, C(40, 44, 54), C(40, 44, 54), false, false);
            g.Disc(tc + dir * 6.5f, 0.78f, 10, 0f, dir, C(28, 40, 64), C(70, 110, 160));
            // annex with flat roof + snow, glowing windows
            PG.Box2(g, new Vector3(9.5f, 2.2f, 4f), new Vector3(4f, 2.2f, 3f), wall, Snow);
            PG.Box2(g, new Vector3(9.5f, 4.55f, 4f), new Vector3(4.3f, 0.2f, 3.3f), band, Snow);
            for (int i = 0; i < 3; i++) GlowWindow(g, glow, new Vector3(7f + i * 2.4f, 2.6f, 0.95f), Vector3.back, 0.6f, 0.55f, band);
            // small secondary dome
            g.Frustum(new Vector3(-10f, 0f, 6f), new Vector3(-10f, 3.2f, 6f), 3f, 3f, 3f, 3f, 12, 0f, wallD, wall, false, false);
            for (int j = 0; j < 4; j++)
            {
                float t0 = Mathf.PI * 0.5f * j / 4, t1 = Mathf.PI * 0.5f * (j + 1) / 4;
                for (int k = 0; k < 12; k++)
                {
                    float a0 = k * Mathf.PI * 2f / 12, a1 = (k + 1) * Mathf.PI * 2f / 12;
                    Vector3 off = new Vector3(-10f, 0f, 6f);
                    Vector3 p00 = off + DomeP(3.1f, 3.2f, t0, a0), p01 = off + DomeP(3.1f, 3.2f, t0, a1), p10 = off + DomeP(3.1f, 3.2f, t1, a0), p11 = off + DomeP(3.1f, 3.2f, t1, a1);
                    Color32 col = j >= 2 ? Snow : domeD;
                    g.QuadOut(p00, p01, p11, p10, (p00 + p11) * 0.5f - off - Up * 3.2f, col, col, col, col);
                }
            }
            // lattice radio mast with dish
            Vector3 mb = new Vector3(13f, 0f, 9f);
            float mh = 22f;
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                Vector3 off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                PG.Rod(g, mb + off * 0.9f, mb + off * 0.25f + Up * mh, 0.07f, k == 0 ? C(220, 60, 50) : Steel);
            }
            for (float y = 1.5f; y < mh; y += 2.5f)
            {
                float w = Mathf.Lerp(0.9f, 0.25f, y / mh);
                for (int k = 0; k < 3; k++)
                {
                    float a = k * Mathf.PI * 2f / 3f, b = (k + 1) * Mathf.PI * 2f / 3f;
                    PG.Rod(g, mb + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * w + Up * y, mb + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * w + Up * (y + 1.2f), 0.04f, Steel, 3);
                }
            }
            Vector3 dc = mb + Up * 13f + new Vector3(-0.8f, 0f, -0.6f);
            Vector3 dn = new Vector3(-0.6f, 0.5f, -0.6f).normalized;
            g.Frustum(dc, dc + dn * 0.7f, 0.25f, 0.25f, 1.8f, 1.8f, 12, 0f, Steel, C(236, 238, 242), true, false);
            beacon = mb + Up * (mh + 0.4f);
            // weather station + railing ring around the drum
            for (int k = 0; k < 24; k++)
            {
                float a = k * Mathf.PI * 2f / 24f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (R0 + 2.2f);
                if (Vector3.Dot(p.normalized, Vector3.back) > 0.93f) continue;
                PG.Rod(g, p, p + Up * 1.1f, 0.04f, SteelDark);
                float a2 = (k + 1) * Mathf.PI * 2f / 24f;
                Vector3 p2 = new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)) * (R0 + 2.2f);
                if (Vector3.Dot(p2.normalized, Vector3.back) > 0.93f) continue;
                PG.Rod(g, p + Up * 1.1f, p2 + Up * 1.1f, 0.04f, SteelDark);
            }
            return m;
        }

        static Vector3 DomeP(float r, float y0, float th, float a)
        {
            return new Vector3(Mathf.Cos(th) * Mathf.Cos(a) * r, y0 + Mathf.Sin(th) * r * 0.92f, Mathf.Cos(th) * Mathf.Sin(a) * r);
        }

        // ------------------------------------------------------------------ wind turbine
        public const float TurbineHub = 46f;

        /// <summary>Turbine tower + nacelle (nacelle faces -Z towards the wind; hub at (0, TurbineHub, -2.6)).</summary>
        public static PropModel TurbineTower()
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 w = C(240, 242, 246), wd = C(206, 210, 218);
            g.Frustum(Vector3.down * 1f, Up * 0.6f, 3.2f, 3.2f, 3.0f, 3.0f, 10, 0f, C(150, 150, 154), C(176, 176, 180), false, true);
            g.Frustum(Up * 0.6f, Up * (TurbineHub - 1.4f), 1.9f, 1.9f, 1.05f, 1.05f, 12, 0f, wd, w, false, false);
            PG.Box2(g, new Vector3(0f, TurbineHub, 0.6f), new Vector3(1.3f, 1.35f, 3.2f), w, w);
            g.Frustum(new Vector3(0f, TurbineHub, -2.6f), new Vector3(0f, TurbineHub, -2.65f), 1.3f, 1.3f, 1.3f, 1.3f, 10, 0f, wd, wd, false, false);
            PG.BoxMM(g, new Vector3(-0.2f, 0.6f, -1.95f), new Vector3(0.2f, 2.8f, -1.75f), C(150, 156, 166));
            // red band near the base, logo stripe
            g.Frustum(Up * 8f, Up * 9.2f, 1.75f, 1.75f, 1.72f, 1.72f, 12, 0f, C(196, 52, 46), C(196, 52, 46), false, false);
            return m;
        }

        /// <summary>Rotor: hub nose + three blades in the XY plane (spins around Z).</summary>
        public static LifeGeo Rotor()
        {
            var g = new LifeGeo();
            Color32 w = C(244, 246, 250), wd = C(214, 218, 226), tip = C(214, 60, 52);
            g.Frustum(Vector3.zero, Vector3.back * 2.2f, 1.25f, 1.25f, 0.1f, 0.1f, 10, 0f, w, w, true, false);
            const float L = 21f;
            for (int b = 0; b < 3; b++)
            {
                float a = b * Mathf.PI * 2f / 3f;
                Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f), s = new Vector3(-d.y, d.x, 0f);
                Vector3 r0 = d * 0.9f, r1 = d * 4f, r2 = d * (L - 2f), r3 = d * L;
                float c0 = 0.45f, c1 = 1.3f, c2 = 0.45f;
                Vector3 tw = Vector3.back * 0.12f;
                // blade as two-sided quads (chord twisted slightly)
                PG.Quad2(g, r0 - s * c0, r1 - s * c1 * 0.4f + tw, r1 + s * c1, r0 + s * c0, wd, w, w, wd);
                PG.Quad2(g, r1 - s * c1 * 0.4f + tw, r2 - s * c2 * 0.3f + tw * 2f, r2 + s * c2, r1 + s * c1, w, w, w, w);
                PG.Quad2(g, r2 - s * c2 * 0.3f + tw * 2f, r3 + tw * 2f, r3 + s * 0.15f, r2 + s * c2, tip, tip, tip, tip);
                g.Frustum(r0, r1, 0.4f, 0.25f, 0.25f, 0.15f, 5, 0f, w, w, false, false);
            }
            return g;
        }

        // ------------------------------------------------------------------ cable car
        /// <summary>Cable-car station (open bull-wheel bay facing +Z = up the line), snow on the roof.</summary>
        public static PropModel CableStation(bool top, out Vector3 wheel)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var glow = m[PK.Glow];
            Color32 wall = top ? C(214, 92, 64) : C(64, 104, 150), wallD = Sh(wall, 0.82f), con = C(170, 168, 166);
            float hx = 7f, hz = 6f, H = 6.5f;
            PG.Box2(g, new Vector3(0f, 0.3f, 0f), new Vector3(hx + 0.6f, 1f, hz + 0.6f), con, Snow);
            PG.Box2(g, new Vector3(0f, H * 0.5f + 0.8f, -hz * 0.4f), new Vector3(hx, H * 0.5f, hz * 0.6f), wall, wall);
            // big roof over the bay, sloping up the line
            Vector3 a0 = new Vector3(-hx - 0.6f, H + 1.3f, -hz - 0.6f), a1 = new Vector3(hx + 0.6f, H + 1.3f, -hz - 0.6f);
            Vector3 b1 = new Vector3(hx + 0.6f, H + 2.4f, hz + 1.5f), b0 = new Vector3(-hx - 0.6f, H + 2.4f, hz + 1.5f);
            g.QuadOut(a0, a1, b1, b0, Vector3.down, C(56, 56, 60), C(56, 56, 60), C(56, 56, 60), C(56, 56, 60));
            Vector3 l = Up * 0.35f;
            g.QuadOut(a0 + l, a1 + l, b1 + l, b0 + l, Up, Snow, Snow, SnowShade, SnowShade);
            PG.Beam(g, a0 + l * 0.5f, a1 + l * 0.5f, 0.2f, 0.2f, SnowShade);
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Beam(g, new Vector3(s * hx, 0.8f, hz + 1f), new Vector3(s * hx, H + 2.3f, hz + 1f), 0.25f, 0.25f, wallD);
                PG.Beam(g, new Vector3(s * hx, 0.8f, 0f), new Vector3(s * hx, H + 1.8f, 0f), 0.25f, 0.25f, wallD);
            }
            // bull wheel (horizontal) + housing
            wheel = new Vector3(0f, H - 0.4f, hz * 0.5f);
            g.Frustum(wheel - Up * 0.2f, wheel + Up * 0.2f, 3.4f, 3.4f, 3.4f, 3.4f, 16, 0f, SteelDark, Steel, true, true);
            PG.Beam(g, wheel, wheel + Up * (H + 2f - wheel.y), 0.35f, 0.35f, SteelDark);
            // windows + sign band
            for (int i = 0; i < 4; i++)
                GlowWindow(g, glow, new Vector3(-hx + 1.6f + i * 3.6f, 3.6f, -hz - 0.02f - hz * 0.4f + hz * 0.4f - 0.0f - 0f), Vector3.back, 0.9f, 0.7f, wallD);
            PG.BoxMM(g, new Vector3(-4f, H + 0.2f, -hz - 0.08f - 0.0f), new Vector3(4f, H + 1f, -hz + 0.05f), C(246, 240, 226));
            PG.BoxMM(g, new Vector3(-3.6f, H + 0.35f, -hz - 0.12f), new Vector3(3.6f, H + 0.85f, -hz - 0.07f), wall);
            return m;
        }

        /// <summary>Steel lattice pylon of height H with a cross-arm (cables at x = +-armHalf at the top).</summary>
        public static PropModel Pylon(float H, float armHalf)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            Color32 s = C(190, 196, 206), sd = C(130, 138, 150);
            float b = 1.6f + H * 0.04f, t = 0.55f;
            var leg = new Vector3[4];
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? -1f : 1f, sz = (k & 2) == 0 ? -1f : 1f;
                leg[k] = new Vector3(sx, 0f, sz);
                PG.Rod(g, new Vector3(sx * b, -0.5f, sz * b), new Vector3(sx * t, H, sz * t), 0.12f, sd, 4);
            }
            for (float y = 2f; y < H - 0.5f; y += 3f)
            {
                float w0 = Mathf.Lerp(b, t, y / H), w1 = Mathf.Lerp(b, t, (y + 3f) / H);
                for (int e = 0; e < 4; e++)
                {
                    Vector3 c0 = Corner(e, w0, y), c1 = Corner(e + 1, w1, y + 3f), c2 = Corner(e + 1, w0, y);
                    PG.Rod(g, c0, c1, 0.05f, s, 3);
                    PG.Rod(g, c0, c2, 0.05f, s, 3);
                }
            }
            PG.Beam(g, new Vector3(-armHalf - 0.6f, H + 0.3f, 0f), new Vector3(armHalf + 0.6f, H + 0.3f, 0f), 0.25f, 0.35f, C(232, 112, 40));
            for (int sx = -1; sx <= 1; sx += 2)
            {
                PG.BoxMM(g, new Vector3(sx * armHalf - 0.15f, H - 0.3f, -1.4f), new Vector3(sx * armHalf + 0.15f, H + 0.05f, 1.4f), sd);
                for (int w = -1; w <= 1; w++)
                    g.Frustum(new Vector3(sx * armHalf - 0.18f, H - 0.15f, w * 0.9f), new Vector3(sx * armHalf + 0.18f, H - 0.15f, w * 0.9f), 0.32f, 0.32f, 0.32f, 0.32f, 8, 0f, Black, Black, true, true);
            }
            // snow on the arm, ladder rungs feel
            PG.Beam(g, new Vector3(-armHalf - 0.6f, H + 0.68f, 0f), new Vector3(armHalf + 0.6f, H + 0.68f, 0f), 0.22f, 0.05f, Snow);
            return m;
        }

        static Vector3 Corner(int e, float w, float y)
        {
            switch (e & 3)
            {
                case 0: return new Vector3(-w, y, -w);
                case 1: return new Vector3(w, y, -w);
                case 2: return new Vector3(w, y, w);
                default: return new Vector3(-w, y, w);
            }
        }

        /// <summary>Gondola cabin hanging below its grip at the origin (faces +Z / -Z).</summary>
        public static LifeGeo Gondola(Color32 body)
        {
            var g = new LifeGeo();
            Color32 bodyD = Sh(body, 0.8f), glass = C(48, 70, 96), roof = C(232, 236, 242);
            PG.Rod(g, Vector3.zero, Up * -2.4f, 0.07f, SteelDark);
            PG.Beam(g, new Vector3(-0.3f, 0.05f, 0f), new Vector3(0.3f, 0.05f, 0f), 0.12f, 0.15f, Black);
            PG.Beam(g, new Vector3(0f, -2.4f, -0.5f), new Vector3(0f, -2.4f, 0.5f), 0.06f, 0.06f, SteelDark);
            float y0 = -4.9f, y1 = -2.5f;
            PG.Box2(g, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(1.05f, (y1 - y0) * 0.5f, 1.1f), body, roof);
            PG.BoxMM(g, new Vector3(-1.08f, y0 + 1.0f, -0.95f), new Vector3(1.08f, y1 - 0.3f, 0.95f), glass);
            PG.BoxMM(g, new Vector3(-0.92f, y0 + 1.0f, -1.13f), new Vector3(0.92f, y1 - 0.3f, 1.13f), glass);
            PG.BoxMM(g, new Vector3(-1.1f, y0 - 0.05f, -1.15f), new Vector3(1.1f, y0 + 0.35f, 1.15f), bodyD);
            PG.BoxMM(g, new Vector3(-1.12f, y1 - 0.1f, -1.17f), new Vector3(1.12f, y1 + 0.12f, 1.17f), roof);
            return g;
        }

        // ------------------------------------------------------------------ roadside
        /// <summary>Orange-and-black banded snow pole with a reflector (Props).</summary>
        public static PropModel SnowPole()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            const float H = 2.4f;
            int bands = 8;
            for (int i = 0; i < bands; i++)
            {
                float y0 = -0.3f + (H + 0.3f) * i / bands, y1 = -0.3f + (H + 0.3f) * (i + 1) / bands;
                Color32 c = i >= bands - 3 ? ((i & 1) == 0 ? Orange : Black) : Orange;
                g.Frustum(Up * y0, Up * y1, 0.05f, 0.05f, 0.05f, 0.05f, 4, 0.3f, c, c, false, i == bands - 1);
            }
            PG.BoxMM(g, new Vector3(-0.055f, H - 0.75f, -0.07f), new Vector3(0.055f, H - 0.62f, 0.07f), C(250, 236, 120));
            return m;
        }

        /// <summary>Wooden slat snow fence section along X (length L), red-brown boards, snow drift at its foot.</summary>
        public static PropModel SnowFence(float L, System.Random rng, bool orange)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 post = C(96, 70, 52), slat = orange ? C(236, 104, 34) : C(150, 70, 52);
            int posts = Mathf.Max(2, Mathf.RoundToInt(L / 2.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = -L * 0.5f + L * i / (posts - 1);
                PG.Beam(g, new Vector3(x, -0.4f, 0f), new Vector3(x + R(rng, -0.05f, 0.05f), 1.75f, 0f), 0.06f, 0.06f, post);
            }
            if (orange)
            {
                // plastic mesh fence: a band with holes suggested by darker stripes
                for (float y = 0.2f; y < 1.4f; y += 0.28f)
                    PG.Beam(g, new Vector3(-L * 0.5f, y, 0.05f), new Vector3(L * 0.5f, y, 0.05f), 0.015f, 0.09f, slat);
            }
            else
            {
                PG.Beam(g, new Vector3(-L * 0.5f, 0.45f, 0.06f), new Vector3(L * 0.5f, 0.45f, 0.06f), 0.03f, 0.05f, post);
                PG.Beam(g, new Vector3(-L * 0.5f, 1.35f, 0.06f), new Vector3(L * 0.5f, 1.35f, 0.06f), 0.03f, 0.05f, post);
                for (float x = -L * 0.5f + 0.1f; x < L * 0.5f; x += 0.22f)
                    PG.BoxMM(g, new Vector3(x - 0.05f, 0.05f + R(rng, 0f, 0.1f), 0.08f), new Vector3(x + 0.05f, 1.6f + R(rng, -0.1f, 0.05f), 0.11f), (int)(x * 7f) % 3 == 0 ? Sh(slat, 0.86f) : slat);
            }
            // drift piled against the windward side + snow on the top rail
            int sd = g.v.Count;
            g.Ellipsoid(new Vector3(0f, -0.15f, -0.35f), new Vector3(L * 0.55f, 0.6f, 0.9f), 9, 3, Snow, SnowShade);
            PG.Lumpy(g, sd, new Vector3(0f, -0.15f, -0.35f), 0.1f, R(rng, 0f, 20f));
            PG.Beam(g, new Vector3(-L * 0.5f, 1.66f, 0.08f), new Vector3(L * 0.5f, 1.66f, 0.08f), 0.06f, 0.04f, Snow);
            return m;
        }

        /// <summary>Curve chevron sign (orange / black) on a post, facing -Z.</summary>
        public static PropModel Chevron(bool left)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            PG.Beam(g, new Vector3(0f, -0.3f, 0.05f), new Vector3(0f, 1.6f, 0.05f), 0.05f, 0.05f, SteelDark);
            PG.BoxMM(g, new Vector3(-0.45f, 1.2f, -0.02f), new Vector3(0.45f, 1.9f, 0.02f), Orange);
            float s = left ? -1f : 1f;
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.28f;
                Vector3 a = new Vector3(x - 0.08f * s, 1.28f, -0.03f), b = new Vector3(x + 0.08f * s, 1.55f, -0.03f), c = new Vector3(x - 0.08f * s, 1.82f, -0.03f);
                PG.Beam(g, a, b, 0.045f, 0.01f, Black);
                PG.Beam(g, b, c, 0.045f, 0.01f, Black);
            }
            PG.BoxMM(g, new Vector3(-0.47f, 1.9f, -0.04f), new Vector3(0.47f, 1.98f, 0.06f), Snow);
            return m;
        }

        /// <summary>Lamp post with a warm lantern (village / viewpoints).</summary>
        public static PropModel Lamp()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var glow = m[PK.Glow];
            Color32 iron = C(44, 46, 52);
            g.Frustum(Vector3.down * 0.3f, Up * 3.6f, 0.09f, 0.09f, 0.06f, 0.06f, 6, 0f, iron, iron, false, false);
            PG.Beam(g, Up * 3.5f, new Vector3(0f, 3.5f, 0.55f), 0.04f, 0.04f, iron);
            Vector3 c = new Vector3(0f, 3.15f, 0.55f);
            glow.Frustum(c - Up * 0.25f, c + Up * 0.25f, 0.16f, 0.16f, 0.2f, 0.2f, 6, 0f, Warm, WarmHot, true, true);
            g.Frustum(c + Up * 0.25f, c + Up * 0.45f, 0.26f, 0.26f, 0.02f, 0.02f, 6, 0f, iron, iron, true, false);
            g.Frustum(c + Up * 0.3f, c + Up * 0.5f, 0.24f, 0.24f, 0.02f, 0.02f, 6, 0f, Snow, Snow, false, false);
            return m;
        }

        // ------------------------------------------------------------------ vehicles
        /// <summary>Snowmobile (length along Z, nose +Z).</summary>
        public static PropModel Snowmobile(Color32 body)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 bodyD = Sh(body, 0.75f), seat = C(30, 30, 34), ski = C(40, 42, 48);
            // skis curling up at the front
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Beam(g, new Vector3(s * 0.48f, 0.06f, 0.2f), new Vector3(s * 0.48f, 0.06f, 1.3f), 0.08f, 0.03f, ski);
                PG.Beam(g, new Vector3(s * 0.48f, 0.06f, 1.3f), new Vector3(s * 0.48f, 0.25f, 1.55f), 0.08f, 0.03f, ski);
                PG.Beam(g, new Vector3(s * 0.48f, 0.08f, 0.8f), new Vector3(s * 0.32f, 0.5f, 0.75f), 0.03f, 0.03f, ski);
            }
            // track tunnel
            PG.Box2(g, new Vector3(0f, 0.28f, -0.55f), new Vector3(0.36f, 0.24f, 0.85f), C(36, 36, 40), C(36, 36, 40));
            // hood
            g.Frustum(new Vector3(0f, 0.55f, 0.05f), new Vector3(0f, 0.5f, 1.15f), 0.46f, 0.32f, 0.2f, 0.12f, 6, 0f, body, bodyD, true, true);
            PG.Box2(g, new Vector3(0f, 0.55f, -0.2f), new Vector3(0.44f, 0.18f, 0.35f), body, body);
            // windshield
            g.TriOut(new Vector3(-0.32f, 0.78f, 0.35f), new Vector3(0.32f, 0.78f, 0.35f), new Vector3(0f, 1.08f, 0.15f), Vector3.forward, C(120, 150, 176), C(120, 150, 176), C(170, 196, 214));
            // seat + handlebar
            PG.Box2(g, new Vector3(0f, 0.72f, -0.75f), new Vector3(0.24f, 0.1f, 0.62f), seat, seat);
            PG.Beam(g, new Vector3(-0.35f, 0.92f, 0.1f), new Vector3(0.35f, 0.92f, 0.1f), 0.025f, 0.025f, Black);
            PG.BoxMM(g, new Vector3(-0.3f, 0.6f, -1.42f), new Vector3(0.3f, 0.75f, -1.36f), C(220, 40, 40));
            // snow dust on the seat back
            PG.Box2(g, new Vector3(0f, 0.84f, -1.1f), new Vector3(0.2f, 0.03f, 0.2f), Snow, Snow);
            return m;
        }

        /// <summary>Crashed snowplough: orange truck nosed into a drift with its V blade, amber beacon (Glow).</summary>
        public static PropModel Snowplough()
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var glow = m[PK.Glow];
            Color32 body = C(238, 136, 34), bodyD = C(196, 100, 26), tyre = C(36, 36, 40), glass = C(60, 82, 98), blade = C(246, 200, 40);
            float[] axz = { 1.9f, -1.9f };
            foreach (float z in axz)
                for (int s = -1; s <= 1; s += 2)
                    g.Frustum(new Vector3(s * 1.0f, 0.6f, z), new Vector3(s * 1.35f, 0.6f, z), 0.6f, 0.6f, 0.6f, 0.6f, 10, 0f, tyre, tyre, true, true);
            PG.Box2(g, new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 0.25f, 3.2f), C(50, 50, 54), C(50, 50, 54));
            // cab
            PG.Box2(g, new Vector3(0f, 2.1f, 1.6f), new Vector3(1.2f, 0.9f, 1.0f), body, body);
            PG.BoxMM(g, new Vector3(-1.05f, 2.2f, 2.58f), new Vector3(1.05f, 2.85f, 2.62f), glass);
            for (int s = -1; s <= 1; s += 2) PG.BoxMM(g, new Vector3(s * 1.2f - 0.02f, 2.2f, 1.0f), new Vector3(s * 1.2f + 0.02f, 2.8f, 2.3f), glass);
            PG.Box2(g, new Vector3(0f, 3.02f, 1.6f), new Vector3(1.25f, 0.06f, 1.05f), bodyD, Snow);
            // dump body with a load of grit, snow crust
            PG.Box2(g, new Vector3(0f, 1.9f, -1.5f), new Vector3(1.25f, 0.75f, 1.9f), bodyD, C(120, 100, 84));
            PG.Box2(g, new Vector3(0f, 2.68f, -1.5f), new Vector3(1.1f, 0.06f, 1.7f), Snow, Snow);
            // V blade in front
            Vector3 tip = new Vector3(0f, 0.8f, 4.6f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 side = new Vector3(s * 1.6f, 0.8f, 3.4f);
                PG.Quad2(g, tip + Up * -0.5f, side + Up * -0.5f, side + Up * 0.7f, tip + Up * 0.9f, blade, blade, Sh(blade, 0.9f), Sh(blade, 0.9f));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 a = Vector3.Lerp(tip, side, 0.15f + k * 0.3f);
                    PG.Beam(g, a + Up * -0.4f, a + Up * 0.6f, 0.05f, 0.05f, Black);
                }
            }
            PG.Beam(g, new Vector3(0f, 1.0f, 3.2f), tip, 0.12f, 0.12f, C(60, 60, 64));
            // amber beacons
            glow.Frustum(new Vector3(-0.8f, 3.08f, 2.2f), new Vector3(-0.8f, 3.38f, 2.2f), 0.13f, 0.13f, 0.11f, 0.11f, 6, 0f, C(255, 170, 40), C(255, 210, 90), true, true);
            glow.Frustum(new Vector3(0.8f, 3.08f, 2.2f), new Vector3(0.8f, 3.38f, 2.2f), 0.13f, 0.13f, 0.11f, 0.11f, 6, 0f, C(255, 170, 40), C(255, 210, 90), true, true);
            return m;
        }

        // ------------------------------------------------------------------ decor
        /// <summary>Prayer-flag line from a to b (model space), sagging, flags flutter (Strong kind).</summary>
        public static void FlagLine(PropModel m, Vector3 a, Vector3 b, System.Random rng, float sag)
        {
            var g = m[PK.Props];
            var st = m[PK.Strong];
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(4, Mathf.RoundToInt(len / 0.55f));
            Vector3 prev = a;
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(dir, Up).normalized;
            if (side.sqrMagnitude < 0.1f) side = Vector3.right;
            int start = rng.Next(5);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                Vector3 p = Vector3.Lerp(a, b, t) - Up * (sag * 4f * t * (1f - t));
                PG.Rod(g, prev, p, 0.012f, C(60, 52, 48), 3);
                if (i < n)
                {
                    Color32 c = FlagCols[(start + i) % FlagCols.Length];
                    Vector3 w = dir * 0.2f;
                    float h = R(rng, 0.32f, 0.4f);
                    Color32 top = PG.A(c, 40), bot = PG.A(Sh(c, 0.9f), 255);
                    PG.Quad2(st, p - w, p + w, p + w - Up * h + side * 0.05f, p - w - Up * h + side * 0.05f, top, top, bot, bot);
                }
                prev = p;
            }
        }

        /// <summary>Tall pole for flag lines (with a snow cap).</summary>
        public static void FlagPole(LifeGeo g, Vector3 at, float h)
        {
            g.Frustum(at - Up * 0.4f, at + Up * h, 0.07f, 0.07f, 0.05f, 0.05f, 5, 0f, LogDark, Log, false, true);
            g.Ellipsoid(at + Up * (h + 0.05f), new Vector3(0.1f, 0.06f, 0.1f), 5, 2, Snow, Snow);
        }

        /// <summary>Stone cairn (mani pile) with a snow crown.</summary>
        public static PropModel Cairn(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            float y = 0f;
            float r = R(rng, 0.7f, 1.0f);
            for (int i = 0; i < 5; i++)
            {
                float rr = r * (1f - i * 0.16f);
                PG.Boulder(g, new Vector3(R(rng, -0.08f, 0.08f), y + rr * 0.3f, R(rng, -0.08f, 0.08f)), new Vector3(rr, rr * 0.38f, rr * 0.9f), i % 2 == 0 ? RockC : RockA, RockB, R(rng, 0f, 30f));
                y += rr * 0.55f;
            }
            g.Ellipsoid(new Vector3(0f, y + 0.05f, 0f), new Vector3(r * 0.35f, 0.14f, r * 0.35f), 6, 2, Snow, SnowShade);
            return m;
        }

        /// <summary>Ice sculptures for the plaza: 0 dragon, 1 crystal obelisk, 2 crane, 3 snowman, 4 ice lantern ring.</summary>
        public static PropModel IceSculpture(int kind, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var glow = m[PK.Glow];
            Color32 ice = IceC, iceL = IceWhite, iceD = IceDeep;
            // plinth of packed snow blocks
            if (kind != 3) PG.Box2(g, new Vector3(0f, 0.25f, 0f), new Vector3(1.6f, 0.45f, 1.6f), SnowShade, Snow);
            switch (kind)
            {
                case 0:
                {
                    // serpentine dragon: a chain of ellipsoids along a sine, head with horns
                    Vector3 prev = Vector3.zero;
                    for (int i = 0; i < 12; i++)
                    {
                        float t = i / 11f;
                        Vector3 p = new Vector3(Mathf.Sin(t * Mathf.PI * 2.2f) * 1.1f, 1.0f + Mathf.Sin(t * Mathf.PI) * 1.8f + t * 0.8f, (t - 0.5f) * 3.6f);
                        float r = Mathf.Lerp(0.42f, 0.22f, Mathf.Abs(t - 0.6f) * 1.6f);
                        g.Ellipsoid(p, new Vector3(r, r, r * 1.2f), 6, 4, iceL, ice);
                        if (i > 0) g.Frustum(prev, p, r, r, r * 0.9f, r * 0.9f, 6, 0f, ice, iceL, false, false);
                        if (i % 2 == 0) g.Frustum(p + Up * r * 0.7f, p + Up * (r + 0.35f), 0.12f, 0.06f, 0.01f, 0.01f, 3, 0f, iceL, IceWhite, false, false);
                        prev = p;
                    }
                    Vector3 h = prev + new Vector3(0f, 0.2f, 0.45f);
                    g.Ellipsoid(h, new Vector3(0.38f, 0.32f, 0.6f), 7, 4, iceL, ice);
                    for (int s = -1; s <= 1; s += 2)
                        g.Frustum(h + new Vector3(s * 0.18f, 0.2f, -0.2f), h + new Vector3(s * 0.4f, 0.85f, -0.6f), 0.07f, 0.07f, 0.01f, 0.01f, 4, 0f, iceL, IceWhite, false, false);
                    glow.Ellipsoid(h + new Vector3(0f, 0.1f, 0.25f), new Vector3(0.12f, 0.12f, 0.12f), 5, 3, C(150, 220, 255), C(120, 200, 255));
                    break;
                }
                case 1:
                {
                    // faceted crystal cluster
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI * 2f / 6f + R(rng, -0.3f, 0.3f);
                        Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        float H = i == 0 ? 4.2f : R(rng, 1.4f, 2.8f);
                        Vector3 b0 = i == 0 ? Up * 0.7f : Up * 0.7f + d * 0.55f;
                        Vector3 tip = b0 + (i == 0 ? Up : (Up + d * 0.45f).normalized) * H;
                        float rr = i == 0 ? 0.55f : 0.3f;
                        g.Frustum(b0, tip - (tip - b0).normalized * rr, rr, rr, rr * 0.9f, rr * 0.9f, 6, a, iceD, iceL, false, false);
                        g.Frustum(tip - (tip - b0).normalized * rr, tip, rr * 0.9f, rr * 0.9f, 0.01f, 0.01f, 6, a, iceL, IceWhite, false, false);
                    }
                    glow.Ellipsoid(Up * 1.0f, new Vector3(0.4f, 0.3f, 0.4f), 6, 3, C(120, 200, 255), C(90, 170, 255));
                    break;
                }
                case 2:
                {
                    // crane with raised wings
                    Vector3 body = Up * 1.9f;
                    g.Ellipsoid(body, new Vector3(0.5f, 0.45f, 0.85f), 7, 4, iceL, ice);
                    g.Frustum(body + new Vector3(0f, 0.2f, 0.6f), body + new Vector3(0f, 1.6f, 1.1f), 0.14f, 0.14f, 0.09f, 0.09f, 5, 0f, ice, iceL, false, false);
                    g.Ellipsoid(body + new Vector3(0f, 1.7f, 1.15f), new Vector3(0.15f, 0.15f, 0.2f), 5, 3, iceL, ice);
                    g.Frustum(body + new Vector3(0f, 1.7f, 1.3f), body + new Vector3(0f, 1.62f, 1.75f), 0.05f, 0.05f, 0.005f, 0.005f, 4, 0f, iceD, iceD, false, false);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        PG.Quad2(g, body + new Vector3(s * 0.35f, 0.2f, 0.3f), body + new Vector3(s * 1.6f, 1.4f, 0.1f), body + new Vector3(s * 2.1f, 1.0f, -0.6f), body + new Vector3(s * 0.35f, 0.1f, -0.4f), iceL, IceWhite, ice, ice);
                        g.Frustum(body + new Vector3(s * 0.2f, -0.4f, 0f), new Vector3(s * 0.2f, 0.7f, 0.1f), 0.05f, 0.05f, 0.05f, 0.05f, 4, 0f, iceD, iceD, false, false);
                    }
                    break;
                }
                case 3:
                {
                    // snowman with a red scarf and a carrot nose
                    g.Ellipsoid(Up * 0.75f, new Vector3(0.95f, 0.85f, 0.95f), 8, 5, Snow, SnowShade);
                    g.Ellipsoid(Up * 2.05f, new Vector3(0.68f, 0.62f, 0.68f), 8, 5, Snow, SnowShade);
                    g.Ellipsoid(Up * 3.0f, new Vector3(0.46f, 0.44f, 0.46f), 8, 5, Snow, SnowShade);
                    g.Frustum(Up * 2.58f, Up * 2.72f, 0.52f, 0.52f, 0.5f, 0.5f, 8, 0f, C(200, 40, 40), C(220, 50, 46), false, false);
                    PG.Beam(g, new Vector3(0.3f, 2.62f, -0.45f), new Vector3(0.45f, 2.0f, -0.62f), 0.1f, 0.03f, C(200, 40, 40));
                    g.Frustum(new Vector3(0f, 3.0f, -0.42f), new Vector3(0f, 2.96f, -0.85f), 0.07f, 0.07f, 0.005f, 0.005f, 5, 0f, C(240, 120, 30), C(240, 120, 30), true, false);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        g.Ellipsoid(new Vector3(s * 0.17f, 3.12f, -0.4f), new Vector3(0.05f, 0.05f, 0.04f), 4, 2, Black, Black);
                        PG.Beam(g, new Vector3(s * 0.6f, 2.2f, 0f), new Vector3(s * 1.4f, 2.8f, 0f), 0.03f, 0.03f, LogDark);
                    }
                    g.Frustum(Up * 3.38f, Up * 3.45f, 0.5f, 0.5f, 0.5f, 0.5f, 8, 0f, Black, Black, true, true);
                    g.Frustum(Up * 3.45f, Up * 3.95f, 0.32f, 0.32f, 0.3f, 0.3f, 8, 0f, Black, Black, false, true);
                    break;
                }
                default:
                {
                    // ring of ice-block lanterns with candles
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        Vector3 p = new Vector3(Mathf.Cos(a) * 1.25f, 0.7f, Mathf.Sin(a) * 1.25f);
                        PG.Box2(g, p + Up * 0.35f, new Vector3(0.28f, 0.35f, 0.28f), ice, iceL);
                        glow.Ellipsoid(p + Up * 0.4f, new Vector3(0.12f, 0.18f, 0.12f), 5, 3, WarmHot, Warm);
                    }
                    g.Frustum(Up * 0.7f, Up * 2.6f, 0.45f, 0.45f, 0.18f, 0.18f, 6, 0f, iceD, iceL, false, true);
                    glow.Ellipsoid(Up * 2.75f, new Vector3(0.22f, 0.3f, 0.22f), 6, 3, WarmHot, Warm);
                    break;
                }
            }
            return m;
        }

        /// <summary>Ice-fishing hut on runners (colourful), facing -Z.</summary>
        public static PropModel FishingHut(Color32 col)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var glow = m[PK.Glow];
            PG.Beam(g, new Vector3(-0.8f, 0.05f, -1.3f), new Vector3(-0.8f, 0.05f, 1.3f), 0.06f, 0.05f, LogDark);
            PG.Beam(g, new Vector3(0.8f, 0.05f, -1.3f), new Vector3(0.8f, 0.05f, 1.3f), 0.06f, 0.05f, LogDark);
            PG.Box2(g, new Vector3(0f, 1.2f, 0f), new Vector3(1f, 1.05f, 1.2f), col, col);
            for (int s = -1; s <= 1; s += 2)
                PG.Box2(g, new Vector3(s * 0.55f, 2.45f, 0f), new Vector3(0.62f, 0.07f, 1.35f), Sh(col, 0.7f), Snow);
            g.TriOut(new Vector3(-1f, 2.25f, -1.2f), new Vector3(1f, 2.25f, -1.2f), new Vector3(0f, 2.75f, -1.2f), Vector3.back, col, col, col);
            g.TriOut(new Vector3(-1f, 2.25f, 1.2f), new Vector3(1f, 2.25f, 1.2f), new Vector3(0f, 2.75f, 1.2f), Vector3.forward, col, col, col);
            PG.BoxMM(g, new Vector3(-0.35f, 0.2f, -1.23f), new Vector3(0.35f, 1.8f, -1.2f), Sh(col, 0.6f));
            GlowWindow(g, glow, new Vector3(1.03f, 1.5f, 0f), Vector3.right, 0.25f, 0.25f, C(240, 240, 236));
            PG.Rod(g, new Vector3(0.6f, 2.2f, 0.7f), new Vector3(0.6f, 3.1f, 0.7f), 0.06f, SteelDark, 5);
            return m;
        }

        /// <summary>Crevasse along X: dark blue slot between two snow lips (length L, width W). Origin on the ground.</summary>
        public static PropModel Crevasse(float L, float W, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            const int N = 10;
            var lip = new float[N + 1];
            for (int i = 0; i <= N; i++) lip[i] = W * (0.35f + 0.65f * Mathf.Sin(Mathf.PI * i / N)) * R(rng, 0.85f, 1.15f);
            for (int i = 0; i < N; i++)
            {
                float x0 = -L * 0.5f + L * i / N, x1 = -L * 0.5f + L * (i + 1) / N;
                for (int s = -1; s <= 1; s += 2)
                {
                    // inner wall: blue gradient down into darkness
                    Vector3 a = new Vector3(x0, 0.15f, s * lip[i] * 0.5f), b = new Vector3(x1, 0.15f, s * lip[i + 1] * 0.5f);
                    Vector3 c = new Vector3(x1, -2.6f, s * lip[i + 1] * 0.12f), d = new Vector3(x0, -2.6f, s * lip[i] * 0.12f);
                    g.QuadOut(a, b, c, d, new Vector3(0f, 0f, -s), IceC, IceC, C(20, 50, 96), C(20, 50, 96));
                    // overhanging snow lip
                    Vector3 o0 = new Vector3(x0, 0.25f, s * (lip[i] * 0.5f + 0.9f)), o1 = new Vector3(x1, 0.25f, s * (lip[i + 1] * 0.5f + 0.9f));
                    g.QuadOut(a + Up * 0.12f, b + Up * 0.12f, o1, o0, Up, Snow, Snow, Snow, Snow);
                    g.QuadOut(a + Up * 0.12f, b + Up * 0.12f, b - Up * 0.05f - new Vector3(0f, 0f, s * 0.15f), a - Up * 0.05f - new Vector3(0f, 0f, s * 0.15f), new Vector3(0f, -0.3f, -s), IceWhite, IceWhite, IceC, IceC);
                }
            }
            // dark bottom
            g.QuadOut(new Vector3(-L * 0.5f, -2.5f, -W * 0.3f), new Vector3(L * 0.5f, -2.5f, -W * 0.3f), new Vector3(L * 0.5f, -2.5f, W * 0.3f), new Vector3(-L * 0.5f, -2.5f, W * 0.3f), Up, C(14, 30, 66), C(14, 30, 66), C(14, 30, 66), C(14, 30, 66));
            return m;
        }

        /// <summary>Frozen waterfall: cascading icicle curtain (height H, width W) in front of a rock backing, frozen pool. Faces -Z.</summary>
        public static PropModel FrozenFall(float H, float W, System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            // rock backing wall (dark, snowy ledges)
            Crag(g, new Vector3(-W * 0.55f, -2f, 3.5f), H + 8f, W * 0.45f, rng);
            Crag(g, new Vector3(W * 0.6f, -2f, 3.2f), H + 4f, W * 0.42f, rng);
            PG.Box2(g, new Vector3(0f, H * 0.5f, 3.2f), new Vector3(W * 0.6f, H * 0.5f + 1f, 2.2f), RockB, Snow);
            // icicle columns in three tiers
            for (int tier = 0; tier < 3; tier++)
            {
                float yTop = H * (1f - tier * 0.33f), yBot = H * (0.62f - tier * 0.33f) - 0.5f;
                float z = 1.0f - tier * 0.7f;
                int cols = 9 + tier * 2;
                for (int i = 0; i < cols; i++)
                {
                    float x = -W * 0.45f + W * 0.9f * (i + R(rng, 0f, 0.8f)) / cols;
                    float bot = yBot + R(rng, -1.5f, 1f) * (tier == 2 ? 0f : 1f);
                    float r = R(rng, 0.35f, 0.75f);
                    Color32 c0 = (i & 1) == 0 ? IceC : IceWhite;
                    g.Frustum(new Vector3(x, yTop, z), new Vector3(x + R(rng, -0.2f, 0.2f), Mathf.Max(0f, bot), z - 0.3f), r * 1.3f, r, r * 0.4f, r * 0.35f, 6, R(rng, 0f, 1f), c0, IceDeep, false, true);
                }
                // bulging ice shelf at the tier's top
                int st = g.v.Count;
                g.Ellipsoid(new Vector3(0f, yTop, z + 0.3f), new Vector3(W * 0.48f, 0.9f, 1.2f), 10, 3, IceWhite, IceC);
                PG.Lumpy(g, st, new Vector3(0f, yTop, z + 0.3f), 0.12f, tier * 5f);
            }
            // frozen pool with snow rim
            g.Disc(new Vector3(0f, 0.12f, -3.5f), W * 0.62f, 14, 0f, Up, IceC, IceDeep);
            int pr = g.v.Count;
            g.Frustum(new Vector3(0f, -0.3f, -3.5f), new Vector3(0f, 0.35f, -3.5f), W * 0.75f, W * 0.62f, W * 0.66f, W * 0.55f, 14, 0f, SnowShade, Snow, false, false);
            PG.Lumpy(g, pr, new Vector3(0f, 0f, -3.5f), 0.04f, 3f);
            return m;
        }

        /// <summary>Viewpoint railing section along X (length L): timber posts and rails with snow, facing -Z (the drop).</summary>
        public static PropModel Railing(float L)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            int posts = Mathf.Max(2, Mathf.RoundToInt(L / 2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = -L * 0.5f + L * i / (posts - 1);
                PG.Beam(g, new Vector3(x, -0.4f, 0f), new Vector3(x, 1.1f, 0f), 0.08f, 0.08f, LogDark);
                g.Ellipsoid(new Vector3(x, 1.15f, 0f), new Vector3(0.11f, 0.06f, 0.11f), 5, 2, Snow, Snow);
            }
            PG.Beam(g, new Vector3(-L * 0.5f, 1.02f, 0f), new Vector3(L * 0.5f, 1.02f, 0f), 0.06f, 0.06f, Log);
            PG.Beam(g, new Vector3(-L * 0.5f, 0.55f, 0f), new Vector3(L * 0.5f, 0.55f, 0f), 0.05f, 0.05f, Log);
            PG.Beam(g, new Vector3(-L * 0.5f, 1.1f, 0f), new Vector3(L * 0.5f, 1.1f, 0f), 0.06f, 0.025f, Snow);
            return m;
        }

        /// <summary>Coin-operated binocular viewer on a post (faces -Z).</summary>
        public static PropModel Binoculars()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 body = C(40, 120, 150);
            g.Frustum(Vector3.down * 0.3f, Up * 1.1f, 0.09f, 0.09f, 0.07f, 0.07f, 6, 0f, SteelDark, SteelDark, false, false);
            PG.Box2(g, new Vector3(0f, 1.3f, 0f), new Vector3(0.22f, 0.2f, 0.25f), body, body);
            for (int s = -1; s <= 1; s += 2)
                g.Frustum(new Vector3(s * 0.11f, 1.38f, -0.2f), new Vector3(s * 0.11f, 1.42f, -0.48f), 0.08f, 0.08f, 0.1f, 0.1f, 8, 0f, body, Black, false, true);
            PG.Box2(g, new Vector3(0f, 1.52f, 0f), new Vector3(0.24f, 0.03f, 0.27f), Snow, Snow);
            return m;
        }

        /// <summary>Wooden bench with a snow cushion (faces -Z).</summary>
        public static PropModel Bench()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            for (int s = -1; s <= 1; s += 2) PG.BoxMM(g, new Vector3(s * 0.8f - 0.06f, -0.3f, -0.2f), new Vector3(s * 0.8f + 0.06f, 0.45f, 0.2f), LogDark);
            PG.Box2(g, new Vector3(0f, 0.48f, 0f), new Vector3(0.95f, 0.04f, 0.22f), Log, Snow);
            PG.BoxMM(g, new Vector3(-0.95f, 0.55f, 0.18f), new Vector3(0.95f, 0.95f, 0.24f), Log);
            PG.Box2(g, new Vector3(0f, 0.56f, 0f), new Vector3(0.8f, 0.06f, 0.18f), Snow, Snow);
            return m;
        }

        /// <summary>Hexagonal lookout pavilion with a snow-loaded Chinese roof (radius r).</summary>
        public static PropModel Pavilion(float r)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                g.Frustum(p - Up * 0.3f, p + Up * 3f, 0.16f, 0.16f, 0.14f, 0.14f, 6, 0f, Pal.Red, Pal.RedBright, false, false);
            }
            g.Frustum(Up * -0.5f, Up * 0.25f, r + 0.6f, r + 0.6f, r + 0.5f, r + 0.5f, 6, 0f, C(150, 148, 146), Snow, false, true);
            PG.Roof(g, Up * 3f, r * 0.95f, r * 0.95f, r * 0.7f, 0.9f, 0.5f, Snow, SnowShade, Pal.RedDeep, Snow, true);
            return m;
        }

        /// <summary>Timber gate arch over the road (span along X), snow-loaded roof and the region name board.</summary>
        public static PropModel GateArch(float span)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            float hx = span * 0.5f, H = 6.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                PG.Box2(g, new Vector3(s * hx, 0.2f, 0f), new Vector3(0.75f, 0.6f, 0.75f), C(140, 138, 136), Snow);
                g.Frustum(new Vector3(s * hx, 0.6f, 0f), new Vector3(s * hx, H, 0f), 0.3f, 0.3f, 0.28f, 0.28f, 8, 0f, Pal.Red, Pal.RedBright, false, false);
            }
            PG.Beam(g, new Vector3(-hx - 0.9f, H - 0.4f, 0f), new Vector3(hx + 0.9f, H - 0.4f, 0f), 0.22f, 0.25f, Pal.RedDeep);
            PG.Beam(g, new Vector3(-hx - 0.6f, H - 1.6f, 0f), new Vector3(hx + 0.6f, H - 1.6f, 0f), 0.16f, 0.16f, Pal.Red);
            PG.BoxMM(g, new Vector3(-2.2f, H - 1.45f, -0.12f), new Vector3(2.2f, H - 0.62f, 0.12f), C(36, 60, 104));
            PG.BoxMM(g, new Vector3(-2.0f, H - 1.35f, -0.16f), new Vector3(2.0f, H - 0.72f, 0.16f), Pal.Gold);
            PG.Roof(g, new Vector3(0f, H - 0.15f, 0f), hx + 0.6f, 0.6f, 1.1f, 0.8f, 0.45f, Snow, SnowShade, Pal.RedDeep, Snow, false);
            return m;
        }

        // ------------------------------------------------------------------ people
        static readonly Color32[] Jackets =
        {
            C(214, 52, 46), C(40, 104, 196), C(246, 190, 40), C(56, 150, 96), C(128, 72, 170), C(240, 120, 40), C(38, 150, 160), C(236, 236, 240)
        };

        /// <summary>Spectator in winter gear facing -Z: puffy jacket, beanie with a pom-pom, scarf. pose 0 arms down, 1 waving, 2 camera.</summary>
        public static PropModel Person(System.Random rng, int pose)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 jacket = Jackets[rng.Next(Jackets.Length)], jacketD = Sh(jacket, 0.82f);
            Color32 pants = rng.NextDouble() < 0.5 ? C(40, 44, 62) : C(58, 58, 64), boot = C(70, 50, 40), skin = C(240, 202, 170);
            Color32 hat = Jackets[rng.Next(Jackets.Length)], scarf = Jackets[rng.Next(Jackets.Length)];
            float s = R(rng, 0.92f, 1.06f);
            for (int k = -1; k <= 1; k += 2)
            {
                g.Frustum(new Vector3(k * 0.1f, 0f, -0.02f) * s, new Vector3(k * 0.1f, 0.14f, -0.02f) * s, 0.08f * s, 0.12f * s, 0.08f * s, 0.11f * s, 5, 0f, boot, boot, true, false);
                g.Frustum(new Vector3(k * 0.1f, 0.12f, 0f) * s, new Vector3(k * 0.11f, 0.78f, 0f) * s, 0.075f * s, 0.075f * s, 0.095f * s, 0.095f * s, 5, 0f, pants, pants, false, false);
            }
            g.Frustum(new Vector3(0f, 0.72f, 0f) * s, new Vector3(0f, 1.42f, 0f) * s, 0.25f * s, 0.2f * s, 0.23f * s, 0.18f * s, 7, 0f, jacketD, jacket, true, false);
            g.Ellipsoid(new Vector3(0f, 1.4f, 0f) * s, new Vector3(0.23f, 0.1f, 0.18f) * s, 7, 3, jacket, jacketD);
            g.Frustum(new Vector3(0f, 1.43f, 0f) * s, new Vector3(0f, 1.53f, 0f) * s, 0.12f * s, 0.12f * s, 0.11f * s, 0.11f * s, 6, 0f, scarf, scarf, false, true);
            g.Ellipsoid(new Vector3(0f, 1.64f, 0f) * s, new Vector3(0.11f, 0.13f, 0.12f) * s, 6, 4, skin, skin);
            g.Ellipsoid(new Vector3(0f, 1.7f, 0f) * s, new Vector3(0.125f, 0.1f, 0.13f) * s, 6, 3, hat, Sh(hat, 0.85f));
            g.Ellipsoid(new Vector3(0f, 1.83f, 0f) * s, new Vector3(0.05f, 0.05f, 0.05f) * s, 4, 3, Snow, Snow);
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 sh = new Vector3(k * 0.24f, 1.36f, 0f) * s;
                Vector3 hand;
                if (pose == 1 && k > 0) hand = new Vector3(0.38f, 1.98f, -0.05f) * s;
                else if (pose == 2) hand = new Vector3(k * 0.08f, 1.55f, -0.3f) * s;
                else hand = new Vector3(k * 0.3f, 0.82f, 0.04f) * s;
                g.Frustum(sh, hand, 0.075f * s, 0.075f * s, 0.06f * s, 0.06f * s, 5, 0f, jacket, jacketD, false, false);
                g.Ellipsoid(hand, new Vector3(0.055f, 0.055f, 0.055f) * s, 4, 3, Black, Black);
            }
            if (pose == 2) PG.BoxMM(g, new Vector3(-0.09f, 1.5f, -0.42f) * s, new Vector3(0.09f, 1.62f, -0.3f) * s, Black);
            return m;
        }

        /// <summary>Low wind-sculpted snow drift (along X), lee side in blue shade.</summary>
        public static PropModel Drift(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            int st = g.v.Count;
            Vector3 c = new Vector3(0f, -0.2f, 0f);
            g.Ellipsoid(c, new Vector3(R(rng, 2.5f, 5.5f), R(rng, 0.5f, 1.1f), R(rng, 1.2f, 2.2f)), 9, 3, Snow, SnowShade);
            PG.Lumpy(g, st, c, 0.12f, R(rng, 0f, 50f));
            for (int i = st; i < g.v.Count; i++) if (g.n[i].z > 0.35f) g.c[i] = SnowShade;
            return m;
        }

        /// <summary>Red spectator tent / race-control shelter with flags.</summary>
        public static PropModel Tent(Color32 col)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            float hx = 2.6f, hz = 2f, H = 2.1f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    PG.Rod(g, new Vector3(sx * hx, -0.2f, sz * hz), new Vector3(sx * hx, H, sz * hz), 0.04f, Steel, 4);
            Vector3 t = new Vector3(0f, H + 1f, 0f);
            Vector3[] cs = { new Vector3(-hx, H, -hz), new Vector3(hx, H, -hz), new Vector3(hx, H, hz), new Vector3(-hx, H, hz) };
            for (int i = 0; i < 4; i++)
            {
                Color32 cc = (i & 1) == 0 ? col : C(244, 244, 240);
                g.TriOut(cs[i], cs[(i + 1) % 4], t, (cs[i] + cs[(i + 1) % 4]) * 0.5f, cc, cc, cc);
                g.TriOut(cs[i], cs[(i + 1) % 4], t, -((cs[i] + cs[(i + 1) % 4]) * 0.5f), Sh(cc, 0.7f), Sh(cc, 0.7f), Sh(cc, 0.7f));
            }
            // back wall + valance + snow on the roof
            PG.BoxMM(g, new Vector3(-hx, 0f, hz - 0.03f), new Vector3(hx, H, hz + 0.03f), Sh(col, 0.85f));
            for (int i = 0; i < 4; i++)
                PG.Beam(g, cs[i] - Up * 0.18f, cs[(i + 1) % 4] - Up * 0.18f, 0.02f, 0.18f, col);
            g.Ellipsoid(t - Up * 0.5f, new Vector3(1.2f, 0.25f, 0.9f), 6, 2, Snow, SnowShade);
            // a table with a flask
            PG.Box2(g, new Vector3(0f, 0.75f, 0.6f), new Vector3(1.2f, 0.04f, 0.4f), C(236, 236, 236), C(236, 236, 236));
            g.Frustum(new Vector3(0.5f, 0.79f, 0.6f), new Vector3(0.5f, 1.1f, 0.6f), 0.08f, 0.08f, 0.08f, 0.08f, 6, 0f, C(200, 40, 40), C(220, 60, 50), false, true);
            return m;
        }

        // ------------------------------------------------------------------ continuous strips (world space, relative to 'origin')
        /// <summary>
        /// Rounded snow bank along a polyline (pts relative to origin, already on the ground). side = +1 right / -1 left of
        /// the travel direction. Height h, width w; varies along the run.
        /// </summary>
        public static void BankStrip(LifeGeo g, List<Vector3> pts, List<Vector3> rights, float h, float w, float seed, bool wall)
        {
            const int P = 6;
            int n = pts.Count;
            if (n < 2) return;
            var prof = new Vector3[n, P];
            for (int i = 0; i < n; i++)
            {
                float v = 0.75f + 0.25f * Mathf.Sin(i * 0.37f + seed) + 0.12f * Mathf.Sin(i * 1.13f + seed * 2.1f);
                float hh = h * v, ww = w * (0.85f + 0.15f * v);
                for (int k = 0; k < P; k++)
                {
                    float t = k / (float)(P - 1);
                    float across, up;
                    if (wall)
                    {
                        // near-vertical cut face rising from the road side, rounded crown, back slope
                        float[] ax = { -0.05f, 0.02f, 0.12f, 0.35f, 0.7f, 1.2f };
                        float[] ay = { -0.4f, 0.55f, 0.92f, 1.0f, 0.75f, -0.3f };
                        across = ax[k] * ww; up = ay[k] * hh;
                    }
                    else
                    {
                        across = t * ww;
                        up = Mathf.Sin(Mathf.PI * t) * hh - 0.25f;
                    }
                    prof[i, k] = pts[i] + rights[i] * across + Up * up;
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < P - 1; k++)
                {
                    Vector3 a = prof[i, k], b = prof[i + 1, k], c = prof[i + 1, k + 1], d = prof[i, k + 1];
                    Vector3 outward = Up + (k < P / 2 ? -rights[i] : rights[i]) * 0.6f;
                    Color32 col;
                    if (wall)
                    {
                        // layered cut face: banded blue-white strata
                        col = k == 0 ? SnowBlue : (k == 1 ? ((i & 1) == 0 ? SnowShade : C(214, 226, 242)) : (k == 2 ? SnowShade : Snow));
                    }
                    else col = k == 0 ? SnowShade : (k == P - 2 ? SnowShade : Snow);
                    g.QuadOut(a, b, c, d, outward, col, col, col, col);
                }
        }
    }
}
