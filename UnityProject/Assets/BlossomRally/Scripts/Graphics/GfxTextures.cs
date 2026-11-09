// GfxTextures.cs - procedural textures for the graphics upgrade (generated once, cached):
//  Noise    - 256² tileable RGBA value-noise fbm (4 independent channels, different scales); global _BR_Noise
//  Paper    - 256² tileable rice paper for the ink & watercolour mode (r fibres, g blotches, b grain)
//  LensDirt - 256² smudges / specks that glow where bloom hits the lens
//  Puff     - 128² soft cloud puff (a = coverage, r = lit-from-above shading)
//  Drop     - 64² lens droplet (rg = refraction normal, a = mask)
using UnityEngine;

namespace BlossomRally
{
    public static class GfxTextures
    {
        static Texture2D noise, paper, dirt, puff, drop;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % period) + period) % period, xb = (xa + 1) % period;
            int ya = ((y0 % period) + period) % period, yb = (ya + 1) % period;
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Seamless fbm on the unit square, roughly 0..1.</summary>
        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * TileNoise(u * period, v * period, period, seed + o * 31);
                norm += amp;
                period *= 2;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        static Texture2D Make(int n, string name, bool mips, TextureWrapMode wrap)
        {
            return new Texture2D(n, n, TextureFormat.RGBA32, mips, true)
            {
                name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear, anisoLevel = 2, hideFlags = HideFlags.DontSave
            };
        }

        static byte B(float v) { return (byte)(Mathf.Clamp01(v) * 255f + 0.5f); }

        /// <summary>Tileable fbm noise, four channels: r large blobs, g medium, b fine, a cellular-ish dapple.</summary>
        public static Texture2D Noise
        {
            get
            {
                if (noise != null) return noise;
                const int n = 256;
                noise = Make(n, "BR_Noise", true, TextureWrapMode.Repeat);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        float r = Fbm(u, v, 4, 4, 11);
                        float g = Fbm(u, v, 8, 4, 23);
                        float b = Fbm(u, v, 16, 3, 37);
                        // leafy dapple: sharpened mid-frequency noise (gaps between leaves)
                        float a = Fbm(u, v, 12, 3, 53);
                        a = Mathf.SmoothStep(0.35f, 0.75f, a);
                        px[y * n + x] = new Color32(B(r), B(g), B(b), B(a));
                    }
                noise.SetPixels32(px);
                noise.Apply(true, false);
                return noise;
            }
        }

        public static Texture2D Paper
        {
            get
            {
                if (paper != null) return paper;
                const int n = 256;
                paper = Make(n, "BR_Paper", true, TextureWrapMode.Repeat);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        // long fibres: noise stretched along a slanted direction, sharpened into strands
                        float f1 = Fbm(u, v, 32, 2, 71);
                        float f2 = Fbm(u, v, 64, 2, 83);
                        float fib = Mathf.Abs(f1 - 0.5f) * 2f;
                        fib = 1f - Mathf.SmoothStep(0f, 0.12f, fib) * 0.6f - (1f - f2) * 0.25f;
                        float blot = Fbm(u, v, 4, 4, 97);
                        float grain = Hash(x, y, 101) * 0.5f + Fbm(u, v, 128, 1, 113) * 0.5f;
                        px[y * n + x] = new Color32(B(fib), B(blot), B(grain), 255);
                    }
                paper.SetPixels32(px);
                paper.Apply(true, false);
                return paper;
            }
        }

        public static Texture2D LensDirt
        {
            get
            {
                if (dirt != null) return dirt;
                const int n = 256;
                dirt = Make(n, "BR_LensDirt", true, TextureWrapMode.Clamp);
                var val = new float[n * n];
                var rng = new System.Random(1717);
                // soft round smudges of different sizes
                for (int s = 0; s < 70; s++)
                {
                    float cx = (float)rng.NextDouble() * n, cy = (float)rng.NextDouble() * n;
                    float rad = Mathf.Lerp(3f, 22f, Mathf.Pow((float)rng.NextDouble(), 2.2f));
                    float str = Mathf.Lerp(0.25f, 0.9f, (float)rng.NextDouble());
                    int x0 = Mathf.Max(0, (int)(cx - rad)), x1 = Mathf.Min(n - 1, (int)(cx + rad));
                    int y0 = Mathf.Max(0, (int)(cy - rad)), y1 = Mathf.Min(n - 1, (int)(cy + rad));
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rad;
                            if (d >= 1f) continue;
                            float k = (1f - d * d);
                            k *= k;
                            // brighter rim: dried droplet look
                            k = k * 0.6f + Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 7f) * 0.5f;
                            val[y * n + x] += k * str;
                        }
                }
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = x / (float)n, v = y / (float)n;
                        float smear = Fbm(u * 0.5f, v, 4, 4, 131);
                        float e = Mathf.Clamp01(val[y * n + x] * 0.55f + Mathf.SmoothStep(0.55f, 0.8f, smear) * 0.35f);
                        // a little more dirt towards the edges of the lens
                        float ex = u - 0.5f, ey = v - 0.5f;
                        e *= 0.55f + 0.9f * Mathf.Clamp01((ex * ex + ey * ey) * 2.4f);
                        byte b = B(e);
                        px[y * n + x] = new Color32(b, b, b, b);
                    }
                dirt.SetPixels32(px);
                dirt.Apply(true, false);
                return dirt;
            }
        }

        public static Texture2D Puff
        {
            get
            {
                if (puff != null) return puff;
                const int n = 128;
                puff = Make(n, "BR_CloudPuff", true, TextureWrapMode.Clamp);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        // flat-bottomed cumulus: a dome plus a few lobes
                        float d = Mathf.Sqrt(u * u * 0.8f + (v > 0 ? v * v * 1.1f : v * v * 3.2f));
                        float lobes = 0.13f * Mathf.Sin(Mathf.Atan2(v, u) * 5f + 1.3f) * Mathf.Clamp01(v + 0.4f);
                        float nse = Fbm((u + 1f) * 0.5f, (v + 1f) * 0.5f, 4, 4, 151) - 0.5f;
                        float a = Mathf.Clamp01((1f - d + lobes + nse * 0.45f) * 2.6f);
                        a = a * a * (3f - 2f * a);
                        float lit = Mathf.Clamp01(0.45f + v * 0.55f + nse * 0.3f);
                        px[y * n + x] = new Color32(B(lit), B(lit), B(lit), B(a));
                    }
                puff.SetPixels32(px);
                puff.Apply(true, false);
                return puff;
            }
        }

        public static Texture2D Drop
        {
            get
            {
                if (drop != null) return drop;
                const int n = 64;
                drop = Make(n, "BR_LensDrop", true, TextureWrapMode.Clamp);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        // slightly egg-shaped drop, heavier at the bottom
                        float vv = v < 0 ? v * 0.85f : v * 1.15f;
                        float d = Mathf.Sqrt(u * u + vv * vv);
                        float a = Mathf.Clamp01((1f - d) * 7f);
                        // lens-like refraction: the drop flips what is behind it
                        float nx = -u * 0.5f + 0.5f, ny = -vv * 0.5f + 0.5f;
                        px[y * n + x] = new Color32(B(nx), B(ny), B(Mathf.Clamp01(1f - d)), B(a));
                    }
                drop.SetPixels32(px);
                drop.Apply(true, false);
                return drop;
            }
        }
    }
}
