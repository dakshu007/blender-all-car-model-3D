// GameManager.cs - Blossom Rally entry point: builds the valley, car-select showroom, driving,
// drift scoring and HUD. Press Play in ANY scene - it bootstraps itself if needed.
using System.Collections;
using UnityEngine;

namespace BlossomRally
{
    public class GameManager : MonoBehaviour
    {
        enum State { Loading, Select, Drive }

        public static GameManager Instance { get; private set; }

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

        GUIStyle big, mid, small, title, shadow;

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
            if (InputReader.Down(KeyCode.G)) { atmosphere.CycleFog(); Toast("Fog density changed"); }
            if (InputReader.Down(KeyCode.F)) { postFx.effectEnabled = !postFx.effectEnabled; Toast(postFx.effectEnabled ? "Colour grade ON" : "Colour grade OFF"); }
            if (InputReader.Down(KeyCode.H)) showHelp = !showHelp;
            if (InputReader.Down(KeyCode.Escape) || InputReader.Down(KeyCode.Tab)) EnterSelect();
            if (car != null && (car.UprightTimer > 2f || car.transform.position.y < -30f)) Respawn();
            UpdateDrift(Time.deltaTime);
            if (helpTimer > 0f) { helpTimer -= Time.deltaTime; if (helpTimer <= 0f) showHelp = false; }
            if (toastTimer > 0f) toastTimer -= Time.deltaTime;
            if (lastDriftTimer > 0f) lastDriftTimer -= Time.deltaTime;
        }

        void UpdateDrift(float dt)
        {
            if (car == null) return;
            bool active = car.Grounded && car.SpeedKmh > 30f && Mathf.Abs(car.SlipAngle) > 12f && Mathf.Abs(car.SlipAngle) < 100f;
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
                Panel(new Rect(16, 16, 360, 268), 0.45f);
                Label(new Rect(30, 26, 340, 260),
                      "W / ↑   throttle        S / ↓   brake / reverse\n" +
                      "A D / ← →   steer      SPACE   handbrake (drift!)\n\n" +
                      "1 - 9   switch car       R   reset to road\n" +
                      "C   camera (chase / cockpit / hood / cinematic)\n" +
                      "P   petals (calm / spring / storm)\n" +
                      "G   fog      F   colour grade      H   help\n" +
                      "ESC   car select\n\n" +
                      "Tip: flick with handbrake, then hold throttle\nand counter-steer to chain long drifts.", small, white);
            }
        }
    }
}
