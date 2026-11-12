// Harness.cs - offline terrain / road check for the six-region world (NOT shipped in the game).
// Compiles the game's pure world code (WorldField, WorldLayout, regions' *.cs except *.Build.cs) against a
// tiny UnityEngine math stub and renders a shaded top-down map plus road statistics.
//   ./run.sh                      whole world, 4 m per pixel  -> out/world.png
//   ./run.sh --region City        one region (+150 m margin), 2 m per pixel -> out/City.png
//   ./run.sh --profile x0 z0 x1 z1   height profile along a line
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BlossomRally;

public static class Harness
{
    public static int Main(string[] args)
    {
        string only = null; float mpp = 4f; string outDir = "out";
        float[] prof = null; bool noStats = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--region") { only = args[++i]; mpp = 2f; }
            else if (args[i] == "--mpp") mpp = float.Parse(args[++i]);
            else if (args[i] == "--out") outDir = args[++i];
            else if (args[i] == "--nostats") noStats = true;
            else if (args[i] == "--profile") { prof = new float[4]; for (int k = 0; k < 4; k++) prof[k] = float.Parse(args[++i]); }
        }
        Directory.CreateDirectory(outDir);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var regions = WorldRegions.Create();
        var field = new WorldField(regions);
        field.TraceRiver();
        field.BuildRoadSamples();
        field.PlanRegions();
        Console.WriteLine($"roads sampled: {field.roads.Count} roads, {field.allSamples.Count} samples ({sw.ElapsedMilliseconds} ms)");

        // ---------------- stages
        var stages = new List<StageDef>();
        foreach (var r in regions) if (r.Id != RegionId.Valley) r.AddStages(stages);
        foreach (var s in stages)
            Console.WriteLine($"stage {s.name}: road '{s.roadName}' -> {(field.RoadIndex(s.roadName) >= 0 ? "OK" : "MISSING ROAD")}");

        // ---------------- road report
        Console.WriteLine("\nroad                                  region     surf    len(m)  maxGrade  maxCut  maxFill  minR(m)  hMin..hMax");
        bool bad = false;
        for (int r = 0; r < field.roads.Count; r++)
        {
            var def = field.roads[r];
            if (only != null && def.region.ToString() != only && !(def.connector && (WorldLayout.Connectors.Any(c => c.name == def.name && (c.a.ToString() == only || c.b.ToString() == only))))) continue;
            var list = field.roadSamples[r];
            float maxG = 0, maxCut = 0, maxFill = 0, minR = 1e9f, hMin = 1e9f, hMax = -1e9f; Vector2 gAt = Vector2.zero, cutAt = Vector2.zero;
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var s = list[i];
                hMin = Math.Min(hMin, s.height); hMax = Math.Max(hMax, s.height);
                if (i > 0) { float d = Vector2.Distance(s.pos, list[i - 1].pos); if (d > 0.01f) { float g = Math.Abs(s.height - list[i - 1].height) / d; if (g > maxG) { maxG = g; gAt = s.pos; } } }
                if (!s.bridge && !def.fixedHeight)
                {
                    float nat = field.BaseHeight(s.pos.x, s.pos.y);
                    if (nat - s.height > maxCut) { maxCut = nat - s.height; cutAt = s.pos; }
                    maxFill = Math.Max(maxFill, s.height - nat);
                }
                if (i > 2 && i < n - 3)
                {
                    Vector2 a = list[i - 3].pos, b = s.pos, c = list[i + 3].pos;
                    float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
                    float area2 = Math.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x));
                    if (area2 > 1e-4f) minR = Math.Min(minR, ab * bc * ca / (2 * area2));
                }
                if (!WorldLayout.InPlayable(s.pos, 6f)) { if (!bad) Console.WriteLine($"  !! {def.name} leaves the playable area at {s.pos}"); bad = true; }
            }
            Console.WriteLine($"{def.name,-38} {def.region,-10} {def.surface,-7} {list[n - 1].distance,7:F0}  {maxG * 100,6:F1}%  {maxCut,6:F1}  {maxFill,6:F1}  {minR,7:F1}  {hMin,6:F1}..{hMax,6:F1}" + (maxG > 0.12f || maxCut > 6f ? $"   [grade@{gAt} cut@{cutAt}]" : ""));
        }

        if (!noStats) RoadStats.Run(field, only);

        if (prof != null)
        {
            Console.WriteLine("\nprofile:");
            for (int k = 0; k <= 40; k++)
            {
                float t = k / 40f;
                float x = prof[0] + (prof[2] - prof[0]) * t, z = prof[1] + (prof[3] - prof[1]) * t;
                Console.WriteLine($"  ({x,7:F0},{z,7:F0})  base {field.BaseHeight(x, z),7:F1}  final {field.HeightAt(x, z),7:F1}  region {WorldLayout.At(new Vector2(x, z))}");
            }
            return 0;
        }

        // ---------------- render
        float x0 = WorldLayout.MinX - WorldLayout.TerrainPad, z0 = WorldLayout.MinZ - WorldLayout.TerrainPad;
        float x1 = WorldLayout.MaxX + WorldLayout.TerrainPad, z1 = WorldLayout.MaxZ + WorldLayout.TerrainPad;
        if (only != null)
        {
            var id = (RegionId)Enum.Parse(typeof(RegionId), only);
            var rc = WorldLayout.RectOf(id);
            x0 = rc.xMin - 150; z0 = rc.yMin - 150; x1 = rc.xMax + 150; z1 = rc.yMax + 150;
        }
        int W = (int)((x1 - x0) / mpp) + 1, H = (int)((z1 - z0) / mpp) + 1;
        var hgt = new float[W * H];
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                hgt[j * W + i] = field.HeightAt(x0 + i * mpp, z0 + j * mpp);
        Console.WriteLine($"\nheights {W}x{H} in {sw.ElapsedMilliseconds} ms");
        var img = new byte[W * H * 3];
        var stats = new Dictionary<RegionId, List<float>>();
        int steep = 0;
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float h = hgt[j * W + i];
                float hl = hgt[j * W + Math.Max(0, i - 1)], hr = hgt[j * W + Math.Min(W - 1, i + 1)];
                float hd = hgt[Math.Max(0, j - 1) * W + i], hu = hgt[Math.Min(H - 1, j + 1) * W + i];
                var nrm = new Vector3(hl - hr, 2f * mpp, hd - hu).normalized;
                float x = x0 + i * mpp, z = z0 + j * mpp;
                if (nrm.y < 0.57f && WorldLayout.InPlayable(new Vector2(x, z))) steep++;
                Color c = field.GroundColor(x, z, h, nrm);
                var light = new Vector3(-0.5f, 0.75f, 0.45f).normalized;
                float sh = 0.55f + 0.6f * Math.Max(0f, Vector3.Dot(nrm, light));
                c = c * sh;
                var at = WorldLayout.At(new Vector2(x, z));
                bool wet = at != RegionId.Valley ? h < WorldField.WaterY - 0.05f
                         : (h < WorldField.WaterY - 0.05f && (field.RiverDistance(new Vector2(x, z)) < 17f || MapData.LakeK(new Vector2(x, z)) < 1.4f));
                if (wet) c = Color.Lerp(c, new Color(0.25f, 0.55f, 0.70f), 0.75f);
                var id = WorldLayout.At(new Vector2(x, z));
                if (!stats.ContainsKey(id)) stats[id] = new List<float>();
                if (WorldLayout.InPlayable(new Vector2(x, z)) && (i + j) % 3 == 0) stats[id].Add(h);
                // contour lines every 10 m
                if (Math.Floor(h / 10f) != Math.Floor(hl / 10f) || Math.Floor(h / 10f) != Math.Floor(hd / 10f)) c = c * 0.85f;
                // tile borders
                if (Math.Abs(((x + 500) % 1000 + 1000) % 1000) < mpp && z > WorldLayout.MinZ && z < WorldLayout.MaxZ) c = Color.Lerp(c, Color.white, 0.35f);
                if (Math.Abs(((z + 500) % 1000 + 1000) % 1000) < mpp && x > WorldLayout.MinX && x < WorldLayout.MaxX) c = Color.Lerp(c, Color.white, 0.35f);
                int o = ((H - 1 - j) * W + i) * 3;
                img[o] = (byte)(Mathf.Clamp01(c.r) * 255); img[o + 1] = (byte)(Mathf.Clamp01(c.g) * 255); img[o + 2] = (byte)(Mathf.Clamp01(c.b) * 255);
            }
        // roads
        foreach (var s in field.allSamples)
        {
            var def = field.roads[s.road];
            Color rc = def.surface == Surface.Asphalt ? new Color(0.22f, 0.22f, 0.25f) : def.surface == Surface.Gravel ? new Color(0.70f, 0.64f, 0.52f)
                     : def.surface == Surface.Dirt ? new Color(0.55f, 0.40f, 0.28f) : def.surface == Surface.Snow ? new Color(0.93f, 0.95f, 1f)
                     : def.surface == Surface.Ice ? new Color(0.70f, 0.85f, 0.95f) : def.surface == Surface.Sand ? new Color(0.88f, 0.70f, 0.48f) : new Color(0.75f, 0.70f, 0.62f);
            if (s.bridge) rc = new Color(0.85f, 0.80f, 0.70f);
            if (def.connector) rc = Color.Lerp(rc, new Color(1f, 0.3f, 0.5f), 0.45f);
            float rad = s.halfWidth / mpp;
            int ci = (int)((s.pos.x - x0) / mpp), cj = (int)((s.pos.y - z0) / mpp);
            int R = (int)Math.Ceiling(rad);
            for (int dj = -R; dj <= R; dj++)
                for (int di = -R; di <= R; di++)
                {
                    if (di * di + dj * dj > rad * rad + 0.5f) continue;
                    int ii = ci + di, jj = cj + dj;
                    if (ii < 0 || jj < 0 || ii >= W || jj >= H) continue;
                    int o = ((H - 1 - jj) * W + ii) * 3;
                    img[o] = (byte)(rc.r * 255); img[o + 1] = (byte)(rc.g * 255); img[o + 2] = (byte)(rc.b * 255);
                }
        }
        string name = only ?? "world";
        using (var f = File.Create(Path.Combine(outDir, name + ".ppm")))
        {
            var hdr = System.Text.Encoding.ASCII.GetBytes($"P6\n{W} {H}\n255\n");
            f.Write(hdr, 0, hdr.Length);
            f.Write(img, 0, img.Length);
        }
        Console.WriteLine("\nregion        hMin    hMean   hMax");
        foreach (var kv in stats.OrderBy(k => (int)k.Key))
            if (kv.Value.Count > 0) Console.WriteLine($"{kv.Key,-10} {kv.Value.Min(),7:F1} {kv.Value.Average(),7:F1} {kv.Value.Max(),7:F1}");
        Console.WriteLine($"steep (>55 deg) playable pixels: {steep}");
        Console.WriteLine($"wrote {outDir}/{name}.ppm ({W}x{H}) in {sw.ElapsedMilliseconds} ms");
        return 0;
    }
}
