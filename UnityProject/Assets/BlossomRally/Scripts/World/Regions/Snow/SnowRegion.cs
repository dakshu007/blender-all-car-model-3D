// SnowRegion.cs - Frost Summit (雪顶), the north-east 1 km tile (x 500..1500, z 500..1500): a high alpine winter
// plateau. Pure half (terrain, ground colour, surfaces, roads, pads, stages); the content lives in *.Build.cs.
//
//   * the asphalt connector from the Cloud Peak Mountains arrives at GateSnowWest (690, 1140, 124 m) and carries on
//     as the snow-dusted Frost Pass road to the lodge junction,
//   * the packed-snow Frost Run sweeps south past the frozen lake, along the cliff rim high above Jade Harbour City
//     (viewpoints), north along the wind-turbine ridge and up four switchbacks to the summit observatory,
//   * the Glacier Link closes the loop along the lake's north shore past the ski-lodge village,
//   * an ice circuit runs on the frozen lake (two snow spurs lead onto it),
//   * a cable car climbs from the village to the Ski Peak; the Frost Crown peaks rise on the north-east rim.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class SnowRegion : Region
    {
        public override RegionId Id { get { return RegionId.Snow; } }
        public override string Name { get { return "Frost Summit"; } }
        public override string Hanzi { get { return "雪顶"; } }
        public override string Tagline { get { return "Snow poles, a frozen lake and the observatory above the clouds"; } }
        public override Color Accent { get { return new Color(0.75f, 0.88f, 1.0f); } }

        // ------------------------------------------------------------------ layout (shared with the Build half)
        public const float LakeLevel = 132f;
        public static readonly Vector2 LakeC = new Vector2(1100f, 900f);
        public static readonly Vector2 LakeR = new Vector2(218f, 146f);
        /// <summary>Summit observatory (end of the Frost Run).</summary>
        public static readonly Vector2 Obs = new Vector2(1246f, 1392f);
        /// <summary>Ski Peak (cable car top station) and the village base station.</summary>
        public static readonly Vector2 SkiPeak = new Vector2(1000f, 1428f);
        public const float SkiPeakH = 232f;
        public static readonly Vector2 CableBottom = new Vector2(998f, 1236f);
        public static readonly Vector2 CableTop = new Vector2(1000f, 1410f);
        /// <summary>Ski-lodge village (centre of the ice-sculpture plaza).</summary>
        public static readonly Vector2 Plaza = new Vector2(948f, 1150f);
        /// <summary>Summit face profile along z (knots: z, height); steep bands = switchback legs, gentle = hairpins.</summary>
        static readonly float[] FaceZ = { 1060f, 1150f, 1162f, 1219f, 1265f, 1316f, 1360f, 1394f, 1440f };
        static readonly float[] FaceY = { 132f, 152f, 154.5f, 173.3f, 179.0f, 195.8f, 201.2f, 212.4f, 217f };

        /// <summary>Height of the summit face at z (monotone cubic through the knots).</summary>
        public static float FaceHeight(float z)
        {
            int n = FaceZ.Length;
            if (z <= FaceZ[0]) return FaceY[0] + (z - FaceZ[0]) * 0.25f;
            if (z >= FaceZ[n - 1]) return FaceY[n - 1] + (z - FaceZ[n - 1]) * 0.1f;
            int i = 0;
            while (z > FaceZ[i + 1]) i++;
            float h0 = FaceZ[i + 1] - FaceZ[i];
            float t = (z - FaceZ[i]) / h0;
            float m0 = Slope(i), m1 = Slope(i + 1);
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * FaceY[i] + (t3 - 2f * t2 + t) * h0 * m0 + (-2f * t3 + 3f * t2) * FaceY[i + 1] + (t3 - t2) * h0 * m1;
        }

        static float Slope(int i)
        {
            int n = FaceZ.Length;
            if (i == 0) return (FaceY[1] - FaceY[0]) / (FaceZ[1] - FaceZ[0]);
            if (i == n - 1) return (FaceY[n - 1] - FaceY[n - 2]) / (FaceZ[n - 1] - FaceZ[n - 2]);
            float a = (FaceY[i] - FaceY[i - 1]) / (FaceZ[i] - FaceZ[i - 1]), b = (FaceY[i + 1] - FaceY[i]) / (FaceZ[i + 1] - FaceZ[i]);
            return a * b <= 0f ? 0f : 2f * a * b / (a + b);
        }

        public static readonly Vector2[] FrostPassPts =
        {
            WorldLayout.GateSnowWest, new Vector2(745f, 1139f), new Vector2(800f, 1131f), new Vector2(850f, 1114f), new Vector2(888f, 1092f)
        };

        public static readonly Vector2[] FrostRunPts =
        {
            new Vector2(888f, 1092f), new Vector2(852f, 1056f), new Vector2(800f, 1022f), new Vector2(758f, 974f), new Vector2(738f, 912f),
            new Vector2(736f, 846f), new Vector2(752f, 782f), new Vector2(790f, 728f), new Vector2(846f, 690f), new Vector2(910f, 655f),
            new Vector2(975f, 614f), new Vector2(1045f, 574f), new Vector2(1118f, 553f), new Vector2(1190f, 548f), new Vector2(1262f, 553f),
            new Vector2(1322f, 576f), new Vector2(1368f, 622f), new Vector2(1398f, 690f), new Vector2(1412f, 776f), new Vector2(1408f, 860f),
            new Vector2(1392f, 930f), new Vector2(1384f, 1000f), new Vector2(1390f, 1070f), new Vector2(1394f, 1120f), new Vector2(1384f, 1160f),
            // switchbacks up the summit face
            new Vector2(1300f, 1185f), new Vector2(1214f, 1210f), new Vector2(1180f, 1226f), new Vector2(1176f, 1252f), new Vector2(1200f, 1266f),
            new Vector2(1300f, 1290f), new Vector2(1372f, 1310f), new Vector2(1400f, 1326f), new Vector2(1398f, 1350f), new Vector2(1372f, 1362f),
            new Vector2(1310f, 1378f), Obs
        };

        public static readonly Vector2[] GlacierLinkPts =
        {
            new Vector2(1385f, 1036f), new Vector2(1330f, 1060f), new Vector2(1260f, 1076f), new Vector2(1180f, 1082f), new Vector2(1100f, 1080f),
            new Vector2(1030f, 1078f), new Vector2(960f, 1074f), new Vector2(908f, 1062f), new Vector2(872f, 1058f)
        };

        public static readonly Vector2[] LodgeLanePts =
        {
            new Vector2(1012f, 1077f), new Vector2(1006f, 1120f), new Vector2(994f, 1162f), new Vector2(996f, 1200f), new Vector2(998f, 1222f)
        };

        public static readonly Vector2[] IceCircuitPts =
        {
            new Vector2(944f, 900f), new Vector2(958f, 952f), new Vector2(1008f, 988f), new Vector2(1076f, 996f), new Vector2(1134f, 978f),
            new Vector2(1190f, 992f), new Vector2(1244f, 976f), new Vector2(1272f, 926f), new Vector2(1262f, 862f), new Vector2(1214f, 822f),
            new Vector2(1146f, 812f), new Vector2(1086f, 836f), new Vector2(1024f, 818f), new Vector2(968f, 842f)
        };

        public static readonly Vector2[] LakeWestPts = { new Vector2(737f, 890f), new Vector2(800f, 896f), new Vector2(870f, 900f), new Vector2(944f, 900f) };
        public static readonly Vector2[] LakeNorthPts = { new Vector2(1104f, 1080f), new Vector2(1100f, 1040f), new Vector2(1076f, 996f) };

        /// <summary>Cliff-rim viewpoints over the city: x, z, half width (along the rim), depth (towards the brink).</summary>
        public static readonly Vector4[] Viewpoints =
        {
            new Vector4(1110f, 528f, 20f, 19f), new Vector4(1242f, 529f, 24f, 20f), new Vector4(1338f, 566f, 13f, 12f)
        };

        /// <summary>Wind turbines on the east ridge and the south-east rim.</summary>
        public static readonly Vector2[] Turbines =
        {
            new Vector2(1452f, 690f), new Vector2(1456f, 790f), new Vector2(1450f, 890f), new Vector2(1446f, 990f), new Vector2(1440f, 580f), new Vector2(1352f, 548f)
        };

        // ------------------------------------------------------------------ terrain
        static float S(float a, float b, float x) { return WorldNoise.Smooth(a, b, x); }

        /// <summary>Elliptic lake metric: 1 on the (warped) shoreline.</summary>
        public static float LakeE(float x, float z)
        {
            float dx = (x - LakeC.x) / LakeR.x, dz = (z - LakeC.y) / LakeR.y;
            float a = Mathf.Atan2(dz, dx);
            float wob = 1f + 0.06f * Mathf.Sin(a * 3f + 0.7f) + 0.04f * Mathf.Sin(a * 5f + 2.1f);
            return Mathf.Sqrt(dx * dx + dz * dz) / wob;
        }

        static float SMax(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.LerpUnclamped(a, b, h) + k * h * (1f - h);
        }

        public override float Height(float x, float z)
        {
            float n = WorldNoise.Fbm(x, z, 300f, 3, 5301);                               // 3
            float n2 = WorldNoise.Fbm(x, z, 125f, 2, 5307);                              // 2
            float h = 141f + 8f * n + 4.5f * n2;
            h += 15f * S(860f, 610f, z) * (1f - 0.5f * S(1250f, 1400f, x));                // the cliff rim rises
            h += 17f * S(1200f, 1480f, x) * S(560f, 700f, z) * (1f - 0.35f * S(950f, 1100f, z));   // east turbine ridge

            // frozen lake (flat at its level, gentle shore)
            float e = LakeE(x, z);
            float band = 2.25f - 0.75f * S(1150f, 1320f, x);
            if (e < band) h = Mathf.Lerp(LakeLevel, h, S(1.0f, band, e));

            // summit face: rises north in steps (steep where the switchback legs climb, gentle at the hairpins)
            float face = FaceHeight(z) - 0.62f * Mathf.Max(0f, 1150f - x);
            if (face > h - 20f) h = SMax(h, face, 10f);

            // Ski Peak (concave cone, cable car above it)
            float dsx = x - SkiPeak.x, dsz = z - SkiPeak.y;
            float ds = Mathf.Sqrt(dsx * dsx + dsz * dsz);
            if (ds < 420f)
            {
                float a = Mathf.Atan2(dsz, dsx);
                float r = ds / (92f * (1f + 0.22f * Mathf.Sin(a * 3f + 0.8f) + 0.12f * Mathf.Sin(a * 5f + 2.6f)));
                float cone = 128f + (SkiPeakH - 128f) * Mathf.Exp(-Mathf.Pow(r, 1.55f));
                cone += 6f * WorldNoise.Ridged(x, z, 70f, 2, 5311) * S(30f, 90f, ds) * S(260f, 120f, ds);   // 2
                h = SMax(h, cone, 8f);
            }

            // Frost Crown: jagged peaks on the north-east rim (seen from the city and the valley)
            float pk = 0f;
            pk += 135f * Bell(x, z, 1545f, 1505f, 115f);
            pk += 95f * Bell(x, z, 1595f, 1300f, 95f);
            pk += 80f * Bell(x, z, 1170f, 1575f, 110f);
            pk += 60f * Bell(x, z, 780f, 1530f, 120f);
            pk += 55f * Bell(x, z, 1540f, 540f, 110f);
            if (pk > 0.5f)
            {
                float rr = WorldNoise.Ridged(x, z, 85f, 3, 5323);                         // 3
                h += pk * (0.7f + 0.55f * rr);
            }

            // gateway basin: the connector arrives at 124 m
            float gx = x - WorldLayout.GateSnowWest.x, gz = z - WorldLayout.GateSnowWest.y;
            float dg = Mathf.Sqrt(gx * gx + gz * gz);
            if (dg < 320f) h = Mathf.Lerp(h, WorldLayout.HSnowWest + 1.2f * n, S(320f, 135f, dg));
            return h;
        }

        static float Bell(float x, float z, float cx, float cz, float r)
        {
            float dx = x - cx, dz = z - cz;
            float d2 = (dx * dx + dz * dz) / (r * r);
            return d2 > 6f ? 0f : Mathf.Exp(-d2);
        }

        /// <summary>
        /// The framework's City|Snow ridge would leave a 60-80 m lip on the plateau. North of the brink (z ~ 500..540,
        /// wavy) the snowfield is pulled back to the plateau height, so it runs flat to the cliff edge and then drops
        /// to the city: the escarpment the rim viewpoints look out from. The west ridge and the east rim are kept.
        /// </summary>
        public override float Shape(float x, float z, float h, float weight)
        {
            if (z < 465f || z > 705f || x < 560f || x > 1462f) return h;
            float b = 7f * Mathf.Sin(x * 0.013f + 0.4f) + 4f * Mathf.Sin(x * 0.037f + 1.3f);
            float k = S(470f + b, 504f + b, z) * S(560f, 640f, x) * S(1462f, 1418f, x);
            if (k <= 0f) return h;
            return Mathf.Lerp(h, Height(x, z), k);
        }

        // ------------------------------------------------------------------ ground colour
        static readonly Color SnowA = new Color(0.90f, 0.93f, 0.98f);
        static readonly Color SnowB = new Color(0.95f, 0.96f, 0.99f);
        static readonly Color SnowShade = new Color(0.72f, 0.81f, 0.95f);
        static readonly Color Rock = new Color(0.42f, 0.43f, 0.48f);
        static readonly Color RockWarm = new Color(0.55f, 0.50f, 0.46f);
        static readonly Color Ice = new Color(0.58f, 0.78f, 0.92f);
        static readonly Color IceDeep = new Color(0.40f, 0.62f, 0.82f);

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            float n1 = WorldNoise.Fbm(x, z, 70f, 2, 5401);
            // wind-carved sastrugi: thin ripples across the prevailing wind (from the west-south-west)
            float across = -x * 0.33f + z * 0.94f;
            float rip = Mathf.Sin(across * 0.42f + n1 * 7f + Mathf.Sin(x * 0.021f) * 2f);
            rip = rip * rip * rip;
            Color c = Color.Lerp(SnowA, SnowB, 0.5f + 0.5f * n1);
            c = Color.Lerp(c, new Color(0.82f, 0.87f, 0.96f), Mathf.Max(0f, rip) * 0.35f);
            c = Color.Lerp(c, new Color(0.98f, 0.98f, 1f), Mathf.Max(0f, -rip) * 0.25f);

            // blue shadows on slopes facing away from the sun (north-facing)
            float slope = 1f - normal.y;
            float north = Mathf.Clamp01(normal.z * 3f) * Mathf.Clamp01(slope * 7f);
            c = Color.Lerp(c, SnowShade, north * 0.55f);
            // warm sunlit south-facing slopes
            float south = Mathf.Clamp01(-normal.z * 3f) * Mathf.Clamp01(slope * 6f);
            c = Color.Lerp(c, new Color(0.98f, 0.95f, 0.90f), south * 0.3f);

            // exposed rock on steep ground, snow clinging to the ledges
            float steep = S(0.86f, 0.66f, normal.y);
            if (steep > 0f)
            {
                float strata = 0.5f + 0.5f * Mathf.Sin(h * 0.9f + n1 * 4f);
                Color rk = Color.Lerp(Rock, RockWarm, Mathf.Clamp01(0.5f + n1 + (-normal.z) * 0.6f));
                rk = Color.Lerp(rk, rk * 0.75f, strata * 0.5f);
                float ledge = Mathf.Clamp01(strata * 1.6f - 0.6f) * S(0.62f, 0.8f, normal.y);
                c = Color.Lerp(c, rk, steep * (0.85f - 0.6f * ledge));
            }

            // frozen lake: clear blue ice, white frost and pressure ridges near the shore
            float e = LakeE(x, z);
            if (e < 1.12f)
            {
                float crack = Mathf.Abs(WorldNoise.Fbm(x, z, 46f, 2, 5411));
                Color ic = Color.Lerp(IceDeep, Ice, S(0.2f, 0.95f, e) * 0.8f + 0.2f * (0.5f + 0.5f * n1));
                ic = Color.Lerp(ic, new Color(0.88f, 0.94f, 1f), S(0.025f, 0f, crack) * 0.8f);
                ic = Color.Lerp(ic, new Color(0.93f, 0.96f, 1f), S(0.86f, 1.02f, e));
                c = Color.Lerp(ic, c, S(1.0f, 1.12f, e));
            }

            // crevasse field on the glacier below the summit face
            float gl = S(70f, 0f, Mathf.Abs(x - 1180f) - 90f) * S(60f, 0f, Mathf.Abs(z - 1180f) - 80f);
            if (gl > 0f)
            {
                float cr = Mathf.Abs(WorldNoise.Fbm(x * 1.8f, z * 0.6f, 60f, 2, 5421));
                c = Color.Lerp(c, new Color(0.30f, 0.52f, 0.78f), S(0.03f, 0.005f, cr) * gl * 0.9f);
                c = Color.Lerp(c, new Color(0.74f, 0.86f, 0.98f), S(0.08f, 0.03f, cr) * gl * 0.4f);
            }

            // trampled village snow and ski tracks
            float dvx = x - 960f, dvz = z - 1180f;
            float village = S(130f, 70f, Mathf.Sqrt(dvx * dvx * 0.7f + dvz * dvz));
            if (village > 0f) c = Color.Lerp(c, new Color(0.86f, 0.87f, 0.89f) + new Color(0.04f, 0.03f, 0.02f) * n1, village * 0.45f);
            float sx = x - SkiPeak.x, sz = z - SkiPeak.y;
            float dsk = Mathf.Sqrt(sx * sx + sz * sz);
            if (dsk < 200f)
            {
                float piste = S(30f, 12f, Mathf.Abs(x - 1000f + 18f * Mathf.Sin(z * 0.03f)) ) * S(200f, 150f, dsk) * S(0f, 30f, dsk);
                c = Color.Lerp(c, new Color(0.97f, 0.98f, 1f), piste * 0.6f);
                float lines = 0.5f + 0.5f * Mathf.Sin((x - 1000f + 18f * Mathf.Sin(z * 0.03f)) * 1.3f);
                c = Color.Lerp(c, new Color(0.86f, 0.89f, 0.95f), piste * lines * 0.2f);
            }

            // tussock and bare tundra peeking out at the warm, low gateway
            float dgx = x - WorldLayout.GateSnowWest.x, dgz = z - WorldLayout.GateSnowWest.y;
            float gate = S(260f, 120f, Mathf.Sqrt(dgx * dgx + dgz * dgz));
            if (gate > 0f)
            {
                float tus = S(0.2f, 0.55f, WorldNoise.Fbm(x, z, 24f, 2, 5431) + 0.25f * n1) * S(0.95f, 0.99f, normal.y + 0.04f);
                c = Color.Lerp(c, new Color(0.66f, 0.63f, 0.52f), gate * tus * 0.42f);
            }

            // higher = brighter, crisper
            c = Color.Lerp(c, new Color(0.97f, 0.98f, 1f), S(190f, 260f, h) * 0.35f * (1f - steep));
            float o = WorldLayout.OutsideDistance(new Vector2(x, z));
            if (o > 0f) c = Color.Lerp(c, new Color(0.86f, 0.90f, 0.97f), S(0f, 200f, o) * 0.4f);
            return c;
        }

        public override Color VergeColor(float x, float z)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(x * 0.37f + z * 0.29f) * Mathf.Sin(x * 0.13f - z * 0.21f);
            return Color.Lerp(new Color(0.80f, 0.83f, 0.89f), new Color(0.88f, 0.90f, 0.95f), k);
        }

        public override Surface GroundSurface(float x, float z)
        {
            return LakeE(x, z) < 1.0f ? Surface.Ice : Surface.Snow;
        }

        public override float GrassDensity(float x, float z, float edge)
        {
            // only a few dry tussocks poke out of the thin snow by the gateway
            float dgx = x - WorldLayout.GateSnowWest.x, dgz = z - WorldLayout.GateSnowWest.y;
            float d2 = dgx * dgx + dgz * dgz;
            if (d2 > 240f * 240f || edge < 1.5f) return 0f;
            float tus = 0.5f + 0.5f * Mathf.Sin(x * 0.11f + 0.4f) * Mathf.Sin(z * 0.13f + 1.1f);
            float k = Mathf.Clamp01(1f - Mathf.Sqrt(d2) / 240f);
            return tus > 0.72f ? 0.35f * k : 0f;
        }

        public override void GrassColors(float x, float z, float r01, out Color root, out Color tip)
        {
            root = new Color(0.36f, 0.32f, 0.22f);
            tip = r01 < 0.3f ? new Color(0.86f, 0.80f, 0.58f) : (r01 < 0.85f ? new Color(0.74f, 0.68f, 0.46f) : new Color(0.95f, 0.96f, 0.98f));
        }

        // ------------------------------------------------------------------ roads
        public override void AddRoads(List<RoadDef> roads)
        {
            roads.Add(new RoadDef("Road_Snow_FrostPass", Surface.Asphalt, 9f, false, FrostPassPts)
            { tint = new Color(1.22f, 1.26f, 1.34f), cutWidth = 14f });
            roads.Add(new RoadDef("Road_Snow_FrostRun", Surface.Snow, 10f, false, FrostRunPts) { maxGrade = 0.11f, cutWidth = 11f });
            roads.Add(new RoadDef("Road_Snow_GlacierLink", Surface.Snow, 9f, false, GlacierLinkPts) { cutWidth = 10f });
            roads.Add(new RoadDef("Road_Snow_LodgeLane", Surface.Snow, 8f, false, LodgeLanePts) { cutWidth = 8f });
            roads.Add(new RoadDef("Road_Snow_IceCircuit", Surface.Ice, 12f, true, IceCircuitPts)
            { fixedHeight = true, heightOffset = LakeLevel + 0.22f, cutWidth = 3f, lines = false });
            roads.Add(new RoadDef("Road_Snow_LakeWest", Surface.Snow, 9f, false, LakeWestPts) { cutWidth = 8f });
            roads.Add(new RoadDef("Road_Snow_LakeNorth", Surface.Snow, 9f, false, LakeNorthPts) { cutWidth = 8f });
        }

        public override void Plan()
        {
            var f = Field;
            if (f == null) return;
            // ice-sculpture plaza and the lodge terraces
            f.AddFlatRect(Plaza, new Vector2(26f, 18f), -12f, BaseHeight(Plaza), 14f);
            // cable car base station + top station
            f.AddPad(CableBottom + new Vector2(0f, 12f), 14f, RoadH(LodgeLanePts[LodgeLanePts.Length - 1], 135f));
            f.AddPad(CableTop, 13f, BaseHeight(CableTop) - 1.5f);
            // summit observatory knoll
            f.AddPad(Obs + new Vector2(-8f, 18f), 26f, RoadH(Obs, 238f));
            // cliff-rim viewpoints
            foreach (var v in Viewpoints)
            {
                Vector2 c = new Vector2(v.x, v.y);
                f.AddFlatRect(c, new Vector2(v.z, v.w), 0f, RoadH(c, 156f), 10f);
            }
            // turbine footings
            foreach (var t in Turbines) f.AddPad(t, 7f, BaseHeight(t));
        }

        float BaseHeight(Vector2 p) { return Field.BaseHeight(p.x, p.y); }

        float RoadH(Vector2 p, float fallback)
        {
            RoadSample rs;
            float d = Field.NearestRoad(p, out rs, 120f);
            return d < 120f ? rs.height : fallback;
        }

        // ------------------------------------------------------------------ gameplay
        public override Vector3 SpawnPoint
        {
            get
            {
                Vector2 p = new Vector2(748f, 1139f);
                float y = WorldLayout.HSnowWest;
                if (Field != null && Field.RoadsReady)
                {
                    RoadSample rs;
                    if (Field.NearestRoad(p, out rs, 30f) < 30f) { p = rs.pos; y = rs.height; }
                }
                return new Vector3(p.x, y, p.y);
            }
        }
        public override float SpawnYaw { get { return 95f; } }

        public override void AddStages(List<StageDef> stages)
        {
            stages.Add(new StageDef
            {
                key = "FrostSummitRun", name = "Frost Summit Run", roadName = "Road_Snow_FrostRun", region = RegionId.Snow,
                blurb = "Time attack - packed snow past the frozen lake, along the cliff rim and up the switchbacks to the observatory",
                kind = StageKind.TimeAttack, startOffset = 30f, endOffset = 24f, goldSpeed = 17.5f
            });
            stages.Add(new StageDef
            {
                key = "FrozenLakeDrift", name = "Frozen Lake Ice Drift", roadName = "Road_Snow_IceCircuit", region = RegionId.Snow,
                blurb = "Drift challenge - slide the glassy ice circuit on the frozen lake before time runs out",
                kind = StageKind.Drift, startOffset = 20f, limitSpeed = 10.5f, bronzePts = 3000, silverPts = 7000, goldPts = 12000
            });
        }
    }
}
