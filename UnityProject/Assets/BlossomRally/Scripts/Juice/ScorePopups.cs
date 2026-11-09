// ScorePopups.cs - animated score pop-ups ("PERFECT DRIFT!", "BIG AIR!  1.4 s", "NEAR MISS!", ...)
// in the UI.cs look: bold label with the soft shadow on a translucent plum pill, scale-punch in, float
// up, fade out. Stacked in up to three slots near the top-centre, below the free-roam drift score and the
// stage timer (so the drift meter at the bottom and the score at the top stay readable). Animation runs on
// real time (so it stays snappy during slow motion) but freezes while the game is paused.
using UnityEngine;

namespace BlossomRally
{
    public class ScorePopups : MonoBehaviour
    {
        public static ScorePopups Instance { get; private set; }

        const int Slots = 3;
        const float Life = 1.9f;

        class Pop
        {
            public string text, sub;
            public Color color;
            public float age = 99f, size = 1f;
            public bool live;
            public int slot;
        }

        readonly Pop[] pops = new Pop[5];
        GUIStyle big, small;
        readonly GUIContent gc = new GUIContent();

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < pops.Length; i++) pops[i] = new Pop();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Show a pop-up. sub is an optional smaller second line; size scales the punch (1 = normal).</summary>
        public static void Show(string text, string sub, Color color, float size = 1f)
        {
            var s = Instance;
            if (s == null || !JuiceSettings.ScorePopups) return;
            s.Add(text, sub, color, size);
        }

        void Add(string text, string sub, Color color, float size)
        {
            // same headline still on screen (e.g. chained near misses): refresh it instead of stacking
            for (int i = 0; i < pops.Length; i++)
                if (pops[i].live && pops[i].text == text && pops[i].age < Life - 0.3f)
                {
                    pops[i].sub = sub; pops[i].age = 0f; pops[i].size = size; pops[i].color = color;
                    return;
                }
            Pop p = null;
            for (int i = 0; i < pops.Length; i++) if (!pops[i].live) { p = pops[i]; break; }
            if (p == null)
            {
                p = pops[0];
                for (int i = 1; i < pops.Length; i++) if (pops[i].age > p.age) p = pops[i];
            }
            p.live = false;
            int slot = FreeSlot();
            if (slot < 0)
            {
                // all slots busy: retire the oldest and take its slot
                Pop oldest = null;
                for (int i = 0; i < pops.Length; i++)
                    if (pops[i].live && (oldest == null || pops[i].age > oldest.age)) oldest = pops[i];
                slot = oldest != null ? oldest.slot : 0;
                if (oldest != null) oldest.live = false;
            }
            p.text = text; p.sub = sub; p.color = color; p.size = size;
            p.age = 0f; p.slot = slot; p.live = true;
        }

        int FreeSlot()
        {
            for (int s = 0; s < Slots; s++)
            {
                bool used = false;
                for (int i = 0; i < pops.Length; i++) if (pops[i].live && pops[i].slot == s) { used = true; break; }
                if (!used) return s;
            }
            return -1;
        }

        /// <summary>Remove everything (event restart, leaving drive mode).</summary>
        public static void Clear()
        {
            var s = Instance;
            if (s == null) return;
            for (int i = 0; i < s.pops.Length; i++) s.pops[i].live = false;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;                   // paused: freeze
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < pops.Length; i++)
            {
                if (!pops[i].live) continue;
                pops[i].age += dt;
                if (pops[i].age >= Life) pops[i].live = false;
            }
        }

        static bool Visible()
        {
            var gm = GameManager.Instance;
            if (gm == null || GameManager.HideHud || !gm.IsDriving) return false;
            if (ReplaySystem.Instance != null && ReplaySystem.Instance.Active) return false;
            if (PhotoMode.Instance != null && PhotoMode.Instance.Active) return false;
            return true;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            bool any = false;
            for (int i = 0; i < pops.Length; i++) any |= pops[i].live;
            if (!any || !Visible()) return;
            GUI.depth = -12;                  // above the stage HUD (-10), below the medal stamp (-15)
            UI.Styles();
            if (big == null)
            {
                big = new GUIStyle(UI.Title) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(UI.Mid) { fontSize = 19, alignment = TextAnchor.MiddleCenter };
            }
            float W = Screen.width;
            float baseY = 160f;                // below the stage timer (10..76) and the drift score (70..154)
            Matrix4x4 m0 = GUI.matrix;
            for (int i = 0; i < pops.Length; i++)
            {
                Pop p = pops[i];
                if (!p.live) continue;
                float a = p.age;
                // scale punch: overshoot, then a damped wobble back to 1
                float punch = 1f + (0.75f * p.size) * Mathf.Exp(-a * 11f) * Mathf.Cos(a * 22f);
                if (a < 0.06f) punch = Mathf.Lerp(0.4f, 1f + 0.75f * p.size, a / 0.06f);
                float alpha = Mathf.Clamp01(a / 0.06f) * Mathf.Clamp01((Life - a) / 0.45f);
                float rise = 26f * (1f - Mathf.Exp(-a * 2.2f));
                float slotH = 64f;
                float cy = baseY + p.slot * slotH + 26f - rise;

                gc.text = p.text;
                Vector2 sz = big.CalcSize(gc);
                float subW = 0f;
                if (!string.IsNullOrEmpty(p.sub)) { gc.text = p.sub; subW = small.CalcSize(gc).x; }
                float pw = Mathf.Max(sz.x, subW) + 48f, ph = string.IsNullOrEmpty(p.sub) ? 50f : 70f;

                GUI.matrix = m0;
                GUIUtility.ScaleAroundPivot(new Vector2(punch, punch), new Vector2(W * 0.5f, cy));
                Rect pill = new Rect(W * 0.5f - pw * 0.5f, cy - 25f, pw, ph);
                UI.Panel(pill, 0.42f * alpha);
                // accent bar in the pop-up colour
                var oc = GUI.color;
                GUI.color = new Color(p.color.r, p.color.g, p.color.b, 0.85f * alpha);
                GUI.DrawTexture(new Rect(pill.x + 14f, pill.y + 46f, pill.width - 28f, 2f), Texture2D.whiteTexture);
                GUI.color = oc;
                UI.Label(new Rect(pill.x, pill.y + 2f, pw, 44f), p.text, big, new Color(p.color.r, p.color.g, p.color.b, alpha));
                if (!string.IsNullOrEmpty(p.sub))
                    UI.Label(new Rect(pill.x, pill.y + 46f, pw, 24f), p.sub, small, new Color(UI.White.r, UI.White.g, UI.White.b, alpha));
            }
            GUI.matrix = m0;
        }
    }
}
