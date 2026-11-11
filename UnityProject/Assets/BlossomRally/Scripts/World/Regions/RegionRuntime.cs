// RegionRuntime.cs - keeps the six-region world cheap and alive while playing:
//  * streams each region's content in / out with camera distance (the terrain always stays),
//  * ticks the regions near the camera (effects, animation) and refreshes their night-glow materials,
//  * tracks which region the player's car is in and shows a short "entering ..." banner,
//  * exposes the blend presence of each region at the camera for grading / weather / audio.
using UnityEngine;

namespace BlossomRally
{
    public class RegionRuntime : MonoBehaviour
    {
        public static RegionRuntime Instance { get; private set; }
        /// <summary>Region the player's car (or the camera, before a car exists) is in.</summary>
        public static RegionId Current { get; private set; }
        static readonly float[] presence = new float[6];

        /// <summary>0..1 how much the camera is inside a region (soft at the borders).</summary>
        public static float Presence(RegionId id) { return presence[(int)id]; }

        public const float StreamDistance = 720f;

        WorldBuilder world;
        Region[] regions;
        bool[] active;
        float streamT, matT, bannerT = -1f;
        Region banner;
        Camera cam;
        float camT;
        bool first = true;

        public static RegionRuntime Create(WorldBuilder w, Region[] regs)
        {
            var go = new GameObject("RegionRuntime");
            var rt = go.AddComponent<RegionRuntime>();
            rt.world = w;
            rt.regions = regs;
            rt.active = new bool[regs.Length];
            for (int i = 0; i < regs.Length; i++) rt.active[i] = true;
            Instance = rt;
            return rt;
        }

        static float RectDistance(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.xMin - p.x, p.x - r.xMax));
            float dz = Mathf.Max(0f, Mathf.Max(r.yMin - p.y, p.y - r.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        Vector3 Eye()
        {
            camT -= Time.unscaledDeltaTime;
            if (cam == null || camT <= 0f) { cam = Camera.main; camT = 1f; }
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        void Update()
        {
            if (regions == null) return;
            Vector3 eye = Eye();
            Vector2 e2 = new Vector2(eye.x, eye.z);
            WorldLayout.Weights(e2, presence);

            streamT -= Time.unscaledDeltaTime;
            if (streamT <= 0f)
            {
                streamT = 0.3f;
                for (int i = 0; i < regions.Length; i++)
                {
                    var r = regions[i];
                    if (r == null || r.Context == null || r.Context.Root == null) continue;
                    bool want = RectDistance(r.Rect, e2) < StreamDistance;
                    if (want != active[i] || first)
                    {
                        active[i] = want;
                        r.Context.Root.gameObject.SetActive(want);
                    }
                }
                first = false;
            }

            float dt = Time.deltaTime;
            matT -= Time.unscaledDeltaTime;
            bool mats = matT <= 0f;
            if (mats) matT = 0.5f;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                if (r == null || r.Context == null || !active[i]) continue;
                if (mats) r.Context.UpdateMaterials();
                if (dt > 0f)
                {
                    try { r.Tick(dt, eye, presence[(int)r.Id]); }
                    catch (System.Exception ex) { Debug.LogException(ex); }
                }
            }

            // player region + banner
            var gm = GameManager.Instance;
            Vector3 car = gm != null && gm.Car != null ? gm.Car.transform.position : eye;
            RegionId now = WorldLayout.At(new Vector2(car.x, car.z));
            if (now != Current)
            {
                Current = now;
                var reg = world != null && world.Field != null ? world.Field.RegionOf(now) : null;
                if (reg != null && gm != null && gm.Car != null)
                {
                    banner = reg;
                    bannerT = 0f;
                    try { reg.OnEnter(); } catch (System.Exception ex) { Debug.LogException(ex); }
                }
            }
            if (bannerT >= 0f)
            {
                bannerT += Time.unscaledDeltaTime;
                if (bannerT > 4.6f) bannerT = -1f;
            }
        }

        void OnGUI()
        {
            if (bannerT < 0f || banner == null) return;
            UI.Styles();
            float a = Mathf.Clamp01(bannerT / 0.5f) * Mathf.Clamp01((4.6f - bannerT) / 0.8f);
            float W = Screen.width, H = Screen.height;
            float s = Mathf.Clamp(H / 1080f, 0.6f, 2f);
            float slide = (1f - Mathf.Clamp01(bannerT / 0.6f));
            slide = slide * slide * 60f * s;
            Rect panel = new Rect(W * 0.5f - 330f * s, H * 0.13f - slide, 660f * s, 118f * s);
            UI.Panel(panel, 0.5f * a);
            Color acc = banner.Accent;
            var bar = new Rect(panel.x, panel.yMax - 5f * s, panel.width, 5f * s);
            var old = GUI.color;
            GUI.color = new Color(acc.r, acc.g, acc.b, a);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = old;
            UI.Label(new Rect(panel.x, panel.y + 8f * s, panel.width, 22f * s), "ENTERING" + (banner.Hanzi.Length > 0 ? "  ·  " + banner.Hanzi : ""), UI.SmallCentre, new Color(1f, 1f, 1f, 0.85f * a));
            UI.Label(new Rect(panel.x, panel.y + 30f * s, panel.width, 50f * s), banner.Name.ToUpperInvariant(), UI.Title, new Color(UI.White.r, UI.White.g, UI.White.b, a));
            UI.Label(new Rect(panel.x, panel.y + 82f * s, panel.width, 24f * s), banner.Tagline, UI.SmallCentre, new Color(acc.r, acc.g, acc.b, a));
        }
    }
}
