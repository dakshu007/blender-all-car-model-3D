// RoadStats.cs - road geometry checks for the terrain harness (NOT shipped): kinks, sample spacing, edge folds,
// cut / fill slope angles beside the road, bank, terrain poking through the road mesh on the real 4 m grid
// (and the 8 / 16 m LOD grids), and junction contacts (T / X / end-to-end) with their height mismatch.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BlossomRally;

public static class RoadStats
{
    const float Cell = 4f;
    static float gx0 = WorldLayout.MinX - WorldLayout.TerrainPad, gz0 = WorldLayout.MinZ - WorldLayout.TerrainPad;
    static readonly Dictionary<long, float> grid = new Dictionary<long, float>();
    static WorldField F;

    static float GridH(int i, int j)
    {
        long k = ((long)i << 32) ^ (uint)j;
        float h;
        if (!grid.TryGetValue(k, out h)) { h = F.HeightAt(gx0 + i * Cell, gz0 + j * Cell); grid[k] = h; }
        return h;
    }

    /// <summary>LOD vertex height (step = 1, 2, 4 cells) as WorldBuilder builds it (min of the fine block near roads).</summary>
    static float LodH(int i, int j, int step)
    {
        if (step == 1) return GridH(i, j);
        float h = GridH(i, j);
        if (WorldBuilderLodRule(i, j, step))
        {
            int r = step;
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++) h = Math.Min(h, GridH(i + di, j + dj));
        }
        return h;
    }

    static bool WorldBuilderLodRule(int i, int j, int step)
    {
        float e = F.EdgeDistance(new Vector2(gx0 + i * Cell, gz0 + j * Cell));
        return e < step * Cell + 2f;
    }

    /// <summary>Height of the terrain mesh (same triangulation as WorldBuilder.TerrainHeight) on a grid of step cells.</summary>
    public static float MeshH(float x, float z, int step)
    {
        float c = Cell * step;
        float fx = (x - gx0) / c, fz = (z - gz0) / c;
        int i = (int)Math.Floor(fx), j = (int)Math.Floor(fz);
        float u = fx - i, w = fz - j;
        float ha = LodH(i * step, j * step, step), hb = LodH((i + 1) * step, j * step, step);
        float hc = LodH(i * step, (j + 1) * step, step), hd = LodH((i + 1) * step, (j + 1) * step, step);
        if (u + w <= 1f) return ha + (hb - ha) * u + (hc - ha) * w;
        return hd + (hc - hd) * (1f - u) + (hb - hd) * (1f - w);
    }

    static float SlopeDeg(float dh, float run) { return (float)(Math.Atan2(Math.Abs(dh), run) * 180.0 / Math.PI); }

    public static void Run(WorldField field, string only)
    {
        F = field;
        Console.WriteLine("\nroad geometry                          kink°  dK/m   spacing    folds  cutSlope°  >45°made  bank%  poke4(cm) poke8 poke16  gap(cm)");
        int totalMade = 0, totalFolds = 0; float worstPoke = -1f, worstKink = 0f, worstSlope = 0f;
        for (int r = 0; r < field.roads.Count; r++)
        {
            var def = field.roads[r];
            if (only != null && def.region.ToString() != only) continue;
            var L = field.roadSamples[r];
            int n = L.Count;
            bool closed = def.closed;
            float maxTurn = 0f, maxDK = 0f, spMin = 1e9f, spMax = 0f, maxBank = 0f;
            int folds = 0;
            float prevK = float.NaN;
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i == 0 || i == n - 1)) continue;
                Vector2 a = L[(i - 1 + n) % n].pos, b = L[i].pos, c = L[(i + 1) % n].pos;
                Vector2 d0 = b - a, d1 = c - b;
                float l0 = d0.magnitude, l1 = d1.magnitude;
                if (i > 0 || closed) { spMin = Math.Min(spMin, l0); spMax = Math.Max(spMax, l0); }
                if (l0 < 1e-4f || l1 < 1e-4f) continue;
                float cr = (d0.x * d1.y - d0.y * d1.x) / (l0 * l1), dt = Vector2.Dot(d0, d1) / (l0 * l1);
                float turn = (float)Math.Atan2(cr, dt);
                maxTurn = Math.Max(maxTurn, Math.Abs(turn) * 57.2958f);
                float k = turn / (0.5f * (l0 + l1));
                if (!float.IsNaN(prevK)) maxDK = Math.Max(maxDK, Math.Abs(k - prevK));
                prevK = k;
                maxBank = Math.Max(maxBank, Math.Abs(L[i].bank));
            }
            // edge folds: an edge 1 m outside the road running backwards
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 prev = Vector2.zero;
                for (int i = 0; i < n; i++)
                {
                    var s = L[i];
                    Vector2 p = s.pos + s.Right * side * (s.halfWidth + 1f);
                    if (i > 0 && Vector2.Dot(p - prev, s.tangent) < 0.02f) folds++;
                    prev = p;
                }
            }
            // cut / fill faces and poke-through
            float maxSlope = 0f, poke4 = -9f, poke8 = -9f, poke16 = -9f, gap = 0f;
            int made = 0; Vector2 pokeAt = Vector2.zero; float pokeLat = 0f;
            for (int i = 0; i < n; i += 3)
            {
                var s = L[i];
                if (s.bridge) continue;
                Vector2 rt = s.Right;
                for (int side = -1; side <= 1; side += 2)
                {
                    float hp = F.HeightAt(s.pos.x + rt.x * side * s.halfWidth, s.pos.y + rt.y * side * s.halfWidth);
                    float np = F.BaseHeight(s.pos.x + rt.x * side * s.halfWidth, s.pos.y + rt.y * side * s.halfWidth);
                    for (float e = 2f; e <= 26f; e += 2f)
                    {
                        Vector2 q = s.pos + rt * side * (s.halfWidth + e);
                        RoadSample o;
                        float dq = F.NearestRoad(q, out o, 44f);
                        if (o.road != r || Math.Abs(o.distance - s.distance) > 30f) break;
                        float hq = F.HeightAt(q.x, q.y), nq = F.BaseHeight(q.x, q.y);
                        float sl = SlopeDeg(hq - hp, 2f), nsl = SlopeDeg(nq - np, 2f);
                        if (sl > maxSlope) maxSlope = sl;
                        if (sl > 45f && nsl < 40f)
                        {
                            made++;
                            if (Environment.GetEnvironmentVariable("DEBUGROAD") == def.name && made < 6)
                            {
                                Console.WriteLine($"   made@{q} e={e} side={side} s={s.distance:F0} road h={s.height:F1}");
                                for (float ee = 0; ee <= 30; ee += 2f) { Vector2 qq = s.pos + rt * side * (s.halfWidth + ee); Console.WriteLine($"      e{ee,3}: final {F.HeightAt(qq.x, qq.y),7:F2} base {F.BaseHeight(qq.x, qq.y),7:F2}"); }
                            }
                        }
                        hp = hq; np = nq;
                    }
                }
                // poke: terrain mesh above the road surface (at the edges, quarter points, centre)
                for (int k = -4; k <= 4; k++)
                {
                    float lat = s.halfWidth * k / 4f;
                    Vector2 q = s.pos + rt * lat;
                    RoadSample o;
                    float dO = F.NearestRoad(q, out o, 20f);
                    if (o.road != r && dO < o.halfWidth + 0.3f) continue;     // inside another road (clipped there)
                    if (o.bridge) continue;
                    float surf = s.SurfaceAt(lat);
                    float pk = MeshH(q.x, q.y, 1) - surf;
                    if (pk > poke4) { poke4 = pk; pokeAt = q; pokeLat = lat; }
                    if (i % 6 == 0)
                    {
                        poke8 = Math.Max(poke8, MeshH(q.x, q.y, 2) - surf);
                        poke16 = Math.Max(poke16, MeshH(q.x, q.y, 4) - surf);
                    }
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 q = s.pos + rt * side * (s.halfWidth + 0.4f);
                    gap = Math.Max(gap, s.SurfaceAt(side * s.halfWidth) - MeshH(q.x, q.y, 1));
                }
            }
            if (poke4 > 0.02f) Console.WriteLine($"   poke {poke4 * 100:F0} cm at {pokeAt} lat {pokeLat:F1}");
            // layout shift: how far the final centreline moved from the designer's Catmull-Rom line
            var orig = Spline.Sample(def.points, def.closed, 1f);
            float shift = 0f;
            for (int i = 0; i < n; i += 2)
            {
                float bd = 1e9f;
                for (int j = 0; j < orig.Count; j++) bd = Math.Min(bd, (orig[j] - L[i].pos).sqrMagnitude);
                shift = Math.Max(shift, (float)Math.Sqrt(bd));
            }
            if (shift > 1.5f) Console.WriteLine($"   layout shift {shift:F1} m");
            totalMade += made; totalFolds += folds;
            worstPoke = Math.Max(worstPoke, poke4); worstKink = Math.Max(worstKink, maxTurn); worstSlope = Math.Max(worstSlope, maxSlope);
            Console.WriteLine($"{def.name,-38} {maxTurn,5:F1} {maxDK * 1000,6:F1} {spMin,4:F2}..{spMax,4:F2} {folds,5}  {maxSlope,8:F1}  {made,7}  {maxBank * 100,5:F1}  {poke4 * 100,7:F0} {poke8 * 100,6:F0} {poke16 * 100,6:F0}  {gap * 100,6:F0}");
        }
        Console.WriteLine($"TOTAL: road-made steep faces (>45° where nature <40°): {totalMade}, edge folds: {totalFolds}, worst kink {worstKink:F1}°, worst cut slope {worstSlope:F1}°, worst poke {worstPoke * 100:F0} cm");
        Junctions(field, only);
    }

    // ------------------------------------------------------------------ junction contacts (independent of WorldField's own list)
    static void Junctions(WorldField field, string only)
    {
        Console.WriteLine("\njunction contacts (slave -> master): type  angle°  dH(cm) = slave edge vs master surface at the mouth");
        int count = 0; float worst = 0f;
        var lines = new List<string>();
        for (int r = 0; r < field.roads.Count; r++)
        {
            var L = field.roadSamples[r];
            if (field.roads[r].closed) continue;
            for (int end = 0; end < 2; end++)
            {
                var e = L[end == 0 ? 0 : L.Count - 1];
                var inner = L[end == 0 ? Math.Min(L.Count - 1, 3) : Math.Max(0, L.Count - 4)];
                for (int o = 0; o < field.roads.Count; o++)
                {
                    if (o == r) continue;
                    var M = field.roadSamples[o];
                    int bi = -1; float bd = 1e9f;
                    for (int j = 0; j < M.Count; j++) { float d = (M[j].pos - e.pos).sqrMagnitude; if (d < bd) { bd = d; bi = j; } }
                    bd = (float)Math.Sqrt(bd);
                    if (bd > M[bi].halfWidth + 3f) continue;
                    if (only != null && field.roads[r].region.ToString() != only && field.roads[o].region.ToString() != only) continue;
                    var m = M[bi];
                    Vector2 dir = (inner.pos - e.pos).normalized;
                    float ang = (float)(Math.Acos(Math.Min(1f, Math.Abs(Vector2.Dot(dir, m.tangent)))) * 57.2958);
                    // the slave's surface where its centreline crosses the master's edge vs the master's surface there
                    float worstHere = 0f;
                    for (int j = 0; j < L.Count; j++)
                    {
                        int jj = end == 0 ? j : L.Count - 1 - j;
                        var s = L[jj];
                        RoadSample mm; float dm = NearestOn(field, o, s.pos, out mm);
                        if (dm < mm.halfWidth) continue;
                        // first slave sample outside the master: compare along the slave's cross-section
                        for (int k = -1; k <= 1; k++)
                        {
                            Vector2 q = s.pos + s.Right * (k * s.halfWidth * 0.9f);
                            RoadSample m2; float d2 = NearestOn(field, o, q, out m2);
                            float lat = Vector2.Dot(q - m2.pos, m2.Right);
                            float ms = m2.SurfaceAt(Mathf.Clamp(lat, -m2.halfWidth, m2.halfWidth));
                            worstHere = Math.Max(worstHere, Math.Abs(s.SurfaceAt(k * s.halfWidth * 0.9f) - ms));
                        }
                        break;
                    }
                    bool e2e = (M[bi].distance < 3f || M[bi].distance > M[M.Count - 1].distance - 3f) && !field.roads[o].closed;
                    count++; worst = Math.Max(worst, worstHere);
                    lines.Add($"  {field.roads[r].name,-30} -> {field.roads[o].name,-30} {(e2e ? "end" : "T  ")}  {ang,4:F0}  {worstHere * 100,6:F0}  endOff {bd:F1} m");
                }
            }
        }
        // crossings (centrelines intersect away from the ends)
        int xs = 0;
        for (int r = 0; r < field.roads.Count; r++)
            for (int o = r + 1; o < field.roads.Count; o++)
            {
                var A = field.roadSamples[r]; var B = field.roadSamples[o];
                if (only != null && field.roads[r].region.ToString() != only && field.roads[o].region.ToString() != only) continue;
                for (int i = 0; i + 1 < A.Count; i++)
                    for (int j = 0; j + 1 < B.Count; j++)
                    {
                        if ((A[i].pos - B[j].pos).sqrMagnitude > 16f) continue;
                        Vector2 hit;
                        if (!Spline.SegmentIntersect(A[i].pos, A[i + 1].pos, B[j].pos, B[j + 1].pos, out hit)) continue;
                        bool nearEnd = (!field.roads[r].closed && (A[i].distance < 6f || A[i].distance > A[A.Count - 1].distance - 6f))
                                    || (!field.roads[o].closed && (B[j].distance < 6f || B[j].distance > B[B.Count - 1].distance - 6f));
                        if (nearEnd) continue;
                        xs++;
                        float dh = Math.Abs(A[i].height - B[j].height);
                        lines.Add($"  X {field.roads[r].name,-28} x {field.roads[o].name,-28} dH {dh * 100,6:F0} cm{(A[i].bridge || B[j].bridge ? " (bridge)" : "")}");
                    }
            }
        foreach (var l in lines) Console.WriteLine(l);
        Console.WriteLine("WorldField junctions (slave <- master):");
        foreach (var j in field.junctions)
            if (only == null || field.roads[j.slave].region.ToString() == only || field.roads[j.master].region.ToString() == only)
                Console.WriteLine($"  {field.roads[j.slave].name,-30} <- {field.roads[j.master].name,-30} {(j.endToEnd ? "end-to-end" : "T")} slaveEnd {j.slaveEnd} mouth {j.mouth}");
        Console.WriteLine($"junction ends: {count}, crossings: {xs}, worst mouth mismatch {worst * 100:F0} cm");
    }

    static float NearestOn(WorldField field, int road, Vector2 p, out RoadSample best)
    {
        var M = field.roadSamples[road];
        float bd = 1e9f; best = M[0]; int bi = 0;
        for (int j = 0; j < M.Count; j++) { float d = (M[j].pos - p).sqrMagnitude; if (d < bd) { bd = d; bi = j; } }
        best = M[bi];
        // project on the neighbouring segment for a precise distance
        float dist = (float)Math.Sqrt(bd);
        for (int k = -1; k <= 0; k++)
        {
            int a = bi + k, b = a + 1;
            if (a < 0 || b >= M.Count) continue;
            float t; float d = Spline.DistToSegment(p, M[a].pos, M[b].pos, out t);
            if (d < dist)
            {
                dist = d;
                var s = M[a]; s.pos = Vector2.Lerp(M[a].pos, M[b].pos, t); s.height = Mathf.Lerp(M[a].height, M[b].height, t); best = s;
            }
        }
        return dist;
    }
}
