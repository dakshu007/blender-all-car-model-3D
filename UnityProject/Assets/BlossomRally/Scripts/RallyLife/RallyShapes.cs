// RallyShapes.cs - low-poly vertex-coloured templates for RallyLife (built with LifeGeo from
// ValleyLife.cs): people (one parametric figure for spectators, marshals, mechanics, farmers, monks,
// kids...), rally props, vehicles, boats and animals. Vertex alpha = wind sway weight (0 = static).
// Conventions: feet / base at the origin, facing +Z, metres.
using UnityEngine;

namespace BlossomRally
{
    internal static class RallyShapes
    {
        static Color32 C(int r, int g, int b, int a = 0) { return LifeGeo.C(r, g, b, a); }
        static Color32 Sh(Color32 c, float k) { return LifeGeo.Shade(c, k); }

        // ============================================================== people
        public const int Standing = 0, LegsSeparate = 1, Sitting = 2, Robe = 3;
        public const int HatNone = 0, HatCap = 1, HatConical = 2, HatBun = 3, HatBald = 4, HatBand = 5, HatElder = 6, HatHelmet = 7;

        public static readonly Vector3 ShoulderR = new Vector3(0.2f, 1.33f, 0f);
        public static readonly Vector3 ShoulderL = new Vector3(-0.2f, 1.33f, 0f);
        public static readonly Vector3 HipR = new Vector3(0.095f, 0.8f, 0f);
        public static readonly Vector3 HipL = new Vector3(-0.095f, 0.8f, 0f);
        /// <summary>Shoulder height of the Sitting variant (sits on a 0.46 m seat).</summary>
        public const float SitDrop = 0.34f;

        public struct Look
        {
            public Color32 top, bottom, skin, hair, hat, vest;
            public int body, hatStyle;
            public bool vestOn, basket;
        }

        public static readonly Color32[] Skins = { C(242, 204, 170), C(228, 186, 150), C(214, 170, 132), C(246, 214, 184) };
        public static readonly Color32[] Hairs = { C(30, 26, 28), C(46, 34, 30), C(20, 20, 24), C(70, 52, 40) };
        public static readonly Color32[] Tops =
        {
            C(214, 58, 52), C(66, 132, 196), C(244, 196, 72), C(92, 170, 120), C(238, 238, 232), C(232, 128, 160),
            C(56, 62, 92), C(238, 140, 64), C(150, 96, 186), C(52, 150, 160), C(196, 44, 70), C(250, 220, 200)
        };
        public static readonly Color32[] Bottoms = { C(52, 66, 110), C(40, 42, 50), C(96, 84, 70), C(70, 92, 140), C(130, 120, 104) };

        public static Look RandomLook(System.Random rng, int body)
        {
            var k = new Look
            {
                top = Tops[rng.Next(Tops.Length)], bottom = Bottoms[rng.Next(Bottoms.Length)],
                skin = Skins[rng.Next(Skins.Length)], hair = Hairs[rng.Next(Hairs.Length)],
                hat = Tops[rng.Next(Tops.Length)], body = body
            };
            int h = rng.Next(10);
            k.hatStyle = h < 3 ? HatNone : h < 6 ? HatCap : h < 8 ? HatBun : h < 9 ? HatConical : HatElder;
            if (k.hatStyle == HatConical) k.hat = C(214, 184, 118);
            return k;
        }

        public static LifeGeo Person(Look k)
        {
            var g = new LifeGeo();
            const float hex = Mathf.PI / 6f;
            Color32 shoe = C(44, 38, 40);
            float y0 = 0f;
            switch (k.body)
            {
                case Standing:
                    for (int s = -1; s <= 1; s += 2)
                    {
                        g.Frustum(new Vector3(0.095f * s, 0.06f, 0f), new Vector3(0.095f * s, 0.84f, 0f), 0.062f, 0.07f, 0.08f, 0.088f, 5, 0f, k.bottom, k.bottom, false, false);
                        g.Box(new Vector3(0.095f * s, 0.04f, 0.04f), new Vector3(0.058f, 0.04f, 0.1f), shoe);
                    }
                    break;
                case LegsSeparate:
                    g.Frustum(new Vector3(0f, 0.7f, 0f), new Vector3(0f, 0.86f, 0f), 0.17f, 0.12f, 0.18f, 0.12f, 6, hex, k.bottom, k.bottom, true, false);
                    break;
                case Sitting:
                    y0 = -SitDrop;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        g.Frustum(new Vector3(0.1f * s, 0.5f, -0.02f), new Vector3(0.1f * s, 0.48f, 0.4f), 0.085f, 0.085f, 0.075f, 0.075f, 5, 0f, k.bottom, k.bottom, true, true);
                        g.Frustum(new Vector3(0.1f * s, 0.46f, 0.42f), new Vector3(0.1f * s, 0.06f, 0.45f), 0.07f, 0.07f, 0.062f, 0.062f, 5, 0f, k.bottom, k.bottom, false, false);
                        g.Box(new Vector3(0.1f * s, 0.04f, 0.5f), new Vector3(0.056f, 0.04f, 0.1f), shoe);
                    }
                    break;
                default:   // robe down to the feet
                    g.Frustum(new Vector3(0f, 0.02f, 0f), new Vector3(0f, 1.38f, 0f), 0.27f, 0.22f, 0.18f, 0.125f, 6, hex, Sh(k.top, 0.92f), k.top, true, true);
                    break;
            }
            float b = k.body == Sitting ? y0 : 0f;
            if (k.body != Robe)
                g.Frustum(new Vector3(0f, 0.8f + b, 0f), new Vector3(0f, 1.38f + b, 0f), 0.19f, 0.13f, 0.205f, 0.12f, 6, hex, Sh(k.top, 0.94f), k.top, true, true);
            if (k.vestOn)
            {
                g.Frustum(new Vector3(0f, 0.9f + b, 0f), new Vector3(0f, 1.35f + b, 0f), 0.206f, 0.142f, 0.214f, 0.132f, 6, hex, k.vest, k.vest, false, true);
                Color32 refl = C(236, 236, 214);
                g.Frustum(new Vector3(0f, 1.02f + b, 0f), new Vector3(0f, 1.08f + b, 0f), 0.212f, 0.146f, 0.212f, 0.145f, 6, hex, refl, refl, false, false);
            }
            if (k.basket)
            {
                Color32 bam = C(186, 150, 92);
                g.Frustum(new Vector3(0f, 0.92f + b, -0.24f), new Vector3(0f, 1.36f + b, -0.27f), 0.13f, 0.11f, 0.2f, 0.17f, 6, 0f, Sh(bam, 0.85f), bam, true, false);
            }
            g.Frustum(new Vector3(0f, 1.36f + b, 0f), new Vector3(0f, 1.47f + b, 0f), 0.05f, 0.05f, 0.05f, 0.05f, 5, 0f, k.skin, k.skin, false, false);
            Vector3 hc = new Vector3(0f, 1.57f + b, 0.005f);
            g.Ellipsoid(hc, new Vector3(0.105f, 0.12f, 0.11f), 6, 4, k.skin, k.skin);
            Color32 eye = C(30, 26, 30);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 e = new Vector3(0.04f * s, 1.585f + b, 0.112f);
                g.QuadOut(e + new Vector3(-0.011f, -0.014f, 0f), e + new Vector3(0.011f, -0.014f, 0f), e + new Vector3(0.011f, 0.014f, 0f), e + new Vector3(-0.011f, 0.014f, 0f), Vector3.forward, eye, eye, eye, eye);
            }
            Vector3 hairC = new Vector3(0f, 1.61f + b, -0.014f);
            Vector3 hairR = new Vector3(0.112f, 0.098f, 0.115f);
            switch (k.hatStyle)
            {
                case HatBald: break;
                case HatCap:
                    g.Ellipsoid(hairC, hairR, 6, 3, k.hair, k.hair);
                    g.Frustum(new Vector3(0f, 1.62f + b, -0.008f), new Vector3(0f, 1.71f + b, -0.008f), 0.117f, 0.117f, 0.104f, 0.104f, 6, 0f, k.hat, k.hat, false, true);
                    g.Box(new Vector3(0f, 1.63f + b, 0.13f), new Vector3(0.085f, 0.011f, 0.065f), Sh(k.hat, 0.85f));
                    break;
                case HatConical:
                    g.Ellipsoid(hairC, hairR, 6, 3, k.hair, k.hair);
                    g.Frustum(new Vector3(0f, 1.64f + b, 0f), new Vector3(0f, 1.84f + b, 0f), 0.36f, 0.36f, 0f, 0f, 8, 0f, k.hat, Sh(k.hat, 1.1f), true, false);
                    break;
                case HatBun:
                    g.Ellipsoid(hairC, hairR, 6, 3, k.hair, k.hair);
                    g.Ellipsoid(new Vector3(0f, 1.71f + b, -0.07f), new Vector3(0.065f, 0.06f, 0.065f), 5, 3, k.hair, k.hair);
                    break;
                case HatBand:
                    g.Ellipsoid(hairC, hairR, 6, 3, k.hair, k.hair);
                    g.Frustum(new Vector3(0f, 1.6f + b, -0.005f), new Vector3(0f, 1.645f + b, -0.005f), 0.119f, 0.122f, 0.116f, 0.12f, 6, 0f, k.hat, k.hat, false, false);
                    break;
                case HatElder:
                    g.Ellipsoid(hairC, hairR, 6, 3, C(200, 200, 196), C(200, 200, 196));
                    g.Frustum(new Vector3(0f, 1.65f + b, -0.012f), new Vector3(0f, 1.705f + b, -0.012f), 0.1f, 0.1f, 0.09f, 0.09f, 6, 0f, C(30, 30, 36), C(30, 30, 36), false, true);
                    break;
                case HatHelmet:
                    g.Ellipsoid(new Vector3(0f, 1.63f + b, -0.005f), new Vector3(0.13f, 0.12f, 0.135f), 6, 3, k.hat, k.hat);
                    break;
                default:
                    g.Ellipsoid(hairC, hairR, 6, 3, k.hair, k.hair);
                    break;
            }
            return g;
        }

        /// <summary>Arm hanging from a shoulder pivot at the origin.</summary>
        public static LifeGeo Arm(Color32 sleeve, Color32 skin)
        {
            var g = new LifeGeo();
            g.Frustum(new Vector3(0f, 0.03f, 0f), new Vector3(0f, -0.41f, 0f), 0.058f, 0.058f, 0.05f, 0.05f, 5, 0f, sleeve, sleeve, true, true);
            g.Box(new Vector3(0f, -0.46f, 0f), new Vector3(0.035f, 0.05f, 0.032f), skin);
            return g;
        }

        /// <summary>Arm holding a marshal flag on a stick (flag cloth flutters in the wind).</summary>
        public static LifeGeo FlagArm(Color32 sleeve, Color32 skin, Color32 flag)
        {
            var g = Arm(sleeve, skin);
            Color32 stick = C(80, 64, 50);
            g.Frustum(new Vector3(0f, -0.4f, 0f), new Vector3(0f, -1.3f, 0f), 0.013f, 0.013f, 0.011f, 0.011f, 4, 0f, stick, stick, false, true);
            int from = g.v.Count;
            Vector3 a = new Vector3(0f, -1.3f, 0f), bb = new Vector3(0.55f, -1.3f, 0f), c = new Vector3(0.55f, -0.92f, 0f), d = new Vector3(0f, -0.92f, 0f);
            RallyGeo.Quad2(g, a, bb, c, d, flag);
            RallyGeo.Wind(g, from, p => Mathf.Clamp01(p.x / 0.55f) * 0.7f);
            return g;
        }

        /// <summary>Leg from a hip pivot at the origin.</summary>
        public static LifeGeo Leg(Color32 trousers)
        {
            var g = new LifeGeo();
            g.Frustum(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.76f, 0f), 0.08f, 0.085f, 0.062f, 0.066f, 5, 0f, trousers, trousers, true, false);
            g.Box(new Vector3(0f, -0.76f, 0.04f), new Vector3(0.056f, 0.04f, 0.1f), C(44, 38, 40));
            return g;
        }

        /// <summary>Long-handled hoe; pivot at the hands (origin), handle along -Z/+Y, blade at the far end.</summary>
        public static LifeGeo Hoe()
        {
            var g = new LifeGeo();
            Color32 wood = C(150, 112, 70), iron = C(70, 70, 76);
            g.Frustum(new Vector3(0f, 0f, -0.3f), new Vector3(0f, 0f, 1.25f), 0.018f, 0.018f, 0.018f, 0.018f, 4, 0f, wood, wood, true, true);
            g.Box(new Vector3(0f, -0.1f, 1.25f), new Vector3(0.1f, 0.12f, 0.012f), iron);
            return g;
        }

        public static LifeGeo FishingRod()
        {
            var g = new LifeGeo();
            Color32 bam = C(196, 164, 96);
            g.Frustum(Vector3.zero, new Vector3(0f, 0f, 3f), 0.02f, 0.02f, 0.006f, 0.006f, 4, 0f, bam, bam, true, true);
            return g;
        }

        /// <summary>Unit string / line along +Z (scale z for the length).</summary>
        public static LifeGeo StringUnit(Color32 col)
        {
            var g = new LifeGeo();
            RallyGeo.OBox(g, new Vector3(0f, 0f, 0.5f), Vector3.right, Vector3.up, Vector3.forward, new Vector3(0.008f, 0.008f, 0.5f), col);
            return g;
        }

        public static LifeGeo Paddle()
        {
            var g = new LifeGeo();
            Color32 wood = C(170, 124, 70), blade = C(200, 54, 44);
            // shaft from the top grip (origin) down along -Y
            g.Frustum(Vector3.zero, new Vector3(0f, -1.15f, 0f), 0.02f, 0.02f, 0.02f, 0.02f, 4, 0f, wood, wood, true, false);
            g.Box(new Vector3(0f, -1.3f, 0f), new Vector3(0.012f, 0.2f, 0.09f), blade);
            return g;
        }

        // ============================================================== rally props
        public static LifeGeo HayPiece(int variant)
        {
            var g = new LifeGeo();
            Color32 straw = variant % 2 == 0 ? C(226, 196, 112) : C(214, 182, 98);
            Color32 twine = C(150, 110, 60);
            for (int k = 0; k < 2; k++)
            {
                float y = 0.225f + k * 0.45f;
                Color32 s = Sh(straw, k == 0 ? 0.92f : 1.04f);
                g.Box(new Vector3(0f, y, 0f), new Vector3(0.55f, 0.225f, 0.25f), s);
                for (int t = -1; t <= 1; t += 2)
                    g.Box(new Vector3(0.24f * t, y, 0f), new Vector3(0.018f, 0.228f, 0.253f), twine);
            }
            return g;
        }

        /// <summary>Stack of three tyres (r 0.33) with a painted band on the middle one.</summary>
        public static LifeGeo TyreStack(Color32 band)
        {
            var g = new LifeGeo();
            for (int k = 0; k < 3; k++)
                Tyre(g, new Vector3(0f, k * 0.21f, 0f), k == 1 ? band : C(36, 36, 40));
            return g;
        }

        /// <summary>A tyre lying flat (outer r 0.33, inner r 0.19, 0.2 high) from y0 up.</summary>
        public static void Tyre(LifeGeo g, Vector3 at, Color32 col)
        {
            const int n = 8;
            float ro = 0.33f, ri = 0.19f, h = 0.2f;
            Color32 side = col, top = Sh(col, 1.15f), inner = Sh(col, 0.6f);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n, am = (a0 + a1) * 0.5f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 dm = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
                Vector3 lo = at, hi = at + Vector3.up * h, mid = at + Vector3.up * (h * 0.5f);
                // bulging outer wall: two bands
                g.QuadOut(lo + d0 * (ro - 0.03f), lo + d1 * (ro - 0.03f), mid + d1 * ro, mid + d0 * ro, dm, side, side, side, side);
                g.QuadOut(mid + d0 * ro, mid + d1 * ro, hi + d1 * (ro - 0.03f), hi + d0 * (ro - 0.03f), dm, side, side, side, side);
                g.QuadOut(hi + d0 * (ro - 0.03f), hi + d1 * (ro - 0.03f), hi + d1 * ri, hi + d0 * ri, Vector3.up, top, top, top, top);
                g.QuadOut(hi + d0 * ri, hi + d1 * ri, lo + d1 * ri, lo + d0 * ri, -dm, inner, inner, inner, inner);
            }
        }

        public static LifeGeo TapePost()
        {
            var g = new LifeGeo();
            Color32 w = C(240, 240, 236), r = C(210, 40, 40);
            g.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.025f, 0.3f, 0.025f), w);
            g.Box(new Vector3(0f, 0.8f, 0f), new Vector3(0.025f, 0.2f, 0.025f), r);
            g.Box(new Vector3(0f, 1.05f, 0f), new Vector3(0.026f, 0.05f, 0.026f), w);
            return g;
        }

        /// <summary>Chevron board facing -Z (towards the approaching car); arrows point +X when dir = 1.</summary>
        public static LifeGeo Chevron(int dir, bool yellow)
        {
            var g = new LifeGeo();
            Color32 bg = yellow ? C(244, 200, 40) : C(206, 36, 40), fg = yellow ? C(28, 28, 32) : C(248, 248, 244);
            Color32 post = C(120, 120, 124);
            for (int s = -1; s <= 1; s += 2)
                g.Box(new Vector3(0.6f * s, 0.62f, 0.04f), new Vector3(0.04f, 0.62f, 0.04f), post);
            g.Box(new Vector3(0f, 1.2f, 0f), new Vector3(0.85f, 0.32f, 0.025f), bg);
            for (int k = -1; k <= 1; k++)
            {
                float x = k * 0.48f * dir;
                Vector3 tip = new Vector3(x + 0.16f * dir, 1.2f, -0.03f);
                Vector3 up = new Vector3(x - 0.1f * dir, 1.45f, -0.03f), dn = new Vector3(x - 0.1f * dir, 0.95f, -0.03f);
                Vector3 up2 = up + new Vector3(0.14f * dir, 0f, 0f), dn2 = dn + new Vector3(0.14f * dir, 0f, 0f), tip2 = tip + new Vector3(0.14f * dir, 0f, 0f);
                g.QuadOut(up, up2, tip2, tip, Vector3.back, fg, fg, fg, fg);
                g.QuadOut(tip, tip2, dn2, dn, Vector3.back, fg, fg, fg, fg);
            }
            return g;
        }

        /// <summary>Distance board (white, red border, black digits) facing -Z.</summary>
        public static LifeGeo DistanceBoard(string text)
        {
            var g = new LifeGeo();
            Color32 white = C(246, 246, 240), red = C(206, 36, 40), ink = C(24, 24, 28), post = C(120, 120, 124);
            g.Box(new Vector3(0f, 0.75f, 0.03f), new Vector3(0.04f, 0.75f, 0.04f), post);
            g.Box(new Vector3(0f, 1.55f, 0f), new Vector3(0.48f, 0.34f, 0.02f), red);
            g.Box(new Vector3(0f, 1.55f, -0.006f), new Vector3(0.42f, 0.28f, 0.02f), white);
            RallyGeo.Number(g, text, new Vector3(0f, 1.55f, -0.03f), Vector3.right, Vector3.up, Vector3.back, 0.34f, ink);
            return g;
        }

        /// <summary>Pop-up gazebo tent 3 x 3 m.</summary>
        public static LifeGeo Tent(Color32 roof, Color32 trim, float size = 3f)
        {
            var g = new LifeGeo();
            float h = size * 0.5f;
            Color32 leg = C(210, 210, 214);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    g.Box(new Vector3(h * sx * 0.97f, 1.1f, h * sz * 0.97f), new Vector3(0.03f, 1.1f, 0.03f), leg);
            Vector3 apex = new Vector3(0f, 3.0f, 0f);
            Vector3[] c = { new Vector3(-h, 2.2f, -h), new Vector3(h, 2.2f, -h), new Vector3(h, 2.2f, h), new Vector3(-h, 2.2f, h) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = c[i], b = c[(i + 1) % 4];
                Vector3 mid = (a + b) * 0.5f;
                Color32 col = i % 2 == 0 ? roof : Sh(roof, 0.9f);
                g.TriOut(a, b, apex, (mid - Vector3.up * 2.2f) + Vector3.up * 1.5f, col, col, col);
                g.TriOut(a, apex, b, -((mid - Vector3.up * 2.2f) + Vector3.up * 1.5f), Sh(col, 0.7f), Sh(col, 0.7f), Sh(col, 0.7f));
                // valance
                Vector3 o = (mid - new Vector3(0f, 2.2f, 0f)).normalized;
                g.QuadOut(a, b, b - Vector3.up * 0.25f, a - Vector3.up * 0.25f, o, trim, trim, trim, trim);
                g.QuadOut(a, a - Vector3.up * 0.25f, b - Vector3.up * 0.25f, b, -o, Sh(trim, 0.7f), Sh(trim, 0.7f), Sh(trim, 0.7f), Sh(trim, 0.7f));
            }
            return g;
        }

        /// <summary>Street food stall: counter, striped awning, steamer baskets, red lanterns. Front = +Z.</summary>
        public static LifeGeo FoodStall(Color32 awningA, Color32 awningB)
        {
            var g = new LifeGeo();
            Color32 wood = C(150, 100, 60), woodD = C(110, 72, 44), sign = C(196, 40, 40), gold = C(236, 190, 80);
            g.Box(new Vector3(0f, 0.5f, 0.3f), new Vector3(1.2f, 0.5f, 0.35f), wood);
            g.Box(new Vector3(0f, 1.02f, 0.32f), new Vector3(1.25f, 0.03f, 0.4f), woodD);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    g.Box(new Vector3(1.2f * sx, 1.15f, 0.25f + 0.45f * sz), new Vector3(0.04f, 1.15f, 0.04f), woodD);
            // awning: 6 stripes sloping to the front
            for (int i = 0; i < 6; i++)
            {
                float x0 = -1.35f + i * 0.45f, x1 = x0 + 0.45f;
                Color32 col = i % 2 == 0 ? awningA : awningB;
                Vector3 a = new Vector3(x0, 2.45f, -0.3f), b = new Vector3(x1, 2.45f, -0.3f), c = new Vector3(x1, 2.1f, 1.05f), d = new Vector3(x0, 2.1f, 1.05f);
                g.QuadOut(a, b, c, d, Vector3.up, col, col, col, col);
                g.QuadOut(a, d, c, b, Vector3.down, Sh(col, 0.7f), Sh(col, 0.7f), Sh(col, 0.7f), Sh(col, 0.7f));
                Vector3 e = c - Vector3.up * 0.18f, f = d - Vector3.up * 0.18f;
                g.QuadOut(d, c, e, f, Vector3.forward, col, col, col, col);
            }
            g.Box(new Vector3(0f, 2.6f, -0.3f), new Vector3(0.8f, 0.2f, 0.03f), sign);
            g.Box(new Vector3(0f, 2.6f, -0.26f), new Vector3(0.6f, 0.06f, 0.02f), gold);
            Color32 bamboo = C(208, 176, 112);
            for (int k = 0; k < 3; k++)
            {
                float x = -0.7f + k * 0.55f;
                for (int s = 0; s < 3 - (k % 2); s++)
                    g.Frustum(new Vector3(x, 1.05f + s * 0.13f, 0.3f), new Vector3(x, 1.17f + s * 0.13f, 0.3f), 0.2f, 0.2f, 0.2f, 0.2f, 7, 0f, Sh(bamboo, 1f - 0.06f * s), bamboo, false, true);
            }
            Color32 lan = C(220, 40, 36);
            for (int s = -1; s <= 1; s += 2)
                g.Ellipsoid(new Vector3(1.0f * s, 1.85f, 1.0f), new Vector3(0.16f, 0.19f, 0.16f), 6, 3, lan, Sh(lan, 0.8f));
            return g;
        }

        /// <summary>Tall feather / teardrop banner on a flexible pole.</summary>
        public static LifeGeo FeatherFlag(Color32 a, Color32 b, float height)
        {
            var g = new LifeGeo();
            Color32 pole = C(60, 60, 66);
            g.Frustum(Vector3.zero, new Vector3(0f, height, 0f), 0.025f, 0.025f, 0.015f, 0.015f, 4, 0f, pole, pole, false, true);
            int from = g.v.Count;
            int n = 6;
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                float y0 = 0.9f + t0 * (height - 1.0f), y1 = 0.9f + t1 * (height - 1.0f);
                float w0 = 0.6f * Mathf.Sin(Mathf.PI * (0.25f + 0.65f * t0)) + 0.1f, w1 = 0.6f * Mathf.Sin(Mathf.PI * (0.25f + 0.65f * t1)) + 0.1f;
                Color32 col = i == n - 2 ? b : a;
                RallyGeo.Quad2(g, new Vector3(0f, y0, 0f), new Vector3(w0, y0, 0f), new Vector3(w1, y1, 0f), new Vector3(0f, y1, 0f), col);
            }
            RallyGeo.Wind(g, from, p => Mathf.Clamp01(p.x / 0.7f) * 0.8f + 0.1f);
            return g;
        }

        /// <summary>Flag pole with a rectangular flag (wind weight across the cloth).</summary>
        public static LifeGeo FlagPole(Color32 a, Color32 b, float height, float w = 1.2f, float h = 0.8f)
        {
            var g = new LifeGeo();
            Color32 pole = C(236, 236, 236), knob = C(230, 190, 80);
            g.Frustum(Vector3.zero, new Vector3(0f, height, 0f), 0.04f, 0.04f, 0.025f, 0.025f, 5, 0f, pole, pole, false, true);
            g.Ellipsoid(new Vector3(0f, height + 0.05f, 0f), new Vector3(0.06f, 0.06f, 0.06f), 5, 3, knob, knob);
            int from = g.v.Count;
            float y1 = height - 0.1f, y0 = y1 - h;
            RallyGeo.Quad2(g, new Vector3(0f, y0, 0f), new Vector3(w * 0.5f, y0, 0f), new Vector3(w * 0.5f, y1, 0f), new Vector3(0f, y1, 0f), a);
            RallyGeo.Quad2(g, new Vector3(w * 0.5f, y0, 0f), new Vector3(w, y0, 0f), new Vector3(w, y1, 0f), new Vector3(w * 0.5f, y1, 0f), b);
            RallyGeo.Wind(g, from, p => Mathf.Clamp01(p.x / w) * 0.9f);
            return g;
        }

        /// <summary>Low-poly hatchback, 3.9 m long, facing +Z.</summary>
        public static LifeGeo ParkedCar(Color32 paint)
        {
            var g = new LifeGeo();
            Color32 glass = C(52, 64, 86), dark = C(30, 30, 34), lamp = C(250, 240, 210), tail = C(200, 30, 30), trim = Sh(paint, 0.75f);
            g.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.84f, 0.3f, 1.95f), paint);
            g.Box(new Vector3(0f, 0.3f, 0f), new Vector3(0.86f, 0.06f, 1.97f), trim);
            // cabin: trapezoid prism
            float yb = 0.85f, yt = 1.42f;
            Vector3[] lo = { new Vector3(-0.8f, yb, -1.6f), new Vector3(0.8f, yb, -1.6f), new Vector3(0.8f, yb, 0.75f), new Vector3(-0.8f, yb, 0.75f) };
            Vector3[] hi = { new Vector3(-0.66f, yt, -1.4f), new Vector3(0.66f, yt, -1.4f), new Vector3(0.66f, yt, 0.05f), new Vector3(-0.66f, yt, 0.05f) };
            Vector3 ctr = new Vector3(0f, (yb + yt) * 0.5f, -0.5f);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Vector3 mid = (lo[i] + lo[j] + hi[i] + hi[j]) * 0.25f;
                g.QuadOut(lo[i], lo[j], hi[j], hi[i], mid - ctr, glass, glass, glass, glass);
            }
            g.QuadOut(hi[0], hi[1], hi[2], hi[3], Vector3.up, paint, paint, paint, paint);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    g.Frustum(new Vector3(0.78f * sx, 0.32f, 1.25f * sz), new Vector3(0.94f * sx, 0.32f, 1.25f * sz), 0.32f, 0.32f, 0.32f, 0.32f, 8, 0f, dark, dark, true, true);
            for (int s = -1; s <= 1; s += 2)
            {
                g.Box(new Vector3(0.55f * s, 0.62f, 1.96f), new Vector3(0.16f, 0.07f, 0.02f), lamp);
                g.Box(new Vector3(0.6f * s, 0.65f, -1.96f), new Vector3(0.14f, 0.07f, 0.02f), tail);
            }
            return g;
        }

        /// <summary>Rally car with a two-colour livery and roof vent, facing +Z (service park).</summary>
        public static LifeGeo RallyCar(Color32 paint, Color32 stripe, int number)
        {
            var g = ParkedCar(paint);
            g.Box(new Vector3(0f, 0.86f, 0.4f), new Vector3(0.3f, 0.015f, 1.1f), stripe);
            g.Box(new Vector3(0f, 1.43f, -0.65f), new Vector3(0.25f, 0.015f, 0.7f), stripe);
            g.Box(new Vector3(0f, 1.47f, -0.55f), new Vector3(0.12f, 0.04f, 0.1f), C(30, 30, 34));
            // door number panels
            Color32 white = C(246, 246, 240), ink = C(24, 24, 28);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 n = new Vector3(s, 0f, 0f);
                g.Box(new Vector3(0.85f * s, 0.6f, -0.2f), new Vector3(0.02f, 0.2f, 0.3f), white);
                RallyGeo.Number(g, number.ToString(), new Vector3(0.875f * s, 0.6f, -0.2f), new Vector3(0f, 0f, s), Vector3.up, n, 0.24f, ink);
            }
            // rear wing
            g.Box(new Vector3(0f, 1.48f, -1.5f), new Vector3(0.75f, 0.025f, 0.14f), stripe);
            return g;
        }

        public static LifeGeo ToolBox(Color32 col)
        {
            var g = new LifeGeo();
            Color32 drawer = Sh(col, 0.8f), handle = C(200, 200, 204), wheel = C(30, 30, 34);
            g.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.38f, 0.45f, 0.25f), col);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.25f + k * 0.2f;
                g.Box(new Vector3(0f, y, 0.252f), new Vector3(0.34f, 0.006f, 0.004f), drawer);
                g.Box(new Vector3(0f, y + 0.09f, 0.26f), new Vector3(0.12f, 0.012f, 0.01f), handle);
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    g.Box(new Vector3(0.3f * sx, 0.05f, 0.18f * sz), new Vector3(0.04f, 0.05f, 0.04f), wheel);
            return g;
        }

        public static LifeGeo TyreRack(int count)
        {
            var g = new LifeGeo();
            for (int k = 0; k < count; k++) Tyre(g, new Vector3(0f, k * 0.21f, 0f), C(34, 34, 38));
            return g;
        }

        public static LifeGeo BambooPole(float height)
        {
            var g = new LifeGeo();
            Color32 a = C(156, 168, 88), b = C(126, 140, 70);
            int seg = Mathf.Max(2, Mathf.RoundToInt(height / 0.8f));
            for (int i = 0; i < seg; i++)
            {
                float y0 = height * i / seg, y1 = height * (i + 1) / seg;
                g.Frustum(new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f), 0.045f, 0.045f, 0.042f, 0.042f, 5, 0f, a, a, false, i == seg - 1);
                g.Frustum(new Vector3(0f, y1 - 0.03f, 0f), new Vector3(0f, y1, 0f), 0.05f, 0.05f, 0.05f, 0.05f, 5, 0f, b, b, false, false);
            }
            return g;
        }

        // ============================================================== helicopter
        public static LifeGeo HeliBody()
        {
            var g = new LifeGeo();
            Color32 white = C(240, 240, 244), pink = C(232, 96, 140), glass = C(50, 70, 100), dark = C(40, 40, 46);
            g.Ellipsoid(new Vector3(0f, 0f, 0.2f), new Vector3(0.9f, 0.85f, 1.9f), 8, 5, white, Sh(white, 0.85f));
            g.Ellipsoid(new Vector3(0f, 0.12f, 1.05f), new Vector3(0.78f, 0.62f, 1.05f), 8, 4, glass, glass);
            g.Frustum(new Vector3(0f, 0.25f, -1.4f), new Vector3(0f, 0.45f, -5.6f), 0.35f, 0.3f, 0.09f, 0.08f, 6, 0f, white, white, false, true);
            g.Box(new Vector3(0f, 0.05f, 0.2f), new Vector3(0.905f, 0.09f, 1.3f), pink);
            g.Box(new Vector3(0f, 0.32f, -3.4f), new Vector3(0.215f, 0.05f, 1.2f), pink);
            g.TriOut(new Vector3(0f, 0.4f, -5.0f), new Vector3(0f, 1.4f, -5.6f), new Vector3(0f, 0.45f, -5.7f), Vector3.right, pink, pink, pink);
            g.TriOut(new Vector3(0f, 0.4f, -5.0f), new Vector3(0f, 0.45f, -5.7f), new Vector3(0f, 1.4f, -5.6f), Vector3.left, pink, pink, pink);
            g.Box(new Vector3(0f, 0.42f, -5.3f), new Vector3(0.7f, 0.03f, 0.18f), white);
            g.Frustum(new Vector3(0f, 0.75f, 0.1f), new Vector3(0f, 1.05f, 0.1f), 0.12f, 0.12f, 0.1f, 0.1f, 6, 0f, dark, dark, false, true);
            for (int s = -1; s <= 1; s += 2)
            {
                RallyGeo.Beam(g, new Vector3(0.7f * s, -1.15f, 1.2f), new Vector3(0.7f * s, -1.15f, -1.0f), 0.04f, dark);
                RallyGeo.Beam(g, new Vector3(0.7f * s, -1.15f, 0.7f), new Vector3(0.45f * s, -0.6f, 0.7f), 0.03f, dark);
                RallyGeo.Beam(g, new Vector3(0.7f * s, -1.15f, -0.6f), new Vector3(0.45f * s, -0.6f, -0.6f), 0.03f, dark);
            }
            // camera ball under the nose
            g.Ellipsoid(new Vector3(0f, -0.75f, 1.4f), new Vector3(0.18f, 0.18f, 0.18f), 6, 3, dark, dark);
            return g;
        }

        public static LifeGeo HeliRotor(float radius, int blades, float chord)
        {
            var g = new LifeGeo();
            Color32 blade = C(46, 46, 52), hub = C(80, 80, 88);
            for (int i = 0; i < blades; i++)
            {
                float a = i * Mathf.PI * 2f / blades;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), p = new Vector3(-d.z, 0f, d.x);
                Vector3 a0 = d * 0.15f - p * chord * 0.5f, a1 = d * radius - p * chord * 0.5f, a2 = d * radius + p * chord * 0.5f, a3 = d * 0.15f + p * chord * 0.5f;
                RallyGeo.Quad2(g, a0, a1, a2, a3, blade);
            }
            g.Frustum(new Vector3(0f, -0.05f, 0f), new Vector3(0f, 0.08f, 0f), 0.16f, 0.16f, 0.1f, 0.1f, 6, 0f, hub, hub, true, true);
            return g;
        }

        // ============================================================== boats
        /// <summary>Generic boat hull: length L, beam B, sides H, deck colour; bow at +Z.</summary>
        public static void Hull(LifeGeo g, float L, float B, float H, Color32 side, Color32 inner, Color32 rim, float bowLift)
        {
            int n = 10;
            var top = new Vector3[n + 1];
            var bottom = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;                      // 0 stern .. 1 bow
                float z = -L * 0.5f + L * t;
                float w = B * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 0.96f, t)), 0.6f);
                float lift = bowLift * Mathf.Pow(Mathf.Abs(t - 0.5f) * 2f, 2.5f);
                top[i] = new Vector3(w, H + lift, z);
                bottom[i] = new Vector3(w * 0.55f, 0f + lift * 0.6f, z);
            }
            for (int i = 0; i < n; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = M(top[i], s), b = M(top[i + 1], s), c = M(bottom[i + 1], s), d = M(bottom[i], s);
                    g.QuadOut(a, b, c, d, new Vector3(s, -0.3f, 0f), side, side, Sh(side, 0.75f), Sh(side, 0.75f));
                    g.QuadOut(a, d, c, b, new Vector3(-s, 0.3f, 0f), inner, inner, inner, inner);
                    // keel strip
                    if (s > 0) g.QuadOut(M(bottom[i], 1), M(bottom[i + 1], 1), M(bottom[i + 1], -1), M(bottom[i], -1), Vector3.down, Sh(side, 0.6f), Sh(side, 0.6f), Sh(side, 0.6f), Sh(side, 0.6f));
                    // floor (inside, a bit above the keel)
                    if (s > 0)
                    {
                        Vector3 f0 = M(bottom[i], 1) + Vector3.up * 0.08f, f1 = M(bottom[i + 1], 1) + Vector3.up * 0.08f;
                        Vector3 f2 = M(bottom[i + 1], -1) + Vector3.up * 0.08f, f3 = M(bottom[i], -1) + Vector3.up * 0.08f;
                        g.QuadOut(f0, f1, f2, f3, Vector3.up, inner, inner, inner, inner);
                    }
                    // gunwale rim
                    g.QuadOut(a, b, b + new Vector3(-0.06f * s, 0f, 0f), a + new Vector3(-0.06f * s, 0f, 0f), Vector3.up, rim, rim, rim, rim);
                }
        }

        static Vector3 M(Vector3 p, int s) { return new Vector3(p.x * s, p.y, p.z); }

        public static LifeGeo Sampan()
        {
            var g = new LifeGeo();
            Color32 wood = C(132, 92, 58), inner = C(160, 116, 74), rim = C(96, 64, 40), mat = C(196, 168, 108);
            Hull(g, 4.6f, 1.25f, 0.42f, wood, inner, rim, 0.25f);
            // arched woven canopy over the middle
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.58f, 0.42f + Mathf.Sin(a0) * 0.62f, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.58f, 0.42f + Mathf.Sin(a1) * 0.62f, 0f);
                Vector3 z0 = new Vector3(0f, 0f, -0.9f), z1 = new Vector3(0f, 0f, 0.3f);
                Color32 c = Sh(mat, 0.9f + 0.05f * i);
                RallyGeo.Quad2(g, p0 + z0, p1 + z0, p1 + z1, p0 + z1, c);
            }
            return g;
        }

        public static LifeGeo DragonBoat(Color32 body, Color32 stripe)
        {
            var g = new LifeGeo();
            Color32 inner = C(150, 104, 64), rim = C(236, 190, 70);
            Hull(g, 11f, 1.3f, 0.45f, body, inner, rim, 0.3f);
            // painted scale stripe along the side
            for (int s = -1; s <= 1; s += 2)
                g.Box(new Vector3(0.56f * s, 0.3f, 0f), new Vector3(0.02f, 0.05f, 4.2f), stripe);
            Color32 gold = C(240, 196, 70), red = C(214, 44, 38), green = C(56, 150, 96), white = C(244, 244, 236), black = C(20, 20, 24);
            // dragon head at the bow
            Vector3 neck0 = new Vector3(0f, 0.55f, 5.4f), neck1 = new Vector3(0f, 1.25f, 5.95f);
            g.Frustum(neck0, neck1, 0.2f, 0.18f, 0.16f, 0.15f, 6, 0f, body, body, false, false);
            g.Box(new Vector3(0f, 1.32f, 6.15f), new Vector3(0.17f, 0.17f, 0.3f), body);
            g.Box(new Vector3(0f, 1.18f, 6.42f), new Vector3(0.13f, 0.05f, 0.18f), red);
            g.Box(new Vector3(0f, 1.38f, 6.45f), new Vector3(0.14f, 0.06f, 0.16f), gold);
            for (int s = -1; s <= 1; s += 2)
            {
                g.Box(new Vector3(0.17f * s, 1.42f, 6.25f), new Vector3(0.02f, 0.05f, 0.05f), white);
                g.Box(new Vector3(0.18f * s, 1.42f, 6.26f), new Vector3(0.012f, 0.03f, 0.03f), black);
                g.Frustum(new Vector3(0.1f * s, 1.48f, 6.05f), new Vector3(0.18f * s, 1.85f, 5.8f), 0.04f, 0.04f, 0.01f, 0.01f, 4, 0f, gold, gold, false, false);
                g.Frustum(new Vector3(0.12f * s, 1.25f, 6.1f), new Vector3(0.38f * s, 1.05f, 5.85f), 0.05f, 0.05f, 0.01f, 0.01f, 4, 0f, green, green, false, false);
            }
            // curled tail at the stern
            g.Frustum(new Vector3(0f, 0.55f, -5.4f), new Vector3(0f, 1.15f, -5.75f), 0.16f, 0.15f, 0.1f, 0.1f, 6, 0f, body, body, false, false);
            g.Frustum(new Vector3(0f, 1.15f, -5.75f), new Vector3(0f, 1.25f, -5.45f), 0.1f, 0.1f, 0.03f, 0.03f, 6, 0f, gold, gold, false, true);
            // drum on the bow
            g.Frustum(new Vector3(0f, 0.2f, 4.2f), new Vector3(0f, 0.75f, 4.2f), 0.3f, 0.3f, 0.3f, 0.3f, 8, 0f, red, red, false, false);
            g.Disc(new Vector3(0f, 0.75f, 4.2f), 0.3f, 8, 0f, Vector3.up, C(236, 220, 180), C(220, 200, 160));
            return g;
        }

        // ============================================================== animals
        public static LifeGeo Duck(int kind)
        {
            var g = new LifeGeo();
            Color32 body, head, wing, beak = C(240, 150, 40);
            switch (kind)
            {
                case 0: body = C(150, 140, 128); head = C(40, 110, 70); wing = C(110, 96, 84); break;   // mallard drake
                case 1: body = C(156, 120, 84); head = C(140, 106, 72); wing = C(120, 90, 62); break;   // hen
                default: body = C(246, 220, 96); head = C(250, 228, 110); wing = C(232, 204, 80); break; // duckling
            }
            g.Ellipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(0.13f, 0.09f, 0.2f), 6, 4, wing, body);
            g.Frustum(new Vector3(0f, 0.1f, 0.13f), new Vector3(0f, 0.24f, 0.16f), 0.045f, 0.04f, 0.04f, 0.04f, 5, 0f, body, head, false, false);
            g.Ellipsoid(new Vector3(0f, 0.27f, 0.17f), new Vector3(0.06f, 0.06f, 0.075f), 6, 3, head, head);
            g.Box(new Vector3(0f, 0.255f, 0.26f), new Vector3(0.022f, 0.012f, 0.04f), beak);
            g.Tri2(new Vector3(-0.05f, 0.11f, -0.17f), new Vector3(0.05f, 0.11f, -0.17f), new Vector3(0f, 0.17f, -0.27f), wing, wing, wing);
            return g;
        }

        public static LifeGeo CraneBody()
        {
            var g = new LifeGeo();
            Color32 white = C(246, 246, 242), black = C(26, 26, 30), red = C(214, 30, 36), bill = C(196, 186, 140), leg = C(40, 40, 44);
            g.Ellipsoid(Vector3.zero, new Vector3(0.2f, 0.18f, 0.5f), 6, 4, white, Sh(white, 0.9f));
            g.Frustum(new Vector3(0f, 0.04f, 0.38f), new Vector3(0f, 0.08f, 1.15f), 0.06f, 0.06f, 0.035f, 0.035f, 5, 0f, black, black, false, false);
            g.Ellipsoid(new Vector3(0f, 0.09f, 1.2f), new Vector3(0.06f, 0.06f, 0.08f), 5, 3, white, white);
            g.Box(new Vector3(0f, 0.15f, 1.2f), new Vector3(0.035f, 0.012f, 0.04f), red);
            g.Frustum(new Vector3(0f, 0.08f, 1.26f), new Vector3(0f, 0.07f, 1.45f), 0.018f, 0.018f, 0.004f, 0.004f, 4, 0f, bill, bill, false, false);
            g.Tri2(new Vector3(-0.12f, 0.04f, -0.42f), new Vector3(0.12f, 0.04f, -0.42f), new Vector3(0f, 0.02f, -0.75f), black, black, black);
            for (int s = -1; s <= 1; s += 2)
                g.Frustum(new Vector3(0.05f * s, -0.08f, -0.35f), new Vector3(0.04f * s, -0.12f, -1.25f), 0.015f, 0.015f, 0.012f, 0.012f, 4, 0f, leg, leg, false, true);
            return g;
        }

        public static LifeGeo CraneWing(int side)
        {
            var g = new LifeGeo();
            Color32 white = C(244, 244, 240), black = C(28, 28, 32);
            float s = side;
            Vector3 rf = new Vector3(0f, 0f, 0.2f), rb = new Vector3(0f, 0f, -0.22f);
            Vector3 mf = new Vector3(0.6f * s, 0.02f, 0.16f), mb = new Vector3(0.58f * s, 0f, -0.3f);
            Vector3 tf = new Vector3(1.15f * s, 0f, 0.05f), tb = new Vector3(1.05f * s, 0f, -0.3f);
            g.Tri2(rf, mf, mb, white, white, white);
            g.Tri2(rf, mb, rb, white, white, white);
            g.Tri2(mf, tf, tb, black, black, black);
            g.Tri2(mf, tb, mb, black, black, black);
            return g;
        }

        public static readonly Color32[] CatFur = { C(226, 140, 64), C(36, 34, 38), C(210, 208, 204), C(150, 130, 112) };

        public static LifeGeo CatBody(Color32 fur)
        {
            var g = new LifeGeo();
            Color32 belly = Sh(fur, 1.15f), nose = C(220, 130, 140), eye = C(110, 170, 70);
            g.Ellipsoid(new Vector3(0f, 0.13f, -0.03f), new Vector3(0.1f, 0.13f, 0.13f), 6, 4, fur, Sh(fur, 0.85f));
            g.Ellipsoid(new Vector3(0f, 0.2f, 0.06f), new Vector3(0.07f, 0.09f, 0.06f), 5, 3, belly, belly);
            for (int s = -1; s <= 1; s += 2)
                g.Box(new Vector3(0.045f * s, 0.03f, 0.08f), new Vector3(0.022f, 0.03f, 0.035f), belly);
            Vector3 hc = new Vector3(0f, 0.31f, 0.07f);
            g.Ellipsoid(hc, new Vector3(0.072f, 0.065f, 0.068f), 6, 3, fur, fur);
            for (int s = -1; s <= 1; s += 2)
            {
                g.Tri2(hc + new Vector3(0.025f * s, 0.045f, -0.01f), hc + new Vector3(0.065f * s, 0.04f, 0f), hc + new Vector3(0.05f * s, 0.11f, -0.01f), fur, fur, fur);
                g.Box(hc + new Vector3(0.028f * s, 0.012f, 0.064f), new Vector3(0.01f, 0.01f, 0.004f), eye);
            }
            g.Box(hc + new Vector3(0f, -0.012f, 0.068f), new Vector3(0.008f, 0.006f, 0.004f), nose);
            return g;
        }

        /// <summary>Tail from a pivot at the origin, curling along -Z then up.</summary>
        public static LifeGeo CatTail(Color32 fur)
        {
            var g = new LifeGeo();
            Vector3 a = Vector3.zero, b = new Vector3(0f, -0.01f, -0.12f), c = new Vector3(0.04f, 0.02f, -0.22f), d = new Vector3(0.09f, 0.1f, -0.25f);
            g.Frustum(a, b, 0.022f, 0.022f, 0.02f, 0.02f, 4, 0f, fur, fur, false, false);
            g.Frustum(b, c, 0.02f, 0.02f, 0.018f, 0.018f, 4, 0f, fur, fur, false, false);
            g.Frustum(c, d, 0.018f, 0.018f, 0.012f, 0.012f, 4, 0f, fur, fur, false, true);
            return g;
        }

        public static LifeGeo ChickenBody(Color32 plumage)
        {
            var g = new LifeGeo();
            Color32 leg = C(230, 170, 60), tail = Sh(plumage, 0.7f);
            g.Ellipsoid(new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.11f, 0.14f), 6, 4, plumage, Sh(plumage, 0.88f));
            g.Tri2(new Vector3(-0.03f, 0.25f, -0.1f), new Vector3(0.03f, 0.25f, -0.1f), new Vector3(0f, 0.4f, -0.2f), tail, tail, tail);
            g.Tri2(new Vector3(-0.05f, 0.22f, -0.11f), new Vector3(0f, 0.36f, -0.17f), new Vector3(-0.02f, 0.3f, -0.22f), tail, tail, tail);
            for (int s = -1; s <= 1; s += 2)
            {
                g.Frustum(new Vector3(0.035f * s, 0.13f, 0f), new Vector3(0.035f * s, 0.01f, 0.01f), 0.01f, 0.01f, 0.008f, 0.008f, 4, 0f, leg, leg, false, false);
                g.Box(new Vector3(0.035f * s, 0.006f, 0.03f), new Vector3(0.015f, 0.006f, 0.03f), leg);
            }
            return g;
        }

        /// <summary>Chicken neck + head from a pivot at the neck base (origin).</summary>
        public static LifeGeo ChickenHead(Color32 plumage)
        {
            var g = new LifeGeo();
            Color32 comb = C(220, 40, 40), beak = C(236, 180, 70), eye = C(20, 20, 20);
            g.Frustum(Vector3.zero, new Vector3(0f, 0.1f, 0.04f), 0.045f, 0.045f, 0.035f, 0.035f, 5, 0f, plumage, plumage, false, false);
            g.Ellipsoid(new Vector3(0f, 0.12f, 0.05f), new Vector3(0.04f, 0.045f, 0.05f), 5, 3, plumage, plumage);
            g.Box(new Vector3(0f, 0.17f, 0.05f), new Vector3(0.008f, 0.025f, 0.035f), comb);
            g.Box(new Vector3(0f, 0.08f, 0.08f), new Vector3(0.008f, 0.018f, 0.01f), comb);
            g.Box(new Vector3(0f, 0.115f, 0.1f), new Vector3(0.01f, 0.01f, 0.022f), beak);
            for (int s = -1; s <= 1; s += 2) g.Box(new Vector3(0.035f * s, 0.13f, 0.07f), new Vector3(0.004f, 0.008f, 0.008f), eye);
            return g;
        }

        static readonly Color32 BuffaloHide = C(70, 68, 72);

        public static LifeGeo BuffaloBody()
        {
            var g = new LifeGeo();
            Color32 hide = BuffaloHide, belly = Sh(hide, 0.8f), hoof = C(30, 28, 30);
            g.Ellipsoid(new Vector3(0f, 1.05f, 0f), new Vector3(0.55f, 0.5f, 1.15f), 8, 5, hide, belly);
            g.Ellipsoid(new Vector3(0f, 1.25f, 0.55f), new Vector3(0.5f, 0.42f, 0.55f), 7, 4, hide, belly);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    g.Frustum(new Vector3(0.3f * sx, 0.85f, 0.7f * sz), new Vector3(0.3f * sx, 0.08f, 0.72f * sz), 0.13f, 0.13f, 0.08f, 0.08f, 5, 0f, hide, belly, false, false);
                    g.Box(new Vector3(0.3f * sx, 0.05f, 0.74f * sz), new Vector3(0.08f, 0.05f, 0.09f), hoof);
                }
            return g;
        }

        /// <summary>Head from a neck pivot (origin), muzzle along +Z, swept-back crescent horns.</summary>
        public static LifeGeo BuffaloHead()
        {
            var g = new LifeGeo();
            Color32 hide = BuffaloHide, muzzle = C(50, 48, 52), horn = C(200, 190, 170), eye = C(16, 16, 18);
            g.Frustum(new Vector3(0f, 0f, -0.1f), new Vector3(0f, -0.1f, 0.25f), 0.24f, 0.26f, 0.2f, 0.2f, 6, 0f, hide, hide, false, false);
            g.Ellipsoid(new Vector3(0f, -0.12f, 0.42f), new Vector3(0.19f, 0.21f, 0.3f), 6, 4, hide, Sh(hide, 0.9f));
            g.Ellipsoid(new Vector3(0f, -0.24f, 0.66f), new Vector3(0.14f, 0.12f, 0.12f), 6, 3, muzzle, muzzle);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 h0 = new Vector3(0.12f * s, 0.02f, 0.32f), h1 = new Vector3(0.38f * s, 0.06f, 0.22f), h2 = new Vector3(0.55f * s, 0.12f, 0f), h3 = new Vector3(0.5f * s, 0.24f, -0.2f);
                g.Frustum(h0, h1, 0.07f, 0.07f, 0.06f, 0.06f, 5, 0f, horn, horn, false, false);
                g.Frustum(h1, h2, 0.06f, 0.06f, 0.045f, 0.045f, 5, 0f, horn, horn, false, false);
                g.Frustum(h2, h3, 0.045f, 0.045f, 0.012f, 0.012f, 5, 0f, horn, horn, false, true);
                g.Tri2(new Vector3(0.15f * s, -0.04f, 0.3f), new Vector3(0.32f * s, -0.12f, 0.25f), new Vector3(0.16f * s, -0.14f, 0.34f), hide, hide, hide);
                g.Box(new Vector3(0.17f * s, -0.08f, 0.5f), new Vector3(0.012f, 0.02f, 0.02f), eye);
            }
            return g;
        }

        public static LifeGeo BuffaloTail()
        {
            var g = new LifeGeo();
            Color32 hide = BuffaloHide, tuft = C(30, 28, 30);
            g.Frustum(Vector3.zero, new Vector3(0f, -0.7f, -0.08f), 0.035f, 0.035f, 0.025f, 0.025f, 4, 0f, hide, hide, false, false);
            g.Ellipsoid(new Vector3(0f, -0.78f, -0.09f), new Vector3(0.05f, 0.1f, 0.05f), 4, 3, tuft, tuft);
            return g;
        }

        // ============================================================== village props
        public static LifeGeo ChessTable()
        {
            var g = new LifeGeo();
            Color32 stone = C(176, 170, 160), top = C(196, 190, 180), board = C(226, 196, 140), line = C(150, 60, 40);
            g.Frustum(Vector3.zero, new Vector3(0f, 0.68f, 0f), 0.22f, 0.22f, 0.16f, 0.16f, 6, 0f, Sh(stone, 0.85f), stone, true, false);
            g.Frustum(new Vector3(0f, 0.68f, 0f), new Vector3(0f, 0.75f, 0f), 0.5f, 0.5f, 0.5f, 0.5f, 8, 0f, top, top, true, true);
            g.Box(new Vector3(0f, 0.755f, 0f), new Vector3(0.25f, 0.006f, 0.28f), board);
            for (int k = -2; k <= 2; k++)
            {
                g.Box(new Vector3(0f, 0.762f, k * 0.11f), new Vector3(0.23f, 0.002f, 0.004f), line);
                g.Box(new Vector3(k * 0.1f, 0.762f, 0f), new Vector3(0.004f, 0.002f, 0.26f), line);
            }
            var rng = new System.Random(5);
            for (int i = 0; i < 18; i++)
            {
                float x = -0.2f + (float)rng.NextDouble() * 0.4f, z = -0.24f + (float)rng.NextDouble() * 0.48f;
                Color32 pc = i % 2 == 0 ? C(200, 40, 36) : C(30, 30, 34);
                g.Frustum(new Vector3(x, 0.758f, z), new Vector3(x, 0.775f, z), 0.022f, 0.022f, 0.022f, 0.022f, 6, 0f, C(236, 220, 180), pc, false, true);
            }
            return g;
        }

        public static LifeGeo Stool()
        {
            var g = new LifeGeo();
            Color32 stone = C(176, 170, 160);
            g.Frustum(Vector3.zero, new Vector3(0f, 0.44f, 0f), 0.2f, 0.2f, 0.17f, 0.17f, 6, 0f, Sh(stone, 0.85f), stone, false, true);
            return g;
        }

        /// <summary>Wind chime top: a small bronze roof cap with a hook string (origin = hook point).</summary>
        public static LifeGeo ChimeTop()
        {
            var g = new LifeGeo();
            Color32 bronze = C(176, 120, 60), cord = C(150, 40, 40);
            g.Frustum(new Vector3(0f, -0.2f, 0f), Vector3.zero, 0.006f, 0.006f, 0.006f, 0.006f, 4, 0f, cord, cord, false, false);
            g.Frustum(new Vector3(0f, -0.26f, 0f), new Vector3(0f, -0.18f, 0f), 0.13f, 0.13f, 0.03f, 0.03f, 6, 0f, bronze, Sh(bronze, 1.2f), true, false);
            return g;
        }

        /// <summary>Hanging chime rod from a pivot at its top (origin).</summary>
        public static LifeGeo ChimeRod(float len)
        {
            var g = new LifeGeo();
            Color32 metal = C(206, 200, 186);
            g.Frustum(Vector3.zero, new Vector3(0f, -len, 0f), 0.012f, 0.012f, 0.012f, 0.012f, 5, 0f, metal, Sh(metal, 0.8f), true, true);
            return g;
        }

        /// <summary>Straw-roofed lean-to field shelter (farmers) - front open towards +Z.</summary>
        public static LifeGeo FieldBasket()
        {
            var g = new LifeGeo();
            Color32 bam = C(186, 150, 92);
            g.Frustum(Vector3.zero, new Vector3(0f, 0.38f, 0f), 0.18f, 0.18f, 0.26f, 0.26f, 7, 0f, Sh(bam, 0.85f), bam, true, false);
            g.Frustum(new Vector3(0f, 0.36f, 0f), new Vector3(0f, 0.42f, 0f), 0.24f, 0.24f, 0.24f, 0.24f, 7, 0f, C(96, 140, 60), C(110, 160, 70), false, true);
            return g;
        }
    }
}
