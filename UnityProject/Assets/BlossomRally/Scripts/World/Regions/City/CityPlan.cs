// CityPlan.cs - Jade Harbour City layout (pure: no engine objects, compiled by the terrain harness too).
//
//   * Levels: the upper town plateau at 18 m (west, centre and north), soft ramps (<= 9 %) down to the
//     waterfront at 2.5 m in the south / south-east, a harbour basin (inlet) and the open sea to the south.
//   * Shoreline: a signed distance (positive in the water) shared by the terrain carve and the quay walls.
//   * Streets: control points of every city road (the plateau grid is flat at exactly 18 m).
//   * Zones and lots: plazas, parks, quays and every building footprint, generated deterministically so the
//     ground colour (pure) and the buildings (Build) always agree.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal enum CityZoneKind { Plaza, Park, Lot, Quay, Yard, OldTown, Stadium, Promenade, Garden, Temple }

    internal struct CityZone
    {
        public Vector2 c, half;
        public CityZoneKind kind;
        public int lot;          // index into CityPlan.Lots for Lot zones (-1 otherwise)
    }

    /// <summary>Tower styles of the generator (CityTowers.Build.cs).</summary>
    internal enum CityStyle { Setback = 0, Round = 1, Bamboo = 2, Pagoda = 3, Midrise = 4, Shophouse = 5, Slab = 6, Twin = 7 }

    internal sealed class CityLot
    {
        public Vector2 c, half;       // footprint centre and half size (local x / z before yaw)
        public float yaw;             // degrees
        public float baseY;           // ground level of the lot (flat pad)
        public float height;          // roof height above baseY (without spire)
        public CityStyle style;
        public int palette;           // CityTowers palette index
        public int seed;
        public bool skyline;          // stamped into the always-visible skyline root
        public bool neon;             // neon signs on its lower floors
        public Vector2 face;          // direction of the street it faces (for signs / entrances)
    }

    internal static class CityPlan
    {
        // ------------------------------------------------------------------ levels
        public const float Plat = 18f;             // upper town
        public const float Low = 2.5f;             // waterfront
        public const float BasinFloor = -5f;
        public const float QuayZ = -445f;          // seafront quay line
        public const float SeaW = 690f, SeaE = 1455f;
        public const float BasinX0 = 1130f, BasinX1 = 1320f, BasinTop = -40f, BasinR = 70f;
        public static readonly Vector2 Gate = WorldLayout.GateCityWest;

        /// <summary>0..1 ramp that is linear in the middle with eased ends (max slope 1.25 x the average).</summary>
        public static float SoftRamp(float a, float b, float x)
        {
            float t = (x - a) / (b - a);
            const float e = 0.2f;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (t < e) return t * t / (2f * e * (1f - e));
            if (t > 1f - e) { float u = 1f - t; return 1f - u * u / (2f * e * (1f - e)); }
            return (t - e * 0.5f) / (1f - e);
        }

        public static float RampW(float z) { return SoftRamp(-375f, -160f, z); }
        public static float RampE(float z) { return SoftRamp(-352f, -140f, z); }

        /// <summary>Land level before the water carve: plateau 18 m, ramps, waterfront 2.5 m.</summary>
        public static float Level(float x, float z)
        {
            float r = x < 1225f ? RampW(z) : RampE(z);
            return Low + (Plat - Low) * r;
        }

        public static float SeaSD(float x, float z)
        {
            return (QuayZ - z) - Mathf.Max(0f, SeaW - x) * 0.9f - Mathf.Max(0f, x - SeaE) * 0.9f;
        }

        public static float BasinSD(float x, float z)
        {
            const float cx = (BasinX0 + BasinX1) * 0.5f, hx = (BasinX1 - BasinX0) * 0.5f;
            const float cz = (-520f + BasinTop) * 0.5f, hz = (BasinTop + 520f) * 0.5f;
            float qx = Mathf.Abs(x - cx) - (hx - BasinR), qz = Mathf.Abs(z - cz) - (hz - BasinR);
            float ox = Mathf.Max(qx, 0f), oz = Mathf.Max(qz, 0f);
            float outside = Mathf.Sqrt(ox * ox + oz * oz) + Mathf.Min(Mathf.Max(qx, qz), 0f) - BasinR;
            return -outside;
        }

        /// <summary>Signed distance to the shoreline (quay face): positive over water, negative on land.</summary>
        public static float ShoreSD(float x, float z)
        {
            return Mathf.Max(SeaSD(x, z), BasinSD(x, z));
        }

        public static bool InBasin(float x, float z) { return BasinSD(x, z) > 0f; }

        // ------------------------------------------------------------------ streets
        public static readonly Vector2[] JadeAvenue =
        {
            new Vector2(690f, -130f), new Vector2(760f, -130f), new Vector2(880f, -130f), new Vector2(1000f, -130f),
            new Vector2(1116f, -130f), new Vector2(1225f, -130f), new Vector2(1334f, -130f), new Vector2(1415f, -130f)
        };
        public const float BridgeX0 = 1116f, BridgeX1 = 1334f, BridgeZ = -130f, PylonX = 1225f;

        public static readonly Vector2[] CentralAvenue = { new Vector2(740f, 50f), new Vector2(900f, 50f), new Vector2(1100f, 50f), new Vector2(1300f, 50f), new Vector2(1415f, 50f) };
        public static readonly Vector2[] NorthAvenue = { new Vector2(740f, 230f), new Vector2(900f, 230f), new Vector2(1100f, 230f), new Vector2(1300f, 230f), new Vector2(1415f, 230f) };
        public static readonly Vector2[] WestStreet = { new Vector2(740f, -130f), new Vector2(740f, 0f), new Vector2(740f, 120f), new Vector2(740f, 230f) };
        public static readonly Vector2[] MidStreetA = { new Vector2(880f, -130f), new Vector2(880f, -40f), new Vector2(880f, 50f) };
        public static readonly Vector2[] MidStreetB = { new Vector2(880f, 50f), new Vector2(880f, 140f), new Vector2(880f, 230f) };
        public static readonly Vector2[] BankStreetA = { new Vector2(1040f, -130f), new Vector2(1040f, -40f), new Vector2(1040f, 50f) };
        public static readonly Vector2[] BankStreetB = { new Vector2(1040f, 50f), new Vector2(1040f, 140f), new Vector2(1040f, 230f) };
        public static readonly Vector2[] EastStreet = { new Vector2(1415f, -130f), new Vector2(1415f, 0f), new Vector2(1415f, 120f), new Vector2(1415f, 230f) };
        public static readonly Vector2[] LanternAlley = { new Vector2(740f, 140f), new Vector2(810f, 140f), new Vector2(880f, 140f) };

        public static readonly Vector2 CircusC = new Vector2(1227f, 140f);
        public const float CircusRx = 115f, CircusRz = 56f;
        public static Vector2[] NeonCircus()
        {
            const int n = 24;
            var p = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;     // starts at the south point (spur from Central Avenue)
                p[i] = CircusC + new Vector2(Mathf.Cos(a) * CircusRx, Mathf.Sin(a) * CircusRz);
            }
            return p;
        }
        public static readonly Vector2[] CircusSpurS = { new Vector2(1227f, 50f), new Vector2(1227f, 67f), new Vector2(1227f, 84f) };
        public static readonly Vector2[] CircusSpurN = { new Vector2(1227f, 230f), new Vector2(1227f, 213f), new Vector2(1227f, 196f) };

        /// <summary>The street circuit: down the harbour side, along the seafront and back up the hillside.</summary>
        public static readonly Vector2[] HarbourStreet =
        {
            new Vector2(1040f, -130f), new Vector2(1042f, -165f), new Vector2(1062f, -208f), new Vector2(1094f, -250f),
            new Vector2(1101f, -310f), new Vector2(1099f, -362f), new Vector2(1082f, -396f), new Vector2(1045f, -409f),
            new Vector2(965f, -406f), new Vector2(880f, -409f), new Vector2(805f, -405f), new Vector2(745f, -399f),
            new Vector2(716f, -374f), new Vector2(721f, -330f), new Vector2(757f, -292f), new Vector2(792f, -253f),
            new Vector2(806f, -205f), new Vector2(800f, -165f), new Vector2(800f, -130f)
        };

        public static readonly Vector2[] TerraceRise =
        {
            new Vector2(880f, -130f), new Vector2(882f, -180f), new Vector2(872f, -245f), new Vector2(888f, -315f), new Vector2(882f, -372f), new Vector2(880f, -407f)
        };

        public static readonly Vector2[] TerminalRoad =
        {
            new Vector2(1415f, -130f), new Vector2(1419f, -190f), new Vector2(1417f, -270f), new Vector2(1410f, -332f), new Vector2(1398f, -375f), new Vector2(1388f, -400f)
        };
        public static readonly Vector2 TerminalEnd = new Vector2(1388f, -400f);

        // ------------------------------------------------------------------ zones and lots
        public static readonly List<CityZone> Zones = new List<CityZone>();
        public static readonly List<CityLot> Lots = new List<CityLot>();
        /// <summary>Flat pads for Plan(): centre, half size, height, blend.</summary>
        public static readonly List<Vector4> FlatPads = new List<Vector4>();
        public static readonly List<float> FlatPadH = new List<float>();
        const float ZCell = 50f;
        static readonly Dictionary<long, List<int>> zoneGrid = new Dictionary<long, List<int>>();

        // landmarks
        public static readonly Vector2 TvTower = CircusC;
        public static readonly Vector2 Stadium = new Vector2(982f, 312f);
        public static readonly Vector2 StadiumR = new Vector2(70f, 46f);
        public static readonly Vector2 Temple = new Vector2(812f, 192f);
        public static readonly Vector2 MoonGate = new Vector2(858f, 140f);
        public static readonly Vector2 JadePlaza = new Vector2(810f, -88f);
        public static readonly Vector2 GatewayArch = new Vector2(712f, -130f);
        public static readonly Vector2 FerryPier = new Vector2(1000f, -445f);
        public static readonly Vector2 Lighthouse = new Vector2(1262f, -575f);

        static CityPlan()
        {
            var rng = new System.Random(5150);
            // ---------- plazas, parks, quays
            Zone(CityZoneKind.Plaza, 810f, -88f, 58f, 30f);              // Jade Plaza (fountain, metro)
            Zone(CityZoneKind.Park, 668f, 120f, 55f, 245f);              // Gateway Park greenbelt (west)
            Zone(CityZoneKind.Plaza, CircusC.x, CircusC.y, 98f, 42f);    // Neon Circus inner plaza
            Zone(CityZoneKind.Promenade, 905f, -430f, 215f, 14f);       // seafront promenade
            Zone(CityZoneKind.Quay, 1095f, -425f, 32f, 18f);              // ferry quay corner
            Zone(CityZoneKind.Yard, 1388f, -395f, 64f, 48f);              // container terminal
            Zone(CityZoneKind.Stadium, Stadium.x, Stadium.y, StadiumR.x + 8f, StadiumR.y + 10f);
            Zone(CityZoneKind.OldTown, 810f, 140f, 56f, 76f);
            Zone(CityZoneKind.Temple, Temple.x, Temple.y + 4f, 40f, 24f);
            Zone(CityZoneKind.Garden, 1170f, 305f, 85f, 55f);             // north park under the cliffs
            Zone(CityZoneKind.Plaza, 960f, -145f, 70f, 10f);              // harbour view terrace

            // ---------- flat pads (plateau blocks are flat anyway; these guard the edges and lower levels)
            Pad(810f, -88f, 62f, 34f, Plat, 6f);
            Pad(CircusC.x, CircusC.y, 104f, 48f, Plat, 6f);
            Pad(Stadium.x, Stadium.y, StadiumR.x + 10f, StadiumR.y + 12f, Plat, 10f);
            Pad(1388f, -398f, 66f, 46f, Low, 8f);                         // terminal yard
            Pad(905f, -430f, 218f, 16f, Low, 4f);                         // promenade

            // ---------- tower lots (blocks between street centrelines, inset for pavements)
            // B2: the CBD core, tallest towers (Jade Spire)
            Tower(918f, -82f, 22f, 22f, 0f, 168f, CityStyle.Setback, 0, true, new Vector2(0f, -1f));
            Tower(992f, -88f, 24f, 24f, 0f, 222f, CityStyle.Pagoda, 2, true, new Vector2(0f, -1f));
            Tower(925f, 6f, 20f, 20f, 0f, 126f, CityStyle.Round, 1, true, new Vector2(-1f, 0f));
            Tower(997f, 4f, 22f, 18f, 0f, 186f, CityStyle.Bamboo, 1, true, new Vector2(1f, 0f));
            // B1 north half (Jade Plaza to the south)
            Tower(780f, 8f, 20f, 20f, 0f, 104f, CityStyle.Round, 3, true, new Vector2(0f, -1f));
            Tower(842f, 0f, 16f, 26f, 0f, 78f, CityStyle.Slab, 0, false, new Vector2(0f, -1f));
            // B3: between the CBD and the basin
            Tower(1083f, -84f, 20f, 26f, 0f, 142f, CityStyle.Twin, 4, true, new Vector2(-1f, 0f));
            Tower(1080f, 6f, 22f, 22f, 0f, 96f, CityStyle.Setback, 3, false, new Vector2(-1f, 0f));
            // B7: harbour gate towers along the basin head
            Tower(1160f, 8f, 20f, 18f, 0f, 112f, CityStyle.Bamboo, 2, true, new Vector2(0f, 1f));
            Tower(1227f, 8f, 26f, 18f, 0f, 150f, CityStyle.Round, 0, true, new Vector2(0f, 1f));
            Tower(1293f, 8f, 20f, 18f, 0f, 92f, CityStyle.Setback, 4, false, new Vector2(0f, 1f));
            // B8: east plateau strip
            Tower(1376f, -84f, 22f, 22f, 0f, 118f, CityStyle.Pagoda, 3, true, new Vector2(1f, 0f));
            Tower(1376f, 4f, 20f, 22f, 0f, 84f, CityStyle.Slab, 1, false, new Vector2(1f, 0f));
            // B5: north of the CBD
            Tower(920f, 98f, 22f, 22f, 0f, 92f, CityStyle.Midrise, 4, false, new Vector2(0f, -1f));
            Tower(998f, 96f, 20f, 24f, 0f, 132f, CityStyle.Twin, 0, true, new Vector2(0f, -1f));
            Tower(918f, 186f, 20f, 22f, 0f, 70f, CityStyle.Slab, 2, false, new Vector2(-1f, 0f));
            Tower(998f, 186f, 22f, 20f, 0f, 106f, CityStyle.Round, 4, false, new Vector2(1f, 0f));
            // B6: corners of the Neon Circus block
            Tower(1072f, 92f, 16f, 22f, 0f, 64f, CityStyle.Slab, 3, false, new Vector2(1f, 0f));
            Tower(1072f, 188f, 16f, 22f, 0f, 82f, CityStyle.Midrise, 1, false, new Vector2(1f, 0f));
            Tower(1381f, 92f, 18f, 22f, 0f, 96f, CityStyle.Setback, 2, false, new Vector2(-1f, 0f));
            Tower(1381f, 188f, 18f, 22f, 0f, 72f, CityStyle.Slab, 4, false, new Vector2(-1f, 0f));
            // north strip under the cliffs
            Tower(780f, 286f, 22f, 20f, 0f, 88f, CityStyle.Midrise, 0, false, new Vector2(0f, -1f));
            Tower(850f, 300f, 18f, 22f, 0f, 116f, CityStyle.Bamboo, 3, true, new Vector2(0f, -1f));
            Tower(1088f, 280f, 18f, 22f, 0f, 74f, CityStyle.Slab, 0, false, new Vector2(0f, -1f));
            Tower(1290f, 290f, 22f, 22f, 0f, 98f, CityStyle.Pagoda, 4, false, new Vector2(0f, -1f));
            Tower(1372f, 286f, 20f, 26f, 0f, 62f, CityStyle.Midrise, 2, false, new Vector2(0f, -1f));
            // east of the east street (narrow strip)
            Tower(1442f, -60f, 12f, 30f, 0f, 46f, CityStyle.Midrise, 3, false, new Vector2(-1f, 0f));
            Tower(1442f, 140f, 12f, 30f, 0f, 54f, CityStyle.Slab, 1, false, new Vector2(-1f, 0f));
            // hillside towers on terraces between the plateau and the seafront
            HillTower(960f, -232f, 18f, 18f, 58f, CityStyle.Midrise, 1);
            HillTower(1000f, -318f, 20f, 16f, 46f, CityStyle.Slab, 3);
            HillTower(935f, -322f, 16f, 16f, 40f, CityStyle.Midrise, 0);
            HillTower(842f, -300f, 14f, 18f, 36f, CityStyle.Midrise, 2);
            // terminal warehouses and offices
            Lot(1438f, -322f, 14f, 24f, 0f, Level(1438f, -322f), 14f, CityStyle.Midrise, 4, false, new Vector2(-1f, 0f));

            // ---------- seafront shophouses (qilou arcades) facing the promenade
            for (float x = 762f; x < 1046f; )
            {
                float w = 9f + (float)rng.NextDouble() * 5f;
                if (x + 2f * w > 866f && x < 896f) { x = 896f; continue; }
                if (x + 2f * w > 1050f) break;
                float cx = x + w;
                Lot(cx, -383f, w - 0.6f, 10f, 0f, Low, 10f + (float)rng.NextDouble() * 9f, CityStyle.Shophouse, rng.Next(6), false, new Vector2(0f, -1f));
                x += 2f * w;
            }

            // ---------- filler mid-rises in the free space of the plateau blocks
            Fill(rng, 750f, -120f, 1112f, 368f, 64);
            Fill(rng, 1132f, -26f, 1458f, 368f, 40);
            Fill(rng, 1342f, -124f, 1458f, -20f, 8);

            // ---------- zone grid (fast lookup for the ground colour)
            for (int i = 0; i < Zones.Count; i++)
            {
                var z = Zones[i];
                float r = Mathf.Max(z.half.x, z.half.y) * 1.42f + 4f;
                int x0 = Mathf.FloorToInt((z.c.x - r) / ZCell), x1 = Mathf.FloorToInt((z.c.x + r) / ZCell);
                int z0 = Mathf.FloorToInt((z.c.y - r) / ZCell), z1 = Mathf.FloorToInt((z.c.y + r) / ZCell);
                for (int gx = x0; gx <= x1; gx++)
                    for (int gz = z0; gz <= z1; gz++)
                    {
                        long k = WorldField.Key(gx, gz);
                        List<int> l;
                        if (!zoneGrid.TryGetValue(k, out l)) { l = new List<int>(); zoneGrid[k] = l; }
                        l.Add(i);
                    }
            }
        }

        struct RoadLine { public Vector2[] p; public float hw; }
        static List<RoadLine> roadLines;

        /// <summary>Distance from p to the nearest street edge (control polylines; good enough for planning).</summary>
        public static float StreetEdge(Vector2 p)
        {
            if (roadLines == null)
            {
                roadLines = new List<RoadLine>();
                System.Action<Vector2[], float> add = (a, w) => roadLines.Add(new RoadLine { p = a, hw = w * 0.5f });
                add(JadeAvenue, 12f); add(CentralAvenue, 11f); add(NorthAvenue, 10f); add(WestStreet, 9f);
                add(MidStreetA, 9f); add(MidStreetB, 9f); add(BankStreetA, 9f); add(BankStreetB, 9f); add(EastStreet, 9f);
                add(LanternAlley, 7f); add(CircusSpurS, 9f); add(CircusSpurN, 9f); add(HarbourStreet, 10f); add(TerraceRise, 9f);
                add(TerminalRoad, 10f);
                var c = NeonCircus();
                var cl = new Vector2[c.Length + 1];
                for (int i = 0; i < c.Length; i++) cl[i] = c[i];
                cl[c.Length] = c[0];
                add(cl, 11f);
            }
            float best = 1e9f;
            foreach (var r in roadLines)
                for (int i = 0; i < r.p.Length - 1; i++)
                {
                    float t;
                    float d = Spline.DistToSegment(p, r.p[i], r.p[i + 1], out t) - r.hw;
                    if (d < best) best = d;
                }
            return best;
        }

        static bool RectFree(Vector2 c, Vector2 half, float margin)
        {
            foreach (var z in Zones)
            {
                if (z.kind == CityZoneKind.Park || z.kind == CityZoneKind.Promenade) continue;
                if (Mathf.Abs(c.x - z.c.x) < half.x + z.half.x + margin && Mathf.Abs(c.y - z.c.y) < half.y + z.half.y + margin) return false;
            }
            // street clearance at the corners, edge midpoints and centre
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    if (StreetEdge(c + new Vector2(half.x * i, half.y * j)) < 7f) return false;
            return true;
        }

        static void Fill(System.Random rng, float x0, float z0, float x1, float z1, int tries)
        {
            for (int t = 0; t < tries * 6 && tries > 0; t++)
            {
                Vector2 half = new Vector2(9f + (float)rng.NextDouble() * 9f, 9f + (float)rng.NextDouble() * 9f);
                Vector2 c = new Vector2(Mathf.Lerp(x0 + half.x, x1 - half.x, (float)rng.NextDouble()), Mathf.Lerp(z0 + half.y, z1 - half.y, (float)rng.NextDouble()));
                if (ShoreSD(c.x, c.y) > -half.magnitude - 12f) continue;
                if (!RectFree(c, half, 7f)) continue;
                float core = Mathf.Clamp01(1f - Vector2.Distance(c, new Vector2(980f, -40f)) / 420f);
                float h = 22f + (float)rng.NextDouble() * 26f + core * 36f;
                // face the nearest street
                Vector2 face = new Vector2(0f, -1f);
                float bd = 1e9f;
                for (int k = 0; k < 4; k++)
                {
                    Vector2 dir = k == 0 ? new Vector2(1f, 0f) : k == 1 ? new Vector2(-1f, 0f) : k == 2 ? new Vector2(0f, 1f) : new Vector2(0f, -1f);
                    float d = StreetEdge(c + Vector2.Scale(dir, half) + dir * 12f);
                    if (d < bd) { bd = d; face = dir; }
                }
                var st = rng.NextDouble() < 0.5 ? CityStyle.Midrise : CityStyle.Slab;
                Lot(c.x, c.y, half.x, half.y, 0f, Plat, h, st, rng.Next(5), false, face);
                Pad(c.x, c.y, half.x + 4f, half.y + 4f, Plat, 5f);
                tries--;
            }
        }

        static void Zone(CityZoneKind k, float x, float z, float hx, float hz)
        {
            Zones.Add(new CityZone { c = new Vector2(x, z), half = new Vector2(hx, hz), kind = k, lot = -1 });
        }

        static void Pad(float x, float z, float hx, float hz, float h, float blend)
        {
            FlatPads.Add(new Vector4(x, z, hx, hz));
            FlatPadH.Add(h);
            FlatPadH.Add(blend);
        }

        static void Lot(float x, float z, float hx, float hz, float yaw, float baseY, float height, CityStyle st, int pal, bool sky, Vector2 face)
        {
            int idx = Lots.Count;
            Lots.Add(new CityLot
            {
                c = new Vector2(x, z), half = new Vector2(hx, hz), yaw = yaw, baseY = baseY, height = height, style = st,
                palette = pal, seed = 1000 + idx * 37, skyline = sky, neon = st != CityStyle.Shophouse && height < 140f, face = face
            });
            Zones.Add(new CityZone { c = new Vector2(x, z), half = new Vector2(hx + 2f, hz + 2f), kind = CityZoneKind.Lot, lot = idx });
        }

        static void Tower(float x, float z, float hx, float hz, float yaw, float height, CityStyle st, int pal, bool sky, Vector2 face)
        {
            Lot(x, z, hx, hz, yaw, Plat, height, st, pal, sky, face);
            Pad(x, z, hx + 6f, hz + 6f, Plat, 6f);
        }

        static void HillTower(float x, float z, float hx, float hz, float height, CityStyle st, int pal)
        {
            float y = Level(x, z);
            Lot(x, z, hx, hz, 0f, y, height, st, pal, false, new Vector2(0f, -1f));
            Pad(x, z, hx + 5f, hz + 5f, y, 9f);
        }

        /// <summary>The zone covering p (lots win over the areas they sit in), or -1.</summary>
        public static int ZoneAt(float x, float z)
        {
            List<int> l;
            if (!zoneGrid.TryGetValue(WorldField.Key(Mathf.FloorToInt(x / ZCell), Mathf.FloorToInt(z / ZCell)), out l)) return -1;
            int best = -1;
            for (int k = 0; k < l.Count; k++)
            {
                var zn = Zones[l[k]];
                if (Mathf.Abs(x - zn.c.x) > zn.half.x || Mathf.Abs(z - zn.c.y) > zn.half.y) continue;
                if (best < 0 || zn.kind == CityZoneKind.Lot) best = l[k];
                if (zn.kind == CityZoneKind.Lot) break;
            }
            return best;
        }

        /// <summary>The road polylines (with names) the region adds, in order.</summary>
        public static float Hash(float x, float z)
        {
            return WorldNoise.Hash01(Mathf.FloorToInt(x), Mathf.FloorToInt(z), 911);
        }
    }
}
