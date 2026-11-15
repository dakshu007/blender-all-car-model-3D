// Showroom.cs - GameplayHooks.OpenShowroom(id): the vehicle dealership. "city_cars" (Jade Harbour Showroom)
// sells the cars, "coast_bikes" (or any id with "bike") the motorbikes; the stock is read from CarCatalog at
// runtime. Left: the vehicle on a lacquered turntable (rendered by ShowroomStage into a texture, drag to turn);
// right: the line-up (price / OWNED / CURRENT) and the selected vehicle's card with stat bars and Buy /
// "Owned - set as current". Buying asks for confirmation, stamps a SOLD seal and saves.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class Showroom : MonoBehaviour
    {
        public static Showroom Instance { get; private set; }
        public bool IsOpen { get { return EcoMenus.Current == (object)this; } }

        readonly List<CarSpec> stock = new List<CarSpec>();
        string showroomId = "", title = "", subtitle = "";
        int sel, shownSel = -1;
        bool confirm;
        int confirmChoice;          // 0 = buy, 1 = cancel
        float openT, soldT = 10f, shakeT = 10f, listScroll;
        ShowroomStage stage;
        string detailFor;
        string specLine = "", needText = "", classLine = "", confirmTitle = "", confirmLeft = "";
        static readonly string[] StatNames = { "Power", "Top speed", "Grip", "Drift", "Agility", "Off-road" };
        readonly float[] stats = new float[6];
        readonly float[] statsShown = new float[6];
        bool dragging;

        void Awake() { Instance = this; }

        void OnDestroy() { if (stage != null) stage.Dispose(); }

        // ------------------------------------------------------------------ open / close
        public void Open(string id)
        {
            var sm = StageManager.Instance;
            if (sm != null && sm.Running) { EconomyDirector.Toast("Finish the event first"); return; }
            if (!EcoMenus.TryOpen(this)) return;
            showroomId = string.IsNullOrEmpty(id) ? "city_cars" : id;
            VehiclePrices.Stock(showroomId, stock);
            title = VehiclePrices.ShowroomName(showroomId).ToUpperInvariant();
            bool bikes = showroomId.IndexOf("bike", System.StringComparison.OrdinalIgnoreCase) >= 0;
            int forSale = 0;
            foreach (var c in stock) if (!VehiclePrices.Owned(c)) forSale++;
            subtitle = stock.Count == 0 ? (bikes ? "New motorbikes arriving soon" : "Restocking - come back soon")
                     : forSale + (forSale == 1 ? " vehicle" : " vehicles") + " for sale   ·   " + (stock.Count - forSale) + " in your garage";
            sel = 0;
            var gm = GameManager.Instance;
            if (gm != null && gm.Car != null)
                for (int i = 0; i < stock.Count; i++) if (stock[i].id == gm.Car.spec.id && forSale == 0) sel = i;
            shownSel = -1;
            confirm = false;
            openT = Time.unscaledTime;
            listScroll = 0f;
            if (stage == null) stage = new ShowroomStage();
            stage.SetActive(true);
            UI.Sound("MenuSelect");
        }

        public void Close()
        {
            if (!IsOpen) return;
            EcoMenus.Close(this);
            if (stage != null) stage.SetActive(false);
            UI.Sound("MenuBack");
        }

        CarSpec Selected { get { return sel >= 0 && sel < stock.Count ? stock[sel] : null; } }

        bool IsCurrent(CarSpec c)
        {
            var gm = GameManager.Instance;
            return c != null && gm != null && gm.Car != null && gm.Car.spec != null && gm.Car.spec.id == c.id;
        }

        // ------------------------------------------------------------------ input
        void Update()
        {
            soldT += Time.unscaledDeltaTime;
            shakeT += Time.unscaledDeltaTime;
            if (!IsOpen) return;
            if (EcoMenus.Blocked) { EcoMenus.Close(this); if (stage != null) stage.SetActive(false); return; }
            if (shownSel != sel) SelectionChanged();
            for (int i = 0; i < stats.Length; i++) statsShown[i] = Mathf.Lerp(statsShown[i], stats[i], MenuArt.Damp(Time.unscaledDeltaTime, 8f));
            if (stage != null)
            {
                float turn = 0f;
                if (InputReader.Key(KeyCode.Q) || GamepadBridge.Button(PadButton.LeftShoulder)) turn -= 1f;
                if (InputReader.Key(KeyCode.E) || GamepadBridge.Button(PadButton.RightShoulder)) turn += 1f;
                float rx = GamepadBridge.RightStick.x;
                if (Mathf.Abs(rx) > 0.2f) turn += rx;
                if (turn != 0f) stage.Nudge(turn * 90f * Time.unscaledDeltaTime);
                stage.Tick(Time.unscaledDeltaTime);
            }
            if (!EcoMenus.InputFor(this)) return;
            if (confirm)
            {
                if (InputReader.MenuLeft || InputReader.MenuRight || InputReader.MenuUp || InputReader.MenuDown) { confirmChoice = 1 - confirmChoice; UI.Sound("MenuMove"); }
                if (InputReader.MenuConfirm) { if (confirmChoice == 0) Buy(); else { confirm = false; UI.Sound("MenuBack"); } }
                else if (InputReader.MenuBack) { confirm = false; UI.Sound("MenuBack"); }
                return;
            }
            int n = stock.Count;
            if (n > 0)
            {
                if (InputReader.MenuUp) { sel = (sel - 1 + n) % n; UI.Sound("MenuMove"); }
                if (InputReader.MenuDown) { sel = (sel + 1) % n; UI.Sound("MenuMove"); }
                if (InputReader.MenuConfirm) Primary();
            }
            if (InputReader.MenuBack) Close();
        }

        void SelectionChanged()
        {
            shownSel = sel;
            var c = Selected;
            confirm = false;
            if (c == null) { if (stage != null) stage.Show(null); return; }
            if (stage != null) stage.Show(c);
            stats[0] = VehiclePrices.StatPower(c); stats[1] = VehiclePrices.StatSpeed(c); stats[2] = VehiclePrices.StatGrip(c);
            stats[3] = VehiclePrices.StatDrift(c); stats[4] = VehiclePrices.StatAgility(c); stats[5] = VehiclePrices.StatOffroad(c);
            detailFor = c.id;
            classLine = c.carClass + "   ·   " + c.era;
            specLine = c.drive + "   ·   " + Mathf.RoundToInt(c.mass) + " kg   ·   " + Mathf.RoundToInt(c.maxSpeedKmh) + " km/h";
            int need = VehiclePrices.Price(c) - Wallet.Balance;
            needText = need > 0 ? "Need " + Wallet.Format(need) + " more" : "";
        }

        /// <summary>Enter / A / the big button.</summary>
        void Primary()
        {
            var c = Selected;
            if (c == null) return;
            if (VehiclePrices.Owned(c))
            {
                if (IsCurrent(c)) { UI.Sound("MenuMove"); return; }
                SetCurrent(c);
                return;
            }
            if (!Wallet.CanAfford(VehiclePrices.Price(c)))
            {
                shakeT = 0f;
                UI.Sound("MenuBack");
                EconomyDirector.Toast("Not enough money yet - jobs and rally prizes pay well");
                return;
            }
            confirm = true;
            confirmChoice = 0;
            confirmTitle = "Buy the " + c.name + "?";
            confirmLeft = "You'll have " + Wallet.Format(Wallet.Balance - VehiclePrices.Price(c)) + " left";
            UI.Sound("MenuSelect");
        }

        void Buy()
        {
            var c = Selected;
            confirm = false;
            if (c == null || VehiclePrices.Owned(c)) return;
            int price = VehiclePrices.Price(c);
            if (!Wallet.TrySpend(price, "Bought " + c.name)) { shakeT = 0f; UI.Sound("MenuBack"); return; }
            Wallet.Grant(c.id);
            var d = EconomySave.Data;
            d.spent += price;
            d.vehiclesBought++;
            soldT = 0f;
            UI.Sound("MedalGold");
            InputReader.Rumble(0.3f, 0.5f, 0.3f);
            EconomyDirector.Toast("The " + c.name + " is yours!");
            EconomyDirector.RequestSave();
            shownSel = -1;     // refresh the card
        }

        void SetCurrent(CarSpec c)
        {
            var gm = GameManager.Instance;
            int idx = VehiclePrices.IndexOf(c.id);
            if (gm == null || idx < 0) return;
            gm.SwapCar(idx);
            EcoMenus.Rehold();
            UI.Sound("MenuSelect");
            EconomyDirector.Toast(PlayerState.OnFoot ? "Your " + c.name + " is parked outside" : "Enjoy the " + c.name + "!");
            EconomyDirector.RequestSave();
        }

        // ------------------------------------------------------------------ drawing
        void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -10;
            EcoGui.Styles();
            float s = EcoGui.S, W = Screen.width, H = Screen.height;
            float a = MenuArt.EaseOut((Time.unscaledTime - openT) / 0.35f);
            MenuArt.Fill(new Rect(0f, 0f, W, H), new Color(MenuArt.PlumDeep.r, MenuArt.PlumDeep.g, MenuArt.PlumDeep.b, 0.92f * a));
            float m = 28f * s;
            float rightW = Mathf.Clamp(W * 0.34f, 420f * s, 560f * s);
            Rect stageR = new Rect(m, 96f * s, W - rightW - m * 3f, H - 96f * s - 84f * s);
            Rect right = new Rect(stageR.xMax + m, 96f * s, rightW, stageR.height);

            // header
            MenuArt.Text(new Rect(m, 18f * s, EcoGui.Width(MenuArt.Title, title) + 10f, 50f * s), title, MenuArt.Title, MenuArt.A(UI.Pink, a));
            MenuArt.Text(new Rect(m, 62f * s, W * 0.6f, 24f * s), subtitle, MenuArt.Small, MenuArt.A(UI.White, 0.75f * a));

            DrawStage(stageR, a, s);
            DrawList(new Rect(right.x, right.y, right.width, right.height * 0.42f), a, s);
            DrawCard(new Rect(right.x, right.y + right.height * 0.42f + 14f * s, right.width, right.height * 0.58f - 14f * s), a, s);

            // footer
            bool pad = InputReader.PadConnected, c0, c1, c2, c3;
            float fx = m, fy = H - 56f * s;
            fx += EcoGui.Chip(fx, fy, pad ? "D-PAD" : "↑ ↓", "choose", out c0);
            fx += EcoGui.Chip(fx, fy, pad ? "A" : "ENTER", PrimaryLabel(), out c1);
            fx += EcoGui.Chip(fx, fy, pad ? "LB RB" : "Q E / DRAG", "turn", out c3);
            EcoGui.Chip(fx, fy, pad ? "B" : "ESC", "leave", out c2);
            if (c1 && !confirm) Primary();
            if (c2) { if (confirm) confirm = false; else Close(); }

            if (confirm) DrawConfirm(a, s);
        }

        string PrimaryLabel()
        {
            var c = Selected;
            if (c == null) return "-";
            if (VehiclePrices.Owned(c)) return IsCurrent(c) ? "current vehicle" : "set as current";
            return "buy";
        }

        void DrawStage(Rect r, float a, float s)
        {
            MenuArt.Tex(new Rect(r.x - 12f, r.y - 8f, r.width + 24f, r.height + 28f), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.5f * a));
            Texture tex = stage != null ? stage.Render((int)r.width, (int)r.height) : null;
            if (tex != null) MenuArt.Tex(r, tex, MenuArt.A(Color.white, a), ScaleMode.ScaleAndCrop);
            else MenuArt.Fill(r, new Color(0.3f, 0.18f, 0.25f, a));
            MenuArt.Tex(r, MenuArt.Vignette, new Color(0f, 0f, 0f, 0.45f * a));
            MenuArt.Outline(r, Mathf.Max(1f, 2f * s), MenuArt.A(UI.Gold, 0.5f * a));
            var c = Selected;
            if (c != null)
            {
                MenuArt.Text(new Rect(r.x + 26f * s, r.yMax - 92f * s, r.width - 52f * s, 50f * s), c.name, MenuArt.Big, MenuArt.A(UI.White, a));
                MenuArt.Text(new Rect(r.x + 26f * s, r.yMax - 50f * s, r.width - 52f * s, 28f * s), classLine, MenuArt.Body, MenuArt.A(UI.Pink, a));
            }
            else
                MenuArt.Text(r, subtitle, MenuArt.Mid, MenuArt.A(UI.White, 0.8f * a));
            if (stage != null && stage.Missing) MenuArt.Text(r, "Preview unavailable", MenuArt.BodyC, MenuArt.A(UI.White, 0.6f * a));

            // SOLD seal
            if (soldT < 2.2f && c != null && VehiclePrices.Owned(c))
            {
                float k = MenuArt.EaseOut(soldT / 0.25f);
                float sa = Mathf.Clamp01((2.2f - soldT) * 2f);
                float size = Mathf.Lerp(380f, 200f, k) * s;
                Rect sr = new Rect(r.center.x - size * 0.5f, r.center.y - size * 0.5f - 20f * s, size, size);
                var seal = MenuArt.Seal;
                if (seal != null) MenuArt.Tex(sr, seal, MenuArt.A(MenuArt.Vermilion, sa * k));
                else MenuArt.Fill(sr, MenuArt.A(MenuArt.Vermilion, 0.8f * sa * k));
                MenuArt.Text(sr, "SOLD", MenuArt.Head, MenuArt.A(UI.White, sa * k), false);
            }

            // drag to turn
            var e = Event.current;
            if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && !confirm) { dragging = true; e.Use(); }
            else if (e.type == EventType.MouseUp) dragging = false;
            else if (e.type == EventType.MouseDrag && dragging && stage != null) { stage.Nudge(-e.delta.x * 0.4f); e.Use(); }
        }

        void DrawList(Rect r, float a, float s)
        {
            MenuArt.Card(r, a);
            float rowH = 46f * s;
            float inner = r.height - 16f * s;
            float top = sel * rowH, bottom = top + rowH;
            if (top < listScroll) listScroll = top;
            if (bottom > listScroll + inner) listScroll = bottom - inner;
            GUI.BeginClip(new Rect(r.x + 8f * s, r.y + 10f * s, r.width - 16f * s, inner));
            float w = r.width - 16f * s;
            for (int i = 0; i < stock.Count; i++)
            {
                Rect row = new Rect(0f, i * rowH - listScroll, w, rowH - 4f * s);
                if (row.yMax < 0f || row.y > inner) continue;
                var c = stock[i];
                bool selected = i == sel, owned = VehiclePrices.Owned(c), current = IsCurrent(c);
                MenuArt.Fill(row, selected ? MenuArt.A(UI.Pink, 0.9f * a) : new Color(1f, 1f, 1f, (i % 2 == 0 ? 0.05f : 0.02f) * a));
                Color tc = selected ? MenuArt.PlumDeep : UI.White;
                MenuArt.Text(new Rect(row.x + 14f * s, row.y, w * 0.62f, row.height), c.name, MenuArt.Body, MenuArt.A(tc, a), !selected);
                string right = current ? "CURRENT" : owned ? "OWNED" : VehiclePrices.PriceText(c);
                Color rc = selected ? MenuArt.PlumDeep : current ? EcoGui.Jade : owned ? EcoGui.Muted : Wallet.CanAfford(VehiclePrices.Price(c)) ? UI.Gold : EcoGui.Coral;
                MenuArt.Text(new Rect(row.x, row.y, w - 14f * s, row.height), right, owned ? MenuArt.SmallR : MenuArt.BodyR, MenuArt.A(rc, a), !selected);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none) && !confirm)
                {
                    if (selected) Primary();
                    else { sel = i; UI.Sound("MenuMove"); }
                }
            }
            GUI.EndClip();
            if (stock.Count == 0) MenuArt.Text(r, "Nothing on the floor today", MenuArt.BodyC, MenuArt.A(UI.White, 0.7f * a));
        }

        void DrawCard(Rect r, float a, float s)
        {
            var c = Selected;
            float shake = shakeT < 0.35f ? Mathf.Sin(shakeT * 60f) * (0.35f - shakeT) * 26f * s : 0f;
            r.x += shake;
            MenuArt.Card(r, a);
            if (c == null) return;
            float x = r.x + 22f * s, w = r.width - 44f * s, y = r.y + 14f * s;
            MenuArt.Text(new Rect(x, y, w, 24f * s), specLine, MenuArt.Small, MenuArt.A(UI.White, 0.8f * a));
            y += 32f * s;
            for (int i = 0; i < StatNames.Length; i++)
            {
                MenuArt.Text(new Rect(x, y, 110f * s, 22f * s), StatNames[i], MenuArt.Small, MenuArt.A(UI.White, a));
                EcoGui.Bar(new Rect(x + 112f * s, y + 8f * s, w - 112f * s, 8f * s), 0.08f + statsShown[i] * 0.92f, UI.Pink, a);
                y += 26f * s;
            }
            float by = r.yMax - 66f * s;
            bool owned = VehiclePrices.Owned(c), current = IsCurrent(c);
            int price = VehiclePrices.Price(c);
            bool afford = Wallet.CanAfford(price);
            // price line
            float py = by - 46f * s;
            if (!owned)
            {
                MenuArt.Text(new Rect(x, py, w * 0.5f, 40f * s), "Price", MenuArt.Body, MenuArt.A(UI.White, 0.7f * a));
                MenuArt.Text(new Rect(x, py, w, 40f * s), VehiclePrices.PriceText(c), RightBig(), MenuArt.A(afford ? UI.Gold : EcoGui.Coral, a));
                if (!afford && needText.Length > 0 && detailFor == c.id)
                    MenuArt.Text(new Rect(x, py + 30f * s, w, 20f * s), needText, MenuArt.SmallR, MenuArt.A(EcoGui.Coral, 0.9f * a));
            }
            else
                MenuArt.Text(new Rect(x, py, w, 40f * s), current ? "You're driving this one" : "In your garage", MenuArt.BodyC, MenuArt.A(EcoGui.Jade, a));
            Rect b = new Rect(x, by, w, 52f * s);
            string label = owned ? (current ? "Current vehicle" : "Owned - set as current") : afford ? "Buy" : "Not enough money";
            Color acc = owned ? EcoGui.Jade : UI.Gold;
            if (EcoGui.Button(b, label, true, !current && (owned || afford), acc, a) && !confirm) Primary();
        }

        static GUIStyle rightBig;
        static int rightBigSize;
        static GUIStyle RightBig()
        {
            if (rightBig == null || rightBigSize != MenuArt.Big.fontSize)
            {
                rightBig = new GUIStyle(MenuArt.Big) { alignment = TextAnchor.MiddleRight };
                rightBigSize = MenuArt.Big.fontSize;
            }
            return rightBig;
        }

        void DrawConfirm(float a, float s)
        {
            var c = Selected;
            if (c == null) { confirm = false; return; }
            float W = Screen.width, H = Screen.height;
            MenuArt.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.45f * a));
            float pw = 560f * s, ph = 250f * s;
            Rect p = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
            MenuArt.Card(p, a);
            MenuArt.Text(new Rect(p.x, p.y + 20f * s, pw, 40f * s), confirmTitle, MenuArt.Mid, MenuArt.A(UI.White, a));
            MenuArt.Text(new Rect(p.x, p.y + 64f * s, pw, 44f * s), VehiclePrices.PriceText(c), MenuArt.Title, MenuArt.A(UI.Gold, a));
            MenuArt.Text(new Rect(p.x, p.y + 110f * s, pw, 24f * s), confirmLeft, MenuArt.SmallC, MenuArt.A(UI.White, 0.7f * a));
            float bw = (pw - 72f * s) * 0.5f;
            Rect b0 = new Rect(p.x + 24f * s, p.yMax - 76f * s, bw, 52f * s);
            Rect b1 = new Rect(b0.xMax + 24f * s, b0.y, bw, b0.height);
            if (EcoGui.Button(b0, "Buy", confirmChoice == 0, true, UI.Gold, a)) Buy();
            if (EcoGui.Button(b1, "Not yet", confirmChoice == 1, true, UI.Pink, a)) { confirm = false; UI.Sound("MenuBack"); }
        }
    }
}
