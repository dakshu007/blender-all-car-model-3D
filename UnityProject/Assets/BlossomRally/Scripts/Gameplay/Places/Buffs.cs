// Buffs.cs - small, cosy, timed boosts bought in tea houses / restaurants or received in temples
// ("Jasmine tea: +10% job pay for 5 min", "Incense blessing", "A wish: luck"). Owned by the Places module.
// Other modules READ the multipliers (economy: Buffs.JobPayMultiplier when paying a job, Buffs.ShopDiscount in
// shops, Buffs.Luck for tips / random rewards). Timers run on unscaled time minus pauses (Time.time based, so
// they freeze while the game is paused). Not saved: they are short.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public enum BuffKind { JobPay = 0, Discount = 1, Luck = 2 }

    public sealed class Buff
    {
        public string id, title, hanzi;
        public BuffKind kind;
        /// <summary>JobPay: +fraction of pay (0.1 = +10%). Discount: fraction off. Luck: 0..1.</summary>
        public float amount;
        public float endsAt;          // Time.time
        public float duration;
        public Color colour;
        public float Remaining { get { return Mathf.Max(0f, endsAt - Time.time); } }
    }

    public static class Buffs
    {
        static readonly List<Buff> active = new List<Buff>();
        /// <summary>Active buffs (expired ones are removed lazily by Tick / the getters).</summary>
        public static List<Buff> Active { get { Prune(); return active; } }
        /// <summary>Raised when a buff is added, refreshed or expires.</summary>
        public static event System.Action Changed;

        /// <summary>Multiplier for job pay: 1 + sum of active JobPay buffs (capped at +40%).</summary>
        public static float JobPayMultiplier { get { return 1f + Mathf.Min(0.4f, Sum(BuffKind.JobPay)); } }
        /// <summary>Fraction off shop prices (0..0.25).</summary>
        public static float ShopDiscount { get { return Mathf.Min(0.25f, Sum(BuffKind.Discount)); } }
        /// <summary>Luck 0..1 (tips, random rewards).</summary>
        public static float Luck { get { return Mathf.Clamp01(Sum(BuffKind.Luck)); } }

        public static bool Has(string id)
        {
            Prune();
            for (int i = 0; i < active.Count; i++) if (active[i].id == id) return true;
            return false;
        }

        static float Sum(BuffKind k)
        {
            Prune();
            float s = 0f;
            for (int i = 0; i < active.Count; i++) if (active[i].kind == k) s += active[i].amount;
            return s;
        }

        /// <summary>Add (or refresh, same id) a buff for 'minutes'.</summary>
        public static Buff Add(string id, string title, string hanzi, BuffKind kind, float amount, float minutes, Color colour)
        {
            Prune();
            Buff b = null;
            for (int i = 0; i < active.Count; i++) if (active[i].id == id) { b = active[i]; break; }
            if (b == null) { b = new Buff(); active.Add(b); }
            b.id = id; b.title = title; b.hanzi = hanzi; b.kind = kind; b.amount = amount; b.colour = colour;
            b.duration = Mathf.Max(1f, minutes * 60f);
            b.endsAt = Time.time + b.duration;
            if (Changed != null) Changed();
            return b;
        }

        public static void Clear()
        {
            if (active.Count == 0) return;
            active.Clear();
            if (Changed != null) Changed();
        }

        static float pruneT = -1f;
        static void Prune()
        {
            float t = Time.time;
            if (t == pruneT) return;
            pruneT = t;
            bool removed = false;
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].endsAt <= t) { active.RemoveAt(i); removed = true; }
            if (removed && Changed != null) Changed();
        }

        // ------------------------------------------------------------------ presets used by the places
        public static readonly Color TeaGreen = new Color(0.55f, 0.85f, 0.55f), Blessing = new Color(1f, 0.78f, 0.35f),
                                     LuckRed = new Color(1f, 0.45f, 0.45f), Warm = new Color(1f, 0.6f, 0.35f), Sea = new Color(0.45f, 0.8f, 0.95f);

        public static void Tea(string name, float pay, float minutes)
        {
            Add("tea", name, "茶", BuffKind.JobPay, pay, minutes, TeaGreen);
        }

        public static void Food(string name, float pay, float minutes)
        {
            Add("food", name, "饭", BuffKind.JobPay, pay, minutes, Sea);
        }

        public static void WarmDrink(string name, float pay, float minutes)
        {
            Add("warm", name, "暖", BuffKind.JobPay, pay, minutes, Warm);
        }
    }

    /// <summary>Tiny HUD row of the active buffs (top centre, only while any is active and the HUD is shown).</summary>
    public class BuffsHud : MonoBehaviour
    {
        static bool registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (registered) return;
            registered = true;
            WorldHooks.WorldBuilt += w =>
            {
                if (FindAnyObjectByType<BuffsHud>() != null) return;
                var go = new GameObject("BuffsHud");
                go.AddComponent<BuffsHud>().useGUILayout = false;
            };
        }

        // cached "4:59" strings (no per-frame garbage): index = seconds left
        static readonly string[] timeStr = new string[1801];
        static string TimeStr(float s)
        {
            int i = Mathf.Clamp(Mathf.CeilToInt(s), 0, 1800);
            return timeStr[i] ?? (timeStr[i] = (i / 60) + ":" + (i % 60).ToString("00"));
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (GameManager.HideHud) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving) return;
            var list = Buffs.Active;
            if (list.Count == 0) return;
            UI.Styles();
            float s = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            float w = 150f * s, h = 34f * s, gap = 8f * s;
            float total = list.Count * w + (list.Count - 1) * gap;
            float x = Screen.width * 0.5f - total * 0.5f, y = 10f * s;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                var r = new Rect(x + i * (w + gap), y, w, h);
                UI.Panel(r, 0.55f);
                var old = GUI.color;
                GUI.color = b.colour;
                float k = b.duration > 0f ? Mathf.Clamp01(b.Remaining / b.duration) : 0f;
                GUI.DrawTexture(new Rect(r.x, r.yMax - 3f * s, r.width * k, 3f * s), Texture2D.whiteTexture);
                GUI.color = old;
                UI.Label(new Rect(r.x + 6f * s, r.y, 30f * s, h), b.hanzi, UI.MidLeft, b.colour);
                UI.Label(new Rect(r.x + 34f * s, r.y + 1f * s, w - 40f * s, h * 0.55f), b.title, UI.Small, UI.White);
                UI.Label(new Rect(r.x + 34f * s, r.y + h * 0.48f, w - 40f * s, h * 0.5f), TimeStr(b.Remaining), UI.Small, new Color(1f, 1f, 1f, 0.7f));
            }
        }
    }
}
