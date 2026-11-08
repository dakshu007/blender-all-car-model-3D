// UI.cs - shared IMGUI look (plum panels, pink / white labels with a soft shadow) and a small
// keyboard + gamepad + mouse menu list used by the title, pause, events and settings screens.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class UI
    {
        public static readonly Color White = new Color(1f, 0.97f, 0.96f, 1f);
        public static readonly Color Pink = new Color(1f, 0.72f, 0.82f, 1f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.42f, 1f);
        public static GUIStyle Big, Mid, Small, Title, Huge, Row, MidLeft, MidRight, SmallCentre;

        public static void Styles()
        {
            if (Big != null) return;
            Big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            Mid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            MidLeft = new GUIStyle(Mid) { alignment = TextAnchor.MiddleLeft };
            MidRight = new GUIStyle(Mid) { alignment = TextAnchor.MiddleRight };
            Small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true };
            SmallCentre = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter };
            Title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Huge = new GUIStyle(GUI.skin.label) { fontSize = 76, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Row = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleLeft };
        }

        public static void Label(Rect r, string text, GUIStyle st, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0.15f, 0.08f, 0.12f, 0.55f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        public static void Panel(Rect r, float alpha)
        {
            var old = GUI.color;
            GUI.color = new Color(0.20f, 0.10f, 0.16f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Bar(Rect r, float fill, Color c)
        {
            Panel(r, 0.5f);
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static System.Action<string> OnSound;
        public static void Sound(string id) { if (OnSound != null) OnSound(id); }
    }

    public class MenuItem
    {
        public string label;
        public System.Func<string> value;          // shown after the label (optional)
        public System.Action<int> change;          // left / right (-1 / +1)
        public System.Action select;               // enter / click
        public System.Func<bool> visible;
        public bool Visible { get { return visible == null || visible(); } }
    }

    public class MenuList
    {
        public string title;
        public string footer = "↑ ↓ choose   ← → change   ENTER select   ESC back";
        public readonly List<MenuItem> items = new List<MenuItem>();
        public int index;
        public float width = 520f;

        public MenuItem Add(string label, System.Action select = null, System.Func<string> value = null, System.Action<int> change = null, System.Func<bool> visible = null)
        {
            var m = new MenuItem { label = label, select = select, value = value, change = change, visible = visible };
            items.Add(m);
            return m;
        }

        void Step(int d)
        {
            if (items.Count == 0) return;
            for (int k = 0; k < items.Count; k++)
            {
                index = (index + d + items.Count) % items.Count;
                if (items[index].Visible) return;
            }
        }

        /// <summary>Handle keyboard / gamepad. Returns true when the player backs out.</summary>
        public bool HandleInput()
        {
            if (items.Count == 0) return InputReader.MenuBack;
            if (!items[index].Visible) Step(1);
            if (InputReader.MenuUp) { Step(-1); UI.Sound("MenuMove"); }
            if (InputReader.MenuDown) { Step(1); UI.Sound("MenuMove"); }
            var it = items[index];
            if (it.change != null)
            {
                if (InputReader.MenuLeft) { it.change(-1); UI.Sound("MenuMove"); }
                if (InputReader.MenuRight) { it.change(1); UI.Sound("MenuMove"); }
            }
            if (InputReader.MenuConfirm)
            {
                if (it.select != null) { UI.Sound("MenuSelect"); it.select(); }
                else if (it.change != null) { it.change(1); UI.Sound("MenuMove"); }
            }
            if (InputReader.MenuBack) { UI.Sound("MenuBack"); return true; }
            return false;
        }

        public void Draw(float cx, float cy)
        {
            UI.Styles();
            const float rowH = 42f;
            int vis = 0;
            foreach (var m in items) if (m.Visible) vis++;
            float ph = 104f + vis * rowH, pw = width;
            float px = cx - pw * 0.5f, py = cy - ph * 0.5f;
            UI.Panel(new Rect(px, py, pw, ph), 0.72f);
            UI.Label(new Rect(px, py + 12, pw, 50), title, UI.Title, UI.Pink);
            int row = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var m = items[i];
                if (!m.Visible) continue;
                Rect r = new Rect(px + 26, py + 72 + row * rowH, pw - 52, rowH - 6);
                row++;
                bool sel = i == index;
                if (sel) UI.Panel(r, 0.55f);
                string val = m.value != null ? m.value() : "";
                UI.Label(new Rect(r.x + 12, r.y, r.width - 24, r.height), (sel ? "›  " : "   ") + m.label, UI.Row, sel ? UI.Pink : UI.White);
                if (val.Length > 0)
                    UI.Label(new Rect(r.x + 12, r.y, r.width - 24, r.height), (m.change != null && sel ? "‹  " + val + "  ›" : val),
                             new GUIStyle(UI.Row) { alignment = TextAnchor.MiddleRight }, sel ? UI.Gold : UI.White);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    index = i;
                    if (m.select != null) { UI.Sound("MenuSelect"); m.select(); }
                    else if (m.change != null)
                    {
                        bool left = Event.current.mousePosition.x < r.center.x;
                        m.change(left ? -1 : 1);
                        UI.Sound("MenuMove");
                    }
                }
            }
            UI.Label(new Rect(px, py + ph - 30, pw, 24), footer, UI.SmallCentre, new Color(1f, 1f, 1f, 0.75f));
        }
    }
}
