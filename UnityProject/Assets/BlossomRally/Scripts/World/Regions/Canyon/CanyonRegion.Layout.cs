// CanyonRegion.Layout.cs - Red Canyon (丹霞) layout data shared by the terrain (pure) and the content (Build):
// mesas and buttes, the gorge, the dry wash, the Crescent Spring oasis, the dune sea, the playa, the rainbow
// hills, the road corridors and every landmark spot. Pure C# (no engine objects) so the harness can use it.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    /// <summary>One terrain mesa / butte: a warped ellipse with a terraced Danxia profile.</summary>
    public struct CanyonMesa
    {
        public Vector2 c;          // centre (world x, z)
        public float rx, rz;       // radii (m) at the foot
        public float yaw;          // degrees, rotates the x radius towards +z
        public float height;       // plateau height above the plain (m)
        public float edge;         // fraction of the radius taken by the slope (0..1)
        public bool crown;         // gets a vertical banded cap-rock cliff ring (Build)
        public bool skyline;       // big enough to be seen from other regions
        public CanyonMesa(float x, float z, float rx, float rz, float yaw, float h, float edge, bool crown, bool sky)
        { c = new Vector2(x, z); this.rx = rx; this.rz = rz; this.yaw = yaw; height = h; this.edge = edge; this.crown = crown; skyline = sky; }
    }

    public sealed partial class CanyonRegion
    {
        // ------------------------------------------------------------------ the plain
        /// <summary>Tilted desert plain through both gateways (south gate 30 m, east gate 40 m, rising to the north-east).</summary>
        public static float Plane(float x, float z) { return 30f + 0.02f * (x + 1010f) + 0.017f * (z - 690f); }

        // ------------------------------------------------------------------ mesas and buttes
        public static readonly CanyonMesa[] Mesas =
        {
            // Red Citadel: the big striped mesa in the middle of the mesa-loop basin
            new CanyonMesa(-842f, 800f, 66f, 48f, 12f, 46f, 0.62f, true, true),
            // north-west table mesa between the watchtower and the dune sea
            new CanyonMesa(-1182f, 1388f, 52f, 40f, 25f, 42f, 0.62f, true, true),
            // watchtower knoll (low, flat topped)
            new CanyonMesa(-1062f, 1394f, 34f, 30f, 0f, 9f, 0.75f, false, false),
            // buttes
            new CanyonMesa(-1360f, 945f, 28f, 26f, 30f, 36f, 0.85f, true, true),
            new CanyonMesa(-1338f, 846f, 22f, 20f, 0f, 27f, 0.88f, true, false),
            new CanyonMesa(-740f, 1150f, 28f, 25f, 40f, 34f, 0.85f, true, true),
            new CanyonMesa(-676f, 1060f, 24f, 22f, 0f, 30f, 0.9f, true, false),
            new CanyonMesa(-930f, 892f, 22f, 20f, 0f, 29f, 0.9f, true, false),
            new CanyonMesa(-1300f, 682f, 28f, 24f, 15f, 28f, 0.85f, true, false),
            new CanyonMesa(-1184f, 700f, 26f, 24f, 0f, 24f, 0.88f, true, false),
            new CanyonMesa(-760f, 700f, 20f, 18f, 0f, 22f, 0.9f, true, false),
            new CanyonMesa(-1160f, 1225f, 22f, 20f, 0f, 26f, 0.9f, true, false),
        };

        // ------------------------------------------------------------------ canyonlands (plateau cut by the road canyons)
        /// <summary>0..1 weight of the dissected plateau: the gorge tableland, the slot block and the central canyon.</summary>
        public static float CanyonlandsMask(float x, float z)
        {
            float k = Mathf.Min(EllK(x, z, new Vector2(-880f, 1085f), 300f, 190f), EllK(x, z, new Vector2(-828f, 942f), 112f, 72f));
            k = Mathf.Min(k, EllK(x, z, new Vector2(-1112f, 1060f), 80f, 190f));
            float m = WorldNoise.Smooth(1.0f, 0.78f, k);
            if (m <= 0f) return 0f;
            m *= WorldNoise.Smooth(0.85f, 1.2f, EllK(x, z, new Vector2(-848f, 778f), 170f, 92f));    // mesa-loop basin
            m *= WorldNoise.Smooth(0.8f, 1.15f, EllK(x, z, new Vector2(-1000f, 1318f), 105f, 72f));  // caravanserai basin
            m *= 1f - HillsMask(x, z);
            return m;
        }

        /// <summary>The slot: where the main track squeezes through the plateau between near-vertical walls.</summary>
        public static readonly Vector2 SlotA = new Vector2(-786f, 914f), SlotB = new Vector2(-878f, 953f);

        /// <summary>Dry side canyons (no road) that dissect the plateau into mesas.</summary>
        public static readonly Vector2[][] SideCanyons =
        {
            new[] { new Vector2(-1024f, 1076f), new Vector2(-1060f, 1098f), new Vector2(-1092f, 1112f) },
            new[] { new Vector2(-782f, 1112f), new Vector2(-735f, 1082f), new Vector2(-700f, 1036f), new Vector2(-690f, 990f) },
            new[] { new Vector2(-1052f, 958f), new Vector2(-1012f, 958f), new Vector2(-988f, 942f) },
            new[] { new Vector2(-1112f, 1196f), new Vector2(-1060f, 1186f), new Vector2(-1022f, 1160f) },
        };

        // ------------------------------------------------------------------ gorge (bridged by the main track)
        public static readonly Vector2[] Gorge =
        {
            new Vector2(-1024f, 1076f), new Vector2(-978f, 1097f), new Vector2(-930f, 1092f), new Vector2(-882f, 1106f),
            new Vector2(-832f, 1096f), new Vector2(-782f, 1112f)
        };
        public const float GorgeDepth = 16f, GorgeFloor = 4f, GorgeTop = 24f;

        // ------------------------------------------------------------------ dry wash (gravel braids from the south gate to the oasis)
        public static readonly Vector2[] Wash =
        {
            new Vector2(-1022f, 704f), new Vector2(-1082f, 744f), new Vector2(-1150f, 774f), new Vector2(-1204f, 822f),
            new Vector2(-1238f, 890f), new Vector2(-1252f, 958f), new Vector2(-1268f, 1012f)
        };
        public const float WashHalf = 30f, WashDepth = 2.6f;

        // ------------------------------------------------------------------ Crescent Spring oasis (月牙泉 inspired)
        public static readonly Vector2 OasisA = new Vector2(-1286f, 1046f);      // outer circle centre
        public const float OasisRA = 44f;
        public static readonly Vector2 OasisB = new Vector2(-1305f, 1063f);      // bite circle (makes the crescent)
        public const float OasisRB = 38f;
        public const float PoolLevel = 24.2f;

        /// <summary>Signed distance to the crescent pool shoreline (negative = water).</summary>
        public static float OasisSdf(float x, float z)
        {
            float da = Mathf.Sqrt((x - OasisA.x) * (x - OasisA.x) + (z - OasisA.y) * (z - OasisA.y)) - OasisRA;
            float db = OasisRB - Mathf.Sqrt((x - OasisB.x) * (x - OasisB.x) + (z - OasisB.y) * (z - OasisB.y));
            return Mathf.Max(da, db);
        }

        // ------------------------------------------------------------------ dune sea, playa, rainbow hills
        public static readonly Vector2 DuneC = new Vector2(-1352f, 1338f);
        public const float DuneRX = 250f, DuneRZ = 230f;
        public static readonly Vector2 PlayaC = new Vector2(-1350f, 712f);
        public const float PlayaRX = 118f, PlayaRZ = 84f, PlayaH = 21f;
        public static readonly Vector2 HillsC = new Vector2(-786f, 1338f);
        public const float HillsRX = 185f, HillsRZ = 150f;

        public static float EllK(float x, float z, Vector2 c, float rx, float rz)
        {
            float dx = (x - c.x) / rx, dz = (z - c.y) / rz;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static readonly Vector2 DuneC2 = new Vector2(-1330f, 1090f);
        public const float DuneRX2 = 95f, DuneRZ2 = 125f;

        /// <summary>0..1 dune sea weight: the big north-west erg plus a lobe that cradles the Crescent Spring.</summary>
        public static float DuneMask(float x, float z)
        {
            float k = Mathf.Min(EllK(x, z, DuneC, DuneRX, DuneRZ), EllK(x, z, DuneC2, DuneRX2, DuneRZ2));
            return WorldNoise.Smooth(1.0f, 0.62f, k);
        }
        public static readonly Vector2 YardangC = new Vector2(-1318f, 880f);
        public const float YardangRX = 115f, YardangRZ = 100f;
        public static float YardangMask(float x, float z) { return WorldNoise.Smooth(1.0f, 0.6f, EllK(x, z, YardangC, YardangRX, YardangRZ)); }
        public static float BadlandsMask(float x, float z)
        {
            return Mathf.Max(WorldNoise.Smooth(1.0f, 0.5f, EllK(x, z, new Vector2(-835f, 650f), 215f, 60f)),
                             WorldNoise.Smooth(1.0f, 0.55f, EllK(x, z, new Vector2(-1170f, 650f), 95f, 40f)));
        }
        public static float PlayaMask(float x, float z)
        {
            float k = EllK(x, z, PlayaC, PlayaRX, PlayaRZ);
            if (k > 1.4f) return 0f;
            k += 0.22f * WorldNoise.Fbm(x, z, 55f, 2, 813);
            return WorldNoise.Smooth(1.0f, 0.7f, k);
        }
        public static float HillsMask(float x, float z) { return WorldNoise.Smooth(1.0f, 0.55f, EllK(x, z, HillsC, HillsRX, HillsRZ)); }

        // ------------------------------------------------------------------ landmark spots
        public static readonly Vector2 Caravanserai = new Vector2(-994f, 1290f);  // 46 x 46 m walled compound
        public const float CaravanseraiYaw = 22f;
        public static readonly Vector2 Watchtower = new Vector2(-1062f, 1394f);
        public static readonly Vector2 TeaHouse = new Vector2(-1227f, 1046f);
        public static readonly Vector2 Well = new Vector2(-1238f, 1012f);
        public static readonly Vector2 Pavilion = new Vector2(-1330f, 1052f);     // on the dune shoulder above the crescent
        public static readonly Vector2 Wreck = new Vector2(-1262f, 760f);          // old truck on the playa
        public static readonly Vector2[] CaravanPath =
        {
            new Vector2(-1010f, 1250f), new Vector2(-1060f, 1225f), new Vector2(-1150f, 1215f), new Vector2(-1222f, 1180f),
            new Vector2(-1262f, 1120f), new Vector2(-1262f, 1080f)
        };

        // ------------------------------------------------------------------ polyline helpers
        public static float PolyDist(Vector2[] pl, float x, float z, out float along, out float total)
        {
            Vector2 p = new Vector2(x, z);
            float best = 1e9f, acc = 0f;
            along = 0f;
            for (int i = 0; i < pl.Length - 1; i++)
            {
                float t;
                float seg = Vector2.Distance(pl[i], pl[i + 1]);
                float d = Spline.DistToSegment(p, pl[i], pl[i + 1], out t);
                if (d < best) { best = d; along = acc + t * seg; }
                acc += seg;
            }
            total = acc;
            return best;
        }

        /// <summary>Where the main track bridges the gorge (the gorge narrows there, like a real crossing point).</summary>
        public static readonly Vector2 GorgeCross = new Vector2(-929.5f, 1093f);

        /// <summary>Depth the gorge carves at (x, z) (0 outside), irregular rims, faded at both ends.</summary>
        public static float GorgeCarve(float x, float z, float rimNoise)
        {
            if (x < -1080f || x > -730f || z < 1030f || z > 1170f) return 0f;
            float along, total;
            float d = PolyDist(Gorge, x, z, out along, out total);
            float dx = x - GorgeCross.x, dz = z - GorgeCross.y;
            float nearX = WorldNoise.Smooth(16f, 70f, Mathf.Sqrt(dx * dx + dz * dz));
            float top = Mathf.Lerp(13f, GorgeTop, nearX), flo = Mathf.Lerp(2.2f, GorgeFloor, nearX);
            d += rimNoise * nearX;
            if (d >= top) return 0f;
            float ends = WorldNoise.Smooth(0f, 46f, along) * WorldNoise.Smooth(total, total - 46f, along);
            float prof = d <= flo ? 1f : 1f - WorldNoise.Smooth(flo, top, d);
            return GorgeDepth * Mathf.Lerp(0.8f, 1f, nearX) * prof * ends;
        }

        /// <summary>Bridge abutment embankments either side of the gorge crossing (keeps the track level onto the deck).</summary>
        public static float Embankment(float x, float z)
        {
            float u = Mathf.Abs(x - GorgeCross.x), v = Mathf.Abs(z - GorgeCross.y);
            if (u > 16f || v > 46f) return 0f;
            float ramp = v < 13f ? WorldNoise.Smooth(6f, 13f, v) : 1f - WorldNoise.Smooth(13f, 46f, v);
            return 2.6f * ramp * WorldNoise.Smooth(16f, 7f, u);
        }
    }
}
