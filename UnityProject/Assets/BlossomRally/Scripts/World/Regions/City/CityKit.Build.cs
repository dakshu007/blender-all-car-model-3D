// CityKit.Build.cs - shared geometry for Jade Harbour City: a world-space chunk accumulator (long strips such
// as kerbs, barriers and quay walls are merged per 334 m map chunk), the city palette, hanzi-like neon glyphs
// and the small street props (trees in planters, lamps, signals, bollards, benches, shelters, cars, scooters).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    /// <summary>World-space geometry collected per map chunk, stamped at the chunk origin.</summary>
    internal sealed class CityChunkGeo
    {
        readonly Dictionary<long, PropModel> models = new Dictionary<long, PropModel>();
        readonly Dictionary<long, Vector3> origins = new Dictionary<long, Vector3>();
        readonly bool skyline;
        public CityChunkGeo(bool sky = false) { skyline = sky; }

        static int Cell(float x) { return Mathf.FloorToInt((x + 1840f) / 334f); }

        /// <summary>Geometry of kind k for the chunk containing p; 'o' is the origin to subtract from world points.</summary>
        public LifeGeo At(PK k, Vector3 p, out Vector3 o)
        {
            long key = WorldField.Key(Cell(p.x), Cell(p.z));
            PropModel m;
            if (!models.TryGetValue(key, out m))
            {
                m = new PropModel();
                models[key] = m;
                origins[key] = new Vector3((Cell(p.x) + 0.5f) * 334f - 1840f, 0f, (Cell(p.z) + 0.5f) * 334f - 1840f);
            }
            o = origins[key];
            return m[k];
        }

        public void Flush(RegionContext ctx)
        {
            foreach (var kv in models)
            {
                if (skyline) ctx.StampSkyline(kv.Value, origins[kv.Key], 0f);
                else ctx.Stamp(kv.Value, origins[kv.Key], 0f);
            }
            models.Clear();
            origins.Clear();
        }

        // ---- world-space primitives
        public void Box(PK k, Vector3 c, Vector3 half, float yawDeg, Color32 col)
        {
            Vector3 o;
            var g = At(k, c, out o);
            PG.OBox(g, c - o, half, Quaternion.Euler(0f, yawDeg, 0f), col);
        }

        public void Beam(PK k, Vector3 a, Vector3 b, float hw, float hh, Color32 col)
        {
            Vector3 o;
            var g = At(k, a, out o);
            PG.Beam(g, a - o, b - o, hw, hh, col);
        }

        public void Quad(PK k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            Vector3 o;
            var g = At(k, a, out o);
            g.QuadOut(a - o, b - o, c - o, d - o, outward, ca, cb, cc, cd);
        }

        public void Quad(PK k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 col)
        {
            Quad(k, a, b, c, d, outward, col, col, col, col);
        }
    }

    internal static class CityKit
    {
        public static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        public static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }
        public static float R(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }
        static readonly Vector3 Up = Vector3.up;

        // ---- city palette
        public static readonly Color32 Concrete = C(196, 194, 188), ConcreteDark = C(150, 150, 148), Kerb = C(214, 212, 206);
        public static readonly Color32 Steel = C(88, 96, 104), SteelDark = C(52, 58, 66), SteelLight = C(150, 158, 166);
        public static readonly Color32 Jade = C(62, 168, 140), JadeDeep = C(34, 112, 96), JadeLight = C(150, 214, 190);
        public static readonly Color32 Gold = C(226, 182, 92), Champagne = C(222, 198, 150), Pearl = C(238, 238, 232);
        public static readonly Color32 BarrierRed = C(204, 46, 42), BarrierWhite = C(242, 240, 236);
        public static readonly Color32 LampWarm = C(255, 226, 168), LampCool = C(226, 240, 255);
        public static readonly Color32 Leaf = C(84, 146, 82), LeafDark = C(52, 104, 62), Bark = C(98, 78, 62);

        /// <summary>Neon colours (sRGB, glow kind).</summary>
        public static readonly Color32[] Neon =
        {
            C(255, 70, 160), C(60, 230, 255), C(80, 255, 170), C(255, 196, 70), C(255, 72, 60), C(170, 110, 255)
        };

        // ------------------------------------------------------------------ hanzi-like glyphs
        /// <summary>
        /// Adds one abstract hanzi-like glyph (3-6 neon strokes in a unit cell) to g, in the plane spanned by
        /// right / up around 'c' (size s, stroke width w), facing 'n'. Strokes are thin quads 2 cm off the board.
        /// </summary>
        public static void Glyph(LifeGeo g, Vector3 c, Vector3 right, Vector3 up, Vector3 n, float s, float w, Color32 col, System.Random rng)
        {
            Vector3 o = c + n * 0.03f;
            System.Action<float, float, float, float> stroke = (x0, y0, x1, y1) =>
            {
                Vector3 a = o + right * (x0 - 0.5f) * s + up * (y0 - 0.5f) * s;
                Vector3 b = o + right * (x1 - 0.5f) * s + up * (y1 - 0.5f) * s;
                Vector3 d = (b - a);
                if (d.sqrMagnitude < 1e-6f) return;
                Vector3 side = Vector3.Cross(n, d.normalized) * (w * 0.5f);
                Vector3 ext = d.normalized * (w * 0.5f);
                g.QuadOut(a - side - ext, b - side + ext, b + side + ext, a + side - ext, n, col, col, col, col);
            };
            int kind = rng.Next(6);
            // radical on the left in half of the glyphs
            float lx = 0f;
            if (rng.NextDouble() < 0.5)
            {
                lx = 0.34f;
                if (rng.NextDouble() < 0.5) { stroke(0.06f, 0.85f, 0.2f, 0.7f); stroke(0.04f, 0.55f, 0.2f, 0.45f); stroke(0.06f, 0.08f, 0.22f, 0.3f); }   // water radical
                else { stroke(0.17f, 0.95f, 0.17f, 0.05f); stroke(0.03f, 0.7f, 0.3f, 0.7f); stroke(0.06f, 0.35f, 0.28f, 0.48f); }                       // tree / hand radical
            }
            float x0 = lx + 0.04f, x1 = 0.96f, xm = (x0 + x1) * 0.5f;
            switch (kind)
            {
                case 0:   // 口 box with a middle bar (日)
                    stroke(x0, 0.15f, x0, 0.85f); stroke(x1, 0.15f, x1, 0.85f); stroke(x0, 0.85f, x1, 0.85f); stroke(x0, 0.15f, x1, 0.15f);
                    stroke(x0, 0.5f, x1, 0.5f);
                    break;
                case 1:   // 王 three bars and a stem
                    stroke(x0, 0.88f, x1, 0.88f); stroke(x0 + 0.06f, 0.5f, x1 - 0.06f, 0.5f); stroke(x0 - 0.02f, 0.1f, x1 + 0.02f, 0.1f); stroke(xm, 0.1f, xm, 0.88f);
                    break;
                case 2:   // 大 / 木 cross with legs
                    stroke(x0, 0.62f, x1, 0.62f); stroke(xm, 0.95f, xm, 0.45f); stroke(xm, 0.5f, x0, 0.05f); stroke(xm, 0.5f, x1, 0.05f);
                    break;
                case 3:   // 门 gate frame
                    stroke(x0, 0.05f, x0, 0.9f); stroke(x0 + 0.12f, 0.9f, x1, 0.9f); stroke(x1, 0.9f, x1, 0.08f); stroke(x0 + 0.06f, 0.98f, x0 + 0.16f, 0.86f);
                    stroke(xm - 0.12f, 0.6f, xm + 0.12f, 0.6f);
                    break;
                case 4:   // 田 grid
                    stroke(x0, 0.12f, x0, 0.88f); stroke(x1, 0.12f, x1, 0.88f); stroke(x0, 0.88f, x1, 0.88f); stroke(x0, 0.12f, x1, 0.12f);
                    stroke(xm, 0.12f, xm, 0.88f); stroke(x0, 0.5f, x1, 0.5f);
                    break;
                default:  // 上 / 止 steps
                    stroke(xm, 0.95f, xm, 0.08f); stroke(xm, 0.55f, x1, 0.55f); stroke(x0, 0.08f, x1, 0.08f); stroke(x0 + 0.08f, 0.45f, x0 + 0.08f, 0.08f);
                    break;
            }
            if (rng.NextDouble() < 0.35) stroke(x1 - 0.1f, 0.98f, x1 + 0.02f, 0.86f);   // dot
        }

        // ------------------------------------------------------------------ small props (local space, ground at y = 0)
        public static PropModel StreetTree(System.Random rng, int variant, bool planter)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            if (planter)
            {
                PG.Box2(g, new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.3f, 1.3f), ConcreteDark, C(110, 86, 64));
                PG.Box2(g, new Vector3(0f, 0.62f, 0f), new Vector3(1.38f, 0.04f, 1.38f), Kerb, Kerb);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 1.05f + R(rng, 0f, 0.4f);
                    Color32 fl = i % 3 == 0 ? C(240, 120, 150) : i % 3 == 1 ? C(250, 220, 110) : C(240, 240, 236);
                    g.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.95f, 0.72f, Mathf.Sin(a) * 0.95f), new Vector3(0.28f, 0.16f, 0.28f), 5, 3, fl, Sh(fl, 0.8f));
                }
            }
            float H = R(rng, 4.2f, 6.2f);
            Color32 top, bot;
            switch (variant % 4)
            {
                case 0: top = C(104, 164, 88, 30); bot = C(58, 112, 64, 20); break;      // camphor
                case 1: top = C(150, 190, 92, 30); bot = C(92, 140, 66, 20); break;      // plane tree
                case 2: top = C(230, 196, 80, 30); bot = C(176, 142, 54, 20); break;     // ginkgo gold
                default: top = C(244, 186, 206, 30); bot = C(204, 128, 160, 20); break;  // flowering cherry
            }
            g.Frustum(new Vector3(0f, 0.3f, 0f), new Vector3(0f, H * 0.6f, 0f), 0.2f, 0.2f, 0.13f, 0.13f, 6, 0f, Bark, Sh(Bark, 1.15f), false, true);
            int blobs = 3 + rng.Next(2);
            for (int i = 0; i < blobs; i++)
            {
                float a = i * Mathf.PI * 2f / blobs + R(rng, 0f, 1f);
                Vector3 c = new Vector3(Mathf.Cos(a) * R(rng, 0.5f, 1.0f), H * R(rng, 0.68f, 0.86f), Mathf.Sin(a) * R(rng, 0.5f, 1.0f));
                int s = g.v.Count;
                g.Ellipsoid(c, new Vector3(R(rng, 1.3f, 1.8f), R(rng, 1.0f, 1.4f), R(rng, 1.3f, 1.8f)), 7, 4, top, bot);
                PG.Lumpy(g, s, c, 0.12f, i * 3.7f + variant);
            }
            int s2 = g.v.Count;
            Vector3 cap = new Vector3(0f, H, 0f);
            g.Ellipsoid(cap, new Vector3(1.4f, 1.1f, 1.4f), 7, 4, top, bot);
            PG.Lumpy(g, s2, cap, 0.1f, variant * 2.1f);
            return m;
        }

        /// <summary>Street lamp facing +z (arm over the road towards +z). twin = arms both ways (median).</summary>
        public static PropModel Lamp(bool avenue, bool twin)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            float H = avenue ? 9f : 7f;
            g.Frustum(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.6f, 0f), 0.24f, 0.24f, 0.17f, 0.17f, 6, 0f, SteelDark, Steel, false, true);
            g.Frustum(new Vector3(0f, 0.5f, 0f), new Vector3(0f, H, 0f), 0.11f, 0.11f, 0.07f, 0.07f, 6, 0f, Steel, SteelLight, false, true);
            for (int s = twin ? -1 : 1; s <= 1; s += 2)
            {
                Vector3 a = new Vector3(0f, H - 0.2f, 0f), b = new Vector3(0f, H + 0.25f, s * 1.1f), c = new Vector3(0f, H + 0.15f, s * 2.4f);
                PG.Beam(g, a, b, 0.05f, 0.05f, SteelLight);
                PG.Beam(g, b, c, 0.05f, 0.05f, SteelLight);
                PG.Box2(g, c + new Vector3(0f, 0.02f, 0f), new Vector3(0.28f, 0.08f, 0.55f), SteelDark, SteelLight);
                PG.Box2(gl, c + new Vector3(0f, -0.08f, 0f), new Vector3(0.22f, 0.03f, 0.48f), LampWarm, LampWarm);
            }
            // small jade banner on avenue lamps
            if (avenue)
            {
                PG.Box2(m[PK.Strong], new Vector3(0.06f, H - 2.6f, 0.45f), new Vector3(0.02f, 1.0f, 0.4f), A(Jade, 60), A(Jade, 120));
                PG.Box2(m[PK.Strong], new Vector3(0.09f, H - 2.6f, 0.45f), new Vector3(0.005f, 0.6f, 0.08f), A(Gold, 80), A(Gold, 120));
            }
            return m;
        }

        public static Color32 A(Color32 c, int a) { return PG.A(c, a); }

        /// <summary>Promenade lantern post (round paper-lantern head).</summary>
        public static PropModel LanternPost()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            g.Frustum(Vector3.zero, Up * 0.4f, 0.22f, 0.22f, 0.16f, 0.16f, 8, 0f, ConcreteDark, Concrete, false, true);
            g.Frustum(Up * 0.4f, Up * 4.2f, 0.08f, 0.08f, 0.06f, 0.06f, 6, 0f, C(40, 44, 48), C(60, 66, 72), false, true);
            PG.Beam(g, Up * 4.1f, new Vector3(0f, 4.3f, 0.7f), 0.03f, 0.03f, Gold);
            PG.Lantern(gl, new Vector3(0f, 4.3f, 0.7f), 0.32f, 0.55f, C(232, 60, 48), 60);
            gl.Ellipsoid(Up * 4.45f, new Vector3(0.2f, 0.26f, 0.2f), 6, 4, LampWarm, Sh(LampWarm, 0.85f));
            return m;
        }

        /// <summary>Signal pole at the corner; the arm reaches along +z over the road, heads face -x (oncoming traffic).</summary>
        public static PropModel TrafficLight(float reach, int phase, bool headsPlusX = false)
        {
            float hx = headsPlusX ? -1f : 1f;
            var m = new PropModel();
            var g = m[PK.Props];
            var gl = m[PK.Glow];
            g.Frustum(Vector3.zero, Up * 6.2f, 0.13f, 0.13f, 0.1f, 0.1f, 6, 0f, SteelDark, Steel, false, true);
            PG.Beam(g, new Vector3(0f, 5.9f, 0f), new Vector3(0f, 5.9f, reach), 0.07f, 0.09f, Steel);
            PG.Beam(g, new Vector3(0f, 5.0f, 0f), new Vector3(0f, 5.85f, reach * 0.35f), 0.03f, 0.03f, Steel);
            Color32 off = C(40, 40, 44);
            foreach (float zz in new[] { reach * 0.55f, reach * 0.95f })
            {
                Vector3 c = new Vector3(-0.1f * hx, 5.25f, zz);
                PG.Box2(g, c, new Vector3(0.2f, 0.62f, 0.2f), C(30, 32, 36), C(30, 32, 36));
                for (int i = 0; i < 3; i++)
                {
                    bool on = i == phase;
                    Color32 lit = i == 0 ? C(255, 50, 40) : i == 1 ? C(255, 180, 40) : C(60, 255, 150);
                    gl.Disc(c + new Vector3(-0.21f * hx, 0.38f - i * 0.38f, 0f), 0.13f, 8, 0f, Vector3.left * hx, on ? lit : Sh(lit, 0.25f), on ? lit : Sh(lit, 0.2f));
                }
            }
            // pedestrian signal and push button box
            PG.Box2(g, new Vector3(-0.15f * hx, 2.6f, 0f), new Vector3(0.14f, 0.3f, 0.14f), C(30, 32, 36), C(30, 32, 36));
            gl.Disc(new Vector3(-0.3f * hx, 2.6f, 0f), 0.09f, 6, 0f, Vector3.left * hx, phase == 2 ? C(255, 60, 50) : C(80, 255, 170), off);
            return m;
        }

        public static PropModel Bollard()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            g.Frustum(Vector3.zero, Up * 0.9f, 0.13f, 0.13f, 0.11f, 0.11f, 6, 0f, C(60, 64, 70), C(80, 86, 92), false, true);
            g.Frustum(Up * 0.66f, Up * 0.76f, 0.125f, 0.125f, 0.12f, 0.12f, 6, 0f, Gold, Gold, false, false);
            return m;
        }

        public static PropModel Bench()
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 wood = C(160, 112, 72), iron = C(50, 56, 62);
            for (int s = -1; s <= 1; s += 2) PG.Box2(g, new Vector3(s * 0.8f, 0.24f, 0f), new Vector3(0.05f, 0.24f, 0.24f), iron, iron);
            for (int i = 0; i < 3; i++) PG.Box2(g, new Vector3(0f, 0.47f, -0.16f + i * 0.16f), new Vector3(0.95f, 0.025f, 0.06f), wood, Sh(wood, 1.1f));
            for (int i = 0; i < 2; i++) PG.Box2(g, new Vector3(0f, 0.62f + i * 0.17f, 0.28f), new Vector3(0.95f, 0.05f, 0.025f), wood, Sh(wood, 1.1f));
            return m;
        }

        /// <summary>Bus shelter facing -z (the road side), 6 m long.</summary>
        public static PropModel BusShelter(System.Random rng)
        {
            var m = new PropModel();
            var g = m[PK.Landmark];
            var gl = m[PK.Glow];
            Color32 frame = C(46, 120, 104), glass = C(170, 210, 214);
            for (int i = -1; i <= 1; i += 2)
                for (int j = 0; j < 2; j++)
                    PG.Box2(g, new Vector3(i * 2.9f, 1.3f, j == 0 ? -0.7f : 0.7f), new Vector3(0.06f, 1.3f, 0.06f), frame, frame);
            PG.Box2(g, new Vector3(0f, 2.66f, 0f), new Vector3(3.2f, 0.08f, 1.15f), frame, C(220, 226, 222));
            PG.Box2(g, new Vector3(0f, 1.35f, 0.72f), new Vector3(2.85f, 1.15f, 0.02f), glass, glass);
            PG.Box2(g, new Vector3(0f, 0.45f, 0.45f), new Vector3(2.2f, 0.04f, 0.2f), C(150, 110, 74), C(170, 128, 86));
            // glowing advert panel at one end
            PG.Box2(g, new Vector3(2.9f, 1.35f, 0f), new Vector3(0.09f, 1.0f, 0.62f), frame, frame);
            Color32 ad = Neon[rng.Next(Neon.Length)];
            PG.Box2(gl, new Vector3(2.9f, 1.35f, 0f), new Vector3(0.1f, 0.85f, 0.5f), Sh(ad, 0.85f), ad);
            PG.Box2(gl, new Vector3(0f, 2.55f, -0.9f), new Vector3(2.6f, 0.04f, 0.05f), LampCool, LampCool);
            // route sign
            g.Frustum(new Vector3(-3.4f, 0f, -0.9f), new Vector3(-3.4f, 3.2f, -0.9f), 0.05f, 0.05f, 0.05f, 0.05f, 5, 0f, Steel, Steel, false, true);
            PG.Box2(gl, new Vector3(-3.4f, 3.0f, -0.9f), new Vector3(0.03f, 0.28f, 0.28f), Jade, JadeLight);
            return m;
        }

        /// <summary>Low-poly parked hatchback / sedan / van, length along +z, ground at 0.</summary>
        public static PropModel Car(Color32 paint, int type)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            float L = type == 2 ? 2.4f : 2.15f, W = 0.9f;
            Color32 glass = C(52, 70, 86), dark = C(28, 28, 32), lightF = C(240, 236, 220), lightR = C(200, 40, 40);
            PG.Box2(g, new Vector3(0f, 0.55f, 0f), new Vector3(W, 0.3f, L), paint, paint);
            if (type == 2) PG.Box2(g, new Vector3(0f, 1.2f, -0.2f), new Vector3(W * 0.97f, 0.36f, L * 0.86f), paint, Sh(paint, 1.08f));
            else
            {
                float cl = type == 0 ? 0.9f : 1.05f;
                g.Frustum(new Vector3(0f, 0.85f, -0.25f), new Vector3(0f, 1.38f, -0.3f), W * 0.97f, cl * 1.15f, W * 0.8f, cl * 0.75f, 4, Mathf.PI * 0.25f, glass, Sh(paint, 1.05f), false, true);
            }
            PG.Box2(g, new Vector3(0f, 0.38f, L + 0.02f), new Vector3(W * 0.95f, 0.12f, 0.04f), C(60, 62, 66), C(60, 62, 66));
            for (int s = -1; s <= 1; s += 2)
            {
                g.Box(new Vector3(s * 0.62f, 0.62f, L + 0.01f), new Vector3(0.18f, 0.07f, 0.02f), lightF);
                g.Box(new Vector3(s * 0.62f, 0.66f, -L - 0.01f), new Vector3(0.16f, 0.07f, 0.02f), lightR);
                for (int k = -1; k <= 1; k += 2)
                    g.Frustum(new Vector3(s * (W - 0.05f), 0.33f, k * L * 0.62f), new Vector3(s * (W + 0.02f), 0.33f, k * L * 0.62f), 0.33f, 0.33f, 0.33f, 0.33f, 8, 0f, dark, dark, true, true);
            }
            return m;
        }

        public static PropModel Scooter(Color32 paint)
        {
            var m = new PropModel();
            var g = m[PK.Props];
            Color32 dark = C(30, 30, 34);
            for (int k = -1; k <= 1; k += 2)
                g.Frustum(new Vector3(-0.05f, 0.25f, k * 0.6f), new Vector3(0.05f, 0.25f, k * 0.6f), 0.25f, 0.25f, 0.25f, 0.25f, 8, 0f, dark, dark, true, true);
            PG.Box2(g, new Vector3(0f, 0.45f, -0.15f), new Vector3(0.16f, 0.16f, 0.5f), paint, paint);
            PG.Box2(g, new Vector3(0f, 0.68f, -0.3f), new Vector3(0.15f, 0.06f, 0.3f), dark, dark);
            PG.Beam(g, new Vector3(0f, 0.4f, 0.55f), new Vector3(0f, 1.0f, 0.45f), 0.05f, 0.05f, paint);
            PG.Beam(g, new Vector3(-0.3f, 1.0f, 0.45f), new Vector3(0.3f, 1.0f, 0.45f), 0.02f, 0.02f, dark);
            return m;
        }

        public static readonly Color32[] CarPaints =
        {
            C(238, 238, 234), C(40, 42, 48), C(186, 40, 44), C(64, 120, 186), C(150, 156, 160), C(232, 196, 120), C(80, 168, 150), C(250, 176, 186)
        };
    }
}
