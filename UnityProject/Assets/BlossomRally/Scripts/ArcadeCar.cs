// ArcadeCar.cs - drift-friendly arcade rally physics on a Rigidbody: raycast spring/damper
// suspension, friction-circle tyres with surface grip, RWD/AWD drive, handbrake, downforce,
// wheel visuals (spin + steer + suspension) and drift telemetry.
//
// Drifting is an explicit, controllable state:
//   * start:   hold SPACE and steer (above ~30 km/h). The tail kicks out in the steer direction.
//   * hold:    THROTTLE holds / widens the angle, steering INTO the drift deepens it,
//              COUNTER-STEER reduces it. Holding SPACE tightens the line.
//   * switch:  SPACE while steering the other way flicks into the opposite drift.
//   * slides you didn't start with SPACE are caught by the same controller, so they stay controllable.
//   * exit:    counter-steer or lift off until the car straightens, or slow right down.
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
        public class Wheel
        {
            public string slot;
            public Vector3 local;          // wheel centre in car space at rest
            public float radius = 0.32f;
            public bool front, driven;
            public Transform visual;
            public Quaternion restLocal;
            public float spin;
            public bool grounded;
            public Vector3 contact, normal;
            public float springLength;
            public Surface surface;
            public float lateralSlip;
            public float load;
        }

        public CarSpec spec;
        public Wheel[] wheels = new Wheel[0];
        public bool controlsEnabled = true;

        [HideInInspector] public float throttle, brake, steerInput, handbrake;
        public float SpeedKmh { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SlipAngle { get; private set; }         // degrees, signed
        public float DriftIntensity { get; private set; }    // 0..1
        public bool Grounded { get; private set; }
        public float SteerAngle { get; private set; }
        public float EngineRpm { get; private set; }
        /// <summary>Simulated gear 1..5 (for engine sound, HUD).</summary>
        public int Gear { get; private set; } = 1;
        /// <summary>Last surface under the rear wheels.</summary>
        public Surface RearSurface { get; private set; } = Surface.Asphalt;
        public float UprightTimer { get; private set; }
        public bool IsDrifting { get { return driftActive; } }
        public int DriftDirection { get { return driftDir; } }       // +1 right, -1 left
        public float DriftAngle { get; private set; }                // degrees, + = into the drift
        public float DriftTargetAngle { get; private set; }
        public System.Action<float> OnImpact;

        Rigidbody rb;
        const float SusLen = 0.22f;
        const float RestComp = 0.08f;
        float k, damper;

        // drift state
        bool driftActive;
        int driftDir = 1;
        float driftTime, driftExitTimer, driftBlend, driftAirTime, settleTimer;
        int lastGrounded;
        Vector3 prevFlatVel;

        public Rigidbody Body { get { return rb; } }

        public void Init(CarSpec s, Wheel[] w)
        {
            spec = s;
            wheels = w;
            rb = GetComponent<Rigidbody>();
            rb.mass = spec.mass;
            rb.SetDamping(0.02f, 0.6f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.centerOfMass = new Vector3(0f, 0.40f, 0.05f);
            rb.maxAngularVelocity = 8f;
            k = spec.mass * 9.81f / 4f / RestComp;
            damper = 0.32f * 2f * Mathf.Sqrt(k * spec.mass / 4f);
            foreach (var wh in wheels)
            {
                wh.springLength = SusLen;
                if (wh.visual != null) wh.restLocal = Quaternion.Inverse(transform.rotation) * wh.visual.rotation;
            }
        }

        void Update()
        {
            if (!controlsEnabled) { throttle = brake = steerInput = handbrake = 0f; return; }
            float targetSteer = InputReader.Steer;
            float rate = InputReader.AnalogSteer ? 14f : (Mathf.Abs(targetSteer) > 0.01f ? 5f : 7f);
            steerInput = Mathf.MoveTowards(steerInput, targetSteer, Time.deltaTime * rate);
            throttle = InputReader.Throttle;
            brake = InputReader.Brake;
            handbrake = InputReader.Handbrake ? 1f : 0f;
        }

        void FixedUpdate()
        {
            if (rb == null || spec == null) return;
            float dt = Time.fixedDeltaTime;
            Vector3 up = transform.up;
            Vector3 vel = rb.GetVelocity();
            Vector3 flatVel = Vector3.ProjectOnPlane(vel, up);
            float speed = flatVel.magnitude;
            ForwardSpeed = Vector3.Dot(vel, transform.forward);
            SpeedKmh = speed * 3.6f;
            float maxSpeed = spec.maxSpeedKmh / 3.6f;

            // drive request (S brakes, then reverses when nearly stopped)
            float drive = throttle;
            float brakeAmt = brake;
            if (brake > 0.1f && ForwardSpeed < 1.5f && throttle < 0.1f) { drive = -0.55f * brake; brakeAmt = 0f; }

            // speed-sensitive steering with a touch of automatic counter-steer while sliding
            float steerLimit = spec.maxSteer * Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(speed / 42f));
            float slip = 0f;
            if (speed > 3f) slip = Vector3.SignedAngle(transform.forward, flatVel.normalized, up);
            if (ForwardSpeed < 0f) slip = 0f;
            SlipAngle = slip;
            // how fast the direction of travel is turning (rad/s) - used to settle the car after a drift
            float velYaw = (speed > 3f && prevFlatVel.sqrMagnitude > 1f) ? Vector3.SignedAngle(prevFlatVel, flatVel, up) * Mathf.Deg2Rad / dt : 0f;
            prevFlatVel = flatVel;

            UpdateDriftState(dt, speed, slip, up);
            float steerRel = steerInput * driftDir;

            if (driftActive)
            {
                // front wheels follow the slide (natural counter-steer) with some driver input on top
                SteerAngle = Mathf.Clamp(slip * 0.85f + steerInput * steerLimit * 0.45f, -spec.maxSteer, spec.maxSteer);
            }
            else
            {
                float counter = Mathf.Clamp(slip * 0.35f, -spec.maxSteer * 0.5f, spec.maxSteer * 0.5f) * Mathf.Clamp01((speed - 5f) / 10f);
                SteerAngle = Mathf.Clamp(steerInput * steerLimit + counter * (1f - Mathf.Abs(steerInput) * 0.6f), -spec.maxSteer, spec.maxSteer);
            }
            float rearDriftGrip = Mathf.Lerp(1f, 0.22f * spec.driftGrip / 0.6f, driftBlend);
            float frontDriftGrip = Mathf.Lerp(1f, 0.55f, driftBlend);
            int drivenCount = 0;
            foreach (var w in wheels) if (w.driven) drivenCount++;
            drivenCount = Mathf.Max(1, drivenCount);
            float torqueCurve = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01(speed / maxSpeed), 2.2f)) * 0.92f + 0.08f;
            if (speed > maxSpeed && drive > 0f) torqueCurve = 0f;

            int grounded = 0;
            foreach (var w in wheels)
            {
                Vector3 attach = transform.TransformPoint(w.local + Vector3.up * SusLen);
                float rayLen = SusLen + RestComp + w.radius;
                RaycastHit hit;
                w.grounded = Physics.Raycast(attach, -up, out hit, rayLen + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                             && hit.collider.attachedRigidbody != rb;
                if (!w.grounded)
                {
                    w.springLength = Mathf.MoveTowards(w.springLength, SusLen + RestComp, dt * 2f);
                    w.load = 0f;
                    w.lateralSlip = 0f;
                    continue;
                }
                grounded++;
                w.contact = hit.point;
                w.normal = hit.normal;
                var tag = hit.collider.GetComponent<SurfaceTag>();
                if (tag == null && hit.collider.transform.parent != null) tag = hit.collider.transform.parent.GetComponent<SurfaceTag>();
                w.surface = tag != null ? tag.surface : Surface.Grass;
                float len = Mathf.Clamp(hit.distance - w.radius, 0f, SusLen + RestComp);
                w.springLength = len;
                float comp = (SusLen + RestComp) - len;
                float vUp = Vector3.Dot(rb.GetPointVelocity(attach), up);
                float springF = Mathf.Max(0f, k * comp - damper * vUp);
                springF = Mathf.Min(springF, spec.mass * 9.81f * 2.5f);
                rb.AddForceAtPosition(up * springF, attach);
                w.load = springF;

                // tyre frame on the contact plane
                Quaternion steerRot = w.front ? Quaternion.AngleAxis(SteerAngle, up) : Quaternion.identity;
                Vector3 fwd = Vector3.ProjectOnPlane(steerRot * transform.forward, hit.normal).normalized;
                Vector3 right = Vector3.Cross(hit.normal, fwd).normalized;
                Vector3 pv = rb.GetPointVelocity(hit.point);
                float vLat = Vector3.Dot(pv, right);
                float vLong = Vector3.Dot(pv, fwd);

                float mu = spec.grip * SurfaceGrip(w.surface);
                if (w.front) mu *= frontDriftGrip;
                else
                {
                    mu *= rearDriftGrip;
                    if (handbrake > 0.5f && !driftActive) mu *= 0.45f;
                }
                float maxF = mu * springF;
                float latF = Mathf.Clamp(-vLat * (spec.mass * 0.25f) / dt * 0.85f, -maxF, maxF);

                float longF = 0f;
                if (w.driven && Mathf.Abs(drive) > 0.01f)
                {
                    float share = spec.drive == Drive.AWD ? (w.front ? spec.frontTorque : 1f - spec.frontTorque) * 2f / drivenCount : 1f / drivenCount;
                    longF += drive * spec.power * torqueCurve * share;
                }
                if (brakeAmt > 0.01f)
                    longF += -Mathf.Sign(vLong) * Mathf.Min(Mathf.Abs(vLong) * spec.mass * 0.25f / dt, 9500f * brakeAmt * (w.front ? 0.3f : 0.2f));
                if (handbrake > 0.5f && !w.front)   // locked rears; lighter while drifting so SPACE can tighten the line
                    longF += -Mathf.Sign(vLong) * Mathf.Min(Mathf.Abs(vLong) * spec.mass * 0.25f / dt, driftActive ? 700f : 2600f);
                longF += -vLong * 18f;  // rolling resistance

                Vector3 F = right * latF + fwd * longF;
                float cap = maxF * 1.08f;
                if (F.magnitude > cap) F = F.normalized * cap;
                w.lateralSlip = Mathf.Abs(vLat);   // sideways sliding speed (m/s) - drives smoke + skid marks
                // apply slightly above the contact patch to tame body roll
                rb.AddForceAtPosition(F, hit.point + up * 0.32f);
            }
            Grounded = grounded > 0;
            lastGrounded = grounded;

            // aero drag + downforce
            rb.AddForce(-vel * vel.magnitude * 0.38f);
            if (Grounded) rb.AddForce(-up * speed * speed * 1.4f);

            float slideAmt = Mathf.Clamp01((Mathf.Abs(slip) - 6f) / 25f);
            if (driftActive && grounded >= 2)
            {
                // ---- drift controller ----
                // target angle from the driver's inputs
                float wantAngle = 4f + 18f * throttle + 20f * Mathf.Max(steerRel, 0f) - 22f * Mathf.Max(-steerRel, 0f) + (handbrake > 0.5f ? 8f : 0f);
                wantAngle = Mathf.Clamp(wantAngle, 0f, 50f);
                DriftTargetAngle = wantAngle;
                float angle = DriftAngle;
                // the car's path curves into the drift (more angle / more steer = tighter line)
                float pathRate = (0.20f + 0.85f * Mathf.Clamp01(angle / 40f)) * (0.75f + 0.45f * Mathf.Max(steerRel, 0f));
                pathRate = Mathf.Min(pathRate, 8.5f / Mathf.Max(speed, 1f)) * driftBlend;
                Vector3 turned = Quaternion.AngleAxis(driftDir * pathRate * Mathf.Rad2Deg * dt, up) * flatVel;
                rb.SetVelocity(vel - flatVel + turned);
                flatVel = turned;
                // yaw: follow the path + close the angle error (critically damped)
                float yawNow = Vector3.Dot(rb.angularVelocity, up);
                float correction = Mathf.Clamp((wantAngle - angle) * Mathf.Deg2Rad * 3.2f, -2.2f, 2.2f);
                float yawWant = driftDir * (pathRate + correction);
                rb.AddTorque(up * (yawWant - yawNow) * 14f * driftBlend, ForceMode.Acceleration);
                // throttle keeps the momentum going through the slide, lifting scrubs speed
                if (throttle > 0.05f)
                    rb.AddForce(flatVel.normalized * throttle * 2.4f * Mathf.Clamp01(1f - speed / maxSpeed), ForceMode.Acceleration);
                else
                    rb.AddForce(-flatVel.normalized * Mathf.Min(speed, 1.2f), ForceMode.Acceleration);
            }
            else if (grounded >= 2 && speed > 5f)
            {
                // normal grip driving: light yaw help so the car turns in crisply, plus spin protection
                float assist = spec.driftYaw * (handbrake > 0.5f ? 0.9f : Mathf.Lerp(0.22f, 0.7f, slideAmt));
                rb.AddTorque(up * steerInput * assist, ForceMode.Acceleration);
                if (slideAmt > 0f && throttle > 0.1f)
                    rb.AddForce(flatVel.normalized * throttle * slideAmt * 2.2f, ForceMode.Acceleration);
                // limit spin-outs
                float yawRate = Vector3.Dot(rb.angularVelocity, up);
                if (Mathf.Abs(yawRate) > 2.8f) rb.AddTorque(-up * (yawRate - Mathf.Sign(yawRate) * 2.8f) * 6f, ForceMode.Acceleration);
                if (Mathf.Abs(slip) > 70f && Mathf.Abs(steerInput) < 0.2f) rb.AddTorque(-up * Mathf.Sign(slip) * 1.5f, ForceMode.Acceleration);
                // just after a drift: line the car up with its direction of travel (no pendulum swing)
                if (settleTimer > 0f)
                    rb.AddTorque(up * (velYaw - yawRate) * 7f * (settleTimer / 0.45f), ForceMode.Acceleration);
            }
            if (grounded == 0)
            {
                // air: gently level out
                Vector3 axis = Vector3.Cross(up, Vector3.up);
                rb.AddTorque(axis * 4f - rb.angularVelocity * 0.5f, ForceMode.Acceleration);
            }
            DriftIntensity = Grounded && speed > 6f ? Mathf.Clamp01((Mathf.Abs(slip) - 8f) / 30f) : 0f;
            if (driftActive && Grounded) DriftIntensity = Mathf.Max(DriftIntensity, 0.35f);
            else if (handbrake > 0.5f && speed > 5f && Grounded) DriftIntensity = Mathf.Max(DriftIntensity, 0.3f);

            UprightTimer = Vector3.Dot(up, Vector3.up) < 0.25f ? UprightTimer + dt : 0f;

            // simulated 5-speed gearbox for the engine sound
            float ratio = Mathf.Clamp01(speed / maxSpeed);
            float gearPos = ratio * 5f;
            float inGear = gearPos - Mathf.Floor(gearPos);
            Gear = Mathf.Clamp(Mathf.FloorToInt(gearPos) + 1, 1, 5);
            foreach (var wr in wheels) if (!wr.front && wr.grounded) RearSurface = wr.surface;
            float target = 900f + (Mathf.Floor(gearPos) < 1 ? ratio * 5f : inGear) * 5600f + throttle * 700f + DriftIntensity * 900f;
            EngineRpm = Mathf.Lerp(EngineRpm, Mathf.Clamp(target, 850f, 7800f), dt * 6f);
        }

        void UpdateDriftState(float dt, float speed, float slip, Vector3 up)
        {
            bool hb = handbrake > 0.5f && controlsEnabled;
            if (!driftActive)
            {
                settleTimer = Mathf.Max(0f, settleTimer - dt);
                if (hb && Mathf.Abs(steerInput) > 0.3f && speed > 8f && ForwardSpeed > 5f && lastGrounded >= 2)
                    StartDrift(steerInput > 0f ? 1 : -1, up, true);          // SPACE + steer starts a drift
                else if (settleTimer <= 0f && speed > 10f && ForwardSpeed > 5f && lastGrounded >= 2 && brake < 0.5f
                         && Mathf.Abs(slip) > 16f && Mathf.Abs(slip) < 80f)
                    StartDrift(slip > 0f ? -1 : 1, up, false);               // catch an accidental slide so it stays controllable
            }
            else if (hb && steerInput * driftDir < -0.3f && driftTime > 0.3f && speed > 7f)
            {
                StartDrift(-driftDir, up, true);                             // SPACE + opposite steer: flick to the other side
            }

            DriftAngle = driftActive ? -slip * driftDir : 0f;
            if (driftActive)
            {
                driftTime += dt;
                driftAirTime = lastGrounded == 0 ? driftAirTime + dt : 0f;
                // the driver asks to straighten (counter-steer / lift) and the car has done so
                bool calm = !hb && driftTime > 0.45f && (DriftAngle < 7f || (DriftTargetAngle < 12f && DriftAngle < 13f));
                driftExitTimer = calm ? driftExitTimer + dt : 0f;
                if (driftExitTimer > 0.22f || speed < 4.5f || ForwardSpeed < 0.5f || DriftAngle > 95f || driftAirTime > 0.8f)
                {
                    driftActive = false;
                    settleTimer = 0.45f;
                }
            }
            driftBlend = Mathf.MoveTowards(driftBlend, driftActive ? 1f : 0f, dt / (driftActive ? 0.12f : 0.3f));
            if (!driftActive) DriftTargetAngle = 0f;
        }

        void StartDrift(int dir, Vector3 up, bool kick)
        {
            driftActive = true;
            driftDir = dir;
            driftTime = 0f;
            driftExitTimer = 0f;
            driftAirTime = 0f;
            if (!kick) return;
            // kick the tail out
            float yaw = Vector3.Dot(rb.angularVelocity, up);
            float want = dir * 1.5f;
            if (yaw * dir < 1.5f) rb.angularVelocity += up * (want - yaw) * 0.8f;
        }

        public void ResetDrift()
        {
            driftActive = false;
            driftBlend = 0f;
            DriftAngle = 0f;
            settleTimer = 0f;
            prevFlatVel = Vector3.zero;
        }

        static float SurfaceGrip(Surface s)
        {
            switch (s)
            {
                case Surface.Asphalt: return 1.0f;
                case Surface.Paving: return 0.95f;
                case Surface.Gravel: return 0.80f;
                case Surface.Dirt: return 0.76f;
                default: return 0.66f;
            }
        }

        void LateUpdate()
        {
            if (spec == null) return;
            float dt = Time.deltaTime;
            foreach (var w in wheels)
            {
                if (w.visual == null) continue;
                Vector3 attach = transform.TransformPoint(w.local + Vector3.up * SusLen);
                Vector3 centre = attach - transform.up * w.springLength;
                float roll = ForwardSpeed / Mathf.Max(0.1f, w.radius) * Mathf.Rad2Deg * dt;
                if (w.driven && throttle > 0.5f && SpeedKmh < 25f) roll += 900f * dt;     // launch wheelspin
                if (!w.front && handbrake > 0.5f) roll = 0f;
                w.spin = Mathf.Repeat(w.spin + roll, 360f);
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
    }
}
