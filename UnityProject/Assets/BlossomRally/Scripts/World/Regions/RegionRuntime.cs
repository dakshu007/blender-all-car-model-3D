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

        /// <summary>Distance from a region's tile at which its content streams in (per quality preset).</summary>
        public static float StreamDistance { get { return GameSettings.StreamDistance; } }
        /// <summary>Extra distance before streamed-in content is dropped again (no on/off flicker at the edge).</summary>
        public const float StreamHysteresis = 70f;
        /// <summary>Main-thread budget per frame for switching streamed content on (ms).</summary>
        const double StageBudgetMs = 1.2;

        /// <summary>How much of the global rain is shown at the camera (snow region snows instead, the desert stays dry).</summary>
        public static float RainVisual
        {
            get { return Mathf.Clamp01(1f - presence[(int)RegionId.Snow] - 0.85f * presence[(int)RegionId.Canyon]); }
        }

        /// <summary>How many cherry petals drift at the camera (the valley is full of blossom; other regions have little or none).</summary>
        public static float PetalAmount
        {
            get
            {
                if (Instance == null) return 1f;
                return Mathf.Clamp01(presence[(int)RegionId.Valley] + 0.45f * presence[(int)RegionId.Coast]
                                     + 0.35f * presence[(int)RegionId.Mountains] + 0.25f * presence[(int)RegionId.City]);
            }
        }

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
            rt.pending = new int[regs.Length];
            for (int i = 0; i < regs.Length; i++) rt.active[i] = true;
            Instance = rt;
            return rt;
        }

        /// <summary>Temporarily activate every region's content (map capture); false resumes streaming.</summary>
        public static void ShowAll(bool all)
        {
            var rt = Instance;
            if (rt == null || rt.regions == null) return;
            if (all) rt.FlushStaging();
            for (int i = 0; i < rt.regions.Length; i++)
            {
                var r = rt.regions[i];
                if (r == null || r.Context == null || r.Context.Root == null) continue;
                if (all) { r.Context.Root.gameObject.SetActive(true); rt.active[i] = true; }
            }
            if (!all) { rt.first = true; rt.streamT = 0f; }
        }

        // ---------------------------------------------------------------- staged activation
        // Switching a whole region on in one SetActive (thousands of colliders + renderers + scripts) is a
        // visible hitch. Instead the root comes on with its children parked inactive, and the children are
        // switched back on a few per frame within a small time budget (big groups are split further).
        // Ticks for that region wait until it is complete.
        readonly System.Collections.Generic.List<Transform> staged = new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<int> stagedRegion = new System.Collections.Generic.List<int>();
        int stagedHead;
        int[] pending = new int[6];          // parked children still waiting, per region
        readonly System.Diagnostics.Stopwatch stageWatch = new System.Diagnostics.Stopwatch();
        const int SplitAbove = 48;          // a child with more children than this is itself staged child by child

        void Park(Transform t, int region)
        {
            for (int c = 0; c < t.childCount; c++)
            {
                var ch = t.GetChild(c);
                if (!ch.gameObject.activeSelf) continue;   // stays off: it was off on purpose
                ch.gameObject.SetActive(false);            // cheap: the parent is not active in the hierarchy
                staged.Add(ch);
                stagedRegion.Add(region);
                pending[region]++;
            }
        }

        bool Staging(int region) { return region < pending.Length && pending[region] > 0; }

        void StageIn(int region, Transform root)
        {
            Park(root, region);
            root.gameObject.SetActive(true);
        }

        void StageOut(int region, Transform root)
        {
            root.gameObject.SetActive(false);
            // un-park anything still waiting (cheap now that the root is off) so the hierarchy is whole again
            for (int k = stagedHead; k < staged.Count; k++)
                if (stagedRegion[k] == region)
                {
                    if (staged[k] != null) staged[k].gameObject.SetActive(true);
                    stagedRegion[k] = -1;
                }
            if (region < pending.Length) pending[region] = 0;
        }

        void PumpStaging()
        {
            if (stagedHead >= staged.Count) { staged.Clear(); stagedRegion.Clear(); stagedHead = 0; return; }
            stageWatch.Reset();
            stageWatch.Start();
            int done = 0;
            while (stagedHead < staged.Count && (done == 0 || stageWatch.Elapsed.TotalMilliseconds < StageBudgetMs))
            {
                var t = staged[stagedHead];
                int reg = stagedRegion[stagedHead];
                stagedHead++;
                if (reg < 0) continue;
                pending[reg]--;
                if (t == null) continue;
                if (t.childCount > SplitAbove)
                {
                    // a big group (e.g. all the colliders): park its children and stage them one by one
                    Park(t, reg);
                    t.gameObject.SetActive(true);
                }
                else t.gameObject.SetActive(true);
                done++;
            }
            stageWatch.Stop();
        }

        void FlushStaging()
        {
            for (int k = stagedHead; k < staged.Count; k++)
                if (staged[k] != null && stagedRegion[k] >= 0) staged[k].gameObject.SetActive(true);
            staged.Clear();
            stagedRegion.Clear();
            stagedHead = 0;
            for (int i = 0; i < pending.Length; i++) pending[i] = 0;
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
                float sd = StreamDistance;
                bool switched = false;
                for (int i = 0; i < regions.Length; i++)
                {
                    var r = regions[i];
                    if (r == null || r.Context == null || r.Context.Root == null) continue;
                    float d = RectDistance(r.Rect, e2);
                    bool want = active[i] ? d < sd + StreamHysteresis : d < sd;
                    if (first)
                    {
                        // loading / teleport: switch immediately (the screen is covered or the view jumps anyway)
                        active[i] = want;
                        r.Context.Root.gameObject.SetActive(want);
                    }
                    else if (want != active[i] && !switched)
                    {
                        // at most one region per step: two regions never pay their switch in the same frame
                        active[i] = want;
                        switched = true;
                        if (want) StageIn(i, r.Context.Root);
                        else StageOut(i, r.Context.Root);
                    }
                }
                if (first) FlushStaging();
                first = false;
            }
            PumpStaging();

            float dt = Time.deltaTime;
            matT -= Time.unscaledDeltaTime;
            bool mats = matT <= 0f;
            if (mats) matT = 0.5f;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                if (r == null || r.Context == null || !active[i]) continue;
                if (Staging(i)) continue;     // still switching on
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
                    bannerHead = "ENTERING" + (reg.Hanzi.Length > 0 ? "  ·  " + reg.Hanzi : "");
                    bannerName = reg.Name.ToUpperInvariant();
                    try { reg.OnEnter(); } catch (System.Exception ex) { Debug.LogException(ex); }
                }
            }
            if (bannerT >= 0f)
            {
                bannerT += Time.unscaledDeltaTime;
                if (bannerT > 4.6f) bannerT = -1f;
            }
        }

        string bannerHead = "", bannerName = "";

        void Awake() { useGUILayout = false; }      // plain GUI calls only: skip the layout pass (and its garbage)

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
            UI.Label(new Rect(panel.x, panel.y + 8f * s, panel.width, 22f * s), bannerHead, UI.SmallCentre, new Color(1f, 1f, 1f, 0.85f * a));
            UI.Label(new Rect(panel.x, panel.y + 30f * s, panel.width, 50f * s), bannerName, UI.Title, new Color(UI.White.r, UI.White.g, UI.White.b, a));
            UI.Label(new Rect(panel.x, panel.y + 82f * s, panel.width, 24f * s), banner.Tagline, UI.SmallCentre, new Color(acc.r, acc.g, acc.b, a));
        }
    }
}
