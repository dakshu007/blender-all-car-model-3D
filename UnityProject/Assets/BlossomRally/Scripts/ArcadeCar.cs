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
            foreach (var w in wheels)
            {
                if (w.visual == null) continue;
                Vector3 attach = transform.TransformPoint(w.local + Vector3.up * VehicleDynamics.SusLen);
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
            if (OnImpact != null) OnImpact(c.relativeVelocity.magnitude);
        }

        // ------------------------------------------------------------------------------- Rigidbody wrapper
        sealed class UnityBody : IVehicleBody
        {
            readonly Rigidbody rb;
            static readonly Dictionary<Collider, Surface> surfaceCache = new Dictionary<Collider, Surface>();
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
                hit.surface = SurfaceOf(h.collider);
                return true;
            }

            static Surface SurfaceOf(Collider c)
            {
                // keyed by the collider itself (GetInstanceID is a hard error in newer Unity 6)
                Surface s;
                if (surfaceCache.TryGetValue(c, out s)) return s;
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
