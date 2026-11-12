// MountainsMap.cs - Cloud Peak Mountains (云峰) layout data, pure C# (no engine objects): road control points
// with designed heights, the gorge stream, the jade lake, hand-authored coarse height / ruggedness / forest
// matrices, and the road-corridor grid that makes the terrain follow every road (so cuts and fills stay small).
// Shared by the terrain (MountainsRegion.cs), the harness and the content builder (*.Build.cs).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    /// <summary>One designed road: control points (x, designed height or NaN = interpolate, z) plus corridor settings.</summary>
    public sealed class MountainsRoadPlan
    {
        public string name;
        public Surface surface;
        public float width, fall, maxGrade;
        public Vector3[] pts;           // x, height (NaN = by length), z
        public List<Vector2> bridges;   // bridge spans (metres along the road)
        public bool corridorOnly;       // framework connector: shapes the terrain only, not added as a road
        public MountainsRoadPlan(string n, Surface s, float w, float f, float g, params Vector3[] p)
        { name = n; surface = s; width = w; fall = f; maxGrade = g; pts = p; }
        public Vector2[] Points2D()
        {
            var o = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = new Vector2(pts[i].x, pts[i].z);
            return o;
        }
    }

    public static class MountainsMap
    {
        const float N = float.NaN;
        static Vector3 P(float x, float z, float h = N) { return new Vector3(x, h, z); }

        // ------------------------------------------------------------------ key places
        public static readonly Vector2 Col = new Vector2(-50f, 985f);          // central col junction (road 1 x east / west passes)
        public const float ColH = 70f;
        public static readonly Vector2 Monastery = new Vector2(214f, 1386f);   // plaza centre on the summit
        public const float MonasteryH = 176f;
        public static readonly Vector2 LakeC = new Vector2(176f, 1106f);
        public static readonly Vector2 LakeR = new Vector2(50f, 29f);
        public const float LakeY = 77f;
        public static readonly Vector2 PoolC = new Vector2(-28f, 596f);        // plunge pool under the valley falls
        public static readonly Vector2 CableLow = new Vector2(112f, 1162f), CableHigh = new Vector2(262f, 1404f);   // cable car stations

        // ------------------------------------------------------------------ roads
        public const string RoadMain = "Road_Mountains_CloudPeak";
        public const string RoadEast = "Road_Mountains_EastPass";
        public const string RoadWest = "Road_Mountains_WestPass";
        public const string RoadForest = "Road_Mountains_PineForest";
        public const string RoadSpur = "Road_Mountains_CloudSea";
        public const string RoadRim = "Road_Mountains_BasinRim";

        public static readonly MountainsRoadPlan[] Roads = MakeRoads();

        static MountainsRoadPlan[] MakeRoads()
        {
            var main = new MountainsRoadPlan(RoadMain, Surface.Asphalt, 9f, 34f, 0.11f,
                P(-110f, 700f, WorldLayout.HMountainsSouth), P(-114f, 745f, 25.8f), P(-105f, 795f), P(-91f, 838f), P(-84f, 864f),
                P(-94f, 886f), P(-116f, 886f), P(-158f, 872f), P(-198f, 874f), P(-222f, 890f), P(-218f, 913f), P(-194f, 926f),
                P(-145f, 934f), P(-98f, 940f), P(-62f, 956f), P(Col.x, Col.y, ColH),
                P(-44f, 1020f), P(-24f, 1048f), P(10f, 1068f), P(40f, 1080f), P(48f, 1100f), P(30f, 1116f),
                P(-30f, 1124f), P(-100f, 1130f), P(-160f, 1138f), P(-192f, 1151f), P(-196f, 1173f), P(-172f, 1187f),
                P(-110f, 1193f), P(-30f, 1200f), P(30f, 1210f), P(60f, 1228f), P(54f, 1250f), P(28f, 1262f),
                P(-30f, 1270f), P(-80f, 1280f), P(-104f, 1298f), P(-94f, 1320f), P(-60f, 1332f), P(10f, 1344f),
                P(80f, 1354f), P(140f, 1364f), P(186f, 1374f, MonasteryH));
            var east = new MountainsRoadPlan(RoadEast, Surface.Asphalt, 9f, 20f, 0.10f,
                P(WorldLayout.GateMountainsEast.x, WorldLayout.GateMountainsEast.y, WorldLayout.HMountainsEast),
                P(272f, 1112f), P(218f, 1070f), P(162f, 1044f), P(120f, 1033f), P(76f, 1020f), P(24f, 1000f), P(Col.x, Col.y, ColH));
            var west = new MountainsRoadPlan(RoadWest, Surface.Asphalt, 8.5f, 26f, 0.10f,
                P(WorldLayout.GateMountainsWest.x, WorldLayout.GateMountainsWest.y, WorldLayout.HMountainsWest),
                P(-272f, 954f), P(-214f, 968f), P(-156f, 980f), P(-104f, 987f), P(Col.x, Col.y, ColH));
            var forest = new MountainsRoadPlan(RoadForest, Surface.Gravel, 7.5f, 26f, 0.11f,
                P(-242f, 966f), P(-252f, 920f), P(-282f, 872f), P(-330f, 846f), P(-390f, 828f), P(-422f, 788f),
                P(-416f, 738f), P(-386f, 700f), P(-344f, 668f), P(-300f, 634f, 33f), P(-244f, 628f), P(-196f, 660f),
                P(-204f, 712f), P(-236f, 758f), P(-226f, 806f), P(-190f, 822f), P(-162f, 846f), P(-160f, 871f));
            var spur = new MountainsRoadPlan(RoadSpur, Surface.Gravel, 7.5f, 22f, 0.10f,
                P(244f, 1090f), P(258f, 1040f), P(276f, 990f), P(290f, 940f, 98f), P(300f, 890f), P(318f, 846f), P(324f, 800f, 104f),
                P(318f, 752f, 106f));
            var rim = new MountainsRoadPlan(RoadRim, Surface.Dirt, 7.5f, 22f, 0.10f,
                P(-290f, 951f), P(-330f, 995f), P(-378f, 1040f), P(-408f, 1100f), P(-418f, 1160f), P(-404f, 1220f), P(-372f, 1262f, 84f));
            var all = new[] { main, east, west, forest, spur, rim };
            // open ends that meet another road take that road's designed height there
            for (int r = 1; r < all.Length; r++)
                for (int e = 0; e < 2; e++)
                {
                    int i = e == 0 ? 0 : all[r].pts.Length - 1;
                    if (!float.IsNaN(all[r].pts[i].y)) continue;
                    Vector2 at = new Vector2(all[r].pts[i].x, all[r].pts[i].z);
                    float best = 1e9f, hb = 60f;
                    for (int o = 0; o < r; o++)
                        foreach (var sp in SampleRoad(all[o].pts, 2f))
                        {
                            float d = (new Vector2(sp.x, sp.z) - at).sqrMagnitude;
                            if (d < best) { best = d; hb = sp.y; }
                        }
                    all[r].pts[i].y = hb;
                }
            return all;
        }

        public static MountainsRoadPlan Plan(string name)
        {
            foreach (var r in Roads) if (r.name == name) return r;
            return null;
        }

        // designed heights along the framework connectors (they shape our side of the blend only)
        static readonly float[] ConnSouthH = { 0f, 2.5f, 4.5f, 9f, 16.5f, 22f, WorldLayout.HMountainsSouth };
        static readonly float[] ConnEastH = { WorldLayout.HMountainsEast, 101f, 107f, 113.5f, 119f, WorldLayout.HSnowWest };
        static readonly float[] ConnWestH = { WorldLayout.HMountainsWest, 54.5f, 51f, 47f, 43f, WorldLayout.HCanyonEast };

        // ------------------------------------------------------------------ gorge stream (lake outlet -> valley falls)
        // x, bed height, z; the last points drop into the plunge pool where the valley river ends.
        public static readonly Vector3[] Stream =
        {
            P(150f, 1093f, 76.8f), P(141f, 1076f, 76.4f), P(134f, 1060f, 70f), P(127f, 1046f, 65f), P(121f, 1031f, 63.5f),
            P(114f, 1012f, 59f), P(100f, 988f, 51f), P(78f, 963f, 42f), P(50f, 940f, 35f), P(22f, 900f, 30.5f),
            P(-4f, 852f, 26.5f), P(-28f, 800f, 23f), P(-44f, 745f, 19.5f), P(-46f, 690f, 16.6f), P(-38f, 648f, 14.2f),
            P(-32f, 626f, 12.8f), P(-30f, 614f, 7.5f), P(-29f, 604f, 1.5f), P(-28f, 596f, -2.6f)
        };
        /// <summary>Bridge of the east pass over the lake outlet ravine (point on the stream under the deck).</summary>
        public static Vector2 BridgeAt { get { return new Vector2(Stream[4].x, Stream[4].z); } }

        /// <summary>Distance to the stream centreline; h = bed height there, t = 0..1 along the whole stream.</summary>
        public static float StreamDistance(float x, float z, out float h, out float along)
        {
            float best = 1e9f; h = 0f; along = 0f;
            Vector2 p = new Vector2(x, z);
            int n = Stream.Length;
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 a3 = Stream[i], b3 = Stream[i + 1];
                // cheap reject
                float minx = Mathf.Min(a3.x, b3.x) - 60f, maxx = Mathf.Max(a3.x, b3.x) + 60f;
                if (x < minx || x > maxx) continue;
                float minz = Mathf.Min(a3.z, b3.z) - 60f, maxz = Mathf.Max(a3.z, b3.z) + 60f;
                if (z < minz || z > maxz) continue;
                float t;
                float d = Spline.DistToSegment(p, new Vector2(a3.x, a3.z), new Vector2(b3.x, b3.z), out t);
                if (d < best) { best = d; h = Mathf.Lerp(a3.y, b3.y, t); along = (i + t) / (n - 1); }
            }
            return best;
        }

        // ------------------------------------------------------------------ coarse hand-authored fields
        // 13 columns (x = -600 .. 600, step 100) x 15 rows (z = 400 .. 1800, step 100)
        static readonly float[] HeightGrid =
        {
            //  -600 -500 -400 -300 -200 -100    0  100  200  300  400  500  600
                 30,  25,  15,   8,   5,   4,   5,  15,  28,  35,  40,  55,  70,    // 400
                 36,  30,  20,  13,   9,   7,  10,  26,  40,  46,  54,  68,  80,    // 500
                 40,  36,  30,  24,  18,  14,  18,  40,  56,  62,  70,  86,  98,    // 600
                 44,  42,  38,  34,  29,  24,  30,  60,  66,  76,  88, 102, 112,    // 700
                 47,  46,  44,  41,  37,  32,  42,  72,  62,  96,  82, 116, 124,    // 800
                 50,  50,  50,  50,  48,  46,  50,  70,  72, 102,  90, 124, 132,    // 900
                 52,  56,  62,  60,  63,  66,  72,  70,  86, 104, 112, 128, 138,    // 1000
                 60,  66,  70,  54,  82,  88,  86,  84,  84,  96, 112, 128, 138,    // 1100
                 70,  76,  78,  50, 104, 114, 120, 122, 112, 112, 122, 134, 140,    // 1200
                 84,  88,  92,  70, 128, 140, 146, 146, 150, 136, 138, 145, 150,    // 1300
                110, 100, 108, 130, 155, 162, 165, 168, 172, 165, 160, 165, 168,    // 1400
                150, 150, 150, 165, 175, 185, 195, 205, 210, 205, 192, 185, 185,    // 1500
                180, 180, 180, 185, 190, 200, 210, 220, 225, 220, 210, 200, 200,    // 1600
                200, 200, 200, 205, 210, 215, 220, 230, 235, 230, 220, 215, 210,    // 1700
                205, 205, 205, 210, 215, 220, 225, 235, 240, 235, 225, 220, 215,    // 1800
        };

        static readonly float[] RuggedGrid =
        {
                .2f, .2f, .2f, .2f, .1f, .1f, .2f, .3f, .4f, .4f, .4f, .4f, .4f,
                .3f, .3f, .2f, .15f, .1f, .1f, .2f, .5f, .6f, .6f, .6f, .6f, .6f,
                .4f, .4f, .3f, .25f, .2f, .15f, .5f, .9f, .9f, .8f, .8f, .7f, .7f,
                .4f, .4f, .3f, .25f, .2f, .15f, .6f, 1f, 1f, .9f, .9f, .7f, .7f,
                .4f, .4f, .3f, .25f, .2f, .2f, .6f, 1f, 1f, .8f, 1f, .7f, .7f,
                .4f, .4f, .3f, .25f, .25f, .2f, .5f, .9f, 1f, .8f, 1f, .7f, .7f,
                .4f, .5f, .4f, .3f, .2f, .15f, .3f, .5f, .7f, .6f, .8f, .7f, .7f,
                .5f, .8f, .9f, .6f, .3f, .2f, .2f, .3f, .5f, .5f, .7f, .7f, .7f,
                .5f, .9f, 1f, .8f, .35f, .25f, .25f, .3f, .5f, .6f, .7f, .7f, .7f,
                .6f, .9f, 1f, .9f, .5f, .3f, .3f, .35f, .5f, .7f, .7f, .7f, .7f,
                .7f, .8f, .9f, .9f, .7f, .5f, .4f, .4f, .5f, .7f, .7f, .7f, .7f,
                .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f,
                .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f,
                .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f,
                .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f, .7f,
        };

        /// <summary>Pine forest cover (0 meadow .. 1 forest).</summary>
        static readonly float[] ForestGrid =
        {
                .6f, .6f, .5f, .4f, .3f, .2f, .2f, .3f, .4f, .4f, .5f, .5f, .5f,
                .7f, .7f, .7f, .6f, .5f, .3f, .3f, .4f, .5f, .5f, .5f, .5f, .5f,
                .8f, .8f, .9f, .9f, .8f, .4f, .4f, .6f, .6f, .5f, .5f, .5f, .5f,
                .8f, .9f, 1f, 1f, .9f, .4f, .5f, .7f, .5f, .5f, .5f, .5f, .5f,
                .8f, .9f, 1f, 1f, .9f, .4f, .6f, .7f, .5f, .5f, .5f, .5f, .5f,
                .8f, .9f, 1f, .9f, .7f, .3f, .5f, .6f, .5f, .5f, .6f, .6f, .6f,
                .7f, .8f, .8f, .6f, .3f, .1f, .2f, .4f, .5f, .7f, .7f, .6f, .6f,
                .6f, .7f, .7f, .7f, .4f, .15f, .1f, .2f, .4f, .7f, .7f, .6f, .6f,
                .6f, .7f, .7f, .8f, .5f, .2f, .15f, .2f, .3f, .6f, .6f, .6f, .6f,
                .5f, .6f, .7f, .8f, .5f, .25f, .2f, .2f, .3f, .5f, .6f, .6f, .6f,
                .4f, .5f, .6f, .6f, .5f, .4f, .3f, .3f, .3f, .5f, .5f, .5f, .5f,
                .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f,
                .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f,
                .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f,
                .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f, .4f,
        };

        const int GX = 13, GZ = 15;
        const float G0X = -600f, G0Z = 400f, GStep = 100f;

        static float Cubic(float a, float b, float c, float d, float t)
        {
            // Catmull-Rom
            return b + 0.5f * t * (c - a + t * (2f * a - 5f * b + 4f * c - d + t * (3f * (b - c) + d - a)));
        }

        static float Bicubic(float[] g, float x, float z)
        {
            float fx = (x - G0X) / GStep, fz = (z - G0Z) / GStep;
            fx = Mathf.Clamp(fx, 0f, GX - 1.001f); fz = Mathf.Clamp(fz, 0f, GZ - 1.001f);
            int ix = (int)fx, iz = (int)fz;
            float tx = fx - ix, tz = fz - iz;
            float r0 = 0f, r1 = 0f, r2 = 0f, r3 = 0f;
            for (int k = -1; k <= 2; k++)
            {
                int zz = Mathf.Clamp(iz + k, 0, GZ - 1) * GX;
                float a = g[zz + Mathf.Clamp(ix - 1, 0, GX - 1)], b = g[zz + ix], c = g[zz + Mathf.Min(ix + 1, GX - 1)], d = g[zz + Mathf.Min(ix + 2, GX - 1)];
                float v = Cubic(a, b, c, d, tx);
                if (k == -1) r0 = v; else if (k == 0) r1 = v; else if (k == 1) r2 = v; else r3 = v;
            }
            return Cubic(r0, r1, r2, r3, tz);
        }

        public static float Macro(float x, float z) { return Bicubic(HeightGrid, x, z); }
        public static float Rugged(float x, float z) { return Mathf.Clamp01(Bicubic(RuggedGrid, x, z)); }
        public static float Forest(float x, float z) { return Mathf.Clamp01(Bicubic(ForestGrid, x, z)); }

        // ------------------------------------------------------------------ granite towers (cellular feature points)
        public const float TowerCell = 96f;
        public const int TowerSeed = 3203;

        /// <summary>Feature point of tower cell (cx, cz) in world metres.</summary>
        public static Vector2 TowerPoint(int cx, int cz)
        {
            return new Vector2((cx + WorldNoise.Hash01(cx, cz, TowerSeed)) * TowerCell, (cz + WorldNoise.Hash01(cx, cz, TowerSeed + 17)) * TowerCell);
        }

        // ------------------------------------------------------------------ natural terrain
        public static float Natural(float x, float z)
        {
            float h = Macro(x, z);
            float rug = Rugged(x, z);
            // granite ridges: warped, soft-crested ridged fBm (long knife-edge arêtes between deep valleys)
            float wx = x + 70f * WorldNoise.Value(x * 0.0035f, z * 0.0035f, 3091), wz = z + 70f * WorldNoise.Value(x * 0.0035f, z * 0.0035f, 3093);
            float v = WorldNoise.Fbm(wx, wz, 210f, 4, 3101, 0.45f);
            float crest = Mathf.Max(0f, 1f - Mathf.Sqrt(v * v + 0.0025f) / 0.42f);
            h += rug * 52f * (crest * crest * (1.7f - 0.7f * crest) - 0.30f);
            // isolated granite towers (dressed with rock meshes in Build)
            if (rug > 0.5f)
            {
                float c = WorldNoise.Cellular(x, z, TowerCell, TowerSeed);
                float tw = WorldNoise.Smooth(0.30f, 0.05f, c);
                h += (rug - 0.5f) * 2f * 22f * tw * tw * (3f - 2f * tw);
            }
            h += 3.4f * WorldNoise.Fbm(x, z, 60f, 3, 3301);
            return h;
        }

        // ------------------------------------------------------------------ road corridors (precomputed)
        const float CX0 = -620f, CZ0 = 380f, CCell = 4f;
        const int CNX = 311, CNZ = 316;
        static readonly float[] corK = new float[CNX * CNZ];
        static readonly float[] corKH = new float[CNX * CNZ];

        /// <summary>Sampled centrelines of the designed roads (x, height, z), same spline as the game roads.</summary>
        public static readonly List<List<Vector3>> Sampled = new List<List<Vector3>>();

        static MountainsMap()
        {
            var sumW = new float[CNX * CNZ];
            var sumWH = new float[CNX * CNZ];
            foreach (var r in Roads) Splat(SampleRoad(r.pts, 2f), r.width * 0.5f, r.fall, sumW, sumWH, true);
            foreach (var c in WorldLayout.Connectors)
            {
                float[] hs = c.name == "Road_Link_ValleyMountains" ? ConnSouthH : c.name == "Road_Link_MountainsSnow" ? ConnEastH
                           : c.name == "Road_Link_MountainsCanyon" ? ConnWestH : null;
                if (hs == null || hs.Length != c.points.Length) continue;
                var pts = new Vector3[hs.Length];
                for (int i = 0; i < hs.Length; i++) pts[i] = new Vector3(c.points[i].x, hs[i], c.points[i].y);
                Splat(SampleRoad(pts, 2f), c.width * 0.5f, 30f, sumW, sumWH, false);
            }
            for (int i = 0; i < corK.Length; i++)
                corKH[i] = sumW[i] > 0f ? corK[i] * sumWH[i] / sumW[i] : 0f;
        }

        /// <summary>Catmull-Rom samples (x, h, z); NaN heights are interpolated by length between the known ones.</summary>
        public static List<Vector3> SampleRoad(Vector3[] pts, float step)
        {
            int n = pts.Length;
            var xz = new Vector2[n];
            for (int i = 0; i < n; i++) xz[i] = new Vector2(pts[i].x, pts[i].z);
            var outP = new List<Vector2>();
            var seg = new List<float>();     // fractional control-point index per sample
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = xz[Mathf.Max(i - 1, 0)], p1 = xz[i], p2 = xz[i + 1], p3 = xz[Mathf.Min(i + 2, n - 1)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) / step));
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    outP.Add(Spline.CatmullRom(p0, p1, p2, p3, t));
                    seg.Add(i + t);
                }
            }
            outP.Add(xz[n - 1]); seg.Add(n - 1);
            // arc length at every sample and at every control point
            var s = new float[outP.Count];
            for (int i = 1; i < outP.Count; i++) s[i] = s[i - 1] + Vector2.Distance(outP[i], outP[i - 1]);
            var sc = new float[n];
            for (int i = 0, j = 0; i < n; i++)
            {
                while (j < seg.Count - 1 && seg[j] < i) j++;
                sc[i] = s[j];
            }
            // heights at control points
            var hc = new float[n];
            for (int i = 0; i < n; i++) hc[i] = pts[i].y;
            for (int i = 0; i < n; i++)
            {
                if (!float.IsNaN(hc[i])) continue;
                int a = i - 1, b = i + 1;
                while (b < n && float.IsNaN(pts[b].y)) b++;
                if (a < 0 || b >= n) { hc[i] = a >= 0 ? hc[a] : (b < n ? pts[b].y : 0f); continue; }
                hc[i] = Mathf.Lerp(hc[a], pts[b].y, (sc[i] - sc[a]) / Mathf.Max(0.01f, sc[b] - sc[a]));
            }
            var res = new List<Vector3>(outP.Count);
            for (int i = 0; i < outP.Count; i++)
            {
                int a = Mathf.Min((int)seg[i], n - 2);
                float h = Mathf.Lerp(hc[a], hc[a + 1], Mathf.Clamp01((s[i] - sc[a]) / Mathf.Max(0.01f, sc[a + 1] - sc[a])));
                res.Add(new Vector3(outP[i].x, h, outP[i].y));
            }
            return res;
        }

        static void Splat(List<Vector3> samples, float hw, float fall, float[] sumW, float[] sumWH, bool keep)
        {
            if (keep) Sampled.Add(samples);
            float R = hw + fall;
            int r = Mathf.CeilToInt(R / CCell);
            for (int si = 0; si < samples.Count; si++)
            {
                Vector3 s = samples[si];
                int ci = Mathf.RoundToInt((s.x - CX0) / CCell), cj = Mathf.RoundToInt((s.z - CZ0) / CCell);
                for (int dj = -r; dj <= r; dj++)
                {
                    int j = cj + dj;
                    if (j < 0 || j >= CNZ) continue;
                    float pz = CZ0 + j * CCell - s.z;
                    for (int di = -r; di <= r; di++)
                    {
                        int i = ci + di;
                        if (i < 0 || i >= CNX) continue;
                        float px = CX0 + i * CCell - s.x;
                        float d2 = px * px + pz * pz;
                        if (d2 > R * R) continue;
                        float d = Mathf.Sqrt(d2);
                        float k = 1f - WorldNoise.Smooth(hw + 2.5f, R, d);
                        if (k <= 0f) continue;
                        int idx = j * CNX + i;
                        float w = k / ((d2 + 16f) * (d2 + 16f));
                        sumW[idx] += w;
                        sumWH[idx] += w * s.y;
                        if (k > corK[idx]) corK[idx] = k;
                    }
                }
            }
        }

        /// <summary>Road corridor at (x,z): k = 0..1 influence, kh = k * designed height.</summary>
        public static void Corridor(float x, float z, out float k, out float kh)
        {
            float fx = (x - CX0) / CCell, fz = (z - CZ0) / CCell;
            k = 0f; kh = 0f;
            if (fx < 0f || fz < 0f || fx >= CNX - 1 || fz >= CNZ - 1) return;
            int i = (int)fx, j = (int)fz;
            float u = fx - i, w = fz - j;
            int a = j * CNX + i;
            float k00 = corK[a], k10 = corK[a + 1], k01 = corK[a + CNX], k11 = corK[a + CNX + 1];
            if (k00 + k10 + k01 + k11 <= 0f) return;
            k = (k00 * (1f - u) + k10 * u) * (1f - w) + (k01 * (1f - u) + k11 * u) * w;
            kh = (corKH[a] * (1f - u) + corKH[a + 1] * u) * (1f - w) + (corKH[a + CNX] * (1f - u) + corKH[a + CNX + 1] * u) * w;
        }

        // ------------------------------------------------------------------ final shaping (after blend + ridges)
        public static float ShapeHeight(float x, float z, float h, float weight)
        {
            float wf = WorldNoise.Smooth(0.12f, 0.6f, weight);
            if (wf <= 0f) return h;
            float h0 = h;
            // the monastery stands on its own peak: nothing nearby rises above its terraces
            float mx = x - 226f, mz = z - 1414f;
            float md2 = mx * mx + mz * mz;
            if (md2 < 200f * 200f && z < 1530f)
            {
                float md = Mathf.Sqrt(md2);
                float allowed = MonasteryH + 10f - Mathf.Max(0f, md - 54f) * 0.6f;
                float k0 = WorldNoise.Smooth(200f, 130f, md) * WorldNoise.Smooth(1530f, 1470f, z);
                h = Mathf.Lerp(h, Mathf.Min(h, allowed), k0);
            }
            // roads: the terrain follows every designed road
            float k, kh;
            Corridor(x, z, out k, out kh);
            if (k > 0f) h = h * (1f - k) + kh;
            // jade lake basin with a raised rim (the water never floats)
            float lx = (x - LakeC.x) / LakeR.x, lz = (z - LakeC.y) / LakeR.y;
            float lk2 = lx * lx + lz * lz;
            if (lk2 < 2.6f)
            {
                float lk = Mathf.Sqrt(lk2);
                if (lk < 1f) h = Mathf.Min(h, LakeY - 0.7f - 5.5f * (1f - lk2));
                else h = Mathf.Max(h, Mathf.Lerp(LakeY + 0.35f, h, WorldNoise.Smooth(1f, 1.6f, lk)));
                if (lk < 1.12f && lk >= 1f) h = Mathf.Min(h, LakeY + 0.5f);
            }
            // gorge: stream bed with steep granite walls
            float sh, along;
            float sd = StreamDistance(x, z, out sh, out along);
            if (sd < 70f)
            {
                float fw = Mathf.Lerp(3.5f, 7f, along);
                float slope = Mathf.Lerp(1.25f, 1.05f, along);
                float allowed = sh + Mathf.Max(0f, sd - fw) * slope;
                h = SMin(h, allowed, 3f);
                if (sd < fw) h = Mathf.Lerp(sh - 0.35f, h, WorldNoise.Smooth(fw * 0.4f, fw, sd));
            }
            // plunge pool where the gorge meets the valley river
            float pd = Vector2.Distance(new Vector2(x, z), PoolC);
            if (pd < 26f) h = Mathf.Lerp(-2.8f, h, WorldNoise.Smooth(11f, 26f, pd));
            return Mathf.Lerp(h0, h, wf);
        }

        static float SMin(float a, float b, float k)
        {
            float hh = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - hh * hh * k * 0.25f;
        }
    }
}
