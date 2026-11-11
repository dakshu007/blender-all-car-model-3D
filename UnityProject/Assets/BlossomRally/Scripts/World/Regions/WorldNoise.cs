// WorldNoise.cs - deterministic, allocation-free 2D noise for terrain and placement (pure C#, no engine
// calls, identical in the game and in the offline terrain harness). Value noise with quintic fade,
// fBm, ridged multifractal, a cellular (Worley) distance and a domain-warp helper.
// All outputs are documented ranges; everything is a pure function of (x, z, seed).
using UnityEngine;

namespace BlossomRally
{
    public static class WorldNoise
    {
        static uint Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)z * 0xC2B2AE3Du;
                h *= 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x165667B1u;
                h ^= h >> 13;
                return h;
            }
        }

        /// <summary>Uniform 0..1 from an integer lattice point.</summary>
        public static float Hash01(int x, int z, int seed)
        {
            return (Hash(x, z, seed) & 0xFFFFFF) / 16777215f;
        }

        static float Fade(float t) { return t * t * t * (t * (t * 6f - 15f) + 10f); }

        /// <summary>Value noise, range -1..1, feature size 1 unit.</summary>
        public static float Value(float x, float z, int seed)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz;
            float a = Hash01(ix, iz, seed), b = Hash01(ix + 1, iz, seed);
            float c = Hash01(ix, iz + 1, seed), d = Hash01(ix + 1, iz + 1, seed);
            float u = Fade(fx), w = Fade(fz);
            float v = a + (b - a) * u + (c - a) * w + (a - b - c + d) * u * w;
            return v * 2f - 1f;
        }

        /// <summary>Fractal sum of value noise, range about -1..1. scale = metres per feature of the first octave.</summary>
        public static float Fbm(float x, float z, float scale, int octaves, int seed, float gain = 0.5f, float lacunarity = 2.03f)
        {
            float f = 1f / scale, amp = 1f, sum = 0f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(x * f, z * f, seed + o * 131) * amp;
                norm += amp;
                amp *= gain;
                f *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Ridged multifractal, range 0..1 (1 = sharp ridge crest). Good for mountain chains.</summary>
        public static float Ridged(float x, float z, float scale, int octaves, int seed, float gain = 0.5f)
        {
            float f = 1f / scale, amp = 1f, sum = 0f, norm = 0f, weight = 1f;
            for (int o = 0; o < octaves; o++)
            {
                float n = 1f - Mathf.Abs(Value(x * f, z * f, seed + o * 97));
                n *= n;
                n *= weight;
                weight = Mathf.Clamp01(n * 2f);
                sum += n * amp;
                norm += amp;
                amp *= gain;
                f *= 2.07f;
            }
            return sum / norm;
        }

        /// <summary>Cellular noise: distance (in cell units, 0..~1.2) from (x,z)/scale to the nearest jittered feature point.</summary>
        public static float Cellular(float x, float z, float scale, int seed)
        {
            x /= scale; z /= scale;
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float best = 9f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cz = iz + dz;
                    float px = cx + Hash01(cx, cz, seed), pz = cz + Hash01(cx, cz, seed + 17);
                    float d = (px - x) * (px - x) + (pz - z) * (pz - z);
                    if (d < best) best = d;
                }
            return Mathf.Sqrt(best);
        }

        /// <summary>Domain warp: offsets (x,z) by fBm so shapes look less regular.</summary>
        public static Vector2 Warp(float x, float z, float scale, float amount, int seed)
        {
            return new Vector2(x + Fbm(x, z, scale, 3, seed) * amount, z + Fbm(x, z, scale, 3, seed + 911) * amount);
        }

        /// <summary>Smooth Hermite step (same as Mathf.SmoothStep with edges a, b; a may be > b).</summary>
        public static float Smooth(float a, float b, float x)
        {
            if (a == b) return x < a ? 0f : 1f;
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
