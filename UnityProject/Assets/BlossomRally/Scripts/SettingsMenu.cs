// SettingsMenu.cs - graphics, audio and controls settings (quality preset, grass density,
// resolution, fullscreen, vsync, FPS counter, volumes, rumble, keyboard remapping).
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public class SettingsMenu : MonoBehaviour
    {
        public static SettingsMenu Instance { get; private set; }
        public bool IsOpen { get; private set; }

        MenuList main, controls, effects, driving;
        MenuList current;
        System.Action onClose;
        GameAction? capturing;
        int captureFrame;
        readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        int resIndex;

        static readonly GameAction[] Remappable =
            { GameAction.Throttle, GameAction.Brake, GameAction.SteerLeft, GameAction.SteerRight, GameAction.Handbrake,
              GameAction.Reset, GameAction.Camera, GameAction.Replay, GameAction.Photo };

        void Awake()
        {
            Instance = this;
            main = new MenuList { title = "SETTINGS", width = 600f };
            main.Add("Quality", null, () => GameSettings.QualityNames[GameSettings.Quality], d => GameSettings.SetQuality(GameSettings.Quality + d));
            main.Add("Grass density", null, () => Mathf.RoundToInt(GameSettings.GrassDensity * 100f) + "%", d => GameSettings.SetGrassDensity(GameSettings.GrassDensity + d * 0.1f));
            main.Add("Resolution", ApplyResolution, () => resolutions.Count > 0 ? resolutions[resIndex].x + " × " + resolutions[resIndex].y : Screen.width + " × " + Screen.height,
                     d => { if (resolutions.Count > 0) resIndex = (resIndex + d + resolutions.Count) % resolutions.Count; });
            main.Add("Fullscreen", () => Screen.fullScreen = !Screen.fullScreen, () => Screen.fullScreen ? "On" : "Off", d => Screen.fullScreen = !Screen.fullScreen);
            main.Add("V-Sync", null, () => GameSettings.VSync ? "On" : "Off", d => GameSettings.SetVSync(!GameSettings.VSync));
            main.Add("FPS counter", null, () => GameSettings.ShowFps ? "On" : "Off", d => GameSettings.ShowFps = !GameSettings.ShowFps);
            main.Add("Master volume", null, () => Pct(GameSettings.MasterVolume), d => GameSettings.SetMasterVolume(GameSettings.MasterVolume + d * 0.1f));
            main.Add("Music volume", null, () => Pct(GameSettings.MusicVolume), d => GameSettings.SetMusicVolume(GameSettings.MusicVolume + d * 0.1f));
            main.Add("Effects volume", null, () => Pct(GameSettings.SfxVolume), d => GameSettings.SetSfxVolume(GameSettings.SfxVolume + d * 0.1f));
            main.Add("Gamepad rumble", null, () => GameSettings.Rumble ? "On" : "Off", d => GameSettings.SetRumble(!GameSettings.Rumble));
            main.Add("Driving & assists...", () => { current = driving; driving.index = 0; });
            main.Add("Visual effects...", () => { current = effects; effects.index = 0; });
            main.Add("Keyboard controls...", () => { current = controls; controls.index = 0; });
            main.Add("Back", Close);

            driving = new MenuList { title = "DRIVING", width = 640f, footer = "← → change   ESC back" };
            driving.Add("Handling", null, () => DriveSettings.LevelNames[(int)DriveSettings.Level], d => DriveSettings.CycleLevel(d));
            driving.Add("Traction control", null, () => OnOff(DriveSettings.Tcs), d => DriveSettings.SetTcs(!DriveSettings.Tcs));
            driving.Add("Anti-lock brakes", null, () => OnOff(DriveSettings.Abs), d => DriveSettings.SetAbs(!DriveSettings.Abs));
            driving.Add("Stability help", null, () => OnOff(DriveSettings.Stability), d => DriveSettings.SetStability(!DriveSettings.Stability));
            driving.Add("Gearbox", null, () => DriveSettings.ManualGears ? "Manual" : "Automatic", d => DriveSettings.SetManualGears(!DriveSettings.ManualGears));
            driving.Add("Back", () => { current = main; });

            effects = new MenuList { title = "VISUAL EFFECTS", width = 640f, footer = "← → change   ESC back" };
            effects.Add("Enhanced graphics", null, () => OnOff(GraphicsFX.Enhanced), d => GraphicsFX.Enhanced = !GraphicsFX.Enhanced);
            effects.Add("Ink & watercolour mode", null, () => OnOff(PostFX.InkMode), d => PostFX.InkMode = !PostFX.InkMode);
            effects.Add("Motion blur", null, () => OnOff(PostFX.MotionBlur), d => PostFX.MotionBlur = !PostFX.MotionBlur);
            effects.Add("Lens effects", null, () => OnOff(PostFX.LensEffects), d => PostFX.LensEffects = !PostFX.LensEffects);
            effects.Add("Slow-motion moments", null, () => OnOff(JuiceSettings.SlowMoMoments), d => JuiceSettings.SlowMoMoments = !JuiceSettings.SlowMoMoments);
            effects.Add("Score pop-ups", null, () => OnOff(JuiceSettings.ScorePopups), d => JuiceSettings.ScorePopups = !JuiceSettings.ScorePopups);
            effects.Add("Reduced camera motion", null, () => OnOff(JuiceSettings.ReducedMotion), d => JuiceSettings.ReducedMotion = !JuiceSettings.ReducedMotion);
            effects.Add("Back", () => { current = main; });

            controls = new MenuList { title = "CONTROLS", width = 600f, footer = "ENTER rebind   ESC back   (gamepad: RT/LT/stick, A handbrake, Start pause)" };
            foreach (var a in Remappable)
            {
                var act = a;
                controls.Add(Nice(act), () => { capturing = act; captureFrame = Time.frameCount; }, () => capturing == act ? "press a key..." : GameSettings.Binding(act).ToString());
            }
            controls.Add("Reset to defaults", GameSettings.ResetBindings);
            controls.Add("Back", () => { current = main; });
        }

        static string OnOff(bool v) { return v ? "On" : "Off"; }

        static string Pct(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }

        static string Nice(GameAction a)
        {
            switch (a)
            {
                case GameAction.SteerLeft: return "Steer left";
                case GameAction.SteerRight: return "Steer right";
                case GameAction.Reset: return "Reset to road";
                case GameAction.Replay: return "Instant replay";
                case GameAction.Photo: return "Photo mode";
                default: return a.ToString();
            }
        }

        public void Open(System.Action closed)
        {
            onClose = closed;
            IsOpen = true;
            current = main;
            main.index = 0;
            capturing = null;
            resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(v) && r.width >= 960) resolutions.Add(v);
            }
            resIndex = 0;
            for (int i = 0; i < resolutions.Count; i++) if (resolutions[i].x == Screen.width && resolutions[i].y == Screen.height) resIndex = i;
        }

        void ApplyResolution()
        {
            if (resolutions.Count == 0) return;
            var r = resolutions[resIndex];
            Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
        }

        public void Close()
        {
            IsOpen = false;
            capturing = null;
            var cb = onClose;
            onClose = null;
            if (cb != null) cb();
        }

        void Update()
        {
            if (!IsOpen || capturing.HasValue) return;
            if (current.HandleInput())
            {
                if (current != main) current = main;
                else Close();
            }
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            GUI.depth = -20;
            if (capturing.HasValue)
            {
                var e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && Time.frameCount > captureFrame)
                {
                    if (e.keyCode != KeyCode.Escape) GameSettings.SetBinding(capturing.Value, e.keyCode);
                    capturing = null;
                    UI.Sound("MenuSelect");
                    e.Use();
                }
            }
            UI.Panel(new Rect(0, 0, Screen.width, Screen.height), 0.3f);
            current.Draw(Screen.width * 0.5f, Screen.height * 0.5f);
        }
    }
}
