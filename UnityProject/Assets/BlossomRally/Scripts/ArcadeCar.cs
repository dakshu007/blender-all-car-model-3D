// ArcadeCar.cs - the player car: a thin Unity wrapper around VehicleDynamics (Scripts/Driving).
//
//   * reads the player's input (keyboard / gamepad), queues manual gear shifts
//   * runs VehicleDynamics every physics step on the Rigidbody (raycast suspension, slip-curve tyres, engine,
//     gearbox, brakes, assists - see VehicleDynamics.cs for the full model and DriveSettings.cs for assists)
//   * applies ICarModifier components (damage, wear: power / grip factors, steering pull) and Atmosphere.Wetness
//   * raises GameEvents.TakeOff / Landed, animates the wheels (real wheel speed: wheelspin and locked wheels show)
//
// Drifting: SPACE (handbrake) + steer kicks the tail out (the handbrake dips the clutch and locks the rears);
// throttle and steering then hold the angle (steer into = more angle, counter-steer = less), or provoke one with
// power (RWD), a lift or a Scandinavian flick. DriftAngle / DriftTargetAngle / IsDrifting feed the drift HUD.
// Manual gears (DriveSettings.ManualGears): LeftShift / E up, LeftCtrl / Q down; gamepad RB up, R3 down
// (or flick the right stick up / down). Brake to a stop and hold to reverse.
//
// Two-wheelers (spec.twoWheeler, set up by CarFactory.SetupBike): the physics body stays upright (balance assist
// in VehicleDynamics); here the MODEL is tilted about the tyre contact line by the lean + crash wobble and
// pitched about the rear / front contact by wheelies / stoppies, the <ID>_Fork steers about its rake axis with the
// front wheel in it, hard hits start a wobble (Crash) and a tipped-over bike rights itself after a moment.
using System.Collections.Generic;
using UnityEngine;

namespace BlossomRally
{
    public static class RigidbodyCompat
    {
        public static Vector3 GetVelocity(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(this Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(this Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }
    }

    public class ArcadeCar : MonoBehaviour
    {
        /// <summary>A wheel: physics state lives in DriveWheel; this adds the visual.</summary>
        public class Wheel : DriveWheel
        {
            public Transform visual;
            public Quaternion restLocal;
        }

        public CarSpec spec;
        [System.NonSerialized] public Wheel[] wheels = new Wheel[0];
        public bool controlsEnabled = true;

        /// <summary>What the car is doing with the controls (filtered): throttle = engine load, brake = brake pedal.</summary>
        [HideInInspector] public float throttle, brake, steerInput, handbrake;

        public float SpeedKmh { get { return dyn.SpeedKmh; } }
        public float ForwardSpeed { get { return dyn.ForwardSpeed; } }
        public float SlipAngle { get { return dyn.SlipAngle; } }           // degrees, signed (+ = moving to the right of the nose)
        public float DriftIntensity { get { return dyn.DriftIntensity; } } // 0..1
        public bool Grounded { get { return dyn.Grounded; } }
        public float SteerAngle { get { return dyn.SteerAngle; } }
        public float EngineRpm { get { return dyn.EngineRpm; } }
        /// <summary>Current gear 1..n (1 while reversing; see GearLabel / IsReversing).</summary>
        public int Gear { get { return dyn.Gear; } }
        public Surface RearSurface { get { return dyn.RearSurface; } }
        public float UprightTimer { get { return dyn.UprightTimer; } }
        public bool IsDrifting { get { return dyn.IsDrifting; } }
        public int DriftDirection { get { return dyn.DriftDirection; } }   // +1 right, -1 left
        public float DriftAngle { get { return dyn.DriftAngle; } }         // degrees, + = into the drift
        public float DriftTargetAngle { get { return dyn.DriftTargetAngle; } }
        public System.Action<float> OnImpact;

        // ---- extra telemetry (new)
        public VehicleDynamics Dynamics { get { return dyn; } }
        public string GearLabel { get { return dyn.GearLabel; } }          // "R", "1".."6"
        public bool IsReversing { get { return dyn.Reversing; } }
        public bool IsShifting { get { return dyn.Shifting; } }
        public bool OnRevLimiter { get { return dyn.OnLimiter; } }
        public bool TcsActive { get { return dyn.TcsActive; } }
        public bool AbsActive { get { return dyn.AbsActive; } }
        public float AirTime { get { return dyn.AirTime; } }
        public float LateralG { get { return dyn.LateralG; } }
        public float LongitudinalG { get { return dyn.LongitudinalG; } }
        public int GroundedWheels { get { return dyn.GroundedWheels; } }
        public float RedlineRpm { get { return spec != null ? spec.limiterRpm : 7600f; } }

        // ---- camera framing (set by CarFactory from the vehicle data; defaults = the original cars)
        /// <summary>Driver's eye point in car space (cockpit / interior cameras).</summary>
        [System.NonSerialized] public Vector3 DriverEye = new Vector3(-0.36f, 1.06f, 0.05f);
        /// <summary>Bonnet camera point in car space.</summary>
        [System.NonSerialized] public Vector3 HoodCam = new Vector3(0f, 1.25f, 1.1f);
        /// <summary>How much taller than a classic rally car this one is (m, 0 for the original cars):
        /// chase / showroom cameras lift and pull back by this.</summary>
        [System.NonSerialized] public float FrameExtra;

        // ---- two-wheelers (CarFactory.SetupBike)
        /// <summary>True for motorbikes (CarSpec.twoWheeler).</summary>
        public bool IsTwoWheeler { get { return spec != null && spec.twoWheeler; } }
        /// <summary>Visual lean (deg, + = right) incl. the crash wobble; 0 for cars.</summary>
        public float Lean { get { return dyn.Lean + dyn.Wobble; } }
        /// <summary>Rider's extra lean relative to the bike (deg).</summary>
        public float RiderLean { get { return dyn.RiderLean; } }
        /// <summary>Visual pitch (deg): + wheelie, - stoppie.</summary>
        public float Wheelie { get { return dyn.Wheelie; } }
        /// <summary>Rider anchors in car space (from the vehicle data): hips on the seat, right grip, right peg.</summary>
        [System.NonSerialized] public Vector3 RiderSeat = new Vector3(0f, 0.85f, -0.25f), RiderGrip = new Vector3(0.3f, 1.0f, 0.35f),
                                              RiderPeg = new Vector3(0.18f, 0.36f, -0.3f);
        /// <summary>The instantiated model (tilted for bikes).</summary>
        [System.NonSerialized] public Transform Model;
        Transform fork;
        Vector3 modelRestPos, forkRestPos, steerAxis = Vector3.up;
        Quaternion modelRestRot = Quaternion.identity, forkRestRot = Quaternion.identity;
        Quaternion visRot = Quaternion.identity;
        Vector3 visPivot;
        float tipTimer;

        Rigidbody rb;
        readonly VehicleDynamics dyn = new VehicleDynamics();
        UnityBody body;
        float rawThrottle, rawBrake, rawSteer, rawHandbrake;
        bool rawAnalog;
        int pendingShift;
        readonly List<ICarModifier> modifiers = new List<ICarModifier>();
        float modifierScan;

        public Rigidbody Body { get { return rb; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigurePhysics()
        {
            // 100 Hz physics: smoother tyre forces and suspension (the model is also stable at Unity's default 50 Hz)
            if (Time.fixedDeltaTime > 0.0125f) Time.fixedDeltaTime = 0.01f;
            if (Time.maximumDeltaTime > 0.1f) Time.maximumDeltaTime = 0.1f;   // no catch-up spiral after a hitch
            DriveSettings.Load();
        }

        public void Init(CarSpec s, Wheel[] w)
        {
            spec = s;
            wheels = w;
            rb = GetComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.SetDamping(0.02f, 0.1f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.maxAngularVelocity = 8f;
            ApplyMassProperties();
            dyn.Init(spec, w, spec.mass);
            body = new UnityBody(rb);
            foreach (var wh in wheels)
                if (wh.visual != null) wh.restLocal = Quaternion.Inverse(transform.rotation) * wh.visual.rotation;
        }

        /// <summary>Two-wheelers: the model to tilt, the steering fork (origin on the steering axis, may be null)
        /// and the rake (deg from vertical). Call after Init, with the model at rest.</summary>
        public void SetupBike(Transform model, Transform forkT, float rakeDeg)
        {
            Model = model;
            fork = forkT;
            if (model != null) { modelRestPos = model.localPosition; modelRestRot = model.localRotation; }
            if (forkT != null)
            {
                forkRestPos = transform.InverseTransformPoint(forkT.position);
                forkRestRot = Quaternion.Inverse(transform.rotation) * forkT.rotation;
            }
            float r = Mathf.Clamp(rakeDeg, 0f, 45f) * Mathf.Deg2Rad;
            steerAxis = new Vector3(0f, Mathf.Cos(r), -Mathf.Sin(r));
        }

        /// <summary>Car-space point -> where it is drawn on the tilted bike (car space). Identity for cars.</summary>
        public Vector3 VisualPoint(Vector3 local)
        {
            if (!IsTwoWheeler) return local;
            // pitch about the wheel contact (visPivot), then lean about the contact line (the car's z axis at y = 0)
            return visRot2 * (visPivot + visRot * (local - visPivot));
        }

        /// <summary>Rotation of the tilted bike relative to the car root (identity for cars).</summary>
        public Quaternion VisualRotation { get { return IsTwoWheeler ? visRot2 * visRot : Quaternion.identity; } }
        Quaternion visRot2 = Quaternion.identity;

        void ApplyMassProperties()
        {
            if (rb == null || spec == null) return;
            Vector3 com, inertia;
            VehicleDynamics.MassProperties(spec, wheels, out com, out inertia);
            rb.centerOfMass = com;
            rb.inertiaTensor = inertia;
            rb.inertiaTensorRotation = Quaternion.identity;
        }

        void Start()
        {
            ApplyMassProperties();   // other components may have added colliders since Init
        }

        void Update()
        {
            if (!controlsEnabled)
            {
                rawThrottle = rawBrake = rawSteer = rawHandbrake = 0f;
                pendingShift = 0;
                return;
            }
            rawSteer = InputReader.Steer;
            rawAnalog = InputReader.AnalogSteer;
            rawThrottle = InputReader.Throttle;
            rawBrake = InputReader.Brake;
            rawHandbrake = InputReader.Handbrake ? 1f : 0f;
            int sh = InputReader.ShiftRequest();
            if (sh != 0) pendingShift = sh;
        }

        void FixedUpdate()
        {
            if (rb == null || spec == null) return;
            float dt = Time.fixedDeltaTime;
            var input = new DriveInput
            {
                throttle = controlsEnabled ? rawThrottle : 0f,
                brake = controlsEnabled ? rawBrake : 0f,
                steer = controlsEnabled ? rawSteer : 0f,
                handbrake = controlsEnabled ? rawHandbrake : 0f,
                analog = rawAnalog,
                shift = pendingShift,
            };
            pendingShift = 0;
            dyn.Step(dt, input, body, GatherModifiers(dt), Atmosphere.Wetness, DriveSettings.Profile);

            if (controlsEnabled)
            {
                throttle = dyn.Reversing ? dyn.Brake : dyn.Throttle;
                brake = dyn.Brake;
                steerInput = dyn.Steer;
                handbrake = dyn.Handbrake;
            }
            else throttle = brake = steerInput = handbrake = 0f;

            if (IsTwoWheeler && !rb.isKinematic)
            {
                // safety net: a bike knocked onto its side picks itself up (wobbling) instead of lying there
                tipTimer = Vector3.Angle(transform.up, Vector3.up) > 65f ? tipTimer + dt : 0f;
                if (tipTimer > 0.45f)
                {
                    tipTimer = 0f;
                    Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                    if (f.sqrMagnitude < 1e-3f) f = Vector3.ProjectOnPlane(transform.up, Vector3.up);
                    rb.position = rb.position + Vector3.up * 0.35f;
                    rb.rotation = Quaternion.LookRotation(f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward, Vector3.up);
                    rb.angularVelocity = Vector3.zero;
                    rb.SetVelocity(rb.GetVelocity() * 0.3f);
                    dyn.Crash(0.7f);
                }
            }
            if (!rb.isKinematic)
            {
                if (dyn.TookOffThisStep) GameEvents.RaiseTakeOff(dyn.TakeOffPosition);
                if (dyn.LandedThisStep) GameEvents.RaiseLanded(dyn.LandingAirTime, dyn.LandingImpactSpeed);
            }
        }

        CarModifiers GatherModifiers(float dt)
        {
            modifierScan -= dt;
            if (modifierScan <= 0f)
            {
                modifierScan = 0.5f;          // components arrive through WorldHooks.CarSpawned at any time
                modifiers.Clear();
                GetComponents(modifiers);
            }
            var m = CarModifiers.None;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                var asObj = mod as Object;
                if (mod == null || (asObj is Object && asObj == null)) continue;
                var beh = mod as Behaviour;
                if (beh != null && !beh.enabled) continue;
                m.power *= Mathf.Max(0f, mod.PowerFactor);
                m.grip *= Mathf.Max(0.05f, mod.GripFactor);
                m.steerPull += mod.SteerPullDeg;
            }
            return m;
        }

        /// <summary>Clear drift / drivetrain state after a teleport (respawn, event start).</summary>
        public void ResetDrift()
        {
            dyn.ResetState();
            if (rb != null && !rb.isKinematic)
            {
                float v = Vector3.Dot(rb.GetVelocity(), transform.forward);
                if (Mathf.Abs(v) > 2f) dyn.MatchGear(v);
            }
        }

        void LateUpdate()
        {
            if (spec == null) return;
            float dt = Time.deltaTime;
            if (IsTwoWheeler) { BikeLateUpdate(dt); return; }
            foreach (var w in wheels)
            {
                if (w.visual == null) continue;
                Vector3 attach = transform.TransformPoint(w.local + Vector3.up * dyn.SuspensionLength);
                Vector3 centre = attach - transform.up * w.springLength;
                w.spin = Mathf.Repeat(w.spin + w.angularVelocity * Mathf.Rad2Deg * dt, 360f);
                float steer = w.front ? SteerAngle : 0f;
                w.visual.position = centre;
                w.visual.rotation = transform.rotation * Quaternion.AngleAxis(steer, Vector3.up) *
                                    Quaternion.AngleAxis(w.spin, Vector3.right) * w.restLocal;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (IsTwoWheeler && speed > 6f && c.contactCount > 0)
            {
                // a hard hit on anything but the ground under the wheels: a short wobble, then ride on
                Vector3 n = c.GetContact(0).normal;
                if (Vector3.Dot(n, transform.up) < 0.7f) dyn.Crash(Mathf.InverseLerp(6f, 20f, speed));
            }
            if (OnImpact != null) OnImpact(speed);
        }

        // ------------------------------------------------------------------------------- two-wheeler visuals
        void BikeLateUpdate(float dt)
        {
            // tilt: lean (+ wobble) about the tyre contact line, wheelie / stoppie about the rear / front contact
            float zr = -0.7f, zf = 0.7f;
            foreach (var w in wheels) { if (w.front) zf = w.local.z; else zr = w.local.z; }
            float pitch = dyn.Wheelie;
            visPivot = new Vector3(0f, 0f, pitch >= 0f ? zr : zf);
            visRot = Quaternion.AngleAxis(-pitch, Vector3.right);
            visRot2 = Quaternion.AngleAxis(-(dyn.Lean + dyn.Wobble), Vector3.forward);
            Quaternion q = visRot2 * visRot;
            if (Model != null)
            {
                Model.localPosition = VisualPoint(modelRestPos);
                Model.localRotation = q * modelRestRot;
            }
            // steering: small at speed (a bike steers by leaning), full lock when walking pace
            float steerVis = SteerAngle * Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(Mathf.Abs(dyn.ForwardSpeed) / 25f));
            Quaternion steerQ = Quaternion.AngleAxis(steerVis, steerAxis);
            if (fork != null)
            {
                fork.position = transform.TransformPoint(VisualPoint(forkRestPos));
                fork.rotation = transform.rotation * q * steerQ * forkRestRot;
            }
            foreach (var w in wheels)
            {
                if (w.visual == null) continue;
                Vector3 c = w.local + Vector3.up * (dyn.SuspensionLength - w.springLength);
                Quaternion s = Quaternion.identity;
                if (w.front && fork != null) { c = forkRestPos + steerQ * (c - forkRestPos); s = steerQ; }
                else if (w.front) s = Quaternion.AngleAxis(steerVis, Vector3.up);
                w.spin = Mathf.Repeat(w.spin + w.angularVelocity * Mathf.Rad2Deg * dt, 360f);
                w.visual.position = transform.TransformPoint(VisualPoint(c));
                w.visual.rotation = transform.rotation * q * s * Quaternion.AngleAxis(w.spin, Vector3.right) * w.restLocal;
            }
        }

        // ------------------------------------------------------------------------------- Rigidbody wrapper
        sealed class UnityBody : IVehicleBody
        {
            readonly Rigidbody rb;
            static readonly Dictionary<Collider, Surface> surfaceCache = new Dictionary<Collider, Surface>();
            static readonly HashSet<Collider> terrainCache = new HashSet<Collider>();
            const int Mask = ~((1 << CarExtras.CarLayer) | (1 << 2));     // not cars, not Ignore Raycast

            public UnityBody(Rigidbody r) { rb = r; }

            public Vector3 Position { get { return rb.position; } }
            public Quaternion Rotation { get { return rb.rotation; } }
            public Vector3 Velocity { get { return rb.GetVelocity(); } }
            public Vector3 AngularVelocity { get { return rb.angularVelocity; } }
            public Vector3 PointVelocity(Vector3 p) { return rb.GetPointVelocity(p); }
            public void AddForceAtPosition(Vector3 f, Vector3 p) { if (!rb.isKinematic) rb.AddForceAtPosition(f, p); }
            public void AddForce(Vector3 f) { if (!rb.isKinematic) rb.AddForce(f); }
            public void AddAcceleration(Vector3 a) { if (!rb.isKinematic) rb.AddForce(a, ForceMode.Acceleration); }
            public void AddAngularAcceleration(Vector3 a) { if (!rb.isKinematic) rb.AddTorque(a, ForceMode.Acceleration); }

            public bool CastWheel(Vector3 origin, Vector3 dir, float maxDist, out WheelHit hit)
            {
                hit = default(WheelHit);
                RaycastHit h;
                if (!Physics.Raycast(origin, dir, out h, maxDist, Mask, QueryTriggerInteraction.Ignore)) return false;
                if (h.collider.attachedRigidbody == rb) return false;
                hit.point = h.point;
                hit.normal = h.normal;
                hit.distance = h.distance;
                hit.surface = SurfaceOf(h.collider, h.point);
                return true;
            }

            static Surface SurfaceOf(Collider c, Vector3 point)
            {
                // keyed by the collider itself (GetInstanceID is a hard error in newer Unity 6)
                Surface s;
                if (terrainCache.Contains(c))
                {
                    var f = WorldField.Active;
                    return f != null ? f.GroundSurface(point.x, point.z) : Surface.Grass;
                }
                if (surfaceCache.TryGetValue(c, out s)) return s;
                if (c.GetComponent<TerrainSurface>() != null)
                {
                    if (terrainCache.Count > 4096) terrainCache.Clear();
                    terrainCache.Add(c);
                    var f = WorldField.Active;
                    return f != null ? f.GroundSurface(point.x, point.z) : Surface.Grass;
                }
                var tag = c.GetComponent<SurfaceTag>();
                if (tag == null && c.transform.parent != null) tag = c.transform.parent.GetComponent<SurfaceTag>();
                s = tag != null ? tag.surface : Surface.Grass;
                if (surfaceCache.Count > 8192) surfaceCache.Clear();
                surfaceCache[c] = s;
                return s;
            }
        }
    }
}
