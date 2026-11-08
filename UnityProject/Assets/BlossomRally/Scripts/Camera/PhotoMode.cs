// PhotoMode.cs - freeze the world and fly a free camera around the car to take pictures.
// Keyboard: WASD / arrows fly, Q / E down / up, SHIFT faster (CTRL slower), hold right mouse to look,
// mouse wheel or Z / X zoom (15-90°), R / F roll, TAB options panel, H hide everything,
// ENTER / SPACE take a picture, ESC or the Photo key exit.
// Gamepad: left stick move, right stick look, LT / RT down / up, d-pad zoom / roll (panel: navigate),
// Y options panel, Select hide UI, A take a picture, B exit.
// Options: filters, exposure, saturation, vignette, depth of field (auto-focus on the car), aperture,
// letterbox bars and a "Blossom Rally" watermark - all rendered by PostFX, so they end up in the file.
using System.Collections;
using System.IO;
using UnityEngine;

namespace BlossomRally
{
    public class PhotoMode : MonoBehaviour
    {
        public static PhotoMode Instance { get; private set; }
        /// <summary>Sound hook: "PhotoShutter" (picture taken), "Camera" (enter), "Whoosh" (exit).</summary>
        public static System.Action<string> OnSound;
        /// <summary>Full path of the last picture saved this session (null if none).</summary>
        public static string LastSavedPath { get; private set; }

        public const int SuperSize = 2;
        const float MaxDistance = 60f;
        const float MinFov = 15f, MaxFov = 90f;

        public bool Active { get; private set; }
        /// <summary>True while active and for a frame after exiting: GameManager should skip its own
        /// driving keys (ESC, TAB, H, R...) while this is set.</summary>
        public static bool BlockingInput
        {
            get { return Instance != null && (Instance.Active || Time.frameCount - Instance.toggleFrame <= 1); }
        }

        // saved on Enter
        float prevTimeScale = 1f;
        bool prevAudioPause, prevEffect, prevControls;
        Vector3 prevCamPos;
        Quaternion prevCamRot;
        float prevFov;
        float pThrottle, pBrake, pSteer, pHandbrake;
        int toggleFrame = -10;

        Camera cam;
        CameraRig rig;
        PostFX fx;
        ArcadeCar car;

        // free camera
        Vector3 pos, moveVel;
        float yaw, pitch, roll, fov = 60f;
        Vector2 mouseDelta;
        float wheelDelta;
        readonly RaycastHit[] hits = new RaycastHit[12];

        // options (kept between photo sessions)
        enum Row { Filter, Exposure, Saturation, Vignette, Dof, AutoFocus, Focus, Aperture, Letterbox, Logo, Fov, Reset }
        static readonly string[] RowNames = { "Filter", "Exposure", "Saturation", "Vignette", "Depth of field", "Auto focus", "Focus distance", "Aperture", "Letterbox", "Logo", "Field of view", "Reset all" };
        static readonly float[] Apertures = { 1.2f, 1.4f, 2f, 2.8f, 4f, 5.6f, 8f, 11f, 16f };
        static readonly string[] LetterboxNames = { "Off", "1.85 : 1", "2.39 : 1" };
        static readonly float[] LetterboxAspect = { 0f, 1.85f, 2.39f };
        int filter;
        float ev, sat = 1f, vig = -1f;
        bool dof, autoFocus = true;
        float focusDist = 10f;
        int apIdx = 3, lbIdx;
        bool logo;

        // UI
        bool panelOpen = true, hideAll;
        int row;
        float repeatAt;
        int repeatDir;
        bool hideForCapture, capturing;
        float flash;
        string toast = "";
        float toastTimer;
        GUIStyle sTitle, sRow, sSmall, sToast;
        static Texture2D logoTex;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Active) Exit();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static void Sound(string id)
        {
            if (OnSound != null) OnSound(id);
        }

        // ------------------------------------------------------------------ enter / exit
        /// <summary>Freeze time and take over the camera (does nothing while a replay is running).</summary>
        public void Enter()
        {
            if (Active) return;
            if (ReplaySystem.Instance != null && ReplaySystem.Instance.Active) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Rig == null || gm.Car == null) return;
            rig = gm.Rig;
            cam = rig.GetComponent<Camera>();
            if (cam == null) return;
            fx = rig.GetComponent<PostFX>();
            car = gm.Car;

            prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            prevAudioPause = AudioListener.pause;
            AudioListener.pause = true;
            prevCamPos = cam.transform.position;
            prevCamRot = cam.transform.rotation;
            prevFov = cam.fieldOfView;
            rig.externalControl = true;
            GameManager.HideHud = true;
            prevControls = car.controlsEnabled;
            pThrottle = car.throttle; pBrake = car.brake; pSteer = car.steerInput; pHandbrake = car.handbrake;
            car.controlsEnabled = false;
            if (fx != null)
            {
                prevEffect = fx.effectEnabled;
                if (vig < 0f) vig = fx.vignette;
            }
            else if (vig < 0f) vig = 0.9f;

            pos = prevCamPos;
            Vector3 e = prevCamRot.eulerAngles;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -89f, 89f);
            yaw = e.y;
            roll = 0f;
            fov = Mathf.Clamp(prevFov, MinFov, MaxFov);
            moveVel = Vector3.zero;
            mouseDelta = Vector2.zero;
            wheelDelta = 0f;
            hideAll = false;
            hideForCapture = capturing = false;
            flash = 0f;
            toastTimer = 0f;
            repeatDir = 0;
            Active = true;
            toggleFrame = Time.frameCount;
            ApplyCamera();
            PushEffects();
            Sound("Camera");
        }

        /// <summary>Leave photo mode: time, camera, HUD and post effects go back to normal.</summary>
        public void Exit()
        {
            if (!Active) return;
            Active = false;
            toggleFrame = Time.frameCount;
            StopAllCoroutines();
            hideForCapture = capturing = false;
            Time.timeScale = prevTimeScale;
            AudioListener.pause = prevAudioPause;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(prevCamPos, prevCamRot);
                cam.fieldOfView = prevFov;
            }
            if (fx != null)
            {
                fx.ResetPhoto();
                fx.effectEnabled = prevEffect;
            }
            if (car != null)
            {
                car.throttle = pThrottle; car.brake = pBrake; car.steerInput = pSteer; car.handbrake = pHandbrake;
                car.controlsEnabled = prevControls;
            }
            if (rig != null) rig.externalControl = false;
            GameManager.HideHud = false;
            Sound("Whoosh");
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            if (!Active) return;
            var gm = GameManager.Instance;
            if (car == null || cam == null || rig == null || gm == null || gm.Car != car) { Exit(); return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (Time.frameCount != toggleFrame && !capturing)
            {
                if (InputReader.Down(KeyCode.Escape) || GamepadBridge.ButtonDown(PadButton.East) || InputReader.ActionDown(GameAction.Photo))
                {
                    Exit();
                    return;
                }
                if (InputReader.Down(KeyCode.Return) || InputReader.Down(KeyCode.KeypadEnter) || InputReader.Down(KeyCode.Space) ||
                    GamepadBridge.ButtonDown(PadButton.South))
                    StartCoroutine(Capture());
                if (InputReader.Down(KeyCode.Tab) || GamepadBridge.ButtonDown(PadButton.North)) { panelOpen = !panelOpen; if (panelOpen) hideAll = false; }
                if (InputReader.Down(KeyCode.H) || GamepadBridge.ButtonDown(PadButton.Select)) hideAll = !hideAll;
                if (PanelInput) PanelNavigation();
            }
            Fly(dt);
            ApplyCamera();
            PushEffects();
            if (flash > 0f) flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 3f);
            if (toastTimer > 0f) toastTimer -= Time.unscaledDeltaTime;
        }

        bool PanelInput { get { return panelOpen && !hideAll; } }

        void Fly(float dt)
        {
            bool arrows = !PanelInput;          // with the panel open the arrows / d-pad drive the panel
            Vector3 input = Vector3.zero;
            if (InputReader.Key(KeyCode.W) || (arrows && InputReader.Key(KeyCode.UpArrow))) input.z += 1f;
            if (InputReader.Key(KeyCode.S) || (arrows && InputReader.Key(KeyCode.DownArrow))) input.z -= 1f;
            if (InputReader.Key(KeyCode.D) || (arrows && InputReader.Key(KeyCode.RightArrow))) input.x += 1f;
            if (InputReader.Key(KeyCode.A) || (arrows && InputReader.Key(KeyCode.LeftArrow))) input.x -= 1f;
            if (InputReader.Key(KeyCode.E)) input.y += 1f;
            if (InputReader.Key(KeyCode.Q)) input.y -= 1f;
            Vector2 ls = GamepadBridge.LeftStick;
            if (ls.magnitude > 0.15f) { input.x += ls.x; input.z += ls.y; }
            input.y += GamepadBridge.RightTrigger - GamepadBridge.LeftTrigger;
            input = Vector3.ClampMagnitude(input, 1f);
            float speed = 7f;
            if (InputReader.Key(KeyCode.LeftShift) || InputReader.Key(KeyCode.RightShift) || GamepadBridge.Button(PadButton.LeftStickPress)) speed *= 4f;
            if (InputReader.Key(KeyCode.LeftControl) || InputReader.Key(KeyCode.RightControl)) speed *= 0.25f;
            Quaternion moveRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 want = (moveRot * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y) * speed;
            moveVel = Vector3.Lerp(moveVel, want, 1f - Mathf.Exp(-dt * 8f));
            pos += moveVel * dt;

            // look: right mouse drag (collected in OnGUI, works with every input backend) + right stick
            float sens = 0.15f * Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(MinFov, 60f, fov));
            yaw += mouseDelta.x * sens;
            pitch += mouseDelta.y * sens;
            mouseDelta = Vector2.zero;
            Vector2 rs = GamepadBridge.RightStick;
            if (rs.magnitude > 0.15f)
            {
                float ps = Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(MinFov, 60f, fov));
                yaw += rs.x * 110f * ps * dt;
                pitch -= rs.y * 80f * ps * dt;
            }
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            yaw = Mathf.Repeat(yaw, 360f);

            // roll and zoom
            if (InputReader.Key(KeyCode.R)) roll += 35f * dt;
            if (InputReader.Key(KeyCode.F)) roll -= 35f * dt;
            if (InputReader.Key(KeyCode.Z)) fov -= 30f * dt;
            if (InputReader.Key(KeyCode.X)) fov += 30f * dt;
            if (!PanelInput)
            {
                if (GamepadBridge.Button(PadButton.DpadLeft)) roll += 35f * dt;
                if (GamepadBridge.Button(PadButton.DpadRight)) roll -= 35f * dt;
                if (GamepadBridge.Button(PadButton.DpadUp)) fov -= 30f * dt;
                if (GamepadBridge.Button(PadButton.DpadDown)) fov += 30f * dt;
            }
            fov += wheelDelta * 1.5f;
            wheelDelta = 0f;
            roll = Mathf.Clamp(roll, -45f, 45f);
            fov = Mathf.Clamp(fov, MinFov, MaxFov);

            Constrain();
        }

        // stay within reach of the car and never go underground
        void Constrain()
        {
            Vector3 c = car.transform.position;
            Vector3 off = pos - c;
            if (off.magnitude > MaxDistance) pos = c + off.normalized * MaxDistance;
            int mask = Physics.DefaultRaycastLayers & ~((1 << WorldBuilder.LayerGrass) | (1 << WorldBuilder.LayerTrees));
            int n = Physics.RaycastNonAlloc(pos + Vector3.up * 3f, Vector3.down, hits, 400f, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float groundY = float.NegativeInfinity;
            Rigidbody body = car.Body;
            for (int i = 0; i < n; i++)
            {
                if (body != null && hits[i].rigidbody == body) continue;
                if (hits[i].distance < best) { best = hits[i].distance; groundY = hits[i].point.y; }
            }
            if (groundY > float.NegativeInfinity && pos.y < groundY + 0.3f)
            {
                pos.y = groundY + 0.3f;
                if (moveVel.y < 0f) moveVel.y = 0f;
            }
        }

        void ApplyCamera()
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, roll));
            cam.fieldOfView = fov;
            if (autoFocus && car != null)
            {
                Vector3 target = car.transform.position + Vector3.up * 0.6f;
                float d = Vector3.Dot(target - pos, cam.transform.forward);
                focusDist = Mathf.Clamp(d > 0.5f ? d : (target - pos).magnitude, 0.5f, 300f);
            }
        }

        void PushEffects()
        {
            if (fx == null) return;
            fx.effectEnabled = true;
            fx.filter = filter;
            fx.exposureEV = ev;
            fx.saturationMul = sat;
            fx.vignetteAdd = vig - fx.vignette;
            fx.dof = dof;
            fx.focusDistance = focusDist;
            fx.aperture = Apertures[apIdx];
            float lb = 0f;
            if (LetterboxAspect[lbIdx] > 0f)
            {
                float aspect = cam.aspect;
                if (aspect < LetterboxAspect[lbIdx]) lb = (1f - aspect / LetterboxAspect[lbIdx]) * 0.5f;
            }
            fx.letterbox = lb;
            fx.showLogo = logo;
            fx.logoTexture = logo ? LogoTexture : null;
        }

        // ------------------------------------------------------------------ options panel
        void PanelNavigation()
        {
            if (InputReader.Down(KeyCode.UpArrow) || GamepadBridge.ButtonDown(PadButton.DpadUp)) row = (row + RowNames.Length - 1) % RowNames.Length;
            if (InputReader.Down(KeyCode.DownArrow) || GamepadBridge.ButtonDown(PadButton.DpadDown)) row = (row + 1) % RowNames.Length;
            int dir = 0;
            bool left = InputReader.Key(KeyCode.LeftArrow) || GamepadBridge.Button(PadButton.DpadLeft);
            bool right = InputReader.Key(KeyCode.RightArrow) || GamepadBridge.Button(PadButton.DpadRight);
            if (InputReader.Down(KeyCode.LeftArrow) || GamepadBridge.ButtonDown(PadButton.DpadLeft)) { dir = -1; repeatAt = Time.unscaledTime + 0.35f; }
            else if (InputReader.Down(KeyCode.RightArrow) || GamepadBridge.ButtonDown(PadButton.DpadRight)) { dir = 1; repeatAt = Time.unscaledTime + 0.35f; }
            else if ((left || right) && Time.unscaledTime > repeatAt && Continuous((Row)row))
            {
                dir = left ? -1 : 1;
                repeatAt = Time.unscaledTime + 0.05f;
            }
            if (dir != 0) Change((Row)row, dir);
        }

        static bool Continuous(Row r)
        {
            return r == Row.Exposure || r == Row.Saturation || r == Row.Vignette || r == Row.Focus || r == Row.Fov;
        }

        void Change(Row r, int dir)
        {
            switch (r)
            {
                case Row.Filter: filter = (filter + dir + PostFX.FilterNames.Length) % PostFX.FilterNames.Length; break;
                case Row.Exposure: ev = Mathf.Clamp(Mathf.Round((ev + dir * 0.1f) * 10f) / 10f, -2f, 2f); break;
                case Row.Saturation: sat = Mathf.Clamp(Mathf.Round((sat + dir * 0.05f) * 20f) / 20f, 0f, 2f); break;
                case Row.Vignette: vig = Mathf.Clamp(Mathf.Round((vig + dir * 0.1f) * 10f) / 10f, 0f, 2.5f); break;
                case Row.Dof: dof = !dof; break;
                case Row.AutoFocus: autoFocus = !autoFocus; break;
                case Row.Focus: autoFocus = false; focusDist = Mathf.Clamp(focusDist * (dir > 0 ? 1.06f : 1f / 1.06f), 0.5f, 300f); break;
                case Row.Aperture: apIdx = Mathf.Clamp(apIdx + dir, 0, Apertures.Length - 1); break;
                case Row.Letterbox: lbIdx = (lbIdx + dir + LetterboxNames.Length) % LetterboxNames.Length; break;
                case Row.Logo: logo = !logo; break;
                case Row.Fov: fov = Mathf.Clamp(fov + dir * 1f, MinFov, MaxFov); break;
                case Row.Reset: ResetOptions(); break;
            }
        }

        void ResetOptions()
        {
            filter = 0; ev = 0f; sat = 1f; vig = fx != null ? fx.vignette : 0.9f;
            dof = false; autoFocus = true; apIdx = 3; lbIdx = 0; logo = false;
            roll = 0f;
            fov = Mathf.Clamp(prevFov, MinFov, MaxFov);
        }

        string Value(Row r)
        {
            switch (r)
            {
                case Row.Filter: return PostFX.FilterNames[filter];
                case Row.Exposure: return (ev >= 0f ? "+" : "") + ev.ToString("0.0") + " EV";
                case Row.Saturation: return Mathf.RoundToInt(sat * 100f) + " %";
                case Row.Vignette: return vig.ToString("0.0");
                case Row.Dof: return dof ? "On" : "Off";
                case Row.AutoFocus: return autoFocus ? "On (car)" : "Off";
                case Row.Focus: return focusDist < 10f ? focusDist.ToString("0.0") + " m" : Mathf.RoundToInt(focusDist) + " m";
                case Row.Aperture: return "f/" + Apertures[apIdx].ToString("0.#");
                case Row.Letterbox: return LetterboxNames[lbIdx];
                case Row.Logo: return logo ? "On" : "Off";
                case Row.Fov: return Mathf.RoundToInt(fov) + "°";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ capture
        IEnumerator Capture()
        {
            capturing = true;
            hideForCapture = true;
            string path = null, error = null;
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "Screenshots");
                Directory.CreateDirectory(dir);
                string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                path = Path.Combine(dir, "BlossomRally_" + stamp + ".png");
                for (int n = 2; File.Exists(path) && n < 100; n++) path = Path.Combine(dir, "BlossomRally_" + stamp + "_" + n + ".png");
                ScreenCapture.CaptureScreenshot(path, SuperSize);
            }
            catch (System.Exception ex)
            {
                error = ex.Message;
                path = null;
            }
            Sound("PhotoShutter");
            // the shot is grabbed at the end of this frame: keep the UI hidden until it has been taken
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return null;
            hideForCapture = false;
            capturing = false;
            flash = 1f;
            if (path != null)
            {
                LastSavedPath = path;
                ShowToast("Saved  " + path);
            }
            else ShowToast("Could not save the photo: " + error);
        }

        void ShowToast(string s)
        {
            toast = s;
            toastTimer = 4f;
        }

        // ------------------------------------------------------------------ watermark texture
        /// <summary>Procedural "Blossom Rally" logo (blossom + rounded pixel lettering), straight alpha, sRGB values.</summary>
        public static Texture2D LogoTexture
        {
            get
            {
                if (logoTex == null) logoTex = BuildLogo();
                return logoTex;
            }
        }

        static readonly string[] GlyphChars = { "B", "L", "O", "S", "M", "R", "A", "Y" };
        static readonly string[][] Glyphs =
        {
            new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" },
            new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
            new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" },
            new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
            new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" },
            new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
            new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
            new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
        };

        static Texture2D BuildLogo()
        {
            const string text = "BLOSSOM RALLY";
            const int S = 8;                         // pixels per glyph cell
            const int flowerCells = 9;
            int textCells = 0;
            foreach (char ch in text) textCells += ch == ' ' ? 3 : 6;
            int gw = 1 + flowerCells + 1 + textCells + 1, gh = 9;
            var grid = new bool[gw, gh];
            int cx = 1 + flowerCells + 1;
            foreach (char ch in text)
            {
                if (ch == ' ') { cx += 3; continue; }
                int gi = System.Array.IndexOf(GlyphChars, ch.ToString());
                if (gi >= 0)
                    for (int r = 0; r < 7; r++)
                        for (int c = 0; c < 5; c++)
                            if (Glyphs[gi][r][c] == '1') grid[cx + c, 1 + (6 - r)] = true;
                cx += 6;
            }
            int W = gw * S, H = gh * S;
            var px = new Color32[W * H];
            Color pink = new Color(1f, 0.72f, 0.82f), white = new Color(1f, 0.98f, 0.97f), plum = new Color(0.22f, 0.10f, 0.17f);
            Color petal = new Color(1f, 0.80f, 0.88f), heart = new Color(1f, 0.86f, 0.45f);
            Vector2 fc = new Vector2(1f + flowerCells * 0.5f, gh * 0.5f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                    float dText = TextDist(grid, gw, gh, p);
                    float dShadow = TextDist(grid, gw, gh, p + new Vector2(-0.18f, 0.22f));
                    float dFlower = FlowerDist(p - fc, out bool isHeart);
                    float dFlowerShadow = FlowerDist(p - fc + new Vector2(-0.18f, 0.22f), out bool _);
                    float d = Mathf.Min(dText, dFlower);
                    float ds = Mathf.Min(dShadow, dFlowerShadow);
                    float aShadow = Mathf.Clamp01(0.5f - (ds - 0.3f) * S) * 0.45f;
                    float aOutline = Mathf.Clamp01(0.5f - (d - 0.26f) * S);
                    float aFill = Mathf.Clamp01(0.5f - d * S);
                    Color fill = dFlower < dText ? (isHeart ? heart : petal) : Color.Lerp(pink, white, Mathf.Clamp01((p.y - 1f) / 7f) * 0.25f);
                    // premultiplied "over": shadow, then white outline, then fill
                    Color acc = new Color(plum.r * aShadow, plum.g * aShadow, plum.b * aShadow, aShadow);
                    acc = Over(acc, white, aOutline);
                    acc = Over(acc, fill, aFill);
                    Color outc = acc.a > 0.001f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : new Color(1f, 1f, 1f, 0f);
                    px[y * W + x] = outc;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true)
            {
                name = "BR_PhotoLogo",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Color Over(Color dstPremul, Color src, float a)
        {
            return new Color(src.r * a + dstPremul.r * (1f - a), src.g * a + dstPremul.g * (1f - a),
                             src.b * a + dstPremul.b * (1f - a), a + dstPremul.a * (1f - a));
        }

        // distance (in cells) to the union of rounded glyph cells around p
        static float TextDist(bool[,] grid, int gw, int gh, Vector2 p)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y);
            float best = 10f;
            for (int y = iy - 1; y <= iy + 1; y++)
                for (int x = ix - 1; x <= ix + 1; x++)
                {
                    if (x < 0 || y < 0 || x >= gw || y >= gh || !grid[x, y]) continue;
                    Vector2 q = new Vector2(Mathf.Abs(p.x - (x + 0.5f)), Mathf.Abs(p.y - (y + 0.5f))) - new Vector2(0.55f - 0.3f, 0.55f - 0.3f);
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 0.3f;
                    if (d < best) best = d;
                }
            return best;
        }

        // five round petals and a centre (in cells, relative to the flower centre)
        static float FlowerDist(Vector2 p, out bool isHeart)
        {
            float dCentre = p.magnitude - 0.9f;
            float d = 10f;
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + i * 72f) * Mathf.Deg2Rad;
                Vector2 c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.75f;
                d = Mathf.Min(d, (p - c).magnitude - 1.35f);
            }
            isHeart = dCentre < 0.2f;
            return Mathf.Min(d, dCentre);
        }

        // ------------------------------------------------------------------ UI
        void Styles()
        {
            if (sTitle != null) return;
            sTitle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sRow = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            sToast = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        }

        static void Label(Rect r, string text, GUIStyle st, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0.15f, 0.08f, 0.12f, 0.55f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        static void Panel(Rect r, float alpha)
        {
            var old = GUI.color;
            GUI.color = new Color(0.20f, 0.10f, 0.16f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (!Active) return;
            Event e = Event.current;
            // mouse look / zoom - read from IMGUI events so it works with either input backend
            if (e.type == EventType.MouseDrag && e.button == 1) { mouseDelta += e.delta; e.Use(); }
            else if (e.type == EventType.ScrollWheel) { wheelDelta += e.delta.y; e.Use(); }
            if (hideForCapture) return;
            Styles();
            GUI.depth = -20;
            float W = Screen.width, H = Screen.height;
            Color white = new Color(1f, 0.97f, 0.96f, 1f);
            Color pink = new Color(1f, 0.72f, 0.82f, 1f);

            if (flash > 0f)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, flash * 0.85f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = old;
            }
            if (toastTimer > 0f)
            {
                float a = Mathf.Clamp01(toastTimer);
                Panel(new Rect(W * 0.15f, H * 0.12f, W * 0.7f, 56), 0.55f * a);
                Label(new Rect(W * 0.15f + 10, H * 0.12f, W * 0.7f - 20, 56), toast, sToast, new Color(1f, 1f, 1f, a));
            }
            if (hideAll) return;

            Label(new Rect(24, 18, 400, 40), "PHOTO MODE", sTitle, pink);
            string hint = InputReader.PadConnected
                ? "LS move   RS look   LT / RT down / up   D-pad zoom / roll   Y options   SELECT hide   A photo   B exit"
                : "WASD fly   Q / E down / up   SHIFT fast   RMB look   WHEEL or Z / X zoom   R / F roll   TAB options   H hide   ENTER photo   ESC exit";
            Panel(new Rect(0, H - 40, W, 40), 0.45f);
            Label(new Rect(10, H - 38, W - 20, 36), hint, sSmall, white);

            if (!panelOpen) return;
            float pw = 380f, rh = 30f;
            float ph = 64f + RowNames.Length * rh + 34f;
            float px = W - pw - 20f, py = Mathf.Max(20f, (H - 40f - ph) * 0.5f);
            Panel(new Rect(px, py, pw, ph), 0.6f);
            Label(new Rect(px + 18, py + 10, pw - 36, 40), "OPTIONS", sTitle, pink);
            for (int i = 0; i < RowNames.Length; i++)
            {
                Rect r = new Rect(px + 12, py + 56 + i * rh, pw - 24, rh - 4);
                bool sel = i == row;
                if (sel) Panel(r, 0.5f);
                Row rr = (Row)i;
                bool dim = rr == Row.Focus && !dof || rr == Row.AutoFocus && !dof || rr == Row.Aperture && !dof;
                Color c = sel ? pink : new Color(white.r, white.g, white.b, dim ? 0.55f : 1f);
                Label(new Rect(r.x + 10, r.y, 170, r.height), (sel ? "›  " : "   ") + RowNames[i], sRow, c);
                string val = Value(rr);
                if (rr == Row.Reset)
                {
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { row = i; ResetOptions(); }
                    continue;
                }
                Rect lr = new Rect(r.x + 180, r.y, 26, r.height), vr = new Rect(r.x + 206, r.y, r.width - 238, r.height), rr2 = new Rect(r.xMax - 30, r.y, 26, r.height);
                Label(lr, "‹", new GUIStyle(sRow) { alignment = TextAnchor.MiddleCenter }, c);
                Label(vr, val, new GUIStyle(sRow) { alignment = TextAnchor.MiddleCenter }, c);
                Label(rr2, "›", new GUIStyle(sRow) { alignment = TextAnchor.MiddleCenter }, c);
                if (GUI.Button(lr, GUIContent.none, GUIStyle.none)) { row = i; Change(rr, -1); }
                else if (GUI.Button(rr2, GUIContent.none, GUIStyle.none) || GUI.Button(vr, GUIContent.none, GUIStyle.none)) { row = i; Change(rr, 1); }
                else if (GUI.Button(new Rect(r.x, r.y, 180, r.height), GUIContent.none, GUIStyle.none)) row = i;
            }
            Label(new Rect(px, py + ph - 32, pw, 26), InputReader.PadConnected ? "D-pad ↑ ↓ choose   ← → change   Y hide" : "↑ ↓ choose   ← → change   TAB hide panel",
                  sSmall, white);
        }
    }
}
