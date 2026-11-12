// MapData.cs - the "Open World Rally - Map Blueprint v1" (1 km x 1 km valley) in world units.
// Blueprint pixel coordinates are converted with P(): 1404 px = 1000 m, map centre = origin,
// +X = east, +Z = north (Unity forward).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum Surface { Grass = 0, Asphalt = 1, Gravel = 2, Dirt = 3, Paving = 4, Snow = 5, Ice = 6, Sand = 7 }

    public class RoadDef
    {
        public string name;
        public Surface surface;
        public float width;
        public bool closed;
        public Vector2[] points;   // world XZ control points
        public float maxGrade = 0.10f;      // steepest allowed slope (heights are clamped to it, terrain is cut / filled)
        public float cutWidth = 9f;         // terrain blends back from the road shoulder over this many metres
        public bool fixedHeight;            // true: the road sits at exactly heightOffset (flat city streets, causeways)
        public float heightOffset;          // added to the sampled terrain height (or the absolute height when fixedHeight)
        public string texture;              // road texture override (Resources/Env/...), null = by surface
        public Color tint = Color.white;    // texture tint (snow-dusted asphalt, red sand tracks, ...)
        public bool lines = true;           // asphalt: white edge lines + dashed centre line
        public RegionId region = RegionId.Valley;
        public bool connector;              // a framework road that links two regions
        /// <summary>Generic roadside detail (guard rails on drop-offs, retaining walls in deep cuts, apex kerbs, gravel
        /// shoulders) is added where nothing else stands. False for roads whose region dresses them completely.</summary>
        public bool autoDetails = true;
        /// <summary>Stretches (x = from, y = to, metres along the road) carried on a bridge: the terrain below is left
        /// alone (water, gorge) and the owning region builds the deck / piers. The road surface stays drivable.</summary>
        public List<Vector2> bridgeSpans;
        public RoadDef(string n, Surface s, float w, bool c, Vector2[] p) { name = n; surface = s; width = w; closed = c; points = p; }
    }

    public static class MapData
    {
        public const float Half = 500f;

        public static Vector2 P(float px, float py)
        {
            return new Vector2((px - 62f) / 1.404f - 500f, 500f - (py - 153f) / 1.404f);
        }

        static Vector2[] Px(params float[] xy)
        {
            var o = new Vector2[xy.Length / 2];
            for (int i = 0; i < o.Length; i++) o[i] = P(xy[i * 2], xy[i * 2 + 1]);
            return o;
        }

        // 1 - Main loop (asphalt, ~3 km), through the Paifang gate at the south apex
        public static readonly Vector2[] MainLoop = Px(
            640, 1296, 540, 1215, 450, 1095, 400, 940, 382, 780, 395, 640, 440, 530, 545, 462, 720, 440, 860, 440,
            985, 460, 1075, 515, 1112, 615, 1114, 770, 1108, 905, 1075, 1010, 995, 1085, 900, 1145, 820, 1188, 725, 1250);

        // 2 - Terrace climb (gravel) up through the tea terraces to the east lookout
        public static readonly Vector2[] TerraceClimb = Px(
            1114, 790, 1160, 800, 1235, 822, 1390, 775, 1262, 702, 1395, 632, 1268, 562, 1415, 470, 1438, 440);

        // 3 - Bamboo run (dirt) loop through the west forest
        public static readonly Vector2[] BambooRun = Px(
            383, 800, 330, 772, 242, 708, 168, 748, 150, 850, 222, 950, 250, 1048, 175, 1108, 205, 1165, 330, 1145, 438, 1092);

        // 4 - Pagoda hairpins (asphalt): four switchback legs up the northern mountain
        public static readonly Vector2[] PagodaHairpins = Px(
            905, 442, 900, 420, 1080, 405, 1095, 388, 1080, 372, 900, 358, 885, 342, 900, 326,
            1075, 312, 1090, 296, 1075, 280, 910, 268, 898, 252, 915, 238, 985, 240);

        // Lantern Street (stone paving) from the gate down to the garage / start
        public static readonly Vector2[] LanternStreet = Px(640, 1296, 640, 1340, 640, 1400, 641, 1462, 600, 1466, 520, 1466, 470, 1466);

        public static readonly Vector2[] River = Px(
            726, 40, 722, 153, 720, 300, 724, 440, 768, 600, 818, 760, 792, 900, 762, 1050, 832, 1185, 900, 1350, 932, 1556, 945, 1680);

        public static readonly Vector2 LakeCenter = P(588, 852);
        public static readonly Vector2 LakeRadii = new Vector2(46f, 28f);

        public static readonly Vector2 MountainCenter = P(1000, 232);
        public const float MountainHeight = 46f;
        public const float MountainSigma = 110f;

        public static readonly Vector2 Gate = P(640, 1322);
        public static readonly Vector2 Garage = P(452, 1466);
        public static readonly Vector2 PagodaTop = P(1004, 210);

        public static readonly Vector2[] Lookouts = { P(457, 830), P(864, 848), P(1440, 436), P(95, 915), P(1072, 184) };

        // Reference house spots from the blueprint (the game lines the houses up along Lantern Street)
        public static readonly Vector3[] Houses =
        {
            new Vector3(565, 1362, -90), new Vector3(568, 1418, -90), new Vector3(722, 1360, 90), new Vector3(790, 1412, 90),
            new Vector3(392, 1392, 0), new Vector3(538, 1520, 180), new Vector3(760, 1506, 90), new Vector3(470, 1395, 0),
            new Vector3(690, 1528, 180), new Vector3(812, 1462, 90)
        };

        // Village ground (beige) rectangle in pixels
        public static readonly Rect VillagePx = new Rect(360, 1330, 530, 226);

        public static bool InBambooForest(Vector2 p)
        {
            Vector2 a = P(62, 310), b = P(350, 1270);
            return p.x > a.x && p.x < b.x && p.y < a.y && p.y > b.y;
        }

        public static bool InTeaTerraces(Vector2 p)
        {
            Vector2 a = P(1190, 340), b = P(1466, 1310);
            return p.x > a.x && p.x < b.x + 40 && p.y < a.y && p.y > b.y;
        }

        public static bool InVillage(Vector2 p)
        {
            Vector2 a = P(VillagePx.xMin, VillagePx.yMin), b = P(VillagePx.xMax, VillagePx.yMax + 60);
            return p.x > a.x && p.x < b.x && p.y < a.y && p.y > b.y;
        }

        public static float LakeK(Vector2 p)
        {
            Vector2 d = p - LakeCenter;
            return Mathf.Sqrt((d.x / LakeRadii.x) * (d.x / LakeRadii.x) + (d.y / LakeRadii.y) * (d.y / LakeRadii.y));
        }

        public static List<RoadDef> Roads()
        {
            return new List<RoadDef>
            {
                new RoadDef("Road_MainLoop", Surface.Asphalt, 10f, true, MainLoop),
                new RoadDef("Road_TerraceClimb", Surface.Gravel, 6.5f, false, TerraceClimb),
                new RoadDef("Road_BambooRun", Surface.Dirt, 6.5f, false, BambooRun),
                new RoadDef("Road_PagodaHairpins", Surface.Asphalt, 8.5f, false, PagodaHairpins),
                new RoadDef("Road_LanternStreet", Surface.Paving, 8f, false, LanternStreet),
            };
        }
    }
}
