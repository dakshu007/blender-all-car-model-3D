// InputReader.cs - one place for all player input:
//  * keyboard through the legacy Input Manager, or the Input System package when only the new
//    backend is enabled (so the game works with every "Active Input Handling" setting)
//  * gamepad (Xbox / PlayStation / MFi): RT throttle, LT brake, left stick steer, A/B handbrake,
//    Y camera, X photo, Select reset, Start pause, LB/RB replay, d-pad menus - with rumble
//  * remappable keyboard bindings from GameSettings
using UnityEngine;

namespace BlossomRally
{
    public static class InputReader
    {
        static bool axesOk = true;

        /// <summary>True when some keyboard path is available (legacy manager or Input System).</summary>
        public static bool Legacy
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return true;
#else
                return GamepadBridge.KeyboardAvailable;
#endif
            }
        }

        public static bool PadConnected { get { return GamepadBridge.Connected; } }

        public static bool Key(KeyCode k)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(k);
#else
            return GamepadBridge.KeyHeld(k);
#endif
        }

        public static bool Down(KeyCode k)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(k);
#else
            return GamepadBridge.KeyDown(k);
#endif
        }

        static float Axis(string name)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!axesOk || GamepadBridge.Connected) return 0f;     // the Input System pad is read directly
            try { return Input.GetAxisRaw(name); }
            catch (System.ArgumentException) { axesOk = false; return 0f; }
#else
            return 0f;
#endif
        }

        static bool Bound(GameAction a) { return Key(GameSettings.Binding(a)); }
        static bool BoundDown(GameAction a) { return Down(GameSettings.Binding(a)); }

        public static float Throttle
        {
            get
            {
                float k = (Bound(GameAction.Throttle) || Key(KeyCode.UpArrow)) ? 1f : 0f;
                float pad = GamepadBridge.RightTrigger;
                float ax = Axis("Vertical") > 0.2f ? Axis("Vertical") : 0f;
                return Mathf.Clamp01(Mathf.Max(k, Mathf.Max(pad, ax)));
            }
        }

        public static float Brake
        {
            get
            {
                float k = (Bound(GameAction.Brake) || Key(KeyCode.DownArrow)) ? 1f : 0f;
                float pad = GamepadBridge.LeftTrigger;
                float ax = Axis("Vertical") < -0.2f ? -Axis("Vertical") : 0f;
                return Mathf.Clamp01(Mathf.Max(k, Mathf.Max(pad, ax)));
            }
        }

        /// <summary>True while the steering value comes from an analogue stick (no keyboard smoothing).</summary>
        public static bool AnalogSteer { get; private set; }

        public static float Steer
        {
            get
            {
                float k = 0f;
                if (Bound(GameAction.SteerLeft) || Key(KeyCode.LeftArrow)) k -= 1f;
                if (Bound(GameAction.SteerRight) || Key(KeyCode.RightArrow)) k += 1f;
                if (Mathf.Abs(k) > 0f) { AnalogSteer = false; return k; }
                float s = GamepadBridge.LeftStick.x;
                if (Mathf.Abs(s) > 0.1f)
                {
                    AnalogSteer = true;
                    // dead zone + gentle response curve: precise small corrections, full lock at the edge
                    float m = Mathf.Clamp01((Mathf.Abs(s) - 0.1f) / 0.85f);
                    return Mathf.Sign(s) * (0.55f * m + 0.45f * m * m);
                }
                float a = Axis("Horizontal");
                AnalogSteer = Mathf.Abs(a) > 0.15f;
                return AnalogSteer ? a : 0f;
            }
        }

        public static bool Handbrake
        {
            get
            {
                return Bound(GameAction.Handbrake) || Key(KeyCode.JoystickButton0) ||
                       GamepadBridge.Button(PadButton.South) || GamepadBridge.Button(PadButton.East);
            }
        }

        // ---- manual gears (driving only; DriveSettings.ManualGears) ----
        static int flickState;
        static int shiftFrame = -1, shiftValue;

        /// <summary>
        /// Gear-shift request this frame: +1 up (LeftShift / E, gamepad RB or right stick flicked up),
        /// -1 down (LeftCtrl / Q, gamepad R3 or right stick flicked down), 0 none. Safe to call several times a frame.
        /// </summary>
        public static int ShiftRequest()
        {
            if (shiftFrame == Time.frameCount) return shiftValue;
            shiftFrame = Time.frameCount;
            int r = 0;
            if (Down(KeyCode.LeftShift) || Down(KeyCode.E) || GamepadBridge.ButtonDown(PadButton.RightShoulder)) r = 1;
            else if (Down(KeyCode.LeftControl) || Down(KeyCode.Q) || GamepadBridge.ButtonDown(PadButton.RightStickPress)) r = -1;
            float ry = GamepadBridge.RightStick.y;
            int f = ry > 0.75f ? 1 : (ry < -0.75f ? -1 : 0);
            if (f != 0 && flickState == 0 && r == 0) r = f;
            if (Mathf.Abs(ry) < 0.35f) flickState = 0; else if (f != 0) flickState = f;
            shiftValue = r;
            return r;
        }

        /// <summary>Pressed this frame: keyboard binding or its gamepad button.</summary>
        public static bool ActionDown(GameAction a)
        {
            if (BoundDown(a)) return true;
            switch (a)
            {
                case GameAction.Reset: return GamepadBridge.ButtonDown(PadButton.Select);
                case GameAction.Camera: return GamepadBridge.ButtonDown(PadButton.North);
                case GameAction.Photo: return GamepadBridge.ButtonDown(PadButton.West);
                case GameAction.Replay: return GamepadBridge.ButtonDown(PadButton.LeftShoulder);
                case GameAction.Map: return GamepadBridge.ButtonDown(PadButton.DpadDown);
                default: return false;
            }
        }

        // ---- menu navigation (keyboard + gamepad) ----
        static float stickRepeat;
        static int stickDir;

        static int StickNav(bool vertical)
        {
            Vector2 v = GamepadBridge.LeftStick;
            float a = vertical ? v.y : v.x;
            int d = a > 0.6f ? 1 : (a < -0.6f ? -1 : 0);
            int key = (vertical ? 10 : 20) + d;
            if (d == 0) { if (stickDir / 10 == (vertical ? 1 : 2)) stickDir = 0; return 0; }
            if (stickDir != key) { stickDir = key; stickRepeat = Time.unscaledTime + 0.35f; return d; }
            if (Time.unscaledTime > stickRepeat) { stickRepeat = Time.unscaledTime + 0.12f; return d; }
            return 0;
        }

        public static bool MenuUp { get { return Down(KeyCode.UpArrow) || Down(KeyCode.W) || GamepadBridge.ButtonDown(PadButton.DpadUp) || StickNav(true) > 0; } }
        public static bool MenuDown { get { return Down(KeyCode.DownArrow) || Down(KeyCode.S) || GamepadBridge.ButtonDown(PadButton.DpadDown) || StickNav(true) < 0; } }
        public static bool MenuLeft { get { return Down(KeyCode.LeftArrow) || Down(KeyCode.A) || GamepadBridge.ButtonDown(PadButton.DpadLeft) || StickNav(false) < 0; } }
        public static bool MenuRight { get { return Down(KeyCode.RightArrow) || Down(KeyCode.D) || GamepadBridge.ButtonDown(PadButton.DpadRight) || StickNav(false) > 0; } }
        public static bool MenuConfirm { get { return Down(KeyCode.Return) || Down(KeyCode.KeypadEnter) || Down(KeyCode.Space) || GamepadBridge.ButtonDown(PadButton.South); } }
        public static bool MenuBack { get { return Down(KeyCode.Escape) || Down(KeyCode.Backspace) || GamepadBridge.ButtonDown(PadButton.East); } }
        public static bool PauseDown { get { return Down(KeyCode.Escape) || GamepadBridge.ButtonDown(PadButton.Start); } }

        // ---- rumble ----
        static float impulseLow, impulseHigh, impulseEnd, contLow, contHigh;
        static RumbleDriver driver;

        /// <summary>Short rumble burst (impacts, checkpoints, gear shifts).</summary>
        public static void Rumble(float low, float high, float seconds)
        {
            EnsureDriver();
            impulseLow = Mathf.Max(impulseLow, low);
            impulseHigh = Mathf.Max(impulseHigh, high);
            impulseEnd = Mathf.Max(impulseEnd, Time.unscaledTime + seconds);
        }

        /// <summary>Continuous rumble level (surface texture, drifting); call every frame.</summary>
        public static void RumbleContinuous(float low, float high)
        {
            EnsureDriver();
            contLow = low;
            contHigh = high;
        }

        static void EnsureDriver()
        {
            if (driver != null || !Application.isPlaying) return;
            var go = new GameObject("RumbleDriver");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            driver = go.AddComponent<RumbleDriver>();
        }

        class RumbleDriver : MonoBehaviour
        {
            float lastL = -1f, lastH = -1f;

            void LateUpdate()
            {
                if (Time.unscaledTime > impulseEnd) { impulseLow = impulseHigh = 0f; }
                float l = Mathf.Max(impulseLow, contLow), h = Mathf.Max(impulseHigh, contHigh);
                if (!GameSettings.Rumble || Time.timeScale < 0.01f) l = h = 0f;
                contLow = contHigh = 0f;   // continuous rumble must be refreshed every frame
                if (Mathf.Abs(l - lastL) > 0.02f || Mathf.Abs(h - lastH) > 0.02f)
                {
                    GamepadBridge.SetRumble(l, h);
                    lastL = l; lastH = h;
                }
            }

            void OnDisable() { GamepadBridge.SetRumble(0f, 0f); }
            void OnApplicationPause(bool p) { if (p) GamepadBridge.SetRumble(0f, 0f); }
        }
    }
}
