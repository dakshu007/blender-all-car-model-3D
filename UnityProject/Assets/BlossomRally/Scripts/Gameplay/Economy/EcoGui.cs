// EcoGui.cs - small IMGUI kit for the economy screens (wallet HUD, job board, showroom, shops), in the
// MenuArt look: plum cards with a pink rule, gold prices, keycap chips. Text widths are cached so the
// screens don't allocate GUIContent every frame.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class EcoGui
    {
        public static readonly Color Jade = new Color(0.36f, 0.86f, 0.70f, 1f);
        public static readonly Color Coral = new Color(1f, 0.52f, 0.50f, 1f);
        public static readonly Color Muted = new Color(1f, 0.97f, 0.96f, 0.62f);

        static readonly Dictionary<GUIStyle, Dictionary<string, float>> widths = new Dictionary<GUIStyle, Dictionary<string, float>>();
        static GUIStyle widthFor;
        static readonly GUIContent tmp = new GUIContent();

        public static float S { get { return MenuArt.S; } }

        public static void Styles() { MenuArt.Styles(); }

        /// <summary>Cached text width for a style (per style and string; reset when the styles are rebuilt).</summary>
        public static float Width(GUIStyle st, string s)
        {
            if (string.IsNullOrEmpty(s) || st == null) return 0f;
            if (!ReferenceEquals(widthFor, MenuArt.Small)) { widths.Clear(); widthFor = MenuArt.Small; }
            Dictionary<string, float> d;
            if (!widths.TryGetValue(st, out d)) { d = new Dictionary<string, float>(); widths[st] = d; }
            float w;
            if (d.TryGetValue(s, out w)) return w;
            tmp.text = s;
            w = st.CalcSize(tmp).x;
            if (d.Count > 400) d.Clear();
            d[s] = w;
            return w;
        }

        public static bool Hover(Rect r) { return r.Contains(Event.current.mousePosition); }

        /// <summary>Keycap chip ("ENTER  buy"); returns its width (plus gap). Clickable.</summary>
        public static float Chip(float x, float y, string key, string label, out bool clicked)
        {
            float s = S, h = 28f * s;
            float kw = Mathf.Max(h, Width(MenuArt.TinyC, key) + 14f * s), lw = Width(MenuArt.Small, label) + 10f * s;
            Rect all = new Rect(x, y, kw + lw + 8f * s, h);
            MenuArt.Fill(new Rect(x, y, kw, h), MenuArt.A(UI.White, 0.92f));
            MenuArt.Text(new Rect(x, y, kw, h), key, MenuArt.TinyC, MenuArt.Plum, false);
            MenuArt.Text(new Rect(x + kw + 8f * s, y, lw, h), label, MenuArt.Small, UI.White);
            clicked = GUI.Button(all, GUIContent.none, GUIStyle.none);
            return all.width + 18f * s;
        }

        public static float ChipWidth(string key, string label)
        {
            float s = S, h = 28f * s;
            return Mathf.Max(h, Width(MenuArt.TinyC, key) + 14f * s) + Width(MenuArt.Small, label) + 10f * s + 8f * s + 18f * s;
        }

        /// <summary>A wide button. Returns true when clicked.</summary>
        public static bool Button(Rect r, string label, bool selected, bool enabled, Color accent, float a = 1f)
        {
            bool hover = enabled && Hover(r);
            Color fill = !enabled ? new Color(0.35f, 0.28f, 0.32f, 0.7f * a)
                       : selected ? MenuArt.A(accent, 0.95f * a)
                       : hover ? MenuArt.A(accent, 0.55f * a) : new Color(MenuArt.Plum.r, MenuArt.Plum.g, MenuArt.Plum.b, 0.9f * a);
            MenuArt.Tex(new Rect(r.x - 6f, r.y - 3f, r.width + 12f, r.height + 12f), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.3f * a));
            MenuArt.Fill(r, fill);
            if (selected && enabled) MenuArt.Outline(r, Mathf.Max(1f, 2f * S), MenuArt.A(UI.White, 0.9f * a));
            Color tc = !enabled ? MenuArt.A(UI.White, 0.45f * a) : selected ? MenuArt.A(MenuArt.PlumDeep, a) : MenuArt.A(UI.White, a);
            MenuArt.Text(r, label, MenuArt.Mid, tc, !selected);
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        public static void Bar(Rect r, float v, Color c, float a)
        {
            MenuArt.Fill(r, new Color(0f, 0f, 0f, 0.35f * a));
            MenuArt.Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), MenuArt.A(c, a));
        }

        /// <summary>Full-screen dim behind a menu.</summary>
        public static void Dim(float a)
        {
            MenuArt.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.08f, 0.03f, 0.06f, 0.55f * a));
            MenuArt.Tex(new Rect(0f, 0f, Screen.width, Screen.height), MenuArt.Vignette, new Color(0f, 0f, 0f, 0.6f * a));
        }

        /// <summary>Gold coin with a ¥.</summary>
        public static void Coin(Rect r, float a)
        {
            MenuArt.Tex(new Rect(r.x - r.width * 0.25f, r.y - r.height * 0.25f, r.width * 1.5f, r.height * 1.5f), MenuArt.Glow, new Color(1f, 0.8f, 0.3f, 0.35f * a));
            MenuArt.Tex(r, MenuArt.Dot, MenuArt.A(new Color(0.86f, 0.62f, 0.16f, 1f), a));
            float k = r.width * 0.12f;
            MenuArt.Tex(new Rect(r.x + k, r.y + k, r.width - 2f * k, r.height - 2f * k), MenuArt.Dot, MenuArt.A(UI.Gold, a));
            MenuArt.Text(r, "¥", MenuArt.Mid, MenuArt.A(new Color(0.55f, 0.32f, 0.05f, 1f), a), false);
        }

        /// <summary>Padlock icon drawn from the shared soft textures.</summary>
        public static void Lock(Rect r, Color c)
        {
            float w = r.width, h = r.height;
            Rect shackle = new Rect(r.x + w * 0.18f, r.y, w * 0.64f, w * 0.64f);
            MenuArt.Tex(shackle, MenuArt.Ring, c);
            Rect body = new Rect(r.x, r.y + h * 0.40f, w, h * 0.60f);
            MenuArt.Fill(body, c);
            MenuArt.Fill(new Rect(r.x + w * 0.44f, body.y + body.height * 0.3f, w * 0.12f, body.height * 0.4f), MenuArt.A(MenuArt.PlumDeep, c.a));
        }

        /// <summary>Kind / category colour tag.</summary>
        public static void Tag(Rect r, string text, Color c, float a)
        {
            MenuArt.Fill(r, MenuArt.A(c, 0.9f * a));
            MenuArt.Text(r, text, MenuArt.TinyC, MenuArt.A(MenuArt.PlumDeep, a), false);
        }

        /// <summary>m:ss text cache.</summary>
        public sealed class Clock
        {
            int key = int.MinValue;
            string text = "";
            public string Get(float seconds)
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(seconds));
                if (t == key) return text;
                key = t;
                text = (t / 60) + ":" + (t % 60).ToString("00");
                return text;
            }
        }

        /// <summary>Money text cache ("¥12,345").</summary>
        public sealed class MoneyText
        {
            int key = int.MinValue;
            string text = "";
            public string Get(int v)
            {
                if (v == key) return text;
                key = v;
                text = Wallet.Format(v);
                return text;
            }
        }
    }
}
