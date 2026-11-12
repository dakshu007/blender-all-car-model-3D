// Minimap.cs - GTA-style minimap in the bottom-left corner while driving.
//
// The painted world map (ValleyMap.MapTexture; road lines from WorldBuilder road samples when the painting isn't ready)
// is rendered, rotated so the camera's heading points up, into a small 4x MSAA RenderTexture (one textured quad plus
// road / stage / GPS lines: a few hundred triangles, capped at 60 Hz, no per-frame allocation). OnGUI then draws that
// texture as a rounded rectangle (IMGUI's rounded DrawTexture: the rotation never touches IMGUI clipping) with the
// player arrow (centre, a little low like GTA), upright marker badges, edge-clamped badges for off-screen targets
// (waypoint, nearby events, garage), a compass N riding the rim, the GPS distance and the region name + hanzi below.
// Zooms out with speed. Hidden in menus, photo mode, replays, results, the garage and the title, or by Settings > Minimap.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(960)]
    public class Minimap : MonoBehaviour
    {
        public static Minimap Instance { get; private set; }
        /// <summary>True while the minimap is on screen.</summary>
        public static bool Visible { get { return Instance != null && Instance.visible; } }
        /// <summary>Top edge (GUI y) of the minimap block, or the screen height when hidden (for HUD text placed above it).</summary>
        public static float TopEdge { get { return Visible ? Instance.rect.y - 8f * MenuArt.S : Screen.height; } }

        const float RefreshHz = 60f;
        const float PivotY = 0.64f;                   // player sits a little below the centre
        static readonly Vector2 MapC = new Vector2(0f, 500f);
        const float MapSize = 3000f;

        RenderTexture rt;
        bool visible;
        float fade;
        Rect rect;
        float heading;                                // camera yaw, degrees (0 = north / +z)
        float size = 280f;                            // metres across the minimap width
        Vector2 center;
        float nextRender;
        bool headingInit;
        // transform used by the last render (OnGUI places the badges with it so they stay locked to the map)
        Vector2 rC, rPivot;
        float rCos = 1f, rSin, rPpm = 1f;
        readonly DistText dist = new DistText();
        int regionShown = -1;
        float hanziW, nameW;

        void Awake()
        {
            Instance = this;
            Gps.Arrived += OnArrived;
        }

        void OnDestroy()
        {
            Gps.Arrived -= OnArrived;
            if (rt != null) { rt.Release(); Destroy(rt); rt = null; }
            if (Instance == this) Instance = null;
        }

        static void OnArrived()
        {
            if (GameManager.Instance != null) GameManager.Instance.Toast("You have arrived");
            UI.Sound("MenuSelect");
        }

        static bool ShouldShow()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || gm.Car == null || !GameSettings.Minimap) return false;
            if (GameManager.HideHud || ValleyMap.IsOpen || PhotoAlbum.IsOpen) return false;
            if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) return false;
            if (PhotoMode.Instance != null && PhotoMode.Instance.Active) return false;
            if (ReplaySystem.Instance != null && ReplaySystem.Instance.Active) return false;
            if (StageManager.Instance != null && StageManager.Instance.ShowingResults) return false;
            return true;
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            var car = gm != null ? gm.Car : null;
            if (car != null && gm.IsDriving) Gps.Track(new Vector2(car.transform.position.x, car.transform.position.z));
            visible = ShouldShow();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (!visible) { fade = 0f; headingInit = false; return; }
            fade = Mathf.MoveTowards(fade, 1f, dt * 4f);

            float s = MenuArt.S, W = Screen.width, H = Screen.height;
            float w = Mathf.Round(Mathf.Min(340f * s, W * 0.3f)), h = Mathf.Round(w * 0.64f);
            rect = new Rect(Mathf.Round(26f * s), Mathf.Round(H - 26f * s - 30f * s - h), w, h);

            // heading from the camera (smooth), falling back to the car
            Vector3 fwd = car.transform.forward;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 cf = cam.transform.forward;
                if (new Vector2(cf.x, cf.z).sqrMagnitude > 0.05f) fwd = cf;
            }
            float target = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            if (!headingInit) { heading = target; headingInit = true; }
            heading = Mathf.LerpAngle(heading, target, MenuArt.Damp(dt, 9f));
            // zoom out with speed
            float want = Mathf.Lerp(260f, 560f, Mathf.Clamp01((car.SpeedKmh - 25f) / 140f));
            size = Mathf.Lerp(size, want, MenuArt.Damp(dt, 1.6f));
            center = new Vector2(car.transform.position.x, car.transform.position.z);

            if (Time.unscaledTime >= nextRender)
            {
                nextRender = Time.unscaledTime + 1f / RefreshHz;
                Render();
            }
        }

        // ------------------------------------------------------------------ map <-> minimap pixels
        Vector2 W2M(Vector2 p)
        {
            Vector2 rel = p - rC;
            float ox = rel.x * rCos - rel.y * rSin, oy = rel.x * rSin + rel.y * rCos;
            return new Vector2(rPivot.x + ox * rPpm, rPivot.y - oy * rPpm);
        }

        Vector2 M2W(Vector2 m)
        {
            float ox = (m.x - rPivot.x) / rPpm, oy = (rPivot.y - m.y) / rPpm;
            return rC + new Vector2(ox * rCos + oy * rSin, -ox * rSin + oy * rCos);
        }

        static Vector2 UV(Vector2 w)
        {
            return new Vector2((w.x - (MapC.x - MapSize * 0.5f)) / MapSize, (w.y - (MapC.y - MapSize * 0.5f)) / MapSize);
        }

        void Render()
        {
            int pw = Mathf.Max(16, Mathf.RoundToInt(rect.width)), ph = Mathf.Max(16, Mathf.RoundToInt(rect.height));
            if (rt == null || rt.width != pw || rt.height != ph)
            {
                if (rt != null) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(pw, ph, 0, RenderTextureFormat.ARGB32) { name = "BR_Minimap", antiAliasing = 4, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                rt.Create();
            }
            float s = MenuArt.S;
            rC = center;
            float a = heading * Mathf.Deg2Rad;
            rCos = Mathf.Cos(a); rSin = Mathf.Sin(a);
            rPpm = pw / size;
            rPivot = new Vector2(pw * 0.5f, ph * PivotY);

            var tex = ValleyMap.MapTexture;
            bool painted = tex != null && MapGL.CanTexture;
            if (!MapGL.BeginTarget(rt, painted ? new Color(0.6f, 0.68f, 0.55f, 1f) : new Color(0.93f, 0.9f, 0.82f, 1f))) return;
            Vector2 c0 = new Vector2(0f, 0f), c1 = new Vector2(pw, 0f), c2 = new Vector2(pw, ph), c3 = new Vector2(0f, ph);
            if (painted)
                MapGL.TexQuad(tex, c0, c1, c2, c3, UV(M2W(c0)), UV(M2W(c1)), UV(M2W(c2)), UV(M2W(c3)), Color.white);
            MapGL.Colored();
            if (!painted)
            {
                // paper wash with a faint grid every 100 m
                Vector2 wc = rC;
                float reach = size * 0.9f;
                for (float gx = Mathf.Floor((wc.x - reach) / 100f) * 100f; gx <= wc.x + reach; gx += 100f)
                    MapGL.Seg(W2M(new Vector2(gx, wc.y - reach)), W2M(new Vector2(gx, wc.y + reach)), 1f, new Color(0.45f, 0.35f, 0.3f, 0.12f));
                for (float gz = Mathf.Floor((wc.y - reach) / 100f) * 100f; gz <= wc.y + reach; gz += 100f)
                    MapGL.Seg(W2M(new Vector2(wc.x - reach, gz)), W2M(new Vector2(wc.x + reach, gz)), 1f, new Color(0.45f, 0.35f, 0.3f, 0.12f));
            }
            Roads(painted, s);

            // the event being driven
            var st = StageManager.Instance;
            if (st != null && st.Running && st.CurrentEvent >= 0)
            {
                var course = MapMarkers.Course(st.CurrentEvent);
                Line(course, null, 7f * s, MenuArt.A(MenuArt.Ink, 0.55f));
                Line(course, null, 4f * s, new Color(1f, 0.82f, 0.3f, 1f));
            }
            // GPS route
            if (Gps.HasRoute)
            {
                Line(Gps.Path, center, 8.5f * s, MenuArt.A(MenuArt.Ink, 0.7f));
                Line(Gps.Path, center, 5f * s, MapMarkers.RouteCol);
                Line(Gps.Path, center, 1.6f * s, new Color(1f, 0.9f, 0.95f, 0.85f));
            }
            else if (Gps.Active)
                Dashed(center, Gps.Waypoint, 3f * s, MapMarkers.RouteCol);
            MapGL.EndTarget();
        }

        void Roads(bool painted, float s)
        {
            var g = Gps.Graph;
            if (g == null) return;
            float reach = size * 0.95f, r2 = reach * reach;
            float casing = (painted ? 4.2f : 6f) * s, core = (painted ? 2.2f : 3.6f) * s;
            Color ink = painted ? new Color(0.12f, 0.09f, 0.1f, 0.28f) : new Color(0.12f, 0.09f, 0.1f, 0.7f);
            Color fill = painted ? new Color(1f, 0.97f, 0.9f, 0.55f) : new Color(1f, 0.98f, 0.94f, 1f);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i + 1 < g.Count; i++)
                {
                    if (g.Road[i] != g.Road[i + 1]) continue;
                    Vector2 a = g.Pos[i];
                    if ((a - rC).sqrMagnitude > r2) continue;
                    if (pass == 0) MapGL.Seg(W2M(a), W2M(g.Pos[i + 1]), casing, ink);
                    else MapGL.Seg(W2M(a), W2M(g.Pos[i + 1]), core, fill);
                }
        }

        void Line(System.Collections.Generic.List<Vector2> pts, Vector2? from, float w, Color c)
        {
            if (pts == null || pts.Count == 0) return;
            float reach = size * 1.1f, r2 = reach * reach;
            Vector2 prev = from.HasValue ? from.Value : pts[0];
            bool prevIn = (prev - rC).sqrMagnitude < r2;
            for (int i = from.HasValue ? 0 : 1; i < pts.Count; i++)
            {
                Vector2 p = pts[i];
                bool pin = (p - rC).sqrMagnitude < r2;
                if (pin || prevIn) MapGL.Seg(W2M(prev), W2M(p), w, c);
                prev = p; prevIn = pin;
            }
        }

        void Dashed(Vector2 a, Vector2 b, float w, Color c)
        {
            float len = Vector2.Distance(a, b);
            if (len < 1f) return;
            float step = 14f / rPpm * MenuArt.S;
            int n = Mathf.Min(400, Mathf.CeilToInt(len / step));
            for (int i = 0; i < n; i += 2)
            {
                Vector2 p0 = Vector2.Lerp(a, b, i / (float)n), p1 = Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)n));
                MapGL.Seg(W2M(p0), W2M(p1), w, c);
            }
        }

        // ------------------------------------------------------------------ drawing
        void OnGUI()
        {
            if (!visible || rt == null || Event.current.type != EventType.Repaint) return;
            GUI.depth = 2;
            MenuArt.Styles();
            float s = MenuArt.S, k = MenuArt.Ease(fade);
            Rect r = rect;
            float rad = 18f * s, bw = Mathf.Max(2f, 3f * s);
            var white = Texture2D.whiteTexture;

            // drop shadow, ink frame, map, inner vignette, paper hairline
            MenuArt.Tex(new Rect(r.x - 14f * s, r.y - 8f * s, r.width + 28f * s, r.height + 26f * s), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.55f * k));
            GUI.DrawTexture(new Rect(r.x - bw, r.y - bw, r.width + bw * 2f, r.height + bw * 2f), white, ScaleMode.StretchToFill, true, 0f,
                            MenuArt.A(MenuArt.Ink, 0.92f * k), 0f, rad + bw);
            GUI.DrawTexture(r, rt, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, k), 0f, rad);
            GUI.DrawTexture(r, MenuArt.Vignette, ScaleMode.StretchToFill, true, 0f, new Color(0.10f, 0.05f, 0.08f, 0.45f * k), 0f, rad);
            GUI.DrawTexture(r, white, ScaleMode.StretchToFill, true, 0f, MenuArt.A(MenuArt.PaperCol, 0.55f * k), Mathf.Max(1f, 1.2f * s), rad);

            Rect inner = new Rect(r.x + 13f * s, r.y + 13f * s, r.width - 26f * s, r.height - 26f * s);
            Vector2 piv = r.position + rPivot;
            Markers(r, inner, piv, s, k);

            // player arrow (relative to the map rotation)
            var gm = GameManager.Instance;
            if (gm != null && gm.Car != null)
            {
                Vector3 cf = gm.Car.transform.forward;
                float rel = (Mathf.Atan2(cf.x, cf.z) * Mathf.Rad2Deg - heading) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(rel), -Mathf.Cos(rel));
                MenuArt.Tex(new Rect(piv.x - 22f * s, piv.y - 22f * s, 44f * s, 44f * s), MenuArt.Glow, new Color(1f, 1f, 1f, 0.55f * k));
                if (MapGL.BeginScreen(r))
                {
                    MapGL.Arrow(piv, dir, 10f * s, new Color(1f, 0.98f, 0.95f, k), MenuArt.A(MenuArt.Ink, k));
                    MapGL.EndScreen();
                }
            }
            Compass(r, piv, s, k);
            if (Gps.Active) DistanceChip(r, s, k);
            RegionStrip(r, s, k);
        }

        Vector2 Clamp(Rect inner, Vector2 piv, Vector2 p, out bool clamped)
        {
            clamped = !inner.Contains(p);
            if (!clamped) return p;
            Vector2 d = p - piv;
            float t = 1f;
            if (d.x > 1e-4f) t = Mathf.Min(t, (inner.xMax - piv.x) / d.x);
            else if (d.x < -1e-4f) t = Mathf.Min(t, (inner.xMin - piv.x) / d.x);
            if (d.y > 1e-4f) t = Mathf.Min(t, (inner.yMax - piv.y) / d.y);
            else if (d.y < -1e-4f) t = Mathf.Min(t, (inner.yMin - piv.y) / d.y);
            return piv + d * Mathf.Max(0f, t);
        }

        void Markers(Rect r, Rect inner, Vector2 piv, float s, float k)
        {
            var list = MapMarkers.All;
            var st = StageManager.Instance;
            bool racing = st != null && st.Running;
            Vector2 o = r.position;
            float far = size * 1.5f;
            // pass 0: landmarks, passes, fast travel (never clamped); pass 1: garage + events (clamped when near)
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    bool important = m.eventIndex >= 0 || m.icon == MapIcon.Garage;
                    if (important != (pass == 1)) continue;
                    if (racing && m.eventIndex >= 0) continue;
                    float d2 = (m.pos - rC).sqrMagnitude;
                    if (!important && d2 > far * far) continue;
                    Vector2 p = o + W2M(m.pos);
                    bool clamped;
                    p = Clamp(inner, piv, p, out clamped);
                    float bs;
                    Color ring = new Color(0f, 0f, 0f, 0f);
                    if (m.eventIndex >= 0)
                    {
                        if (clamped && d2 > 750f * 750f) continue;
                        bs = 21f * s;
                        int medal = GameSettings.BestMedal(StageKey(m.eventIndex));
                        if (medal > 0) ring = MenuArt.MedalColor(medal);
                    }
                    else if (m.icon == MapIcon.Garage) { if (clamped && d2 > 900f * 900f) continue; bs = 21f * s; }
                    else
                    {
                        if (clamped) continue;
                        bs = (m.travel ? 17f : 15f) * s;
                    }
                    if (clamped) bs *= 0.85f;
                    MapMarkers.Badge(p, bs, m.icon, MapMarkers.IconColor(m.icon), ring, k * (m.landmark ? 0.82f : 1f));
                }
            if (Gps.Active)
            {
                bool clamped;
                Vector2 p = Clamp(new Rect(inner.x, inner.y + 10f * s, inner.width, inner.height - 10f * s), piv, o + W2M(Gps.Waypoint), out clamped);
                MapMarkers.Pin(p + new Vector2(0f, clamped ? 9f * s : 0f), (clamped ? 20f : 24f) * s, k);
            }
        }

        static string[] stageKeys;
        static string StageKey(int i)
        {
            var defs = StageDef.All;
            if (stageKeys == null || stageKeys.Length != defs.Length) stageKeys = new string[defs.Length];
            if (stageKeys[i] == null) stageKeys[i] = "Stage_" + defs[i].key;
            return stageKeys[i];
        }

        void Compass(Rect r, Vector2 piv, float s, float k)
        {
            // north = world +z; ride the rim of the minimap
            Vector2 dir = new Vector2(-rSin, -rCos);
            Rect rim = new Rect(r.x + 14f * s, r.y + 14f * s, r.width - 28f * s, r.height - 28f * s);
            bool clamped;
            Vector2 p = Clamp(rim, piv, piv + dir * 4000f, out clamped);
            float d = 22f * s;
            MenuArt.Tex(new Rect(p.x - d * 0.5f, p.y - d * 0.5f, d, d), MenuArt.Dot, MenuArt.A(MenuArt.Ink, 0.92f * k));
            MenuArt.Tex(new Rect(p.x - d * 0.5f, p.y - d * 0.5f, d, d), MenuArt.Ring, MenuArt.A(MenuArt.PaperCol, 0.7f * k));
            MenuArt.Text(new Rect(p.x - d * 0.5f, p.y - d * 0.5f - 1f, d, d), "N", MenuArt.TinyC, new Color(1f, 0.42f, 0.36f, k), false);
        }

        void DistanceChip(Rect r, float s, float k)
        {
            string txt = Gps.HasRoute || Gps.Remaining > 0f ? dist.Get(Gps.Remaining) : "--";
            float tw = MenuArt.Small.CalcSize(TempContent(txt)).x;
            float h = 26f * s, w = tw + 40f * s;
            Rect chip = new Rect(r.xMax - w - 10f * s, r.y + 10f * s, w, h);
            GUI.DrawTexture(chip, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, MenuArt.A(MenuArt.Ink, 0.82f * k), 0f, h * 0.5f);
            MenuArt.Tex(new Rect(chip.x + 7f * s, chip.y + 4f * s, 18f * s, 18f * s), MapMarkers.Icon(MapIcon.Waypoint), MenuArt.A(MapMarkers.RouteCol, k));
            MenuArt.Text(new Rect(chip.x + 29f * s, chip.y, tw + 8f * s, h), txt, MenuArt.Small, MenuArt.A(UI.White, k), false);
        }

        static readonly GUIContent temp = new GUIContent();
        static GUIContent TempContent(string t) { temp.text = t; return temp; }

        void RegionStrip(Rect r, float s, float k)
        {
            var id = RegionRuntime.Current;
            string name = MapMarkers.RegionUpper(id), hz = MapMarkers.RegionHanzi(id);
            if ((int)id != regionShown || hanziW <= 0f)
            {
                regionShown = (int)id;
                hanziW = hz.Length > 0 ? MenuArt.MidL.CalcSize(TempContent(hz)).x : 0f;
                nameW = MenuArt.Small.CalcSize(TempContent(name)).x;
            }
            float y = r.yMax + 7f * s, h = 26f * s, x = r.x + 2f * s;
            Color acc = MapMarkers.RegionAccent(id);
            MenuArt.Fill(new Rect(x, y + 3f * s, 4f * s, h - 6f * s), MenuArt.A(acc, k));
            x += 12f * s;
            if (hanziW > 0f)
            {
                MenuArt.Text(new Rect(x, y, hanziW + 6f * s, h), hz, MenuArt.MidL, MenuArt.A(acc, k));
                x += hanziW + 12f * s;
            }
            MenuArt.Text(new Rect(x, y + 1f * s, nameW + 8f * s, h), name, MenuArt.Small, MenuArt.A(UI.White, 0.95f * k));
        }
    }
}
