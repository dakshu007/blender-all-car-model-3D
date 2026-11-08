// GameManager.cs - Blossom Rally entry point: builds the valley, car-select showroom, driving,
// drift scoring, HUD and the ESC pause menu (weather / fog, petals, colour grade, camera, car).
// Press Play in ANY scene - it bootstraps itself if needed.
using System.Collections;
using UnityEngine;

namespace BlossomRally
{
    public class GameManager : MonoBehaviour
    {
        enum State { Loading, Select, Drive }

        public static GameManager Instance { get; private set; }

        // ---- public hooks used by events, replay, photo mode, valley life ----
        public ArcadeCar Car { get { return car; } }
        public WorldBuilder World { get { return world; } }
        public CameraRig Rig { get { return rig; } }
        public bool IsDriving { get { return state == State.Drive && !paused; } }
        /// <summary>Set by photo / replay modes to hide the driving HUD.</summary>
        public static bool HideHud;

        /// <summary>Place the player's car at a pose with zero velocity (event restarts, checkpoint resets).</summary>
        public void TeleportCar(Vector3 p, Quaternion r)
        {
            if (car == null || car.Body == null) return;
            car.Body.SetVelocity(Vector3.zero);
            car.Body.angularVelocity = Vector3.zero;
            car.transform.SetPositionAndRotation(p, r);
            car.Body.position = p;
            car.Body.rotation = r;
            car.ResetDrift();
            rig.SnapBehind();
            ResetDrift(false);
        }

        State state = State.Loading;
        WorldBuilder world;
        Atmosphere atmosphere;
        CameraRig rig;
        PostFX postFx;
        PetalWeather petals;
        ArcadeCar car;
        int carIndex;
        bool showHelp = true;
        float helpTimer = 12f;
        string toast = "";
        float toastTimer;

        // drift scoring
        float driftPoints, driftCombo = 1f, driftHold, driftEndTimer, totalScore, bestDrift;
        bool drifting;
        string lastDrift = "";
        float lastDriftTimer;

        GUIStyle big, mid, small, title, shadow, button;

        // pause menu
        bool paused;
        int menuIndex;
        static readonly string[] MenuItems = { "Resume", "Weather", "Petals", "Colour grade", "Camera", "Change car", "Controls help" };
        static readonly string[] PetalNames = { "Calm", "Spring", "Blossom storm" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Instance == null && FindAll<GameManager>().Length == 0)
                new GameObject("BlossomRally").AddComponent<GameManager>();
        }

        static T[] FindAll<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            atmosphere = gameObject.AddComponent<Atmosphere>();
            Camera cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
            }
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.position = new Vector3(0f, 120f, -300f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            rig = cam.gameObject.AddComponent<CameraRig>();
            postFx = cam.gameObject.AddComponent<PostFX>();
            petals = new GameObject("PetalWeather").AddComponent<PetalWeather>();
            petals.follow = cam.transform;
            world = new GameObject("World").AddComponent<WorldBuilder>();
            foreach (var l in FindAll<Light>())
                if (l != Atmosphere.Sun && l.type == LightType.Directional) l.enabled = false;
        }

        IEnumerator Start()
        {
            yield return world.Build();
            SpawnCar(0, world.SpawnPosition, world.SpawnRotation);
            EnterSelect();
        }

        void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (p) menuIndex = 0;
        }

        void OnDisable()
        {
            if (paused) { Time.timeScale = 1f; AudioListener.pause = false; }
        }

        void MenuActivate(int i, int dir)
        {
            switch (i)
            {
                case 0: SetPaused(false); break;
                case 1: atmosphere.SetWeather(atmosphere.WeatherIndex + dir); break;
                case 2: petals.SetLevel(petals.level + dir); break;
                case 3: postFx.effectEnabled = !postFx.effectEnabled; break;
                case 4: rig.Cycle(); break;
                case 5: SetPaused(false); EnterSelect(); break;
                case 6: showHelp = !showHelp; helpTimer = 0f; break;
            }
        }

        string MenuValue(int i)
        {
            switch (i)
            {
                case 1: return atmosphere.Current.name;
                case 2: return PetalNames[petals.level];
                case 3: return postFx.effectEnabled ? "On" : "Off";
                case 4: return rig.mode.ToString();
                case 6: return showHelp ? "Shown" : "Hidden";
                default: return "";
            }
        }

        void EnterSelect()
        {
            state = State.Select;
            if (car != null) car.controlsEnabled = false;
            rig.showroom = true;
        }

        void EnterDrive()
        {
            state = State.Drive;
            car.controlsEnabled = true;
            rig.showroom = false;
            rig.mode = CamMode.Chase;
            rig.SnapBehind();
            helpTimer = 12f;
            showHelp = true;
            Toast(CarCatalog.Cars[carIndex].name + " - enjoy the valley!");
        }

        void SpawnCar(int index, Vector3 pos, Quaternion rot)
        {
            carIndex = (index % CarCatalog.Cars.Length + CarCatalog.Cars.Length) % CarCatalog.Cars.Length;
            Vector3 v = Vector3.zero;
            if (car != null)
            {
                if (car.Body != null) v = car.Body.GetVelocity();
                Destroy(car.gameObject);
            }
            car = CarFactory.Create(CarCatalog.Cars[carIndex], pos, rot);
            if (car == null) return;
            car.controlsEnabled = state == State.Drive;
            if (car.Body != null) car.Body.SetVelocity(v);
            car.OnImpact = OnImpact;
            rig.target = car;
            ResetDrift(false);
        }

        void OnImpact(float speed)
        {
            if (speed > 6f)
            {
                rig.Shake(Mathf.Clamp01(speed / 25f));
                if (drifting) { Toast("Drift broken!"); ResetDrift(false); }
            }
        }

        void Respawn()
        {
            if (car == null) return;
            Vector3 p; Quaternion r;
            world.RespawnPoint(car.transform.position, car.transform.forward, out p, out r);
            car.Body.SetVelocity(Vector3.zero);
            car.Body.angularVelocity = Vector3.zero;
            car.transform.SetPositionAndRotation(p, r);
            car.Body.position = p;
            car.Body.rotation = r;
            rig.SnapBehind();
            car.ResetDrift();
            ResetDrift(false);
        }

        void Toast(string s)
        {
            toast = s;
            toastTimer = 3f;
        }

        void Update()
        {
            if (state == State.Loading) return;
            if (paused)
            {
                if (InputReader.Down(KeyCode.Escape)) { SetPaused(false); return; }
                if (InputReader.Down(KeyCode.UpArrow) || InputReader.Down(KeyCode.W)) menuIndex = (menuIndex + MenuItems.Length - 1) % MenuItems.Length;
                if (InputReader.Down(KeyCode.DownArrow) || InputReader.Down(KeyCode.S)) menuIndex = (menuIndex + 1) % MenuItems.Length;
                if (InputReader.Down(KeyCode.LeftArrow) || InputReader.Down(KeyCode.A)) { if (menuIndex > 0 && menuIndex < 5) MenuActivate(menuIndex, -1); }
                if (InputReader.Down(KeyCode.RightArrow) || InputReader.Down(KeyCode.D)) { if (menuIndex > 0 && menuIndex < 5) MenuActivate(menuIndex, 1); }
                if (InputReader.Down(KeyCode.Return) || InputReader.Down(KeyCode.KeypadEnter) || InputReader.Down(KeyCode.Space)) MenuActivate(menuIndex, 1);
                return;
            }
            if (state == State.Select)
            {
                if (InputReader.Down(KeyCode.RightArrow) || InputReader.Down(KeyCode.D)) SpawnCar(carIndex + 1, world.SpawnPosition, world.SpawnRotation);
                if (InputReader.Down(KeyCode.LeftArrow) || InputReader.Down(KeyCode.A)) SpawnCar(carIndex - 1, world.SpawnPosition, world.SpawnRotation);
                if (InputReader.Down(KeyCode.Return) || InputReader.Down(KeyCode.KeypadEnter) || InputReader.Down(KeyCode.Space)) EnterDrive();
                return;
            }
            // driving
            for (int i = 0; i < CarCatalog.Cars.Length; i++)
                if (InputReader.Down(KeyCode.Alpha1 + i))
                {
                    SpawnCar(i, car.transform.position + Vector3.up * 0.6f, Quaternion.LookRotation(Vector3.ProjectOnPlane(car.transform.forward, Vector3.up)));
                    Toast(CarCatalog.Cars[i].name);
                }
            if (InputReader.Down(KeyCode.R)) Respawn();
            if (InputReader.Down(KeyCode.C)) rig.Cycle();
            if (InputReader.Down(KeyCode.P)) { petals.CycleLevel(); Toast(new[] { "Petals: calm", "Petals: spring", "Petals: blossom storm" }[petals.level]); }
            if (InputReader.Down(KeyCode.G)) { atmosphere.CycleWeather(); Toast("Weather: " + atmosphere.Current.name); }
            if (InputReader.Down(KeyCode.F)) { postFx.effectEnabled = !postFx.effectEnabled; Toast(postFx.effectEnabled ? "Colour grade ON" : "Colour grade OFF"); }
            if (InputReader.Down(KeyCode.H)) showHelp = !showHelp;
            if (InputReader.Down(KeyCode.Escape)) { SetPaused(true); return; }
            if (InputReader.Down(KeyCode.Tab)) EnterSelect();
            if (car != null && (car.UprightTimer > 2f || car.transform.position.y < -30f)) Respawn();
            UpdateDrift(Time.deltaTime);
            if (helpTimer > 0f) { helpTimer -= Time.deltaTime; if (helpTimer <= 0f) showHelp = false; }
            if (toastTimer > 0f) toastTimer -= Time.deltaTime;
            if (lastDriftTimer > 0f) lastDriftTimer -= Time.deltaTime;
        }

        void UpdateDrift(float dt)
        {
            if (car == null) return;
            bool active = (car.IsDrifting && car.Grounded && car.SpeedKmh > 20f && car.DriftAngle > 6f && car.DriftAngle < 100f)
                          || (car.Grounded && car.SpeedKmh > 30f && Mathf.Abs(car.SlipAngle) > 12f && Mathf.Abs(car.SlipAngle) < 100f);
            if (active)
            {
                drifting = true;
                driftEndTimer = 0.7f;
                driftHold += dt;
                driftCombo = Mathf.Min(8f, 1f + Mathf.Floor(driftHold / 2.5f));
                driftPoints += Mathf.Abs(car.SlipAngle) * car.SpeedKmh * 0.06f * dt * driftCombo;
            }
            else if (drifting)
            {
                driftEndTimer -= dt;
                if (driftEndTimer <= 0f) ResetDrift(true);
            }
        }

        void ResetDrift(bool bank)
        {
            if (bank && driftPoints > 50f)
            {
                int pts = Mathf.RoundToInt(driftPoints);
                totalScore += pts;
                bestDrift = Mathf.Max(bestDrift, pts);
                lastDrift = (pts > 3000 ? "LEGENDARY DRIFT  +" : pts > 1200 ? "GREAT DRIFT  +" : "DRIFT  +") + pts;
                lastDriftTimer = 2.5f;
            }
            drifting = false;
            driftPoints = 0f;
            driftCombo = 1f;
            driftHold = 0f;
        }

        // ------------------------------------------------------------------ HUD
        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true };
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            shadow = new GUIStyle(GUI.skin.label);
            button = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
        }

        void Label(Rect r, string text, GUIStyle st, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0.15f, 0.08f, 0.12f, 0.55f * c.a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, st);
            GUI.color = c;
            GUI.Label(r, text, st);
            GUI.color = old;
        }

        void Panel(Rect r, float alpha)
        {
            var old = GUI.color;
            GUI.color = new Color(0.20f, 0.10f, 0.16f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawPauseMenu(float W, float H, Color white, Color pink)
        {
            Panel(new Rect(0, 0, W, H), 0.35f);
            float pw = 460f, rowH = 44f, ph = 110f + MenuItems.Length * rowH;
            float px = (W - pw) * 0.5f, py = (H - ph) * 0.5f;
            Panel(new Rect(px, py, pw, ph), 0.7f);
            Label(new Rect(px, py + 14, pw, 50), "PAUSED", title, pink);
            for (int i = 0; i < MenuItems.Length; i++)
            {
                Rect r = new Rect(px + 30, py + 80 + i * rowH, pw - 60, rowH - 6);
                bool sel = i == menuIndex;
                if (sel) Panel(r, 0.5f);
                string val = MenuValue(i);
                string text = (sel ? "›  " : "   ") + MenuItems[i] + (val.Length > 0 ? "   ·   " + val : "");
                Label(new Rect(r.x + 10, r.y, r.width - 10, r.height), text, button, sel ? pink : white);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { menuIndex = i; MenuActivate(i, 1); }
            }
            Label(new Rect(px, py + ph - 30, pw, 24), "↑ ↓ choose   ← → / ENTER change   ESC resume", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter }, white);
        }

        void OnGUI()
        {
            Styles();
            float W = Screen.width, H = Screen.height;
            Color white = new Color(1f, 0.97f, 0.96f, 1f);
            Color pink = new Color(1f, 0.72f, 0.82f, 1f);
            if (!InputReader.Legacy)
            {
                Panel(new Rect(0, 0, W, 60), 0.8f);
                Label(new Rect(0, 10, W, 40), "Enable Project Settings > Player > Active Input Handling = Both, then restart Unity.", mid, white);
            }
            if (state == State.Loading)
            {
                Panel(new Rect(0, 0, W, H), 0.55f);
                Label(new Rect(0, H * 0.38f, W, 60), "BLOSSOM RALLY", title, pink);
                Label(new Rect(0, H * 0.46f, W, 40), world.Status + "...", mid, white);
                Panel(new Rect(W * 0.3f, H * 0.54f, W * 0.4f, 10), 0.6f);
                var old = GUI.color; GUI.color = pink;
                GUI.DrawTexture(new Rect(W * 0.3f, H * 0.54f, W * 0.4f * world.Progress, 10), Texture2D.whiteTexture);
                GUI.color = old;
                return;
            }
            var spec = CarCatalog.Cars[carIndex];
            if (state == State.Select)
            {
                Panel(new Rect(0, H - 190, W, 190), 0.45f);
                Label(new Rect(0, H - 182, W, 50), "<   " + spec.name + "   >", title, white);
                Label(new Rect(0, H - 132, W, 30), spec.carClass + "  ·  " + spec.era + "  ·  " + spec.drive + "  ·  " + Mathf.RoundToInt(spec.maxSpeedKmh) + " km/h", mid, pink);
                Label(new Rect(0, H - 92, W, 30), "Car " + (carIndex + 1) + " / " + CarCatalog.Cars.Length + "      ← / → choose      ENTER drive", mid, white);
                Label(new Rect(0, 24, W, 50), "BLOSSOM RALLY", title, pink);
                return;
            }
            // driving HUD
            if (car != null)
            {
                Label(new Rect(W - 330, H - 120, 300, 60), Mathf.RoundToInt(car.SpeedKmh) + " km/h", big, white);
                Label(new Rect(W - 330, H - 60, 300, 30), spec.name, new GUIStyle(mid) { alignment = TextAnchor.MiddleRight }, pink);
            }
            Label(new Rect(20, H - 60, 400, 30), "Score " + Mathf.RoundToInt(totalScore) + "    Best drift " + Mathf.RoundToInt(bestDrift),
                  new GUIStyle(mid) { alignment = TextAnchor.MiddleLeft }, white);
            if (car != null && car.IsDrifting)
            {
                // drift angle meter: fill = current angle, tick = the angle your inputs ask for
                float bw = 280f, bx = (W - bw) * 0.5f, by = H - 46f;
                Panel(new Rect(bx - 4, by - 4, bw + 8, 18), 0.45f);
                var oc = GUI.color;
                GUI.color = new Color(pink.r, pink.g, pink.b, 0.9f);
                GUI.DrawTexture(new Rect(bx, by, bw * Mathf.Clamp01(car.DriftAngle / 60f), 10), Texture2D.whiteTexture);
                GUI.color = white;
                GUI.DrawTexture(new Rect(bx + bw * Mathf.Clamp01(car.DriftTargetAngle / 60f) - 1.5f, by - 4, 3, 18), Texture2D.whiteTexture);
                GUI.color = oc;
                Label(new Rect(0, by - 34, W, 28), (car.DriftDirection > 0 ? "DRIFT  >  " : "<  DRIFT  ") + Mathf.RoundToInt(car.DriftAngle) + "°", mid, white);
            }
            if (drifting && driftPoints > 10f)
            {
                Label(new Rect(0, 70, W, 40), "DRIFT  x" + driftCombo.ToString("0"), mid, pink);
                Label(new Rect(0, 104, W, 50), Mathf.RoundToInt(driftPoints).ToString(), title, white);
            }
            else if (lastDriftTimer > 0f)
            {
                Label(new Rect(0, 90, W, 50), lastDrift, title, new Color(pink.r, pink.g, pink.b, Mathf.Clamp01(lastDriftTimer)));
            }
            if (toastTimer > 0f) Label(new Rect(0, H * 0.22f, W, 40), toast, mid, new Color(1f, 1f, 1f, Mathf.Clamp01(toastTimer)));
            if (showHelp)
            {
                Panel(new Rect(16, 16, 400, 318), 0.45f);
                Label(new Rect(30, 26, 380, 310),
                      "W / ↑   throttle        S / ↓   brake / reverse\n" +
                      "A D / ← →   steer      SPACE   handbrake\n\n" +
                      "DRIFT:  hold SPACE + steer to kick the tail out\n" +
                      "   W holds the slide · steer INTO it = more angle\n" +
                      "   counter-steer or lift off = straighten out\n" +
                      "   tap SPACE + steer the other way = switch sides\n\n" +
                      "1 - 9   switch car       R   reset to road\n" +
                      "C   camera      P   petals      F   colour grade\n" +
                      "G   weather (clear / haze / cloudy / fog off)\n" +
                      "ESC   pause menu      TAB   car select      H   help", small, white);
            }
            if (paused) DrawPauseMenu(W, H, white, pink);
        }
    }
}
