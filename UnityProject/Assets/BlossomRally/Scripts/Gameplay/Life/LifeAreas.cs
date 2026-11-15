// LifeAreas.cs - where people belong: pavements beside the roads of towns and villages, and wander areas
// (plazas, quays, the beach, monastery terraces, the ski-lodge village, the canyon oasis).
//
//   * Pavements: for every road sample inside a pedestrian zone (all of Jade Harbour City; Lantern Street and
//     the Blossom Valley village; the Lotus Coast harbour; the ski lodge; the oasis; the monastery) and each
//     side, a walkable flag: the point 2.2 m past the road edge must be off every other road, level with the
//     road (no cliff or fill) and above water. Runs of walkable samples become pedestrian sites (~32 m).
//   * Wander areas: hand-placed rectangles (positions copied from the region layouts, like MapMarkers does,
//     so the module still compiles against region placeholders). Their points are validated lazily, the
//     first time the player comes near and the region's colliders are live: flat ground (downward raycast
//     agrees with the terrain), clear of roads and water, no collider in the way. Benches are placed then too.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal enum SiteStyle { City, Valley, Harbour, Beach, Monastery, Snow, Desert, Hikers }

    internal sealed class BenchSeat
    {
        public Vector3 pos;      // where the sitter's feet go (in front of the bench)
        public float yaw;        // facing
        public bool taken;
    }

    internal sealed class LifeArea
    {
        public string name;
        public RegionId region;
        public Vector2 c, half;
        public float yaw;
        public float hMin = -999f, hMax = 999f, minEdge = 1.5f;
        public SiteStyle style;
        public float density = 1f;
        public int benches;
        public bool built;
        public readonly List<Vector3> pts = new List<Vector3>();
        public readonly List<BenchSeat> seats = new List<BenchSeat>();

        public Vector2 Point(float lx, float lz)
        {
            float a = yaw * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            return c + new Vector2(lx * cs + lz * sn, -lx * sn + lz * cs);
        }
    }

    internal sealed class PaveRoad
    {
        public int road;
        public float seg, hw;
        public bool[] wL, wR;     // side -1 / +1
        public bool Walk(int side, int i) { return side < 0 ? wL[i] : wR[i]; }
    }

    internal sealed class PedSite
    {
        public Vector2 c;
        public float r, weight;
        public int kind;          // 0 pavement run, 1 wander area
        public SiteStyle style;
        public int pave, i0, i1, side;
        public LifeArea area;
    }

    internal static class LifeAreas
    {
        public const float PaveOffset = 2.2f;
        static readonly Vector2 Gate = new Vector2(-88.3f, -332.6f);        // MapData.Gate
        static readonly Vector2 Garage = new Vector2(-222.2f, -435.2f);     // MapData.Garage
        static readonly Vector2 SnowPlaza = new Vector2(948f, 1150f);
        static readonly Vector2 Oasis = new Vector2(-1227f, 1046f);
        static readonly Vector2 Monastery = new Vector2(214f, 1386f);

        static LifeArea A(string n, RegionId r, SiteStyle s, float x, float z, float hx, float hz, float density, int benches, float yaw = 0f)
        {
            return new LifeArea { name = n, region = r, style = s, c = new Vector2(x, z), half = new Vector2(hx, hz), density = density, benches = benches, yaw = yaw };
        }

        public static List<LifeArea> Build()
        {
            const float Water = WorldField.WaterY;
            var l = new List<LifeArea>
            {
                // Blossom Valley
                A("Village lanes", RegionId.Valley, SiteStyle.Valley, -99f, -440f, 185f, 98f, 0.9f, 3),
                A("Paifang gate", RegionId.Valley, SiteStyle.Valley, Gate.x, Gate.y + 6f, 16f, 10f, 0.8f, 1),
                A("Lotus lake shore", RegionId.Valley, SiteStyle.Valley, -125f, 2f, 72f, 50f, 0.45f, 3),
                // Jade Harbour City (CityPlan zones)
                A("Jade Plaza", RegionId.City, SiteStyle.City, 810f, -88f, 56f, 28f, 1.3f, 4),
                A("Neon Circus plaza", RegionId.City, SiteStyle.City, 1227f, 140f, 70f, 30f, 1.1f, 3),
                A("Seafront promenade", RegionId.City, SiteStyle.City, 905f, -430f, 212f, 12f, 1.1f, 4),
                A("Old Town", RegionId.City, SiteStyle.City, 810f, 140f, 54f, 72f, 0.9f, 2),
                A("City temple", RegionId.City, SiteStyle.City, 812f, 196f, 38f, 22f, 0.6f, 1),
                A("North gardens", RegionId.City, SiteStyle.City, 1170f, 305f, 82f, 52f, 0.6f, 3),
                A("Gateway Park", RegionId.City, SiteStyle.City, 668f, 120f, 52f, 240f, 0.55f, 3),
                A("Harbour view terrace", RegionId.City, SiteStyle.City, 960f, -145f, 68f, 8f, 0.6f, 2),
                A("Ferry quay", RegionId.City, SiteStyle.City, 1095f, -425f, 30f, 16f, 0.6f, 1),
                // Lotus Coast
                A("Harbour quay", RegionId.Coast, SiteStyle.Harbour, -1182f, 252f, 42f, 104f, 1.1f, 2),
                A("Mazu temple", RegionId.Coast, SiteStyle.Harbour, -1192f, 334f, 14f, 12f, 0.5f, 0),
                A("Sunset beach", RegionId.Coast, SiteStyle.Beach, -1182f, -262f, 38f, 112f, 1.0f, 0),
                A("Lighthouse lawn", RegionId.Coast, SiteStyle.Hikers, -1335f, -12f, 18f, 14f, 0.3f, 0),
                // Cloud Peak monastery terraces (flat rects at yaw 12)
                A("Monastery plaza", RegionId.Mountains, SiteStyle.Monastery, 214f, 1386f, 26f, 15f, 1.0f, 2, 12f),
                A("Monastery terrace", RegionId.Mountains, SiteStyle.Monastery, 222f, 1420f, 23f, 12f, 0.7f, 0, 12f),
                A("Monastery upper hall", RegionId.Mountains, SiteStyle.Monastery, 230f, 1448f, 17f, 10f, 0.5f, 0, 12f),
                A("Cable car station", RegionId.Mountains, SiteStyle.Hikers, 112f, 1162f, 12f, 10f, 0.35f, 1),
                A("Jade lake shore", RegionId.Mountains, SiteStyle.Hikers, 176f, 1106f, 70f, 48f, 0.3f, 2),
                // Frost Summit ski lodge
                A("Ice sculpture plaza", RegionId.Snow, SiteStyle.Snow, 948f, 1150f, 24f, 16f, 1.1f, 3, -12f),
                A("Lodge village", RegionId.Snow, SiteStyle.Snow, 948f, 1150f, 60f, 45f, 0.6f, 0, -12f),
                A("Cable base station", RegionId.Snow, SiteStyle.Snow, 998f, 1248f, 13f, 11f, 0.5f, 1),
                A("Observatory", RegionId.Snow, SiteStyle.Snow, 1238f, 1410f, 20f, 18f, 0.2f, 0),
                // Red Canyon oasis
                A("Oasis", RegionId.Canyon, SiteStyle.Desert, -1255f, 1040f, 45f, 40f, 1.0f, 3),
                A("Caravanserai", RegionId.Canyon, SiteStyle.Desert, -994f, 1290f, 21f, 21f, 0.6f, 1, 22f),
                A("Watchtower", RegionId.Canyon, SiteStyle.Desert, -1062f, 1394f, 10f, 10f, 0.2f, 0),
            };
            foreach (var a in l)
            {
                a.hMin = Water + 0.35f;
                switch (a.name)
                {
                    case "Harbour quay": a.hMin = 2.3f; a.hMax = 3.8f; a.minEdge = 1.2f; break;
                    case "Sunset beach": a.hMin = 0.4f; a.hMax = 3.6f; a.minEdge = 2.5f; break;
                    case "Oasis": a.hMin = 24.55f; break;                              // CanyonLayout.PoolLevel + margin
                    case "Jade lake shore": a.hMin = 77.4f; break;                     // MountainsMap.LakeY + margin
                    case "Seafront promenade": case "Ferry quay": a.minEdge = 1.0f; break;
                }
            }
            return l;
        }

        /// <summary>Is a pavement point at p part of a pedestrian zone (and in which style)?</summary>
        public static bool PaveZone(Vector2 p, RegionId roadRegion, bool connector, out SiteStyle style)
        {
            style = SiteStyle.City;
            RegionId at = WorldLayout.At(p);
            if (roadRegion == RegionId.City && at == RegionId.City && !connector) { style = SiteStyle.City; return true; }
            if (at == RegionId.Valley)
            {
                style = SiteStyle.Valley;
                return (MapData.InVillage(p) || (p - Gate).sqrMagnitude < 45f * 45f) && (p - Garage).sqrMagnitude > 22f * 22f;
            }
            if (at == RegionId.Coast) { style = SiteStyle.Harbour; return p.x < -1100f && p.y > 140f && p.y < 350f; }
            if (at == RegionId.Snow) { style = SiteStyle.Snow; return (p - SnowPlaza).sqrMagnitude < 85f * 85f; }
            if (at == RegionId.Canyon) { style = SiteStyle.Desert; return (p - Oasis).sqrMagnitude < 70f * 70f; }
            if (at == RegionId.Mountains) { style = SiteStyle.Monastery; return (p - Monastery).sqrMagnitude < 70f * 70f; }
            return false;
        }

        /// <summary>Walkable flags for both pavements of every road in a pedestrian zone, and their sites.</summary>
        public static void BuildPavements(LifeDirector d, List<PaveRoad> paves, List<PedSite> sites)
        {
            var f = d.Field;
            for (int r = 0; r < f.roads.Count; r++)
            {
                var def = f.roads[r];
                var s = f.roadSamples[r];
                int n = s != null ? s.Count : 0;
                if (n < 6) continue;
                if (def.surface != Surface.Asphalt && def.surface != Surface.Paving && def.surface != Surface.Snow) continue;
                RegionId reg = WorldLayout.At(s[n / 2].pos);
                // quick reject: no sample of this road in any zone
                bool any = false;
                SiteStyle st;
                for (int i = 0; i < n && !any; i += 4) any = PaveZone(s[i].pos, reg, def.connector, out st);
                if (!any) continue;
                var pr = new PaveRoad { road = r, hw = def.width * 0.5f, wL = new bool[n], wR = new bool[n] };
                pr.seg = Mathf.Max(0.5f, s[n - 1].distance / (n - 1));
                int count = 0;
                for (int i = 0; i < n; i++)
                {
                    var rs = s[i];
                    if (rs.bridge) continue;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 p = rs.pos + rs.Right * side * (pr.hw + PaveOffset);
                        if (!PaveZone(p, reg, def.connector, out st)) continue;
                        float g = d.Ground(p.x, p.y);
                        if (g < WorldField.WaterY + 0.3f || Mathf.Abs(g - rs.height) > 0.9f) continue;
                        if (f.EdgeDistance(p) < 1.0f) continue;
                        if (side < 0) pr.wL[i] = true; else pr.wR[i] = true;
                        count++;
                    }
                }
                if (count < 6) continue;
                int pi = paves.Count;
                paves.Add(pr);
                // runs of walkable samples -> sites of up to 16 samples
                for (int side = -1; side <= 1; side += 2)
                {
                    int i = 0;
                    while (i < n)
                    {
                        if (!pr.Walk(side, i)) { i++; continue; }
                        int j = i;
                        while (j + 1 < n && pr.Walk(side, j + 1) && j - i < 15) j++;
                        if (j - i >= 3)
                        {
                            Vector2 c = (s[i].pos + s[j].pos) * 0.5f;
                            PaveZone(c, reg, def.connector, out st);
                            sites.Add(new PedSite
                            {
                                kind = 0, pave = pi, i0 = i, i1 = j, side = side, c = c, r = (j - i) * pr.seg * 0.5f + 4f, style = st,
                                weight = (st == SiteStyle.City ? 0.42f : 0.36f) * (j - i + 1) / 16f
                            });
                        }
                        i = j + 1;
                    }
                }
            }
        }

        /// <summary>Validate wander points and place benches (needs the region's colliders, so call it when live).</summary>
        public static void BuildArea(LifeDirector d, LifeArea a, System.Random rng)
        {
            a.built = true;
            var f = d.Field;
            int mask = LifeDirector.SolidMask;
            int want = Mathf.Clamp(Mathf.RoundToInt(a.half.x * a.half.y / 60f), 10, 40);
            for (int t = 0; t < want * 4 && a.pts.Count < want; t++)
            {
                Vector2 p = a.Point(((float)rng.NextDouble() * 2f - 1f) * a.half.x, ((float)rng.NextDouble() * 2f - 1f) * a.half.y);
                Vector3 g;
                if (Valid(d, a, p, mask, out g)) a.pts.Add(g);
            }
            if (a.benches <= 0 || a.pts.Count < 4) return;
            var geo = new LifeGeo();
            int placed = 0;
            for (int t = 0; t < 40 && placed < a.benches; t++)
            {
                Vector3 p = a.pts[rng.Next(a.pts.Count)];
                Vector2 p2 = new Vector2(p.x, p.z);
                if (f.EdgeDistance(p2) < 3f) continue;
                bool near = false;
                foreach (var s in a.seats) if ((s.pos - p).sqrMagnitude < 25f) { near = true; break; }
                if (near) continue;
                // face the middle of the area (the plaza, the sea, the lake)
                Vector2 toC = a.c - p2;
                float yaw = toC.sqrMagnitude > 4f ? LifeDirector.YawOf(toC) : (float)rng.NextDouble() * 360f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                if (Physics.CheckBox(p + Vector3.up * 0.55f, new Vector3(0.95f, 0.45f, 0.45f), q, mask, QueryTriggerInteraction.Ignore)) continue;
                float h0 = d.Ground(p.x, p.z);
                Vector3 l = q * new Vector3(0.8f, 0f, 0f), fw = q * new Vector3(0f, 0f, 0.4f);
                if (Mathf.Abs(d.Ground(p.x + l.x, p.z + l.z) - h0) > 0.25f || Mathf.Abs(d.Ground(p.x - l.x, p.z - l.z) - h0) > 0.25f) continue;
                LifeVehicles.Bench(geo, p, yaw);
                for (int s = -1; s <= 1; s += 2)
                    a.seats.Add(new BenchSeat { pos = p + q * new Vector3(s * 0.42f, 0f, 0.42f), yaw = yaw });
                placed++;
            }
            if (placed > 0) LifeMesh.Make("LifeBenches_" + a.name, geo, LifeMesh.VertexColour, d.RegionRoot(a.region), GameSettings.Quality >= 1);
        }

        /// <summary>A standing spot: off roads, above water, flat, not inside anything. g = ground point.</summary>
        public static bool Valid(LifeDirector d, LifeArea a, Vector2 p, int mask, out Vector3 g)
        {
            g = Vector3.zero;
            float h = d.Ground(p.x, p.y);
            if (h < a.hMin || h > a.hMax) return false;
            if (d.Field.EdgeDistance(p) < a.minEdge) return false;
            if (Mathf.Abs(d.Ground(p.x + 1f, p.y) - h) > 0.45f || Mathf.Abs(d.Ground(p.x, p.y + 1f) - h) > 0.45f) return false;
            float y = h;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, h + 3.5f, p.y), Vector3.down, out hit, 6f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.point.y > h + 0.7f) return false;          // on a roof, wall or stall
                if (hit.point.y > h - 0.4f) y = Mathf.Max(h, hit.point.y);
            }
            if (Physics.CheckCapsule(new Vector3(p.x, y + 0.45f, p.y), new Vector3(p.x, y + 1.6f, p.y), 0.32f, mask, QueryTriggerInteraction.Ignore)) return false;
            g = new Vector3(p.x, y, p.y);
            return true;
        }
    }
}
