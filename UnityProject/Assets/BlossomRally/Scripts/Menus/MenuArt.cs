// MenuArt.cs - shared look for the Blossom Rally menu screens (valley map, photo album, loading
// screen, garage showroom): colours, resolution-scaled IMGUI styles, small procedural textures
// (paper grain, soft dot, ring, flags), thread-safe value noise + CPU raster helpers used by the
// ink-painting generators, and an immediate-mode GL helper for thick anti-aliased-looking lines.
// Also bootstraps the "BR_Menus" host object that carries the menu components.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class MenuArt
    {
        // ---- palette (matches UI.cs: plum panels, pink / white / gold text) ----
        public static readonly Color Plum = new Color(0.20f, 0.10f, 0.16f, 1f);
        public static readonly Color PlumDeep = new Color(0.12f, 0.05f, 0.10f, 1f);
        public static readonly Color Ink = new Color(0.10f, 0.08f, 0.10f, 1f);
        public static readonly Color InkSoft = new Color(0.22f, 0.18f, 0.20f, 1f);
        public static readonly Color PaperCol = new Color(0.95f, 0.91f, 0.83f, 1f);
        public static readonly Color Vermilion = new Color(0.78f, 0.16f, 0.14f, 1f);
        public static readonly Color Lacquer = new Color(0.62f, 0.07f, 0.09f, 1f);
        public static readonly Color GoldMedal = new Color(1f, 0.82f, 0.32f, 1f);
        public static readonly Color SilverMedal = new Color(0.86f, 0.89f, 0.95f, 1f);
        public static readonly Color BronzeMedal = new Color(0.88f, 0.58f, 0.38f, 1f);

        public static Color MedalColor(int m)
        {
            return m == 3 ? GoldMedal : m == 2 ? SilverMedal : m == 1 ? BronzeMedal : new Color(0.7f, 0.62f, 0.66f, 1f);
        }

        public static string MedalName(int m) { return m == 3 ? "Gold" : m == 2 ? "Silver" : m == 1 ? "Bronze" : "No medal"; }

        public static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, c.a * a); }

        // ---- bootstrap: one hidden host carrying the menu screens ----
        static bool booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            var go = new GameObject("BR_Menus");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<LoadingScreenDriver>();
            go.AddComponent<ValleyMap>();
            go.AddComponent<PhotoAlbum>();
            go.AddComponent<ResultsCamera>();
        }

        // ---- resolution scaled styles ----
        public static float S { get { return Mathf.Clamp(Screen.height / 1080f, 0.7f, 2.2f); } }
        static int styleHeight = -1;
        public static GUIStyle Head, Title, Big, Mid, MidL, MidR, Body, BodyC, BodyR, Small, SmallC, SmallR, Tiny, TinyC, Calli;

        public static void Styles()
        {
            UI.Styles();
            if (styleHeight == Screen.height && Head != null) return;
            styleHeight = Screen.height;
            float s = S;
            GUIStyle b = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Clip, richText = false };
            System.Func<int, FontStyle, TextAnchor, GUIStyle> mk = (size, fs, an) =>
                new GUIStyle(b) { fontSize = Mathf.Max(9, Mathf.RoundToInt(size * s)), fontStyle = fs, alignment = an };
            Head = mk(64, FontStyle.Bold, TextAnchor.MiddleCenter);
            Title = mk(38, FontStyle.Bold, TextAnchor.MiddleCenter);
            Big = mk(30, FontStyle.Bold, TextAnchor.MiddleLeft);
            Mid = mk(22, FontStyle.Bold, TextAnchor.MiddleCenter);
            MidL = mk(22, FontStyle.Bold, TextAnchor.MiddleLeft);
            MidR = mk(22, FontStyle.Bold, TextAnchor.MiddleRight);
            Body = mk(18, FontStyle.Normal, TextAnchor.MiddleLeft);
            BodyC = mk(18, FontStyle.Normal, TextAnchor.MiddleCenter);
            BodyR = mk(18, FontStyle.Normal, TextAnchor.MiddleRight);
            Small = mk(15, FontStyle.Normal, TextAnchor.MiddleLeft);
            SmallC = mk(15, FontStyle.Normal, TextAnchor.MiddleCenter);
            SmallR = mk(15, FontStyle.Normal, TextAnchor.MiddleRight);
            Tiny = mk(13, FontStyle.Normal, TextAnchor.MiddleLeft);
            TinyC = mk(13, FontStyle.Normal, TextAnchor.MiddleCenter);
            Calli = mk(26, FontStyle.Italic, TextAnchor.MiddleCenter);
            Small.wordWrap = true;
            Body.wordWrap = true;
        }

        /// <summary>Text with an optional soft shadow (plum for light text, none for ink on paper).</summary>
        public static void Text(Rect r, string s, GUIStyle st, Color c, bool shadow = true)
        {
            var old = GUI.color;
            if (shadow)
            {
                GUI.color = new Color(0.12f, 0.05f, 0.09f, 0.6f * c.a);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, st);
            }
            GUI.color = c;
            GUI.Label(r, s, st);
            GUI.color = old;
        }

        public static void Fill(Rect r, Color c)
        {
            if (c.a <= 0f) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Tex(Rect r, Texture t, Color c, ScaleMode mode = ScaleMode.StretchToFill)
        {
            if (t == null || c.a <= 0f) return;
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t, mode, true);
            GUI.color = old;
        }

        /// <summary>Plum panel with a thin pink top rule and soft drop shadow.</summary>
        public static void Card(Rect r, float alpha, bool accent = true)
        {
            Tex(new Rect(r.x - 10f, r.y - 6f, r.width + 20f, r.height + 22f), SoftRect, new Color(0f, 0f, 0f, 0.35f * alpha));
            Fill(r, new Color(Plum.r, Plum.g, Plum.b, 0.8f * alpha));
            if (accent) Fill(new Rect(r.x, r.y, r.width, Mathf.Max(2f, 3f * S)), A(UI.Pink, 0.85f * alpha));
        }

        /// <summary>Hairline rectangle outline.</summary>
        public static void Outline(Rect r, float w, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c);
            Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c);
            Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        /// <summary>Tiled paper grain over a rect.</summary>
        public static void PaperFill(Rect r, Color tint)
        {
            var old = GUI.color;
            GUI.color = tint;
            float k = 1f / (256f * Mathf.Max(0.75f, S));
            GUI.DrawTextureWithTexCoords(r, Paper, new Rect(r.x * k, r.y * k, r.width * k, r.height * k), true);
            GUI.color = old;
        }

        /// <summary>A keycap-style hint chip ("ENTER  race"). Returns its width; clickable when 'click' is set.</summary>
        public static float Chip(float x, float y, string key, string label, bool click, out bool clicked, bool light = false)
        {
            Styles();
            float s = S, h = 28f * s;
            var kc = new GUIContent(key);
            var lc = new GUIContent(label);
            float kw = Mathf.Max(h, TinyC.CalcSize(kc).x + 14f * s), lw = Small.CalcSize(lc).x + 10f * s;
            Rect all = new Rect(x, y, kw + lw + 8f * s, h);
            Fill(new Rect(x, y, kw, h), light ? A(Ink, 0.85f) : A(UI.White, 0.92f));
            Text(new Rect(x, y, kw, h), key, TinyC, light ? PaperCol : Plum, false);
            Text(new Rect(x + kw + 8f * s, y, lw, h), label, Small, light ? Ink : UI.White, !light);
            clicked = click && GUI.Button(all, GUIContent.none, GUIStyle.none);
            return all.width + 18f * s;
        }

        public static float Chip(float x, float y, string key, string label, bool light = false)
        {
            bool c;
            return Chip(x, y, key, label, false, out c, light);
        }

        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
        public static float Damp(float dt, float rate) { return 1f - Mathf.Exp(-rate * dt); }

        // ---- procedural textures ----
        static Texture2D paper, dot, ring, softRect, flagStart, flagFinish, vignette;

        public static Texture2D Paper
        {
            get
            {
                if (paper != null) return paper;
                const int n = 256;
                paper = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BR_PaperTile", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // tileable: noise sampled on a torus-ish wrap via periodic lattice
                        float g = Noise.Tile(x / 32f, y / 32f, 8, 11) * 0.5f + Noise.Tile(x / 8f, y / 8f, 32, 12) * 0.3f + Noise.Hash01(x, y, 13) * 0.2f;
                        float fib = Noise.Tile(x / 64f, y / 4f, 4, 14);
                        fib = Mathf.Clamp01((fib - 0.72f) * 4f);
                        float k = 0.93f + g * 0.07f - fib * 0.05f;
                        px[y * n + x] = new Color32((byte)(242 * k), (byte)(233 * k), (byte)(214 * k), 255);
                    }
                paper.SetPixels32(px);
                paper.Apply(true, true);
                return paper;
            }
        }

        /// <summary>White disc with a soft edge.</summary>
        public static Texture2D Dot
        {
            get
            {
                if (dot != null) return dot;
                dot = Radial("BR_SoftDot", 64, r => Mathf.Clamp01((1f - r) * 6f));
                return dot;
            }
        }

        /// <summary>Soft glow blob (gaussian falloff).</summary>
        public static Texture2D Glow
        {
            get
            {
                if (glow != null) return glow;
                glow = Radial("BR_Glow", 64, r => Mathf.Exp(-r * r * 4.5f) * Mathf.Clamp01((1f - r) * 4f));
                return glow;
            }
        }
        static Texture2D glow;

        public static Texture2D Ring
        {
            get
            {
                if (ring != null) return ring;
                ring = Radial("BR_Ring", 64, r => Mathf.Clamp01((1f - r) * 8f) * Mathf.Clamp01((r - 0.72f) * 8f));
                return ring;
            }
        }

        /// <summary>White rectangle with very soft edges, for drop shadows.</summary>
        public static Texture2D SoftRect
        {
            get
            {
                if (softRect != null) return softRect;
                const int n = 64;
                softRect = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BR_SoftRect", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float ex = Mathf.Min(x, n - 1 - x) / 14f, ey = Mathf.Min(y, n - 1 - y) / 14f;
                        float a = Ease(Mathf.Clamp01(ex)) * Ease(Mathf.Clamp01(ey));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                softRect.SetPixels32(px);
                softRect.Apply(false, true);
                return softRect;
            }
        }

        /// <summary>Dark edges, clear centre (full-screen vignette).</summary>
        public static Texture2D Vignette
        {
            get
            {
                if (vignette != null) return vignette;
                const int n = 128;
                vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BR_Vignette", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((d - 0.55f) * 1.4f) * 255));
                    }
                vignette.SetPixels32(px);
                vignette.Apply(false, true);
                return vignette;
            }
        }

        static Texture2D Radial(string name, int n, System.Func<float, float> f)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(f(Mathf.Sqrt(dx * dx + dy * dy)));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Little pink pennant on a pole (start icon).</summary>
        public static Texture2D FlagStart
        {
            get
            {
                if (flagStart != null) return flagStart;
                flagStart = Flag("BR_FlagStart", false);
                return flagStart;
            }
        }

        /// <summary>Chequered flag (finish icon).</summary>
        public static Texture2D FlagFinish
        {
            get
            {
                if (flagFinish != null) return flagFinish;
                flagFinish = Flag("BR_FlagFinish", true);
                return flagFinish;
            }
        }

        static Texture2D Flag(string name, bool chequer)
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            var px = new Color32[n * n];
            Color32 clear = new Color32(0, 0, 0, 0), ink = new Color32(30, 22, 26, 255), cream = new Color32(250, 244, 232, 255);
            Color32 pink = new Color32(240, 120, 160, 255);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int fy = n - 1 - y;    // fy: 0 = top
                    Color32 c = clear;
                    // pole with an outline
                    if (x >= 10 && x <= 15 && fy >= 4 && fy <= 60) c = (x == 10 || x == 15) ? ink : new Color32(200, 160, 90, 255);
                    // cloth (slight wave)
                    float wave = Mathf.Sin(x * 0.18f) * 2.5f;
                    float top = 6f + wave, bottom = 36f + wave;
                    if (x > 15 && x < 58 && fy > top && fy < bottom)
                    {
                        bool edge = x == 57 || fy < top + 2f || fy > bottom - 2f;
                        if (chequer) c = edge ? ink : (((x - 16) / 7 + (int)((fy - top) / 7f)) & 1) == 0 ? ink : cream;
                        else
                        {
                            float tri = (x - 15f) / 42f;                    // pennant narrows to a point
                            float mid = (top + bottom) * 0.5f, half = (bottom - top) * 0.5f * (1f - tri);
                            if (Mathf.Abs(fy - mid) < half) c = Mathf.Abs(fy - mid) > half - 2f ? ink : pink;
                        }
                    }
                    px[y * n + x] = c;
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        // ---- red seal stamp (procedural, cached) ----
        static Texture2D seal;
        public static Texture2D Seal
        {
            get
            {
                if (seal != null) return seal;
                const int n = 96;
                var buf = new float[n * n];
                InkRaster.SealStamp(buf, n, n, n * 0.5f, n * 0.5f, n * 0.9f, 7);
                seal = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BR_Seal", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        px[(n - 1 - y) * n + x] = new Color32(196, 36, 34, (byte)(Mathf.Clamp01(buf[y * n + x]) * 235));
                seal.SetPixels32(px);
                seal.Apply(true, true);
                return seal;
            }
        }
    }

    /// <summary>Thread-safe hash / value noise (no Unity calls, usable from worker threads).</summary>
    public static class Noise
    {
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float S(float t) { return t * t * (3f - 2f * t); }

        public static float Value(float x, float y, int seed)
        {
            int xi = FastFloor(x), yi = FastFloor(y);
            float fx = S(x - xi), fy = S(y - yi);
            float a = Hash01(xi, yi, seed), b = Hash01(xi + 1, yi, seed), c = Hash01(xi, yi + 1, seed), d = Hash01(xi + 1, yi + 1, seed);
            float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
            return ab + (cd - ab) * fy;
        }

        /// <summary>Value noise that repeats every 'period' cells (for tileable textures).</summary>
        public static float Tile(float x, float y, int period, int seed)
        {
            int xi = FastFloor(x), yi = FastFloor(y);
            float fx = S(x - xi), fy = S(y - yi);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Hash01(x0, y0, seed), b = Hash01(x1, y0, seed), c = Hash01(x0, y1, seed), d = Hash01(x1, y1, seed);
            float ab = a + (b - a) * fx, cd = c + (d - c) * fx;
            return ab + (cd - ab) * fy;
        }

        public static float Fbm(float x, float y, int seed, int oct)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < oct; i++)
            {
                sum += Value(x, y, seed + i * 31) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        public static int FastFloor(float v) { int i = (int)v; return v < i ? i - 1 : i; }
        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
    }

    /// <summary>CPU raster helpers on float coverage buffers (row 0 = top). Max-blend.</summary>
    public static class InkRaster
    {
        public static void Disc(float[] buf, int w, int h, float cx, float cy, float r, float val)
        {
            if (r <= 0.05f) return;
            int x0 = Mathf.Max(0, (int)(cx - r - 1)), x1 = Mathf.Min(w - 1, (int)(cx + r + 1));
            int y0 = Mathf.Max(0, (int)(cy - r - 1)), y1 = Mathf.Min(h - 1, (int)(cy + r + 1));
            for (int y = y0; y <= y1; y++)
            {
                float dy = y + 0.5f - cy;
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f) * val;
                    int i = y * w + x;
                    if (a > buf[i]) buf[i] = a;
                }
            }
        }

        /// <summary>Brush stroke: discs from a (radius ra) to b (radius rb), with a dry-brush texture.</summary>
        public static void Stroke(float[] buf, int w, int h, Vector2 a, Vector2 b, float ra, float rb, float val, int seed)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.35f, Mathf.Min(ra, rb) * 0.45f)));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector2 p = Vector2.Lerp(a, b, t);
                float dry = 0.82f + 0.18f * Noise.Value(p.x * 0.15f, p.y * 0.15f, seed);
                Disc(buf, w, h, p.x, p.y, Mathf.Lerp(ra, rb, t), val * dry);
            }
        }

        /// <summary>Scan-line polygon fill (even-odd).</summary>
        public static void Poly(float[] buf, int w, int h, Vector2[] pts, float val)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(maxY));
            var xs = new List<float>(8);
            for (int y = y0; y <= y1; y++)
            {
                float sy = y + 0.5f;
                xs.Clear();
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    Vector2 pi = pts[i], pj = pts[j];
                    if ((pi.y > sy) != (pj.y > sy)) xs.Add(pi.x + (sy - pi.y) / (pj.y - pi.y) * (pj.x - pi.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    float xa = xs[k], xb = xs[k + 1];
                    int ia = Mathf.Max(0, Mathf.FloorToInt(xa)), ib = Mathf.Min(w - 1, Mathf.CeilToInt(xb));
                    for (int x = ia; x <= ib; x++)
                    {
                        float cov = Mathf.Clamp01(Mathf.Min(x + 1f, xb) - Mathf.Max((float)x, xa));
                        float a = cov * val;
                        int idx = y * w + x;
                        if (a > buf[idx]) buf[idx] = a;
                    }
                }
            }
        }

        /// <summary>Square seal stamp centred at (cx, cy): ragged red square with carved pseudo-glyph lines.</summary>
        public static void SealStamp(float[] buf, int w, int h, float cx, float cy, float size, int seed)
        {
            float half = size * 0.5f;
            var rng = new System.Random(seed);
            int x0 = Mathf.Max(0, (int)(cx - half - 2)), x1 = Mathf.Min(w - 1, (int)(cx + half + 2));
            int y0 = Mathf.Max(0, (int)(cy - half - 2)), y1 = Mathf.Min(h - 1, (int)(cy + half + 2));
            // carved strokes in local [-1, 1] space: a 2x2 grid of pseudo characters
            var strokes = new List<Vector4>();
            for (int gx = 0; gx < 2; gx++)
                for (int gy = 0; gy < 2; gy++)
                {
                    float ox = gx == 0 ? -0.46f : 0.46f, oy = gy == 0 ? -0.46f : 0.46f;
                    int n = 3 + rng.Next(3);
                    for (int s = 0; s < n; s++)
                    {
                        bool horiz = rng.NextDouble() < 0.55;
                        float c0 = (float)rng.NextDouble() * 0.56f - 0.28f, a0 = (float)rng.NextDouble() * 0.2f - 0.3f, a1 = 0.1f + (float)rng.NextDouble() * 0.2f;
                        strokes.Add(horiz ? new Vector4(ox + a0, oy + c0, ox + a1, oy + c0) : new Vector4(ox + c0, oy + a0, ox + c0, oy + a1));
                    }
                }
            float border = 0.13f, lineW = 0.075f;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float lx = (x + 0.5f - cx) / half, ly = (y + 0.5f - cy) / half;
                    float rag = (Noise.Value(x * 0.35f, y * 0.35f, seed) - 0.5f) * 0.08f;
                    float edge = Mathf.Max(Mathf.Abs(lx), Mathf.Abs(ly)) + rag;
                    if (edge > 1f) continue;
                    float v = 1f;
                    bool inner = edge < 1f - border;
                    if (inner)
                    {
                        // thin carved frame line + glyph strokes (paper shows through)
                        if (edge > 1f - border - 0.07f) v = 0f;
                        else
                            foreach (var st in strokes)
                            {
                                Vector2 a = new Vector2(st.x, st.y), b = new Vector2(st.z, st.w), p = new Vector2(lx, ly);
                                Vector2 ab = b - a;
                                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                                if ((a + ab * t - p).magnitude < lineW) { v = 0f; break; }
                            }
                    }
                    // uneven stamp pressure
                    v *= 0.78f + 0.22f * Noise.Value(x * 0.6f, y * 0.6f, seed + 5);
                    if (Noise.Hash01(x, y, seed + 9) < 0.05f) v *= 0.4f;
                    int idx = y * w + x;
                    if (v > buf[idx]) buf[idx] = v;
                }
        }
    }

    /// <summary>Immediate-mode GL quads in GUI coordinates (y down), clipped to a rect. Use only in OnGUI Repaint.</summary>
    public static class InkGL
    {
        static Material mat;
        static bool tried;
        static Rect clip;
        static bool linear;
        static float sh;

        public static bool Available
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    Shader s = Shader.Find("Hidden/Internal-Colored");
                    if (s == null) s = Shader.Find("Sprites/Default");
                    if (s != null)
                    {
                        mat = new Material(s) { name = "BR_InkGL", hideFlags = HideFlags.HideAndDontSave };
                        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                        if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    }
                }
                return mat != null;
            }
        }

        /// <summary>Starts a batch of quads. Returns false outside Repaint or when GL drawing is unavailable.</summary>
        public static bool Begin(Rect clipRect)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint || !Available) return false;
            clip = clipRect;
            linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            sh = Screen.height;
            mat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);
            return true;
        }

        public static void End()
        {
            GL.End();
            GL.PopMatrix();
        }

        static void Col(Color c) { GL.Color(linear ? new Color(c.linear.r, c.linear.g, c.linear.b, c.a) : c); }

        static void V(Vector2 p) { GL.Vertex3(p.x, sh - p.y, 0f); }

        /// <summary>Thick segment (GUI coords), clipped to the batch rect.</summary>
        public static void Seg(Vector2 a, Vector2 b, float width, Color c)
        {
            if (!ClipSeg(ref a, ref b)) return;
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector2 n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
            Vector2 e = d / len * (width * 0.35f);       // slight overlap so joints don't crack
            Col(c);
            V(a - e + n); V(b + e + n); V(b + e - n); V(a - e - n);
        }

        /// <summary>Filled disc (fan of degenerate quads).</summary>
        public static void Disc(Vector2 c, float r, Color col, int sides = 14)
        {
            if (!clip.Contains(c)) return;
            Col(col);
            float step = Mathf.PI * 2f / sides;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * step, a1 = a0 + step;
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
                V(c); V(p0); V(p1); V(p1);
            }
        }

        /// <summary>Liang-Barsky clip of a segment to the batch rect.</summary>
        static bool ClipSeg(ref Vector2 a, ref Vector2 b)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            if (!Clip1(-d.x, a.x - clip.xMin, ref t0, ref t1) || !Clip1(d.x, clip.xMax - a.x, ref t0, ref t1) ||
                !Clip1(-d.y, a.y - clip.yMin, ref t0, ref t1) || !Clip1(d.y, clip.yMax - a.y, ref t0, ref t1)) return false;
            Vector2 a2 = a + d * t0, b2 = a + d * t1;
            a = a2; b = b2;
            return true;
        }

        static bool Clip1(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
    }
}
