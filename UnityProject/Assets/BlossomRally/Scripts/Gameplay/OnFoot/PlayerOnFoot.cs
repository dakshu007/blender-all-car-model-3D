// PlayerOnFoot.cs - the player's person: getting out of / into vehicles and the third-person controller.
//  * F / gamepad Y in the car (slow enough, not during an event): the car parks (handbrake = kinematic, engine
//    and lights off, cockpit crew hidden) and the person slides out beside the driver door (other side if blocked).
//  * On foot: camera-relative walk (2.5 m/s), sprint (Shift / LB, 6.2 m/s), jump (Space / A), CharacterController
//    slopes and steps, slides off too-steep slopes, no swimming (pushed back out of deep water), R / Select puts
//    the person on the nearest road. Camera: CameraRig.person orbit (mouse / right stick).
//  * F near the current car or any registered vehicle (PlayerVehicles): walk to the door, slide into the seat,
//    the car un-parks and the crew (or, for vehicles without a cockpit crew, the visible rider) takes over.
//  * GameManager calls ForceIntoCar() before fast travel, events, the garage and the title screen.
// Keeps PlayerState (OnFoot, Person, InTransition, OnFootChanged) correct.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    [DefaultExecutionOrder(50)]
    public sealed class PlayerOnFoot : MonoBehaviour
    {
        public static PlayerOnFoot Instance { get; private set; }

        enum Phase { InVehicle, Exiting, OnFoot, WalkToDoor, SlideIn }

        const float WalkSpeed = 2.5f, SprintSpeed = 6.2f, Gravity = -20f, JumpSpeed = 5.6f;
        /// <summary>The player's person lives on the built-in "Ignore Raycast" layer; our probes mask it out.</summary>
        internal const int PersonLayer = 2;
        internal const int Probe = ~(1 << PersonLayer);
        const float ExitTime = 0.75f, SlideTime = 0.65f, MaxExitKmh = 12f, EnterRange = 1.7f;

        Phase phase = Phase.InVehicle;
        PersonRig rig;
        CharacterController cc;
        ArcadeCar vehicle;            // being entered / exited
        ArcadeCar riding;             // vehicle with a visible rider (no cockpit crew)
        ArcadeCar parking;            // settling before it freezes
        float parkT;
        Seat seat;
        float t;
        Vector3 fromPos, toPos;
        float fromYaw, toYaw;
        bool posed;

        Vector3 hv;                   // horizontal velocity
        float vy, airTime, coyote;
        bool grounded;
        Vector3 lastSafe;
        float safeTimer, waterToast, slideK;
        bool cursorLocked;
        Light parkedLight;
        ArcadeCar parkedLightCar;

        /// <summary>The vehicle the "F  Get in" prompt refers to (null when none in reach).</summary>
        public ArcadeCar NearVehicle { get; private set; }
        public string NearVehicleName { get; private set; }
        /// <summary>True while the person can be steered (on foot, no door animation).</summary>
        public bool Walking { get { return phase == Phase.OnFoot; } }
        public bool Busy { get { return phase == Phase.Exiting || phase == Phase.WalkToDoor || phase == Phase.SlideIn; } }
        public PersonRig Rig { get { return rig; } }

        struct Seat
        {
            public Vector3 seatLocal, doorL, doorR;
            public PersonPose pose;
            public float seatHeight;
            public bool crew;
        }

        static bool booted, toldControls;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            WorldHooks.WorldBuilt += w => Ensure();
        }

        public static PlayerOnFoot Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("PlayerOnFoot");
                Instance = go.AddComponent<PlayerOnFoot>();
                go.AddComponent<InteractionSystem>().useGUILayout = false;
            }
            return Instance;
        }

        /// <summary>Static helper for the game manager: enter the current car at once (no-op when already in).</summary>
        public static void ForceIntoCarIfOnFoot()
        {
            if (Instance != null) Instance.ForceIntoCar();
        }

        public static bool OnFootOrBusy { get { return Instance != null && Instance.phase != Phase.InVehicle; } }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (cursorLocked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        // ================================================================== helpers
        internal static bool GameplayActive(GameManager gm)
        {
            if (gm == null || !gm.IsDriving || GameManager.HideHud) return false;
            if (ValleyMap.IsOpen || PhotoAlbum.IsOpen || GameplayHooks.MenuOpen) return false;
            if (SettingsMenu.Instance != null && SettingsMenu.Instance.IsOpen) return false;
            if (ReplaySystem.BlockingInput || PhotoMode.BlockingInput) return false;
            if (StageManager.Instance != null && StageManager.Instance.ShowingResults) return false;
            return true;
        }

        static void Toast(string s)
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.Toast(s);
        }

        static float Yaw(Transform tr)
        {
            Vector3 f = tr.forward; f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : tr.eulerAngles.y;
        }

        PersonLook LookFor(ArcadeCar v)
        {
            var l = PersonLook.Player;
            var det = v != null ? v.GetComponent<CarDetails>() : null;
            if (det != null && det.a != null)
            {
                l.top = l.bottom = det.suit;
                l.accent = det.accent;
            }
            return l;
        }

        void EnsureRig(PersonLook look)
        {
            if (rig == null)
            {
                rig = PersonFactory.Create(look, Vector3.zero, 0f, null, PersonLayer);
                rig.name = "PlayerPerson";
                rig.hero = true;
                cc = rig.gameObject.AddComponent<CharacterController>();
                cc.height = 1.78f * rig.Scale;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, cc.height * 0.5f + 0.02f, 0f);
                cc.slopeLimit = 50f;
                cc.stepOffset = 0.38f;
                cc.skinWidth = 0.04f;
                cc.minMoveDistance = 0f;
                cc.enabled = false;
            }
            else rig.SetLook(look);
            rig.gameObject.SetActive(true);
        }

        Seat SeatOf(ArcadeCar v)
        {
            var s = new Seat();
            var e = PlayerVehicles.Find(v);
            s.pose = e != null ? e.pose : PersonPose.Drive;
            var det = v.GetComponent<CarDetails>();
            bool anatomy = det != null && det.a != null;
            s.crew = anatomy && s.pose == PersonPose.Drive;
            var box = v.GetComponent<BoxCollider>();
            float halfW = box != null ? box.size.x * 0.5f : 0.9f, cx = box != null ? box.center.x : 0f;
            if (e != null && e.seatLocal.HasValue) s.seatLocal = e.seatLocal.Value;
            else if (anatomy) s.seatLocal = new Vector3(det.a.hipL.x, det.a.seatTopY - 0.30f, det.a.hipL.z - 0.02f);
            else if (s.pose == PersonPose.Ride) s.seatLocal = new Vector3(0f, 0f, -0.12f);
            else s.seatLocal = new Vector3(v.DriverEye.x, v.DriverEye.y - 0.98f, v.DriverEye.z - 0.15f);
            s.seatHeight = e != null && e.seatHeight >= 0f ? e.seatHeight : (s.pose == PersonPose.Ride ? 0.78f : 0.30f);
            float side = s.pose == PersonPose.Ride ? 0.75f : halfW + 0.55f;
            if (e != null && e.doorLocal.HasValue) s.doorL = e.doorLocal.Value;
            else s.doorL = new Vector3(cx - side, 0f, s.seatLocal.z + (s.pose == PersonPose.Ride ? 0f : 0.15f));
            s.doorR = new Vector3(2f * cx - s.doorL.x, s.doorL.y, s.doorL.z);
            return s;
        }

        static float GroundY(Vector3 p, ArcadeCar ignore)
        {
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out hit, 8f, Probe, QueryTriggerInteraction.Ignore)
                && (ignore == null || hit.rigidbody != ignore.Body))
                return hit.point.y;
            var gm = GameManager.Instance;
            return gm != null && gm.World != null ? gm.World.TerrainHeight(p.x, p.z) : p.y;
        }

        static bool Free(Vector3 feet)
        {
            return !Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.5f, 0.28f, Probe, QueryTriggerInteraction.Ignore);
        }

        /// <summary>A free standing spot beside the vehicle (driver side first), on the ground.</summary>
        bool DoorSpot(ArcadeCar v, Seat s, out Vector3 spot)
        {
            Transform tr = v.transform;
            Vector3 back = new Vector3(0f, 0f, -(s.pose == PersonPose.Ride ? 1.4f : 2.9f));
            Vector3 c0 = tr.TransformPoint(s.doorL), c1 = tr.TransformPoint(s.doorR), c2 = tr.TransformPoint(back);
            Vector3 c3 = tr.TransformPoint(s.doorL + new Vector3(-0.6f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = i == 0 ? c0 : i == 1 ? c1 : i == 2 ? c2 : c3;
                c.y = GroundY(c, v) + 0.03f;
                if (Free(c)) { spot = c; return true; }
            }
            spot = c0;
            spot.y = GroundY(c0, v) + 0.03f;
            return false;
        }

        // ================================================================== parking
        void Park(ArcadeCar v)
        {
            if (v == null) return;
            v.controlsEnabled = false;
            parking = v;
            parkT = 0f;
        }

        static void Freeze(ArcadeCar v)
        {
            var rb = v.Body;
            if (rb != null)
            {
                if (!rb.isKinematic) { rb.SetVelocity(Vector3.zero); rb.angularVelocity = Vector3.zero; }
                rb.isKinematic = true;
            }
            v.enabled = false;            // engine silent, no physics step
        }

        void Unpark(ArcadeCar v)
        {
            if (v == null) return;
            if (parking == v) parking = null;
            v.enabled = true;
            var rb = v.Body;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.SetVelocity(Vector3.zero);
                rb.angularVelocity = Vector3.zero;
            }
            v.ResetDrift();
            v.controlsEnabled = true;
        }

        void UpdateParking(float dt)
        {
            if (parking == null) return;
            var gm = GameManager.Instance;
            if (gm != null && gm.Car == parking && phase == Phase.InVehicle) { parking = null; return; }
            parkT += dt;
            var rb = parking.Body;
            float sp = rb != null && !rb.isKinematic ? rb.GetVelocity().magnitude : 0f;
            if (rb == null || rb.isKinematic || (sp < 0.35f && parkT > 0.4f) || parkT > 3f)
            {
                Freeze(parking);
                parking = null;
            }
        }

        // ================================================================== get out
        /// <summary>Leave the current vehicle (instant = no animation).</summary>
        public bool ExitVehicle(bool instant)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Car == null || phase != Phase.InVehicle) return false;
            var v = gm.Car;
            var st = StageManager.Instance;
            if (st != null && st.Running) { Toast("Finish or quit the event to get out"); return false; }
            if (!instant && v.SpeedKmh > MaxExitKmh) { Toast("Slow down to get out"); return false; }
            seat = SeatOf(v);
            Vector3 spot;
            if (!DoorSpot(v, seat, out spot) && !instant) { Toast("No room to get out here"); return false; }

            Park(v);
            if (seat.crew) PlayerState.SetVehicleEmpty(v, true);
            EnsureRig(LookFor(v));
            cc.enabled = false;
            rig.transform.SetParent(null, true);
            if (riding != v) rig.transform.SetPositionAndRotation(v.transform.TransformPoint(seat.seatLocal), Quaternion.Euler(0f, Yaw(v.transform), 0f));
            riding = null;
            rig.seatHeight = seat.seatHeight;
            rig.SetPose(seat.pose);
            rig.airborne = false;
            rig.speed = 0f;
            vehicle = v;
            fromPos = rig.transform.position;
            toPos = spot;
            fromYaw = Yaw(v.transform);
            Vector3 away = spot - v.transform.position; away.y = 0f;
            toYaw = away.sqrMagnitude > 0.01f ? Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg : fromYaw - 90f;
            toYaw = Mathf.LerpAngle(fromYaw, toYaw, 0.65f);
            t = 0f;
            posed = false;
            phase = Phase.Exiting;
            PlayerState.InTransition = true;
            if (gm.Rig != null) { gm.Rig.person = rig.transform; gm.Rig.personEye = 1.6f * rig.Scale; }
            UI.Sound("Whoosh");
            if (instant) FinishExit();
            return true;
        }

        void UpdateExit(float dt)
        {
            if (vehicle == null) { FinishExit(); return; }
            t += dt / ExitTime;
            float k = Mathf.Clamp01(t);
            float e = k * k * (3f - 2f * k);
            if (!posed && k > 0.3f) { posed = true; rig.SetPose(PersonPose.Idle); }
            Vector3 p = Vector3.Lerp(fromPos, toPos, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.12f);
            rig.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, toYaw, e), 0f));
            if (k >= 1f) FinishExit();
        }

        void FinishExit()
        {
            rig.transform.SetPositionAndRotation(toPos, Quaternion.Euler(0f, toYaw, 0f));
            rig.SetPose(PersonPose.Idle);
            cc.enabled = true;
            hv = Vector3.zero; vy = 0f; grounded = true; airTime = 0f;
            lastSafe = toPos;
            phase = Phase.OnFoot;
            PlayerState.InTransition = false;
            PlayerState.Person = rig.transform;
            PlayerState.OnFoot = true;
            var e = PlayerVehicles.Find(vehicle);
            vehicle = null;
            PlayerState.RaiseOnFootChanged(true);
            if (e != null && e.onExited != null) e.onExited();
            if (!toldControls)
            {
                toldControls = true;
                Toast(InputReader.PadConnected ? "On foot - left stick walk, LB sprint, A jump, Y get back in" : "On foot - WASD walk, SHIFT sprint, SPACE jump, mouse look, F get back in");
            }
        }

        // ================================================================== get in
        /// <summary>Get into a vehicle (the current car or a registered one); instant skips the walk / slide.</summary>
        public bool EnterVehicle(ArcadeCar v, bool instant)
        {
            if (v == null || rig == null) return false;
            if (phase != Phase.OnFoot && !(instant && phase != Phase.InVehicle)) return false;
            vehicle = v;
            seat = SeatOf(v);
            if (instant) { FinishEnter(); return true; }
            Vector3 l = v.transform.TransformPoint(seat.doorL), r = v.transform.TransformPoint(seat.doorR);
            Vector3 me = rig.transform.position;
            toPos = (me - l).sqrMagnitude <= (me - r).sqrMagnitude * 1.5f ? l : r;
            toPos.y = GroundY(toPos, v);
            t = 0f;
            phase = Phase.WalkToDoor;
            PlayerState.InTransition = true;
            return true;
        }

        void UpdateWalkToDoor(float dt)
        {
            if (vehicle == null) { CancelEnter(); return; }
            t += dt;
            Vector2 mv = GameplayActive(GameManager.Instance) ? InputReader.Move : Vector2.zero;
            if (t > 0.35f && mv.sqrMagnitude > 0.5f) { CancelEnter(); return; }
            Vector3 d = toPos - rig.transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist < 0.22f || t > 3f) { BeginSlide(); return; }
            Vector3 dir = d / Mathf.Max(dist, 1e-4f);
            float sp = Mathf.Min(WalkSpeed, dist * 4f + 0.6f);
            hv = Vector3.MoveTowards(hv, dir * sp, 14f * dt);
            Face(dir, dt, 720f);
            MoveCC(dt, false);
            rig.SetPose(hv.magnitude > 0.15f ? PersonPose.Walk : PersonPose.Idle);
            rig.speed = hv.magnitude;
        }

        void CancelEnter()
        {
            phase = Phase.OnFoot;
            vehicle = null;
            PlayerState.InTransition = false;
        }

        void BeginSlide()
        {
            cc.enabled = false;
            hv = Vector3.zero;
            fromPos = rig.transform.position;
            fromYaw = Yaw(rig.transform);
            toYaw = Yaw(vehicle.transform);
            rig.seatHeight = seat.seatHeight;
            rig.speed = 0f;
            rig.airborne = false;
            rig.SetPose(seat.pose);
            t = 0f;
            phase = Phase.SlideIn;
            UI.Sound("Whoosh");
        }

        void UpdateSlide(float dt)
        {
            if (vehicle == null) { CancelEnter(); cc.enabled = true; return; }
            t += dt / SlideTime;
            float k = Mathf.Clamp01(t);
            float e = k * k * (3f - 2f * k);
            Vector3 to = vehicle.transform.TransformPoint(seat.seatLocal);
            Vector3 p = Vector3.Lerp(fromPos, to, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.1f);
            rig.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Mathf.LerpAngle(fromYaw, Yaw(vehicle.transform), Mathf.Clamp01(e * 1.4f)), 0f));
            if (k >= 1f) FinishEnter();
        }

        void FinishEnter()
        {
            var gm = GameManager.Instance;
            var v = vehicle;
            vehicle = null;
            if (v == null || gm == null) { phase = Phase.InVehicle; return; }
            if (gm.Car != v) gm.AdoptVehicle(v);
            Unpark(v);
            PlayerState.SetVehicleEmpty(v, false);
            if (cc != null) cc.enabled = false;
            if (seat.crew || rig == null)
            {
                if (rig != null) { rig.transform.SetParent(null, true); rig.gameObject.SetActive(false); }
                riding = null;
            }
            else
            {
                rig.transform.SetParent(v.transform, false);
                rig.transform.localPosition = seat.seatLocal;
                rig.transform.localRotation = Quaternion.identity;
                rig.seatHeight = seat.seatHeight;
                rig.SetPose(seat.pose);
                riding = v;
            }
            if (gm.Rig != null) gm.Rig.person = null;
            hv = Vector3.zero;
            bool was = PlayerState.OnFoot;
            phase = Phase.InVehicle;
            PlayerState.InTransition = false;
            PlayerState.OnFoot = false;
            PlayerState.Person = null;
            if (was) PlayerState.RaiseOnFootChanged(false);
            var e = PlayerVehicles.Find(v);
            if (e != null && e.onEntered != null) e.onEntered();
        }

        /// <summary>Back into the current car at once (fast travel, events, garage, title).</summary>
        public void ForceIntoCar()
        {
            if (phase == Phase.InVehicle) return;
            var gm = GameManager.Instance;
            var v = gm != null ? gm.Car : null;
            if (phase == Phase.Exiting && vehicle != null) v = vehicle;
            if (v == null)
            {
                if (rig != null) { cc.enabled = false; rig.gameObject.SetActive(false); }
                if (gm != null && gm.Rig != null) gm.Rig.person = null;
                bool was = PlayerState.OnFoot;
                phase = Phase.InVehicle;
                PlayerState.OnFoot = false; PlayerState.Person = null; PlayerState.InTransition = false;
                if (was) PlayerState.RaiseOnFootChanged(false);
                return;
            }
            vehicle = v;
            seat = SeatOf(v);
            FinishEnter();
        }

        // ================================================================== on foot
        /// <summary>Put the person on the side of the nearest road (reset key on foot).</summary>
        public void RespawnOnRoad()
        {
            var gm = GameManager.Instance;
            if (rig == null || gm == null || gm.World == null || phase != Phase.OnFoot) return;
            var w = gm.World;
            Vector3 p; Quaternion r;
            Vector3 me = rig.transform.position;
            if (!w.RespawnPoint(me, rig.transform.forward, out p, out r)) return;
            RoadSample s;
            Vector3 spot = p;
            if (w.NearestRoad(new Vector2(p.x, p.z), out s, 30f) != float.MaxValue)
            {
                Vector2 side = s.Right;
                if (Vector2.Dot(side, new Vector2(me.x - s.pos.x, me.z - s.pos.y)) < 0f) side = -side;
                Vector2 q = s.pos + side * (s.halfWidth + 1.4f);
                spot = new Vector3(q.x, 0f, q.y);
            }
            cc.enabled = false;
            spot.y = GroundY(spot, null) + 0.05f;
            Teleport(spot, r.eulerAngles.y);
            Toast("Back on the road");
        }

        void Teleport(Vector3 p, float yaw)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            rig.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = was || phase == Phase.OnFoot;
            hv = Vector3.zero; vy = 0f;
            lastSafe = p;
        }

        void Face(Vector3 dir, float dt, float degPerSec)
        {
            if (dir.sqrMagnitude < 1e-4f) return;
            float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float cur = rig.transform.eulerAngles.y;
            float y = Mathf.MoveTowardsAngle(cur, want, degPerSec * dt);
            y = Mathf.LerpAngle(y, want, 1f - Mathf.Exp(-dt * 10f));
            rig.transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        void MoveCC(float dt, bool allowJump)
        {
            if (!cc.enabled || dt <= 0f) return;
            if (grounded) { coyote = 0.12f; airTime = 0f; } else { coyote -= dt; airTime += dt; }
            if (allowJump && coyote > 0f && InputReader.JumpDown) { vy = JumpSpeed; coyote = 0f; grounded = false; }
            if (grounded && vy < 0f) vy = -3f; else vy += Gravity * dt;
            if (vy < -30f) vy = -30f;

            // slide off slopes the controller cannot climb
            Vector3 slide = Vector3.zero;
            RaycastHit hit;
            Vector3 o = rig.transform.position + Vector3.up * (cc.radius + 0.05f);
            if (grounded && Physics.SphereCast(o, cc.radius * 0.9f, Vector3.down, out hit, 0.4f, Probe, QueryTriggerInteraction.Ignore))
            {
                float ang = Vector3.Angle(hit.normal, Vector3.up);
                slideK = ang > cc.slopeLimit + 2f ? Mathf.MoveTowards(slideK, 1f, dt * 3f) : Mathf.MoveTowards(slideK, 0f, dt * 4f);
                if (slideK > 0f)
                {
                    Vector3 down = Vector3.ProjectOnPlane(Vector3.down, hit.normal).normalized;
                    slide = down * 6f * slideK;
                }
            }
            else slideK = Mathf.MoveTowards(slideK, 0f, dt * 4f);
            var flags = cc.Move((hv + slide + Vector3.up * vy) * dt);
            grounded = (flags & CollisionFlags.Below) != 0 || cc.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
        }

        void UpdateOnFoot(float dt, bool active)
        {
            var gm = GameManager.Instance;
            Vector2 mv = active ? InputReader.Move : Vector2.zero;
            bool sprint = active && InputReader.SprintHeld && mv.sqrMagnitude > 0.05f;
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            var cam = gm != null && gm.Rig != null ? gm.Rig.transform : null;
            if (cam != null)
            {
                fwd = cam.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-4f) fwd = cam.up;
                fwd.y = 0f; fwd.Normalize();
                right = new Vector3(fwd.z, 0f, -fwd.x);
            }
            Vector3 wish = fwd * mv.y + right * mv.x;
            float m = Mathf.Clamp01(wish.magnitude);
            Vector3 target = Vector3.zero;
            if (m > 0.01f)
            {
                float spd = sprint ? SprintSpeed : InputReader.MoveAnalog ? Mathf.Lerp(0.8f, WalkSpeed, m) : WalkSpeed;
                target = wish / m * spd;
            }
            float accel = grounded ? (target.sqrMagnitude > hv.sqrMagnitude ? 12f : 16f) : 3.5f;
            hv = Vector3.MoveTowards(hv, target, accel * dt);
            if (m > 0.05f) Face(wish, dt, sprint ? 540f : 720f);
            MoveCC(dt, active);

            // no swimming: deep water pushes the person back to the last dry spot
            Vector3 p = rig.transform.position;
            const float water = WorldBuilder.WaterY;
            if (grounded && p.y > water - 0.2f)
            {
                safeTimer -= dt;
                if (safeTimer <= 0f) { safeTimer = 0.25f; lastSafe = p; }
            }
            waterToast -= dt;
            if (p.y < water - 0.55f)
            {
                Vector3 back = lastSafe - p; back.y = 0f;
                if (p.y < water - 1.1f || back.sqrMagnitude < 0.01f) Teleport(lastSafe + Vector3.up * 0.05f, rig.transform.eulerAngles.y);
                else hv = back.normalized * 3.5f;
                if (waterToast <= 0f) { waterToast = 6f; Toast("Too deep - no swimming today"); }
            }
            if (p.y < -60f) { RespawnOnRoad(); return; }

            float sp = hv.magnitude;
            rig.speed = sp;
            rig.airborne = !grounded && airTime > 0.12f;
            rig.SetPose(sp < 0.15f ? PersonPose.Idle : (sprint && sp > 3.4f ? PersonPose.Run : PersonPose.Walk));
            if (gm != null && gm.Rig != null)
            {
                gm.Rig.personMoving = sp > 0.6f;
                gm.Rig.personSprint = sprint && sp > 3.4f;
                gm.Rig.personHeading = rig.transform.eulerAngles.y;
            }
        }

        void FindNearVehicle()
        {
            NearVehicle = null;
            NearVehicleName = null;
            if (rig == null || phase != Phase.OnFoot) return;
            var gm = GameManager.Instance;
            Vector3 me = rig.transform.position + Vector3.up * 0.9f;
            float best = EnterRange;
            if (gm != null && gm.Car != null) Consider(gm.Car, me, ref best, gm.Car.spec != null ? gm.Car.spec.name : "Car");
            PlayerVehicles.Prune();
            var all = PlayerVehicles.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].vehicle != null && (gm == null || all[i].vehicle != gm.Car)) Consider(all[i].vehicle, me, ref best, all[i].name);
        }

        void Consider(ArcadeCar v, Vector3 me, ref float best, string name)
        {
            if (!v.gameObject.activeInHierarchy) return;
            var box = v.GetComponent<BoxCollider>();
            float d;
            if (box != null && box.enabled) d = Vector3.Distance(box.ClosestPoint(me), me);
            else d = Vector3.Distance(v.transform.position, me) - 1.2f;
            if (d < best) { best = d; NearVehicle = v; NearVehicleName = name; }
        }

        // ================================================================== frame
        void Update()
        {
            var gm = GameManager.Instance;
            float dt = Time.deltaTime;
            bool active = GameplayActive(gm);
            UpdateParking(dt);

            // the vehicle we ride was swapped away (garage / car keys): rider leaves
            if (phase == Phase.InVehicle && riding != null && (gm == null || gm.Car != riding))
            {
                if (rig != null) { rig.transform.SetParent(null, true); rig.gameObject.SetActive(false); }
                riding = null;
            }
            if (phase == Phase.InVehicle && riding != null && rig != null) rig.steer = riding.steerInput;

            switch (phase)
            {
                case Phase.InVehicle:
                    if (active && dt > 0f && InputReader.VehicleDown && !PlayerState.InTransition) ExitVehicle(false);
                    break;
                case Phase.Exiting:
                    if (dt > 0f) UpdateExit(dt);
                    break;
                case Phase.WalkToDoor:
                    if (dt > 0f) UpdateWalkToDoor(dt);
                    break;
                case Phase.SlideIn:
                    if (dt > 0f) UpdateSlide(dt);
                    break;
                case Phase.OnFoot:
                    if (Interiors.Sitting) break;      // seated in a tea house / temple: the places module owns the pose
                    if (rig == null || !rig.gameObject.activeInHierarchy) { ForceIntoCar(); break; }
                    if (dt > 0f) UpdateOnFoot(dt, active);
                    FindNearVehicle();
                    if (active && dt > 0f && InputReader.VehicleDown && phase == Phase.OnFoot)
                    {
                        if (NearVehicle != null) EnterVehicle(NearVehicle, false);
                        else Toast("No vehicle nearby");
                    }
                    break;
            }
            if (phase != Phase.OnFoot) { NearVehicle = null; NearVehicleName = null; }

            if (gm != null && gm.Rig != null)
            {
                bool personView = phase != Phase.InVehicle && rig != null;
                if (personView && gm.Rig.person != rig.transform) gm.Rig.person = rig.transform;
                gm.Rig.lookInput = active && personView;
                if (personView) gm.Rig.personEye = 1.6f * rig.Scale;
                if (phase != Phase.OnFoot) { gm.Rig.personMoving = false; gm.Rig.personSprint = false; }
            }

            // mouse look: lock the cursor while walking around
            bool wantLock = active && phase != Phase.InVehicle;
            if (wantLock && Cursor.lockState != CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; cursorLocked = true; }
            else if (!wantLock && cursorLocked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; cursorLocked = false; }
        }

        void LateUpdate()
        {
            // parked car: headlights off (CarExtras switches them on by the time of day in Update)
            var gm = GameManager.Instance;
            ArcadeCar parked = gm != null && gm.Car != null && phase != Phase.InVehicle ? gm.Car : null;
            SwitchOff(parked);
            var left = PlayerVehicles.Leftover;
            if (left != null && left != parked && (gm == null || left != gm.Car)) SwitchOffOther(left);
        }

        void SwitchOff(ArcadeCar v)
        {
            if (v == null) return;
            if (parkedLightCar != v)
            {
                parkedLightCar = v;
                var tr = v.transform.Find("Headlights");
                parkedLight = tr != null ? tr.GetComponent<Light>() : null;
            }
            if (parkedLight != null && parkedLight.enabled) parkedLight.enabled = false;
        }

        Light leftLight;
        ArcadeCar leftLightCar;

        void SwitchOffOther(ArcadeCar v)
        {
            if (leftLightCar != v)
            {
                leftLightCar = v;
                var tr = v.transform.Find("Headlights");
                leftLight = tr != null ? tr.GetComponent<Light>() : null;
            }
            if (leftLight != null && leftLight.enabled) leftLight.enabled = false;
        }

        /// <summary>Game manager: a vehicle is being replaced (garage spawn) - forget references to it.</summary>
        internal void VehicleRemoved(ArcadeCar v)
        {
            if (v == null) return;
            if (parking == v) parking = null;
            if (riding == v) { if (rig != null) { rig.transform.SetParent(null, true); rig.gameObject.SetActive(false); } riding = null; }
            PlayerState.SetVehicleEmpty(v, false);
        }

        /// <summary>Game manager: a new car was spawned while the player walks around (showroom swap): it parks, empty.</summary>
        internal void ParkSpawned(ArcadeCar v)
        {
            if (v == null) return;
            if (v.GetComponent<CarDetails>() != null) PlayerState.SetVehicleEmpty(v, true);
            Park(v);
        }

        /// <summary>Game manager: the previous player vehicle stays parked in the world (another vehicle was adopted).</summary>
        internal void ParkLeftBehind(ArcadeCar v)
        {
            if (v == null) return;
            if (riding == v) { if (rig != null) { rig.transform.SetParent(null, true); rig.gameObject.SetActive(false); } riding = null; }
            var det = v.GetComponent<CarDetails>();
            if (det != null) PlayerState.SetVehicleEmpty(v, true);
            if (v.Body != null && !v.Body.isKinematic) { v.controlsEnabled = false; Freeze(v); }
            else { v.controlsEnabled = false; v.enabled = false; }
        }
    }
}
