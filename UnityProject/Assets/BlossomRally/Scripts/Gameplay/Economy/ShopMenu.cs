// ShopMenu.cs - GameplayHooks.OpenShop(ShopDef): a generic counter menu for tea houses, snack stalls, souvenir
// shops... Items come from the caller (name, description, price, onBought). Buying spends from the Wallet and
// calls onBought; free items (price 0) just run. Keyboard, gamepad and mouse.
using UnityEngine;

namespace BlossomRally
{
    public class ShopMenu : MonoBehaviour
    {
        public static ShopMenu Instance { get; private set; }
        public bool IsOpen { get { return EcoMenus.Current == (object)this; } }

        ShopDef shop;
        int sel;
        float openT, shakeT = 10f, boughtT = 10f;
        string[] priceTexts = new string[0];
        string lastBought = "";

        void Awake() { Instance = this; }

        public void Open(ShopDef def)
        {
            if (def == null || def.items == null) return;
            if (!EcoMenus.TryOpen(this)) return;
            shop = def;
            sel = 0;
            openT = Time.unscaledTime;
            priceTexts = new string[def.items.Count];
            for (int i = 0; i < def.items.Count; i++)
            {
                var it = def.items[i];
                priceTexts[i] = it == null ? "" : it.price <= 0 ? "Free" : Wallet.Format(Price(it));
            }
            UI.Sound("MenuSelect");
        }

        public void Close()
        {
            if (!IsOpen) return;
            EcoMenus.Close(this);
            shop = null;
            UI.Sound("MenuBack");
        }

        void Update()
        {
            shakeT += Time.unscaledDeltaTime;
            boughtT += Time.unscaledDeltaTime;
            if (!IsOpen) return;
            if (EcoMenus.Blocked || shop == null) { EcoMenus.Close(this); shop = null; return; }
            if (!EcoMenus.InputFor(this)) return;
            int n = shop.items.Count;
            if (n > 0)
            {
                if (InputReader.MenuUp) { sel = (sel - 1 + n) % n; UI.Sound("MenuMove"); }
                if (InputReader.MenuDown) { sel = (sel + 1) % n; UI.Sound("MenuMove"); }
                if (InputReader.MenuConfirm) Buy(sel);
            }
            if (InputReader.MenuBack) Close();
        }

        /// <summary>Price after the places module's discount buff (a temple blessing, a regular's tea).</summary>
        static int Price(ShopItem it) { return it == null || it.price <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(it.price * (1f - Buffs.ShopDiscount))); }

        void Buy(int i)
        {
            if (shop == null || i < 0 || i >= shop.items.Count) return;
            var it = shop.items[i];
            if (it == null) return;
            int cost = Price(it);
            if (cost > 0 && !Wallet.TrySpend(cost, it.name))
            {
                shakeT = 0f;
                UI.Sound("MenuBack");
                return;
            }
            if (cost > 0) EconomySave.Data.spent += cost;
            lastBought = it.name;
            boughtT = 0f;
            UI.Sound("MedalBronze");
            if (it.onBought != null)
            {
                try { it.onBought(); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            EconomyDirector.RequestSave();
        }

        void OnGUI()
        {
            if (!IsOpen || shop == null) return;
            GUI.depth = -10;
            EcoGui.Styles();
            float s = EcoGui.S, W = Screen.width, H = Screen.height;
            float a = MenuArt.EaseOut((Time.unscaledTime - openT) / 0.3f);
            EcoGui.Dim(a * 0.8f);
            int n = shop.items.Count;
            float rowH = 62f * s;
            float pw = Mathf.Min(W - 40f * s, 640f * s), ph = Mathf.Min(H - 60f * s, 190f * s + Mathf.Max(1, n) * rowH);
            float shake = shakeT < 0.35f ? Mathf.Sin(shakeT * 60f) * (0.35f - shakeT) * 30f * s : 0f;
            Rect p = new Rect((W - pw) * 0.5f + shake, (H - ph) * 0.5f + (1f - a) * 24f * s, pw, ph);
            MenuArt.Card(p, a);
            MenuArt.Text(new Rect(p.x + 28f * s, p.y + 14f * s, pw - 56f * s, 42f * s), string.IsNullOrEmpty(shop.title) ? "Shop" : shop.title, MenuArt.Big, MenuArt.A(UI.Pink, a));
            if (!string.IsNullOrEmpty(shop.subtitle))
                MenuArt.Text(new Rect(p.x + 28f * s, p.y + 52f * s, pw - 56f * s, 22f * s), shop.subtitle, MenuArt.Small, MenuArt.A(UI.White, 0.75f * a));
            float y = p.y + 86f * s;
            for (int i = 0; i < n; i++)
            {
                var it = shop.items[i];
                if (it == null) continue;
                Rect r = new Rect(p.x + 22f * s, y, pw - 44f * s, rowH - 8f * s);
                bool selected = i == sel;
                bool afford = it.price <= 0 || Wallet.CanAfford(Price(it));
                MenuArt.Fill(r, selected ? new Color(1f, 1f, 1f, 0.14f * a) : new Color(1f, 1f, 1f, 0.05f * a));
                if (selected) MenuArt.Outline(r, Mathf.Max(1f, 2f * s), MenuArt.A(UI.Pink, 0.9f * a));
                MenuArt.Text(new Rect(r.x + 16f * s, r.y + 4f * s, r.width * 0.7f, 28f * s), it.name, MenuArt.MidL, MenuArt.A(UI.White, a));
                if (!string.IsNullOrEmpty(it.description))
                    MenuArt.Text(new Rect(r.x + 16f * s, r.y + 30f * s, r.width * 0.72f, 22f * s), it.description, MenuArt.Small, MenuArt.A(UI.White, 0.65f * a));
                MenuArt.Text(new Rect(r.x, r.y, r.width - 16f * s, r.height), i < priceTexts.Length ? priceTexts[i] : "", MenuArt.MidR,
                             MenuArt.A(afford ? UI.Gold : EcoGui.Coral, a));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { if (selected) Buy(i); else { sel = i; UI.Sound("MenuMove"); } }
                y += rowH;
            }
            if (n == 0) MenuArt.Text(new Rect(p.x, y, pw, 40f * s), "Sold out - come back tomorrow.", MenuArt.BodyC, MenuArt.A(UI.White, 0.7f * a));
            if (boughtT < 2f && lastBought.Length > 0)
            {
                float ba = Mathf.Clamp01((2f - boughtT) * 2f);
                MenuArt.Text(new Rect(p.x, p.yMax - 86f * s, pw, 26f * s), lastBought, MenuArt.BodyC, MenuArt.A(EcoGui.Jade, ba * a));
            }
            bool pad = InputReader.PadConnected, c0, c1, c2;
            float fx = p.x + 28f * s, fy = p.yMax - 48f * s;
            fx += EcoGui.Chip(fx, fy, pad ? "D-PAD" : "↑ ↓", "choose", out c0);
            fx += EcoGui.Chip(fx, fy, pad ? "A" : "ENTER", "buy", out c1);
            EcoGui.Chip(fx, fy, pad ? "B" : "ESC", "leave", out c2);
            if (c1) Buy(sel);
            if (c2) Close();
        }
    }
}
