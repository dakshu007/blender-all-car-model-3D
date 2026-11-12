// CanyonRegion.cs - Red Canyon (丹霞): an original Danxia-landform desert in the north-west corner of the world.
// Striped red / rust / orange / cream / ochre / lilac rock strata, terraced mesas and buttes, a slot canyon,
// a gorge, a dry riverbed wash, a crescent-shaped spring in the dunes, a cracked-mud playa and rainbow hills.
// This file is pure (terrain height, ground colour, surfaces, grass); content lives in the *.Build.cs files.
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CanyonRegion : Region
    {
        public override RegionId Id { get { return RegionId.Canyon; } }
        public override string Name { get { return "Red Canyon"; } }
        public override string Hanzi { get { return "丹霞"; } }
        public override string Tagline { get { return "Rainbow rock canyons, mesas and dusty desert tracks"; } }
        public override Color Accent { get { return new Color(0.95f, 0.55f, 0.30f); } }

        // ------------------------------------------------------------------ terrain
        public override float Height(float x, float z)
        {
            float plane = Plane(x, z);
            float h = plane + 2.4f * WorldNoise.Fbm(x, z, 240f, 3, 603) + 1.3f * WorldNoise.Fbm(x, z, 64f, 2, 607);
            float road = CorridorDist(x, z);
            float keep = WorldNoise.Smooth(8f, 34f, road);           // features ease away from the roads
            Vector2 w = WorldNoise.Warp(x, z, 170f, 26f, 611);

            // playa: a flat, slightly sunken clay pan
            float pm = PlayaMask(w.x, w.y);
            if (pm > 0f) h = Mathf.Lerp(h, PlayaH + 0.4f * WorldNoise.Fbm(x, z, 90f, 2, 641), pm);

            // terraced Danxia mesas and buttes
            float feat = 0f;
            for (int i = 0; i < Mesas.Length; i++)
            {
                var m = Mesas[i];
                float dx = w.x - m.c.x, dz = w.y - m.c.y;
                float rmax = Mathf.Max(m.rx, m.rz);
                if (dx * dx + dz * dz >= rmax * rmax) continue;
                float a = m.yaw * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                float lx = (dx * cs + dz * sn) / m.rx, lz = (-dx * sn + dz * cs) / m.rz;
                float k = Mathf.Sqrt(lx * lx + lz * lz);
                if (k >= 1f) continue;
                feat = Mathf.Max(feat, m.height * MesaProfile(k, m.edge));
            }

            // rainbow hills: rounded striped ridges in the north-east
            float hm = HillsMask(x, z);
            if (hm > 0f)
            {
                float r = WorldNoise.Ridged(w.x, w.y, 64f, 3, 621);
                feat = Mathf.Max(feat, hm * (3f + 26f * r * r * (3f - 2f * r)));
            }
            // badlands: small striped rainbow hills along the foot of the southern ridge
            float bm = BadlandsMask(x, z);
            if (bm > 0f)
            {
                float r = WorldNoise.Ridged(w.x, w.y, 52f, 3, 727);
                feat = Mathf.Max(feat, bm * (2.5f + 17f * r * r * (3f - 2f * r)));
            }

            // yardang field: wind-streamlined ridges west of the wash
            float ym = YardangMask(x, z);
            if (ym > 0f) feat = Mathf.Max(feat, ym * 8f * Yardangs(w.x * 0.3f + x * 0.7f, w.y * 0.3f + z * 0.7f));
            h += feat * keep;

            // canyonlands: a terraced plateau, dissected by the road canyons and dry side canyons
            float cl = CanyonlandsMask(x, z);
            if (cl > 0f)
            {
                float slotD = SegDist(x, z, SlotA, SlotB);
                float slot = 1f - WorldNoise.Smooth(14f, 56f, slotD);
                float wz = 0.3f * (1f - slot);
                float d = CorridorDist(x + wz * (w.x - x), z + wz * (w.y - z));
                float n = 0.5f + 0.5f * WorldNoise.Fbm(x, z, 130f, 2, 691);
                float wallStart = Mathf.Lerp(11f + 12f * n, 6.6f, slot);
                float wallW = Mathf.Lerp(31f, 15f, slot);
                float f = Mathf.Clamp01((d - wallStart) / wallW);
                float P = 18f + 11f * (0.5f + 0.5f * WorldNoise.Fbm(x, z, 280f, 2, 697)) + 8f * slot;
                h += cl * P * Terrace(f, 0.22f);
            }

            // dune sea (crescent dunes, gentle windward / steep lee) - only partly eased on the dune track
            float dm = DuneMask(x, z);
            if (dm > 0f) h += dm * Dunes(x, z) * Mathf.Lerp(0.38f, 1f, WorldNoise.Smooth(6f, 30f, road));

            // gorge and dry wash are carved independent of the roads (the main track bridges the gorge)
            float rim = 3f * WorldNoise.Fbm(x, z, 40f, 2, 655);
            h -= GorgeCarve(x, z, rim);
            h += Embankment(x, z);
            if (x < -990f && x > -1300f && z > 670f && z < 1060f)
            {
                float along, total;
                float dw = PolyDist(Wash, x, z, out along, out total);
                if (dw < WashHalf)
                {
                    float bars = 0.5f + 0.5f * WorldNoise.Fbm(x, z, 22f, 2, 661);
                    h -= WashDepth * (1f - WorldNoise.Smooth(WashHalf * 0.45f, WashHalf, dw)) * (0.65f + 0.35f * bars) * WorldNoise.Smooth(0f, 40f, along);
                }
            }

            // Crescent Spring: a basin below the pool level, sandy banks
            float so = OasisSdf(x, z);
            if (so < 70f)
            {
                float bank = PoolLevel + 0.5f + 0.06f * Mathf.Max(0f, so);
                float floor = so < 0f ? PoolLevel - 0.7f - Mathf.Min(1.6f, -so * 0.12f) : bank;
                float t = WorldNoise.Smooth(0f, 70f, so);
                h = Mathf.Lerp(so < 3f ? Mathf.Lerp(floor, PoolLevel + 0.5f, WorldNoise.Smooth(-3f, 3f, so)) : bank, h, t);
            }

            // gateways sit at their stated heights
            h = GateLock(x, z, h, WorldLayout.GateCanyonEast, WorldLayout.HCanyonEast);
            h = GateLock(x, z, h, WorldLayout.GateCanyonSouth, WorldLayout.HCanyonSouth);
            return h;
        }

        static float SegDist(float x, float z, Vector2 a, Vector2 b)
        {
            float t;
            return Spline.DistToSegment(new Vector2(x, z), a, b, out t);
        }

        static float GateLock(float x, float z, float h, Vector2 g, float target)
        {
            float dx = x - g.x, dz = z - g.y;
            float d2 = dx * dx + dz * dz;
            if (d2 > 170f * 170f) return h;
            return Mathf.Lerp(target, h, WorldNoise.Smooth(80f, 170f, Mathf.Sqrt(d2)));
        }

        /// <summary>Terraced Danxia profile: 0 at the foot (k = 1) up to 1 on the plateau, with three ledges.</summary>
        public static float MesaProfile(float k, float edge)
        {
            float f = Mathf.Clamp01((1f - k) / edge);
            return Terrace(f, 0.24f) + 0.03f * (1f - k) * f;
        }

        /// <summary>0..1 ramp with a soft foot / shoulder and three sedimentary ledges (monotonic).</summary>
        public static float Terrace(float f, float ledge)
        {
            f = Mathf.Lerp(f, f * f * (3f - 2f * f), 0.55f);
            const float N = 3f;
            float s = f * N;
            return (s - ledge * Mathf.Sin(2f * Mathf.PI * s) / (2f * Mathf.PI)) / N;
        }

        /// <summary>Yardangs: streamlined wind-carved ridges aligned with the north-west wind (0..1).</summary>
        public static float Yardangs(float x, float z)
        {
            float u = x * 0.8f - z * 0.6f, v = x * 0.6f + z * 0.8f;
            float lane = v / 30f;
            float li = Mathf.Floor(lane);
            float c = Mathf.Abs(lane - li - 0.5f) * 2f;               // 0 on the ridge line, 1 between
            float across = Mathf.Clamp01(1f - c * c * 1.6f);
            float seg = WorldNoise.Value(u / 70f + li * 3.7f, li * 1.3f, 719);
            float along = WorldNoise.Smooth(0.0f, 0.55f, seg);
            return across * across * (3f - 2f * across) * along;
        }

        /// <summary>Crescent dunes moving south-east: gentle windward slopes, steep lee faces, broken crests.</summary>
        public static float Dunes(float x, float z)
        {
            float bend = WorldNoise.Fbm(x, z, 210f, 2, 671);
            float ph = (x * 0.8f - z * 0.6f) / 82f + 1.25f * bend;
            float p = ph - Mathf.Floor(ph);
            float y = p < 0.76f ? WorldNoise.Smooth(0f, 0.76f, p) : 1f - WorldNoise.Smooth(0.76f, 1f, p);
            float amp = 3.5f + 6.5f * (0.5f + 0.5f * WorldNoise.Fbm(x, z, 150f, 2, 677));
            return 5f + amp * y;
        }

        // ------------------------------------------------------------------ ground colour
        static readonly Color[] Strata =
        {
            new Color(0.80f, 0.30f, 0.22f),   // Danxia red
            new Color(0.91f, 0.56f, 0.32f),   // orange
            new Color(0.93f, 0.80f, 0.62f),   // cream
            new Color(0.70f, 0.34f, 0.24f),   // rust
            new Color(0.86f, 0.66f, 0.38f),   // ochre
            new Color(0.72f, 0.58f, 0.66f),   // lilac
            new Color(0.88f, 0.44f, 0.30f),   // vermilion
            new Color(0.93f, 0.72f, 0.52f),   // peach
            new Color(0.64f, 0.26f, 0.22f),   // deep red
            new Color(0.83f, 0.74f, 0.58f),   // pale ochre
        };

        /// <summary>Banded rock colour at world height y (shared with the rock meshes so the stripes line up).</summary>
        public static Color StrataColor(float x, float z, float y)
        {
            float t = (y + 0.004f * x - 0.007f * z + 3.2f * Mathf.Sin(x * 0.0071f + z * 0.0043f) + 1.6f * Mathf.Sin(x * 0.019f - z * 0.023f)) / 4.4f;
            t += 0.3f * Mathf.Sin(t * 1.9f);
            float fl = Mathf.Floor(t);
            float f = t - fl;
            int n = Strata.Length;
            int i0 = ((int)fl % n + n) % n, i1 = (i0 + 1) % n;
            return Color.Lerp(Strata[i0], Strata[i1], WorldNoise.Smooth(0.82f, 1f, f));
        }

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            float rel = h - Plane(x, z);
            float steep = Mathf.Clamp01((0.93f - normal.y) * 4.5f);
            float n1 = WorldNoise.Fbm(x, z, 95f, 2, 681);
            float n2 = WorldNoise.Value(x * 0.085f, z * 0.085f, 683);

            // desert floor: rust sand, darker varnished gravel plains and pale sand sheets at a large scale
            float big = WorldNoise.Fbm(x, z, 320f, 2, 685);
            Color floor = Color.Lerp(new Color(0.84f, 0.55f, 0.39f), new Color(0.91f, 0.71f, 0.51f), 0.5f + 0.5f * n1);
            floor = Color.Lerp(floor, new Color(0.70f, 0.42f, 0.32f), WorldNoise.Smooth(0.1f, 0.55f, big) * 0.6f);
            floor = Color.Lerp(floor, new Color(0.94f, 0.79f, 0.60f), WorldNoise.Smooth(-0.15f, -0.55f, big) * 0.55f);
            floor = Color.Lerp(floor, new Color(0.62f, 0.42f, 0.34f), Mathf.Max(0f, n2 - 0.35f) * 0.6f);

            Color band = StrataColor(x, z, h);
            band = Color.Lerp(band, band * 0.86f, Mathf.Max(0f, -n2) * 0.5f);
            float rock = Mathf.Max(WorldNoise.Smooth(1.2f, 6f, rel), steep);
            // flat plateau tops: weathered caprock, mottled with blown sand and scrub
            float top = WorldNoise.Smooth(0.975f, 0.995f, normal.y) * WorldNoise.Smooth(8f, 14f, rel);
            if (top > 0f)
            {
                Color cap = Color.Lerp(new Color(0.66f, 0.33f, 0.25f), new Color(0.86f, 0.60f, 0.42f), 0.5f + 0.5f * n1);
                cap = Color.Lerp(cap, new Color(0.56f, 0.30f, 0.24f), Mathf.Max(0f, n2) * 0.5f);
                band = Color.Lerp(band, cap, top * 0.8f);
            }
            Color c = Color.Lerp(floor, band, rock);

            // dry wash: pale pebbles and gravel bars
            if (x < -990f && x > -1300f && z > 670f && z < 1060f)
            {
                float along, total;
                float dw = PolyDist(Wash, x, z, out along, out total);
                if (dw < WashHalf + 6f)
                {
                    float bar = WorldNoise.Fbm(x, z, 22f, 2, 661);
                    Color peb = Color.Lerp(new Color(0.80f, 0.74f, 0.64f), new Color(0.93f, 0.88f, 0.78f), 0.5f + 0.5f * bar);
                    if (n2 > 0.45f) peb = Color.Lerp(peb, new Color(0.62f, 0.52f, 0.46f), 0.5f);
                    c = Color.Lerp(c, peb, (1f - WorldNoise.Smooth(WashHalf * 0.55f, WashHalf + 6f, dw)) * 0.85f * WorldNoise.Smooth(0f, 30f, along));
                }
            }

            // playa: pale cracked clay
            Vector2 pw = WorldNoise.Warp(x, z, 170f, 26f, 611);
            float pm = PlayaMask(pw.x, pw.y);
            if (pm > 0f)
            {
                float cell = WorldNoise.Cellular(x, z, 9f, 691);
                Color clay = Color.Lerp(new Color(0.93f, 0.84f, 0.72f), new Color(0.86f, 0.74f, 0.62f), cell);
                c = Color.Lerp(c, clay, pm * (1f - steep));
            }

            // dune sea: golden sand with wind ripples
            float dm = DuneMask(x, z);
            if (dm > 0f)
            {
                float rip = Mathf.Sin((x * 0.8f - z * 0.6f) * 0.42f + 3.5f * n1 + 0.8f * Mathf.Sin(x * 0.05f + z * 0.031f));
                Color sand = Color.Lerp(new Color(0.95f, 0.70f, 0.44f), new Color(0.99f, 0.80f, 0.55f), 0.5f + 0.5f * rip);
                sand = Color.Lerp(sand, new Color(0.88f, 0.56f, 0.36f), Mathf.Clamp01((0.97f - normal.y) * 5f) * 0.6f);   // lee faces in shade
                c = Color.Lerp(c, sand, dm * (1f - 0.6f * WorldNoise.Smooth(6f, 14f, rel - 12f)));
            }

            // Crescent Spring: jade water, dark wet sand, a ring of green
            float so = OasisSdf(x, z);
            if (so < 40f)
            {
                if (so < 0f) c = Color.Lerp(new Color(0.30f, 0.66f, 0.56f), new Color(0.16f, 0.50f, 0.46f), Mathf.Clamp01(-so / 12f));
                else
                {
                    Color wet = new Color(0.62f, 0.48f, 0.36f), green = new Color(0.52f, 0.62f, 0.32f);
                    Color bank = so < 4f ? wet : Color.Lerp(green, c, WorldNoise.Smooth(6f, 34f, so + 6f * n1));
                    c = Color.Lerp(bank, c, WorldNoise.Smooth(2f, 40f, so));
                    if (so < 4f) c = wet;
                }
            }

            // beyond the playable edge: soften into the haze
            float o = WorldLayout.OutsideDistance(new Vector2(x, z));
            if (o > 0f) c = Color.Lerp(c, new Color(0.86f, 0.62f, 0.52f), WorldNoise.Smooth(0f, 200f, o) * 0.35f);
            return c;
        }

        public override Color VergeColor(float x, float z)
        {
            if (DuneMask(x, z) > 0.5f) return new Color(0.96f, 0.78f, 0.56f);
            return new Color(0.88f, 0.70f, 0.54f);
        }

        public override Surface GroundSurface(float x, float z)
        {
            if (DuneMask(x, z) > 0.35f) return Surface.Sand;
            float so = OasisSdf(x, z);
            if (so > 0f && so < 30f) return Surface.Sand;
            if (x < -990f && x > -1300f && z > 670f && z < 1060f)
            {
                float along, total;
                if (PolyDist(Wash, x, z, out along, out total) < WashHalf * 0.8f) return Surface.Gravel;
            }
            if (PlayaMask(x, z) > 0.5f) return Surface.Dirt;
            return Surface.Dirt;
        }

        // ------------------------------------------------------------------ grass: dry tufts at the oasis, a few along the wash
        public override float GrassDensity(float x, float z, float edge)
        {
            float so = OasisSdf(x, z);
            if (so > 2f && so < 34f) return 0.55f * (1f - WorldNoise.Smooth(10f, 34f, so));
            if (edge < 5f && edge > 1.2f)
            {
                float n = WorldNoise.Value(x * 0.08f, z * 0.08f, 701);
                if (n > 0.35f && DuneMask(x, z) < 0.3f) return 0.18f;
            }
            return 0f;
        }

        public override void GrassColors(float x, float z, float r01, out Color root, out Color tip)
        {
            float so = OasisSdf(x, z);
            if (so < 14f)
            {
                root = new Color(0.30f, 0.38f, 0.16f);
                tip = Color.Lerp(new Color(0.58f, 0.70f, 0.30f), new Color(0.78f, 0.74f, 0.40f), r01);
            }
            else
            {
                root = new Color(0.52f, 0.40f, 0.24f);
                tip = Color.Lerp(new Color(0.90f, 0.78f, 0.50f), new Color(0.80f, 0.64f, 0.40f), r01);
            }
        }

        // ------------------------------------------------------------------ flat plots for buildings
        public override void Plan()
        {
            var f = Field;
            if (f == null) return;
            f.AddFlatRect(Caravanserai, new Vector2(27f, 27f), CaravanseraiYaw, f.BaseHeight(Caravanserai.x, Caravanserai.y), 14f);
            f.AddPad(Watchtower, 15f, f.BaseHeight(Watchtower.x, Watchtower.y));
            f.AddFlatRect(TeaHouse, new Vector2(6.8f, 7.6f), -90f, PoolLevel + 1.0f, 5f);
            f.AddPad(Well, 4f, PoolLevel + 0.9f);
            f.AddPad(Pavilion, 6f, f.BaseHeight(Pavilion.x, Pavilion.y));
        }
    }
}
