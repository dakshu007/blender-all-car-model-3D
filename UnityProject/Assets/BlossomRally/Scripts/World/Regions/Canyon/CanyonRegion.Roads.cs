// CanyonRegion.Roads.cs - Red Canyon roads, stages and spawn (pure).
//   Danxia Run    (dirt)   east gateway -> slot canyon -> gorge bridge -> rainbow hills -> watchtower -> down to the wash
//   Wash Gravel   (gravel) south gateway -> dry riverbed braids -> Crescent Spring oasis -> dune sea
//   Mesa Loop     (gravel) around the Red Citadel mesa, from the main track back to the main track
//   Dune Drift    (sand)   closed loop over the dunes in the north-west corner
// Also a road-corridor distance grid: mesas, hills and dunes are eased away from every road, so the roads
// always run on canyon floors (small cut / fill) and the slot canyon is literally carved by the main track.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class CanyonRegion
    {
        public const string RoadMain = "Road_Canyon_DanxiaRun";
        public const string RoadWash = "Road_Canyon_WashGravel";
        public const string RoadLoop = "Road_Canyon_MesaLoop";
        public const string RoadDune = "Road_Canyon_DuneDrift";
        public const string RoadHighway = "Road_Canyon_SilkHighway";

        public static readonly Vector2[] MainPts =
        {
            WorldLayout.GateCanyonEast, new Vector2(-735f, 904f), new Vector2(-785f, 914f), new Vector2(-835f, 931f), new Vector2(-879f, 954f),
            new Vector2(-910f, 990f), new Vector2(-926f, 1040f), new Vector2(-930f, 1095f), new Vector2(-922f, 1150f), new Vector2(-896f, 1198f),
            new Vector2(-852f, 1232f), new Vector2(-795f, 1248f), new Vector2(-742f, 1268f), new Vector2(-708f, 1312f), new Vector2(-716f, 1366f),
            new Vector2(-768f, 1398f), new Vector2(-840f, 1408f), new Vector2(-912f, 1394f), new Vector2(-975f, 1365f), new Vector2(-1030f, 1338f),
            new Vector2(-1080f, 1305f), new Vector2(-1108f, 1258f), new Vector2(-1112f, 1196f), new Vector2(-1085f, 1135f), new Vector2(-1110f, 1070f),
            new Vector2(-1078f, 1010f), new Vector2(-1052f, 958f), new Vector2(-1078f, 900f), new Vector2(-1098f, 842f), new Vector2(-1078f, 790f),
            new Vector2(-1066f, 742f)
        };

        public static readonly Vector2[] WashPts =
        {
            WorldLayout.GateCanyonSouth, new Vector2(-1042f, 724f), new Vector2(-1092f, 758f), new Vector2(-1152f, 790f), new Vector2(-1198f, 836f),
            new Vector2(-1222f, 900f), new Vector2(-1216f, 968f), new Vector2(-1198f, 1032f), new Vector2(-1206f, 1100f), new Vector2(-1238f, 1160f),
            new Vector2(-1272f, 1200f), new Vector2(-1300f, 1231f)
        };

        public static readonly Vector2[] LoopPts =
        {
            new Vector2(-925f, 1031f), new Vector2(-965f, 998f), new Vector2(-990f, 940f), new Vector2(-985f, 870f), new Vector2(-955f, 810f),
            new Vector2(-915f, 748f), new Vector2(-860f, 716f), new Vector2(-790f, 714f), new Vector2(-736f, 742f), new Vector2(-712f, 800f),
            new Vector2(-722f, 858f), new Vector2(-750f, 907f)
        };

        public static readonly Vector2[] DunePts =
        {
            new Vector2(-1300f, 1236f), new Vector2(-1248f, 1262f), new Vector2(-1228f, 1318f), new Vector2(-1250f, 1378f), new Vector2(-1306f, 1412f),
            new Vector2(-1352f, 1390f), new Vector2(-1386f, 1356f), new Vector2(-1428f, 1372f), new Vector2(-1446f, 1312f), new Vector2(-1420f, 1252f),
            new Vector2(-1360f, 1222f)
        };

        public static readonly Vector2[] HighwayPts =
        {
            new Vector2(-1154f, 793f), new Vector2(-1210f, 772f), new Vector2(-1280f, 762f), new Vector2(-1350f, 772f), new Vector2(-1402f, 814f),
            new Vector2(-1428f, 880f), new Vector2(-1432f, 960f), new Vector2(-1426f, 1040f), new Vector2(-1428f, 1120f), new Vector2(-1412f, 1186f),
            new Vector2(-1392f, 1236f)
        };

        /// <summary>Metres along the main track carried by the gorge bridge (x = from, y = to).</summary>
        public static Vector2 BridgeSpan { get; private set; }

        public override void AddRoads(List<RoadDef> roads)
        {
            // order matters for junction heights: the closed dune loop first, then the wash (ends on the loop),
            // the main track (ends on the wash), the mesa loop (both ends on the main track)
            roads.Add(new RoadDef(RoadDune, Surface.Sand, 12f, true, DunePts) { maxGrade = 0.11f, cutWidth = 10f, tint = new Color(1f, 0.93f, 0.84f) });
            roads.Add(new RoadDef(RoadWash, Surface.Gravel, 9f, false, WashPts) { maxGrade = 0.1f, cutWidth = 9f, tint = new Color(1f, 0.95f, 0.88f) });
            var main = new RoadDef(RoadMain, Surface.Dirt, 9f, false, MainPts) { maxGrade = 0.11f, cutWidth = 9f, tint = new Color(1f, 0.86f, 0.76f) };
            BridgeSpan = FindBridgeSpan(MainPts);
            main.bridgeSpans = new List<Vector2> { BridgeSpan };
            roads.Add(main);
            roads.Add(new RoadDef(RoadLoop, Surface.Gravel, 8f, false, LoopPts) { maxGrade = 0.11f, cutWidth = 9f, tint = new Color(1f, 0.92f, 0.84f) });
            roads.Add(new RoadDef(RoadHighway, Surface.Asphalt, 10f, false, HighwayPts) { maxGrade = 0.07f, cutWidth = 12f, tint = new Color(1f, 0.9f, 0.84f) });
        }

        /// <summary>
        /// Where the main track crosses the gorge. Mirrors the framework's road smoothing (3 passes, +-10 samples at
        /// 2 m) on this region's own heights and returns the stretch where the deck would float at least 0.45 m
        /// above the ground: the terrain never pokes through the deck and the approaches stay at grade.
        /// </summary>
        Vector2 FindBridgeSpan(Vector2[] pts)
        {
            var s = Spline.Sample(pts, false, 2f);
            int n = s.Count;
            var raw = new float[n];
            var hs = new float[n];
            var dist = new float[n];
            int deepest = -1; float deep = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist[i] = dist[i - 1] + Vector2.Distance(s[i], s[i - 1]);
                raw[i] = Height(s[i].x, s[i].y);
                hs[i] = raw[i];
                float g = GorgeCarve(s[i].x, s[i].y, 0f);
                if (g > deep) { deep = g; deepest = i; }
            }
            if (deepest < 0) return new Vector2(-10f, -5f);
            var tmp = new float[n];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f; int cnt = 0;
                    for (int k = -10; k <= 10; k++) { int j = i + k; if (j < 0 || j >= n) continue; sum += hs[j]; cnt++; }
                    tmp[i] = sum / cnt;
                }
                System.Array.Copy(tmp, hs, n);
            }
            int a = deepest, b = deepest;
            while (a > 0 && hs[a - 1] - raw[a - 1] > 0.45f) a--;
            while (b < n - 1 && hs[b + 1] - raw[b + 1] > 0.45f) b++;
            return new Vector2(dist[a] - 1f, dist[b] + 1f);
        }

        // ------------------------------------------------------------------ stages & spawn
        public override void AddStages(List<StageDef> stages)
        {
            stages.Add(new StageDef
            {
                key = "CanyonDanxiaDust", name = "Danxia Dust Run", roadName = RoadMain, kind = StageKind.TimeAttack,
                blurb = "Time attack - dirt from the mountain pass through the slot canyon, over the gorge and round the rainbow hills",
                startOffset = 40f, endOffset = 50f, goldSpeed = 19f
            });
            stages.Add(new StageDef
            {
                key = "CanyonDuneDrift", name = "Dune Drift", roadName = RoadDune, kind = StageKind.Drift,
                blurb = "Drift challenge - slide the sand loop over the dunes before time runs out",
                startOffset = 10f, limitSpeed = 11.5f, bronzePts = 2200, silverPts = 5200, goldPts = 9000
            });
            stages.Add(new StageDef
            {
                key = "CanyonWashGravel", name = "Canyon Wash Gravel", roadName = RoadWash, kind = StageKind.TimeAttack,
                blurb = "Time attack - gravel braids up the dry riverbed to the Crescent Spring and the dunes",
                startOffset = 30f, endOffset = 30f, goldSpeed = 19.5f
            });
        }

        public override Vector3 SpawnPoint { get { return new Vector3(-724f, 0f, 902f); } }
        public override float SpawnYaw { get { return -85f; } }

        // ------------------------------------------------------------------ road corridor grid (8 m cells)
        const float GridCell = 8f, GridX0 = -1760f, GridZ0 = 240f;
        const int GridN = 200;
        float[] corridor;

        void BuildCorridor()
        {
            corridor = new float[GridN * GridN];
            for (int i = 0; i < corridor.Length; i++) corridor[i] = 99f;
            var lines = new List<Vector2[]> { DunePts, MainPts, WashPts, LoopPts, HighwayPts };
            lines.AddRange(SideCanyons);
            lines.Add(Gorge);
            foreach (var c in WorldLayout.Connectors)
                if (c.a == RegionId.Canyon || c.b == RegionId.Canyon) lines.Add(c.points);
            for (int l = 0; l < lines.Count; l++)
            {
                var s = Spline.Sample(lines[l], l == 0, 3f);
                foreach (var p in s)
                {
                    int ci = Mathf.FloorToInt((p.x - GridX0) / GridCell), cj = Mathf.FloorToInt((p.y - GridZ0) / GridCell);
                    for (int dj = -9; dj <= 9; dj++)
                        for (int di = -9; di <= 9; di++)
                        {
                            int i = ci + di, j = cj + dj;
                            if (i < 0 || j < 0 || i >= GridN || j >= GridN) continue;
                            float cx = GridX0 + i * GridCell, cz = GridZ0 + j * GridCell;
                            float d = Mathf.Sqrt((cx - p.x) * (cx - p.x) + (cz - p.y) * (cz - p.y));
                            int k = j * GridN + i;
                            if (d < corridor[k]) corridor[k] = d;
                        }
                }
            }
        }

        /// <summary>Approximate distance (m) to the nearest Canyon road centreline (capped at ~72 m).</summary>
        public float CorridorDist(float x, float z)
        {
            if (corridor == null) BuildCorridor();
            float fx = (x - GridX0) / GridCell, fz = (z - GridZ0) / GridCell;
            int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
            if (i < 0 || j < 0 || i >= GridN - 1 || j >= GridN - 1) return 99f;
            float u = fx - i, w = fz - j;
            float a = corridor[j * GridN + i], b = corridor[j * GridN + i + 1], c = corridor[(j + 1) * GridN + i], d = corridor[(j + 1) * GridN + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), w);
        }
    }
}
