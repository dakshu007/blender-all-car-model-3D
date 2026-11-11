// ValleyMap.cs - interactive valley map for choosing an event.
//
// Once the valley is built, an orthographic camera renders it from straight above into a
// RenderTexture; the pixels are read back and repainted on worker threads in an ink-and-wash
// style (edge ink lines, height contours, watercolour on paper grain). The map is shown as an
// unrolling hanging scroll: stage routes are drawn as coloured paths (from WorldBuilder road
// samples via StageCourse), with start / finish flags, checkpoints, best medals and times, an
// animated trace of the selected route and a camera that flies / zooms to each route.
//
// API:  ValleyMap.Open(i => ..., () => ...)   ValleyMap.IsOpen   ValleyMap.Close()
// Input: up/down (stick, d-pad, W/S) choose, ENTER / A race, ESC / B back, mouse wheel / +- / LT RT zoom,
//        drag or right stick to pan, TAB / Y whole-valley overview, click a route or card to select, click again to race.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(950)]
    public class ValleyMap : MonoBehaviour
    {
        public static ValleyMap Instance { get; private set; }
        public static bool IsOpen { get { return Instance != null && Instance.open; } }
        /// <summary>True once the painted map texture exists.</summary>
        public static bool Ready { get { return Instance != null && Instance.mapTex != null; } }
        public static Texture2D MapTexture { get { return Instance != null ? Instance.mapTex : null; } }

        class Route
        {
            public int index;
            public StageDef def;
            public readonly List<Vector2> pts = new List<Vector2>();
            public Vector2[] gates;
            public float length;
            public bool closed;
            public Color col;
            public Vector2 min, max;
            public string surface;
        }

        bool open;
        int openFrame;
        float openTime, closeTime = -10f;
        System.Action<int> onPick;
        System.Action onBack;
        int selected;
        float selectTime;
        Texture2D mapTex;
        bool rendering;
        WorldBuilder world;
        readonly List<Route> routes = new List<Route>();

        // view (world metres)
        Vector2 viewC = Vector2.zero, panOffset;
        // the map covers the whole six-region world: a square of MapSize metres centred on MapC
        static readonly Vector2 MapC = new Vector2(0f, 500f);
        const float MapSize = 3000f;
        float viewSize = MapSize, zoomMul = 1f;
        bool overview;
        Rect mapRect, listRect;
        Vector2 dragStart, lastMouse;
        bool dragging, dragMoved;
        int hoverRoute = -1;
        float listScroll;
        Vector2 lastHoverMouse = new Vector2(-1f, -1f);
        bool mouseMoved;      // IMGUI only sends MouseMove in the editor: detect movement on Repaint instead

        static readonly Color[] RouteCols =
        {
            new Color(1f, 0.52f, 0.70f), new Color(0.93f, 0.30f, 0.24f), new Color(0.46f, 0.76f, 0.38f),
            new Color(0.22f, 0.68f, 0.62f), new Color(1f, 0.76f, 0.26f), new Color(0.62f, 0.52f, 0.92f)
        };

        static bool hooked;

        void Awake()
        {
            Instance = this;
            if (!hooked)
            {
                hooked = true;
                WorldHooks.WorldBuilt += w => { if (Instance != null) Instance.OnWorld(w); };
            }
        }

        void Start()
        {
            if (WorldHooks.World != null && world == null) OnWorld(WorldHooks.World);
        }

        void OnWorld(WorldBuilder w)
        {
            if (w == null || world == w) return;
            world = w;
            BuildRoutes();
            if (!rendering) StartCoroutine(RenderMap());
        }

        // ================================================================== public API
        /// <summary>Open the map. onPick(eventIndex) is called when the player chooses a stage, onBack when they leave.</summary>
        public static void Open(System.Action<int> onPick, System.Action onBack)
        {
            if (Instance == null) return;
            Instance.DoOpen(onPick, onBack);
        }

        public static void Close()
        {
            if (Instance != null && Instance.open) { Instance.open = false; Instance.closeTime = Time.unscaledTime; }
        }

        void DoOpen(System.Action<int> pick, System.Action back)
        {
            if (world == null && WorldHooks.World != null) OnWorld(WorldHooks.World);
            else if (routes.Count == 0 && world != null) BuildRoutes();
            onPick = pick;
            onBack = back;
            open = true;
            openFrame = Time.frameCount;
            openTime = Time.unscaledTime;
            if (routes.Count > 0) selected = Mathf.Clamp(selected, 0, routes.Count - 1);
            overview = false;
            zoomMul = 1f;
            panOffset = Vector2.zero;
            viewC = MapC;
            viewSize = MapSize;
            selectTime = Time.unscaledTime + 0.45f;    // trace starts once the scroll has unrolled
            dragging = false;
            UI.Sound("Whoosh");
        }

        void Select(int i, bool sound = true)
        {
            if (routes.Count == 0) return;
            i = (i % routes.Count + routes.Count) % routes.Count;
            if (i == selected) return;
            selected = i;
            selectTime = Time.unscaledTime;
            panOffset = Vector2.zero;
            zoomMul = 1f;
            overview = false;
            if (sound) UI.Sound("MenuMove");
        }

        void Pick()
        {
            if (routes.Count == 0) return;
            int ev = routes[selected].index;
            UI.Sound("MenuSelect");
            open = false;
            closeTime = Time.unscaledTime;
            var cb = onPick;
            onPick = null; onBack = null;
            if (cb != null) cb(ev);
        }

        void Back()
        {
            UI.Sound("MenuBack");
            open = false;
            closeTime = Time.unscaledTime;
            var cb = onBack;
            onPick = null; onBack = null;
            if (cb != null) cb();
        }

        // ================================================================== routes
        void BuildRoutes()
        {
            routes.Clear();
            if (world == null) return;
            for (int i = 0; i < StageDef.All.Length; i++)
            {
                var def = StageDef.All[i];
                StageCourse c = null;
                try { c = StageCourse.Build(def, world, false); }
                catch (System.Exception e) { Debug.LogException(e); }
                if (c == null) continue;
                var r = new Route { index = i, def = def, length = c.length, closed = c.closed, col = RouteCols[i % RouteCols.Length] };
                r.surface = def.road < world.roads.Count ? world.roads[def.road].surface.ToString().ToLowerInvariant() : "";
                for (float d = c.startD; d <= c.endD + 0.01f; d += 6f)
                {
                    var rs = c.samples[c.IndexAt(d)];
                    if (r.pts.Count == 0 || (rs.pos - r.pts[r.pts.Count - 1]).sqrMagnitude > 4f) r.pts.Add(rs.pos);
                }
                var last = c.samples[c.IndexAt(c.endD)].pos;
                if (r.pts.Count == 0 || (last - r.pts[r.pts.Count - 1]).sqrMagnitude > 1f) r.pts.Add(last);
                r.gates = new Vector2[c.gates.Count];
                for (int g = 0; g < c.gates.Count; g++) r.gates[g] = new Vector2(c.gates[g].centre.x, c.gates[g].centre.z);
                r.min = new Vector2(float.MaxValue, float.MaxValue);
                r.max = new Vector2(float.MinValue, float.MinValue);
                foreach (var p in r.pts) { r.min = Vector2.Min(r.min, p); r.max = Vector2.Max(r.max, p); }
                routes.Add(r);
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, routes.Count - 1));
        }

        // ================================================================== map render
        IEnumerator RenderMap()
        {
            rendering = true;
            yield return null;
            yield return null;
            if (world == null) { rendering = false; yield break; }
            int res = GameSettings.Quality >= 2 ? 3072 : 2048;
            Color32[] src = null;
            try { src = Capture(res); }
            catch (System.Exception e) { Debug.LogException(e); src = null; }
            yield return null;

            // terrain heights + water on a coarse grid (main thread: WorldBuilder isn't thread-safe to call)
            const int G = 384;
            var hgrid = new float[G * G];
            var wgrid = new float[G * G];
            for (int j = 0; j < G; j++)
            {
                for (int i = 0; i < G; i++)
                {
                    float x = MapC.x - MapSize * 0.5f + (i + 0.5f) / G * MapSize, z = MapC.y - MapSize * 0.5f + (j + 0.5f) / G * MapSize;
                    hgrid[j * G + i] = world.TerrainHeight(x, z);
                    var p = new Vector2(x, z);
                    float wd = Mathf.Min(world.RiverDistance(p) - 7f, (MapData.LakeK(p) - 1f) * 30f);
                    wgrid[j * G + i] = Mathf.Clamp01(1f - wd / 4f);
                }
                if ((j & 15) == 15) yield return null;
            }
            if (src != null && !LooksValid(src)) src = null;

            var outPx = new Color32[res * res];
            int pending = 4;
            int rows = res / 4;
            for (int k = 0; k < 4; k++)
            {
                int r0 = k * rows, r1 = k == 3 ? res : (k + 1) * rows;
                System.Threading.WaitCallback job = _ =>
                {
                    try { Paint(src, outPx, res, hgrid, wgrid, G, r0, r1); }
                    catch (System.Exception) { }
                    System.Threading.Interlocked.Decrement(ref pending);
                };
                bool queued = false;
                if (Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    try { queued = System.Threading.ThreadPool.QueueUserWorkItem(job); }
                    catch (System.Exception) { queued = false; }
                }
                if (!queued) { job(null); yield return null; }
            }
            while (System.Threading.Volatile.Read(ref pending) > 0) yield return null;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true)
            {
                name = "BR_ValleyMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            tex.SetPixels32(outPx);
            tex.Apply(true, true);
            mapTex = tex;
            rendering = false;
        }

        Color32[] Capture(int res)
        {
            var go = new GameObject("BR_MapCamera");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = MapSize * 0.5f;
            cam.aspect = 1f;
            cam.transform.SetPositionAndRotation(new Vector3(MapC.x, 900f, MapC.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.70f, 0.52f);
            cam.cullingMask = ~((1 << WorldBuilder.LayerGrass) | (1 << CarExtras.CarLayer) | (1 << 31));
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depthTextureMode = DepthTextureMode.Depth;

            bool fog = RenderSettings.fog;
            float lodBias = QualitySettings.lodBias;
            Light sun = Atmosphere.Sun;
            LightShadows sunShadows = sun != null ? sun.shadows : LightShadows.None;
            Texture cookie = sun != null ? sun.cookie : null;
            var rt = RenderTexture.GetTemporary(res, res, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prevActive = RenderTexture.active;
            RegionRuntime.ShowAll(true);
            try
            {
                RenderSettings.fog = false;
                QualitySettings.lodBias = Mathf.Max(lodBias, 6f);
                if (sun != null) { sun.shadows = LightShadows.None; sun.cookie = null; }
                cam.targetTexture = rt;
                cam.Render();
                var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, res, res), 0, 0, false);
                tex.Apply(false);
                var px = tex.GetPixels32();
                Destroy(tex);
                return px;
            }
            finally
            {
                RegionRuntime.ShowAll(false);
                RenderTexture.active = prevActive;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                RenderSettings.fog = fog;
                QualitySettings.lodBias = lodBias;
                if (sun != null) { sun.shadows = sunShadows; sun.cookie = cookie; }
                Destroy(go);
            }
        }

        static bool LooksValid(Color32[] px)
        {
            double sum = 0, sum2 = 0;
            int n = 0;
            for (int i = 0; i < px.Length; i += 97)
            {
                float l = (px[i].r * 0.3f + px[i].g * 0.59f + px[i].b * 0.11f) / 255f;
                sum += l; sum2 += l * l; n++;
            }
            double mean = sum / n, var = sum2 / n - mean * mean;
            return mean > 0.03 && var > 0.0004;
        }

        /// <summary>Ink-and-wash repaint of the rendered map (worker thread; no Unity API).</summary>
        static void Paint(Color32[] src, Color32[] dst, int res, float[] hg, float[] wg, int G, int r0, int r1)
        {
            var paper = new Color(0.955f, 0.925f, 0.85f);
            var inkC = new Color(0.16f, 0.12f, 0.12f);
            var sepia = new Color(0.45f, 0.32f, 0.22f);
            float cell = res / (float)G;
            float mPerPx = MapSize / res;
            for (int y = r0; y < r1; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // heights / water (bilinear from the grid)
                    float gx = (x + 0.5f) / cell - 0.5f, gy = (y + 0.5f) / cell - 0.5f;
                    float h = Bilinear(hg, G, gx, gy), wat = Bilinear(wg, G, gx, gy);
                    float hx = Bilinear(hg, G, gx + 1f / cell, gy) - h, hy = Bilinear(hg, G, gx, gy + 1f / cell) - h;

                    float r, g, b, line = 0f;
                    if (src != null)
                    {
                        Color32 c = src[y * res + x];
                        r = c.r / 255f; g = c.g / 255f; b = c.b / 255f;
                        // Sobel on luminance -> ink outlines
                        float l00 = L(src, res, x - 1, y - 1), l10 = L(src, res, x, y - 1), l20 = L(src, res, x + 1, y - 1);
                        float l01 = L(src, res, x - 1, y), l21 = L(src, res, x + 1, y);
                        float l02 = L(src, res, x - 1, y + 1), l12 = L(src, res, x, y + 1), l22 = L(src, res, x + 1, y + 1);
                        float sx = (l20 + 2f * l21 + l22) - (l00 + 2f * l01 + l02);
                        float sy = (l02 + 2f * l12 + l22) - (l00 + 2f * l10 + l20);
                        float e = Mathf.Sqrt(sx * sx + sy * sy);
                        line = Smooth(0.18f, 0.55f, e) * 0.55f;
                        // watercolour pigment: gentle desaturation, lifted shadows
                        float lum = r * 0.3f + g * 0.59f + b * 0.11f;
                        r = Mathf.Lerp(lum, r, 1.05f); g = Mathf.Lerp(lum, g, 1.05f); b = Mathf.Lerp(lum, b, 1.05f);
                        r = 0.22f + 0.85f * Mathf.Clamp01(r); g = 0.22f + 0.85f * Mathf.Clamp01(g); b = 0.22f + 0.85f * Mathf.Clamp01(b);
                    }
                    else
                    {
                        // fallback: tint by height + hillshade
                        float hn = Mathf.Clamp01((h + 2f) / 50f);
                        r = Mathf.Lerp(0.70f, 0.78f, hn); g = Mathf.Lerp(0.84f, 0.76f, hn); b = Mathf.Lerp(0.58f, 0.66f, hn);
                        float shade = Mathf.Clamp(1f + (-hx + hy) * 0.35f / mPerPx * 0.25f, 0.75f, 1.15f);
                        r *= shade; g *= shade; b *= shade;
                        if (wat > 0.5f) { r = 0.66f; g = 0.80f; b = 0.86f; }
                        line = wat > 0.45f && wat < 0.6f ? 0.5f : 0f;
                    }
                    // water: cool ink wash with soft ripple lines
                    if (wat > 0.02f)
                    {
                        float wk = Mathf.Clamp01(wat);
                        r = Mathf.Lerp(r, 0.62f, wk * 0.45f); g = Mathf.Lerp(g, 0.76f, wk * 0.45f); b = Mathf.Lerp(b, 0.82f, wk * 0.45f);
                        float rip = Mathf.Sin(y * 0.9f + Noise.Value(x * 0.05f, y * 0.02f, 77) * 9f);
                        if (rip > 0.94f && wk > 0.8f) line = Mathf.Max(line, 0.14f);
                    }
                    // height contours every 5 m (each 4th line darker)
                    float grad = Mathf.Sqrt(hx * hx + hy * hy);
                    if (grad > 1e-4f && wat < 0.3f)
                    {
                        float f = h / 5f;
                        float d = Mathf.Abs(f - Mathf.Round(f)) * 5f / grad;    // distance to the contour in pixels
                        if (d < 0.9f)
                        {
                            bool major = Mathf.Abs(Mathf.Round(f)) % 4f < 0.5f;
                            float a = (1f - d / 0.9f) * (major ? 0.32f : 0.16f);
                            r = Mathf.Lerp(r, sepia.r, a); g = Mathf.Lerp(g, sepia.g, a); b = Mathf.Lerp(b, sepia.b, a);
                        }
                    }
                    // pigment multiplied onto paper with grain
                    float grain = 0.95f + 0.06f * Noise.Value(x * 0.7f, y * 0.7f, 501) + 0.02f * (Noise.Hash01(x, y, 3) - 0.5f);
                    float bloom = 0.92f + 0.08f * Noise.Value(x * 0.012f, y * 0.012f, 909);    // uneven wash
                    r = paper.r * r * grain * bloom; g = paper.g * g * grain * bloom; b = paper.b * b * grain * bloom;
                    r = Mathf.Lerp(r, inkC.r, line); g = Mathf.Lerp(g, inkC.g, line); b = Mathf.Lerp(b, inkC.b, line);
                    // aged edges
                    float ex = Mathf.Min(x, res - 1 - x) / (float)res, ey = Mathf.Min(y, res - 1 - y) / (float)res;
                    float edge = 1f - 0.18f * (1f - Smooth(0f, 0.08f, Mathf.Min(ex, ey)));
                    r *= edge; g *= edge * 0.99f; b *= edge * 0.96f;
                    dst[y * res + x] = new Color32(B(r), B(g), B(b), 255);
                }
            }
        }

        static float L(Color32[] px, int res, int x, int y)
        {
            x = x < 0 ? 0 : (x >= res ? res - 1 : x);
            y = y < 0 ? 0 : (y >= res ? res - 1 : y);
            Color32 c = px[y * res + x];
            return (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
        }

        static float Bilinear(float[] g, int n, float x, float y)
        {
            x = Mathf.Clamp(x, 0f, n - 1.001f); y = Mathf.Clamp(y, 0f, n - 1.001f);
            int i = (int)x, j = (int)y;
            float fx = x - i, fy = y - j;
            float a = g[j * n + i], b = g[j * n + i + 1], c = g[(j + 1) * n + i], d = g[(j + 1) * n + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static byte B(float v) { return (byte)Mathf.Clamp(v * 255f + 0.5f, 0f, 255f); }

        // ================================================================== input
        void Update()
        {
            if (!open || Time.frameCount == openFrame) return;
            if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            if (routes.Count > 0)
            {
                if (InputReader.MenuUp) Select(selected - 1);
                if (InputReader.MenuDown) Select(selected + 1);
            }
            if (InputReader.Down(KeyCode.Tab) || GamepadBridge.ButtonDown(PadButton.North)) { overview = !overview; panOffset = Vector2.zero; zoomMul = 1f; UI.Sound("Camera"); }
            float z = 0f;
            if (InputReader.Key(KeyCode.Equals) || InputReader.Key(KeyCode.KeypadPlus) || InputReader.Key(KeyCode.E)) z -= 1f;
            if (InputReader.Key(KeyCode.Minus) || InputReader.Key(KeyCode.KeypadMinus) || InputReader.Key(KeyCode.Q)) z += 1f;
            z += GamepadBridge.LeftTrigger - GamepadBridge.RightTrigger;
            if (Mathf.Abs(z) > 0.05f) zoomMul = Mathf.Clamp(zoomMul * Mathf.Exp(z * dt * 1.2f), 0.35f, 3f);
            Vector2 rs = GamepadBridge.RightStick;
            if (rs.sqrMagnitude > 0.04f) panOffset += rs * viewSize * 0.7f * dt;
            if (routes.Count > 0 && InputReader.MenuConfirm) { Pick(); return; }
            if (InputReader.MenuBack) { Back(); return; }
        }

        // ================================================================== drawing
        void OnGUI()
        {
            float now = Time.unscaledTime;
            bool closing = !open && now - closeTime < 0.35f;
            if (!open && !closing) return;
            GUI.depth = -10;
            MenuArt.Styles();
            float W = Screen.width, H = Screen.height, s = MenuArt.S;
            float openK = open ? MenuArt.EaseOut((now - openTime) / 0.7f) : 1f - MenuArt.Ease((now - closeTime) / 0.35f);
            var e = Event.current;
            mouseMoved = false;
            if (e.type == EventType.Repaint)
            {
                Animate();
                if ((e.mousePosition - lastHoverMouse).sqrMagnitude > 1f)
                {
                    mouseMoved = lastHoverMouse.x >= 0f;
                    lastHoverMouse = e.mousePosition;
                }
            }

            // ---- backdrop: a darkened ink painting ----
            float bgA = open ? Mathf.Clamp01((now - openTime) / 0.25f) : openK;
            var bg = LoadingScreen.AnyPainting(1);
            MenuArt.Fill(new Rect(0, 0, W, H), new Color(MenuArt.PlumDeep.r, MenuArt.PlumDeep.g, MenuArt.PlumDeep.b, bgA));
            if (bg != null) MenuArt.Tex(new Rect(0, 0, W, H), bg, new Color(1f, 1f, 1f, 0.22f * bgA), ScaleMode.ScaleAndCrop);
            MenuArt.Tex(new Rect(0, 0, W, H), MenuArt.Vignette, new Color(0.05f, 0.02f, 0.04f, 0.8f * bgA));

            // ---- layout ----
            float m = Mathf.Min(H * 0.86f, W * 0.56f);
            float sx = Mathf.Max(24f * s, W * 0.04f), sy = (H - m) * 0.5f;
            Rect scroll = new Rect(sx, sy, m, m);
            float pad = 22f * s;
            mapRect = new Rect(scroll.x + pad, scroll.y + pad, scroll.width - pad * 2f, scroll.height - pad * 2f);
            listRect = new Rect(scroll.xMax + 40f * s, sy, Mathf.Min(640f * s, W - scroll.xMax - 70f * s), m);

            // unroll: the scroll opens from its centre line
            float visH = scroll.height * openK;
            Rect vis = new Rect(scroll.x, scroll.center.y - visH * 0.5f, scroll.width, visH);
            if (visH > 2f)
            {
                MenuArt.Tex(new Rect(vis.x - 16f * s, vis.y - 6f * s, vis.width + 32f * s, vis.height + 30f * s), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.5f));
                MenuArt.PaperFill(vis, Color.white);
                Rect mv = Rect.MinMaxRect(mapRect.xMin, Mathf.Max(mapRect.yMin, vis.yMin + pad * 0.6f), mapRect.xMax, Mathf.Min(mapRect.yMax, vis.yMax - pad * 0.6f));
                if (mv.height > 1f) DrawMap(mv, s, now);
                // double ink border
                MenuArt.Outline(new Rect(mapRect.x - 5f * s, Mathf.Max(vis.y + 4f * s, mapRect.y - 5f * s), mapRect.width + 10f * s,
                                         Mathf.Min(vis.height - 8f * s, mapRect.height + 10f * s)), Mathf.Max(1f, 2.4f * s), MenuArt.A(MenuArt.Ink, 0.85f));
                MenuArt.Outline(new Rect(mapRect.x - 1f, Mathf.Max(vis.y + 9f * s, mapRect.y - 1f), mapRect.width + 2f,
                                         Mathf.Min(vis.height - 18f * s, mapRect.height + 2f)), 1f, MenuArt.A(MenuArt.Ink, 0.6f));
            }
            // wooden rollers at both ends
            Roller(new Rect(scroll.x - 18f * s, vis.y - 12f * s, scroll.width + 36f * s, 22f * s), s);
            Roller(new Rect(scroll.x - 18f * s, vis.yMax - 10f * s, scroll.width + 36f * s, 22f * s), s);

            // ---- list ----
            float listA = open ? MenuArt.Ease((now - openTime - 0.2f) / 0.45f) : openK;
            if (listA > 0.01f) DrawList(listRect, s, listA, now);

            if (open)
            {
                HandleMouse(e);
                if (mouseMoved && !dragging) hoverRoute = mapRect.Contains(e.mousePosition) ? RouteAt(e.mousePosition, 14f * s) : -1;
                // nothing underneath (title menu) may react to clicks while the map is up
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
        }

        void Roller(Rect r, float s)
        {
            for (int i = 0; i < 6; i++)
            {
                float k = i / 5f;
                float shade = 0.22f + 0.22f * Mathf.Sin(k * Mathf.PI);
                MenuArt.Fill(new Rect(r.x + 8f * s, r.y + r.height * k * 0.85f, r.width - 16f * s, r.height / 6f + 1f), new Color(shade * 1.2f, shade * 0.62f, shade * 0.45f, 1f));
            }
            MenuArt.Fill(new Rect(r.x, r.y - 2f * s, 12f * s, r.height + 4f * s), new Color(0.80f, 0.60f, 0.24f));
            MenuArt.Fill(new Rect(r.xMax - 12f * s, r.y - 2f * s, 12f * s, r.height + 4f * s), new Color(0.80f, 0.60f, 0.24f));
            MenuArt.Fill(new Rect(r.x + 2f * s, r.y, 3f * s, r.height), new Color(1f, 0.86f, 0.5f, 0.6f));
            MenuArt.Fill(new Rect(r.xMax - 10f * s, r.y, 3f * s, r.height), new Color(1f, 0.86f, 0.5f, 0.6f));
        }

        void Animate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Vector2 tc;
            float ts;
            if (routes.Count == 0 || overview || Time.unscaledTime < openTime + 0.35f)
            {
                tc = MapC;
                ts = MapSize;
            }
            else
            {
                var r = routes[selected];
                tc = (r.min + r.max) * 0.5f;
                Vector2 ext = r.max - r.min;
                ts = Mathf.Clamp(Mathf.Max(ext.x, ext.y) * 1.45f + 60f, 260f, MapSize);
            }
            ts = Mathf.Clamp(ts * zoomMul, 120f, MapSize);
            tc += panOffset;
            float half = ts * 0.5f;
            tc.x = Mathf.Clamp(tc.x, MapC.x - MapSize * 0.5f + half, MapC.x + MapSize * 0.5f - half);
            tc.y = Mathf.Clamp(tc.y, MapC.y - MapSize * 0.5f + half, MapC.y + MapSize * 0.5f - half);
            panOffset = tc - (overview || routes.Count == 0 ? Vector2.zero : (routes[selected].min + routes[selected].max) * 0.5f);
            if (overview || routes.Count == 0) panOffset = tc;
            // fly: zoom out a little while travelling between distant routes
            float travel = Vector2.Distance(viewC, tc);
            float k = MenuArt.Damp(dt, 3.2f);
            viewC = Vector2.Lerp(viewC, tc, k);
            float wantSize = Mathf.Min(MapSize, ts + travel * 0.6f);
            viewSize = Mathf.Exp(Mathf.Lerp(Mathf.Log(viewSize), Mathf.Log(wantSize), MenuArt.Damp(dt, 2.6f)));
            float h2 = viewSize * 0.5f;
            viewC.x = Mathf.Clamp(viewC.x, MapC.x - MapSize * 0.5f + h2, MapC.x + MapSize * 0.5f - h2);
            viewC.y = Mathf.Clamp(viewC.y, MapC.y - MapSize * 0.5f + h2, MapC.y + MapSize * 0.5f - h2);
        }

        Vector2 W2S(Vector2 w)
        {
            float u = (w.x - viewC.x) / viewSize + 0.5f, v = (w.y - viewC.y) / viewSize + 0.5f;
            return new Vector2(mapRect.x + u * mapRect.width, mapRect.y + (1f - v) * mapRect.height);
        }

        Vector2 S2W(Vector2 sp)
        {
            float u = (sp.x - mapRect.x) / mapRect.width, v = 1f - (sp.y - mapRect.y) / mapRect.height;
            return new Vector2(viewC.x + (u - 0.5f) * viewSize, viewC.y + (v - 0.5f) * viewSize);
        }

        void DrawMap(Rect clip, float s, float now)
        {
            // texture (or paper while it is being painted)
            float u0 = (viewC.x - viewSize * 0.5f - (MapC.x - MapSize * 0.5f)) / MapSize, v0 = (viewC.y - viewSize * 0.5f - (MapC.y - MapSize * 0.5f)) / MapSize, us = viewSize / MapSize;
            float cropTop = (clip.yMin - mapRect.yMin) / mapRect.height, cropBot = (mapRect.yMax - clip.yMax) / mapRect.height;
            Rect uv = new Rect(u0, v0 + us * cropBot, us, us * (1f - cropTop - cropBot));
            if (mapTex != null)
            {
                float fade = 1f;
                GUI.DrawTextureWithTexCoords(clip, mapTex, uv, false);
                MenuArt.Fill(clip, new Color(MenuArt.PaperCol.r, MenuArt.PaperCol.g, MenuArt.PaperCol.b, 0.08f * fade));
            }
            else
            {
                MenuArt.PaperFill(clip, new Color(0.95f, 0.93f, 0.88f));
                MenuArt.Text(new Rect(clip.x, clip.center.y - 20f * s, clip.width, 40f * s), world == null ? "The valley is still loading..." : "Painting the valley...",
                             MenuArt.Mid, MenuArt.A(MenuArt.Ink, 0.55f + 0.25f * Mathf.Sin(now * 3f)), false);
            }
            if (routes.Count == 0) return;

            // ---- routes (GL) ----
            Route sel = routes[selected];
            float traceT = Mathf.Clamp01((now - selectTime) / 2.4f);
            float trace = MenuArt.EaseInOut(traceT);
            float zoomK = Mathf.Clamp(1000f / viewSize, 1f, 3f);
            if (InkGL.Begin(clip))
            {
                for (int i = 0; i < routes.Count; i++)
                {
                    var r = routes[i];
                    if (r == sel) continue;
                    if (r.def.road == sel.def.road && r.def.startOffset == sel.def.startOffset) continue;   // same road as the selection
                    bool hov = i == hoverRoute;
                    Path(r, 1f, (hov ? 6.5f : 4.6f) * s, MenuArt.A(MenuArt.Ink, 0.45f));
                    Path(r, 1f, (hov ? 4f : 2.6f) * s, MenuArt.A(r.col, hov ? 0.95f : 0.75f));
                }
                float wOut = (8.5f + zoomK) * s, wIn = (5f + zoomK * 0.6f) * s;
                // ghost of the full route, then the trace drawn over it
                Path(sel, 1f, wOut, MenuArt.A(MenuArt.Ink, 0.25f));
                Path(sel, 1f, wIn * 0.5f, MenuArt.A(sel.col, 0.35f));
                Path(sel, trace, wOut, MenuArt.A(MenuArt.Ink, 0.8f));
                Path(sel, trace, wIn, sel.col);
                Path(sel, trace, wIn * 0.3f, new Color(1f, 1f, 1f, 0.55f));
                // checkpoints reached by the trace
                for (int g = 1; g < sel.gates.Length - 1; g++)
                {
                    if ((float)g / (sel.gates.Length - 1) > trace + 0.001f) break;
                    Vector2 p = W2S(sel.gates[g]);
                    InkGL.Disc(p, 6.5f * s, MenuArt.Ink);
                    InkGL.Disc(p, 4.2f * s, MenuArt.PaperCol);
                    InkGL.Disc(p, 2.2f * s, sel.col);
                }
                // travelling light: the trace head, then a looping comet
                float head = traceT < 1f ? trace : Mathf.Repeat((now - selectTime - 2.4f) / 5f, 1f);
                Vector2 hp = PointAt(sel, head);
                if (traceT >= 1f)
                {
                    float tail = Mathf.Max(0f, head - 0.06f);
                    PathRange(sel, tail, head, wIn * 0.75f, new Color(1f, 0.97f, 0.9f, 0.85f));
                }
                InkGL.Disc(W2S(hp), (traceT < 1f ? 7f : 5f) * s, new Color(1f, 0.98f, 0.92f, 0.95f));
                InkGL.End();
            }

            // ---- glows + icons + labels (IMGUI) ----
            if (Event.current.type == EventType.Repaint)
            {
                Vector2 hs = W2S(PointAt(sel, traceT < 1f ? trace : Mathf.Repeat((now - selectTime - 2.4f) / 5f, 1f)));
                if (clip.Contains(hs)) MenuArt.Tex(new Rect(hs.x - 26f * s, hs.y - 26f * s, 52f * s, 52f * s), MenuArt.Glow, MenuArt.A(sel.col, 0.9f));
            }
            Landmarks(clip, s);
            for (int i = 0; i < routes.Count; i++)
            {
                var r = routes[i];
                bool isSel = r == sel;
                if (!isSel && r.def.road == sel.def.road && r.def.startOffset == sel.def.startOffset) continue;
                Vector2 st = W2S(r.pts[0]), fn = W2S(r.pts[r.pts.Count - 1]);
                float fs = (isSel ? 34f : 22f) * s;
                if (!r.closed && clip.Contains(fn) && (isSel ? trace > 0.98f : true))
                    MenuArt.Tex(new Rect(fn.x - fs * 0.22f, fn.y - fs * 0.92f, fs, fs), MenuArt.FlagFinish, Color.white);
                if (clip.Contains(st))
                {
                    MenuArt.Tex(new Rect(st.x - fs * 0.22f, st.y - fs * 0.92f, fs, fs), r.closed ? MenuArt.FlagFinish : MenuArt.FlagStart, Color.white);
                    // numbered badge
                    float bs = (isSel ? 24f : 18f) * s;
                    Rect b = new Rect(st.x - bs - 4f * s, st.y - bs * 0.5f, bs, bs);
                    MenuArt.Tex(b, MenuArt.Dot, MenuArt.Ink);
                    MenuArt.Tex(new Rect(b.x + 2f * s, b.y + 2f * s, b.width - 4f * s, b.height - 4f * s), MenuArt.Dot, r.col);
                    MenuArt.Text(b, (r.index + 1).ToString(), MenuArt.TinyC, MenuArt.Ink, false);
                    int medal = GameSettings.BestMedal("Stage_" + r.def.key);
                    if (medal > 0)
                    {
                        Rect md = new Rect(st.x + fs * 0.75f, st.y - fs * 0.85f, 16f * s, 16f * s);
                        MenuArt.Tex(md, MenuArt.Dot, MenuArt.Ink);
                        MenuArt.Tex(new Rect(md.x + 2f * s, md.y + 2f * s, md.width - 4f * s, md.height - 4f * s), MenuArt.Dot, MenuArt.MedalColor(medal));
                    }
                }
            }
            // name tag next to the selected route's start
            {
                Vector2 st = W2S(sel.pts[0]);
                var gc = new GUIContent(sel.def.name);
                float tw = MenuArt.Small.CalcSize(gc).x + 18f * s, th = 24f * s;
                Rect tag = new Rect(st.x + 22f * s, st.y + 4f * s, tw, th);
                if (tag.xMax > clip.xMax) tag.x = st.x - tw - 30f * s;
                if (clip.Contains(tag.center))
                {
                    MenuArt.Fill(tag, MenuArt.A(MenuArt.Ink, 0.82f));
                    MenuArt.Fill(new Rect(tag.x, tag.y, 3f * s, tag.height), sel.col);
                    MenuArt.Text(new Rect(tag.x + 9f * s, tag.y, tw, th), sel.def.name, MenuArt.Small, MenuArt.PaperCol, false);
                }
            }
            Compass(clip, s);
            ScaleBar(clip, s);
            Cartouche(clip, s);
        }

        void Path(Route r, float upto, float w, Color c) { PathRange(r, 0f, upto, w, c); }

        void PathRange(Route r, float from, float upto, float w, Color c)
        {
            int n = r.pts.Count;
            if (n < 2 || upto <= from) return;
            float a = from * (n - 1), b = upto * (n - 1);
            int ia = Mathf.FloorToInt(a), ib = Mathf.Min(n - 2, Mathf.FloorToInt(b));
            Vector2 prev = W2S(Vector2.Lerp(r.pts[ia], r.pts[Mathf.Min(n - 1, ia + 1)], a - ia));
            for (int i = ia + 1; i <= ib + 1 && i < n; i++)
            {
                Vector2 p = i <= ib ? W2S(r.pts[i]) : W2S(Vector2.Lerp(r.pts[ib], r.pts[ib + 1], Mathf.Clamp01(b - ib)));
                InkGL.Seg(prev, p, w, c);
                prev = p;
            }
        }

        static Vector2 PointAt(Route r, float t)
        {
            int n = r.pts.Count;
            if (n == 0) return Vector2.zero;
            float f = Mathf.Clamp01(t) * (n - 1);
            int i = Mathf.Min(n - 2, Mathf.FloorToInt(f));
            if (i < 0) return r.pts[0];
            return Vector2.Lerp(r.pts[i], r.pts[i + 1], f - i);
        }

        void Landmarks(Rect clip, float s)
        {
            Label(clip, MapData.PagodaTop + new Vector2(0f, 14f), "Mountain Pagoda", s);
            Label(clip, MapData.LakeCenter, "Lotus Lake", s);
            Label(clip, MapData.Gate + new Vector2(0f, -40f), "Blossom Village", s);
            Label(clip, MapData.Garage + new Vector2(0f, -22f), "Garage", s);
            Label(clip, MapData.P(1330, 900), "Tea Terraces", s);
            Label(clip, MapData.P(200, 600), "Bamboo Forest", s);
            Label(clip, MapData.Gate + new Vector2(14f, 4f), "Paifang Gate", s);
            // region names (big, shown when zoomed out)
            if (viewSize > 1300f && world != null && world.Field != null)
                foreach (var reg in world.Field.regions)
                {
                    if (reg == null) continue;
                    Vector2 c = reg.Center + new Vector2(0f, 330f);
                    Vector2 p = W2S(c);
                    if (!clip.Contains(p)) continue;
                    var gc = new GUIContent(reg.Name.ToUpperInvariant());
                    var st = MenuArt.MidL;
                    Vector2 sz = st.CalcSize(gc);
                    Rect r = new Rect(p.x - sz.x * 0.5f, p.y - sz.y * 0.5f, sz.x + 6f, sz.y);
                    MenuArt.Tex(new Rect(r.x - 16f * s, r.y - 6f * s, r.width + 32f * s, r.height + 12f * s), MenuArt.Glow, MenuArt.A(MenuArt.PaperCol, 0.85f));
                    Color acc = reg.Accent;
                    MenuArt.Fill(new Rect(r.x, r.yMax, r.width, 3f * s), new Color(acc.r * 0.8f, acc.g * 0.8f, acc.b * 0.8f, 0.9f));
                    MenuArt.Text(r, reg.Name.ToUpperInvariant(), st, MenuArt.A(MenuArt.Ink, 0.92f), false);
                }
        }

        void Label(Rect clip, Vector2 world, string text, float s)
        {
            Vector2 p = W2S(world);
            if (!clip.Contains(p)) return;
            var gc = new GUIContent(text);
            Vector2 sz = MenuArt.Tiny.CalcSize(gc);
            Rect r = new Rect(p.x - sz.x * 0.5f, p.y - sz.y * 0.5f, sz.x + 4f, sz.y);
            MenuArt.Tex(new Rect(r.x - 10f * s, r.y - 4f * s, r.width + 20f * s, r.height + 8f * s), MenuArt.Glow, MenuArt.A(MenuArt.PaperCol, 0.9f));
            var st = MenuArt.Tiny;
            var old = st.fontStyle;
            st.fontStyle = FontStyle.Italic;
            MenuArt.Text(r, text, st, MenuArt.A(MenuArt.Ink, 0.9f), false);
            st.fontStyle = old;
        }

        void Compass(Rect clip, float s)
        {
            Vector2 c = new Vector2(clip.xMax - 40f * s, clip.yMin + 46f * s);
            if (!clip.Contains(c)) return;
            MenuArt.Tex(new Rect(c.x - 26f * s, c.y - 26f * s, 52f * s, 52f * s), MenuArt.Dot, MenuArt.A(MenuArt.PaperCol, 0.85f));
            MenuArt.Tex(new Rect(c.x - 26f * s, c.y - 26f * s, 52f * s, 52f * s), MenuArt.Ring, MenuArt.A(MenuArt.Ink, 0.8f));
            if (InkGL.Begin(clip))
            {
                InkGL.Seg(c + new Vector2(0f, 14f * s), c + new Vector2(0f, -16f * s), 4f * s, MenuArt.Vermilion);
                InkGL.Seg(c + new Vector2(-12f * s, 0f), c + new Vector2(12f * s, 0f), 2f * s, MenuArt.A(MenuArt.Ink, 0.7f));
                InkGL.End();
            }
            MenuArt.Text(new Rect(c.x - 10f * s, c.y - 46f * s, 20f * s, 20f * s), "N", MenuArt.TinyC, MenuArt.Ink, false);
        }

        void ScaleBar(Rect clip, float s)
        {
            float pxPerM = mapRect.width / viewSize;
            float target = 140f * s / pxPerM;
            float[] steps = { 25f, 50f, 100f, 200f, 250f, 500f, 1000f };
            float m = steps[0];
            foreach (float st in steps) if (st <= target) m = st;
            float len = m * pxPerM;
            Vector2 a = new Vector2(clip.xMin + 24f * s, clip.yMax - 26f * s);
            if (!clip.Contains(a)) return;
            MenuArt.Fill(new Rect(a.x, a.y, len, 3f * s), MenuArt.Ink);
            MenuArt.Fill(new Rect(a.x, a.y - 6f * s, 2f * s, 9f * s), MenuArt.Ink);
            MenuArt.Fill(new Rect(a.x + len - 2f * s, a.y - 6f * s, 2f * s, 9f * s), MenuArt.Ink);
            MenuArt.Fill(new Rect(a.x + len * 0.5f - 1f * s, a.y - 4f * s, 2f * s, 7f * s), MenuArt.Ink);
            MenuArt.Text(new Rect(a.x, a.y - 24f * s, len + 60f * s, 20f * s), Mathf.RoundToInt(m) + " m", MenuArt.Tiny, MenuArt.Ink, false);
        }

        void Cartouche(Rect clip, float s)
        {
            Rect r = new Rect(clip.xMin + 16f * s, clip.yMin + 14f * s, 214f * s, 70f * s);
            if (r.yMax > clip.yMax) return;
            MenuArt.Tex(new Rect(r.x - 6f * s, r.y - 4f * s, r.width + 12f * s, r.height + 14f * s), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.25f));
            MenuArt.PaperFill(r, Color.white);
            MenuArt.Outline(r, Mathf.Max(1f, 2f * s), MenuArt.Ink);
            MenuArt.Outline(new Rect(r.x + 4f * s, r.y + 4f * s, r.width - 8f * s, r.height - 8f * s), 1f, MenuArt.A(MenuArt.Ink, 0.6f));
            MenuArt.Text(new Rect(r.x + 12f * s, r.y + 8f * s, r.width - 46f * s, 30f * s), "BLOSSOM RALLY", MenuArt.MidL, MenuArt.Ink, false);
            var st = MenuArt.Tiny;
            MenuArt.Text(new Rect(r.x + 12f * s, r.y + 38f * s, r.width, 22f * s), "six regions  ·  3 km × 2 km", st, MenuArt.A(MenuArt.Ink, 0.75f), false);
            MenuArt.Tex(new Rect(r.xMax - 36f * s, r.y + 10f * s, 26f * s, 26f * s), MenuArt.Seal, Color.white);
        }

        // ---------------------------------------------------------------- list panel
        void DrawList(Rect r, float s, float alpha, float now)
        {
            float slide = (1f - alpha) * 60f * s;
            r.x += slide;
            MenuArt.Text(new Rect(r.x, r.y - 4f * s, r.width, 50f * s), "RALLY STAGES", MenuArt.Title, MenuArt.A(UI.Pink, alpha));
            MenuArt.Text(new Rect(r.x, r.y + 40f * s, r.width, 26f * s), "choose a route on the valley map", MenuArt.SmallC, MenuArt.A(UI.White, 0.75f * alpha));
            if (routes.Count == 0)
            {
                MenuArt.Text(new Rect(r.x, r.y + 120f * s, r.width, 40f * s), "No stages available yet", MenuArt.Mid, MenuArt.A(UI.White, alpha));
                Footer(r, s, alpha);
                return;
            }
            float y = r.y + 82f * s;
            float rowH = 66f * s, selH = 196f * s, gap = 8f * s;
            float avail = r.height - 82f * s - 70f * s;
            float total = routes.Count * (rowH + gap) + (selH - rowH);
            // keep the selected card in view
            float selTop = selected * (rowH + gap);
            float want = Mathf.Clamp(selTop - (avail - selH) * 0.5f, 0f, Mathf.Max(0f, total - avail));
            listScroll = Mathf.Lerp(listScroll, want, MenuArt.Damp(Time.unscaledDeltaTime, 10f));
            y -= listScroll;
            for (int i = 0; i < routes.Count; i++)
            {
                var rt = routes[i];
                bool sel = i == selected;
                float h = sel ? selH : rowH;
                Rect c = new Rect(r.x, y, r.width, h);
                y += h + gap;
                if (c.yMax < r.y + 76f * s || c.y > r.y + 82f * s + avail) continue;
                Card(c, rt, sel, s, alpha, now);
                if (open && GUI.Button(c, GUIContent.none, GUIStyle.none))
                {
                    if (sel) Pick();
                    else Select(i);
                    GUIUtility.ExitGUI();
                }
                if (open && mouseMoved && c.Contains(Event.current.mousePosition) && !sel) Select(i);
            }
            Footer(r, s, alpha);
        }

        void Card(Rect c, Route rt, bool sel, float s, float alpha, float now)
        {
            MenuArt.Card(c, alpha * (sel ? 1f : 0.85f), false);
            if (sel) MenuArt.Fill(c, new Color(1f, 0.72f, 0.82f, 0.10f * alpha));
            MenuArt.Fill(new Rect(c.x, c.y, 6f * s, c.height), MenuArt.A(rt.col, alpha));
            if (sel) MenuArt.Outline(c, Mathf.Max(1f, 2f * s), MenuArt.A(rt.col, (0.6f + 0.3f * Mathf.Sin(now * 4f)) * alpha));
            // number badge
            Rect b = new Rect(c.x + 18f * s, c.y + 14f * s, 36f * s, 36f * s);
            MenuArt.Tex(b, MenuArt.Dot, MenuArt.A(rt.col, alpha));
            MenuArt.Text(b, (rt.index + 1).ToString(), MenuArt.Mid, MenuArt.A(MenuArt.Ink, alpha), false);
            string key = "Stage_" + rt.def.key;
            int medal = GameSettings.BestMedal(key);
            float best = GameSettings.BestTime(key);
            bool drift = rt.def.kind == StageKind.Drift;
            float x = c.x + 68f * s, w = c.width - 68f * s - 16f * s;
            MenuArt.Text(new Rect(x, c.y + 6f * s, w, 32f * s), rt.def.name, MenuArt.MidL, MenuArt.A(sel ? UI.Pink : UI.White, alpha));
            string kind = drift ? "Drift challenge" : "Time attack";
            string meta = kind + "  ·  " + (rt.length / 1000f).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " km " + rt.surface + "  ·  " + (rt.gates.Length - 1) + " gates";
            MenuArt.Text(new Rect(x, c.y + 34f * s, w, 24f * s), meta, MenuArt.Tiny, MenuArt.A(UI.White, 0.75f * alpha));
            // best + medal (right)
            string bs = best > 0f ? (drift ? Pts(best) + " pts" : StageManager.FormatTime(best)) : "--:--";
            MenuArt.Text(new Rect(c.xMax - 190f * s, c.y + 6f * s, 140f * s, 32f * s), bs, MenuArt.MidR, MenuArt.A(best > 0f ? UI.Gold : UI.White, alpha * (best > 0f ? 1f : 0.45f)));
            Rect md = new Rect(c.xMax - 42f * s, c.y + 12f * s, 26f * s, 26f * s);
            MenuArt.Tex(md, MenuArt.Dot, MenuArt.A(MenuArt.Ink, 0.6f * alpha));
            if (medal > 0)
            {
                MenuArt.Tex(new Rect(md.x + 3f * s, md.y + 3f * s, md.width - 6f * s, md.height - 6f * s), MenuArt.Dot, MenuArt.A(MenuArt.MedalColor(medal), alpha));
                MenuArt.Tex(new Rect(md.x + 7f * s, md.y + 6f * s, 6f * s, 6f * s), MenuArt.Dot, new Color(1f, 1f, 1f, 0.7f * alpha));
            }
            else MenuArt.Tex(md, MenuArt.Ring, MenuArt.A(UI.White, 0.35f * alpha));
            if (!sel) return;

            // ---- expanded ----
            float y = c.y + 64f * s;
            MenuArt.Fill(new Rect(x, y, w, 1f), MenuArt.A(UI.White, 0.2f * alpha));
            MenuArt.Text(new Rect(x, y + 6f * s, w, 44f * s), rt.def.blurb, MenuArt.Small, MenuArt.A(UI.White, 0.92f * alpha));
            y += 56f * s;
            float colW = w / 3f;
            for (int k = 0; k < 3; k++)
            {
                int m = 3 - k;
                string target = drift ? Pts(m == 3 ? rt.def.goldPts : m == 2 ? rt.def.silverPts : rt.def.bronzePts)
                                      : StageManager.FormatTime(m == 3 ? rt.def.GoldTime(rt.length) : m == 2 ? rt.def.SilverTime(rt.length) : rt.def.BronzeTime(rt.length));
                Color mc = MenuArt.MedalColor(m);
                bool got = medal >= m;
                Rect cell = new Rect(x + k * colW, y, colW - 8f * s, 50f * s);
                MenuArt.Fill(cell, new Color(0f, 0f, 0f, 0.18f * alpha));
                MenuArt.Tex(new Rect(cell.x + 8f * s, cell.y + 8f * s, 14f * s, 14f * s), MenuArt.Dot, MenuArt.A(mc, (got ? 1f : 0.45f) * alpha));
                MenuArt.Text(new Rect(cell.x + 28f * s, cell.y + 3f * s, cell.width, 22f * s), MenuArt.MedalName(m).ToUpperInvariant(), MenuArt.Tiny, MenuArt.A(mc, (got ? 1f : 0.6f) * alpha));
                MenuArt.Text(new Rect(cell.x + 8f * s, cell.y + 24f * s, cell.width - 12f * s, 24f * s), target + (drift ? " pts" : ""), MenuArt.Small, MenuArt.A(UI.White, alpha));
            }
            y += 58f * s;
            string extra = drift ? "Time limit " + Mathf.RoundToInt(rt.def.TimeLimit(rt.length)) + " s" : (rt.closed ? "One full lap" : "Point to point");
            extra += medal > 0 ? "   ·   best medal: " + MenuArt.MedalName(medal) : "   ·   no medal yet";
            MenuArt.Text(new Rect(x, y, w * 0.62f, 22f * s), extra, MenuArt.Tiny, MenuArt.A(UI.White, 0.7f * alpha));
            float pulse = 0.75f + 0.25f * Mathf.Sin(now * 5f);
            MenuArt.Text(new Rect(x + w * 0.45f, y - 2f * s, w * 0.55f, 26f * s), "ENTER  ›  RACE", MenuArt.BodyR, MenuArt.A(UI.Gold, pulse * alpha));
        }

        void Footer(Rect r, float s, float alpha)
        {
            if (alpha < 0.5f) return;
            float y = r.yMax - 34f * s, x = r.x;
            bool c1, c2, c3;
            bool pad = InputReader.PadConnected;
            x += MenuArt.Chip(x, y, pad ? "A" : "ENTER", "race", open, out c1);
            x += MenuArt.Chip(x, y, pad ? "B" : "ESC", "back", open, out c2);
            x += MenuArt.Chip(x, y, pad ? "Y" : "TAB", overview ? "follow route" : "whole valley", open, out c3);
            MenuArt.Chip(x, y, pad ? "LT RT" : "WHEEL", "zoom");
            if (c3) { overview = !overview; panOffset = Vector2.zero; zoomMul = 1f; UI.Sound("Camera"); }
            if (c2) { Back(); GUIUtility.ExitGUI(); }
        }

        static string Pts(float p) { return Mathf.RoundToInt(p).ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }

        // ---------------------------------------------------------------- mouse on the map
        void HandleMouse(Event e)
        {
            Vector2 mp = e.mousePosition;
            bool overMap = mapRect.Contains(mp);
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    if (overMap)
                    {
                        float f = Mathf.Exp(e.delta.y * 0.08f);
                        Vector2 before = S2W(mp);
                        zoomMul = Mathf.Clamp(zoomMul * f, 0.35f, 3f);
                        // nudge the pan so the point under the cursor stays roughly put
                        Vector2 after = viewC + (before - viewC) * f;
                        panOffset += (before - after) * 0.6f;
                        e.Use();
                    }
                    break;
                case EventType.MouseDown:
                    if (overMap)
                    {
                        dragging = true;
                        dragMoved = false;
                        dragStart = lastMouse = mp;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (dragging)
                    {
                        Vector2 d = mp - lastMouse;
                        lastMouse = mp;
                        if ((mp - dragStart).sqrMagnitude > 25f) dragMoved = true;
                        float mPerPx = viewSize / mapRect.width;
                        panOffset += new Vector2(-d.x, d.y) * mPerPx;
                        viewC += new Vector2(-d.x, d.y) * mPerPx;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (dragging)
                    {
                        dragging = false;
                        if (!dragMoved)
                        {
                            int hit = RouteAt(mp, 16f * MenuArt.S);
                            if (hit >= 0)
                            {
                                if (hit == selected) Pick();
                                else Select(hit);
                            }
                        }
                        e.Use();
                    }
                    break;
            }
        }

        int RouteAt(Vector2 sp, float maxPx)
        {
            int best = -1;
            float bd = maxPx * maxPx;
            for (int i = 0; i < routes.Count; i++)
            {
                var r = routes[i];
                for (int k = 0; k + 1 < r.pts.Count; k += 2)
                {
                    Vector2 a = W2S(r.pts[k]), b = W2S(r.pts[Mathf.Min(k + 2, r.pts.Count - 1)]);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(sp - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    float d = (a + ab * t - sp).sqrMagnitude;
                    // prefer the selected route when routes overlap
                    if (i == selected) d *= 0.8f;
                    if (d < bd) { bd = d; best = i; }
                }
            }
            return best;
        }
    }
}
