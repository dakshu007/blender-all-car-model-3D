// PhotoAlbum.cs - browse the pictures taken in photo mode (Application.persistentDataPath/Screenshots).
// Grid of thumbnails (cached as small PNGs in Screenshots/.thumbs so re-opening is instant), full
// view with next / previous, delete with a confirmation, and "open folder". Works while the game is
// paused (unscaled time only). Large screenshots are downscaled on the GPU by repeated halving blits.
//
// API: PhotoAlbum.Open(onClose)   PhotoAlbum.IsOpen   PhotoAlbum.Close()
// Keys: arrows / d-pad / stick move, ENTER / A view, ESC / B back, DEL / X delete, O / Y open folder,
//       in full view: left / right (LB / RB) previous / next. Mouse: click, wheel scroll, buttons.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(950)]
    public class PhotoAlbum : MonoBehaviour
    {
        public static PhotoAlbum Instance { get; private set; }
        public static bool IsOpen { get { return Instance != null && Instance.open; } }
        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "Screenshots"); } }

        const int ThumbW = 384, ThumbH = 216;
        const int Columns = 4;

        class Photo
        {
            public string path;
            public System.DateTime time;
            public Texture2D thumb;
            public bool failed;
        }

        bool open;
        int openFrame;
        float openTime, closeTime = -10f;
        System.Action onClose;
        readonly List<Photo> photos = new List<Photo>();
        readonly Dictionary<string, Texture2D> thumbCache = new Dictionary<string, Texture2D>();
        int sel;
        bool full;                 // full view
        float fullTime;
        bool confirmDelete;
        float scrollY, scrollTarget;
        Coroutine loader;
        RenderTexture fullTex;
        string fullPath;
        bool fullLoading;
        string message = "";
        float messageTime;

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            ReleaseFull();
            foreach (var kv in thumbCache) if (kv.Value != null) Destroy(kv.Value);
        }

        // ================================================================== API
        public static void Open(System.Action onClose)
        {
            if (Instance == null) return;
            Instance.DoOpen(onClose);
        }

        public static void Close()
        {
            if (Instance != null && Instance.open) Instance.DoClose();
        }

        void DoOpen(System.Action closed)
        {
            onClose = closed;
            open = true;
            openFrame = Time.frameCount;
            openTime = Time.unscaledTime;
            full = false;
            confirmDelete = false;
            Scan();
            sel = 0;
            scrollY = scrollTarget = 0f;
            if (loader != null) StopCoroutine(loader);
            loader = StartCoroutine(LoadThumbs());
            UI.Sound("Whoosh");
        }

        void DoClose()
        {
            open = false;
            closeTime = Time.unscaledTime;
            full = false;
            confirmDelete = false;
            if (loader != null) { StopCoroutine(loader); loader = null; }
            ReleaseFull();
            UI.Sound("MenuBack");
            var cb = onClose;
            onClose = null;
            if (cb != null) cb();
        }

        void Scan()
        {
            photos.Clear();
            try
            {
                string dir = Folder;
                if (!Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "*.png"))
                {
                    Texture2D t;
                    thumbCache.TryGetValue(f, out t);
                    photos.Add(new Photo { path = f, time = File.GetLastWriteTime(f), thumb = t });
                }
                photos.Sort((a, b) => b.time.CompareTo(a.time));
            }
            catch (System.Exception e) { Debug.LogWarning("Photo album: " + e.Message); }
        }

        // ================================================================== loading
        static string ThumbPath(string path)
        {
            string dir = Path.Combine(Path.GetDirectoryName(path), ".thumbs");
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "_t.png");
        }

        IEnumerator LoadThumbs()
        {
            yield return null;
            for (int i = 0; i < photos.Count; i++)
            {
                // load the ones near the selection first
                int idx = NextToLoad();
                if (idx < 0) break;
                var p = photos[idx];
                p.thumb = LoadThumb(p.path);
                if (p.thumb == null) p.failed = true;
                else thumbCache[p.path] = p.thumb;
                yield return null;
            }
            loader = null;
        }

        int NextToLoad()
        {
            int best = -1, bd = int.MaxValue;
            for (int i = 0; i < photos.Count; i++)
            {
                var p = photos[i];
                if (p.thumb != null || p.failed) continue;
                int d = Mathf.Abs(i - sel);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        static Texture2D LoadThumb(string path)
        {
            try
            {
                string tp = ThumbPath(path);
                if (File.Exists(tp) && File.GetLastWriteTime(tp) >= File.GetLastWriteTime(path))
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "BR_Thumb", wrapMode = TextureWrapMode.Clamp };
                    if (ImageConversion.LoadImage(t, File.ReadAllBytes(tp), true)) return t;
                    Destroy(t);
                }
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "BR_PhotoSrc" };
                if (!ImageConversion.LoadImage(src, File.ReadAllBytes(path), false)) { Destroy(src); return null; }
                var thumb = Downscale(src, ThumbW, ThumbH, true);
                Destroy(src);
                if (thumb == null) return null;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tp));
                    File.WriteAllBytes(tp, ImageConversion.EncodeToPNG(thumb));
                }
                catch (System.Exception) { }
                thumb.Apply(false, true);
                return thumb;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Photo album: could not load " + path + ": " + e.Message);
                return null;
            }
        }

        /// <summary>Halving blits down to (about) w x h, cropped to the target aspect. Returns a readable Texture2D or null.</summary>
        static Texture2D Downscale(Texture src, int w, int h, bool readable)
        {
            RenderTexture cur = null;
            Texture from = src;
            int sw = src.width, sh = src.height;
            while (sw / 2 >= w * 2 && sh / 2 >= h * 2)
            {
                sw /= 2; sh /= 2;
                var rt = RenderTexture.GetTemporary(sw, sh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.filterMode = FilterMode.Bilinear;
                Graphics.Blit(from, rt);
                if (cur != null) RenderTexture.ReleaseTemporary(cur);
                cur = rt;
                from = rt;
            }
            // final: crop to the thumbnail aspect (centre) and scale
            float srcAspect = sw / (float)sh, dstAspect = w / (float)h;
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            if (srcAspect > dstAspect) { scale.x = dstAspect / srcAspect; offset.x = (1f - scale.x) * 0.5f; }
            else { scale.y = srcAspect / dstAspect; offset.y = (1f - scale.y) * 0.5f; }
            var fin = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(from, fin, scale, offset);
            if (cur != null) RenderTexture.ReleaseTemporary(cur);
            var prev = RenderTexture.active;
            RenderTexture.active = fin;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "BR_Thumb", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            t.Apply(false, !readable);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(fin);
            return t;
        }

        IEnumerator LoadFull(string path)
        {
            fullLoading = true;
            yield return null;     // show the "developing" state for a frame
            if (!open || path != fullPath) { fullLoading = false; yield break; }
            ReleaseFullTexture();
            try
            {
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "BR_PhotoFull" };
                if (ImageConversion.LoadImage(src, File.ReadAllBytes(path), true))
                {
                    int maxW = Mathf.Max(1280, Screen.width);
                    int w = src.width, h = src.height;
                    RenderTexture cur = null;
                    Texture from = src;
                    while (w / 2 >= maxW) { w /= 2; h /= 2; var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Graphics.Blit(from, rt); if (cur != null) RenderTexture.ReleaseTemporary(cur); cur = rt; from = rt; }
                    fullTex = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "BR_PhotoView", filterMode = FilterMode.Bilinear };
                    fullTex.Create();
                    Graphics.Blit(from, fullTex);
                    if (cur != null) RenderTexture.ReleaseTemporary(cur);
                }
                Destroy(src);
            }
            catch (System.Exception e) { Toast("Could not open the photo: " + e.Message); }
            fullLoading = false;
        }

        void ReleaseFullTexture()
        {
            if (fullTex != null) { fullTex.Release(); Destroy(fullTex); fullTex = null; }
        }

        void ReleaseFull()
        {
            ReleaseFullTexture();
            fullPath = null;
        }

        void ShowFull(int i)
        {
            if (photos.Count == 0) return;
            sel = (i % photos.Count + photos.Count) % photos.Count;
            full = true;
            fullTime = Time.unscaledTime;
            confirmDelete = false;
            fullPath = photos[sel].path;
            StartCoroutine(LoadFull(fullPath));
        }

        void Toast(string s) { message = s; messageTime = Time.unscaledTime; }

        void Delete()
        {
            if (photos.Count == 0) return;
            var p = photos[sel];
            try
            {
                File.Delete(p.path);
                string tp = ThumbPath(p.path);
                if (File.Exists(tp)) File.Delete(tp);
                if (p.thumb != null) { thumbCache.Remove(p.path); Destroy(p.thumb); }
                photos.RemoveAt(sel);
                Toast("Photo deleted");
                UI.Sound("MenuBack");
            }
            catch (System.Exception e) { Toast("Could not delete: " + e.Message); }
            confirmDelete = false;
            if (photos.Count == 0) { full = false; ReleaseFull(); sel = 0; return; }
            sel = Mathf.Clamp(sel, 0, photos.Count - 1);
            if (full) ShowFull(sel);
        }

        void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                Application.OpenURL("file://" + Folder.Replace('\\', '/'));
                Toast("Opened " + Folder);
            }
            catch (System.Exception e) { Toast("Folder: " + Folder + " (" + e.Message + ")"); }
        }

        // ================================================================== input
        void Update()
        {
            if (!open || Time.frameCount == openFrame) return;
            if (confirmDelete)
            {
                if (InputReader.MenuConfirm || InputReader.Down(KeyCode.Y) || GamepadBridge.ButtonDown(PadButton.West)) Delete();
                else if (InputReader.MenuBack || InputReader.Down(KeyCode.N)) { confirmDelete = false; UI.Sound("MenuBack"); }
                return;
            }
            bool del = InputReader.Down(KeyCode.Delete) || InputReader.Down(KeyCode.X) || GamepadBridge.ButtonDown(PadButton.West);
            if (InputReader.Down(KeyCode.O) || GamepadBridge.ButtonDown(PadButton.North)) OpenFolder();
            if (full)
            {
                if (InputReader.MenuLeft || GamepadBridge.ButtonDown(PadButton.LeftShoulder)) { ShowFull(sel - 1); UI.Sound("MenuMove"); }
                else if (InputReader.MenuRight || GamepadBridge.ButtonDown(PadButton.RightShoulder)) { ShowFull(sel + 1); UI.Sound("MenuMove"); }
                if (del && photos.Count > 0) { confirmDelete = true; UI.Sound("MenuMove"); }
                if (InputReader.MenuBack || InputReader.MenuConfirm) { full = false; ReleaseFull(); UI.Sound("MenuBack"); }
                return;
            }
            if (photos.Count > 0)
            {
                int before = sel;
                if (InputReader.MenuLeft) sel = Mathf.Max(0, sel - 1);
                if (InputReader.MenuRight) sel = Mathf.Min(photos.Count - 1, sel + 1);
                if (InputReader.MenuUp) sel = Mathf.Max(0, sel - Columns);
                if (InputReader.MenuDown) sel = Mathf.Min(photos.Count - 1, sel + Columns);
                if (sel != before) UI.Sound("MenuMove");
                if (InputReader.MenuConfirm) { ShowFull(sel); UI.Sound("MenuSelect"); return; }
                if (del) { confirmDelete = true; UI.Sound("MenuMove"); }
            }
            if (InputReader.MenuBack) DoClose();
        }

        // ================================================================== drawing
        void OnGUI()
        {
            float now = Time.unscaledTime;
            bool closing = !open && now - closeTime < 0.3f;
            if (!open && !closing) return;
            GUI.depth = -12;
            MenuArt.Styles();
            float W = Screen.width, H = Screen.height, s = MenuArt.S;
            float a = open ? MenuArt.EaseOut((now - openTime) / 0.4f) : 1f - MenuArt.Ease((now - closeTime) / 0.3f);

            MenuArt.Fill(new Rect(0, 0, W, H), new Color(MenuArt.PlumDeep.r, MenuArt.PlumDeep.g, MenuArt.PlumDeep.b, 0.96f * a));
            var bg = LoadingScreen.AnyPainting(2);
            if (bg != null) MenuArt.Tex(new Rect(0, 0, W, H), bg, new Color(1f, 1f, 1f, 0.12f * a), ScaleMode.ScaleAndCrop);
            MenuArt.Tex(new Rect(0, 0, W, H), MenuArt.Vignette, new Color(0f, 0f, 0f, 0.7f * a));

            float slide = (1f - a) * 40f * s;
            MenuArt.Text(new Rect(0, 26f * s - slide, W, 56f * s), "PHOTO ALBUM", MenuArt.Title, MenuArt.A(UI.Pink, a));
            string sub = photos.Count == 0 ? "no photos yet" : photos.Count + (photos.Count == 1 ? " photo" : " photos");
            MenuArt.Text(new Rect(0, 74f * s - slide, W, 26f * s), sub, MenuArt.SmallC, MenuArt.A(UI.White, 0.75f * a));

            if (full && photos.Count > 0) DrawFull(W, H, s, a, now);
            else DrawGrid(W, H, s, a, now);

            if (open && confirmDelete) DrawConfirm(W, H, s);
            if (now - messageTime < 3f && message.Length > 0)
                MenuArt.Text(new Rect(0, H - 104f * s, W, 28f * s), message, MenuArt.SmallC, MenuArt.A(UI.Gold, Mathf.Clamp01(3f - (now - messageTime))));
            if (a > 0.5f) DrawFooter(W, H, s);
            var ev = Event.current;
            if (open && (ev.type == EventType.MouseDown || ev.type == EventType.MouseUp || ev.type == EventType.ScrollWheel)) ev.Use();
        }

        void DrawGrid(float W, float H, float s, float a, float now)
        {
            Rect area = new Rect(Mathf.Max(30f * s, (W - 1500f * s) * 0.5f), 116f * s, Mathf.Min(1500f * s, W - 60f * s), H - 116f * s - 120f * s);
            if (photos.Count == 0)
            {
                Rect r = new Rect(W * 0.5f - 300f * s, H * 0.4f, 600f * s, 140f * s);
                MenuArt.Card(r, a);
                MenuArt.Text(new Rect(r.x, r.y + 20f * s, r.width, 36f * s), "The album is empty", MenuArt.Mid, MenuArt.A(UI.White, a));
                string key = GameSettings.Binding(GameAction.Photo).ToString();
                MenuArt.Text(new Rect(r.x + 20f * s, r.y + 60f * s, r.width - 40f * s, 60f * s),
                             "Press " + key + " (gamepad X) while driving to open photo mode, then ENTER to take a picture.", MenuArt.Small, MenuArt.A(UI.White, 0.8f * a));
                return;
            }
            float gap = 22f * s;
            float cw = (area.width - gap * (Columns - 1)) / Columns, chh = cw * 9f / 16f, ch = chh + 40f * s;
            int rows = (photos.Count + Columns - 1) / Columns;
            float total = rows * (ch + gap);
            int selRow = sel / Columns;
            float selTop = selRow * (ch + gap);
            if (selTop - scrollTarget < 0f) scrollTarget = selTop;
            if (selTop + ch - scrollTarget > area.height) scrollTarget = selTop + ch - area.height;
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && area.Contains(e.mousePosition)) { scrollTarget += e.delta.y * 30f * s; e.Use(); }
            scrollTarget = Mathf.Clamp(scrollTarget, 0f, Mathf.Max(0f, total - area.height));
            scrollY = Mathf.Lerp(scrollY, scrollTarget, MenuArt.Damp(Time.unscaledDeltaTime, 12f));

            GUI.BeginGroup(area);
            for (int i = 0; i < photos.Count; i++)
            {
                int col = i % Columns, row = i / Columns;
                float appear = MenuArt.EaseOut((now - openTime - i * 0.03f) / 0.4f);
                Rect c = new Rect(col * (cw + gap), row * (ch + gap) - scrollY + (1f - appear) * 30f * s, cw, ch);
                if (c.yMax < 0f || c.y > area.height) continue;
                bool isSel = i == sel;
                float lift = isSel ? 1f : 0f;
                Rect frame = new Rect(c.x - lift * 6f * s, c.y - lift * 6f * s, c.width + lift * 12f * s, c.height + lift * 12f * s);
                MenuArt.Tex(new Rect(frame.x - 10f * s, frame.y - 4f * s, frame.width + 20f * s, frame.height + 22f * s), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.5f * appear * a));
                MenuArt.Fill(frame, new Color(0.97f, 0.95f, 0.91f, appear * a));      // white photo border
                Rect img = new Rect(frame.x + 8f * s, frame.y + 8f * s, frame.width - 16f * s, (frame.width - 16f * s) * 9f / 16f);
                var p = photos[i];
                if (p.thumb != null) MenuArt.Tex(img, p.thumb, new Color(1f, 1f, 1f, appear * a), ScaleMode.ScaleAndCrop);
                else
                {
                    MenuArt.Fill(img, new Color(0.25f, 0.18f, 0.22f, appear * a));
                    MenuArt.Text(img, p.failed ? "unreadable" : "developing...", MenuArt.SmallC, MenuArt.A(UI.White, (p.failed ? 0.6f : 0.4f + 0.3f * Mathf.Sin(now * 4f + i)) * a));
                }
                MenuArt.Text(new Rect(img.x, img.yMax + 2f * s, img.width, frame.yMax - img.yMax - 4f * s), p.time.ToString("d MMM yyyy  ·  HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                             MenuArt.TinyC, MenuArt.A(MenuArt.InkSoft, appear * a), false);
                if (isSel) MenuArt.Outline(new Rect(frame.x - 3f * s, frame.y - 3f * s, frame.width + 6f * s, frame.height + 6f * s), Mathf.Max(2f, 3f * s),
                                           MenuArt.A(UI.Pink, (0.75f + 0.25f * Mathf.Sin(now * 4f)) * a));
                if (open && !confirmDelete && GUI.Button(c, GUIContent.none, GUIStyle.none))
                {
                    if (isSel) { ShowFull(i); UI.Sound("MenuSelect"); }
                    else { sel = i; UI.Sound("MenuMove"); }
                }
            }
            GUI.EndGroup();
        }

        void DrawFull(float W, float H, float s, float a, float now)
        {
            var p = photos[sel];
            Rect area = new Rect(60f * s, 110f * s, W - 120f * s, H - 110f * s - 130f * s);
            float k = MenuArt.EaseOut((now - fullTime) / 0.35f);
            Texture t = fullTex != null ? (Texture)fullTex : p.thumb;
            float aspect = t != null ? t.width / (float)t.height : 16f / 9f;
            float w = area.width, h = w / aspect;
            if (h > area.height) { h = area.height; w = h * aspect; }
            w *= 0.94f + 0.06f * k; h *= 0.94f + 0.06f * k;
            Rect img = new Rect(area.center.x - w * 0.5f, area.center.y - h * 0.5f, w, h);
            Rect frame = new Rect(img.x - 12f * s, img.y - 12f * s, img.width + 24f * s, img.height + 24f * s);
            MenuArt.Tex(new Rect(frame.x - 20f * s, frame.y - 10f * s, frame.width + 40f * s, frame.height + 40f * s), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.6f * a));
            MenuArt.Fill(frame, new Color(0.97f, 0.95f, 0.91f, a));
            if (t != null) MenuArt.Tex(img, t, new Color(1f, 1f, 1f, a * k));
            if (fullLoading || fullTex == null)
                MenuArt.Text(new Rect(img.x, img.yMax - 40f * s, img.width, 30f * s), "developing...", MenuArt.SmallC, MenuArt.A(UI.White, 0.6f + 0.3f * Mathf.Sin(now * 5f)));
            MenuArt.Tex(new Rect(frame.xMax - 52f * s, frame.yMax - 52f * s, 40f * s, 40f * s), MenuArt.Seal, new Color(1f, 1f, 1f, 0.85f * a));
            string info = (sel + 1) + " / " + photos.Count + "    " + p.time.ToString("d MMMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "    " + Path.GetFileName(p.path);
            MenuArt.Text(new Rect(0, frame.yMax + 8f * s, W, 26f * s), info, MenuArt.SmallC, MenuArt.A(UI.White, 0.85f * a));
            // prev / next arrows (clickable)
            if (photos.Count > 1)
            {
                Rect l = new Rect(14f * s, area.center.y - 40f * s, 44f * s, 80f * s), r = new Rect(W - 58f * s, area.center.y - 40f * s, 44f * s, 80f * s);
                MenuArt.Fill(l, new Color(0f, 0f, 0f, 0.35f * a));
                MenuArt.Fill(r, new Color(0f, 0f, 0f, 0.35f * a));
                MenuArt.Text(l, "‹", MenuArt.Title, MenuArt.A(UI.White, a));
                MenuArt.Text(r, "›", MenuArt.Title, MenuArt.A(UI.White, a));
                if (open && !confirmDelete && GUI.Button(l, GUIContent.none, GUIStyle.none)) { ShowFull(sel - 1); UI.Sound("MenuMove"); }
                if (open && !confirmDelete && GUI.Button(r, GUIContent.none, GUIStyle.none)) { ShowFull(sel + 1); UI.Sound("MenuMove"); }
            }
            if (open && !confirmDelete && Event.current.type == EventType.ScrollWheel)
            {
                ShowFull(sel + (Event.current.delta.y > 0f ? 1 : -1));
                Event.current.Use();
            }
        }

        void DrawConfirm(float W, float H, float s)
        {
            MenuArt.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.45f));
            Rect r = new Rect(W * 0.5f - 280f * s, H * 0.5f - 100f * s, 560f * s, 200f * s);
            MenuArt.Card(r, 1f);
            MenuArt.Text(new Rect(r.x, r.y + 20f * s, r.width, 40f * s), "Delete this photo?", MenuArt.Mid, UI.White);
            MenuArt.Text(new Rect(r.x, r.y + 60f * s, r.width, 26f * s), "It will be removed from the Screenshots folder.", MenuArt.SmallC, MenuArt.A(UI.White, 0.75f));
            bool pad = InputReader.PadConnected;
            Rect yes = new Rect(r.x + 40f * s, r.yMax - 66f * s, 220f * s, 44f * s), no = new Rect(r.xMax - 260f * s, r.yMax - 66f * s, 220f * s, 44f * s);
            MenuArt.Fill(yes, new Color(0.75f, 0.18f, 0.22f, 0.95f));
            MenuArt.Fill(no, new Color(1f, 1f, 1f, 0.15f));
            MenuArt.Text(yes, (pad ? "X / A" : "ENTER / Y") + "   delete", MenuArt.BodyC, UI.White);
            MenuArt.Text(no, (pad ? "B" : "ESC / N") + "   keep", MenuArt.BodyC, UI.White);
            if (GUI.Button(yes, GUIContent.none, GUIStyle.none)) Delete();
            if (GUI.Button(no, GUIContent.none, GUIStyle.none)) { confirmDelete = false; UI.Sound("MenuBack"); }
        }

        void DrawFooter(float W, float H, float s)
        {
            bool pad = InputReader.PadConnected;
            float y = H - 60f * s, x = Mathf.Max(30f * s, W * 0.5f - 520f * s);
            bool cView, cBack, cDel, cFolder;
            if (full)
            {
                x += MenuArt.Chip(x, y, pad ? "LB RB" : "← →", "previous / next", false, out cView);
                x += MenuArt.Chip(x, y, pad ? "B" : "ESC", "back to album", open && !confirmDelete, out cBack);
                if (cBack) { full = false; ReleaseFull(); UI.Sound("MenuBack"); }
            }
            else
            {
                x += MenuArt.Chip(x, y, pad ? "A" : "ENTER", "view", false, out cView);
                x += MenuArt.Chip(x, y, pad ? "B" : "ESC", "close", open && !confirmDelete, out cBack);
                if (cBack) { DoClose(); GUIUtility.ExitGUI(); }
            }
            if (photos.Count > 0)
            {
                x += MenuArt.Chip(x, y, pad ? "X" : "DEL", "delete", open && !confirmDelete, out cDel);
                if (cDel) { confirmDelete = true; UI.Sound("MenuMove"); }
            }
            MenuArt.Chip(x, y, pad ? "Y" : "O", "open folder", open && !confirmDelete, out cFolder);
            if (cFolder) OpenFolder();
            MenuArt.Text(new Rect(0, H - 26f * s, W, 20f * s), Folder, MenuArt.TinyC, MenuArt.A(UI.White, 0.45f));
        }
    }
}
