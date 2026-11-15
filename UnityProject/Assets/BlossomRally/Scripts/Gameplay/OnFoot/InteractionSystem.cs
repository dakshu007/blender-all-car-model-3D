// InteractionSystem.cs - finds the nearest usable Interactable (on foot or from the vehicle, per its flags),
// shows a clean prompt card ("E  Enter Tea House" + detail line) and calls Use() on E / gamepad X.
// Also shows "F  Get in <car>" near an enterable vehicle and a quiet "F  Get out" hint when the car is stopped.
// While an E prompt is visible, InputReader.InteractPromptVisible makes gamepad X interact instead of photo
// mode and keeps E / Q from shifting gears. No per-frame allocations.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(40)]
    public sealed class InteractionSystem : MonoBehaviour
    {
        /// <summary>The interactable whose prompt is shown (null = none).</summary>
        public static Interactable Current { get; private set; }

        float fade, vFade, outFade;
        string shownPrompt, shownDetail, vehicleName;
        readonly GUIContent gc = new GUIContent();
        float stoppedFor;

        void Update()
        {
            var gm = GameManager.Instance;
            var foot = PlayerOnFoot.Instance;
            bool active = PlayerOnFoot.GameplayActive(gm) && !PlayerState.InTransition;
            bool onFoot = PlayerState.OnFoot;
            Interactable best = null;
            if (active)
            {
                Vector3 p = PlayerState.Position;
                float bestD = float.MaxValue;
                var all = Interactable.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var it = all[i];
                    if (it == null || !it.isActiveAndEnabled) continue;
                    float r = onFoot ? it.radius : it.radius + 2f;
                    float d = (it.transform.position - p).sqrMagnitude;
                    if (d > r * r || d >= bestD) continue;
                    bool ok;
                    try { ok = it.CanUse(onFoot); }
                    catch (System.Exception e) { Debug.LogException(e); ok = false; }
                    if (!ok) continue;
                    bestD = d;
                    best = it;
                }
            }
            Current = best;
            InputReader.InteractPromptVisible = best != null;
            if (best != null)
            {
                shownPrompt = best.prompt;
                shownDetail = best.detail;
                if (Time.deltaTime > 0f && InputReader.InteractDown)
                {
                    UI.Sound("MenuSelect");
                    try { best.Use(); }
                    catch (System.Exception e) { Debug.LogException(e); }
                }
            }

            // vehicle prompts
            ArcadeCar near = foot != null && active && onFoot ? foot.NearVehicle : null;
            if (near != null) vehicleName = foot.NearVehicleName;
            var car = gm != null ? gm.Car : null;
            var st = StageManager.Instance;
            bool stopped = active && !onFoot && car != null && foot != null && !foot.Busy && car.SpeedKmh < 3f && (st == null || !st.Running);
            stoppedFor = stopped ? stoppedFor + Time.deltaTime : 0f;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            fade = Mathf.MoveTowards(fade, best != null ? 1f : 0f, dt * 6f);
            vFade = Mathf.MoveTowards(vFade, near != null ? 1f : 0f, dt * 6f);
            outFade = Mathf.MoveTowards(outFade, stoppedFor > 1.2f ? 1f : 0f, dt * 3f);
        }

        void OnDisable() { InputReader.InteractPromptVisible = false; Current = null; }

        void OnGUI()
        {
            if (fade <= 0.001f && vFade <= 0.001f && outFade <= 0.001f) return;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsDriving || GameManager.HideHud || ValleyMap.IsOpen || PhotoAlbum.IsOpen || GameplayHooks.MenuOpen) return;
            if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) return;
            MenuArt.Styles();
            float s = MenuArt.S, W = Screen.width, H = Screen.height;
            bool pad = InputReader.PadConnected;
            float y = H - 210f * s;
            if (fade > 0.001f && shownPrompt != null)
            {
                y = Card(W, y, pad ? "X" : "E", shownPrompt, shownDetail, fade, s, true);
                y -= 10f * s;
            }
            if (vFade > 0.001f && vehicleName != null) y = Card(W, y, pad ? "Y" : "F", "Get in", vehicleName, vFade, s, true);
            if (outFade > 0.001f && fade <= 0.001f) Card(W, H - 150f * s, pad ? "Y" : "F", "Get out", null, outFade * 0.75f, s, false);
        }

        /// <summary>Key badge + prompt (+ detail) centred; returns the top edge for stacking upwards.</summary>
        float Card(float W, float bottom, string key, string prompt, string detail, float a, float s, bool card)
        {
            bool two = !string.IsNullOrEmpty(detail);
            float h = (two ? 62f : 44f) * s;
            float key0 = 34f * s;
            gc.text = prompt;
            float pw = MenuArt.Mid.CalcSize(gc).x;
            float dw = 0f;
            if (two) { gc.text = detail; dw = MenuArt.Small.CalcSize(gc).x; }
            float w = key0 + 26f * s + Mathf.Max(pw, dw) + 22f * s;
            float rise = (1f - a) * 10f * s;
            Rect r = new Rect(Mathf.Round((W - w) * 0.5f), Mathf.Round(bottom - h + rise), w, h);
            if (card) MenuArt.Card(r, a);
            else MenuArt.Fill(r, MenuArt.A(MenuArt.Plum, 0.45f * a));
            Rect k = new Rect(r.x + 12f * s, r.y + (h - key0) * 0.5f, key0, key0);
            MenuArt.Fill(k, MenuArt.A(UI.White, 0.95f * a));
            MenuArt.Text(k, key, MenuArt.TinyC, MenuArt.A(MenuArt.Plum, a), false);
            float tx = k.xMax + 14f * s;
            if (two)
            {
                MenuArt.Text(new Rect(tx, r.y + 6f * s, pw + 8f * s, 30f * s), prompt, MenuArt.MidL, MenuArt.A(UI.White, a));
                MenuArt.Text(new Rect(tx, r.y + 34f * s, dw + 8f * s, 22f * s), detail, MenuArt.Small, MenuArt.A(UI.Pink, a));
            }
            else MenuArt.Text(new Rect(tx, r.y + (h - 30f * s) * 0.5f, pw + 8f * s, 30f * s), prompt, MenuArt.MidL, MenuArt.A(UI.White, a));
            return r.y;
        }
    }
}
