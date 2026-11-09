// GameManager.cs - Blossom Rally entry point and game flow:
//   Loading -> Title (valley flyover) -> Garage (turntable car select + livery) -> Drive
// Drive = free roam or an event (rally stage / drift challenge via StageManager).
// Also: drift scoring, HUD, ESC pause menu (weather, time of day, petals, camera, photo mode,
// replay, settings, garage, main menu), and wiring of audio, valley life, replay and photo mode.
// Press Play in ANY scene - it bootstraps itself if needed.
using System.Collections;
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(900)]     // after the camera rig: the title flyover owns the camera
    public class GameManager : MonoBehaviour
    {
        enum State { Loading, Title, Select, Drive }

        public static GameManager Instance { get; private set; }

        // ---- public hooks used by events, replay, photo mode, valley life ----
        public ArcadeCar Car { get { return car; } }
        public WorldBuilder World { get { return world; } }
        public CameraRig Rig { get { return rig; } }
        public bool IsDriving { get { return state == State.Drive && !paused; } }
        /// <summary>Set by photo / replay modes to hide the driving HUD.</summary>
        public static bool HideHud;

        State state = State.Loading;
        WorldBuilder world;
        Atmosphere atmosphere;
        CameraRig rig;
        PostFX postFx;
        PetalWeather petals;
        Garage garage;
        SettingsMenu settings;
        StageManager stages;
        ReplaySystem replay;
        PhotoMode photo;
        ArcadeCar car;
        int carIndex;
        bool showHelp = true;
        float helpTimer = 14f;
        string toast = "";
        float toastTimer;
        int pendingEvent = -1;           // event chosen on the title screen, started after the garage
        float fpsSmooth = 0.016f;

        // drift scoring
        float driftPoints, driftCombo = 1f, driftHold, driftEndTimer, totalScore, bestDrift;
        bool drifting;
        float peakDriftAngle;
        string lastDrift = "";
        float lastDriftTimer;

        // menus
        bool paused;
        MenuList titleMenu, eventMenu, pauseMenu;
        MenuList activeTitleMenu;
        float titleT;
        static readonly string[] PetalNames = { "Calm", "Spring", "Blossom storm" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Instance == null && FindAll<GameManager>().Length == 0)
                new GameObject("BlossomRally").AddComponent<GameManager>();
        }

        /// <summary>All loaded scene objects of a type (works the same on every Unity version, no deprecated API).</summary>
        static T[] FindAll<T>() where T : Component
        {
            var list = new System.Collections.Generic.List<T>();
            foreach (var o in Resources.FindObjectsOfTypeAll<T>())
                if (o != null && o.gameObject.scene.IsValid() && o.gameObject.activeInHierarchy) list.Add(o);
            return list.ToArray();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameSettings.Load();
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
            cam.gameObject.AddComponent<WindscreenFX>();
            petals = new GameObject("PetalWeather").AddComponent<PetalWeather>();
            petals.follow = cam.transform;
            var rain = new GameObject("RainFX").AddComponent<RainFX>();
            rain.follow = cam.transform;
            world = new GameObject("World").AddComponent<WorldBuilder>();
            garage = gameObject.AddComponent<Garage>();
            settings = gameObject.AddComponent<SettingsMenu>();
            stages = gameObject.AddComponent<StageManager>();
            replay = gameObject.AddComponent<ReplaySystem>();
            photo = gameObject.AddComponent<PhotoMode>();
            foreach (var l in FindAll<Light>())
                if (l != Atmosphere.Sun && l.type == LightType.Directional) l.enabled = false;

            // sounds from every system go through one place
            UI.OnSound = PlaySfx;
            StageManager.OnSound = PlaySfx;
            ReplaySystem.OnSound = PlaySfx;
            PhotoMode.OnSound = PlaySfx;
            StageManager.OnToast = Toast;
            stages.OnExitToFreeRoam = () => { Toast("Free roam"); };
            MusicDirector.Ensure();
            MusicDirector.SetMode(MusicMode.Title);
            AmbientAudio.Ensure();
            Sfx.Preload();
            BuildMenus();
        }

        static void PlaySfx(string id)
        {
            SfxId s;
            if (System.Enum.TryParse(id, out s)) Sfx.Play(s);
        }

        IEnumerator Start()
        {
            yield return world.Build();
            garage.Build(world);
            ValleyLife.Create(world);
            WaterLife.Create(world);
            WorldHooks.RaiseWorldBuilt(world);
            SpawnCar(0, garage.TablePos, Quaternion.identity);
            EnterTitle();
        }

        // ------------------------------------------------------------------ menus
        void BuildMenus()
        {
            titleMenu = new MenuList { title = "BLOSSOM RALLY", width = 460f, footer = "↑ ↓ choose   ENTER select" };
            titleMenu.Add("Free Roam", () => { pendingEvent = -1; EnterSelect(); });
            titleMenu.Add("Rally Stages & Drift Challenge", () => { activeTitleMenu = eventMenu; eventMenu.index = 0; RebuildEventMenu(); });
            titleMenu.Add("Settings", () => settings.Open(null));
            titleMenu.Add("Quit", Application.Quit);

            eventMenu = new MenuList { title = "EVENTS", width = 640f };

            pauseMenu = new MenuList { title = "PAUSED", width = 560f };
            pauseMenu.Add("Resume", () => SetPaused(false));
            pauseMenu.Add("Restart event", () => { SetPaused(false); stages.Restart(); }, null, null, () => stages != null && stages.Running);
            pauseMenu.Add("Quit event", () => { SetPaused(false); stages.Abort(); }, null, null, () => stages != null && stages.Running);
            pauseMenu.Add("Weather", null, () => atmosphere.Current.name, d => atmosphere.SetWeather(atmosphere.WeatherIndex + d));
            pauseMenu.Add("Time of day", null, () => atmosphere.CurrentTime.name, d => atmosphere.SetTimeOfDay(atmosphere.TimeIndex + d));
            pauseMenu.Add("Petals", null, () => PetalNames[petals.level], d => petals.SetLevel(petals.level + d));
            pauseMenu.Add("Camera", () => rig.Cycle(), () => rig.mode.ToString(), d => rig.Cycle());
            pauseMenu.Add("Colour grade", null, () => postFx.effectEnabled ? "On" : "Off", d => postFx.effectEnabled = !postFx.effectEnabled);
            pauseMenu.Add("Photo mode", () => { SetPaused(false); photo.Enter(); });
            pauseMenu.Add("Instant replay", () => { SetPaused(false); if (replay.HasRecording) replay.Begin(); else Toast("Nothing to replay yet"); });
            pauseMenu.Add("Settings", () => settings.Open(null));
            pauseMenu.Add("Garage (change car / livery)", () => { SetPaused(false); if (stages.Running) stages.Abort(); pendingEvent = -1; EnterSelect(); });
            pauseMenu.Add("Controls help", () => { showHelp = !showHelp; helpTimer = 0f; }, () => showHelp ? "Shown" : "Hidden");
            pauseMenu.Add("Main menu", () => { SetPaused(false); if (stages.Running) stages.Abort(); EnterTitle(); });
        }

        void RebuildEventMenu()
        {
            eventMenu.items.Clear();
            for (int i = 0; i < stages.EventCount; i++)
            {
                int idx = i;
                eventMenu.Add(stages.EventNames[i], () => { pendingEvent = idx; EnterSelect(); }, () => stages.EventDescription(idx));
            }
            eventMenu.Add("Back", () => activeTitleMenu = titleMenu);
        }

        void SetPaused(bool p)
        {
            paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            if (p) pauseMenu.index = 0;
        }

        void OnDisable()
        {
            if (paused) { Time.timeScale = 1f; AudioListener.pause = false; }
        }

        // ------------------------------------------------------------------ states
        void EnterTitle()
        {
            state = State.Title;
            activeTitleMenu = titleMenu;
            titleMenu.index = 0;
            garage.Show(car);
            garage.OwnsCamera = false;
            rig.showroom = true;
            rig.externalControl = true;
            MusicDirector.SetMode(MusicMode.Title);
        }

        void EnterSelect()
        {
            state = State.Select;
            if (car == null) SpawnCar(carIndex, garage.TablePos, Quaternion.identity);
            garage.Show(car);
            garage.OwnsCamera = true;
            rig.showroom = true;
            rig.externalControl = true;
            MusicDirector.SetMode(MusicMode.Select);
        }

        void EnterDrive()
        {
            state = State.Drive;
            garage.Release(car);
            rig.showroom = false;
            rig.externalControl = false;
            rig.mode = CamMode.Chase;
            TeleportCar(world.SpawnPosition, world.SpawnRotation);
            car.controlsEnabled = true;
            helpTimer = 14f;
            showHelp = true;
            MusicDirector.SetMode(MusicMode.Drive);
            if (pendingEvent >= 0)
            {
                stages.StartEvent(pendingEvent);
                pendingEvent = -1;
            }
            else Toast(CarCatalog.Cars[carIndex].name + " - enjoy the valley!");
        }

        void SpawnCar(int index, Vector3 pos, Quaternion rot)
        {
            carIndex = (index % CarCatalog.Cars.Length + CarCatalog.Cars.Length) % CarCatalog.Cars.Length;
            Vector3 v = Vector3.zero;
            if (car != null)
            {
                if (car.Body != null && !car.Body.isKinematic) v = car.Body.GetVelocity();
                Destroy(car.gameObject);
            }
            car = CarFactory.Create(CarCatalog.Cars[carIndex], pos, rot);
            if (car == null) return;
            car.controlsEnabled = state == State.Drive;
            if (car.Body != null && state == State.Drive) car.Body.SetVelocity(v);
            car.OnImpact = OnImpact;
            WorldHooks.RaiseCarSpawned(car);
            rig.target = car;
            if (state == State.Select || state == State.Title) garage.Show(car);
            ResetDrift(false);
        }

        void OnImpact(float speed)
        {
            if (speed > 6f)
            {
                rig.Shake(Mathf.Clamp01(speed / 25f));
                if (car != null) GameEvents.RaiseImpact(speed, car.transform.position);
                InputReader.Rumble(Mathf.Clamp01(speed / 20f), Mathf.Clamp01(speed / 30f), 0.25f);
                if (drifting) { Toast("Drift broken!"); ResetDrift(false); }
            }
        }

        void Respawn()
        {
            if (car == null) return;
            if (stages != null && stages.HandleRespawn()) return;
            Vector3 p; Quaternion r;
            world.RespawnPoint(car.transform.position, car.transform.forward, out p, out r);
            TeleportCar(p, r);
        }

        /// <summary>Place the player's car at a pose with zero velocity (event restarts, checkpoint resets).</summary>
        public void TeleportCar(Vector3 p, Quaternion r)
        {
            if (car == null || car.Body == null) return;
            if (!car.Body.isKinematic)
            {
                car.Body.SetVelocity(Vector3.zero);
                car.Body.angularVelocity = Vector3.zero;
            }
            car.transform.SetPositionAndRotation(p, r);
            car.Body.position = p;
            car.Body.rotation = r;
            car.ResetDrift();
            rig.SnapBehind();
            ResetDrift(false);
        }

        public void Toast(string s)
        {
            toast = s;
            toastTimer = 3f;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            fpsSmooth = Mathf.Lerp(fpsSmooth, Time.unscaledDeltaTime, 0.05f);
            if (Input2.FpsToggle) GameSettings.ShowFps = !GameSettings.ShowFps;
            if (state == State.Loading) return;
            if (settings.IsOpen) return;
            if (ReplaySystem.BlockingInput || PhotoMode.BlockingInput) return;   // they own input and Esc while active (and one frame after)

            if (state == State.Title)
            {
                if (activeTitleMenu.HandleInput() && activeTitleMenu == eventMenu) activeTitleMenu = titleMenu;
                return;
            }
            if (state == State.Select)
            {
                UpdateSelect();
                return;
            }
            // ---- driving ----
            if (paused)
            {
                if (pauseMenu.HandleInput()) SetPaused(false);
                return;
            }
            bool results = stages.ShowingResults;
            if (InputReader.PauseDown && !results) { SetPaused(true); UI.Sound("MenuSelect"); return; }
            if (!stages.Running)
            {
                for (int i = 0; i < CarCatalog.Cars.Length; i++)
                    if (InputReader.Down(KeyCode.Alpha1 + i))
                    {
                        SpawnCar(i, car.transform.position + Vector3.up * 0.6f, Quaternion.LookRotation(Vector3.ProjectOnPlane(car.transform.forward, Vector3.up)));
                        Toast(CarCatalog.Cars[i].name);
                    }
                if (InputReader.Down(KeyCode.Tab)) { pendingEvent = -1; EnterSelect(); return; }
            }
            if (InputReader.ActionDown(GameAction.Reset) && !results) Respawn();
            if (InputReader.ActionDown(GameAction.Camera)) { rig.Cycle(); UI.Sound("Camera"); }
            if (InputReader.ActionDown(GameAction.Replay) && !results)
            {
                if (replay.HasRecording) { replay.Begin(); return; }
                Toast("Nothing to replay yet");
            }
            if (InputReader.ActionDown(GameAction.Photo)) { photo.Enter(); return; }
            if (InputReader.Down(KeyCode.P) || GamepadBridge.ButtonDown(PadButton.DpadUp)) { petals.CycleLevel(); Toast("Petals: " + PetalNames[petals.level]); }
            if (InputReader.Down(KeyCode.G) || GamepadBridge.ButtonDown(PadButton.DpadRight)) { atmosphere.CycleWeather(); Toast("Weather: " + atmosphere.Current.name); }
            if (InputReader.Down(KeyCode.T) || GamepadBridge.ButtonDown(PadButton.DpadLeft)) { atmosphere.CycleTimeOfDay(); Toast("Time of day: " + atmosphere.CurrentTime.name); }
            if (InputReader.Down(KeyCode.F)) { postFx.effectEnabled = !postFx.effectEnabled; Toast(postFx.effectEnabled ? "Colour grade ON" : "Colour grade OFF"); }
            if (InputReader.Down(KeyCode.H)) showHelp = !showHelp;
            if (car != null && (car.UprightTimer > 2f || car.transform.position.y < -30f)) Respawn();
            UpdateDrift(Time.deltaTime);
            float speed01 = car != null ? Mathf.Clamp01(car.SpeedKmh / 160f) : 0f;
            float intensity = Mathf.Max(0.2f * speed01, drifting ? 0.2f + 0.8f * (driftCombo - 1f) / 7f : 0f);
            MusicDirector.SetIntensity(intensity);
            if (helpTimer > 0f) { helpTimer -= Time.deltaTime; if (helpTimer <= 0f) showHelp = false; }
            if (toastTimer > 0f) toastTimer -= Time.deltaTime;
            if (lastDriftTimer > 0f) lastDriftTimer -= Time.deltaTime;
        }

        void UpdateSelect()
        {
            if (InputReader.MenuLeft) { SpawnCar(carIndex - 1, garage.TablePos, Quaternion.identity); UI.Sound("MenuMove"); }
            if (InputReader.MenuRight) { SpawnCar(carIndex + 1, garage.TablePos, Quaternion.identity); UI.Sound("MenuMove"); }
            if (InputReader.MenuUp) { garage.ChangeColour(1); UI.Sound("MenuMove"); }
            if (InputReader.MenuDown) { garage.ChangeColour(-1); UI.Sound("MenuMove"); }
            if (InputReader.Down(KeyCode.Q) || GamepadBridge.ButtonDown(PadButton.LeftShoulder)) { garage.ChangePattern(-1); UI.Sound("MenuMove"); }
            if (InputReader.Down(KeyCode.E) || GamepadBridge.ButtonDown(PadButton.RightShoulder)) { garage.ChangePattern(1); UI.Sound("MenuMove"); }
            if (InputReader.Down(KeyCode.X) || GamepadBridge.ButtonDown(PadButton.West))
            {
                var ex = car != null ? car.GetComponent<CarExtras>() : null;
                if (ex != null) { ex.Clean(); Toast("Washed and polished"); }
            }
            if (InputReader.MenuConfirm) { UI.Sound("MenuSelect"); EnterDrive(); return; }
            if (InputReader.MenuBack) { UI.Sound("MenuBack"); pendingEvent = -1; EnterTitle(); }
        }

        void LateUpdate()
        {
            if (state != State.Title) return;
            // slow flyover of the valley behind the title menu
            var cam = Camera.main;
            if (cam == null) return;
            titleT += Time.unscaledDeltaTime;
            float a = titleT * 0.035f + 2.2f;
            Vector3 c = new Vector3(MapData.LakeCenter.x * 0.5f, 0f, MapData.LakeCenter.y * 0.5f);
            Vector3 pos = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 300f;
            pos.y = Mathf.Max(world.HeightAt(pos.x, pos.z) + 45f, 70f + Mathf.Sin(titleT * 0.05f) * 12f);
            Vector3 look = Vector3.Lerp(new Vector3(MapData.LakeCenter.x, 0f, MapData.LakeCenter.y),
                                        new Vector3(MapData.PagodaTop.x, 40f, MapData.PagodaTop.y), 0.5f + 0.5f * Mathf.Sin(titleT * 0.04f));
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
            cam.fieldOfView = 52f;
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
                float combo = Mathf.Min(8f, 1f + Mathf.Floor(driftHold / 2.5f));
                if (combo > driftCombo) InputReader.Rumble(0.1f, 0.35f, 0.12f);
                driftCombo = combo;
                driftPoints += Mathf.Abs(car.SlipAngle) * car.SpeedKmh * 0.06f * dt * driftCombo;
                peakDriftAngle = Mathf.Max(peakDriftAngle, Mathf.Abs(car.SlipAngle));
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
                GameEvents.RaiseDriftBanked(pts, peakDriftAngle);
            }
            drifting = false;
            peakDriftAngle = 0f;
            driftPoints = 0f;
            driftCombo = 1f;
            driftHold = 0f;
        }

        // ------------------------------------------------------------------ HUD
        void OnGUI()
        {
            UI.Styles();
            float W = Screen.width, H = Screen.height;
            Color white = UI.White, pink = UI.Pink;
            if (GameSettings.ShowFps)
                UI.Label(new Rect(W - 130, 8, 120, 24), Mathf.RoundToInt(1f / Mathf.Max(0.001f, fpsSmooth)) + " fps", UI.MidRight, white);
            if (!InputReader.Legacy)
            {
                UI.Panel(new Rect(0, 0, W, 60), 0.8f);
                UI.Label(new Rect(0, 10, W, 40), "Enable Project Settings > Player > Active Input Handling = Both, then restart Unity.", UI.Mid, white);
            }
            if (state == State.Loading)
            {
                UI.Panel(new Rect(0, 0, W, H), 0.55f);
                UI.Label(new Rect(0, H * 0.36f, W, 90), "BLOSSOM RALLY", UI.Huge, pink);
                UI.Label(new Rect(0, H * 0.47f, W, 40), world.Status + "...", UI.Mid, white);
                UI.Bar(new Rect(W * 0.3f, H * 0.54f, W * 0.4f, 10), world.Progress, pink);
                return;
            }
            if (settings.IsOpen || photo.Active || replay.Active) return;
            var spec = CarCatalog.Cars[carIndex];
            if (state == State.Title)
            {
                UI.Label(new Rect(0, H * 0.10f, W, 100), "BLOSSOM RALLY", UI.Huge, pink);
                UI.Label(new Rect(0, H * 0.10f + 86, W, 30), "a spring valley rally", UI.Mid, white);
                activeTitleMenu.Draw(W * 0.5f, H * 0.62f);
                UI.Label(new Rect(0, H - 34, W, 24), InputReader.PadConnected ? "Gamepad connected" : "Keyboard  ·  plug in a gamepad for analogue steering and rumble",
                         UI.SmallCentre, new Color(1f, 1f, 1f, 0.7f));
                return;
            }
            if (state == State.Select)
            {
                DrawSelect(W, H, spec);
                return;
            }
            if (HideHud) return;
            // ---- driving HUD ----
            if (car != null)
            {
                UI.Label(new Rect(W - 330, H - 120, 300, 60), Mathf.RoundToInt(car.SpeedKmh) + " km/h", UI.Big, white);
                UI.Label(new Rect(W - 330, H - 60, 300, 30), spec.name + "   ·   gear " + car.Gear, UI.MidRight, pink);
            }
            if (!stages.Running)
                UI.Label(new Rect(20, H - 60, 500, 30), "Score " + Mathf.RoundToInt(totalScore) + "    Best drift " + Mathf.RoundToInt(bestDrift), UI.MidLeft, white);
            if (car != null && car.IsDrifting)
            {
                float bw = 280f, bx = (W - bw) * 0.5f, by = H - 46f;
                UI.Panel(new Rect(bx - 4, by - 4, bw + 8, 18), 0.45f);
                var oc = GUI.color;
                GUI.color = new Color(pink.r, pink.g, pink.b, 0.9f);
                GUI.DrawTexture(new Rect(bx, by, bw * Mathf.Clamp01(car.DriftAngle / 60f), 10), Texture2D.whiteTexture);
                GUI.color = white;
                GUI.DrawTexture(new Rect(bx + bw * Mathf.Clamp01(car.DriftTargetAngle / 60f) - 1.5f, by - 4, 3, 18), Texture2D.whiteTexture);
                GUI.color = oc;
                UI.Label(new Rect(0, by - 34, W, 28), (car.DriftDirection > 0 ? "DRIFT  >  " : "<  DRIFT  ") + Mathf.RoundToInt(car.DriftAngle) + "°", UI.Mid, white);
            }
            if (!stages.Running)
            {
                if (drifting && driftPoints > 10f)
                {
                    UI.Label(new Rect(0, 70, W, 40), "DRIFT  x" + driftCombo.ToString("0"), UI.Mid, pink);
                    UI.Label(new Rect(0, 104, W, 50), Mathf.RoundToInt(driftPoints).ToString(), UI.Title, white);
                }
                else if (lastDriftTimer > 0f)
                    UI.Label(new Rect(0, 90, W, 50), lastDrift, UI.Title, new Color(pink.r, pink.g, pink.b, Mathf.Clamp01(lastDriftTimer)));
            }
            if (toastTimer > 0f) UI.Label(new Rect(0, H * 0.22f, W, 40), toast, UI.Mid, new Color(1f, 1f, 1f, Mathf.Clamp01(toastTimer)));
            if (showHelp && !paused)
            {
                UI.Panel(new Rect(16, 16, 430, 352), 0.45f);
                UI.Label(new Rect(30, 26, 410, 340),
                      Key(GameAction.Throttle) + " / ↑ throttle     " + Key(GameAction.Brake) + " / ↓ brake, reverse\n" +
                      Key(GameAction.SteerLeft) + " " + Key(GameAction.SteerRight) + " / ← → steer     " + Key(GameAction.Handbrake) + " handbrake\n\n" +
                      "DRIFT: hold " + Key(GameAction.Handbrake) + " + steer to kick the tail out\n" +
                      "   throttle holds it · steer INTO it = more angle\n" +
                      "   counter-steer or lift off = straighten out\n" +
                      "   " + Key(GameAction.Handbrake) + " + steer the other way = switch sides\n\n" +
                      Key(GameAction.Reset) + " reset    " + Key(GameAction.Camera) + " camera    " + Key(GameAction.Replay) + " replay    " + Key(GameAction.Photo) + " photo\n" +
                      "G weather    T time of day    P petals    F grade\n" +
                      "1 - 9 switch car    TAB garage    ESC pause    H help\n" +
                      "Gamepad: RT/LT drive, stick steer, A/B handbrake,\nY camera, X photo, LB replay, Select reset, Start pause", UI.Small, white);
            }
            if (paused)
            {
                UI.Panel(new Rect(0, 0, W, H), 0.3f);
                pauseMenu.Draw(W * 0.5f, H * 0.5f);
            }
        }

        static string Key(GameAction a)
        {
            string k = GameSettings.Binding(a).ToString();
            return k == "Space" ? "SPACE" : k;
        }

        void DrawSelect(float W, float H, CarSpec spec)
        {
            float px = W - 470f, py = H * 0.16f, pw = 440f;
            UI.Panel(new Rect(px, py, pw, 470), 0.62f);
            UI.Label(new Rect(px, py + 14, pw, 46), spec.name, UI.Title, UI.White);
            UI.Label(new Rect(px, py + 58, pw, 26), spec.carClass + "  ·  " + spec.era, UI.SmallCentre, UI.Pink);
            UI.Label(new Rect(px, py + 80, pw, 26), spec.drive + "  ·  " + Mathf.RoundToInt(spec.mass) + " kg  ·  " + Mathf.RoundToInt(spec.maxSpeedKmh) + " km/h", UI.SmallCentre, UI.White);
            string[] names = { "Power", "Top speed", "Grip", "Drift", "Agility" };
            float[] vals =
            {
                Mathf.InverseLerp(4.5f, 7.6f, spec.power / spec.mass),
                Mathf.InverseLerp(160f, 215f, spec.maxSpeedKmh),
                Mathf.InverseLerp(0.95f, 1.15f, spec.grip),
                Mathf.InverseLerp(0.75f, 0.52f, spec.driftGrip),
                Mathf.InverseLerp(1250f, 820f, spec.mass)
            };
            for (int i = 0; i < names.Length; i++)
            {
                float y = py + 120 + i * 34;
                UI.Label(new Rect(px + 24, y, 130, 26), names[i], UI.Row, UI.White);
                UI.Bar(new Rect(px + 160, y + 9, pw - 190, 10), 0.12f + vals[i] * 0.88f, UI.Pink);
            }
            float ly = py + 300;
            UI.Label(new Rect(px + 24, ly, pw - 48, 28), "Colour   ‹ " + garage.ColourName + " ›", UI.Row, UI.Gold);
            UI.Label(new Rect(px + 24, ly + 34, pw - 48, 28), "Pattern  ‹ " + garage.PatternName + " ›", UI.Row, UI.Gold);
            UI.Label(new Rect(px + 24, ly + 76, pw - 48, 80),
                     "← →  car (" + (carIndex + 1) + " / " + CarCatalog.Cars.Length + ")     ↑ ↓  colour\nQ / E  pattern     X  wash the car\nENTER  drive     ESC  back", UI.Small, UI.White);
            string head = pendingEvent >= 0 && pendingEvent < stages.EventCount ? stages.EventNames[pendingEvent] : "FREE ROAM";
            UI.Label(new Rect(0, 24, W, 50), "GARAGE  ·  " + head, UI.Title, UI.Pink);
        }
    }

    /// <summary>Small extra hotkeys that don't belong to a game action.</summary>
    static class Input2
    {
        public static bool FpsToggle { get { return InputReader.Down(KeyCode.BackQuote) || InputReader.Down(KeyCode.F1); } }
    }
}
