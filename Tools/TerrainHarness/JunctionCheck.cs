// JunctionCheck.cs - builds the real road meshes (RoadMesh.cs) and checks every junction (NOT shipped):
//   overlap = area covered by two road surfaces at once (z-fighting), gap = road area covered by neither,
//   seam = height step between the joining road's cut edge and the through road's surface.
// Writes out/junction_<n>.ppm crops (slave red, master blue, overlap magenta, gap yellow, lines white).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using BlossomRally;

public static class JunctionCheck
{
    struct Tri { public Vector3 a, b, c; public Vector2 mn, mx; public int road; }

    public static void Run(WorldField field, string only, string outDir)
    {
        var builder = new RoadMeshBuilder(field, (x, z) => RoadStats.MeshH(x, z, 1));
        var tris = new List<Tri>();
        var lineTris = new List<Tri>();
        int clipped = 0;
        for (int r = 0; r < field.roads.Count; r++)
        {
            var def = field.roads[r];
            var list = builder.WithExtensions(r);
            int count = list.Count + (def.closed ? 1 : 0);
            var run = new List<RoadSample>();
            for (int i = 0; i < count; i++)
            {
                if (run.Count >= 240) { Add(builder.Piece(r, run, 7f, def.surface == Surface.Asphalt && def.lines), r, tris, lineTris, ref clipped); var last = run[run.Count - 1]; run.Clear(); run.Add(last); }
                run.Add(list[i % list.Count]);
            }
            if (run.Count > 1) Add(builder.Piece(r, run, 7f, def.surface == Surface.Asphalt && def.lines), r, tris, lineTris, ref clipped);
        }
        foreach (var j in field.junctions) if (j.endToEnd) { var c = builder.Cap(j); if (c != null) Add(c, j.master, tris, lineTris, ref clipped); }
        // spatial hash of top triangles
        var grid = new Dictionary<long, List<int>>();
        for (int i = 0; i < tris.Count; i++)
            for (int x = (int)Math.Floor(tris[i].mn.x / 8f); x <= (int)Math.Floor(tris[i].mx.x / 8f); x++)
                for (int z = (int)Math.Floor(tris[i].mn.y / 8f); z <= (int)Math.Floor(tris[i].mx.y / 8f); z++)
                {
                    long k = ((long)x << 32) ^ (uint)z; List<int> l;
                    if (!grid.TryGetValue(k, out l)) { l = new List<int>(); grid[k] = l; }
                    l.Add(i);
                }
        var lgrid = new Dictionary<long, List<int>>();
        for (int i = 0; i < lineTris.Count; i++)
        {
            long k = ((long)(int)Math.Floor(lineTris[i].mn.x / 8f) << 32) ^ (uint)(int)Math.Floor(lineTris[i].mn.y / 8f); List<int> l;
            if (!lgrid.TryGetValue(k, out l)) { l = new List<int>(); lgrid[k] = l; }
            l.Add(i);
        }
        Console.WriteLine($"\njunction meshes ({clipped} clipped triangles): overlap(m2)  gap(m2)  seam(cm)  lines-in-other-road");
        float worstOv = 0, worstGap = 0, worstSeam = 0; int img = 0, totalLineX = 0;
        var hits = new List<int>();
        foreach (var j in field.junctions)
        {
            if (only != null && field.roads[j.slave].region.ToString() != only) continue;
            const float R = 16f, step = 0.2f;
            int N = (int)(2 * R / step);
            float ov = 0, gap = 0, seam = 0;
            int lineX = 0;
            var pix = new byte[N * N * 3];
            for (int a = 0; a < N; a++)
                for (int b = 0; b < N; b++)
                {
                    Vector2 p = j.point + new Vector2(-R + a * step + 0.037f, -R + b * step + 0.051f);
                    hits.Clear();
                    float hs = float.NaN, hm = float.NaN;
                    List<int> l;
                    if (grid.TryGetValue(((long)(int)Math.Floor(p.x / 8f) << 32) ^ (uint)(int)Math.Floor(p.y / 8f), out l))
                        foreach (int ti in l)
                        {
                            float y;
                            if (!Inside(tris[ti], p, out y)) continue;
                            hits.Add(tris[ti].road);
                            if (tris[ti].road == j.slave) hs = y; else if (tris[ti].road == j.master) hm = y;
                        }
                    bool S = !float.IsNaN(hs), M = !float.IsNaN(hm);
                    // intended road area: inside the master, or inside the slave's band and not past its end
                    RoadSample ms, ss;
                    float dm = field.NearestOnRoad(j.master, p, out ms), ds = field.NearestOnRoad(j.slave, p, out ss);
                    bool inM = dm < ms.halfWidth - 0.15f && !PastEnd(field, j.master, ms, p);
                    bool inS = ds < ss.halfWidth - 0.15f && !PastEnd(field, j.slave, ss, p);
                    bool other = hits.Exists(h => h != j.slave && h != j.master);
                    Color c = new Color(0.2f, 0.4f, 0.2f);
                    if (S && M) { ov += step * step; c = new Color(1f, 0f, 1f); }
                    else if (S) c = new Color(0.8f, 0.25f, 0.2f);
                    else if (M) c = new Color(0.2f, 0.35f, 0.85f);
                    else if ((inM || inS) && !other) { gap += step * step; c = new Color(1f, 0.9f, 0f); }
                    if (other && !S && !M) c = new Color(0.5f, 0.5f, 0.5f);
                    // lane lines of either road over the other road's surface
                    if (lgrid.TryGetValue(((long)(int)Math.Floor(p.x / 8f) << 32) ^ (uint)(int)Math.Floor(p.y / 8f), out l))
                        foreach (int ti in l)
                        {
                            float y;
                            if (!Inside(lineTris[ti], p, out y)) continue;
                            c = Color.white;
                            if ((lineTris[ti].road == j.slave && M && !S)) lineX++;
                        }
                    int o = ((N - 1 - b) * N + a) * 3;
                    pix[o] = (byte)(c.r * 255); pix[o + 1] = (byte)(c.g * 255); pix[o + 2] = (byte)(c.b * 255);
                }
            // seam: the slave's top surface just outside the master vs the master's surface just inside
            var L = field.roadSamples[j.slave];
            for (int k = 0; k < L.Count && k < 30; k++)
            {
                var s = L[j.slaveEnd == 0 ? k : L.Count - 1 - k];
                for (int q = -3; q <= 3; q++)
                {
                    Vector2 p = s.pos + s.Right * (q * s.halfWidth / 3.5f);
                    RoadSample m; float dm = field.NearestOnRoad(j.master, p, out m);
                    if (Math.Abs(dm - m.halfWidth) > 0.12f) continue;
                    Vector2 n = (p - m.pos).normalized;
                    float ys = TopAt(tris, grid, p + n * 0.08f, j.slave), ym = TopAt(tris, grid, p - n * 0.08f, j.master);
                    if (float.IsNaN(ys) || float.IsNaN(ym)) continue;
                    seam = Math.Max(seam, Math.Abs(ys - ym));
                }
            }
            totalLineX += lineX;
            worstOv = Math.Max(worstOv, ov); worstGap = Math.Max(worstGap, gap); worstSeam = Math.Max(worstSeam, seam);
            string tag = $"{field.roads[j.slave].name} <- {field.roads[j.master].name}";
            Console.WriteLine($"  {tag,-64} {ov,7:F2} {gap,8:F2} {seam * 100,7:F1}  {lineX}");
            if (Environment.GetEnvironmentVariable("JUNCTION_IMAGES") != null || ov > 0.5f || gap > 0.5f)
                using (var f = File.Create(Path.Combine(outDir, $"junction_{img++}.ppm")))
                {
                    var hdr = System.Text.Encoding.ASCII.GetBytes($"P6\n{N} {N}\n255\n");
                    f.Write(hdr, 0, hdr.Length); f.Write(pix, 0, pix.Length);
                    Console.WriteLine($"    -> junction_{img - 1}.png");
                }
        }
        Console.WriteLine($"JUNCTIONS: worst overlap {worstOv:F2} m2, worst gap {worstGap:F2} m2, worst seam {worstSeam * 100:F1} cm, line samples over the other road {totalLineX}");
    }

    static bool PastEnd(WorldField f, int road, RoadSample s, Vector2 p)
    {
        if (f.roads[road].closed) return false;
        var L = f.roadSamples[road];
        if (s.distance < 0.05f) return Vector2.Dot(p - s.pos, -s.tangent) > 0f;
        if (s.distance > L[L.Count - 1].distance - 0.05f) return Vector2.Dot(p - s.pos, s.tangent) > 0f;
        return false;
    }

    static float TopAt(List<Tri> tris, Dictionary<long, List<int>> grid, Vector2 p, int road)
    {
        List<int> l;
        if (!grid.TryGetValue(((long)(int)Math.Floor(p.x / 8f) << 32) ^ (uint)(int)Math.Floor(p.y / 8f), out l)) return float.NaN;
        foreach (int ti in l) { float y; if (tris[ti].road == road && Inside(tris[ti], p, out y)) return y; }
        return float.NaN;
    }

    static void Add(RoadMeshData d, int road, List<Tri> tris, List<Tri> lines, ref int clipped)
    {
        clipped += d.clipped;
        for (int i = 0; i < d.ct.Count; i += 3) tris.Add(Make(d.cv[d.ct[i]], d.cv[d.ct[i + 1]], d.cv[d.ct[i + 2]], road));
        for (int i = 0; i < d.lt.Count; i += 3) lines.Add(Make(d.lv[d.lt[i]], d.lv[d.lt[i + 1]], d.lv[d.lt[i + 2]], road));
    }

    static Tri Make(Vector3 a, Vector3 b, Vector3 c, int road)
    {
        var t = new Tri { a = a, b = b, c = c, road = road };
        t.mn = new Vector2(Math.Min(a.x, Math.Min(b.x, c.x)), Math.Min(a.z, Math.Min(b.z, c.z)));
        t.mx = new Vector2(Math.Max(a.x, Math.Max(b.x, c.x)), Math.Max(a.z, Math.Max(b.z, c.z)));
        return t;
    }

    static bool Inside(Tri t, Vector2 p, out float y)
    {
        y = 0;
        if (p.x < t.mn.x || p.x > t.mx.x || p.y < t.mn.y || p.y > t.mx.y) return false;
        float x1 = t.a.x, z1 = t.a.z, x2 = t.b.x, z2 = t.b.z, x3 = t.c.x, z3 = t.c.z;
        float den = (z2 - z3) * (x1 - x3) + (x3 - x2) * (z1 - z3);
        if (Math.Abs(den) < 1e-9f) return false;
        float l1 = ((z2 - z3) * (p.x - x3) + (x3 - x2) * (p.y - z3)) / den;
        float l2 = ((z3 - z1) * (p.x - x3) + (x1 - x3) * (p.y - z3)) / den;
        float l3 = 1 - l1 - l2;
        if (l1 < 0 || l2 < 0 || l3 < 0) return false;
        y = l1 * t.a.y + l2 * t.b.y + l3 * t.c.y;
        return true;
    }
}
