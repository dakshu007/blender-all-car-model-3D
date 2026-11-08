// ReplaySystem.cs - instant replay of the last 30 seconds of driving.
// Records the player car at 30 Hz into a ring buffer (root pose, wheel centres, speed / drift
// telemetry) and plays it back on the real car, frozen as a kinematic body, with 1x / 0.5x /
// 0.25x slow motion, pause, scrubbing and several cameras: auto-switching trackside cameras,
// chase, helicopter orbit, low bumper and a "drift cam" that cuts trackside while sliding.
// Keys: <- / -> seek, up / down speed, SPACE pause, C camera, ESC / Replay key exit.
// Gamepad: LB / RB seek, d-pad up / down speed, A pause, Y camera, B exit.
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(1000)]   // after ArcadeCar / CameraRig, so we record and pose the final transforms
    public class ReplaySystem : MonoBehaviour
    {
        public static ReplaySystem Instance { get; private set; }
        /// <summary>Sound hook: "Whoosh" (enter / exit replay), "Camera" (camera changed).</summary>
        public static System.Action<string> OnSound;

        public const float BufferSeconds = 30f;
        public const float RecordRate = 30f;
        const int MaxWheels = 6;
        const int Capacity = (int)(BufferSeconds * RecordRate) + 8;

        enum ReplayCam { Drift, Trackside, Chase, Helicopter, Bumper }
        static readonly string[] CamNames = { "Drift cam", "Trackside", "Chase", "Helicopter", "Bumper" };
        static readonly float[] Speeds = { 1f, 0.5f, 0.25f };
        static readonly string[] SpeedNames = { "1×", "0.5×", "0.25×" };

        struct Frame
        {
            public float t;
            public Vector3 pos;
            public Quaternion rot;
            public float kmh, drift, fwdSpeed, steer;
        }

        // ---- recording ----
        readonly Frame[] frames = new Frame[Capacity];
        readonly Vector3[] wheelLocal = new Vector3[Capacity * MaxWheels];   // wheel centres in car space
        int head, count;
        float recClock, lastRecT = -999f;
        ArcadeCar recCar;
        int wheelCount;

        // ---- playback ----
        public bool Active { get; private set; }
        /// <summary>True when there is enough recorded driving to replay.</summary>
        public bool HasRecording { get { return count >= 15; } }
        /// <summary>True while active and for a frame after exiting: GameManager should skip its own
        /// driving keys (ESC, C, R...) while this is set, so the exit key doesn't also open the pause menu.</summary>
        public static bool BlockingInput
        {
            get { return Instance != null && (Instance.Active || Time.frameCount - Instance.toggleFrame <= 1); }
        }

        /// <summary>Recorded telemetry at the current playback time (for engine sound / effects while Active).</summary>
        public float PlaybackKmh { get { return Active ? cur.kmh : 0f; } }
        public float PlaybackDrift { get { return Active ? cur.drift : 0f; } }
        /// <summary>Current playback rate (0 while paused).</summary>
        public float PlaybackRate { get { return Active && !paused ? Speeds[speedIdx] : 0f; } }

        ArcadeCar car;
        Rigidbody rb;
        CameraRig rig;
        Camera cam;
        float startT, endT, playT, prevPlayT;
        int speedIdx;
        bool paused;
        int toggleFrame = -10;
        bool padArmed;
        float seekHold;
        ReplayCam camMode = ReplayCam.Drift;
        ReplayCam shot = ReplayCam.Chase;      // what the drift cam is currently showing
        float shotTime;
        float driftSmooth;
        float[] spin = new float[MaxWheels];
        Frame cur;
        readonly Vector3[] curWheels = new Vector3[MaxWheels];

        // camera state
        Vector3 camVel;
        Vector3 chaseDir = Vector3.forward;
        float heliAngle;
        Vector3 tsPos;
        bool tsValid;
        float tsBlocked, tsCheck;
        bool cut = true;
        float savedFov;

        // saved car state
        Vector3 physPos, savedVel, savedAngVel;
        Quaternion physRot;
        bool savedKinematic, savedEnabled, savedControls;
        RigidbodyInterpolation savedInterp;
        CollisionDetectionMode savedCdm;
        float sThrottle, sBrake, sSteer, sHandbrake;
        struct WheelState
        {
            public bool grounded;
            public float lateralSlip, load, springLength, spin;
            public Vector3 contact, normal, visLocalPos;
            public Quaternion visLocalRot;
            public Surface surface;
        }
        WheelState[] savedWheels = new WheelState[0];

        // UI
        GUIStyle sBig, sMid, sSmall;
        bool draggingTimeline;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDisable()
        {
            if (Active) End();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        static void Sound(string id)
        {
            if (OnSound != null) OnSound(id);
        }

        // ------------------------------------------------------------------ recording
        void Clear()
        {
            head = count = 0;
            lastRecT = -999f;
            recClock = 0f;
        }

        Frame F(int i) { return frames[(head + i) % Capacity]; }
        int Phys(int i) { return (head + i) % Capacity; }

        void Record(ArcadeCar c)
        {
            int idx;
            if (count < Capacity) { idx = (head + count) % Capacity; count++; }
            else { idx = head; head = (head + 1) % Capacity; }
            Transform t = c.transform;
            Frame f;
            f.t = recClock;
            f.pos = t.position;
            f.rot = t.rotation;
            f.kmh = c.SpeedKmh;
            f.drift = c.DriftIntensity;
            f.fwdSpeed = c.ForwardSpeed;
            f.steer = c.SteerAngle;
            frames[idx] = f;
            Quaternion inv = Quaternion.Inverse(f.rot);
            wheelCount = Mathf.Min(c.wheels.Length, MaxWheels);
            for (int w = 0; w < wheelCount; w++)
            {
                var wh = c.wheels[w];
                Vector3 world = wh.visual != null ? wh.visual.position : t.TransformPoint(wh.local);
                wheelLocal[idx * MaxWheels + w] = inv * (world - f.pos);
            }
            lastRecT = recClock;
            // drop anything older than the buffer length
            while (count > 1 && recClock - F(0).t > BufferSeconds) { head = (head + 1) % Capacity; count--; }
        }

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (Active)
            {
                if (gm == null || car == null || rb == null || gm.Car != car || rig == null || cam == null) { Abort(); return; }
                Playback();
                return;
            }
            ArcadeCar c = gm != null ? gm.Car : null;
            if (c != recCar)
            {
                Clear();
                recCar = c;
                wheelCount = c != null ? Mathf.Min(c.wheels.Length, MaxWheels) : 0;
            }
            if (c == null || !gm.IsDriving || Time.deltaTime <= 0f) return;
            if (PhotoMode.Instance != null && PhotoMode.Instance.Active) return;
            recClock += Time.deltaTime;
            if (recClock - lastRecT >= 1f / RecordRate - 0.0005f) Record(c);
        }

        // largest logical index whose time is <= t
        int FindIndex(float t)
        {
            int lo = 0, hi = count - 1;
            if (t <= F(0).t) return 0;
            if (t >= F(hi).t) return hi;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (F(mid).t <= t) lo = mid; else hi = mid;
            }
            return lo;
        }

        void Sample(float t, ref Frame f, Vector3[] wheels)
        {
            int i = FindIndex(t);
            int j = Mathf.Min(i + 1, count - 1);
            Frame a = F(i), b = F(j);
            float u = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 0f;
            if ((b.pos - a.pos).sqrMagnitude > 25f * 25f) u = u < 0.5f ? 0f : 1f;     // teleport: cut, don't slide
            f.t = t;
            f.pos = Vector3.LerpUnclamped(a.pos, b.pos, u);
            f.rot = Quaternion.Slerp(a.rot, b.rot, u);
            f.kmh = Mathf.Lerp(a.kmh, b.kmh, u);
            f.drift = Mathf.Lerp(a.drift, b.drift, u);
            f.fwdSpeed = Mathf.Lerp(a.fwdSpeed, b.fwdSpeed, u);
            f.steer = Mathf.Lerp(a.steer, b.steer, u);
            if (wheels != null)
            {
                int pa = Phys(i) * MaxWheels, pb = Phys(j) * MaxWheels;
                for (int w = 0; w < wheelCount; w++) wheels[w] = Vector3.LerpUnclamped(wheelLocal[pa + w], wheelLocal[pb + w], u);
            }
        }

        Vector3 SamplePos(float t)
        {
            Frame f = default(Frame);
            Sample(Mathf.Clamp(t, startT, endT), ref f, null);
            return f.pos;
        }

        // ------------------------------------------------------------------ begin / end
        /// <summary>Start the replay (does nothing if photo mode is active or nothing was recorded).</summary>
        public void Begin()
        {
            if (Active) return;
            if (PhotoMode.Instance != null && PhotoMode.Instance.Active) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.Car == null || gm.Rig == null || gm.Car.Body == null) return;
            if (gm.Car != recCar || !HasRecording) return;
            car = gm.Car;
            rb = car.Body;
            rig = gm.Rig;
            cam = rig.GetComponent<Camera>();
            if (cam == null) return;

            // remember the exact car state
            physPos = rb.position;
            physRot = rb.rotation;
            savedKinematic = rb.isKinematic;
            savedVel = savedKinematic ? Vector3.zero : rb.GetVelocity();
            savedAngVel = savedKinematic ? Vector3.zero : rb.angularVelocity;
            savedInterp = rb.interpolation;
            savedCdm = rb.collisionDetectionMode;
            savedEnabled = car.enabled;
            savedControls = car.controlsEnabled;
            sThrottle = car.throttle; sBrake = car.brake; sSteer = car.steerInput; sHandbrake = car.handbrake;
            savedWheels = new WheelState[car.wheels.Length];
            for (int w = 0; w < car.wheels.Length; w++)
            {
                var wh = car.wheels[w];
                var s = new WheelState
                {
                    grounded = wh.grounded, lateralSlip = wh.lateralSlip, load = wh.load, springLength = wh.springLength,
                    spin = wh.spin, contact = wh.contact, normal = wh.normal, surface = wh.surface,
                    visLocalPos = Vector3.zero, visLocalRot = Quaternion.identity,
                };
                if (wh.visual != null)
                {
                    s.visLocalPos = Quaternion.Inverse(car.transform.rotation) * (wh.visual.position - car.transform.position);
                    s.visLocalRot = Quaternion.Inverse(car.transform.rotation) * wh.visual.rotation;
                }
                savedWheels[w] = s;
                if (w < MaxWheels) spin[w] = wh.spin;
                // CarFX reads these: an airborne, non-sliding wheel emits no smoke / skids during the replay
                wh.grounded = false;
                wh.lateralSlip = 0f;
            }

            // freeze it: the car script stops (its state stays intact), the body becomes kinematic
            car.controlsEnabled = false;
            car.enabled = false;
            rb.interpolation = RigidbodyInterpolation.None;
            if (!savedKinematic)
            {
                rb.SetVelocity(Vector3.zero);
                rb.angularVelocity = Vector3.zero;
            }
            if (rb.collisionDetectionMode == CollisionDetectionMode.Continuous || rb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.isKinematic = true;

            savedFov = cam.fieldOfView;
            rig.externalControl = true;
            GameManager.HideHud = true;

            startT = F(0).t;
            endT = F(count - 1).t;
            playT = prevPlayT = startT;
            speedIdx = 0;
            paused = false;
            padArmed = false;
            seekHold = 0f;
            shotTime = 0f;
            driftSmooth = 0f;
            shot = ReplayCam.Chase;
            tsValid = false;
            cut = true;
            Active = true;
            toggleFrame = Time.frameCount;
            Sample(playT, ref cur, curWheels);
            PoseCar(0f);
            UpdateCamera(0f, 0f);
            Sound("Whoosh");
        }

        /// <summary>Stop the replay and put the car back exactly as it was.</summary>
        public void End()
        {
            if (!Active) return;
            Active = false;
            toggleFrame = Time.frameCount;
            if (car != null && rb != null)
            {
                car.transform.SetPositionAndRotation(physPos, physRot);
                rb.position = physPos;
                rb.rotation = physRot;
                for (int w = 0; w < car.wheels.Length && w < savedWheels.Length; w++)
                {
                    var wh = car.wheels[w];
                    var s = savedWheels[w];
                    wh.grounded = s.grounded; wh.lateralSlip = s.lateralSlip; wh.load = s.load; wh.springLength = s.springLength;
                    wh.spin = s.spin; wh.contact = s.contact; wh.normal = s.normal; wh.surface = s.surface;
                    if (wh.visual != null)
                    {
                        wh.visual.position = physPos + physRot * s.visLocalPos;
                        wh.visual.rotation = physRot * s.visLocalRot;
                    }
                }
                rb.isKinematic = savedKinematic;
                rb.collisionDetectionMode = savedCdm;
                rb.interpolation = savedInterp;
                if (!rb.isKinematic)
                {
                    rb.SetVelocity(savedVel);
                    rb.angularVelocity = savedAngVel;
                }
                car.throttle = sThrottle; car.brake = sBrake; car.steerInput = sSteer; car.handbrake = sHandbrake;
                car.controlsEnabled = savedControls;
                car.enabled = savedEnabled;
            }
            RestoreView();
            Sound("Whoosh");
        }

        void RestoreView()
        {
            if (cam != null) cam.fieldOfView = savedFov;
            if (rig != null)
            {
                rig.externalControl = false;
                rig.SnapBehind();
            }
            GameManager.HideHud = false;
        }

        // the car, rig or game went away mid-replay: leave cleanly
        void Abort()
        {
            if (car != null && rb != null) { End(); return; }      // car still alive (e.g. swapped): restore it fully
            Active = false;
            toggleFrame = Time.frameCount;
            car = null;
            rb = null;
            RestoreView();
            Clear();
            recCar = null;
        }

        // ------------------------------------------------------------------ playback
        void Playback()
        {
            float rdt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            HandleInput(rdt);
            if (!Active) return;

            if (!paused && !draggingTimeline) playT += rdt * Speeds[speedIdx];
            if (playT > endT)
            {
                playT = startT;          // loop
                cut = true;
                tsValid = false;
            }
            playT = Mathf.Clamp(playT, startT, endT);
            // replay time since the last posed frame (includes seeking / timeline dragging)
            float dPlay = playT - prevPlayT;
            if (Mathf.Abs(dPlay) > 0.5f) { cut = true; tsValid = false; dPlay = 0f; }
            prevPlayT = playT;
            Sample(playT, ref cur, curWheels);
            PoseCar(dPlay);
            UpdateCamera(rdt, dPlay);
        }

        void PoseCar(float dPlay)
        {
            car.transform.SetPositionAndRotation(cur.pos, cur.rot);
            rb.position = cur.pos;
            rb.rotation = cur.rot;
            for (int w = 0; w < wheelCount && w < car.wheels.Length; w++)
            {
                var wh = car.wheels[w];
                if (wh.visual == null) continue;
                spin[w] = Mathf.Repeat(spin[w] + cur.fwdSpeed / Mathf.Max(0.1f, wh.radius) * Mathf.Rad2Deg * dPlay, 360f);
                float steer = wh.front ? cur.steer : 0f;
                wh.visual.position = cur.pos + cur.rot * curWheels[w];
                wh.visual.rotation = cur.rot * Quaternion.AngleAxis(steer, Vector3.up) *
                                     Quaternion.AngleAxis(spin[w], Vector3.right) * wh.restLocal;
            }
        }

        void HandleInput(float rdt)
        {
            if (Time.frameCount == toggleFrame) return;     // the key that opened the replay
            if (InputReader.Down(KeyCode.Escape) || GamepadBridge.ButtonDown(PadButton.East) ||
                InputReader.Down(GameSettings.Binding(GameAction.Replay)))
            {
                End();
                return;
            }
            if (InputReader.Down(KeyCode.Space) || GamepadBridge.ButtonDown(PadButton.South)) paused = !paused;
            if (InputReader.Down(KeyCode.UpArrow) || GamepadBridge.ButtonDown(PadButton.DpadUp)) speedIdx = Mathf.Max(0, speedIdx - 1);
            if (InputReader.Down(KeyCode.DownArrow) || GamepadBridge.ButtonDown(PadButton.DpadDown)) speedIdx = Mathf.Min(Speeds.Length - 1, speedIdx + 1);
            if (InputReader.ActionDown(GameAction.Camera))
            {
                camMode = (ReplayCam)(((int)camMode + 1) % CamNames.Length);
                cut = true;
                tsValid = false;
                shotTime = 0f;
                Sound("Camera");
            }

            bool lb = GamepadBridge.Button(PadButton.LeftShoulder), rbt = GamepadBridge.Button(PadButton.RightShoulder);
            if (!lb && !rbt) padArmed = true;           // LB opened the replay: wait for it to be released
            float dir = 0f;
            bool tapBack = InputReader.Down(KeyCode.LeftArrow) || GamepadBridge.ButtonDown(PadButton.DpadLeft) || (padArmed && GamepadBridge.ButtonDown(PadButton.LeftShoulder));
            bool tapFwd = InputReader.Down(KeyCode.RightArrow) || GamepadBridge.ButtonDown(PadButton.DpadRight) || (padArmed && GamepadBridge.ButtonDown(PadButton.RightShoulder));
            if (InputReader.Key(KeyCode.LeftArrow) || GamepadBridge.Button(PadButton.DpadLeft) || (padArmed && lb)) dir -= 1f;
            if (InputReader.Key(KeyCode.RightArrow) || GamepadBridge.Button(PadButton.DpadRight) || (padArmed && rbt)) dir += 1f;
            if (tapBack) playT -= 1f;
            if (tapFwd) playT += 1f;
            if (dir != 0f)
            {
                seekHold += rdt;
                if (seekHold > 0.3f) playT += dir * 5f * rdt;   // hold to scrub
            }
            else seekHold = 0f;
            if (tapBack || tapFwd) { tsValid = false; }
            if (playT > endT) playT = endT;
            if (playT < startT) playT = startT;
        }

        // ------------------------------------------------------------------ cameras
        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
        }

        static int GroundMask
        {
            get { return Physics.DefaultRaycastLayers & ~((1 << WorldBuilder.LayerGrass) | (1 << WorldBuilder.LayerTrees)); }
        }

        void UpdateCamera(float rdt, float dPlay)
        {
            float pdt = Mathf.Abs(dPlay);            // replay-time step (0 while paused)
            driftSmooth = Mathf.Lerp(driftSmooth, cur.drift, 1f - Mathf.Exp(-(pdt + rdt * 0.2f) * 4f));
            shotTime += rdt;

            ReplayCam want = camMode;
            if (camMode == ReplayCam.Drift)
            {
                want = shot;
                if (shot != ReplayCam.Trackside && driftSmooth > 0.35f && shotTime > 1.2f) want = ReplayCam.Trackside;
                else if (shot == ReplayCam.Trackside && driftSmooth < 0.12f && shotTime > 2.5f) want = ReplayCam.Chase;
                if (want != shot) { shot = want; shotTime = 0f; cut = true; tsValid = false; }
            }

            Vector3 carPos = cur.pos;
            Vector3 look = carPos + Vector3.up * 0.8f;
            Vector3 vel = (cur.pos - SamplePos(playT - 0.1f)) / 0.1f;
            Vector3 pos = cam.transform.position;
            Quaternion rot = cam.transform.rotation;
            float fov = cam.fieldOfView;
            float k = 1f - Mathf.Exp(-rdt * 8f);

            switch (want)
            {
                case ReplayCam.Trackside:
                {
                    float d = Vector3.Distance(tsPos, carPos);
                    if (!tsValid || d > 60f) { PlaceTrackside(); cut = true; d = Vector3.Distance(tsPos, carPos); }
                    // switch away if the view has been blocked for a while
                    tsCheck -= rdt;
                    if (tsCheck <= 0f)
                    {
                        tsCheck = 0.25f;
                        tsBlocked = Blocked(tsPos, look) ? tsBlocked + 0.25f : 0f;
                        if (tsBlocked > 0.75f) { tsBlocked = 0f; PlaceTrackside(); cut = true; d = Vector3.Distance(tsPos, carPos); }
                    }
                    pos = tsPos;
                    Quaternion wantRot = Quaternion.LookRotation(look - tsPos);
                    rot = cut ? wantRot : Quaternion.Slerp(rot, wantRot, 1f - Mathf.Exp(-rdt * 10f));
                    float wantFov = Mathf.Clamp(2f * Mathf.Atan(4.2f / Mathf.Max(d, 1f)) * Mathf.Rad2Deg, 14f, 55f);
                    fov = cut ? wantFov : Mathf.Lerp(fov, wantFov, k);
                    break;
                }
                case ReplayCam.Chase:
                {
                    Vector3 fwd = Flat(cur.rot * Vector3.forward);
                    Vector3 wantDir = vel.magnitude > 5f ? Vector3.Slerp(fwd, Flat(vel), 0.4f + 0.45f * driftSmooth).normalized : fwd;
                    chaseDir = cut ? wantDir : Vector3.Slerp(chaseDir, wantDir, 1f - Mathf.Exp(-pdt * 4f)).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, chaseDir).normalized;
                    float side = Mathf.Clamp(Vector3.SignedAngle(fwd, Flat(vel), Vector3.up) / 30f, -1f, 1f) * driftSmooth;
                    Vector3 target = carPos - chaseDir * (6.8f + Mathf.Clamp01(cur.kmh / 200f) * 1.6f) + right * side * 1.8f + Vector3.up * 2.3f;
                    target = AvoidGeometry(look, target);
                    if (cut) { pos = target; camVel = Vector3.zero; }
                    else if (pdt > 0f) pos = Vector3.SmoothDamp(pos, target, ref camVel, 0.14f, Mathf.Infinity, pdt);
                    rot = Quaternion.LookRotation(look + vel * 0.05f - pos) * Quaternion.Euler(0f, 0f, side * 3f);
                    float wantFov = 58f + Mathf.Clamp01(cur.kmh / 190f) * 12f + driftSmooth * 5f;
                    fov = cut ? wantFov : Mathf.Lerp(fov, wantFov, k);
                    break;
                }
                case ReplayCam.Helicopter:
                {
                    if (cut) heliAngle = Mathf.Atan2(-vel.x, -vel.z) * Mathf.Rad2Deg;
                    heliAngle += (pdt + rdt * 0.15f) * 16f;
                    Vector3 target = carPos + Quaternion.Euler(0f, heliAngle, 0f) * new Vector3(0f, 0f, -17f) + Vector3.up * 10f;
                    target = AvoidGeometry(look, target);
                    pos = cut ? target : Vector3.Lerp(pos, target, 1f - Mathf.Exp(-rdt * 6f));
                    rot = Quaternion.LookRotation(look - pos);
                    fov = cut ? 44f : Mathf.Lerp(fov, 44f, k);
                    break;
                }
                case ReplayCam.Bumper:
                {
                    float front = 1.8f;
                    for (int w = 0; w < wheelCount; w++) front = Mathf.Max(front, curWheels[w].z + 0.85f);
                    pos = carPos + cur.rot * new Vector3(0f, 0.42f, front);
                    rot = cur.rot * Quaternion.Euler(2f, 0f, 0f);
                    fov = cut ? 72f : Mathf.Lerp(fov, 72f, k);
                    break;
                }
            }
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            cut = false;
        }

        Vector3 AvoidGeometry(Vector3 look, Vector3 desired)
        {
            Vector3 dir = desired - look;
            float len = dir.magnitude;
            if (len < 0.01f) return desired;
            RaycastHit hit;
            if (Physics.SphereCast(look, 0.3f, dir / len, out hit, len, GroundMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != rb)
                return look + dir / len * Mathf.Max(1.5f, hit.distance - 0.2f);
            return desired;
        }

        bool Blocked(Vector3 from, Vector3 to)
        {
            RaycastHit hit;
            return Physics.Linecast(from, to, out hit, GroundMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != rb;
        }

        // pick a spot 15-30 m to the side of the recorded path, a little ahead of the car
        void PlaceTrackside()
        {
            int i = FindIndex(playT);
            int step = 1;
            float ahead = Random.Range(18f, 34f), acc = 0f;
            int j = i;
            while (true)
            {
                int n = j + step;
                if (n < 0 || n >= count) break;
                acc += Vector3.Distance(F(j).pos, F(n).pos);
                j = n;
                if (acc >= ahead) break;
            }
            Vector3 p = F(j).pos;
            Vector3 dir = Flat(F(Mathf.Min(j + 1, count - 1)).pos - F(Mathf.Max(j - 1, 0)).pos);
            if (j == i) dir = Flat(cur.rot * Vector3.forward);
            if (acc < 5f) p = cur.pos + dir * ahead;       // end of the buffer: extrapolate along the heading
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 target = p + Vector3.up * 0.8f;
            float firstSide = Random.value < 0.5f ? -1f : 1f;
            Vector3 best = p + right * firstSide * 20f + Vector3.up * 1.5f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float side = (attempt % 2 == 0) ? firstSide : -firstSide;
                Vector3 c = p + right * side * Random.Range(15f, 30f) + dir * Random.Range(-6f, 8f);
                RaycastHit hit;
                if (Physics.Raycast(c + Vector3.up * 80f, Vector3.down, out hit, 200f, GroundMask, QueryTriggerInteraction.Ignore) && hit.rigidbody != rb)
                    c.y = hit.point.y + Random.Range(0.8f, 2.2f);
                else
                    c.y = p.y + 1.5f;
                if (attempt == 0) best = c;
                if (!Blocked(c, target) && !Blocked(c, cur.pos + Vector3.up * 0.8f)) { best = c; break; }
            }
            tsPos = best;
            tsValid = true;
            tsBlocked = 0f;
            tsCheck = 0.25f;
        }

        // ------------------------------------------------------------------ HUD
        void Styles()
        {
            if (sBig != null) return;
            sBig = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sMid = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sSmall = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
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

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (!Active) return;
            Styles();
            GUI.depth = -5;
            float W = Screen.width, H = Screen.height;
            Color white = new Color(1f, 0.97f, 0.96f, 1f);
            Color pink = new Color(1f, 0.72f, 0.82f, 1f);

            // title
            Panel(new Rect(20, 20, 300, 74), 0.45f);
            string state = paused ? "REPLAY   ||  PAUSED" : "REPLAY   ►  " + SpeedNames[speedIdx];
            Label(new Rect(34, 24, 290, 38), state, sBig, pink);
            string camName = CamNames[(int)camMode] + (camMode == ReplayCam.Drift ? "  ·  " + CamNames[(int)shot].ToLowerInvariant() : "");
            Label(new Rect(34, 60, 290, 26), camName, sMid, white);

            // speed readout
            Label(new Rect(W - 250, 24, 230, 40), Mathf.RoundToInt(cur.kmh) + " km/h", new GUIStyle(sBig) { alignment = TextAnchor.MiddleRight }, white);

            // timeline
            float bw = Mathf.Min(W * 0.6f, 760f), bx = (W - bw) * 0.5f, by = H - 78f;
            Rect bar = new Rect(bx, by, bw, 12);
            Panel(new Rect(bx - 6, by - 6, bw + 12, 24), 0.5f);
            float span = Mathf.Max(0.01f, endT - startT);
            float frac = Mathf.Clamp01((playT - startT) / span);
            Fill(new Rect(bx, by, bw * frac, 12), new Color(pink.r, pink.g, pink.b, 0.9f));
            Fill(new Rect(bx + bw * frac - 2f, by - 5, 4, 22), white);
            Label(new Rect(bx - 90, by - 8, 80, 28), "-" + (endT - playT).ToString("0.0") + " s", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleRight }, white);
            Label(new Rect(bx + bw + 10, by - 8, 80, 28), span.ToString("0") + " s", new GUIStyle(sSmall) { alignment = TextAnchor.MiddleLeft }, white);

            // click / drag on the timeline to scrub
            Event e = Event.current;
            Rect hitR = new Rect(bx - 6, by - 10, bw + 12, 32);
            if (e.type == EventType.MouseDown && e.button == 0 && hitR.Contains(e.mousePosition)) draggingTimeline = true;
            if (e.type == EventType.MouseUp && e.button == 0) draggingTimeline = false;
            if (draggingTimeline && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                playT = Mathf.Lerp(startT, endT, Mathf.Clamp01((e.mousePosition.x - bx) / bw));
                e.Use();
            }

            string hint = InputReader.PadConnected
                ? "LB / RB seek     D-pad ↑ ↓ speed     A pause     Y camera     B exit"
                : "← → seek     ↑ ↓ speed     SPACE pause     C camera     ESC exit";
            Label(new Rect(0, by + 22, W, 26), hint, sSmall, white);
        }
    }
}
