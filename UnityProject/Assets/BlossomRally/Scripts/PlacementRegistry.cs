// PlacementRegistry.cs - one shared map of what already occupies the valley (trees, houses, pads,
// props) so new props never overlap each other, roads, rivers or the lake.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class PlacementRegistry
    {
        const float Cell = 16f;
        static readonly Dictionary<long, List<Vector3>> grid = new Dictionary<long, List<Vector3>>();   // x, z, radius

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        public static void Clear() { grid.Clear(); }

        /// <summary>Mark a circle as used.</summary>
        public static void Reserve(Vector2 p, float radius)
        {
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell), x1 = Mathf.FloorToInt((p.x + radius) / Cell);
            int z0 = Mathf.FloorToInt((p.y - radius) / Cell), z1 = Mathf.FloorToInt((p.y + radius) / Cell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<Vector3> l;
                    long k = Key(x, z);
                    if (!grid.TryGetValue(k, out l)) { l = new List<Vector3>(); grid[k] = l; }
                    l.Add(new Vector3(p.x, p.y, radius));
                }
        }

        /// <summary>True if a circle overlaps nothing that is registered.</summary>
        public static bool IsFree(Vector2 p, float radius)
        {
            int x0 = Mathf.FloorToInt((p.x - radius) / Cell), x1 = Mathf.FloorToInt((p.x + radius) / Cell);
            int z0 = Mathf.FloorToInt((p.y - radius) / Cell), z1 = Mathf.FloorToInt((p.y + radius) / Cell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<Vector3> l;
                    if (!grid.TryGetValue(Key(x, z), out l)) continue;
                    foreach (var c in l)
                    {
                        float r = radius + c.z;
                        if ((new Vector2(c.x, c.y) - p).sqrMagnitude < r * r) return false;
                    }
                }
            return true;
        }

        /// <summary>
        /// Free of registered objects AND at least roadClearance metres from any road edge, off the river and lake,
        /// inside the playable area. Use roadClearance &lt; 0 to allow the spot on a road verge only (no check).
        /// </summary>
        public static bool CanPlace(WorldBuilder w, Vector2 p, float radius, float roadClearance = 2f)
        {
            if (Mathf.Abs(p.x) > 470f || Mathf.Abs(p.y) > 470f) return false;
            if (!IsFree(p, radius)) return false;
            if (w == null) return true;
            if (w.RiverDistance(p) < 15f + radius) return false;
            if (MapData.LakeK(p) < 1.3f) return false;
            if (roadClearance >= 0f)
            {
                RoadSample rs;
                float d = w.NearestRoad(p, out rs, 40f);
                if (d < 40f && d - rs.halfWidth < radius + roadClearance) return false;
            }
            return true;
        }

        /// <summary>CanPlace + Reserve in one call.</summary>
        public static bool TryReserve(WorldBuilder w, Vector2 p, float radius, float roadClearance = 2f)
        {
            if (!CanPlace(w, p, radius, roadClearance)) return false;
            Reserve(p, radius);
            return true;
        }
    }
}
