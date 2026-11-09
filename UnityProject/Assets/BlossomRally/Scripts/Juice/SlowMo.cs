// SlowMo.cs - short slow-motion "moments" (Big Air, Perfect Drift, finish line) with a smooth ramp in /
// hold / ramp out on real time. Plays nicely with everything else that touches Time.timeScale:
//  * only runs while GameManager.Instance.IsDriving and neither ReplaySystem nor PhotoMode is active
//  * never touches timeScale while it is 0 (pause menu / photo mode own it) and never "unpauses"
//  * whenever the effect is aborted it hands back exactly 1 - but only if the current value is still the one
//    it set (so a pause / unpause in the middle, or photo mode restoring its saved value, end up at 1)
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(950)]   // after GameManager.Update (pause handling) in the same frame
    public class SlowMo : MonoBehaviour
    {
        public static SlowMo Instance { get; private set; }

        const float RampIn = 0.12f, RampOut = 0.35f;

        bool active;
        float t, hold, target = 0.35f;
        bool owned;               // timeScale currently holds a value we wrote
        float lastSet = 1f;

        /// <summary>True while a slow-motion moment is playing.</summary>
        public static bool Active { get { return Instance != null && Instance.active; } }

        /// <summary>Current slow-motion amount 0 (normal) .. 1 (fully slowed) for audio / FX that want to follow it.</summary>
        public static float Amount { get; private set; }

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            Restore();
            if (Instance == this) Instance = null;
        }

        void OnDisable() { Restore(); active = false; Amount = 0f; }

        static bool Allowed()
        {
            var gm = GameManager.Instance;
            if (!JuiceSettings.SlowMoMoments || gm == null || !gm.IsDriving) return false;
            if (ReplaySystem.Instance != null && ReplaySystem.Instance.Active) return false;
            if (PhotoMode.Instance != null && PhotoMode.Instance.Active) return false;
            return true;
        }

        /// <summary>Start (or extend) a moment: hold for holdSeconds of real time at the given time scale.</summary>
        public static bool Trigger(float holdSeconds, float scale = 0.35f)
        {
            var s = Instance;
            if (s == null || !Allowed() || Time.timeScale <= 0f) return false;
            if (s.active)
            {
                // extend: restart the hold; while ramping out, continue from the same depth by ramping in again
                if (s.t > RampIn + s.hold) s.t = RampIn * InvSmooth(Amount);
                else if (s.t > RampIn) s.t = RampIn;
                s.hold = holdSeconds;
                s.target = Mathf.Min(s.target, Mathf.Clamp(scale, 0.1f, 1f));
                return true;
            }
            s.active = true;
            s.t = 0f;
            s.hold = holdSeconds;
            s.target = Mathf.Clamp(scale, 0.1f, 1f);
            return true;
        }

        void Update()
        {
            if (active && (!Allowed() || Time.timeScale <= 0f))
                active = false;               // abort: whoever paused owns timeScale now

            if (active)
            {
                t += Time.unscaledDeltaTime;
                float w;
                if (t < RampIn) w = Smooth(t / RampIn);
                else if (t < RampIn + hold) w = 1f;
                else w = 1f - Smooth((t - RampIn - hold) / RampOut);
                if (t >= RampIn + hold + RampOut) { active = false; w = 0f; }
                Amount = w;
                float s = Mathf.Lerp(1f, target, w);
                if (!active) { Restore(); return; }
                Time.timeScale = s;
                lastSet = s;
                owned = true;
                return;
            }
            Amount = 0f;
            if (owned) Restore();
        }

        /// <summary>Hand timeScale back (1) if it still holds our value; leave 0 alone (pause / photo mode).</summary>
        void Restore()
        {
            if (!owned) return;
            float ts = Time.timeScale;
            if (ts <= 0f) return;                                         // paused: GameManager / PhotoMode restore later
            if (PhotoMode.Instance != null && PhotoMode.Instance.Active) return;
            if (Mathf.Abs(ts - lastSet) < 1e-4f) Time.timeScale = 1f;     // still ours -> back to normal
            owned = false;                                                // someone else set it (e.g. unpause to 1)
            lastSet = 1f;
        }

        static float InvSmooth(float y)
        {
            // inverse of smoothstep on [0, 1]
            y = Mathf.Clamp01(y);
            return 0.5f - Mathf.Sin(Mathf.Asin(1f - 2f * y) / 3f);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
