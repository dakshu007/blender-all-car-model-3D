// LoadingScreen.cs - full-screen ink-wash paintings of the valley while the world is built:
// three procedural paintings (generated on worker threads at startup) cross-fade with a slow
// Ken Burns drift, the title floats in a patch of mist, a brush-stroke progress bar fills along
// the bottom and driving tips rotate with a fade. GameManager calls LoadingScreen.Draw(progress,
// status) from OnGUI while it is loading. The paintings are reused as menu backdrops.
using UnityEngine;

namespace BlossomRally
{
    public static class LoadingScreen
    {
        static InkPainting[] paintings;
        static float shownProgress;
        static int tipIndex = -1;
        static float tipStart;
        static string tipText = "";
        static float firstDraw = -1f;

        internal static void Init()
        {
            if (paintings != null) return;
            int w = 1280, h = 720;
            paintings = new InkPainting[3];
            for (int i = 0; i < paintings.Length; i++)
            {
                paintings[i] = new InkPainting(w, h, i, 7919 * (i + 3));
                paintings[i].Start();
            }
            tipIndex = Random.Range(0, 1000);
        }

        internal static void Poll()
        {
            if (paintings == null) return;
            foreach (var p in paintings) p.Poll(5f);
        }

        /// <summary>A finished painting (0..2) or null while it is still being painted.</summary>
        public static Texture2D Painting(int i)
        {
            if (paintings == null) Init();
            if (paintings == null || paintings.Length == 0) return null;
            var p = paintings[((i % paintings.Length) + paintings.Length) % paintings.Length];
            return p.Done ? p.Texture : null;
        }

        /// <summary>Any finished painting (prefers 'preferred').</summary>
        public static Texture2D AnyPainting(int preferred)
        {
            for (int k = 0; k < 3; k++)
            {
                var t = Painting(preferred + k);
                if (t != null) return t;
            }
            return null;
        }

        static string Key(GameAction a)
        {
            string k = GameSettings.Binding(a).ToString();
            return k == "Space" ? "Space" : k;
        }

        static string Tip(int i)
        {
            switch (((i % 16) + 16) % 16)
            {
                case 0: return "Hold " + Key(GameAction.Handbrake) + " and steer to drift - the handbrake kicks the tail out, the throttle holds the slide.";
                case 1: return "Steer into the slide for more angle; counter-steer or lift off to straighten out.";
                case 2: return "Keep a drift alive to build the combo multiplier - all the way up to x8.";
                case 3: return "Missed a gate? " + Key(GameAction.Reset) + " puts you back at the last checkpoint for a 2 second penalty.";
                case 4: return "Set a new best time to save a ghost - race it next time with the same car.";
                case 5: return "Press " + Key(GameAction.Camera) + " to cycle the chase, cockpit, hood and cinematic cameras.";
                case 6: return "Photo mode (" + Key(GameAction.Photo) + ") freezes the valley: filters, depth of field, letterbox and a free camera.";
                case 7: return "Your snapshots live in the Photo Album - open it from the title or pause menu.";
                case 8: return "Gravel and dirt have less grip than asphalt - brake earlier on the Tea Terrace Sprint.";
                case 9: return "G changes the weather, T the time of day, P the amount of drifting petals.";
                case 10: return "Rain makes the asphalt slippery - and the lanterns glow beautifully at Blue Hour.";
                case 11: return "In the garage press V (gamepad Y) to walk around your car in the showroom.";
                case 12: return "Petals and dust settle on your car as you drive - X in the garage washes and polishes it.";
                case 13: return "The pagoda hairpins reward a late apex and a short, sharp flick of the handbrake.";
                case 14: return "Plug in a gamepad for analogue steering, triggers and rumble.";
                default: return "Pick a stage on the valley map - gold, silver and bronze medals wait on every route.";
            }
        }

        /// <summary>Draw the loading screen. Call from OnGUI (any event) while loading.</summary>
        public static void Draw(float progress, string status)
        {
            if (paintings == null) Init();
            MenuArt.Styles();
            float now = Time.unscaledTime;
            if (firstDraw < 0f) { firstDraw = now; tipStart = now; }
            if (Event.current.type == EventType.Repaint) Poll();
            float W = Screen.width, H = Screen.height, s = MenuArt.S;

            // ---- paper + paintings ----
            MenuArt.PaperFill(new Rect(0, 0, W, H), Color.white);
            int ready = 0;
            for (int i = 0; i < paintings.Length; i++) if (paintings[i].Done) ready++;
            if (ready > 0)
            {
                const float hold = 9f, fade = 2.2f;
                float t = now - firstDraw;
                int slot = Mathf.FloorToInt(t / hold);
                float within = t - slot * hold;
                int cur = ReadyIndex(slot, ready), next = ReadyIndex(slot + 1, ready);
                DrawPainting(cur, within / hold, 1f, W, H);
                if (ready > 1 && within > hold - fade) DrawPainting(next, 0f, MenuArt.Ease((within - (hold - fade)) / fade), W, H);
            }

            // ---- title in a patch of mist ----
            float ty = H * 0.11f;
            MenuArt.Tex(new Rect(W * 0.5f - 520f * s, ty - 90f * s, 1040f * s, 300f * s), MenuArt.Glow, MenuArt.A(MenuArt.PaperCol, 0.85f));
            MenuArt.Text(new Rect(0, ty, W, 90f * s), "BLOSSOM RALLY", MenuArt.Head, MenuArt.Ink, false);
            MenuArt.Text(new Rect(0, ty + 78f * s, W, 34f * s), "a spring valley rally", MenuArt.Calli, new Color(0.62f, 0.14f, 0.20f, 0.95f), false);
            MenuArt.Tex(new Rect(W * 0.5f + 300f * s, ty + 6f * s, 46f * s, 46f * s), MenuArt.Seal, Color.white);

            // ---- bottom band: tip, status, brush progress ----
            float bandH = 150f * s;
            Rect band = new Rect(0, H - bandH, W, bandH);
            for (int i = 0; i < 12; i++)
            {
                float k = i / 12f;
                MenuArt.Fill(new Rect(0, band.y + bandH * k, W, bandH / 12f + 1f), new Color(MenuArt.PlumDeep.r, MenuArt.PlumDeep.g, MenuArt.PlumDeep.b, 0.15f + 0.6f * k));
            }
            // tips rotate with a cross-fade
            const float tipHold = 6.5f;
            if (now - tipStart > tipHold || tipText.Length == 0)
            {
                if (tipText.Length > 0) tipIndex++;
                tipStart = now;
                tipText = Tip(tipIndex);
            }
            float ta = Mathf.Clamp01((now - tipStart) / 0.5f) * Mathf.Clamp01((tipHold - (now - tipStart)) / 0.5f);
            float tx = W * 0.5f - 560f * s;
            MenuArt.Text(new Rect(tx, band.y + 30f * s, 70f * s, 30f * s), "TIP", MenuArt.MidL, MenuArt.A(UI.Pink, ta));
            MenuArt.Text(new Rect(tx + 70f * s, band.y + 22f * s, 1050f * s, 48f * s), tipText, MenuArt.Body, MenuArt.A(UI.White, ta));

            // brush progress bar
            shownProgress = Mathf.MoveTowards(shownProgress, Mathf.Clamp01(progress), Time.unscaledDeltaTime * 0.8f);
            if (progress >= 1f) shownProgress = Mathf.Max(shownProgress, 0.999f);
            float bx = W * 0.5f - 560f * s, bw = 1120f * s, by = band.y + 94f * s, bh = 10f * s;
            MenuArt.Fill(new Rect(bx, by + bh * 0.5f - 1f, bw, 2f), new Color(1f, 1f, 1f, 0.25f));
            float fw = bw * shownProgress;
            // a slightly ragged brush stroke: several offset strips of varying thickness
            for (int i = 0; i < 6; i++)
            {
                float o = (i - 2.5f) * bh * 0.16f;
                float th = bh * (0.55f + 0.1f * Mathf.Sin(i * 1.7f));
                float len = fw * (1f - 0.012f * Mathf.Abs(i - 2.5f));
                MenuArt.Fill(new Rect(bx, by + bh * 0.5f + o - th * 0.5f, len, th), new Color(0.98f, 0.80f, 0.86f, 0.22f));
            }
            MenuArt.Fill(new Rect(bx, by + bh * 0.2f, fw, bh * 0.6f), UI.Pink);
            if (fw > 2f) MenuArt.Tex(new Rect(bx + fw - 18f * s, by + bh * 0.5f - 18f * s, 36f * s, 36f * s), MenuArt.Glow, new Color(1f, 0.9f, 0.95f, 0.9f));
            MenuArt.Text(new Rect(bx, by + 18f * s, bw * 0.7f, 26f * s), (string.IsNullOrEmpty(status) ? "Preparing" : status) + "...", MenuArt.Small, MenuArt.A(UI.White, 0.9f));
            MenuArt.Text(new Rect(bx + bw * 0.5f, by + 18f * s, bw * 0.5f, 26f * s), Mathf.RoundToInt(shownProgress * 100f) + "%", MenuArt.SmallR, UI.Gold);

            // spinning blossom
            Vector2 fc = new Vector2(bx - 34f * s, by + bh * 0.5f);
            float rot = now * 1.6f;
            for (int i = 0; i < 5; i++)
            {
                float a = rot + i * Mathf.PI * 0.4f;
                Vector2 p = fc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f * s;
                MenuArt.Tex(new Rect(p.x - 7f * s, p.y - 7f * s, 14f * s, 14f * s), MenuArt.Dot, new Color(1f, 0.74f, 0.84f, 0.95f));
            }
            MenuArt.Tex(new Rect(fc.x - 3.5f * s, fc.y - 3.5f * s, 7f * s, 7f * s), MenuArt.Dot, UI.Gold);
        }

        static int ReadyIndex(int slot, int ready)
        {
            int n = ((slot % ready) + ready) % ready, seen = 0;
            for (int i = 0; i < paintings.Length; i++)
                if (paintings[i].Done) { if (seen == n) return i; seen++; }
            return 0;
        }

        static void DrawPainting(int i, float t, float alpha, float W, float H)
        {
            var p = paintings[i];
            if (!p.Done) return;
            float fadeIn = MenuArt.Ease((Time.unscaledTime - p.ReadyTime) / 1.2f);
            float a = alpha * fadeIn;
            if (a <= 0.001f) return;
            // cover the screen keeping the aspect, with a slow zoom + pan
            float aspect = p.W / (float)p.H;
            float w = Mathf.Max(W, H * aspect), h = w / aspect;
            if (h < H) { h = H; w = h * aspect; }
            float zoom = 1.06f - 0.05f * t;
            w *= zoom; h *= zoom;
            float panX = (i % 2 == 0 ? -1f : 1f) * (t - 0.5f) * W * 0.02f;
            Rect r = new Rect((W - w) * 0.5f + panX, (H - h) * 0.5f, w, h);
            MenuArt.Tex(r, p.Texture, new Color(1f, 1f, 1f, a));
        }
    }

    /// <summary>Starts the paintings at boot and uploads them as they finish.</summary>
    public class LoadingScreenDriver : MonoBehaviour
    {
        void Awake() { LoadingScreen.Init(); }
        void Update() { LoadingScreen.Poll(); }
    }
}
