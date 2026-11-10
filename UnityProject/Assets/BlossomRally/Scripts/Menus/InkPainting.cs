// InkPainting.cs - procedural Chinese ink-wash (shuimo) landscape painted on the CPU:
// layered misty mountains with brush texture, a pagoda (or lake pavilion) silhouette, a blossom
// branch, bamboo / tea terraces / lake ripples depending on the variant, a pale vermilion sun,
// a column of pseudo calligraphy, a red seal stamp and warm paper grain.
//
// The pixel work is pure C# (own noise, no Unity API), so it runs on a worker thread; only the
// final SetPixels32 / Apply happens on the main thread (Poll()). Where threads are unavailable it
// falls back to time-sliced steps on the main thread.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public sealed class InkPainting
    {
        public readonly int W, H, Variant;
        public Texture2D Texture { get; private set; }
        public bool Done { get { return Texture != null; } }
        /// <summary>Time the texture became ready (unscaled), for fade-ins.</summary>
        public float ReadyTime { get; private set; }

        readonly Color32[] px;
        readonly int seed;
        volatile bool pixelsReady;
        volatile bool failed;
        bool started;
        bool fallback;     // no worker thread: time-sliced on the main thread
        int row;
        bool prepared;

        // layers
        const int Layers = 4;
        float[][] ridge;                 // per layer, per column: ridge line (pixels from top)
        float[] layerDens = new float[Layers], mistStart = new float[Layers], mistLen = new float[Layers];
        Color[] layerTint = new Color[Layers];
        float[] ink, pink, red, water;   // overlay buffers
        Vector2 sun; float sunR;
        float lakeTop = 2f;              // lake surface (fraction of H), > 1 = none
        int terraceLayer = -1;

        static readonly Color Paper = new Color(0.955f, 0.925f, 0.85f);
        static readonly Color InkC = new Color(0.09f, 0.085f, 0.10f);
        static readonly Color SunC = new Color(0.86f, 0.30f, 0.22f);
        static readonly Color SealC = new Color(0.74f, 0.11f, 0.10f);

        public InkPainting(int w, int h, int variant, int seed)
        {
            W = w; H = h; Variant = variant; this.seed = seed;
            px = new Color32[w * h];
        }

        /// <summary>Kick off generation (worker thread when possible).</summary>
        public void Start()
        {
            if (started) return;
            started = true;
            bool threads = Application.platform != RuntimePlatform.WebGLPlayer;
            if (threads)
            {
                try
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { Prepare(); RunRows(0, H); pixelsReady = true; }
                        catch (System.Exception) { failed = true; }
                    });
                    return;
                }
                catch (System.Exception) { }
            }
            fallback = true;
        }

        /// <summary>Call every frame on the main thread: uploads when ready, or advances the fallback.</summary>
        public void Poll(float budgetMs)
        {
            if (Texture != null || !started) return;
            if (fallback && !pixelsReady)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    if (!prepared) { Prepare(); row = 0; }
                    while (row < H && sw.Elapsed.TotalMilliseconds < budgetMs) { RunRows(row, Mathf.Min(H, row + 8)); row += 8; }
                    if (row >= H) pixelsReady = true;
                }
                catch (System.Exception e) { Debug.LogException(e); failed = true; }
            }
            if (failed && !pixelsReady)
            {
                // something went wrong on the worker: paint plain paper so the screen still looks right
                for (int i = 0; i < px.Length; i++) px[i] = Paper;
                pixelsReady = true;
            }
            if (pixelsReady)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, true)
                {
                    name = "BR_InkPainting" + Variant, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2
                };
                t.SetPixels32(px);
                t.Apply(true, true);
                Texture = t;
                ReadyTime = Time.unscaledTime;
            }
        }

        // ================================================================== composition
        void Prepare()
        {
            if (prepared) return;
            prepared = true;
            var rng = new System.Random(seed);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            ink = new float[W * H];
            pink = new float[W * H];
            red = new float[W * H];
            water = null;

            // ---- mountain layers (far -> near) ----
            float[] baseY, amp;
            if (Variant == 1) { baseY = new[] { 0.44f, 0.54f, 0.64f, 0.74f }; amp = new[] { 0.22f, 0.20f, 0.16f, 0.10f }; lakeTop = 0.74f; }
            else if (Variant == 2) { baseY = new[] { 0.40f, 0.52f, 0.66f, 0.86f }; amp = new[] { 0.22f, 0.20f, 0.20f, 0.18f }; terraceLayer = 3; }
            else { baseY = new[] { 0.38f, 0.50f, 0.66f, 0.86f }; amp = new[] { 0.24f, 0.24f, 0.26f, 0.22f }; }
            Color[] tints =
            {
                new Color(0.55f, 0.60f, 0.68f), new Color(0.38f, 0.42f, 0.48f), new Color(0.20f, 0.24f, 0.26f), new Color(0.10f, 0.12f, 0.12f)
            };
            if (Variant == 2) tints[3] = new Color(0.20f, 0.30f, 0.22f);
            float[] dens = { 0.30f, 0.45f, 0.65f, 0.85f };
            ridge = new float[Layers][];
            int pagodaLayer = Variant == 1 ? 3 : 2;
            float pagodaU = Variant == 1 ? R(0.16f, 0.28f) : R(0.58f, 0.72f);
            for (int k = 0; k < Layers; k++)
            {
                layerDens[k] = dens[k];
                layerTint[k] = tints[k];
                mistStart[k] = H * (0.025f + k * 0.02f);
                mistLen[k] = H * (0.07f + k * 0.035f);
                var r = new float[W];
                int peaks = 3 + rng.Next(3);
                var pc = new float[peaks]; var ph = new float[peaks]; var pw = new float[peaks];
                for (int p = 0; p < peaks; p++)
                {
                    pc[p] = R(-0.1f, 1.1f);
                    ph[p] = R(0.45f, 1f);
                    pw[p] = R(0.06f, 0.15f) * (k == 3 ? 1.6f : 1f);
                }
                if (k == pagodaLayer) { pc[0] = pagodaU; ph[0] = 1f; pw[0] = Variant == 1 ? 0.14f : 0.09f; }
                if (k == 0 && Variant != 1) { pc[1] = R(0.3f, 0.45f); ph[1] = 1f; pw[1] = 0.07f; }    // a tall far spire
                int ns = seed + k * 101;
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    float sh = 0f;
                    for (int p = 0; p < peaks; p++)
                    {
                        float d = (u - pc[p]) / pw[p];
                        float bump = ph[p] * Mathf.Exp(-d * d * 1.6f);
                        // steep "shuimo" peaks: sharpen the upper part a little
                        bump = Mathf.Pow(bump, 0.85f);
                        if (bump > sh) sh = Mathf.Max(sh, bump);
                    }
                    sh = Mathf.Max(sh, 0.06f) + (Noise.Fbm(u * 9f, k * 3.7f, ns, 4) - 0.5f) * 0.16f;
                    r[x] = H * (baseY[k] - amp[k] * sh);
                }
                ridge[k] = r;
            }

            // ---- sun / moon ----
            sun = Variant == 1 ? new Vector2(W * 0.74f, H * 0.18f) : Variant == 2 ? new Vector2(W * 0.22f, H * 0.17f) : new Vector2(W * 0.80f, H * 0.20f);
            sunR = H * (Variant == 1 ? 0.055f : 0.07f);

            // ---- pagoda / pavilion on its peak ----
            {
                int pxu = Mathf.Clamp((int)(pagodaU * W), 0, W - 1);
                // find the local top near the chosen column
                int best = pxu;
                for (int x = Mathf.Max(0, pxu - W / 40); x < Mathf.Min(W, pxu + W / 40); x++) if (ridge[pagodaLayer][x] < ridge[pagodaLayer][best]) best = x;
                float baseLine = ridge[pagodaLayer][best] + H * 0.006f;
                if (Variant == 1) Pagoda(best, baseLine, H * 0.085f, H * 0.075f, 2, 0.88f);
                else Pagoda(best, baseLine, H * 0.19f, H * 0.045f, 5, 0.78f + pagodaLayer * 0.05f);
            }

            // ---- variant extras ----
            if (Variant == 1)
            {
                water = new float[W * H];
                // little boat with a fisherman
                float bx = W * R(0.52f, 0.62f), by = H * 0.84f, bl = H * 0.07f;
                InkRaster.Poly(ink, W, H, new[]
                {
                    new Vector2(bx - bl, by - bl * 0.18f), new Vector2(bx + bl, by - bl * 0.22f), new Vector2(bx + bl * 0.6f, by + bl * 0.08f), new Vector2(bx - bl * 0.7f, by + bl * 0.08f)
                }, 0.92f);
                InkRaster.Poly(ink, W, H, new[] { new Vector2(bx - bl * 0.05f, by - bl * 0.18f), new Vector2(bx + bl * 0.2f, by - bl * 0.18f), new Vector2(bx + bl * 0.08f, by - bl * 0.62f) }, 0.9f);
                InkRaster.Poly(ink, W, H, new[] { new Vector2(bx - bl * 0.25f, by - bl * 0.6f), new Vector2(bx + bl * 0.42f, by - bl * 0.6f), new Vector2(bx + bl * 0.08f, by - bl * 0.8f) }, 0.95f);
                InkRaster.Stroke(ink, W, H, new Vector2(bx + bl * 0.2f, by - bl * 0.45f), new Vector2(bx + bl * 1.9f, by - bl * 1.2f), H * 0.0022f, H * 0.0012f, 0.8f, seed + 3);
                InkRaster.Stroke(ink, W, H, new Vector2(bx + bl * 1.9f, by - bl * 1.2f), new Vector2(bx + bl * 1.95f, by + bl * 0.3f), H * 0.0008f, H * 0.0008f, 0.5f, seed + 4);
                Branch(new Vector2(W * 1.01f, H * 0.16f), Mathf.PI * 0.94f, H * 0.022f, 0, rng);
                Birds(rng, new Vector2(W * 0.40f, H * 0.18f), 5);
            }
            else if (Variant == 2)
            {
                Bamboo(rng);
                Branch(new Vector2(W * 1.02f, H * 0.04f), Mathf.PI * 0.82f, H * 0.018f, 0, rng);
                Birds(rng, new Vector2(W * 0.55f, H * 0.22f), 7);
            }
            else
            {
                Branch(new Vector2(-W * 0.02f, H * 0.06f), Mathf.PI * 0.10f, H * 0.024f, 0, rng);
                Birds(rng, new Vector2(W * 0.48f, H * 0.16f), 4);
            }

            // ---- calligraphy column + seal ----
            float colX = Variant == 0 ? W * 0.93f : W * 0.07f, colY = H * 0.30f, cs = H * 0.05f;
            if (Variant == 2) colX = W * 0.93f;
            for (int c = 0; c < 4; c++) Glyph(rng, new Vector2(colX, colY + c * cs * 1.25f), cs);
            InkRaster.SealStamp(red, W, H, colX, colY + 4 * cs * 1.25f + cs * 0.2f, cs * 0.95f, seed + 77);
            InkRaster.SealStamp(red, W, H, W * (Variant == 1 ? 0.92f : 0.08f) + 0f, H * 0.90f, H * 0.075f, seed + 91);
        }

        // ---------------------------------------------------------------- brushwork
        void Pagoda(float cx, float baseY, float height, float width, int tiers, float val)
        {
            float th = height / (tiers + 1.2f);
            float y = baseY;
            // stone platform
            InkRaster.Poly(ink, W, H, new[]
            {
                new Vector2(cx - width * 0.9f, y), new Vector2(cx + width * 0.9f, y), new Vector2(cx + width * 0.75f, y - th * 0.25f), new Vector2(cx - width * 0.75f, y - th * 0.25f)
            }, val);
            y -= th * 0.25f;
            for (int i = 0; i < tiers; i++)
            {
                float k = 1f - i * (tiers > 3 ? 0.12f : 0.2f);
                float bw = width * 0.62f * k, rw = width * 1.18f * k;
                float bodyH = th * 0.5f;
                InkRaster.Poly(ink, W, H, new[]
                {
                    new Vector2(cx - bw * 0.5f, y), new Vector2(cx + bw * 0.5f, y), new Vector2(cx + bw * 0.5f, y - bodyH), new Vector2(cx - bw * 0.5f, y - bodyH)
                }, val * 0.9f);
                float ry = y - bodyH;
                // roof with upturned eaves
                InkRaster.Poly(ink, W, H, new[]
                {
                    new Vector2(cx - rw * 0.5f, ry - th * 0.24f), new Vector2(cx - rw * 0.36f, ry + th * 0.02f), new Vector2(cx, ry + th * 0.06f),
                    new Vector2(cx + rw * 0.36f, ry + th * 0.02f), new Vector2(cx + rw * 0.5f, ry - th * 0.24f),
                    new Vector2(cx + bw * 0.34f, ry - th * 0.42f), new Vector2(cx - bw * 0.34f, ry - th * 0.42f)
                }, val);
                y = ry - th * 0.42f;
            }
            // finial
            InkRaster.Stroke(ink, W, H, new Vector2(cx, y), new Vector2(cx, y - th * 0.9f), Mathf.Max(0.8f, width * 0.05f), Mathf.Max(0.6f, width * 0.025f), val, seed + 5);
            for (int i = 0; i < 3; i++) InkRaster.Disc(ink, W, H, cx, y - th * (0.2f + i * 0.22f), Mathf.Max(0.8f, width * 0.07f), val);
        }

        void Branch(Vector2 start, float angle, float thick, int depth, System.Random rng)
        {
            Vector2 p = start;
            int segs = depth == 0 ? 11 : (depth == 1 ? 6 : 3);
            float segLen = H * (depth == 0 ? 0.045f : depth == 1 ? 0.032f : 0.02f);
            float t = thick;
            for (int i = 0; i < segs; i++)
            {
                angle += ((float)rng.NextDouble() - 0.5f) * 0.7f + (depth == 0 ? 0.03f : 0f);
                Vector2 q = p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * segLen * (0.7f + (float)rng.NextDouble() * 0.6f);
                float t2 = t * 0.86f;
                InkRaster.Stroke(ink, W, H, p, q, t, t2, 0.93f, seed + depth * 7 + i);
                // knots
                if (rng.NextDouble() < 0.12) InkRaster.Disc(ink, W, H, q.x, q.y, t * 1.02f, 0.95f);
                if (depth < 2 && rng.NextDouble() < (depth == 0 ? 0.55 : 0.4) && i > 0)
                    Branch(q, angle + (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + (float)rng.NextDouble() * 0.5f), t2 * 0.6f, depth + 1, rng);
                if (i > 1 && rng.NextDouble() < 0.6) Blossom(q + new Vector2(((float)rng.NextDouble() - 0.5f) * t * 6f, ((float)rng.NextDouble() - 0.5f) * t * 6f), rng);
                p = q;
                t = t2;
            }
            Blossom(p, rng);
            if (rng.NextDouble() < 0.7) Blossom(p + new Vector2(H * 0.012f, -H * 0.01f), rng);
        }

        void Blossom(Vector2 c, System.Random rng)
        {
            float r = H * (0.007f + (float)rng.NextDouble() * 0.006f);
            if (rng.NextDouble() < 0.25)
            {
                InkRaster.Disc(pink, W, H, c.x, c.y, r * 0.55f, 1f);     // bud
                InkRaster.Disc(red, W, H, c.x, c.y, r * 0.25f, 0.6f);
                return;
            }
            float rot = (float)rng.NextDouble() * 6.28f;
            for (int k = 0; k < 5; k++)
            {
                float a = rot + k * Mathf.PI * 0.4f;
                InkRaster.Disc(pink, W, H, c.x + Mathf.Cos(a) * r * 0.85f, c.y + Mathf.Sin(a) * r * 0.85f, r * 0.72f, 0.92f);
            }
            InkRaster.Disc(red, W, H, c.x, c.y, r * 0.32f, 0.85f);
            for (int k = 0; k < 5; k++)
            {
                float a = rot + 0.3f + k * Mathf.PI * 0.4f;
                InkRaster.Disc(ink, W, H, c.x + Mathf.Cos(a) * r * 0.6f, c.y + Mathf.Sin(a) * r * 0.6f, Mathf.Max(0.6f, r * 0.09f), 0.7f);
            }
        }

        void Birds(System.Random rng, Vector2 centre, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 c = centre + new Vector2(((float)rng.NextDouble() - 0.5f) * W * 0.16f, ((float)rng.NextDouble() - 0.5f) * H * 0.08f);
                float s = H * (0.006f + (float)rng.NextDouble() * 0.006f), w = Mathf.Max(0.6f, H * 0.0012f);
                InkRaster.Stroke(ink, W, H, c + new Vector2(-s, -s * 0.5f), c, w, w * 1.3f, 0.85f, seed + i);
                InkRaster.Stroke(ink, W, H, c, c + new Vector2(s, -s * 0.6f), w * 1.3f, w, 0.85f, seed + i + 50);
            }
        }

        void Bamboo(System.Random rng)
        {
            for (int s = 0; s < 4; s++)
            {
                float x = W * (0.03f + s * 0.05f + (float)rng.NextDouble() * 0.03f);
                float w = H * (0.008f + (float)rng.NextDouble() * 0.006f);
                float lean = ((float)rng.NextDouble() - 0.3f) * W * 0.04f;
                float y = H * 1.02f, top = H * (0.05f + (float)rng.NextDouble() * 0.2f);
                float node = H * (0.07f + (float)rng.NextDouble() * 0.03f);
                float val = s % 2 == 0 ? 0.9f : 0.55f;
                while (y > top)
                {
                    float y2 = Mathf.Max(top, y - node);
                    float f0 = 1f - (y - top) / (H - top), f1 = 1f - (y2 - top) / (H - top);
                    Vector2 a = new Vector2(x + lean * f0, y - node * 0.04f), b = new Vector2(x + lean * f1, y2 + node * 0.06f);
                    InkRaster.Stroke(ink, W, H, a, b, w, w * 0.92f, val, seed + s * 13 + (int)y);
                    // node ring
                    InkRaster.Stroke(ink, W, H, b + new Vector2(-w * 1.2f, 0f), b + new Vector2(w * 1.2f, 0f), w * 0.3f, w * 0.3f, Mathf.Min(1f, val + 0.1f), seed + 2);
                    // leaf clusters near the top
                    if (y2 < H * 0.55f && rng.NextDouble() < 0.6)
                        for (int l = 0; l < 3; l++)
                        {
                            float ang = (float)(rng.NextDouble() * 1.2 + (rng.NextDouble() < 0.5 ? 0.2 : 1.8));
                            float len = H * (0.05f + (float)rng.NextDouble() * 0.04f);
                            Vector2 tip = b + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f + 0.35f) * len;
                            Vector2 mid = Vector2.Lerp(b, tip, 0.4f);
                            Vector2 nrm = new Vector2(-(tip - b).y, (tip - b).x).normalized * len * 0.12f;
                            InkRaster.Poly(ink, W, H, new[] { b, mid + nrm, tip, mid - nrm }, val * 0.95f);
                        }
                    y = y2;
                }
            }
        }

        void Glyph(System.Random rng, Vector2 c, float size)
        {
            int n = 3 + rng.Next(4);
            float w = Mathf.Max(0.8f, size * 0.07f);
            for (int i = 0; i < n; i++)
            {
                int kind = rng.Next(4);
                Vector2 a, b;
                float ox = ((float)rng.NextDouble() - 0.5f) * size * 0.6f, oy = ((float)rng.NextDouble() - 0.5f) * size * 0.6f;
                switch (kind)
                {
                    case 0: a = c + new Vector2(-size * 0.4f, oy); b = c + new Vector2(size * 0.4f, oy + size * 0.04f); break;    // heng
                    case 1: a = c + new Vector2(ox, -size * 0.45f); b = c + new Vector2(ox, size * 0.4f); break;                  // shu
                    case 2: a = c + new Vector2(ox * 0.3f, oy * 0.3f); b = a + new Vector2(-size * 0.38f, size * 0.38f); break;   // pie
                    default: a = c + new Vector2(ox * 0.3f, oy * 0.3f); b = a + new Vector2(size * 0.36f, size * 0.36f); break;   // na
                }
                InkRaster.Stroke(ink, W, H, a, b, w * 1.2f, w * 0.7f, 0.88f, seed + i * 3);
            }
        }

        // ================================================================== pixels
        void RunRows(int y0, int y1)
        {
            float aspect = W / (float)H;
            for (int y = y0; y < y1; y++)
            {
                float v = y / (float)H;
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W;
                    int i = y * W + x;
                    // paper base, slightly warmer towards the bottom
                    float r = Paper.r, g = Paper.g, b = Paper.b - v * 0.02f;

                    // vermilion sun / moon wash
                    float dx = x - sun.x, dy = y - sun.y;
                    float ds = Mathf.Sqrt(dx * dx + dy * dy);
                    if (ds < sunR * 1.4f)
                    {
                        float a = Mathf.Clamp01((sunR - ds) / (sunR * 0.12f)) * 0.55f + Mathf.Clamp01(1f - ds / (sunR * 1.4f)) * 0.06f;
                        Mix(ref r, ref g, ref b, SunC, a);
                    }

                    // mountains: translucent washes, darkest along the ridge, fading into mist
                    for (int k = 0; k < Layers; k++)
                    {
                        float top = ridge[k][x];
                        if (y < top) continue;
                        float depth = y - top;
                        float edge = Mathf.Exp(-depth / (H * 0.012f));
                        float body = 1f - Smooth(mistStart[k], mistStart[k] + mistLen[k], depth);
                        if (body <= 0.001f && edge < 0.01f) continue;
                        // vertical "hemp fibre" texture strokes + broad tonal variation
                        float tex = Noise.Value(u * 230f, v * 7f + k * 5.1f, seed + k * 17);
                        float tone = Noise.Value(u * 6f, v * 5f, seed + k * 29);
                        float a = layerDens[k] * (0.34f + 0.5f * edge + 0.16f * tex) * (0.65f + 0.5f * tone) * body;
                        a = Mathf.Clamp01(a + edge * 0.35f * layerDens[k]);
                        if (terraceLayer == k)
                        {
                            float tl = (depth / (H * 0.022f)) + Noise.Value(u * 3f, 0.5f, seed + 3) * 3f + u * 1.5f;
                            float fr = tl - Mathf.Floor(tl);
                            a = Mathf.Clamp01(a * 0.75f + (fr < 0.12f ? 0.35f * body : 0f));
                        }
                        Mix(ref r, ref g, ref b, layerTint[k], a);
                    }

                    // horizontal mist bands drifting across
                    float mist = Noise.Fbm(u * 2.2f * aspect, v * 9f, seed + 400, 3);
                    float band = Mathf.Clamp01((mist - 0.52f) * 3f) * (0.35f + 0.4f * Smooth(0.3f, 0.75f, v));
                    Mix(ref r, ref g, ref b, Paper, band * 0.8f);

                    // lake: pale water with ink ripple lines and a soft shore
                    if (v > lakeTop)
                    {
                        float lv = (v - lakeTop) / (1f - lakeTop);
                        Mix(ref r, ref g, ref b, new Color(0.90f, 0.90f, 0.86f), 0.85f * Smooth(0f, 0.05f, lv));
                        float rip = Mathf.Sin((v * H) * 0.55f + Noise.Value(u * 8f, v * 30f, seed + 9) * 6f);
                        float seg = Noise.Value(u * 12f, v * 60f, seed + 11);
                        if (rip > 0.92f && seg > 0.55f) Mix(ref r, ref g, ref b, InkC, 0.22f * (1f - lv * 0.5f));
                        if (lv < 0.06f) Mix(ref r, ref g, ref b, InkC, 0.12f * (1f - lv / 0.06f) * Noise.Value(u * 40f, 0f, seed + 13));
                    }

                    // brushwork overlays
                    float ia = ink[i];
                    if (ia > 0f) Mix(ref r, ref g, ref b, InkC, ia);
                    float pa = pink[i];
                    if (pa > 0f)
                    {
                        float pv = Noise.Value(x * 0.08f, y * 0.08f, seed + 31);
                        var pc = new Color(Mathf.Lerp(0.93f, 0.99f, pv), Mathf.Lerp(0.55f, 0.80f, pv), Mathf.Lerp(0.66f, 0.84f, pv));
                        Mix(ref r, ref g, ref b, pc, pa * 0.9f);
                    }
                    float ra = red[i];
                    if (ra > 0f) Mix(ref r, ref g, ref b, SealC, ra * 0.92f);

                    // paper grain, fibres and a soft aged vignette
                    float grain = 0.965f + 0.045f * Noise.Value(x * 0.9f, y * 0.9f, seed + 501) + 0.02f * (Noise.Hash01(x, y, seed) - 0.5f);
                    float fib = Noise.Value(x * 0.02f, y * 0.35f, seed + 502);
                    grain -= Mathf.Clamp01((fib - 0.8f) * 5f) * 0.025f;
                    float vx = u * 2f - 1f, vy = v * 2f - 1f;
                    float vig = 1f - 0.16f * Mathf.Clamp01((vx * vx * 0.7f + vy * vy) - 0.35f);
                    float k2 = grain * vig;
                    px[(H - 1 - y) * W + x] = new Color32(B(r * k2), B(g * k2), B(b * k2 * 0.995f), 255);
                }
            }
        }

        static void Mix(ref float r, ref float g, ref float b, Color c, float a)
        {
            r += (c.r - r) * a; g += (c.g - g) * a; b += (c.b - b) * a;
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static byte B(float v) { return (byte)Mathf.Clamp(v * 255f + 0.5f, 0f, 255f); }
    }
}
