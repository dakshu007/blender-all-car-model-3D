// GamepadBridge.cs - gamepad (analogue steer, triggers, buttons, rumble) and keyboard access through
// the Input System package. Lives in its own assembly so the game still compiles when the package is
// missing or the new input backend is disabled: then every call returns "nothing pressed".
using UnityEngine;
#if BR_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace BlossomRally
{
    public enum PadButton { South, East, West, North, Start, Select, DpadUp, DpadDown, DpadLeft, DpadRight, LeftShoulder, RightShoulder, LeftStickPress, RightStickPress }

    public static class GamepadBridge
    {
#if BR_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
        public static bool Supported { get { return true; } }
        public static bool Connected { get { return Gamepad.current != null; } }
        public static bool KeyboardAvailable { get { return Keyboard.current != null; } }

        public static Vector2 LeftStick { get { var g = Gamepad.current; return g != null ? g.leftStick.ReadValue() : Vector2.zero; } }
        public static Vector2 RightStick { get { var g = Gamepad.current; return g != null ? g.rightStick.ReadValue() : Vector2.zero; } }
        public static float RightTrigger { get { var g = Gamepad.current; return g != null ? g.rightTrigger.ReadValue() : 0f; } }
        public static float LeftTrigger { get { var g = Gamepad.current; return g != null ? g.leftTrigger.ReadValue() : 0f; } }

        static ButtonControl Btn(Gamepad g, PadButton b)
        {
            switch (b)
            {
                case PadButton.South: return g.buttonSouth;
                case PadButton.East: return g.buttonEast;
                case PadButton.West: return g.buttonWest;
                case PadButton.North: return g.buttonNorth;
                case PadButton.Start: return g.startButton;
                case PadButton.Select: return g.selectButton;
                case PadButton.DpadUp: return g.dpad.up;
                case PadButton.DpadDown: return g.dpad.down;
                case PadButton.DpadLeft: return g.dpad.left;
                case PadButton.DpadRight: return g.dpad.right;
                case PadButton.LeftShoulder: return g.leftShoulder;
                case PadButton.RightShoulder: return g.rightShoulder;
                case PadButton.LeftStickPress: return g.leftStickButton;
                default: return g.rightStickButton;
            }
        }

        public static bool Button(PadButton b) { var g = Gamepad.current; return g != null && Btn(g, b).isPressed; }
        public static bool ButtonDown(PadButton b) { var g = Gamepad.current; return g != null && Btn(g, b).wasPressedThisFrame; }

        public static void SetRumble(float low, float high)
        {
            var g = Gamepad.current;
            if (g == null) return;
            if (low <= 0.001f && high <= 0.001f) g.ResetHaptics();
            else g.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
        }

        static bool ToKey(KeyCode k, out Key key)
        {
            key = Key.None;
            switch (k)
            {
                case KeyCode.Return: key = Key.Enter; return true;
                case KeyCode.KeypadEnter: key = Key.NumpadEnter; return true;
                case KeyCode.LeftControl: key = Key.LeftCtrl; return true;
                case KeyCode.RightControl: key = Key.RightCtrl; return true;
                case KeyCode.BackQuote: key = Key.Backquote; return true;
                case KeyCode.LeftCommand: key = Key.LeftCommand; return true;
                case KeyCode.RightCommand: key = Key.RightCommand; return true;
            }
            string n = k.ToString();
            if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
            else if (n.StartsWith("Keypad") && n.Length == 7) n = "Numpad" + n.Substring(6);
            return System.Enum.TryParse(n, out key) && key != Key.None;
        }

        public static bool KeyHeld(KeyCode k)
        {
            var kb = Keyboard.current;
            Key key;
            return kb != null && ToKey(k, out key) && kb[key].isPressed;
        }

        public static bool KeyDown(KeyCode k)
        {
            var kb = Keyboard.current;
            Key key;
            return kb != null && ToKey(k, out key) && kb[key].wasPressedThisFrame;
        }
#else
        public static bool Supported { get { return false; } }
        public static bool Connected { get { return false; } }
        public static bool KeyboardAvailable { get { return false; } }
        public static Vector2 LeftStick { get { return Vector2.zero; } }
        public static Vector2 RightStick { get { return Vector2.zero; } }
        public static float RightTrigger { get { return 0f; } }
        public static float LeftTrigger { get { return 0f; } }
        public static bool Button(PadButton b) { return false; }
        public static bool ButtonDown(PadButton b) { return false; }
        public static void SetRumble(float low, float high) { }
        public static bool KeyHeld(KeyCode k) { return false; }
        public static bool KeyDown(KeyCode k) { return false; }
#endif
    }
}
