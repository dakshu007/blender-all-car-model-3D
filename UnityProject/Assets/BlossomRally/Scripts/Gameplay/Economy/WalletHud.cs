// WalletHud.cs - the money readout (top right): a plum pill with a gold ¥ coin, a short count-up when the
// balance changes, a glow pulse, and "+¥120 · Tea delivery" pop-ups that float up and fade (Wallet.Changed).
// Also the little "Saved" blossom after a save. Strings are built once per change; nothing allocates per frame.
using UnityEngine;

namespace BlossomRally
{
    public class WalletHud : MonoBehaviour
    {
        public static WalletHud Instance { get; private set; }
        /// <summary>Screen y just under the wallet (and its pop-ups): other HUD cards stack below it.</summary>
        public static float BottomEdge { get; private set; } = 70f;

        struct Pop { public string amount, reason; public float t0; public bool gain; }
        const int MaxPops = 4;
        readonly Pop[] pops = new Pop[MaxPops];
        int popCount;

        int shown, from, to;
        float animT = 10f, pulseT = 10f, savedT = 10f;
        bool lastGain = true;
        readonly EcoGui.MoneyText money = new EcoGui.MoneyText();

        void Awake()
        {
            Instance = this;
            shown = from = to = Wallet.Balance;
            Wallet.Changed += OnChanged;
            EconomySave.Saved += OnSaved;
        }

        void OnDestroy()
        {
            Wallet.Changed -= OnChanged;
            EconomySave.Saved -= OnSaved;
        }

        void OnSaved() { savedT = 0f; }

        void OnChanged(int delta, string reason)
        {
            if (delta == 0 && (reason == null || !reason.StartsWith("unlock:"))) { shown = from = to = Wallet.Balance; return; }
            if (delta == 0) return;
            from = shown;
            to = Wallet.Balance;
            animT = 0f;
            pulseT = 0f;
            lastGain = delta > 0;
            // newest on top: shift the others down
            if (popCount < MaxPops) popCount++;
            for (int i = popCount - 1; i > 0; i--) pops[i] = pops[i - 1];
            pops[0] = new Pop
            {
                amount = (delta > 0 ? "+" : "−") + Wallet.Format(Mathf.Abs(delta)),
                reason = string.IsNullOrEmpty(reason) ? "" : reason,
                t0 = Time.unscaledTime,
                gain = delta > 0
            };
        }

        bool Visible(out bool inGarage)
        {
            inGarage = false;
            var gm = GameManager.Instance;
            if (gm == null || gm.World == null || !gm.World.Done) return false;
            if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) return false;
            if (ValleyMap.IsOpen || PhotoAlbum.IsOpen || GameManager.HideHud) return false;
            if (EcoMenus.IsOpen) return true;
            var garage = gm.GetComponent<Garage>();
            if (garage != null && garage.Active) { inGarage = garage.OwnsCamera && !garage.Showroom; return inGarage; }
            return gm.IsDriving;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            animT += dt; pulseT += dt; savedT += dt;
            if (to != Wallet.Balance && animT > 0.7f) { from = shown; to = Wallet.Balance; animT = 0f; }
            // stepped count-up: at most 14 different strings per change
            float k = Mathf.Clamp01(animT / 0.65f);
            int steps = 14;
            float q = Mathf.Floor(MenuArt.EaseOut(k) * steps) / steps;
            shown = k >= 1f ? to : Mathf.RoundToInt(Mathf.Lerp(from, to, q));
            // drop expired pop-ups
            while (popCount > 0 && Time.unscaledTime - pops[popCount - 1].t0 > 3.2f) popCount--;
        }

        void OnGUI()
        {
            bool inGarage;
            if (!Visible(out inGarage)) return;
            GUI.depth = -2;
            EcoGui.Styles();
            float s = EcoGui.S, W = Screen.width;
            float top = (GameSettings.ShowFps ? 36f : 14f) + 4f * s;
            string txt = money.Get(shown);
            float tw = EcoGui.Width(MenuArt.Big, txt);
            float h = 50f * s, coin = 34f * s;
            float w = tw + coin + 46f * s;
            float x = W - w - 22f * s;
            float pulse = pulseT < 0.6f ? Mathf.Sin(pulseT / 0.6f * Mathf.PI) : 0f;
            Rect r = new Rect(x, top, w, h);
            Color glow = lastGain ? UI.Gold : EcoGui.Coral;
            if (pulse > 0f) MenuArt.Tex(new Rect(r.x - 24f * s, r.y - 18f * s, r.width + 48f * s, r.height + 36f * s), MenuArt.Glow, MenuArt.A(glow, 0.55f * pulse));
            MenuArt.Tex(new Rect(r.x - 8f, r.y - 4f, r.width + 16f, r.height + 16f), MenuArt.SoftRect, new Color(0f, 0f, 0f, 0.35f));
            MenuArt.Fill(r, new Color(MenuArt.PlumDeep.r, MenuArt.PlumDeep.g, MenuArt.PlumDeep.b, 0.82f));
            MenuArt.Fill(new Rect(r.x, r.yMax - Mathf.Max(2f, 3f * s), r.width, Mathf.Max(2f, 3f * s)), MenuArt.A(UI.Gold, 0.8f));
            float cs = coin * (1f + 0.15f * pulse);
            EcoGui.Coin(new Rect(r.x + 10f * s + (coin - cs) * 0.5f, r.y + (h - cs) * 0.5f - 1f * s, cs, cs), 1f);
            Color tc = Color.Lerp(UI.White, glow, pulse * 0.8f);
            MenuArt.Text(new Rect(r.x + coin + 22f * s, r.y, tw + 10f * s, h - 2f * s), txt, MenuArt.Big, tc);
            BottomEdge = r.yMax + 6f * s;

            // saved indicator: a tiny turning blossom left of the pill
            if (savedT < 1.8f)
            {
                float a = Mathf.Clamp01((1.8f - savedT) * 2f) * Mathf.Clamp01(savedT * 6f);
                float bs = 14f * s;
                float lw = EcoGui.Width(MenuArt.Tiny, "Saved");
                Rect br = new Rect(r.x - lw - bs - 18f * s, r.center.y - bs * 0.5f, bs, bs);
                for (int i = 0; i < 5; i++)
                {
                    float ang = savedT * 3f + i * Mathf.PI * 0.4f;
                    Vector2 c = br.center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * bs * 0.36f;
                    MenuArt.Tex(new Rect(c.x - bs * 0.3f, c.y - bs * 0.3f, bs * 0.6f, bs * 0.6f), MenuArt.Dot, MenuArt.A(UI.Pink, a));
                }
                MenuArt.Text(new Rect(br.xMax + 6f * s, br.y - 2f * s, lw + 4f, bs + 4f * s), "Saved", MenuArt.Tiny, MenuArt.A(UI.White, 0.8f * a));
            }

            // pop-ups: under the pill, or left of the job card column while a job runs
            float right = JobSystem.HasJob && !EcoMenus.IsOpen ? W - 414f * s : r.xMax;
            float y = r.yMax + 6f * s;
            for (int i = 0; i < popCount; i++)
            {
                var p = pops[i];
                float t = Time.unscaledTime - p.t0;
                float a = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((3.2f - t) * 1.6f);
                float rise = (1f - MenuArt.EaseOut(t / 0.4f)) * 14f * s;
                float ph = 26f * s, pw = 420f * s;
                MenuArt.Text(new Rect(right - pw, y + rise, pw, ph), p.amount, MenuArt.MidR, MenuArt.A(p.gain ? UI.Gold : EcoGui.Coral, a));
                if (p.reason.Length > 0)
                    MenuArt.Text(new Rect(right - pw, y + rise + ph - 4f * s, pw, 18f * s), p.reason, MenuArt.SmallR, MenuArt.A(UI.White, 0.75f * a));
                y += (p.reason.Length > 0 ? 44f : 28f) * s;
            }
        }
    }
}
