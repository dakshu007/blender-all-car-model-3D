// CarDetailTextures.cs - small textures generated in code for the car details: a pixel canvas with a
// 5x7 pixel font (plates, door numbers, sponsor stickers, trip computer), a shared tileable noise texture
// (glass dirt / rain drops / heat haze) and tiny particle textures. All fictional names and logos.
using UnityEngine;

namespace BlossomRally
{
    /// <summary>Simple RGBA pixel canvas (row 0 = bottom, like texture UVs).</summary>
    internal sealed class PixelCanvas
    {
        public readonly int w, h;
        public readonly Color32[] px;

        public PixelCanvas(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }

        public void Clear(Color32 c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            if (c.a == 255) { px[y * w + x] = c; return; }
            Color32 d = px[y * w + x];
            float a = c.a / 255f;
            px[y * w + x] = new Color32((byte)(d.r + (c.r - d.r) * a), (byte)(d.g + (c.g - d.g) * a), (byte)(d.b + (c.b - d.b) * a),
                                        (byte)Mathf.Max(d.a, c.a));
        }

        public void Rect(int x0, int y0, int rw, int rh, Color32 c)
        {
            for (int y = y0; y < y0 + rh; y++) for (int x = x0; x < x0 + rw; x++) Set(x, y, c);
        }

        /// <summary>Rounded rectangle (radius r) filled with c.</summary>
        public void RoundRect(int x0, int y0, int rw, int rh, int r, Color32 c)
        {
            for (int y = y0; y < y0 + rh; y++)
                for (int x = x0; x < x0 + rw; x++)
                {
                    int dx = Mathf.Max(0, Mathf.Max(x0 + r - x, x - (x0 + rw - 1 - r)));
                    int dy = Mathf.Max(0, Mathf.Max(y0 + r - y, y - (y0 + rh - 1 - r)));
                    if (dx * dx + dy * dy <= r * r) Set(x, y, c);
                }
        }

        public void Disc(float cx, float cy, float r, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0f) Set(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * a)));
                }
        }

        public void Ring(float cx, float cy, float r, float thick, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - r - thick - 1), x1 = Mathf.CeilToInt(cx + r + thick + 1);
            int y0 = Mathf.FloorToInt(cy - r - thick - 1), y1 = Mathf.CeilToInt(cy + r + thick + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r);
                    float a = Mathf.Clamp01(thick * 0.5f - d + 0.5f);
                    if (a > 0f) Set(x, y, new Color32(c.r, c.g, c.b, (byte)(c.a * a)));
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float thick, Color32 c)
        {
            float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Disc(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, thick * 0.5f, c);
            }
        }

        /// <summary>Draw text with the pixel font: each font pixel becomes a scale x scale block; (x, y) = bottom-left.</summary>
        public void Text(string s, int x, int y, int scale, Color32 c)
        {
            int cx = x;
            foreach (char ch in s)
            {
                var g = PixelFont.Glyph(ch);
                if (g != null)
                    for (int row = 0; row < 7; row++)
                        for (int col = 0; col < 5; col++)
                            if ((g[row] & (1 << (4 - col))) != 0)
                                Rect(cx + col * scale, y + (6 - row) * scale, scale, scale, c);
                cx += 6 * scale;
            }
        }

        public static int TextWidth(string s, int scale) { return Mathf.Max(0, s.Length * 6 - 1) * scale; }

        /// <summary>Largest scale that fits text in (rw, rh), centred.</summary>
        public void TextFit(string s, int x0, int y0, int rw, int rh, Color32 c, int maxScale = 8)
        {
            int scale = maxScale;
            while (scale > 1 && (TextWidth(s, scale) > rw || 7 * scale > rh)) scale--;
            int tx = x0 + (rw - TextWidth(s, scale)) / 2;
            int ty = y0 + (rh - 7 * scale) / 2;
            Text(s, tx, ty, scale, c);
        }

        public Texture2D ToTexture(string name, bool mips, bool linear = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { name = name };
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 2;
            t.SetPixels32(px);
            t.Apply(mips, false);
            return t;
        }

        public void Upload(Texture2D t, bool mips)
        {
            t.SetPixels32(px);
            t.Apply(mips, false);
        }
    }

    /// <summary>Classic 5x7 pixel font: digits, capitals and a few symbols.</summary>
    internal static class PixelFont
    {
        static int[][] table;

        static int[] G(params string[] rows)
        {
            var g = new int[7];
            for (int i = 0; i < 7; i++) g[i] = System.Convert.ToInt32(rows[i], 2);
            return g;
        }

        public static int[] Glyph(char c)
        {
            if (table == null) Build();
            if (c >= 'a' && c <= 'z') c = (char)(c - 32);
            return c < 128 ? table[c] : null;
        }

        static void Build()
        {
            table = new int[128][];
            table['A'] = G("01110", "10001", "10001", "11111", "10001", "10001", "10001");
            table['B'] = G("11110", "10001", "10001", "11110", "10001", "10001", "11110");
            table['C'] = G("01110", "10001", "10000", "10000", "10000", "10001", "01110");
            table['D'] = G("11110", "10001", "10001", "10001", "10001", "10001", "11110");
            table['E'] = G("11111", "10000", "10000", "11110", "10000", "10000", "11111");
            table['F'] = G("11111", "10000", "10000", "11110", "10000", "10000", "10000");
            table['G'] = G("01110", "10001", "10000", "10111", "10001", "10001", "01111");
            table['H'] = G("10001", "10001", "10001", "11111", "10001", "10001", "10001");
            table['I'] = G("01110", "00100", "00100", "00100", "00100", "00100", "01110");
            table['J'] = G("00111", "00010", "00010", "00010", "00010", "10010", "01100");
            table['K'] = G("10001", "10010", "10100", "11000", "10100", "10010", "10001");
            table['L'] = G("10000", "10000", "10000", "10000", "10000", "10000", "11111");
            table['M'] = G("10001", "11011", "10101", "10101", "10001", "10001", "10001");
            table['N'] = G("10001", "10001", "11001", "10101", "10011", "10001", "10001");
            table['O'] = G("01110", "10001", "10001", "10001", "10001", "10001", "01110");
            table['P'] = G("11110", "10001", "10001", "11110", "10000", "10000", "10000");
            table['Q'] = G("01110", "10001", "10001", "10001", "10101", "10010", "01101");
            table['R'] = G("11110", "10001", "10001", "11110", "10100", "10010", "10001");
            table['S'] = G("01111", "10000", "10000", "01110", "00001", "00001", "11110");
            table['T'] = G("11111", "00100", "00100", "00100", "00100", "00100", "00100");
            table['U'] = G("10001", "10001", "10001", "10001", "10001", "10001", "01110");
            table['V'] = G("10001", "10001", "10001", "10001", "10001", "01010", "00100");
            table['W'] = G("10001", "10001", "10001", "10101", "10101", "10101", "01010");
            table['X'] = G("10001", "10001", "01010", "00100", "01010", "10001", "10001");
            table['Y'] = G("10001", "10001", "10001", "01010", "00100", "00100", "00100");
            table['Z'] = G("11111", "00001", "00010", "00100", "01000", "10000", "11111");
            table['0'] = G("01110", "10001", "10011", "10101", "11001", "10001", "01110");
            table['1'] = G("00100", "01100", "00100", "00100", "00100", "00100", "01110");
            table['2'] = G("01110", "10001", "00001", "00010", "00100", "01000", "11111");
            table['3'] = G("11111", "00010", "00100", "00010", "00001", "10001", "01110");
            table['4'] = G("00010", "00110", "01010", "10010", "11111", "00010", "00010");
            table['5'] = G("11111", "10000", "11110", "00001", "00001", "10001", "01110");
            table['6'] = G("00110", "01000", "10000", "11110", "10001", "10001", "01110");
            table['7'] = G("11111", "00001", "00010", "00100", "01000", "01000", "01000");
            table['8'] = G("01110", "10001", "10001", "01110", "10001", "10001", "01110");
            table['9'] = G("01110", "10001", "10001", "01111", "00001", "00010", "01100");
            table['-'] = G("00000", "00000", "00000", "11111", "00000", "00000", "00000");
            table['.'] = G("00000", "00000", "00000", "00000", "00000", "01100", "01100");
            table[':'] = G("00000", "01100", "01100", "00000", "01100", "01100", "00000");
            table['/'] = G("00001", "00010", "00010", "00100", "01000", "01000", "10000");
            table['*'] = G("00000", "00100", "10101", "01110", "10101", "00100", "00000");
            table[' '] = G("00000", "00000", "00000", "00000", "00000", "00000", "00000");
        }
    }

    internal static class CarDetailTextures
    {
        static Texture2D noise, white, shard, rock;

        /// <summary>Tileable value noise, 4 independent channels (64x64).</summary>
        public static Texture2D Noise
        {
            get
            {
                if (noise != null) return noise;
                const int s = 64;
                noise = new Texture2D(s, s, TextureFormat.RGBA32, true, true) { name = "BR_DetailNoise" };
                noise.wrapMode = TextureWrapMode.Repeat;
                var px = new Color32[s * s];
                var lattice = new float[4, 8, 8];
                var rnd = new System.Random(1234);
                for (int c = 0; c < 4; c++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) lattice[c, x, y] = (float)rnd.NextDouble();
                var fine = new float[4, 16, 16];
                for (int c = 0; c < 4; c++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) fine[c, x, y] = (float)rnd.NextDouble();
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        var v = new byte[4];
                        for (int c = 0; c < 4; c++)
                        {
                            float a = Sample(lattice, c, 8, x / 8f, y / 8f);
                            float b = Sample(fine, c, 16, x / 4f, y / 4f);
                            v[c] = (byte)(Mathf.Clamp01(a * 0.65f + b * 0.35f) * 255f);
                        }
                        px[y * s + x] = new Color32(v[0], v[1], v[2], v[3]);
                    }
                noise.SetPixels32(px);
                noise.Apply(true, false);
                return noise;
            }
        }

        static float Sample(float[,,] l, int c, int n, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % n) + n) % n, xb = (xa + 1) % n, ya = ((y0 % n) + n) % n, yb = (ya + 1) % n;
            float a = Mathf.Lerp(l[c, xa, ya], l[c, xb, ya], fx);
            float b = Mathf.Lerp(l[c, xa, yb], l[c, xb, yb], fx);
            return Mathf.Lerp(a, b, fy);
        }

        public static Texture2D White
        {
            get
            {
                if (white != null) return white;
                white = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "BR_DetailWhite" };
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                white.SetPixels32(px);
                white.Apply();
                return white;
            }
        }

        /// <summary>Sharp glass splinter with a bright glint.</summary>
        public static Texture2D Shard
        {
            get
            {
                if (shard != null) return shard;
                var c = new PixelCanvas(32, 32);
                c.Clear(new Color32(255, 255, 255, 0));
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        // triangle (4,3) (28,9) (12,29)
                        float u = x + 0.5f, v = y + 0.5f;
                        bool inside = Edge(4, 3, 28, 9, u, v) >= 0 && Edge(28, 9, 12, 29, u, v) >= 0 && Edge(12, 29, 4, 3, u, v) >= 0;
                        if (inside)
                        {
                            byte k = (byte)(200 + 55 * Mathf.Clamp01(1f - Mathf.Abs(u - v) / 10f));
                            c.Set(x, y, new Color32(k, k, 255, 230));
                        }
                    }
                shard = c.ToTexture("BR_Shard", true);
                return shard;
            }
        }

        static float Edge(float ax, float ay, float bx, float by, float px, float py)
        {
            return (bx - ax) * (py - ay) - (by - ay) * (px - ax);
        }

        /// <summary>Lumpy stone / clod sprite (soft-edged, shaded top-left).</summary>
        public static Texture2D Rock
        {
            get
            {
                if (rock != null) return rock;
                const int s = 32;
                var c = new PixelCanvas(s, s);
                c.Clear(new Color32(255, 255, 255, 0));
                var rnd = new System.Random(77);
                var rad = new float[8];
                for (int i = 0; i < 8; i++) rad[i] = 0.72f + 0.25f * (float)rnd.NextDouble();
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + 0.5f) / s * 2f - 1f, v = (y + 0.5f) / s * 2f - 1f;
                        float ang = (Mathf.Atan2(v, u) / (Mathf.PI * 2f) + 1f) * 8f;
                        int i0 = Mathf.FloorToInt(ang) % 8, i1 = (i0 + 1) % 8;
                        float rr = Mathf.Lerp(rad[i0], rad[i1], ang - Mathf.Floor(ang));
                        float d = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Clamp01((rr - d) * 10f);
                        if (a <= 0f) continue;
                        float shade = Mathf.Clamp01(0.75f + 0.35f * (v - u) * 0.5f - d * 0.15f);
                        byte k = (byte)(255 * shade);
                        c.Set(x, y, new Color32(k, k, k, (byte)(255 * a)));
                    }
                rock = c.ToTexture("BR_Rock", true);
                return rock;
            }
        }
    }
}
