// JuiceWorld.cs - cheap spatial lookups for the juice layer: a flat grid of cherry trees, bamboo
// clumps and tall grass clumps (built once from World_Static / World_Grass after the valley is built),
// and a CPU copy of the road shader's puddle noise so splashes happen where puddles are drawn.
using UnityEngine;

namespace BlossomRally
{
    internal static class JuiceWorld
    {
        public const byte KindCherry = 0, KindBamboo = 1, KindGrass = 2;
        const float Cell = 16f, Origin = -704f;
        const int Dim = 88;                      // 88 * 16 m = 1408 m, covers the +-640 m valley

        static float[] ix, iz, ir;
        static byte[] ik;
        static int[] cellStart, cellItems;
        public static bool Ready { get; private set; }

        /// <summary>Collect vegetation positions (call once after the world is built).</summary>
        public static void Build()
        {
            Ready = false;
            var xs = new System.Collections.Generic.List<Vector4>(4096);
            var stat = GameObject.Find("World_Static");
            if (stat != null)
            {
                Transform rt = stat.transform;
                for (int i = 0; i < rt.childCount; i++)
                {
                    Transform ch = rt.GetChild(i);
                    string n = ch.name;
                    float s = ch.localScale.x;
                    if (n.StartsWith("ENV_Cherry"))
                    {
                        float r = n == "ENV_CherryKing" ? 7.5f : n == "ENV_CherryTree" ? 5.5f : 3.8f;
                        xs.Add(new Vector4(ch.position.x, ch.position.z, r * s, KindCherry));
                    }
                    else if (n == "ENV_Bamboo") xs.Add(new Vector4(ch.position.x, ch.position.z, 2.2f * s, KindBamboo));
                }
            }
            var grass = GameObject.Find("World_Grass");
            if (grass != null)
            {
                Transform gt = grass.transform;
                for (int i = 0; i < gt.childCount; i++)
                {
                    Transform ch = gt.GetChild(i);
                    if (!ch.name.StartsWith("ENV_GrassClump")) continue;
                    xs.Add(new Vector4(ch.position.x, ch.position.z, 0.9f * ch.localScale.x, KindGrass));
                }
            }
            int n2 = xs.Count;
            ix = new float[n2]; iz = new float[n2]; ir = new float[n2]; ik = new byte[n2];
            cellStart = new int[Dim * Dim + 1];
            var cellOf = new int[n2];
            for (int i = 0; i < n2; i++)
            {
                Vector4 v = xs[i];
                ix[i] = v.x; iz[i] = v.y; ir[i] = v.z; ik[i] = (byte)v.w;
                int c = CellIndex(v.x, v.y);
                cellOf[i] = c;
                if (c >= 0) cellStart[c + 1]++;
            }
            for (int c = 0; c < Dim * Dim; c++) cellStart[c + 1] += cellStart[c];
            cellItems = new int[cellStart[Dim * Dim]];
            var fill = new int[Dim * Dim];
            for (int i = 0; i < n2; i++)
            {
                int c = cellOf[i];
                if (c < 0) continue;
                cellItems[cellStart[c] + fill[c]++] = i;
            }
            Ready = true;
        }

        static int CellIndex(float x, float z)
        {
            int cx = Mathf.FloorToInt((x - Origin) / Cell), cz = Mathf.FloorToInt((z - Origin) / Cell);
            if (cx < 0 || cz < 0 || cx >= Dim || cz >= Dim) return -1;
            return cz * Dim + cx;
        }

        /// <summary>
        /// What surrounds a car at p (half body width hw): blossom 0..1 (under / near a cherry canopy),
        /// tallGrass 0..1 (driving through a grass clump), brush 0..1 (branches / bamboo scraping the body)
        /// and the kind of the plant that brushes (KindCherry / KindBamboo), with the direction to it.
        /// </summary>
        public static void Sample(Vector3 p, float hw, out float blossom, out float tallGrass, out float brush, out byte brushKind, out Vector3 brushDir)
        {
            blossom = tallGrass = brush = 0f;
            brushKind = KindCherry;
            brushDir = Vector3.zero;
            if (!Ready) return;
            int cx = Mathf.FloorToInt((p.x - Origin) / Cell), cz = Mathf.FloorToInt((p.z - Origin) / Cell);
            for (int dz = -1; dz <= 1; dz++)
            {
                int z = cz + dz;
                if (z < 0 || z >= Dim) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= Dim) continue;
                    int c = z * Dim + x;
                    for (int k = cellStart[c], e = cellStart[c + 1]; k < e; k++)
                    {
                        int i = cellItems[k];
                        float ddx = ix[i] - p.x, ddz = iz[i] - p.z;
                        float d2 = ddx * ddx + ddz * ddz;
                        float r = ir[i];
                        if (d2 > (r + 4f) * (r + 4f)) continue;
                        float d = Mathf.Sqrt(d2);
                        switch (ik[i])
                        {
                            case KindCherry:
                            {
                                float b = Mathf.Clamp01(1.15f - d / (r * 1.05f));
                                if (b > blossom) blossom = b;
                                float br = Mathf.Clamp01(1f - (d - 0.55f - hw) / 1.5f);
                                if (br > brush) { brush = br; brushKind = KindCherry; brushDir = new Vector3(ddx, 0f, ddz) / Mathf.Max(0.01f, d); }
                                break;
                            }
                            case KindBamboo:
                            {
                                float br = Mathf.Clamp01(1f - (d - r * 0.5f - hw) / 1.8f);
                                if (br > brush) { brush = br; brushKind = KindBamboo; brushDir = new Vector3(ddx, 0f, ddz) / Mathf.Max(0.01f, d); }
                                break;
                            }
                            default:
                            {
                                float g = Mathf.Clamp01(1f - (d - hw * 0.6f) / r);
                                if (g > tallGrass) tallGrass = g;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // ---- CPU copy of the BR_Road.shader puddle field ----
        static float Frac(float v) { return v - Mathf.Floor(v); }

        static float Hash21(float px, float py)
        {
            px = Frac(px * 123.34f);
            py = Frac(py * 456.21f);
            float d = px * (px + 45.32f) + py * (py + 45.32f);
            px += d; py += d;
            return Frac(px * py);
        }

        static float VNoise(float px, float py)
        {
            float ix0 = Mathf.Floor(px), iy0 = Mathf.Floor(py);
            float fx = px - ix0, fy = py - iy0;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            float a = Hash21(ix0, iy0), b = Hash21(ix0 + 1f, iy0), c = Hash21(ix0, iy0 + 1f), d = Hash21(ix0 + 1f, iy0 + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        /// <summary>Puddle coverage 0..1 the road shader draws at (x, z) for the given surface (current wetness included).</summary>
        public static float Puddle(float x, float z, Surface s)
        {
            float wet = Atmosphere.Wetness;
            if (wet < 0.3f || s == Surface.Grass) return 0f;
            float n = VNoise(x * 0.16f, z * 0.16f) * 0.65f + VNoise(x * 0.55f + 7.1f, z * 0.55f + 7.1f) * 0.35f;
            float t = Mathf.Clamp01((n - 0.60f) / 0.07f);
            float amount = s == Surface.Paving ? 0.6f : s == Surface.Asphalt ? 0.8f : 1f;
            return t * t * (3f - 2f * t) * Mathf.Clamp01((wet - 0.3f) * 2.2f) * amount;
        }
    }
}
