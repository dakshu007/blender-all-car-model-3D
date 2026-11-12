// ValleyMap.World.cs - the free-roam WORLD MAP mode of ValleyMap (M / d-pad down while driving; GameManager pauses).
//
// Same hanging scroll and painted map as the event chooser, but: the view starts zoomed in on the player and pans /
// zooms freely (drag, wheel, sticks, WASD, triggers, Q / E); every MapMarkers point is drawn as an ink badge with a
// label (labels appear as you zoom in), region names + hanzi when zoomed out, the region borders, the stage courses,
// the player arrow and the GPS route (flowing dashes towards the waypoint). The right panel shows the place under the
// cursor (mouse, or the crosshair in the middle when using a pad / keys), the GPS status, a legend and the controls.
//   click / A / ENTER : set a waypoint (snaps to the marker under the cursor); again on it clears it (also DEL / LB / right click)
//   X / F             : fast travel to a region point, or race the event under the cursor (not during an event)
//   Y / TAB           : back to the player      B / ESC / M / d-pad down : close
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public partial class ValleyMap
    {
        bool worldMode;
        System.Action onWorldClose;
        System.Action<RegionId> onTravel;
        System.Action<int> onRace;
        Vector2 worldC;
        float worldSize = 900f;
        bool padCursor = true;
        MapPoi hoverPoi;
        Vector2 guiMouse = new Vector2(-1000f, -1000f);
        readonly DistText wpDist = new DistText(), poiDist = new DistText();
        readonly List<Vector2> scr = new List<Vector2>();
        static readonly string[] youAre = new string[6];
        const float MinWorldSize = 160f;

        static readonly MapIcon[] LegendIcons =
        {
            MapIcon.You, MapIcon.Waypoint, MapIcon.TimeAttack, MapIcon.Drift, MapIcon.Garage, MapIcon.FastTravel, MapIcon.Pass,
            MapIcon.Pagoda, MapIcon.Temple, MapIcon.Water, MapIcon.Bridge, MapIcon.Lighthouse, MapIcon.Viewpoint, MapIcon.Town,
            MapIcon.Peak, MapIcon.Observatory, MapIcon.Harbour, MapIcon.Gate, MapIcon.Nature, MapIcon.Star
        };

        // ================================================================== open / close
        /// <summary>
        /// Open the world map. onClose runs when it closes (restore time scale); travel(region) / race(eventIndex) are
        /// offered on fast-travel points / stage starts when not null (they run after onClose).
        /// </summary>
        public static void OpenWorld(System.Action onClose, System.Action<RegionId> travel, System.Action<int> race)
        {
            if (Instance == null) { if (onClose != null) onClose(); return; }
            Instance.DoOpenWorld(onClose, travel, race);
        }

        void DoOpenWorld(System.Action onClose, System.Action<RegionId> travel, System.Action<int> race)
        {
            if (world == null && WorldHooks.World != null) OnWorld(WorldHooks.World);
            else if (routes.Count == 0 && world != null) BuildRoutes();
            worldMode = true;
            onPick = null; onBack = null;
            onWorldClose = onClose; onTravel = travel; onRace = race;
            open = true;
            openFrame = Time.frameCount;
            openTime = Time.unscaledTime;
            Vector2 me = PlayerPos();
            worldC = me;
            worldSize = 900f;
            viewC = me;
            viewSize = 2600f;          // zooms in onto the player as the scroll unrolls
            padCursor = true;
            hoverPoi = null;
            dragging = false;
            MapMarkers.Ensure();
            Gps.Track(me);
            UI.Sound("Whoosh");
        }

        void CloseWorld(System.Action after)
        {
            open = false;
            closeTime = Time.unscaledTime;
            dragging = false;
            var cb = onWorldClose;
            onWorldClose = null; onTravel = null; onRace = null;
            if (cb != null) cb();
            if (after != null) after();
        }

        static Vector2 PlayerPos()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Car != null) { Vector3 p = gm.Car.transform.position; return new Vector2(p.x, p.z); }
            return MapData.Garage;
        }

        static Vector2 PlayerDir()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.Car != null) { Vector3 f = gm.Car.transform.forward; return new Vector2(f.x, -f.z); }
            return new Vector2(0f, -1f);
        }

        Vector2 CursorWorld() { return padCursor ? viewC : S2W(guiMouse); }

        // ================================================================== input
        void UpdateWorld(float dt)
        {
            if (InputReader.MenuBack || InputReader.ActionDown(GameAction.Map)) { UI.Sound("MenuBack"); CloseWorld(null); return; }
            Vector2 mv = Vector2.zero;
            Vector2 ls = GamepadBridge.LeftStick, rs = GamepadBridge.RightStick;
            if (ls.sqrMagnitude > 0.04f) mv += ls;
            if (rs.sqrMagnitude > 0.04f) mv += rs;
            if (InputReader.Key(KeyCode.W) || InputReader.Key(KeyCode.UpArrow)) mv.y += 1f;
            if (InputReader.Key(KeyCode.S) || InputReader.Key(KeyCode.DownArrow)) mv.y -= 1f;
            if (InputReader.Key(KeyCode.A) || InputReader.Key(KeyCode.LeftArrow)) mv.x -= 1f;
            if (InputReader.Key(KeyCode.D) || InputReader.Key(KeyCode.RightArrow)) mv.x += 1f;
            if (mv.sqrMagnitude > 1e-4f) { worldC += Vector2.ClampMagnitude(mv, 1.4f) * worldSize * 0.7f * dt; padCursor = true; }
            float z = 0f;
            if (InputReader.Key(KeyCode.Equals) || InputReader.Key(KeyCode.KeypadPlus) || InputReader.Key(KeyCode.E)) z -= 1f;
            if (InputReader.Key(KeyCode.Minus) || InputReader.Key(KeyCode.KeypadMinus) || InputReader.Key(KeyCode.Q)) z += 1f;
            z += GamepadBridge.LeftTrigger - GamepadBridge.RightTrigger;
            if (Mathf.Abs(z) > 0.05f) worldSize = Mathf.Clamp(worldSize * Mathf.Exp(z * dt * 1.5f), MinWorldSize, MapSize);
            if (InputReader.Down(KeyCode.Tab) || GamepadBridge.ButtonDown(PadButton.North)) { worldC = PlayerPos(); padCursor = true; UI.Sound("Camera"); }
            if (InputReader.MenuConfirm) ToggleWaypoint(CursorWorld(), hoverPoi);
            if ((InputReader.Down(KeyCode.Delete) || GamepadBridge.ButtonDown(PadButton.LeftShoulder)) && Gps.Active) { Gps.Clear(); UI.Sound("MenuBack"); }
            if (InputReader.Down(KeyCode.F) || GamepadBridge.ButtonDown(PadButton.West)) DoAction(hoverPoi);
        }

        bool CanAct(MapPoi m)
        {
            return m != null && ((m.travel && onTravel != null) || (m.eventIndex >= 0 && onRace != null));
        }

        void DoAction(MapPoi m)
        {
            if (!CanAct(m)) return;
            UI.Sound("MenuSelect");
            if (m.travel)
            {
                var t = onTravel;
                var id = m.travelTo;
                CloseWorld(() => t(id));
            }
            else
            {
                var r = onRace;
                int ev = m.eventIndex;
                CloseWorld(() => r(ev));
            }
        }

        void ToggleWaypoint(Vector2 w, MapPoi poi)
        {
            Vector2 target = poi != null ? poi.pos : w;
            float tol = 24f * MenuArt.S;
            if (Gps.Active && (W2S(Gps.Waypoint) - W2S(target)).sqrMagnitude < tol * tol)
            {
                Gps.Clear();
                UI.Sound("MenuBack");
                return;
            }
            Gps.Set(target, poi != null ? poi.name : null);
            Gps.Track(PlayerPos());
            UI.Sound("MenuSelect");
        }

        void HandleMouseWorld(Event e)
        {
            Vector2 mp = e.mousePosition;
            guiMouse = mp;
            bool overMap = mapRect.Contains(mp);
            if (mouseMoved && overMap) padCursor = false;
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    if (overMap)
                    {
                        float f = Mathf.Exp(e.delta.y * 0.08f);
                        Vector2 before = S2W(mp);
                        float ns = Mathf.Clamp(worldSize * f, MinWorldSize, MapSize);
                        worldC = before + (worldC - before) * (ns / worldSize);
                        worldSize = ns;
                        padCursor = false;
                        e.Use();
                    }
                    break;
                case EventType.MouseDown:
                    if (overMap && e.button == 1)
                    {
                        if (Gps.Active) { Gps.Clear(); UI.Sound("MenuBack"); }
                        e.Use();
                    }
                    else if (overMap)
                    {
                        dragging = true;
                        dragMoved = false;
                        dragStart = lastMouse = mp;
                        padCursor = false;
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
                        Vector2 dw = new Vector2(-d.x, d.y) * mPerPx;
                        worldC += dw;
                        viewC += dw;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (dragging)
                    {
                        dragging = false;
                        if (!dragMoved) ToggleWaypoint(S2W(mp), PoiAt(mp, 20f * MenuArt.S));
                        e.Use();
                    }
                    break;
            }
        }

        void AnimateWorld(float dt)
        {
            float half = worldSize * 0.5f;
            worldC.x = Mathf.Clamp(worldC.x, MapC.x - MapSize * 0.5f + half, MapC.x + MapSize * 0.5f - half);
            worldC.y = Mathf.Clamp(worldC.y, MapC.y - MapSize * 0.5f + half, MapC.y + MapSize * 0.5f - half);
            bool intro = Time.unscaledTime < openTime + 0.9f;
            viewSize = Mathf.Exp(Mathf.Lerp(Mathf.Log(viewSize), Mathf.Log(worldSize), MenuArt.Damp(dt, intro ? 4f : 11f)));
            viewC = Vector2.Lerp(viewC, worldC, MenuArt.Damp(dt, 12f));
            float h2 = viewSize * 0.5f;
            viewC.x = Mathf.Clamp(viewC.x, MapC.x - MapSize * 0.5f + h2, MapC.x + MapSize * 0.5f - h2);
            viewC.y = Mathf.Clamp(viewC.y, MapC.y - MapSize * 0.5f + h2, MapC.y + MapSize * 0.5f - h2);
        }

        MapPoi PoiAt(Vector2 sp, float maxPx)
        {
            MapPoi best = null;
            float bd = maxPx * maxPx;
            var list = MapMarkers.All;
            for (int i = 0; i < list.Count; i++)
            {
                float d = (W2S(list[i].pos) - sp).sqrMagnitude;
                if (list[i].landmark) d *= 1.3f;        // stage starts / travel points win ties
                if (d < bd) { bd = d; best = list[i]; }
            }
            return best;
        }

        // ================================================================== map drawing
        void DrawWorld(Rect clip, float s, float now)
        {
            float zk = Mathf.Clamp(1100f / viewSize, 0.85f, 1.3f);
            Vector2 me = PlayerPos();
            Vector2 cursor = padCursor ? mapRect.center : guiMouse;
            hoverPoi = (padCursor || clip.Contains(guiMouse)) && open ? PoiAt(cursor, 20f * s) : null;

            // ---- lines (GL) ----
            if (InkGL.Begin(clip))
            {
                Color border = MenuArt.A(MenuArt.Ink, 0.35f);
                Dash(new Vector2(-500f, -500f), new Vector2(-500f, 1500f), 1.6f * s, border, 10f * s);
                Dash(new Vector2(500f, -500f), new Vector2(500f, 1500f), 1.6f * s, border, 10f * s);
                Dash(new Vector2(-1500f, 500f), new Vector2(1500f, 500f), 1.6f * s, border, 10f * s);
                Color edge = MenuArt.A(MenuArt.Vermilion, 0.35f);
                Dash(new Vector2(-1500f, -500f), new Vector2(1500f, -500f), 2f * s, edge, 14f * s);
                Dash(new Vector2(-1500f, 1500f), new Vector2(1500f, 1500f), 2f * s, edge, 14f * s);
                Dash(new Vector2(-1500f, -500f), new Vector2(-1500f, 1500f), 2f * s, edge, 14f * s);
                Dash(new Vector2(1500f, -500f), new Vector2(1500f, 1500f), 2f * s, edge, 14f * s);
                for (int i = 0; i < routes.Count; i++)
                {
                    Path(routes[i], 1f, 3.6f * s, MenuArt.A(MenuArt.Ink, 0.3f));
                    Path(routes[i], 1f, 1.9f * s, MenuArt.A(routes[i].col, 0.6f));
                }
                if (Gps.Active)
                {
                    if (Gps.HasRoute)
                    {
                        ScreenLine(me, Gps.Path);
                        Poly(10f * s, MenuArt.A(MenuArt.Ink, 0.75f));
                        Poly(6f * s, MapMarkers.RouteCol);
                        Flow(2.4f * s, new Color(1f, 0.95f, 0.97f, 0.95f), now, s);
                    }
                    else Dash(me, Gps.Waypoint, 3f * s, MapMarkers.RouteCol, 12f * s);
                }
                InkGL.End();
            }

            // ---- region names (zoomed out) ----
            float ra = Mathf.InverseLerp(650f, 1150f, viewSize);
            if (ra > 0.01f && world != null && world.Field != null)
                foreach (var id in WorldLayout.All)
                {
                    var reg = world.Field.RegionOf(id);
                    if (reg == null) continue;
                    Vector2 p = W2S(reg.Center + new Vector2(0f, 360f));
                    if (!clip.Contains(p)) continue;
                    Color acc = reg.Accent;
                    Rect nr = new Rect(p.x - 220f * s, p.y - 4f * s, 440f * s, 30f * s);
                    MenuArt.Tex(new Rect(p.x - 150f * s, p.y - 46f * s, 300f * s, 86f * s), MenuArt.Glow, MenuArt.A(MenuArt.PaperCol, 0.85f * ra));
                    string hz = MapMarkers.RegionHanzi(id);
                    if (hz.Length > 0)
                        MenuArt.Text(new Rect(p.x - 120f * s, p.y - 42f * s, 240f * s, 40f * s), hz, MenuArt.Title,
                                     new Color(acc.r * 0.62f, acc.g * 0.62f, acc.b * 0.62f, 0.95f * ra), false);
                    string nm = MapMarkers.RegionUpper(id);
                    MenuArt.Text(nr, nm, MenuArt.Mid, MenuArt.A(MenuArt.Ink, 0.92f * ra), false);
                    float uw = nm.Length * 12.5f * s;
                    MenuArt.Fill(new Rect(p.x - uw * 0.5f, nr.yMax - 1f * s, uw, 3f * s), new Color(acc.r * 0.8f, acc.g * 0.8f, acc.b * 0.8f, 0.9f * ra));
                }

            // ---- markers ----
            var list = MapMarkers.All;
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    int mp = m.eventIndex >= 0 ? 2 : (m.travel || m.icon == MapIcon.Garage ? 1 : 0);
                    if (mp != pass || m == hoverPoi) continue;
                    DrawPoi(m, clip, s, zk, false);
                }
            if (hoverPoi != null) DrawPoi(hoverPoi, clip, s, zk, true);

            // ---- waypoint ----
            if (Gps.Active)
            {
                Vector2 wp = W2S(Gps.Waypoint);
                if (clip.Contains(wp))
                {
                    float ph = Mathf.Repeat(now * 0.9f, 1f);
                    float rr = (14f + 22f * ph) * s;
                    MenuArt.Tex(new Rect(wp.x - rr, wp.y - rr * 0.5f, rr * 2f, rr), MenuArt.Ring, MenuArt.A(MapMarkers.RouteCol, 1f - ph));
                    MapMarkers.Pin(wp, 34f * s * zk, 1f);
                }
            }

            // ---- player ----
            DrawPlayer(clip, me, s, now);

            // ---- cursor ----
            if (open && padCursor)
            {
                Vector2 c = mapRect.center;
                float cr = 16f * s;
                MenuArt.Tex(new Rect(c.x - cr, c.y - cr, cr * 2f, cr * 2f), MenuArt.Ring, MenuArt.A(MenuArt.Ink, 0.85f));
                float t = Mathf.Max(1f, 2f * s), l = 9f * s;
                MenuArt.Fill(new Rect(c.x - cr - l, c.y - t * 0.5f, l, t), MenuArt.Ink);
                MenuArt.Fill(new Rect(c.x + cr, c.y - t * 0.5f, l, t), MenuArt.Ink);
                MenuArt.Fill(new Rect(c.x - t * 0.5f, c.y - cr - l, t, l), MenuArt.Ink);
                MenuArt.Fill(new Rect(c.x - t * 0.5f, c.y + cr, t, l), MenuArt.Ink);
                MenuArt.Fill(new Rect(c.x - t, c.y - t, t * 2f, t * 2f), MenuArt.Vermilion);
            }

            Compass(clip, s);
            ScaleBar(clip, s);
            Cartouche(clip, s);
        }

        void DrawPoi(MapPoi m, Rect clip, float s, float zk, bool hover)
        {
            Vector2 p = W2S(m.pos);
            if (!clip.Contains(p)) return;
            float baseSize = m.eventIndex >= 0 || m.icon == MapIcon.Garage ? 27f : m.travel ? 24f : m.icon == MapIcon.Pass ? 20f : 21f;
            float sz = baseSize * s * zk * (hover ? 1.3f : 1f);
            Color ring = new Color(0f, 0f, 0f, 0f);
            if (m.eventIndex >= 0)
            {
                int medal = GameSettings.BestMedal(StageKey(m.eventIndex));
                if (medal > 0) ring = MenuArt.MedalColor(medal);
            }
            if (hover)
            {
                float hr = sz * 0.95f;
                MenuArt.Tex(new Rect(p.x - hr, p.y - hr, hr * 2f, hr * 2f), MenuArt.Glow, MenuArt.A(UI.Gold, 0.9f));
            }
            MapMarkers.Badge(p, sz, m.icon, MapMarkers.IconColor(m.icon), ring, m.landmark && !hover ? 0.9f : 1f);
            bool label = hover || (m.landmark ? viewSize < 1000f : viewSize < 1500f);
            if (!label) return;
            var st = MenuArt.Tiny;
            if (m.labelStyleH != Screen.height || m.labelW < 0f)
            {
                Vector2 ls = st.CalcSize(new GUIContent(m.name));
                m.labelW = ls.x + 6f; m.labelH = ls.y; m.labelStyleH = Screen.height;
            }
            Rect r = new Rect(p.x - m.labelW * 0.5f, p.y + sz * 0.5f + 3f * s, m.labelW, m.labelH);
            if (hover)
            {
                Rect tag = new Rect(r.x - 8f * s, r.y - 2f * s, r.width + 16f * s, r.height + 4f * s);
                MenuArt.Fill(tag, MenuArt.A(MenuArt.Ink, 0.88f));
                MenuArt.Fill(new Rect(tag.x, tag.y, 3f * s, tag.height), MapMarkers.IconColor(m.icon) == UI.White ? UI.Pink : MapMarkers.IconColor(m.icon));
                MenuArt.Text(r, m.name, st, MenuArt.PaperCol, false);
                return;
            }
            MenuArt.Tex(new Rect(r.x - 10f * s, r.y - 4f * s, r.width + 20f * s, r.height + 8f * s), MenuArt.Glow, MenuArt.A(MenuArt.PaperCol, 0.9f));
            var old = st.fontStyle;
            st.fontStyle = FontStyle.Italic;
            MenuArt.Text(r, m.name, st, MenuArt.A(MenuArt.Ink, m.landmark ? 0.85f : 1f), false);
            st.fontStyle = old;
        }

        void DrawPlayer(Rect clip, Vector2 me, float s, float now)
        {
            Vector2 p = W2S(me);
            Vector2 dir = PlayerDir();
            Rect inner = new Rect(clip.x + 22f * s, clip.y + 22f * s, clip.width - 44f * s, clip.height - 44f * s);
            bool inside = inner.Contains(p);
            if (!inside)
            {
                // off the view: an arrow on the edge pointing towards the player
                Vector2 c = inner.center, d = p - c;
                float t = 1f;
                if (Mathf.Abs(d.x) > 1e-3f) t = Mathf.Min(t, (d.x > 0f ? inner.xMax - c.x : inner.xMin - c.x) / d.x);
                if (Mathf.Abs(d.y) > 1e-3f) t = Mathf.Min(t, (d.y > 0f ? inner.yMax - c.y : inner.yMin - c.y) / d.y);
                p = c + d * t;
                dir = d;
            }
            else
            {
                float ph = Mathf.Repeat(now * 0.7f, 1f);
                float rr = (16f + 26f * ph) * s;
                MenuArt.Tex(new Rect(p.x - rr, p.y - rr, rr * 2f, rr * 2f), MenuArt.Ring, new Color(1f, 1f, 1f, 0.8f * (1f - ph)));
            }
            MenuArt.Tex(new Rect(p.x - 26f * s, p.y - 26f * s, 52f * s, 52f * s), MenuArt.Glow, new Color(1f, 1f, 1f, 0.85f));
            if (MapGL.BeginScreen(clip))
            {
                MapGL.Arrow(p, dir, (inside ? 14f : 10f) * s, new Color(1f, 0.98f, 0.95f, 1f), MenuArt.Ink);
                MapGL.EndScreen();
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

        // ---- GL line helpers (inside InkGL.Begin) ----
        void Dash(Vector2 wa, Vector2 wb, float w, Color c, float dashPx)
        {
            Vector2 a = W2S(wa), b = W2S(wb);
            float len = Vector2.Distance(a, b);
            if (len < 1f) return;
            int n = Mathf.Min(600, Mathf.CeilToInt(len / dashPx));
            for (int i = 0; i < n; i += 2)
                InkGL.Seg(Vector2.Lerp(a, b, i / (float)n), Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)n)), w, c);
        }

        /// <summary>Route in screen space (player first), thinned to about 3 px steps.</summary>
        void ScreenLine(Vector2 me, List<Vector2> pts)
        {
            scr.Clear();
            scr.Add(W2S(me));
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = W2S(pts[i]);
                if (i < pts.Count - 1 && (p - scr[scr.Count - 1]).sqrMagnitude < 9f) continue;
                scr.Add(p);
            }
        }

        void Poly(float w, Color c)
        {
            for (int i = 0; i + 1 < scr.Count; i++) InkGL.Seg(scr[i], scr[i + 1], w, c);
        }

        /// <summary>Short bright dashes travelling along the route towards the waypoint.</summary>
        void Flow(float w, Color c, float now, float s)
        {
            float period = 24f * s, dash = 8f * s;
            float phase = Mathf.Repeat(now * 34f * s, period);
            float acc = 0f;
            for (int i = 0; i + 1 < scr.Count; i++)
            {
                Vector2 a = scr[i], b = scr[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 1e-3f) continue;
                float t0 = acc, t1 = acc + len;
                float d = phase + Mathf.Floor((t0 - phase) / period) * period;
                for (; d < t1; d += period)
                {
                    float ds = Mathf.Max(d, t0), de = Mathf.Min(d + dash, t1);
                    if (de > ds) InkGL.Seg(Vector2.Lerp(a, b, (ds - t0) / len), Vector2.Lerp(a, b, (de - t0) / len), w, c);
                }
                acc = t1;
            }
        }

        // ================================================================== right panel
        void DrawWorldPanel(Rect r, float s, float alpha, float now)
        {
            r.x += (1f - alpha) * 60f * s;
            MenuArt.Text(new Rect(r.x, r.y - 4f * s, r.width, 50f * s), "WORLD MAP", MenuArt.Title, MenuArt.A(UI.Pink, alpha));
            MenuArt.Text(new Rect(r.x, r.y + 40f * s, r.width, 26f * s), YouAre(RegionRuntime.Current), MenuArt.SmallC, MenuArt.A(UI.White, 0.8f * alpha));
            float y = r.y + 78f * s;

            // ---- place under the cursor ----
            Rect card = new Rect(r.x, y, r.width, 150f * s);
            MenuArt.Card(card, alpha);
            var m = hoverPoi;
            if (m != null)
            {
                Color col = MapMarkers.IconColor(m.icon);
                MenuArt.Fill(new Rect(card.x, card.y, 6f * s, card.height), MenuArt.A(col, alpha));
                Color ring = new Color(0f, 0f, 0f, 0f);
                int medal = m.eventIndex >= 0 ? GameSettings.BestMedal(StageKey(m.eventIndex)) : 0;
                if (medal > 0) ring = MenuArt.MedalColor(medal);
                MapMarkers.Badge(new Vector2(card.x + 44f * s, card.y + 42f * s), 46f * s, m.icon, col, ring, alpha);
                float x = card.x + 82f * s, w = card.width - 96f * s;
                MenuArt.Text(new Rect(x, card.y + 8f * s, w, 32f * s), m.name, MenuArt.MidL, MenuArt.A(UI.White, alpha));
                MenuArt.Text(new Rect(x, card.y + 38f * s, w, 22f * s), m.KindLine, MenuArt.Tiny, MenuArt.A(UI.Pink, alpha));
                MenuArt.Text(new Rect(x, card.y + 60f * s, w, 46f * s), m.hint ?? "", MenuArt.Small, MenuArt.A(UI.White, 0.85f * alpha));
                float by = card.yMax - 34f * s;
                MenuArt.Text(new Rect(card.x + 22f * s, by, 200f * s, 26f * s), poiDist.Get(Vector2.Distance(PlayerPos(), m.pos)), MenuArt.Small, MenuArt.A(UI.Gold, alpha));
                MenuArt.Text(new Rect(card.x + 22f * s + 64f * s, by, 120f * s, 26f * s), "away", MenuArt.Tiny, MenuArt.A(UI.White, 0.6f * alpha));
                string act = null;
                if (m.travel) act = onTravel != null ? "X / F   FAST TRAVEL HERE" : "no fast travel during an event";
                else if (m.eventIndex >= 0) act = onRace != null ? "X / F   RACE NOW" : "finish the current event first";
                if (act != null)
                {
                    bool can = CanAct(m);
                    float pulse = can ? 0.75f + 0.25f * Mathf.Sin(now * 5f) : 0.5f;
                    MenuArt.Text(new Rect(card.x + card.width * 0.3f, by, card.width * 0.7f - 16f * s, 26f * s), act, MenuArt.SmallR,
                                 MenuArt.A(can ? UI.Gold : UI.White, pulse * alpha));
                }
            }
            else
            {
                MenuArt.Text(new Rect(card.x, card.y + 30f * s, card.width, 30f * s), "Point at a place to see what it is", MenuArt.BodyC, MenuArt.A(UI.White, 0.9f * alpha));
                MenuArt.Text(new Rect(card.x, card.y + 66f * s, card.width, 26f * s), InputReader.PadConnected ? "left stick moves the crosshair  ·  A drops a waypoint"
                             : "drag to pan  ·  wheel to zoom  ·  click to drop a waypoint", MenuArt.SmallC, MenuArt.A(UI.White, 0.6f * alpha));
            }
            y = card.yMax + 12f * s;

            // ---- GPS ----
            Rect g = new Rect(r.x, y, r.width, 62f * s);
            MenuArt.Card(g, alpha, false);
            MenuArt.Fill(new Rect(g.x, g.y, 6f * s, g.height), MenuArt.A(MapMarkers.RouteCol, alpha));
            MenuArt.Tex(new Rect(g.x + 20f * s, g.y + 13f * s, 34f * s, 34f * s), MapMarkers.Icon(MapIcon.Waypoint), MenuArt.A(Gps.Active ? MapMarkers.RouteCol : UI.White, (Gps.Active ? 1f : 0.35f) * alpha));
            if (Gps.Active)
            {
                MenuArt.Text(new Rect(g.x + 66f * s, g.y + 6f * s, g.width * 0.6f, 22f * s), "GPS WAYPOINT", MenuArt.Tiny, MenuArt.A(UI.Pink, alpha));
                MenuArt.Text(new Rect(g.x + 66f * s, g.y + 26f * s, g.width * 0.6f, 30f * s), Gps.WaypointName, MenuArt.MidL, MenuArt.A(UI.White, alpha));
                float d = Gps.Remaining > 0f ? Gps.Remaining : Vector2.Distance(PlayerPos(), Gps.Waypoint);
                MenuArt.Text(new Rect(g.xMax - 220f * s, g.y + 4f * s, 204f * s, 34f * s), wpDist.Get(d), MenuArt.MidR, MenuArt.A(UI.Gold, alpha));
                MenuArt.Text(new Rect(g.xMax - 220f * s, g.y + 34f * s, 204f * s, 22f * s), Gps.HasRoute ? "by road" : "as the crow flies", MenuArt.SmallR, MenuArt.A(UI.White, 0.6f * alpha));
            }
            else
            {
                MenuArt.Text(new Rect(g.x + 66f * s, g.y + 6f * s, g.width - 80f * s, 26f * s), "No waypoint", MenuArt.MidL, MenuArt.A(UI.White, 0.85f * alpha));
                MenuArt.Text(new Rect(g.x + 66f * s, g.y + 32f * s, g.width - 80f * s, 22f * s), "set one and the GPS draws the road route here and on the minimap", MenuArt.Tiny, MenuArt.A(UI.White, 0.6f * alpha));
            }
            y = g.yMax + 16f * s;

            // ---- legend ----
            MenuArt.Text(new Rect(r.x, y, r.width, 22f * s), "LEGEND", MenuArt.Tiny, MenuArt.A(UI.Pink, alpha));
            y += 24f * s;
            float colW = r.width * 0.5f, rowH = 25f * s;
            int n = LegendIcons.Length + 1;
            for (int i = 0; i < n; i++)
            {
                float lx = r.x + (i % 2) * colW, ly = y + (i / 2) * rowH;
                Vector2 c = new Vector2(lx + 12f * s, ly + rowH * 0.5f);
                string label;
                if (i == LegendIcons.Length)
                {
                    MenuArt.Fill(new Rect(c.x - 12f * s, c.y - 4f * s, 24f * s, 8f * s), MenuArt.A(MenuArt.Ink, alpha));
                    MenuArt.Fill(new Rect(c.x - 11f * s, c.y - 2.5f * s, 22f * s, 5f * s), MenuArt.A(MapMarkers.RouteCol, alpha));
                    label = "GPS route";
                }
                else
                {
                    var ic = LegendIcons[i];
                    if (ic == MapIcon.Waypoint) MapMarkers.Pin(c + new Vector2(0f, 9f * s), 20f * s, alpha);
                    else MapMarkers.Badge(c, 20f * s, ic, ic == MapIcon.You ? MenuArt.Ink : MapMarkers.IconColor(ic), new Color(0f, 0f, 0f, 0f), alpha);
                    label = MapMarkers.IconName(ic);
                }
                MenuArt.Text(new Rect(lx + 30f * s, ly, colW - 34f * s, rowH), label, MenuArt.Tiny, MenuArt.A(UI.White, 0.88f * alpha));
            }
            float legendBottom = y + ((n + 1) / 2) * rowH;
            MenuArt.Text(new Rect(r.x, legendBottom + 2f * s, r.width, 22f * s), "medal ring on a stage start = your best medal", MenuArt.Tiny, MenuArt.A(UI.White, 0.5f * alpha));

            // ---- controls ----
            if (alpha < 0.5f) return;
            bool pad = InputReader.PadConnected;
            bool c1, c2, c3, c4;
            float fy = r.yMax - 34f * s, fx = r.x;
            fx += MenuArt.Chip(fx, fy, pad ? "Y" : "TAB", "find me", open, out c1);
            fx += MenuArt.Chip(fx, fy, pad ? "B" : "M / ESC", "close", open, out c2);
            MenuArt.Chip(fx, fy, pad ? "LT RT" : "WHEEL", "zoom");
            fy -= 36f * s; fx = r.x;
            fx += MenuArt.Chip(fx, fy, pad ? "A" : "CLICK", Gps.Active ? "move / clear waypoint" : "set waypoint", open, out c3);
            if (Gps.Active) fx += MenuArt.Chip(fx, fy, pad ? "LB" : "DEL", "clear");
            c4 = false;
            if (CanAct(m)) MenuArt.Chip(fx, fy, pad ? "X" : "F", m.travel ? "fast travel" : "race", open, out c4);
            if (c1) { worldC = PlayerPos(); padCursor = true; UI.Sound("Camera"); }
            if (c3) ToggleWaypoint(viewC, PoiAt(mapRect.center, 20f * s));
            if (c4) { DoAction(m); GUIUtility.ExitGUI(); }
            if (c2) { UI.Sound("MenuBack"); CloseWorld(null); GUIUtility.ExitGUI(); }
        }

        static string YouAre(RegionId id)
        {
            int i = (int)id;
            if (youAre[i] == null)
            {
                if (MapMarkers.RegionObj(id) == null) return "";
                string hz = MapMarkers.RegionHanzi(id);
                youAre[i] = "You are in " + MapMarkers.RegionName(id) + (hz.Length > 0 ? "  ·  " + hz : "");
            }
            return youAre[i];
        }
    }
}
