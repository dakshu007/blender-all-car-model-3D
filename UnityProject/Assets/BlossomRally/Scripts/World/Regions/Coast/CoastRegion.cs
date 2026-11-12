// CoastRegion.cs - Lotus Coast (莲岸): a sunny South-China coastline west of Blossom Valley.
// Pure half (terrain shape + shared layout). From the sea inwards: karst islets and sea stacks on a turquoise
// shelf, a crescent sand bay with dunes and lotus lagoons in the south, a granite headland with the lighthouse
// in the middle, a stilt fishing harbour with its seafood street in the north bay, and coastal pine hills that
// climb to the valley ridge (east) and the canyon cliffs (north). The sea runs west to the horizon.
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion : Region
    {
        public override RegionId Id { get { return RegionId.Coast; } }
        public override string Name { get { return "Lotus Coast"; } }
        public override string Hanzi { get { return "莲岸"; } }
        public override string Tagline { get { return "Turquoise bays, stilt fishing villages and the lighthouse cliffs"; } }
        public override Color Accent { get { return new Color(0.30f, 0.78f, 0.82f); } }

        // ------------------------------------------------------------------ layout (shared with Build)
        /// <summary>Headland ellipse: centre, semi axes (x, z).</summary>
        public static readonly Vector2 HeadC = new Vector2(-1290f, -5f), HeadR = new Vector2(128f, 80f);
        /// <summary>Lighthouse on the headland's high point.</summary>
        public static readonly Vector2 Lighthouse = new Vector2(-1362f, -4f), KeeperPos = new Vector2(-1336f, 17f);
        /// <summary>Mazu (sea goddess) temple at the north end of the harbour street.</summary>
        public static readonly Vector2 TemplePos = new Vector2(-1192f, 334f);
        /// <summary>Lotus lagoons behind the dunes of the south bay: x, z, radius x, radius z.</summary>
        public static readonly Vector4[] Lagoons =
        {
            new Vector4(-1022f, -318f, 26f, 40f), new Vector4(-1026f, -232f, 24f, 32f), new Vector4(-1058f, -168f, 18f, 22f),
        };
        public const float LagoonY = 0.65f;
        /// <summary>Terrain islets (karst rocks stand on them): x, z, radius, peak height.</summary>
        public static readonly Vector4[] Islets =
        {
            new Vector4(-1440f, -236f, 15f, 1.6f), new Vector4(-1476f, 214f, 18f, 1.8f), new Vector4(-1398f, 380f, 11f, 1.4f),
            new Vector4(-1610f, -60f, 22f, 2.0f), new Vector4(-1660f, 300f, 19f, 1.8f), new Vector4(-1580f, -380f, 17f, 1.6f),
            new Vector4(-1712f, 96f, 14f, 1.5f), new Vector4(-1530f, 60f, 10f, 1.2f), new Vector4(-1500f, -130f, 12f, 1.3f),
        };
        /// <summary>Harbour village quay level.</summary>
        public const float QuayY = 2.9f;
        /// <summary>Crest of the pine ridge (pagoda viewpoint) and the hill above the south bay.</summary>
        public static readonly Vector2 PineHill = new Vector2(-850f, 196f), SouthHill = new Vector2(-880f, -210f);

        static float S(float a, float b, float x) { return WorldNoise.Smooth(a, b, x); }
        static float G(float z, float c, float w) { float t = (z - c) / w; return Mathf.Exp(-t * t); }

        /// <summary>x of the mainland shoreline (height 0) at z. Land is east of it.</summary>
        public static float ShoreX(float z)
        {
            float x = -1238f + 74f * G(z, -272f, 108f) + 50f * G(z, 238f, 80f) - 34f * G(z, 448f, 56f) - 30f * G(z, -448f, 46f);
            return x + 17f * WorldNoise.Fbm(z, 91.7f, 70f, 3, 2201);
        }

        /// <summary>Zone weights along the coast (z warped): rocky cliffs, harbour quay; the rest is sandy beach.</summary>
        public static void Zones(float x, float z, out float rock, out float harbour, out float hc)
        {
            float zw = z + 22f * WorldNoise.Fbm(x, z, 150f, 2, 2207);
            float neck = S(-140f, -100f, zw) * (1f - S(100f, 140f, zw));
            float south = S(-392f, -432f, zw), north = S(388f, 432f, zw);
            rock = Mathf.Clamp01(neck + south + north);
            harbour = S(128f, 160f, zw) * (1f - S(345f, 378f, zw));
            hc = (neck * 12f + south * 15f + north * 19f) / Mathf.Max(0.001f, neck + south + north);
        }

        static float Beach(float d)
        {
            if (d < 0f) return -1.35f * S(0f, -18f, d) - 1.6f * S(-10f, -80f, d) - 6.05f * S(-70f, -280f, d);
            return 1.5f * S(0f, 40f, d) + 1.4f * S(30f, 80f, d);
        }

        static float Rock(float d, float hc)
        {
            if (d < 0f) return -3.2f * S(0f, -12f, d) - 5.8f * S(-8f, -170f, d);
            return hc * S(0f, Mathf.Max(18f, hc * 1.45f), d);
        }

        static float Quay(float d)
        {
            if (d < 0f) return -3.8f * S(0f, -7f, d) - 2.2f * S(-6f, -110f, d) - 3f * S(-100f, -300f, d);
            return QuayY * S(0f, 5f, d);
        }

        /// <summary>Rolling coastal hills (no sea): climbs to the canyon cliffs in the north and the rim in the south.</summary>
        public static float Inland(float x, float z)
        {
            float h = 7f + 0.010f * (x + 1000f) + 6f * WorldNoise.Fbm(x, z, 250f, 3, 2213);
            h += 15f * S(300f, 480f, z) + 9f * S(-330f, -480f, z);
            // the pine ridge (east of the harbour) and a lower hill above the south bay
            float px = (x - PineHill.x) / 150f, pz = (z - PineHill.y) / 120f;
            h += 12f * Mathf.Exp(-(px * px + pz * pz));
            float sx = (x - SouthHill.x) / 120f, sz = (z - SouthHill.y) / 100f;
            h += 7f * Mathf.Exp(-(sx * sx + sz * sz));
            return h;
        }

        public override float Height(float x, float z)
        {
            float rock, harbour, hc;
            Zones(x, z, out rock, out harbour, out hc);
            float beach = Mathf.Max(0f, 1f - rock - harbour);
            float d = x - ShoreX(z);
            float near = beach * Beach(d) + rock * Rock(d, hc) + harbour * Quay(d);
            if (d > 0f)
            {
                // dunes behind the beach and a low bluff where the coast road runs
                if (beach > 0f && d < 190f)
                {
                    float dune = Mathf.Max(0f, WorldNoise.Fbm(x, z, 30f, 2, 2219)) * 2.2f * S(36f, 52f, d) * (1f - S(78f, 100f, d));
                    near += beach * (dune + 3.4f * S(112f, 168f, d));
                }
                if (rock > 0f) near += rock * 2.2f * WorldNoise.Fbm(x, z, 44f, 2, 2221) * S(4f, 30f, d);
                float l0 = beach * 175f + rock * 55f + harbour * 105f;
                near = Mathf.Lerp(near, Inland(x, z), S(l0, l0 + 150f, d));
            }
            float h = near;

            // granite headland (union with the mainland)
            float kx = (x - HeadC.x) / HeadR.x, kz = (z - HeadC.y) / HeadR.y;
            float k2 = kx * kx + kz * kz;
            if (k2 < 6f)
            {
                float r = Mathf.Sqrt(k2) * (1f + 0.19f * WorldNoise.Fbm(x, z, 40f, 2, 2227));
                float hh = 12f + 14f * S(-1200f, -1362f, x) + 2.2f * WorldNoise.Fbm(x, z, 38f, 2, 2231);
                h = Mathf.Max(h, Rock((1f - r) * HeadR.y, hh));
            }

            // offshore islets
            if (x < -1330f)
                foreach (var il in Islets)
                {
                    float dx = x - il.x, dz = z - il.y;
                    float dd = dx * dx + dz * dz, rr = il.z * 2.6f;
                    if (dd > rr * rr) continue;
                    float t = Mathf.Sqrt(dd) / il.z;
                    h = Mathf.Max(h, -9f + (il.w + 9f) * S(2.6f, 0.35f, t));
                }

            // lotus lagoons behind the dunes
            if (x > -1090f && x < -990f && z < -140f && z > -370f)
                foreach (var lg in Lagoons)
                {
                    float ax = (x - lg.x) / lg.z, az = (z - lg.y) / lg.w;
                    float k = Mathf.Sqrt(ax * ax + az * az);
                    if (k < 1.25f) h = Mathf.Lerp(-0.45f, h, S(0.62f, 1.18f, k));
                }

            // gateways: settle the ground at the connector heights
            h = Gate(h, x, z, WorldLayout.GateCoastEast, WorldLayout.HCoastEast);
            h = Gate(h, x, z, WorldLayout.GateCoastNorth, WorldLayout.HCoastNorth);
            // a gentle ramp under the canyon connector as it drops through the ridge notch to the north gate
            if (z > 340f && x > -1100f && x < -890f)
            {
                float ramp = 12f + 4.2f * S(360f, 432f, z) + 0.115f * Mathf.Max(0f, z - 432f);
                h = Mathf.Max(h, ramp - 22f * S(30f, 85f, Mathf.Abs(x + 994f)));
            }

            // harbour village: a flat quay behind the sea wall
            if (z > 120f && z < 370f && d > -6f && d < 120f)
            {
                float k = S(4f, -3f, d) + S(78f, 104f, d) + S(170f, 140f, z) + S(330f, 356f, z);
                h = Mathf.Lerp(QuayY, h, Mathf.Clamp01(k));
            }
            return h;
        }

        static float Gate(float h, float x, float z, Vector2 g, float target)
        {
            float dx = x - g.x, dz = z - g.y;
            float dd = dx * dx + dz * dz;
            if (dd > 190f * 190f) return h;
            return Mathf.Lerp(target, h, S(82f, 172f, Mathf.Sqrt(dd)));
        }
    }
}
