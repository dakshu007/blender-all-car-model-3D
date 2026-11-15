// PlaceSites.cs - finding buildings and free ground for the Places module without touching region code:
//  * FindBox: the existing building a place uses (tea house, temple, monastery hall, ski lodge...) is found by the
//    signature of its box collider (size, approximate position) under a region's collider root or WorldProps -
//    this also works while the region is streamed out (inactive colliders still have their transforms).
//  * FreeSite: spiral search for a flat, free footprint next to (but clear of) a road: tests the region
//    colliders (active or not, as 2D oriented boxes / circles in a hash grid), active physics colliders of other
//    modules, road clearance, slope and water.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    internal sealed class PlaceSites
    {
        struct Obb { public Vector2 c, ax, az; public float hx, hz, yMin, yMax; }

        readonly WorldBuilder world;
        readonly List<Obb> obbs = new List<Obb>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        const float Cell = 24f;
        readonly HashSet<Transform> indexed = new HashSet<Transform>();

        public PlaceSites(WorldBuilder w) { world = w; }

        static long Key(int x, int z) { return ((long)x << 32) ^ (uint)z; }

        // ------------------------------------------------------------------ collider index
        /// <summary>Index every box / capsule / mesh collider under root (inactive included).</summary>
        public void Index(Transform root)
        {
            if (root == null || !indexed.Add(root)) return;
            foreach (var b in root.GetComponentsInChildren<BoxCollider>(true)) AddBox(b.transform, b.center, b.size);
            foreach (var c in root.GetComponentsInChildren<CapsuleCollider>(true))
            {
                Vector3 p = c.transform.TransformPoint(c.center);
                float r = c.radius * Mathf.Max(c.transform.lossyScale.x, c.transform.lossyScale.z);
                float h = c.height * c.transform.lossyScale.y;
                AddObb(new Vector2(p.x, p.z), Vector2.right, Vector2.up, r, r, p.y - h * 0.5f, p.y + h * 0.5f);
            }
            foreach (var m in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (m.sharedMesh == null || m.GetComponent<TerrainSurface>() != null) continue;
                Bounds lb = m.sharedMesh.bounds;
                AddBox(m.transform, lb.center, lb.size);
            }
        }

        void AddBox(Transform t, Vector3 centre, Vector3 size)
        {
            Vector3 c = t.TransformPoint(centre);
            Vector3 ls = t.lossyScale;
            Vector3 fx = t.right, fz = t.forward;
            Vector2 ax = new Vector2(fx.x, fx.z), az = new Vector2(fz.x, fz.z);
            if (ax.sqrMagnitude < 1e-4f || az.sqrMagnitude < 1e-4f) { ax = Vector2.right; az = Vector2.up; }
            ax.Normalize(); az.Normalize();
            float hy = Mathf.Abs(size.y * ls.y) * 0.5f;
            AddObb(new Vector2(c.x, c.z), ax, az, Mathf.Abs(size.x * ls.x) * 0.5f, Mathf.Abs(size.z * ls.z) * 0.5f, c.y - hy, c.y + hy);
        }

        void AddObb(Vector2 c, Vector2 ax, Vector2 az, float hx, float hz, float y0, float y1)
        {
            int idx = obbs.Count;
            obbs.Add(new Obb { c = c, ax = ax, az = az, hx = hx, hz = hz, yMin = y0, yMax = y1 });
            float r = Mathf.Sqrt(hx * hx + hz * hz);
            int x0 = Mathf.FloorToInt((c.x - r) / Cell), x1 = Mathf.FloorToInt((c.x + r) / Cell);
            int z0 = Mathf.FloorToInt((c.y - r) / Cell), z1 = Mathf.FloorToInt((c.y + r) / Cell);
            if (x1 - x0 > 40 || z1 - z0 > 40) return;     // giant (bounds of a whole road mesh): ignore
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<int> l;
                    long k = Key(x, z);
                    if (!grid.TryGetValue(k, out l)) { l = new List<int>(); grid[k] = l; }
                    l.Add(idx);
                }
        }

        static bool SepAxis(Vector2 axis, Obb a, Obb b)
        {
            float ra = a.hx * Mathf.Abs(Vector2.Dot(a.ax, axis)) + a.hz * Mathf.Abs(Vector2.Dot(a.az, axis));
            float rb = b.hx * Mathf.Abs(Vector2.Dot(b.ax, axis)) + b.hz * Mathf.Abs(Vector2.Dot(b.az, axis));
            return Mathf.Abs(Vector2.Dot(b.c - a.c, axis)) > ra + rb;
        }

        static bool Overlap(Obb a, Obb b)
        {
            return !(SepAxis(a.ax, a, b) || SepAxis(a.az, a, b) || SepAxis(b.ax, a, b) || SepAxis(b.az, a, b));
        }

        readonly HashSet<int> seen = new HashSet<int>();

        /// <summary>True when the footprint (centre, half sizes, yaw) overlaps no indexed collider between y0 and y1.</summary>
        public bool FootprintFree(Vector2 c, float hx, float hz, float yaw, float y0, float y1)
        {
            float a = yaw * Mathf.Deg2Rad;
            var q = new Obb { c = c, ax = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)), az = new Vector2(Mathf.Sin(a), Mathf.Cos(a)), hx = hx, hz = hz };
            float r = Mathf.Sqrt(hx * hx + hz * hz);
            int x0 = Mathf.FloorToInt((c.x - r) / Cell), x1 = Mathf.FloorToInt((c.x + r) / Cell);
            int z0 = Mathf.FloorToInt((c.y - r) / Cell), z1 = Mathf.FloorToInt((c.y + r) / Cell);
            seen.Clear();
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    List<int> l;
                    if (!grid.TryGetValue(Key(x, z), out l)) continue;
                    for (int k = 0; k < l.Count; k++)
                    {
                        int i = l[k];
                        if (!seen.Add(i)) continue;
                        var o = obbs[i];
                        if (o.yMax < y0 || o.yMin > y1) continue;
                        if (Overlap(q, o)) return false;
                    }
                }
            return true;
        }

        public void Reserve(Vector2 c, float hx, float hz, float yaw, float y0, float y1)
        {
            float a = yaw * Mathf.Deg2Rad;
            AddObb(c, new Vector2(Mathf.Cos(a), -Mathf.Sin(a)), new Vector2(Mathf.Sin(a), Mathf.Cos(a)), hx, hz, y0, y1);
        }

        // ------------------------------------------------------------------ existing buildings
        /// <summary>
        /// The box collider under root whose size matches (sx, sy, sz; negative = any) within tol, closest to 'near'
        /// (and within maxDist of it). Returns null when none matches.
        /// </summary>
        public static BoxCollider FindBox(Transform root, Vector3 size, float tol, Vector2 near, float maxDist)
        {
            if (root == null) return null;
            BoxCollider best = null;
            float bd = maxDist * maxDist;
            foreach (var b in root.GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 s = b.size;
                if (size.x >= 0f && Mathf.Abs(s.x - size.x) > tol) continue;
                if (size.y >= 0f && Mathf.Abs(s.y - size.y) > tol) continue;
                if (size.z >= 0f && Mathf.Abs(s.z - size.z) > tol) continue;
                Vector3 p = b.transform.TransformPoint(b.center);
                float d = (new Vector2(p.x, p.z) - near).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        // ------------------------------------------------------------------ free ground
        public struct Site { public Vector2 c; public float yaw, floorY, minY; public Vector2 roadPoint; }

        static readonly Collider[] overlapBuf = new Collider[8];

        /// <summary>
        /// Spiral search around 'target' for a footprint (half sizes hx along the front, hz deep) whose FRONT (+Z)
        /// faces the nearest road at frontMin..frontMax metres from the road edge. Clear of colliders and roads,
        /// slope within maxSlope over the footprint, ground above minGround.
        /// </summary>
        public bool FreeSite(Vector2 target, float searchR, float hx, float hz, float frontMin, float frontMax, float maxSlope,
                             float minGround, Rect area, out Site site)
        {
            site = new Site();
            var field = world.Field;
            float bestScore = float.MaxValue;
            bool found = false;
            for (float r = 0f; r <= searchR; r += 4f)
            {
                int steps = r < 0.1f ? 1 : Mathf.Max(8, Mathf.CeilToInt(Mathf.PI * 2f * r / 5f));
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2f / steps + r * 0.13f;
                    Vector2 c = target + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!area.Contains(c)) continue;
                    RoadSample rs;
                    float d = world.NearestRoad(c, out rs, 80f);
                    if (d >= 80f) continue;
                    Vector2 toRoad = rs.pos - c;
                    if (toRoad.sqrMagnitude < 1e-3f) continue;
                    toRoad.Normalize();
                    float yaw = Mathf.Atan2(toRoad.x, toRoad.y) * Mathf.Rad2Deg;
                    // distance from the front face to the road edge
                    float front = d - rs.halfWidth - hz;
                    if (front < frontMin || front > frontMax) continue;
                    // footprint corners / edges clear of every road (with a margin)
                    Quaternion q = Quaternion.Euler(0f, yaw, 0f);
                    bool ok = true;
                    float mn = float.MaxValue, mx = float.MinValue;
                    for (int i = -1; i <= 1 && ok; i++)
                        for (int j = -1; j <= 1 && ok; j++)
                        {
                            Vector3 o = q * new Vector3(i * (hx + 1f), 0f, j * (hz + 1f));
                            Vector2 p = c + new Vector2(o.x, o.z);
                            if (field.EdgeDistance(p) < 2.5f) ok = false;
                            float h = world.TerrainHeight(p.x, p.y);
                            if (h < mn) mn = h;
                            if (h > mx) mx = h;
                        }
                    if (!ok || mx - mn > maxSlope || mn < minGround) continue;
                    if (!FootprintFree(c, hx + 1.2f, hz + 1.5f, yaw, mn - 1f, mx + 12f)) continue;
                    // other modules' live colliders (cars excluded)
                    int mask = ~((1 << WorldBuilder.LayerGrass) | (1 << CarExtras.CarLayer));
                    int hits = Physics.OverlapBoxNonAlloc(new Vector3(c.x, mx + 3f, c.y), new Vector3(hx + 1f, 2.6f, hz + 1f), overlapBuf, q, mask, QueryTriggerInteraction.Ignore);
                    if (hits > 0) continue;
                    float score = r + Mathf.Abs(front - (frontMin + frontMax) * 0.5f) * 0.8f + (mx - mn) * 4f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        site.c = c; site.yaw = yaw; site.floorY = mx + 0.18f; site.minY = mn; site.roadPoint = rs.pos;
                        found = true;
                    }
                }
                if (found && r > 12f) break;      // nearest ring with a hit (small lookahead for a better score)
            }
            return found;
        }
    }
}
