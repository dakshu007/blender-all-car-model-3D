// FinishMoment.cs - the finish-line moment on GameEvents.Finished: half a second of slow motion, a
// sweeping cinematic orbit around the car (takes CameraRig.externalControl while it runs and hands it
// back), pink / white / gold confetti petals bursting from both sides, and a medal "stamp" - a big medal
// disc slamming onto the corner of the results panel with a red seal - drawn above StageManager's results
// (GUI.depth -15 vs -10). Ends cleanly when the player restarts, continues, leaves, or photo mode takes over.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(950)]      // LateUpdate after CameraRig, before ReplaySystem (1000)
    public class FinishMoment : MonoBehaviour
    {
        public static FinishMoment Instance { get; private set; }

        bool running, ownsCamera, tiedToResults, confettiSecond;
        int medal;
        float t;                       // real time since the moment began (frozen while paused)
        float yaw, dist, height, fovStart;
        float stampShake;
        bool stampSounded, sealSounded;
        ArcadeCar car;
        AudioSource voice;
        GUIStyle sMedal, sSeal;

        static readonly string[] MedalNames = { "", "BRONZE", "SILVER", "GOLD" };

        void Awake()
        {
            Instance = this;
            useGUILayout = false;      // plain GUI calls only: skip the layout pass
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
            voice.dopplerLevel = 0f;
        }

        void OnDestroy()
        {
            End();
            if (Instance == this) Instance = null;
        }

        void OnDisable() { End(); }

        public static bool Running { get { return Instance != null && Instance.running; } }

        public void Begin(int medalIndex)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Car == null || gm.Rig == null) return;
            End();
            car = gm.Car;
            medal = Mathf.Clamp(medalIndex, 0, 3);
            running = true;
            t = 0f;
            stampShake = 0f;
            stampSounded = sealSounded = confettiSecond = false;
            var sm = StageManager.Instance;
            tiedToResults = sm != null && sm.ShowingResults;

            SlowMo.Trigger(0.5f, 0.3f);
            GameEvents.RaiseCheer(car.transform.position, medal > 0 ? 1f : 0.6f);

            // camera: start the orbit from wherever the chase camera is now so the sweep is seamless
            bool camFree = (PhotoMode.Instance == null || !PhotoMode.Instance.Active) && (ReplaySystem.Instance == null || !ReplaySystem.Instance.Active);
            if (camFree && !gm.Rig.externalControl)
            {
                gm.Rig.externalControl = true;
                ownsCamera = true;
                Transform ct = gm.Rig.transform;
                Vector3 off = ct.position - car.transform.position;
                yaw = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
                dist = new Vector2(off.x, off.z).magnitude;
                height = off.y;
                var cam = gm.Rig.GetComponent<Camera>();
                fovStart = cam != null ? cam.fieldOfView : 60f;
            }

            if (medal > 0 && JuiceFX.Instance != null)
            {
                Vector3 p = car.transform.position;
                Vector3 r = car.transform.right;
                JuiceFX.Instance.ConfettiBurst(p + r * 2.2f + Vector3.up * 0.6f, (Vector3.up + r * 0.35f).normalized, 70 + 30 * medal, 1f);
                JuiceFX.Instance.ConfettiBurst(p - r * 2.2f + Vector3.up * 0.6f, (Vector3.up - r * 0.35f).normalized, 70 + 30 * medal, 1f);
                Play(JuiceAudio.Confetti, 0.55f);
            }
        }

        /// <summary>Stop the moment and hand the camera back (safe to call any time).</summary>
        public void End()
        {
            if (ownsCamera)
            {
                ownsCamera = false;
                var gm = GameManager.Instance;
                bool photo = PhotoMode.Instance != null && PhotoMode.Instance.Active;
                bool replay = ReplaySystem.Instance != null && ReplaySystem.Instance.Active;
                // only give the camera back if nobody else took it meanwhile (photo / replay / title / garage)
                if (gm != null && gm.Rig != null && !photo && !replay && !gm.Rig.showroom && gm.Rig.externalControl)
                {
                    gm.Rig.externalControl = false;
                    gm.Rig.SnapBehind();
                }
            }
            running = false;
            car = null;
        }

        void Play(AudioClip c, float vol)
        {
            if (c == null || voice == null) return;
            voice.PlayOneShot(c, Mathf.Clamp01(vol * GameSettings.SfxVolume));
        }

        void Update()
        {
            if (!running) return;
            var gm = GameManager.Instance;
            var sm = StageManager.Instance;
            bool photo = PhotoMode.Instance != null && PhotoMode.Instance.Active;
            if (photo && ownsCamera) ownsCamera = false;                  // photo mode owns the camera now (and hands it back itself)
            bool stop = gm == null || car == null || gm.Car != car || gm.Rig == null || gm.Rig.showroom
                        || (tiedToResults && (sm == null || !sm.ShowingResults))
                        || (!tiedToResults && t > 5f)
                        || (ReplaySystem.Instance != null && ReplaySystem.Instance.Active);
            if (stop) { End(); return; }
            if (Time.timeScale <= 0f) return;                             // paused: freeze
            t += Time.unscaledDeltaTime;

            // second, smaller confetti volley
            if (!confettiSecond && t > 0.45f && medal > 0 && JuiceFX.Instance != null)
            {
                confettiSecond = true;
                JuiceFX.Instance.ConfettiBurst(car.transform.position + Vector3.up * 1.4f, Vector3.up, 40 + 25 * medal, 0.8f);
            }
            if (!stampSounded && t > StampAt + StampSlam && medal > 0 && tiedToResults && !GameManager.HideHud)
            {
                stampSounded = true;
                stampShake = 1f;
                Play(JuiceAudio.Stamp, 0.75f);
                InputReader.Rumble(0.45f, 0.2f, 0.12f);
            }
            if (!sealSounded && t > SealAt + StampSlam && medal > 0 && tiedToResults && !GameManager.HideHud)
            {
                sealSounded = true;
                Play(JuiceAudio.Stamp, 0.4f);
            }
            stampShake = Mathf.MoveTowards(stampShake, 0f, Time.unscaledDeltaTime * 3.5f);
        }

        void LateUpdate()
        {
            if (!running || !ownsCamera || car == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Rig == null) return;
            if (Time.timeScale <= 0f) return;
            float dt = Time.unscaledDeltaTime;
            Transform ct = gm.Rig.transform;
            var cam = gm.Rig.GetComponent<Camera>();

            // sweep: fast at first, settling into a slow, steady orbit
            float speed = Mathf.Lerp(48f, 11f, Mathf.Clamp01(t / 2.5f));
            yaw += speed * dt;
            float k = 1f - Mathf.Exp(-dt * 1.4f);
            dist = Mathf.Lerp(dist, 8.2f, k);
            height = Mathf.Lerp(height, 2.1f + 0.5f * Mathf.Sin(t * 0.45f), k);
            Vector3 focus = car.transform.position + Vector3.up * 0.8f;
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 want = car.transform.position + q * new Vector3(0f, 0f, dist) + Vector3.up * height;
            // keep out of hills / walls
            Vector3 dir = want - focus;
            RaycastHit hit;
            if (Physics.SphereCast(focus, 0.3f, dir.normalized, out hit, dir.magnitude, ~(1 << CarExtras.CarLayer), QueryTriggerInteraction.Ignore))
                want = focus + dir.normalized * Mathf.Max(1.6f, hit.distance - 0.2f);
            Vector3 pos = t < 0.05f ? want : Vector3.Lerp(ct.position, want, 1f - Mathf.Exp(-dt * 6f));
            ct.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, Vector3.up));
            if (cam != null) cam.fieldOfView = Mathf.Lerp(fovStart, 46f, Mathf.Clamp01(t / 1.8f));
        }

        // ------------------------------------------------------------------ medal stamp (above the results panel)
        const float StampAt = 0.15f, StampSlam = 0.16f, SealAt = 0.6f;

        void OnGUI()
        {
            if (!running || medal <= 0 || !tiedToResults || GameManager.HideHud) return;
            if (Event.current.type != EventType.Repaint) return;
            if (t < StampAt) return;
            GUI.depth = -15;
            UI.Styles();
            if (sMedal == null)
            {
                sMedal = new GUIStyle(UI.Title) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                sSeal = new GUIStyle(UI.Mid) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            }
            float W = Screen.width, H = Screen.height;
            // StageManager.DrawResults: panel min(580, W - 32) x 420, centred
            float pw = Mathf.Min(580f, W - 32f), ph = 420f, px = (W - pw) * 0.5f, py = Mathf.Max(10f, (H - ph) * 0.5f);
            float R = 74f;
            Vector2 c = new Vector2(px + pw - 26f, py + 30f);           // pinned over the top-right corner
            if (c.x + R > W - 6f) c.x = W - 6f - R;
            if (c.y - R < 4f) c.y = 4f + R;

            float a = t - StampAt;
            float slam = Mathf.Clamp01(a / StampSlam);
            float scale = Mathf.Lerp(2.8f, 1f, slam * slam);              // accelerates into the paper
            if (slam >= 1f) scale = 1f + 0.08f * Mathf.Exp(-(a - StampSlam) * 9f) * Mathf.Cos((a - StampSlam) * 30f);
            float alpha = Mathf.Clamp01(a / 0.08f);
            float rot = Mathf.Lerp(-32f, -10f, slam);
            Vector2 jitter = stampShake > 0f && !JuiceSettings.ReducedMotion
                ? new Vector2(Mathf.Sin(t * 91f), Mathf.Cos(t * 77f)) * 5f * stampShake : Vector2.zero;
            c += jitter;

            Matrix4x4 m0 = GUI.matrix;
            Color oc = GUI.color;
            // impact ring spreading out from the stamp
            if (slam >= 1f)
            {
                float ra = a - StampSlam;
                float rr = R * (1.05f + ra * 1.6f);
                float ringA = Mathf.Clamp01(0.5f - ra * 1.2f);
                if (ringA > 0f)
                {
                    GUI.color = new Color(1f, 0.92f, 0.7f, ringA);
                    GUI.DrawTexture(new Rect(c.x - rr, c.y - rr, rr * 2f, rr * 2f), JuiceTextures.Disc);
                }
            }
            GUIUtility.RotateAroundPivot(rot, c);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), c);
            Color mc = MedalColor(medal);
            // drop shadow, ribbon tails, disc
            GUI.color = new Color(0.15f, 0.06f, 0.1f, 0.45f * alpha);
            GUI.DrawTexture(new Rect(c.x - R + 6f, c.y - R + 8f, R * 2f, R * 2f), JuiceTextures.Disc);
            GUI.color = new Color(0.86f, 0.22f, 0.32f, alpha);
            GUI.DrawTexture(new Rect(c.x - R * 0.62f, c.y + R * 0.45f, R * 0.42f, R * 0.95f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x + R * 0.2f, c.y + R * 0.45f, R * 0.42f, R * 0.95f), Texture2D.whiteTexture);
            GUI.color = new Color(mc.r, mc.g, mc.b, alpha);
            GUI.DrawTexture(new Rect(c.x - R, c.y - R, R * 2f, R * 2f), JuiceTextures.Disc);
            UI.Label(new Rect(c.x - R, c.y - 30f, R * 2f, 34f), MedalNames[medal], sMedal, new Color(0.32f, 0.16f, 0.12f, alpha));
            string stars = medal == 3 ? "★ ★ ★" : medal == 2 ? "★ ★" : "★";
            UI.Label(new Rect(c.x - R, c.y + 2f, R * 2f, 28f), stars, sMedal, new Color(1f, 1f, 1f, 0.9f * alpha));
            GUI.matrix = m0;

            // red seal stamped across the lower-left of the medal
            float sa = t - SealAt;
            if (sa > 0f)
            {
                float sl = Mathf.Clamp01(sa / StampSlam);
                float ss = Mathf.Lerp(2.4f, 1f, sl * sl);
                if (sl >= 1f) ss = 1f + 0.06f * Mathf.Exp(-(sa - StampSlam) * 10f) * Mathf.Cos((sa - StampSlam) * 34f);
                float salpha = Mathf.Clamp01(sa / 0.06f) * 0.92f;
                Vector2 sc = c + new Vector2(-R * 0.78f, R * 0.62f);
                float S = 46f;
                GUIUtility.RotateAroundPivot(8f, sc);
                GUIUtility.ScaleAroundPivot(new Vector2(ss, ss), sc);
                GUI.color = new Color(0.82f, 0.1f, 0.14f, salpha);
                GUI.DrawTexture(new Rect(sc.x - S, sc.y - S, S * 2f, S * 2f), JuiceTextures.Seal);
                GUI.color = oc;
                UI.Label(new Rect(sc.x - S, sc.y - 24f, S * 2f, 22f), "BLOSSOM", sSeal, new Color(1f, 0.95f, 0.92f, salpha));
                UI.Label(new Rect(sc.x - S, sc.y + 0f, S * 2f, 22f), "RALLY", sSeal, new Color(1f, 0.95f, 0.92f, salpha));
                GUI.matrix = m0;
            }
            GUI.color = oc;
        }

        static Color MedalColor(int m)
        {
            switch (m)
            {
                case 3: return new Color(1f, 0.82f, 0.36f);
                case 2: return new Color(0.84f, 0.87f, 0.92f);
                default: return new Color(0.86f, 0.58f, 0.38f);
            }
        }
    }
}
