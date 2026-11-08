// InputReader.cs - keyboard (and optional gamepad axes) via the legacy Input Manager.
// The editor setup script enables the legacy Input Manager ("Both") if a project only has the
// new Input System active; until then this reports Legacy = false and the HUD explains it.
using UnityEngine;

namespace BlossomRally
{
    public static class InputReader
    {
        static bool axesOk = true;

        public static bool Legacy
        {
            get
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return true;
#else
                return false;
#endif
            }
        }

        public static bool Key(KeyCode k)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(k);
#else
            return false;
#endif
        }

        public static bool Down(KeyCode k)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(k);
#else
            return false;
#endif
        }

        static float Axis(string name)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!axesOk) return 0f;
            try { return Input.GetAxisRaw(name); }
            catch (System.ArgumentException) { axesOk = false; return 0f; }
#else
            return 0f;
#endif
        }

        public static float Throttle
        {
            get
            {
                float k = (Key(KeyCode.W) || Key(KeyCode.UpArrow)) ? 1f : 0f;
                return Mathf.Max(k, Mathf.Clamp01(Axis("Vertical") > 0.2f ? Axis("Vertical") : 0f));
            }
        }

        public static float Brake
        {
            get
            {
                float k = (Key(KeyCode.S) || Key(KeyCode.DownArrow)) ? 1f : 0f;
                return Mathf.Max(k, Mathf.Clamp01(Axis("Vertical") < -0.2f ? -Axis("Vertical") : 0f));
            }
        }

        public static float Steer
        {
            get
            {
                float k = 0f;
                if (Key(KeyCode.A) || Key(KeyCode.LeftArrow)) k -= 1f;
                if (Key(KeyCode.D) || Key(KeyCode.RightArrow)) k += 1f;
                if (Mathf.Abs(k) > 0f) return k;
                float a = Axis("Horizontal");
                return Mathf.Abs(a) > 0.15f ? a : 0f;
            }
        }

        public static bool Handbrake
        {
            get { return Key(KeyCode.Space) || Key(KeyCode.JoystickButton0); }
        }
    }
}
