// MountainsRegion.cs - Cloud Peak Mountains (云峰): Huangshan / Zhangjiajie inspired granite mountains north of
// the valley. Pure half: terrain, ground colours, surfaces, grass, roads, pads, stages. Layout data lives in
// MountainsMap.cs; everything the region builds lives in the *.Build.cs files.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed partial class MountainsRegion : Region
    {
        public override RegionId Id { get { return RegionId.Mountains; } }
        public override string Name { get { return "Cloud Peak Mountains"; } }
        public override string Hanzi { get { return "云峰"; } }
        public override string Tagline { get { return "Granite spires above a sea of cloud, cliff roads and the golden monastery"; } }
        public override Color Accent { get { return new Color(0.42f, 0.76f, 0.62f); } }

        // ------------------------------------------------------------------ terrain
        public override float Height(float x, float z) { return MountainsMap.Natural(x, z); }

        public override float Shape(float x, float z, float h, float weight) { return MountainsMap.ShapeHeight(x, z, h, weight); }

        // ------------------------------------------------------------------ slope / height cache (ground surface, grass)
        // BaseHeight on a 4 m grid over the tile + blend band, built once on first use (main thread or harness).
        const float SX0 = -616f, SZ0 = 384f, SCell = 4f;
        const int SNX = 309, SNZ = 309;
        float[] baseGrid;
        readonly object gridLock = new object();

        float[] BaseGrid()
        {
            if (baseGrid != null) return baseGrid;
            lock (gridLock)
            {
                if (baseGrid != null || Field == null) return baseGrid;
                var g = new float[SNX * SNZ];
                for (int j = 0; j < SNZ; j++)
                    for (int i = 0; i < SNX; i++)
                        g[j * SNX + i] = Field.BaseHeight(SX0 + i * SCell, SZ0 + j * SCell);
                baseGrid = g;
                return g;
            }
        }

        /// <summary>Approximate terrain slope (rise per metre, 0 = flat) and height from the cached grid.</summary>
        public float SlopeAt(float x, float z, out float h)
        {
            var g = BaseGrid();
            h = 0f;
            if (g == null) return 0f;
            float fx = (x - SX0) / SCell, fz = (z - SZ0) / SCell;
            int i = Mathf.Clamp((int)fx, 1, SNX - 3), j = Mathf.Clamp((int)fz, 1, SNZ - 3);
            float u = Mathf.Clamp01(fx - i), w = Mathf.Clamp01(fz - j);
            int a = j * SNX + i;
            h = (g[a] * (1f - u) + g[a + 1] * u) * (1f - w) + (g[a + SNX] * (1f - u) + g[a + SNX + 1] * u) * w;
            float gx = (g[a + 1] - g[a - 1] + g[a + SNX + 1] - g[a + SNX - 1]) * 0.25f / SCell;
            float gz = (g[a + SNX] - g[a - SNX] + g[a + SNX + 1] - g[a - SNX + 1]) * 0.25f / SCell;
            return Mathf.Sqrt(gx * gx + gz * gz);
        }

        // ------------------------------------------------------------------ ground colour
        static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }

        static readonly Color MeadowA = new Color(0.42f, 0.65f, 0.37f), MeadowB = new Color(0.52f, 0.73f, 0.44f), MeadowC = new Color(0.36f, 0.58f, 0.40f);
        static readonly Color ForestA = new Color(0.27f, 0.40f, 0.24f), ForestB = new Color(0.40f, 0.36f, 0.24f);
        static readonly Color GraniteA = new Color(0.66f, 0.65f, 0.60f), GraniteB = new Color(0.56f, 0.56f, 0.54f), GraniteWarm = new Color(0.74f, 0.69f, 0.60f);
        static readonly Color GraniteDark = new Color(0.40f, 0.42f, 0.41f), Moss = new Color(0.36f, 0.50f, 0.32f);
        static readonly Color Pebble = new Color(0.68f, 0.66f, 0.60f), WetBank = new Color(0.34f, 0.40f, 0.34f), Jade = new Color(0.18f, 0.44f, 0.40f);
        static readonly Color LeafRed = new Color(0.78f, 0.30f, 0.16f), LeafGold = new Color(0.86f, 0.62f, 0.20f);

        public override Color GroundColor(float x, float z, float h, Vector3 normal)
        {
            float n1 = WorldNoise.Fbm(x, z, 46f, 2, 4101);
            float n2 = WorldNoise.Value(x * 0.21f, z * 0.21f, 4107);
            float forest = MountainsMap.Forest(x, z);
            float rug = MountainsMap.Rugged(x, z);
            // meadow greens with soft patches
            Color c = Mix(MeadowA, MeadowB, 0.5f + 0.9f * n1);
            c = Mix(c, MeadowC, (n2 - 0.3f) * 1.6f);
            // pine forest floor: needles and shade
            float fk = WorldNoise.Smooth(0.35f, 0.8f, forest + 0.25f * n1);
            c = Mix(c, Mix(ForestA, ForestB, 0.45f + 0.8f * n2), fk * 0.85f);
            // alpine wildflower meadows (open, gentle, not forest)
            if (fk < 0.4f && h > 55f)
            {
                float fl = WorldNoise.Value(x * 0.9f, z * 0.9f, 4111);
                if (fl > 0.72f)
                {
                    int pick = (int)(WorldNoise.Hash01(Mathf.FloorToInt(x * 0.5f), Mathf.FloorToInt(z * 0.5f), 4113) * 4f);
                    Color fc = pick == 0 ? new Color(0.96f, 0.95f, 0.88f) : pick == 1 ? new Color(0.98f, 0.84f, 0.32f)
                             : pick == 2 ? new Color(0.84f, 0.62f, 0.86f) : new Color(0.96f, 0.68f, 0.74f);
                    c = Mix(c, fc, (fl - 0.72f) * 2.2f * (1f - fk));
                }
            }
            // maple groves: fallen red and gold leaves
            float maple = MapleK(x, z);
            if (maple > 0f)
            {
                float sp = WorldNoise.Value(x * 1.3f, z * 1.3f, 4121);
                c = Mix(c, sp > 0.1f ? LeafRed : LeafGold, maple * (0.45f + 0.35f * sp));
            }
            // granite on steep slopes and high crags
            float steep = WorldNoise.Smooth(0.86f, 0.62f, normal.y);
            float high = WorldNoise.Smooth(150f, 200f, h) * 0.5f;
            float rock = Mathf.Clamp01(steep + high + (rug - 0.6f) * 0.5f * WorldNoise.Smooth(0.95f, 0.8f, normal.y));
            if (rock > 0f)
            {
                float streak = 0.5f + 0.5f * Mathf.Sin(x * 0.31f + z * 0.07f + n1 * 6f);
                Color g = Mix(GraniteA, GraniteB, 0.5f + n2);
                g = Mix(g, GraniteWarm, n1 * 1.4f);
                g = Mix(g, GraniteDark, (streak - 0.72f) * 2.2f * steep);
                // moss and pine pads cling to ledges
                g = Mix(g, Moss, WorldNoise.Smooth(0.78f, 0.9f, normal.y) * 0.5f + (n2 > 0.55f ? 0.25f : 0f));
                c = Mix(c, g, rock);
            }
            var f = Field;
            // gorge stream: wet banks and pebbles
            float sh, along;
            float sd = MountainsMap.StreamDistance(x, z, out sh, out along);
            if (sd < 16f)
            {
                c = Mix(c, Mix(Pebble, WetBank, 0.4f + n2), (1f - WorldNoise.Smooth(4f, 16f, sd)) * 0.85f);
                if (h < sh + 0.4f) c = Mix(c, WetBank, 0.6f);
            }
            // jade lake bed and sandy shore
            float lx = (x - MountainsMap.LakeC.x) / MountainsMap.LakeR.x, lz = (z - MountainsMap.LakeC.y) / MountainsMap.LakeR.y;
            float lk = Mathf.Sqrt(lx * lx + lz * lz);
            if (lk < 1.35f)
            {
                c = Mix(c, Pebble, (1f - WorldNoise.Smooth(1.05f, 1.35f, lk)) * 0.8f);
                if (lk < 1f) c = Mix(c, Jade, WorldNoise.Smooth(1f, 0.6f, lk));
            }
            float pd = Vector2.Distance(new Vector2(x, z), MountainsMap.PoolC);
            if (pd < 28f) c = Mix(c, Mix(Pebble, WetBank, 0.5f + n2), 1f - WorldNoise.Smooth(14f, 28f, pd));
            // monastery summit: worn earth around the plaza
            float md = Vector2.Distance(new Vector2(x, z), MountainsMap.Monastery);
            if (md < 70f) c = Mix(c, new Color(0.72f, 0.66f, 0.54f), (1f - WorldNoise.Smooth(40f, 70f, md)) * 0.5f);
            // footpaths to the pavilions and the monastery steps
            float pathD = PathDistance(x, z);
            if (pathD < 2.6f) c = Mix(c, new Color(0.66f, 0.58f, 0.44f), 1f - WorldNoise.Smooth(1.2f, 2.6f, pathD));
            // misty distance outside the world edge
            float o = WorldLayout.OutsideDistance(new Vector2(x, z));
            if (o > 0f) c = Mix(c, new Color(0.62f, 0.70f, 0.70f), WorldNoise.Smooth(0f, 180f, o) * 0.45f);
            c.a = 0f;
            return c;
        }

        public override Color VergeColor(float x, float z)
        {
            float forest = MountainsMap.Forest(x, z);
            Color v = Color.Lerp(new Color(0.66f, 0.63f, 0.54f), new Color(0.56f, 0.50f, 0.38f), forest);
            float maple = MapleK(x, z);
            if (maple > 0f) v = Color.Lerp(v, new Color(0.80f, 0.42f, 0.20f), maple * 0.6f);
            return v;
        }

        // ------------------------------------------------------------------ maple groves & footpaths (shared with Build)
        /// <summary>Maple groves (x, z, radius): red / gold autumn accents among the pines.</summary>
        public static readonly Vector3[] MapleGroves =
        {
            new Vector3(-150f, 905f, 30f), new Vector3(-235f, 1010f, 26f), new Vector3(-60f, 1160f, 28f), new Vector3(20f, 1290f, 26f),
            new Vector3(-300f, 760f, 30f), new Vector3(-200f, 690f, 26f), new Vector3(230f, 1140f, 26f), new Vector3(110f, 1330f, 24f),
            new Vector3(-130f, 790f, 22f), new Vector3(-330f, 905f, 22f), new Vector3(70f, 1010f, 20f), new Vector3(-150f, 1240f, 24f)
        };

        public static float MapleK(float x, float z)
        {
            float k = 0f;
            for (int i = 0; i < MapleGroves.Length; i++)
            {
                Vector3 g = MapleGroves[i];
                float dx = x - g.x, dz = z - g.y;
                float d2 = dx * dx + dz * dz;
                if (d2 >= g.z * g.z) continue;
                k = Mathf.Max(k, 1f - Mathf.Sqrt(d2) / g.z);
            }
            return k;
        }

        /// <summary>Foot paths (polylines): to the lookout pavilions, the waterfall viewpoint and up the monastery steps.</summary>
        public static readonly Vector2[][] Paths =
        {
            new[] { new Vector2(-84f, 864f), new Vector2(-66f, 872f), new Vector2(-48f, 868f) },                          // gorge lookout
            new[] { new Vector2(-104f, 650f), new Vector2(-80f, 640f), new Vector2(-58f, 628f), new Vector2(-46f, 612f) }, // falls viewpoint
            new[] { new Vector2(60f, 1228f), new Vector2(84f, 1234f), new Vector2(104f, 1226f) },                          // hairpin C lookout
            new[] { new Vector2(150f, 1060f), new Vector2(150f, 1074f), new Vector2(140f, 1080f) },                        // lake shore steps
        };

        public static float PathDistance(float x, float z)
        {
            float best = 99f;
            Vector2 p = new Vector2(x, z);
            foreach (var path in Paths)
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a = path[i], b = path[i + 1];
                    if (x < Mathf.Min(a.x, b.x) - 4f || x > Mathf.Max(a.x, b.x) + 4f || z < Mathf.Min(a.y, b.y) - 4f || z > Mathf.Max(a.y, b.y) + 4f) continue;
                    float t;
                    float d = Spline.DistToSegment(p, a, b, out t);
                    if (d < best) best = d;
                }
            return best;
        }

        // ------------------------------------------------------------------ surfaces & grass
        public override Surface GroundSurface(float x, float z)
        {
            if (PathDistance(x, z) < 1.8f) return Surface.Dirt;
            float h;
            float s = SlopeAt(x, z, out h);
            if (s > 0.85f) return Surface.Gravel;
            float sh, along;
            if (MountainsMap.StreamDistance(x, z, out sh, out along) < 9f) return Surface.Gravel;
            if (MountainsMap.Forest(x, z) > 0.62f) return Surface.Dirt;
            return Surface.Grass;
        }

        public override float GrassDensity(float x, float z, float edge)
        {
            if (WorldLayout.OutsideDistance(new Vector2(x, z)) > 0f) return 0f;
            float h;
            float s = SlopeAt(x, z, out h);
            if (s > 0.9f) return 0f;
            float flat = WorldNoise.Smooth(0.9f, 0.35f, s);
            float forest = MountainsMap.Forest(x, z);
            float d = 0f;
            if (edge < 7f) d = 0.75f * WorldNoise.Smooth(0.3f, 1.4f, edge);                     // verges
            float meadow = (1f - WorldNoise.Smooth(0.3f, 0.6f, forest));
            float patch = 0.5f + 0.5f * WorldNoise.Value(x * 0.045f, z * 0.045f, 4201);
            d = Mathf.Max(d, meadow * WorldNoise.Smooth(0.35f, 0.75f, patch) * 0.5f);            // alpine meadows
            d = Mathf.Max(d, MapleK(x, z) * 0.3f);
            float sh, along;
            if (MountainsMap.StreamDistance(x, z, out sh, out along) < 8f) return 0f;
            float lx = (x - MountainsMap.LakeC.x) / MountainsMap.LakeR.x, lz = (z - MountainsMap.LakeC.y) / MountainsMap.LakeR.y;
            if (lx * lx + lz * lz < 1.25f) return 0f;
            return d * flat;
        }

        public override void GrassColors(float x, float z, float r01, out Color root, out Color tip)
        {
            root = new Color(0.18f, 0.30f, 0.14f);
            float forest = MountainsMap.Forest(x, z);
            tip = Color.Lerp(new Color(0.56f, 0.78f, 0.36f), new Color(0.44f, 0.62f, 0.30f), forest);
            float n = WorldNoise.Value(x * 0.07f, z * 0.07f, 4301);
            tip = Color.Lerp(tip, new Color(0.70f, 0.82f, 0.44f), Mathf.Max(0f, n) * 0.5f);
            // wildflowers in the meadows, dry stems on the verges, leaves under maples
            if (r01 < 0.05f) tip = new Color(0.82f, 0.78f, 0.52f);
            else if (forest < 0.45f && r01 > 0.93f)
            {
                float k = (r01 - 0.93f) / 0.07f;
                tip = k < 0.3f ? new Color(0.98f, 0.97f, 0.92f) : k < 0.55f ? new Color(1f, 0.84f, 0.30f) : k < 0.8f ? new Color(0.78f, 0.58f, 0.92f) : new Color(1f, 0.62f, 0.72f);
            }
            float maple = MapleK(x, z);
            if (maple > 0.2f && r01 > 0.6f && r01 < 0.75f) tip = r01 < 0.68f ? new Color(0.86f, 0.34f, 0.18f) : new Color(0.92f, 0.68f, 0.24f);
        }

        // ------------------------------------------------------------------ roads
        public override void AddRoads(List<RoadDef> roads)
        {
            foreach (var p in MountainsMap.Roads)
            {
                var def = new RoadDef(p.name, p.surface, p.width, false, p.Points2D()) { maxGrade = p.maxGrade, cutWidth = 10f };
                if (p.name == MountainsMap.RoadEast)
                {
                    float s = DistanceAlong(p, MountainsMap.BridgeAt);
                    def.bridgeSpans = new List<Vector2> { new Vector2(s - 23f, s + 23f) };
                }
                if (p.surface == Surface.Gravel) def.tint = new Color(0.96f, 0.95f, 0.92f);
                roads.Add(def);
            }
        }

        /// <summary>Metres along a designed road's spline to the sample nearest p.</summary>
        public static float DistanceAlong(MountainsRoadPlan p, Vector2 at)
        {
            var s = MountainsMap.SampleRoad(p.pts, 2f);
            float acc = 0f, best = 1e9f, bestS = 0f;
            for (int i = 0; i < s.Count; i++)
            {
                if (i > 0) acc += Vector2.Distance(new Vector2(s[i].x, s[i].z), new Vector2(s[i - 1].x, s[i - 1].z));
                float d = (new Vector2(s[i].x, s[i].z) - at).sqrMagnitude;
                if (d < best) { best = d; bestS = acc; }
            }
            return bestS;
        }

        // ------------------------------------------------------------------ pads (plazas, pavilions, stations)
        public static readonly Vector4[] PavilionSpots =
        {
            new Vector4(-48f, 868f, 7f, 0f),       // gorge lookout (height from the ground)
            new Vector4(104f, 1226f, 7f, 0f),      // hairpin C, over the lake
            new Vector4(330f, 788f, 8f, 0f),       // end of the cloud-sea ridge spur
            new Vector4(-46f, 612f, 6f, 0f),       // valley falls viewpoint
        };

        public override void Plan()
        {
            var f = Field;
            if (f == null) return;
            // monastery: plaza at the road end, two terraces up behind it
            Vector2 m = MountainsMap.Monastery;
            f.AddFlatRect(m, new Vector2(30f, 18f), 12f, MountainsMap.MonasteryH, 10f);
            f.AddFlatRect(m + new Vector2(8f, 34f), new Vector2(26f, 14f), 12f, MountainsMap.MonasteryH + 4.5f, 3f);
            f.AddFlatRect(m + new Vector2(16f, 62f), new Vector2(20f, 12f), 12f, MountainsMap.MonasteryH + 9f, 6f);
            // cable car stations (lake and summit)
            foreach (var st in new[] { MountainsMap.CableLow, MountainsMap.CableHigh })
            {
                Vector2 d = (MountainsMap.CableHigh - MountainsMap.CableLow).normalized;
                f.AddFlatRect(st, new Vector2(9.5f, 7.5f), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, f.BaseHeight(st.x, st.y), 6f);
            }
            foreach (var pv in PavilionSpots)
            {
                Vector2 c = new Vector2(pv.x, pv.y);
                f.AddPad(c, pv.z, f.BaseHeight(c.x, c.y));
            }
        }

        // ------------------------------------------------------------------ gameplay
        public override Vector3 SpawnPoint
        {
            get
            {
                Vector2 p = new Vector2(-113f, 742f);
                float y = Field != null ? Field.BaseHeight(p.x, p.y) : 27f;
                return new Vector3(p.x, y, p.y);
            }
        }
        public override float SpawnYaw { get { return -4f; } }

        public override void AddStages(List<StageDef> stages)
        {
            var main = MountainsMap.Plan(MountainsMap.RoadMain);
            float colS = DistanceAlong(main, MountainsMap.Col);
            stages.Add(new StageDef
            {
                key = "CloudPeakHillclimb", name = "Cloud Peak Hillclimb", blurb = "Time attack - from the gorge to the golden monastery on the summit",
                roadName = MountainsMap.RoadMain, region = RegionId.Mountains, kind = StageKind.TimeAttack, startOffset = 30f, endOffset = 12f, goldSpeed = 15.5f
            });
            stages.Add(new StageDef
            {
                key = "MonasteryHairpins", name = "Monastery Hairpins", blurb = "Drift challenge - slide the stacked hairpins up to the monastery",
                roadName = MountainsMap.RoadMain, region = RegionId.Mountains, kind = StageKind.Drift, startOffset = colS + 20f, endOffset = 12f,
                limitSpeed = 11.5f, bronzePts = 3500, silverPts = 8000, goldPts = 14000
            });
            stages.Add(new StageDef
            {
                key = "PineForestGravel", name = "Pine Forest Gravel", blurb = "Time attack - loose gravel through the pine and maple forest",
                roadName = MountainsMap.RoadForest, region = RegionId.Mountains, kind = StageKind.TimeAttack, startOffset = 22f, endOffset = 18f, goldSpeed = 17f
            });
        }
    }
}
