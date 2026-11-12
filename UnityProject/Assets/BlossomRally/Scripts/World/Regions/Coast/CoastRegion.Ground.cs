// CoastRegion.Ground.cs - Lotus Coast ground: sea-floor sand under the turquoise shelf, wet sand at the
// waterline, bright beaches and grassy dunes, pink-grey granite on the headland and cliffs, lush coastal
// meadows inland, the packed earth of the harbour village. Surfaces, grass density and blade colours.
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CoastRegion
    {
        static Color Lc(Color a, Color b, float t) { return Color.Lerp(a, b, t); }

        static readonly Color SandDry = new Color(0.94f, 0.86f, 0.66f), SandPale = new Color(0.98f, 0.93f, 0.78f);
        static readonly Color SandWet = new Color(0.74f, 0.64f, 0.47f), SandSea = new Color(0.86f, 0.82f, 0.64f);
        static readonly Color SeaBed = new Color(0.36f, 0.50f, 0.46f), SeaWeed = new Color(0.30f, 0.44f, 0.30f);
        static readonly Color DuneGrass = new Color(0.70f, 0.72f, 0.44f), Meadow = new Color(0.47f, 0.68f, 0.30f);
        static readonly Color MeadowLight = new Color(0.62f, 0.78f, 0.38f), MeadowWarm = new Color(0.70f, 0.74f, 0.40f);
        static readonly Color PineFloor = new Color(0.38f, 0.52f, 0.27f);
        static readonly Color Granite = new Color(0.72f, 0.65f, 0.61f), GraniteDark = new Color(0.52f, 0.49f, 0.48f);
        static readonly Color Earth = new Color(0.78f, 0.70f, 0.56f), Mud = new Color(0.50f, 0.56f, 0.36f);
        static readonly Color Paving = new Color(0.76f, 0.73f, 0.68f), PavingWarm = new Color(0.84f, 0.78f, 0.68f), Needles = new Color(0.50f, 0.50f, 0.30f);

        /// <summary>Coastal pine woods (> 0 inside a wood); shared with the tree placement.</summary>
        public static float WoodMask(float x, float z)
        {
            float wx = x + 55f * WorldNoise.Fbm(x, z, 110f, 2, 2253), wz = z + 55f * WorldNoise.Fbm(x, z, 110f, 2, 2257);
            return WorldNoise.Fbm(wx, wz, 165f, 3, 2251, 0.55f);
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            float rock, harbour, hc;
            Zones(x, z, out rock, out harbour, out hc);
            float beach = Mathf.Max(0f, 1f - rock - harbour);
            float d = x - ShoreX(z);
            float n1 = Mathf.Clamp01(0.5f + 0.75f * WorldNoise.Fbm(x, z, 70f, 2, 2241));
            float n2 = Mathf.Clamp01(0.5f + 0.8f * WorldNoise.Fbm(x, z, 17f, 2, 2243));
            float steep = Mathf.Clamp01((0.84f - normal.y) * 4f);
            const float W = WorldField.WaterY;

            Color c;
            if (h < W + 0.05f)
            {
                // under the sea: pale sand on the shelf, weedy teal further out
                float deep = Mathf.Clamp01((W - h) / 7.5f);
                c = Lc(SandSea, SeaBed, Mathf.Sqrt(deep));
                c = Lc(c, SeaWeed, Mathf.Max(0f, n1 - 0.62f) * 1.4f * deep);
                c = Lc(c, GraniteDark, rock * 0.45f + steep * 0.4f);
                if (x > -1090f && h > -1.0f) c = Lc(Mud, SandWet, 0.3f);     // lagoon bed
            }
            else
            {
                // land: meadow base with soft patches
                c = Lc(Meadow, MeadowLight, n1);
                c = Lc(c, MeadowWarm, Mathf.Max(0f, n2 - 0.6f) * 1.6f);
                // needle carpet under the pine woods
                float wood = WoodMask(x, z);
                if (wood > -0.2f && h > 3.5f) c = Lc(c, Lc(PineFloor, Needles, n2 * 0.5f), S(-0.2f, 0.05f, wood) * 0.75f);
                // beach and dunes
                float sand = beach * (1f - S(70f, 120f, d));
                if (sand > 0f)
                {
                    Color s = Lc(SandDry, SandPale, n2 * 0.6f);
                    s = Lc(s, SandWet, 1f - S(0.25f, 0.9f, h - W));
                    float grassy = S(42f, 76f, d) * Mathf.Clamp01(n1 * 1.3f - 0.1f);
                    s = Lc(s, DuneGrass, grassy * 0.8f);
                    c = Lc(c, s, sand);
                }
                // harbour quay: packed earth and sand
                if (harbour > 0f) c = Lc(c, Lc(Paving, PavingWarm, n2), harbour * (1f - S(70f, 96f, d)) * S(-4f, 1f, d));
                // granite: cliffs, the headland rim, outcrops and any steep slope
                float hk = Mathf.Sqrt((x - HeadC.x) * (x - HeadC.x) / (HeadR.x * HeadR.x) + (z - HeadC.y) * (z - HeadC.y) / (HeadR.y * HeadR.y));
                float coastD = Mathf.Max(d, (1f - hk) * HeadR.y);
                float g = Mathf.Max(steep * 1.2f, rock * (1f - S(8f, 26f, coastD)));
                if (hk < 1.1f) g = Mathf.Max(g, Mathf.Max(0f, n2 - 0.62f) * 2.2f);
                if (g > 0f) c = Lc(c, Lc(Granite, GraniteDark, n1 * 0.6f + steep * 0.3f), Mathf.Clamp01(g));
                // offshore islets: granite skirts, scrubby green tops
                if (x < -1380f && h < 6f)
                    foreach (var il in Islets)
                        if ((x - il.x) * (x - il.x) + (z - il.y) * (z - il.y) < il.z * il.z * 2.2f)
                        { c = Lc(Lc(Granite, GraniteDark, n2), Lc(PineFloor, Meadow, n1), S(0.6f, 2.2f, h)); break; }
                // wet rock / sand right at the waterline
                if (h < W + 0.6f) c = Lc(c, Lc(SandWet, GraniteDark, rock), 0.55f * (1f - S(0f, 0.6f, h - W)));
            }
            // lagoon shores: muddy green rims
            if (x > -1092f && x < -988f && z < -138f && z > -372f)
                foreach (var lg in Lagoons)
                {
                    float ax = (x - lg.x) / lg.z, az = (z - lg.y) / lg.w;
                    float k = Mathf.Sqrt(ax * ax + az * az);
                    if (k < 1.3f) c = Lc(c, Mud, (1f - S(0.95f, 1.3f, k)) * 0.5f);
                }
            float o = WorldLayout.OutsideDistance(new Vector2(x, z));
            if (o > 0f && h > W) c = Lc(c, new Color(0.62f, 0.70f, 0.60f), S(0f, 160f, o) * 0.4f);
            return c;
        }

        public override Color VergeColor(float x, float z)
        {
            float d = x - ShoreX(z);
            return d < 130f && z < 140f && z > -440f ? new Color(0.90f, 0.82f, 0.64f) : new Color(0.80f, 0.72f, 0.56f);
        }

        public override Surface GroundSurface(float x, float z)
        {
            float rock, harbour, hc;
            Zones(x, z, out rock, out harbour, out hc);
            float d = x - ShoreX(z);
            if (rock > 0.5f && d < 40f) return Surface.Gravel;
            if (harbour > 0.5f && d < 100f) return Surface.Dirt;
            if (rock + harbour < 0.5f && d < 105f) return Surface.Sand;
            return Surface.Grass;
        }

        public override float GrassDensity(float x, float z, float edge)
        {
            float rock, harbour, hc;
            Zones(x, z, out rock, out harbour, out hc);
            float beach = Mathf.Max(0f, 1f - rock - harbour);
            float d = x - ShoreX(z);
            if (d < 6f) return 0f;
            float n = Mathf.Clamp01(0.5f + 0.75f * WorldNoise.Fbm(x, z, 70f, 2, 2241));
            float dens;
            if (beach > 0.5f && d < 120f) dens = d < 44f ? 0f : 0.5f * Mathf.Clamp01(n * 1.4f - 0.25f);   // dune grass tufts
            else if (harbour > 0.5f && d < 105f) dens = 0f;
            else dens = 0.07f + 0.24f * Mathf.Clamp01(n * 1.5f - 0.4f);                                   // meadows
            if (edge < 4f) dens = Mathf.Max(dens, harbour > 0.5f && d < 105f ? 0.12f : 0.55f * (1f - edge / 4f));
            return dens;
        }

        public override void GrassColors(float x, float z, float r01, out Color root, out Color tip)
        {
            float d = x - ShoreX(z);
            if (d < 125f && z < 150f && z > -440f)
            {
                root = new Color(0.44f, 0.46f, 0.24f);
                tip = Color.Lerp(new Color(0.84f, 0.82f, 0.52f), new Color(0.66f, 0.74f, 0.40f), r01);
            }
            else
            {
                root = new Color(0.20f, 0.34f, 0.12f);
                tip = Color.Lerp(new Color(0.56f, 0.78f, 0.30f), new Color(0.74f, 0.80f, 0.38f), r01 * r01);
            }
        }
    }
}
