// WorldLayout.cs - the six-region world: a 3 x 2 grid of 1 km tiles (3 km east-west, 2 km north-south).
//
//            x: -1500 ........ -500 ........ 500 ........ 1500
//   z 1500  +------------------+-------------+-----------------+
//           |  RED CANYON      |  MOUNTAINS  |  SNOW SUMMIT    |
//   z  500  +------------------+-------------+-----------------+
//           |  LOTUS COAST     |  BLOSSOM    |  JADE CITY      |
//           |  (sea to the W)  |  VALLEY     |                 |
//   z -500  +------------------+-------------+-----------------+
//
// The original 1 km Blossom Valley keeps its exact coordinates (tile 0,0 centred on the origin).
// Neighbouring tiles blend into each other across a soft band at the borders, natural ridges separate the
// regions, and connector roads cross those ridges through low passes (see Connectors). Pure C# (no engine
// calls) so the offline terrain harness can evaluate it.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum RegionId { Valley = 0, City = 1, Coast = 2, Mountains = 3, Snow = 4, Canyon = 5 }

    /// <summary>A road that links two regions across their shared border (owned by the framework).</summary>
    public sealed class ConnectorDef
    {
        public string name;
        public RegionId a, b;
        public Surface surface;
        public float width;
        public Vector2[] points;      // control points: first lies in region a, last in region b
        public ConnectorDef(string n, RegionId ra, RegionId rb, Surface s, float w, params Vector2[] p) { name = n; a = ra; b = rb; surface = s; width = w; points = p; }
    }

    /// <summary>Ridge along an internal border. Passes are cut where connector roads cross it.</summary>
    public struct BorderRidge
    {
        public bool vertical;          // true: border is the line x = coord (from lo to hi along z); false: z = coord
        public float coord, lo, hi;
        public float height;           // crest height above the blended terrain (m)
        public float halfWidth;        // bell half width (m)
        public float offset;           // crest shifted this far from the border line (+ = towards +x / +z)
        public int seed;
        public BorderRidge(bool v, float c, float l, float h, float height, float halfW, float off, int s)
        { vertical = v; coord = c; lo = l; hi = h; this.height = height; halfWidth = halfW; offset = off; seed = s; }
    }

    public static class WorldLayout
    {
        public const float Tile = 1000f;
        // playable area (boundary walls sit on these lines)
        public const float MinX = -1500f, MaxX = 1500f, MinZ = -500f, MaxZ = 1500f;
        /// <summary>Terrain extends this far beyond the playable area (rim mountains / sea).</summary>
        public const float TerrainPad = 240f;
        /// <summary>Half width of the blend band between neighbouring tiles.</summary>
        public const float Blend = 110f;

        public static readonly RegionId[] All = { RegionId.Valley, RegionId.City, RegionId.Coast, RegionId.Mountains, RegionId.Snow, RegionId.Canyon };

        public static Vector2 Center { get { return new Vector2(0.5f * (MinX + MaxX), 0.5f * (MinZ + MaxZ)); } }
        public static Vector2 Size { get { return new Vector2(MaxX - MinX, MaxZ - MinZ); } }

        /// <summary>Tile column (-1, 0, 1) and row (0 = south, 1 = north) of a region.</summary>
        public static void TileOf(RegionId id, out int tx, out int tz)
        {
            switch (id)
            {
                case RegionId.City: tx = 1; tz = 0; return;
                case RegionId.Coast: tx = -1; tz = 0; return;
                case RegionId.Mountains: tx = 0; tz = 1; return;
                case RegionId.Snow: tx = 1; tz = 1; return;
                case RegionId.Canyon: tx = -1; tz = 1; return;
                default: tx = 0; tz = 0; return;
            }
        }

        public static RegionId FromTile(int tx, int tz)
        {
            tx = Mathf.Clamp(tx, -1, 1); tz = Mathf.Clamp(tz, 0, 1);
            if (tz == 0) return tx < 0 ? RegionId.Coast : tx > 0 ? RegionId.City : RegionId.Valley;
            return tx < 0 ? RegionId.Canyon : tx > 0 ? RegionId.Snow : RegionId.Mountains;
        }

        /// <summary>World rectangle (x, z) of a region's tile.</summary>
        public static Rect RectOf(RegionId id)
        {
            int tx, tz;
            TileOf(id, out tx, out tz);
            return new Rect(tx * Tile - Tile * 0.5f, tz * Tile - Tile * 0.5f, Tile, Tile);
        }

        public static Vector2 CenterOf(RegionId id) { return RectOf(id).center; }

        /// <summary>The region whose tile contains p (clamped to the grid).</summary>
        public static RegionId At(Vector2 p)
        {
            return FromTile(Mathf.FloorToInt((p.x + Tile * 0.5f) / Tile), Mathf.FloorToInt((p.y + Tile * 0.5f) / Tile));
        }

        public static bool InPlayable(Vector2 p, float margin = 0f)
        {
            return p.x > MinX + margin && p.x < MaxX - margin && p.y > MinZ + margin && p.y < MaxZ - margin;
        }

        /// <summary>Distance outside the playable rectangle (0 inside).</summary>
        public static float OutsideDistance(Vector2 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(MinX - p.x, p.x - MaxX));
            float dz = Mathf.Max(0f, Mathf.Max(MinZ - p.y, p.y - MaxZ));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Signed distance to the playable edge: positive inside, negative outside.</summary>
        public static float EdgeDistance(Vector2 p)
        {
            float inside = Mathf.Min(Mathf.Min(p.x - MinX, MaxX - p.x), Mathf.Min(p.y - MinZ, MaxZ - p.y));
            return inside >= 0f ? inside : -OutsideDistance(p);
        }

        // ------------------------------------------------------------------ blend weights
        /// <summary>
        /// Partition-of-unity weights of the six tiles at p (index = (int)RegionId). At most four are non-zero
        /// (only near a tile corner); deep inside a tile its weight is exactly 1.
        /// </summary>
        public static void Weights(Vector2 p, float[] w)
        {
            float s1 = WorldNoise.Smooth(-Tile * 0.5f - Blend, -Tile * 0.5f + Blend, p.x);
            float s2 = WorldNoise.Smooth(Tile * 0.5f - Blend, Tile * 0.5f + Blend, p.x);
            float wxW = 1f - s1, wxC = s1 - s2, wxE = s2;
            float t = WorldNoise.Smooth(Tile * 0.5f - Blend, Tile * 0.5f + Blend, p.y);
            float wzS = 1f - t, wzN = t;
            w[(int)RegionId.Coast] = wxW * wzS;
            w[(int)RegionId.Valley] = wxC * wzS;
            w[(int)RegionId.City] = wxE * wzS;
            w[(int)RegionId.Canyon] = wxW * wzN;
            w[(int)RegionId.Mountains] = wxC * wzN;
            w[(int)RegionId.Snow] = wxE * wzN;
        }

        // ------------------------------------------------------------------ connectors (cross-border roads)
        // Gateways: each region's own road network must START (first control point) exactly at its gateway
        // points with roughly the given heading, so the connector and the region road meet end to end.
        public static readonly Vector2 GateValleyEast = new Vector2(226f, -100f);      // on the valley main loop (T-junction)
        public static readonly Vector2 GateCityWest = new Vector2(690f, -130f);
        public static readonly Vector2 GateValleyWest = new Vector2(-437f, 4f);        // on the bamboo run (T-junction)
        public static readonly Vector2 GateCoastEast = new Vector2(-690f, 40f);
        public static readonly Vector2 GateValleyNorth = new Vector2(-72f, 292f);      // on the valley main loop (T-junction)
        public static readonly Vector2 GateMountainsSouth = new Vector2(-110f, 700f);
        public static readonly Vector2 GateMountainsEast = new Vector2(320f, 1160f);
        public static readonly Vector2 GateSnowWest = new Vector2(690f, 1140f);
        public static readonly Vector2 GateMountainsWest = new Vector2(-320f, 940f);
        public static readonly Vector2 GateCanyonEast = new Vector2(-690f, 900f);
        public static readonly Vector2 GateCanyonSouth = new Vector2(-1010f, 690f);
        public static readonly Vector2 GateCoastNorth = new Vector2(-990f, 310f);

        /// <summary>Target ground height at each region gateway (region terrain should sit near it within ~120 m).</summary>
        public const float HCityWest = 18f, HCoastEast = 7f, HMountainsSouth = 24f, HMountainsEast = 96f, HSnowWest = 124f,
                           HMountainsWest = 58f, HCanyonEast = 40f, HCanyonSouth = 30f, HCoastNorth = 12f;

        public static readonly ConnectorDef[] Connectors =
        {
            new ConnectorDef("Road_Link_ValleyCity", RegionId.Valley, RegionId.City, Surface.Asphalt, 10f,
                GateValleyEast, new Vector2(300f, -112f), new Vector2(420f, -128f), new Vector2(540f, -122f), new Vector2(620f, -128f), GateCityWest),
            new ConnectorDef("Road_Link_ValleyCoast", RegionId.Valley, RegionId.Coast, Surface.Gravel, 8f,
                GateValleyWest, new Vector2(-470f, 2f), new Vector2(-540f, 22f), new Vector2(-610f, 30f), GateCoastEast),
            new ConnectorDef("Road_Link_ValleyMountains", RegionId.Valley, RegionId.Mountains, Surface.Asphalt, 9f,
                GateValleyNorth, new Vector2(-82f, 360f), new Vector2(-76f, 430f), new Vector2(-96f, 510f), new Vector2(-88f, 590f), new Vector2(-104f, 650f), GateMountainsSouth),
            new ConnectorDef("Road_Link_MountainsSnow", RegionId.Mountains, RegionId.Snow, Surface.Asphalt, 9f,
                GateMountainsEast, new Vector2(400f, 1150f), new Vector2(480f, 1130f), new Vector2(560f, 1146f), new Vector2(630f, 1140f), GateSnowWest),
            new ConnectorDef("Road_Link_MountainsCanyon", RegionId.Mountains, RegionId.Canyon, Surface.Gravel, 8f,
                GateMountainsWest, new Vector2(-400f, 930f), new Vector2(-480f, 914f), new Vector2(-560f, 906f), new Vector2(-630f, 902f), GateCanyonEast),
            new ConnectorDef("Road_Link_CanyonCoast", RegionId.Canyon, RegionId.Coast, Surface.Dirt, 8f,
                GateCanyonSouth, new Vector2(-1000f, 620f), new Vector2(-984f, 540f), new Vector2(-998f, 460f), new Vector2(-990f, 380f), GateCoastNorth),
        };

        // ------------------------------------------------------------------ ridges between regions
        public static readonly BorderRidge[] Ridges =
        {
            new BorderRidge(true, -500f, -500f, 500f, 30f, 120f, -50f, 11),     // Coast | Valley (crest on the coast side)
            new BorderRidge(true, 500f, -500f, 500f, 9f, 100f, 70f, 12),        // Valley | City (low hills, the skyline stays visible)
            new BorderRidge(true, -500f, 500f, 1500f, 46f, 130f, 0f, 13),       // Canyon | Mountains
            new BorderRidge(true, 500f, 500f, 1500f, 58f, 140f, 0f, 14),        // Mountains | Snow
            new BorderRidge(false, 500f, -1500f, -500f, 34f, 120f, 0f, 15),     // Coast | Canyon
            new BorderRidge(false, 500f, -500f, 500f, 26f, 120f, 70f, 16),      // Valley | Mountains (crest on the mountain side)
            new BorderRidge(false, 500f, 500f, 1500f, 80f, 150f, 30f, 17),      // City | Snow (a steep wall: no road)
        };

        /// <summary>Distance from p to the nearest connector centreline polyline (control points, not the spline).</summary>
        public static float ConnectorDistance(Vector2 p)
        {
            float best = 1e9f;
            foreach (var c in Connectors)
                for (int i = 0; i < c.points.Length - 1; i++)
                {
                    float t;
                    float d = Spline.DistToSegment(p, c.points[i], c.points[i + 1], out t);
                    if (d < best) best = d;
                }
            return best;
        }

        /// <summary>Height added by the border ridges at p (passes are notched where connectors cross).</summary>
        public static float RidgeHeight(Vector2 p)
        {
            float h = 0f;
            float notch = -1f;
            foreach (var r in Ridges)
            {
                float across = (r.vertical ? p.x : p.y) - (r.coord + r.offset);
                float along = r.vertical ? p.y : p.x;
                if (Mathf.Abs(across) >= r.halfWidth) continue;
                // fade the ridge out over 120 m past each end so corners stay natural
                float endFade = WorldNoise.Smooth(r.lo - 120f, r.lo + 60f, along) * (1f - WorldNoise.Smooth(r.hi - 60f, r.hi + 120f, along));
                if (endFade <= 0f) continue;
                float bell = 0.5f * (1f + Mathf.Cos(Mathf.PI * across / r.halfWidth));
                bell *= bell * (3f - 2f * bell);      // flatter shoulders, rounder crest
                float vary = 0.62f + 0.55f * (0.5f + 0.5f * WorldNoise.Fbm(along, r.coord, 260f, 3, r.seed));
                float rough = 0.18f * WorldNoise.Ridged(p.x, p.y, 90f, 3, r.seed + 5);
                float hh = r.height * bell * endFade * (vary + rough);
                if (notch < 0f) notch = ConnectorDistance(p);
                hh *= WorldNoise.Smooth(28f, 150f, notch);
                h += hh;
            }
            return h;
        }

        /// <summary>Rim mountains outside the playable area (the coast's west side opens to the sea instead).</summary>
        public static float RimHeight(Vector2 p)
        {
            float o = OutsideDistance(p);
            float inside = EdgeDistance(p);
            float k = WorldNoise.Smooth(-40f, 190f, -inside);       // starts 40 m inside the edge
            if (k <= 0f) return 0f;
            // west edge of the coast tile: open sea, no rim
            if (p.x < MinX + 40f && p.y < MinZ + Tile + 60f)
            {
                float seaSide = WorldNoise.Smooth(MinZ + Tile + 60f, MinZ + Tile - 120f, p.y);
                k *= 1f - seaSide;
                if (k <= 0f) return 0f;
            }
            float n = WorldNoise.Ridged(p.x, p.y, 210f, 4, 77);
            return k * (52f + 70f * n + 0.08f * o);
        }

        /// <summary>Bottom of the sea west of the coast (the coast region uses it beyond its shoreline).</summary>
        public const float SeaFloor = -9f;
    }
}
