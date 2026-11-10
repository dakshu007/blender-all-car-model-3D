// DriveSettings.cs - driving assists, persisted in PlayerPrefs.
//
//   Assist level   Arcade    - very forgiving: strong counter-steer, drift hold and spin protection, wide drift window
//                  Balanced  - default: natural physics with subtle help (counter-steer, light drift stabiliser)
//                  Sim       - minimal help: you counter-steer and hold the angle yourself
//   Toggles        TCS, ABS, Stability, Manual gears (choosing a level resets TCS/ABS/Stability to its defaults)
//
// Everything reads the numbers through DriveSettings.Profile, so a change applies on the next physics step.
using UnityEngine;

namespace BlossomRally
{
    public enum AssistLevel { Arcade = 0, Balanced = 1, Sim = 2 }

    /// <summary>The numbers one assist level (plus toggles) stands for. Read by VehicleDynamics every step.</summary>
    public struct AssistProfile
    {
        public float counterSteer;     // 0..1: front wheels follow the front axle's direction of travel (1 = hands-off caster)
        public float driftAssist;      // 0..1: yaw stabiliser that holds the drift angle the inputs ask for
        public float driftWindow;      // deg: beyond this the stability assist stops the rotation (spin protection)
        public float stability;        // 0..1: spin protection strength (0 when Stability is off)
        public float airControl;       // 0..1: auto-levelling + steer/pitch authority in the air
        public float lockHighSpeed;    // deg of front slip that full lock asks for at speed (speed-sensitive steering)
        public float tcsSlip;          // driven-wheel slip ratio the traction control allows (0 = off)
        public bool abs;
        public float rollGuard;        // 0..1 anti-roll-over help on kerbs and bumps
        public float slideKeep;        // m/s^2 of forward push while drifting on throttle (momentum keeper)
        public float slipCap;          // max front slip asked for, in multiples of the tyre's peak (0 = no cap)
        public float hbAssist;         // 0..1 extra pivot when the handbrake is pulled with steering
    }

    public static class DriveSettings
    {
        public static readonly string[] LevelNames = { "Arcade", "Balanced", "Sim" };

        public static AssistLevel Level { get; private set; } = AssistLevel.Balanced;
        public static bool Tcs { get; private set; } = true;
        public static bool Abs { get; private set; } = true;
        public static bool Stability { get; private set; } = true;
        public static bool ManualGears { get; private set; }

        public static event System.Action Changed;
        static bool loaded;
        static AssistProfile profile;

        static int GetI(string k, int d) { try { return PlayerPrefs.GetInt(k, d); } catch (System.Exception) { return d; } }
        static void SetI(string k, int v) { try { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); } catch (System.Exception) { } }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Level = (AssistLevel)Mathf.Clamp(GetI("BR_Assist", (int)AssistLevel.Balanced), 0, 2);
            DefaultToggles(Level, out bool tcs, out bool abs, out bool st);
            Tcs = GetI("BR_TCS", tcs ? 1 : 0) == 1;
            Abs = GetI("BR_ABS", abs ? 1 : 0) == 1;
            Stability = GetI("BR_Stability", st ? 1 : 0) == 1;
            ManualGears = GetI("BR_Manual", 0) == 1;
            Rebuild();
        }

        static void DefaultToggles(AssistLevel l, out bool tcs, out bool abs, out bool st)
        {
            tcs = l != AssistLevel.Sim;
            abs = l != AssistLevel.Sim;
            st = l != AssistLevel.Sim;
        }

        /// <summary>Choose an assist level; TCS / ABS / Stability go back to that level's defaults.</summary>
        public static void SetLevel(AssistLevel l)
        {
            Load();
            Level = l;
            DefaultToggles(l, out bool tcs, out bool abs, out bool st);
            Tcs = tcs; Abs = abs; Stability = st;
            SetI("BR_Assist", (int)l);
            SetI("BR_TCS", tcs ? 1 : 0); SetI("BR_ABS", abs ? 1 : 0); SetI("BR_Stability", st ? 1 : 0);
            Rebuild();
        }

        /// <summary>Cycle Arcade / Balanced / Sim by d (for a menu row's left/right).</summary>
        public static void CycleLevel(int d) { Load(); SetLevel((AssistLevel)(((int)Level + d % 3 + 3) % 3)); }

        public static void SetTcs(bool v) { Load(); Tcs = v; SetI("BR_TCS", v ? 1 : 0); Rebuild(); }
        public static void SetAbs(bool v) { Load(); Abs = v; SetI("BR_ABS", v ? 1 : 0); Rebuild(); }
        public static void SetStability(bool v) { Load(); Stability = v; SetI("BR_Stability", v ? 1 : 0); Rebuild(); }
        public static void SetManualGears(bool v) { Load(); ManualGears = v; SetI("BR_Manual", v ? 1 : 0); Rebuild(); }

        public static AssistProfile Profile { get { Load(); return profile; } }

        static void Rebuild()
        {
            var p = new AssistProfile();
            switch (Level)
            {
                case AssistLevel.Arcade:
                    p.counterSteer = 1.0f; p.driftAssist = 1.0f; p.driftWindow = 55f; p.airControl = 1f;
                    p.lockHighSpeed = 8.2f; p.rollGuard = 1f; p.slideKeep = 1.6f; p.hbAssist = 1f; p.slipCap = 1.5f;
                    break;
                case AssistLevel.Balanced:
                    p.counterSteer = 0.82f; p.driftAssist = 0.45f; p.driftWindow = 48f; p.airControl = 0.65f;
                    p.lockHighSpeed = 8.8f; p.rollGuard = 0.8f; p.slideKeep = 0.6f; p.hbAssist = 0.85f; p.slipCap = 1.7f;
                    break;
                default:
                    p.counterSteer = 0.5f; p.driftAssist = 0f; p.driftWindow = 75f; p.airControl = 0.3f;
                    p.lockHighSpeed = 14f; p.rollGuard = 0.5f; p.slideKeep = 0f; p.hbAssist = 0f; p.slipCap = 0f;
                    break;
            }
            p.stability = Stability ? (Level == AssistLevel.Arcade ? 1f : Level == AssistLevel.Balanced ? 0.7f : 0.5f) : 0f;
            p.tcsSlip = Tcs ? (Level == AssistLevel.Arcade ? 0.14f : Level == AssistLevel.Balanced ? 0.24f : 0.3f) : 0f;
            p.abs = Abs;
            profile = p;
            if (Changed != null) Changed();
        }

        /// <summary>Short label for a HUD / menu ("Balanced · TCS ABS ESP").</summary>
        public static string Summary()
        {
            Load();
            return LevelNames[(int)Level] + (Tcs ? " · TCS" : "") + (Abs ? " · ABS" : "") + (Stability ? " · Stability" : "") + (ManualGears ? " · Manual" : "");
        }
    }
}
