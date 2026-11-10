// VehicleDynamics.cs - the Blossom Rally car physics, engine-agnostic so the very same code runs in the game
// (through ArcadeCar's Rigidbody wrapper) and in the offline tuning bench ($SP/sim/bench).
//
// Per fixed step:
//   1. inputs     - keyboard steering ramp (fast in, faster out, quick reversal), analogue smoothing, pedal ramps,
//                   brake-to-reverse
//   2. gearbox    - automatic with hysteresis + power-cut shifts (or manual), auto clutch, rev limiter bounce
//   3. steering   - speed-sensitive lock that asks for front slip rather than wheel angle, plus counter-steer:
//                   the front wheels follow the front axle's direction of travel (like a real caster letting the
//                   wheel go), scaled by the assist level
//   4. suspension - raycast spring / bump + rebound damper / bump stop / anti-roll bars -> per-wheel load,
//                   so braking, throttle and cornering move weight (and grip) between the wheels
//   5. tyres      - slip-ratio + slip-angle combined curve per surface (TyreModel), load sensitivity, wetness,
//                   damage; wheels spin up / lock with an implicit integrator (sub-stepped), LSD + AWD centre
//                   coupling, brakes with optional ABS, handbrake locking the rears, TCS
//   6. assists    - drift stabiliser (holds the angle the inputs ask for), spin protection beyond the drift
//                   window, anti-roll-over on kerbs, air control and soft landings
//   7. telemetry  - slip / drift state / rpm / gear / take-off and landing moments
using UnityEngine;

namespace BlossomRally
{
    public struct WheelHit
    {
        public Vector3 point, normal;
        public float distance;
        public Surface surface;
    }

    /// <summary>What the physics needs from a rigid body (Unity Rigidbody in game, a small integrator in the bench).</summary>
    public interface IVehicleBody
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        Vector3 Velocity { get; }
        Vector3 AngularVelocity { get; }
        Vector3 PointVelocity(Vector3 worldPoint);
        void AddForceAtPosition(Vector3 force, Vector3 worldPoint);
        void AddForce(Vector3 force);
        void AddAcceleration(Vector3 acceleration);
        void AddAngularAcceleration(Vector3 acceleration);
        bool CastWheel(Vector3 origin, Vector3 direction, float maxDistance, out WheelHit hit);
    }

    /// <summary>One wheel: geometry, contact and telemetry (ArcadeCar.Wheel adds the visual).</summary>
    public class DriveWheel
    {
        public string slot;
        public Vector3 local;          // wheel centre in car space at rest
        public float radius = 0.32f;
        public bool front, driven;
        public float spin;             // visual roll angle (deg)
        public bool grounded;
        public Vector3 contact, normal;
        public float springLength;
        public Surface surface;
        public float lateralSlip;      // sliding speed at the contact patch (m/s) - smoke / skids
        public float load;             // N
        // extra telemetry
        public float angularVelocity;  // rad/s, + rolls forward
        public float slipRatio;
        public float slipAngle;        // deg
        public float compression;      // m
        public float gripUsed;         // 0..1+ share of the available grip in use
    }

    public struct DriveInput
    {
        public float throttle, brake, steer, handbrake;   // raw player input
        public bool analog;                               // steer from a stick (no keyboard ramp)
        public int shift;                                 // +1 / -1 requested this step (manual gears)
    }

    public struct CarModifiers
    {
        public float power, grip, steerPull;
        public static CarModifiers None { get { return new CarModifiers { power = 1f, grip = 1f, steerPull = 0f }; } }
    }

    public class VehicleDynamics
    {
        public const float SusLen = 0.22f;          // suspension attach point above the wheel centre at rest
        const float V0 = 3.5f;                      // low-speed slip denominator (m/s)
        const float ForceHeight = 0.04f;            // side forces act this far above the contact (roll centre)
        const float LongForceHeight = 0.0f;         // drive / brake forces (anti-dive / anti-squat)
        const float DriveEff = 0.9f;
        const float RearMargin = 1.07f;

        public CarSpec spec;
        DriveWheel[] wheels;
        float mass, restComp, maxLen;
        float[] kSpring, cBump, cReb, kArb, share, inertiaW, staticLoad, axleGrip;
        int[] partner;
        int drivenCount;
        Vector3 frontMidLocal, rearMidLocal;
        float driveRadius = 0.31f;

        // per-step scratch
        float[] comp, compRate, vx, vy, tanA, muN, rK, tDrive, tBrake, fxAcc, fyAcc;
        bool[] hitOk;
        WheelHit[] hits;
        Vector3[] wFwd, wRight;
        SurfaceGrip[] grip;

        // ---- outputs
        public float SpeedKmh { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float SlipAngle { get; private set; }
        public float DriftIntensity { get; private set; }
        public bool Grounded { get; private set; }
        public int GroundedWheels { get; private set; }
        public float SteerAngle { get; private set; }
        public float EngineRpm { get; private set; } = 900f;
        public int Gear { get; private set; } = 1;
        public bool Reversing { get; private set; }
        public bool Shifting { get { return shiftTimer > 0f; } }
        public bool OnLimiter { get { return limiterCut > 0f; } }
        public bool TcsActive { get; private set; }
        public bool AbsActive { get; private set; }
        public Surface RearSurface { get; private set; } = Surface.Asphalt;
        public float UprightTimer { get; private set; }
        public bool IsDrifting { get; private set; }
        public int DriftDirection { get; private set; } = 1;
        public float DriftAngle { get; private set; }
        public float DriftTargetAngle { get; private set; }
        public float AirTime { get; private set; }
        public float LateralG { get; private set; }
        public float LongitudinalG { get; private set; }
        public float YawRate { get; private set; }
        // filtered driver inputs (what the car is actually doing with the pedals / wheel)
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public float Handbrake { get; private set; }
        // moments (true for the one step they happen)
        public bool TookOffThisStep { get; private set; }
        public Vector3 TakeOffPosition { get; private set; }
        public bool LandedThisStep { get; private set; }
        public float LandingAirTime { get; private set; }
        public float LandingImpactSpeed { get; private set; }

        public string GearLabel { get { return Reversing ? "R" : Gear.ToString(); } }
        public int GearCount { get { return spec != null && spec.gears != null ? spec.gears.Length : 5; } }

        // ---- state
        float engRpm = 900f, shiftTimer, shiftCooldown, limiterCut, limiterTime, tcsFactor = 1f, revTimer;
        bool clutchLocked;
        float driftTime, driftExitTimer, driftAirTime;
        float pathYaw, airTimer;
        bool tookOff;
        Vector3 prevFlatVel, prevVel, lastAirVel;
        bool havePrev;

        public void Init(CarSpec s, DriveWheel[] w, float bodyMass)
        {
            spec = s;
            wheels = w;
            mass = bodyMass;
            int n = w.Length;
            restComp = spec.rideComp;
            maxLen = SusLen + restComp;
            kSpring = new float[n]; cBump = new float[n]; cReb = new float[n]; kArb = new float[n]; share = new float[n];
            inertiaW = new float[n]; staticLoad = new float[n]; axleGrip = new float[n]; partner = new int[n];
            comp = new float[n]; compRate = new float[n]; vx = new float[n]; vy = new float[n]; tanA = new float[n];
            muN = new float[n]; rK = new float[n]; tDrive = new float[n]; tBrake = new float[n]; fxAcc = new float[n]; fyAcc = new float[n];
            hitOk = new bool[n]; hits = new WheelHit[n]; wFwd = new Vector3[n]; wRight = new Vector3[n]; grip = new SurfaceGrip[n];

            int nf = 0, nr = 0;
            Vector3 fm = Vector3.zero, rm = Vector3.zero;
            drivenCount = 0;
            float rSum = 0f;
            for (int i = 0; i < n; i++)
            {
                if (w[i].front) { fm += w[i].local; nf++; } else { rm += w[i].local; nr++; }
                if (w[i].driven) { drivenCount++; rSum += w[i].radius; }
            }
            frontMidLocal = nf > 0 ? fm / nf : new Vector3(0f, 0.3f, 1.2f);
            rearMidLocal = nr > 0 ? rm / nr : new Vector3(0f, 0.3f, -1.2f);
            driveRadius = drivenCount > 0 ? rSum / drivenCount : 0.31f;
            float wf = Mathf.Clamp(spec.weightFront, 0.3f, 0.7f);
            for (int i = 0; i < n; i++)
            {
                var wh = w[i];
                float axleShare = wh.front ? wf : 1f - wf;
                int onAxle = wh.front ? Mathf.Max(1, nf) : Mathf.Max(1, nr);
                staticLoad[i] = mass * 9.81f * axleShare / onAxle;
                kSpring[i] = staticLoad[i] / restComp;
                float crit = 2f * Mathf.Sqrt(kSpring[i] * staticLoad[i] / 9.81f);
                cBump[i] = 0.30f * crit;
                cReb[i] = 0.55f * crit;
                kArb[i] = kSpring[i] * (wh.front ? spec.arbFront : spec.arbRear) * 0.7f;
                axleGrip[i] = wh.front ? spec.frontGrip : spec.rearGrip * RearMargin;   // stable at the limit: the front lets go first
                inertiaW[i] = 0.9f + 2.2f * wh.radius * wh.radius * 4f;     // ~1.7 kg m^2 for a 0.31 m wheel incl. brakes / hub
                if (wh.driven)
                {
                    if (spec.drive == Drive.AWD)
                    {
                        float axle = wh.front ? spec.frontTorque : 1f - spec.frontTorque;
                        share[i] = axle / onAxle;
                    }
                    else share[i] = 1f / Mathf.Max(1, drivenCount);
                }
                partner[i] = -1;
                wh.springLength = SusLen;
                wh.angularVelocity = 0f;
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (i != j && w[i].front == w[j].front && Mathf.Sign(w[i].local.x) != Mathf.Sign(w[j].local.x)) partner[i] = j;
            engRpm = spec.idleRpm;
            EngineRpm = engRpm;
            Gear = 1;
        }

        /// <summary>Centre of mass and principal inertia (pitch, yaw, roll) for the rigid body.</summary>
        public static void MassProperties(CarSpec s, DriveWheel[] w, out Vector3 com, out Vector3 inertia)
        {
            float zf = 1.2f, zr = -1.2f, track = 1.45f;
            int nf = 0, nr = 0;
            float sf = 0f, sr = 0f, tx = 0f;
            foreach (var wh in w)
            {
                if (wh.front) { sf += wh.local.z; nf++; } else { sr += wh.local.z; nr++; }
                tx += Mathf.Abs(wh.local.x);
            }
            if (nf > 0) zf = sf / nf;
            if (nr > 0) zr = sr / nr;
            if (w.Length > 0) track = 2f * tx / w.Length;
            float wfr = Mathf.Clamp(s.weightFront, 0.3f, 0.7f);
            float zc = zr + (zf - zr) * wfr;
            com = new Vector3(0f, s.cgHeight, zc);
            float a = zf - zc, b = zc - zr;
            float yaw = s.mass * a * b * 1.05f * s.inertia;
            float pitch = yaw * 1.1f;
            float roll = s.mass * (track * track / 12f + 0.06f) * 1.4f;
            inertia = new Vector3(pitch, yaw, roll);
        }

        /// <summary>Pick the gear that suits the current speed (after teleports / launches at speed).</summary>
        public void MatchGear(float forwardSpeed)
        {
            ForwardSpeed = forwardSpeed;
            Gear = 1;
            while (Gear < spec.gears.Length && GroundRpm(Gear) > spec.limiterRpm * 0.72f) Gear++;
            gearTotal = GearRatio(Gear);
            engRpm = Mathf.Max(spec.idleRpm, GroundRpm(Gear));
            EngineRpm = engRpm;
            if (wheels != null) foreach (var w in wheels) w.angularVelocity = forwardSpeed / Mathf.Max(0.1f, w.radius);
        }

        public void ResetState()
        {
            IsDrifting = false;
            DriftAngle = DriftTargetAngle = 0f;
            driftTime = driftExitTimer = driftAirTime = 0f;
            Steer = Throttle = Brake = Handbrake = 0f;
            havePrev = false;
            pathYaw = 0f;
            airTimer = 0f;
            tookOff = false;
            tcsFactor = 1f;
            shiftTimer = 0f;
            limiterCut = 0f;
            Reversing = false;
            Gear = 1;
            if (wheels != null) foreach (var w in wheels) w.angularVelocity = 0f;
        }

        // --------------------------------------------------------------------------------------------- step
        public void Step(float dt, DriveInput input, IVehicleBody body, CarModifiers mods, float wetness, AssistProfile a)
        {
            if (spec == null || wheels == null || dt <= 0f) return;
            TookOffThisStep = LandedThisStep = false;
            powerMod = Mathf.Max(0f, mods.power);
            Quaternion rot = body.Rotation;
            Vector3 pos = body.Position;
            Vector3 up = rot * Vector3.up, fwd = rot * Vector3.forward, right = rot * Vector3.right;
            Vector3 vel = body.Velocity;
            Vector3 angVel = body.AngularVelocity;
            Vector3 flatVel = Vector3.ProjectOnPlane(vel, up);
            float speed = flatVel.magnitude;
            ForwardSpeed = Vector3.Dot(vel, fwd);
            SpeedKmh = speed * 3.6f;
            float yawRate = Vector3.Dot(angVel, up);
            YawRate = yawRate;
            float slip = 0f;
            if (speed > 3f && ForwardSpeed > 0f) slip = Vector3.SignedAngle(fwd, flatVel, up);
            SlipAngle = slip;
            // direction-of-travel turn rate (rad/s), smoothed
            if (havePrev && speed > 3f && prevFlatVel.sqrMagnitude > 4f)
            {
                float pr = Vector3.SignedAngle(prevFlatVel, flatVel, up) * Mathf.Deg2Rad / dt;
                pathYaw = Mathf.Lerp(pathYaw, pr, 1f - Mathf.Exp(-dt / 0.05f));
                Vector3 acc = (vel - prevVel) / dt;
                LateralG = Mathf.Lerp(LateralG, Vector3.Dot(acc, right) / 9.81f, 1f - Mathf.Exp(-dt / 0.08f));
                LongitudinalG = Mathf.Lerp(LongitudinalG, Vector3.Dot(acc, fwd) / 9.81f, 1f - Mathf.Exp(-dt / 0.08f));
            }
            else pathYaw = Mathf.Lerp(pathYaw, 0f, 1f - Mathf.Exp(-dt / 0.1f));
            prevFlatVel = flatVel;
            prevVel = vel;
            havePrev = true;

            // ---------------------------------------------------------------- 1. inputs
            float thrIn = Mathf.Clamp01(input.throttle), brkIn = Mathf.Clamp01(input.brake);
            float hb = input.handbrake > 0.5f ? 1f : 0f;
            Throttle = Mathf.MoveTowards(Throttle, thrIn, dt * (thrIn > Throttle ? 8f : 12f));
            Brake = Mathf.MoveTowards(Brake, brkIn, dt * (brkIn > Brake ? 10f : 14f));
            Handbrake = hb;
            float target = Mathf.Clamp(input.steer, -1f, 1f);
            if (input.analog) Steer = Mathf.MoveTowards(Steer, target, dt * 9f);
            else
            {
                float rate;
                if (Mathf.Abs(target) < 0.01f) rate = 7.5f;                               // let go: back to centre fast
                else if (Steer * target < 0f) rate = 11f;                                 // reversal: snap through centre
                else if (Mathf.Abs(target) > Mathf.Abs(Steer))
                    rate = Mathf.Lerp(5.5f, 3.2f, Mathf.Clamp01(speed / 35f)) * (IsDrifting ? 1.5f : 1f);   // turn in
                else rate = 7.5f;
                Steer = Mathf.MoveTowards(Steer, target, dt * rate);
            }

            // brake-to-reverse
            if (!Reversing)
            {
                revTimer = (brkIn > 0.3f && thrIn < 0.1f && ForwardSpeed < 0.6f && speed < 1.5f) ? revTimer + dt : 0f;
                if (revTimer > 0.2f) { Reversing = true; revTimer = 0f; shiftTimer = 0f; }
            }
            else if (thrIn > 0.1f && ForwardSpeed > -0.8f) { Reversing = false; Gear = 1; }
            float driveCmd = Reversing ? Brake : Throttle;
            float brakeCmd = Reversing ? Throttle : Brake;
            if (Reversing && -ForwardSpeed > 9f) driveCmd = 0f;   // reverse speed limit ~32 km/h

            // ---------------------------------------------------------------- 2. gearbox
            UpdateGearbox(dt, driveCmd, input.shift);

            // ---------------------------------------------------------------- 3. steering
            Vector3 fm = pos + rot * frontMidLocal;
            Vector3 vfm = Vector3.ProjectOnPlane(body.PointVelocity(fm), up);
            float gammaF = (speed > 1.5f && Vector3.Dot(vfm, fwd) > 0.5f) ? Vector3.SignedAngle(fwd, vfm, up) : 0f;
            float frontPeak = 7f;
            {
                int nfp = 0; float sum = 0f;
                for (int i = 0; i < wheels.Length; i++) if (wheels[i].front && wheels[i].grounded) { sum += TyreModel.For(wheels[i].surface).peakSlipDeg; nfp++; }
                if (nfp > 0) frontPeak = sum / nfp;
            }
            float hiLock = a.lockHighSpeed * frontPeak / 7f;
            float lockNow = Mathf.Lerp(spec.maxSteer, Mathf.Min(spec.maxSteer, hiLock), Smooth01((speed - 3f) / 20f));
            float cs = a.counterSteer * Mathf.Clamp01((speed - 3f) / 5f) * (ForwardSpeed > 0f && !Reversing ? 1f : 0f);
            // AWD: driven front wheels pointed far outward would drag the car straight - drivers keep them nearer the line
            if (IsDrifting && spec.drive == Drive.AWD) cs *= 0.78f;
            float steerDeg = Steer * lockNow + cs * gammaF + mods.steerPull;
            if (a.slipCap > 0f && speed > 4f && ForwardSpeed > 0f && !Reversing)
            {
                // never ask the front tyres for much more than their peak slip (no scrubbing into understeer)
                float cap = frontPeak * a.slipCap * Mathf.Lerp(1.6f, 1f, Smooth01((speed - 4f) / 6f));
                steerDeg = Mathf.Clamp(steerDeg, gammaF - cap, gammaF + cap);
            }
            float maxLock = spec.maxSteer + 6f;
            SteerAngle = Mathf.Clamp(steerDeg, -maxLock, maxLock);

            // ---------------------------------------------------------------- 4. suspension
            int n = wheels.Length;
            int grounded = 0;
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                Vector3 attach = pos + rot * (w.local + Vector3.up * SusLen);
                WheelHit hit;
                bool ok = body.CastWheel(attach, -up, maxLen + w.radius + 0.05f, out hit);
                float rawLen = ok ? hit.distance - w.radius : maxLen;
                if (ok && rawLen > maxLen + 0.02f) ok = false;
                hitOk[i] = ok;
                if (!ok)
                {
                    w.grounded = false;
                    w.springLength = Mathf.MoveTowards(w.springLength, maxLen, dt * 2f);
                    comp[i] = 0f; compRate[i] = 0f;
                    w.compression = 0f;
                    continue;
                }
                hits[i] = hit;
                float len = Mathf.Clamp(rawLen, 0f, maxLen);
                float bodyVz = Vector3.Dot(body.PointVelocity(attach), up);
                if (w.grounded)
                {
                    float slew = 4f + Mathf.Abs(bodyVz);             // kerbs / steps are absorbed over a few steps
                    len = Mathf.Max(len, w.springLength - slew * dt);
                    compRate[i] = (w.springLength - len) / dt;
                }
                else compRate[i] = Mathf.Max(0f, -bodyVz);
                w.springLength = len;
                comp[i] = maxLen - len;
                w.compression = comp[i];
                w.grounded = true;
                w.contact = hit.point;
                w.normal = hit.normal;
                w.surface = hit.surface;
                grounded++;
            }
            float loadCap = mass * 9.81f * 2.2f;
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                if (!hitOk[i]) { w.load = 0f; continue; }
                float c = comp[i];
                float cr = compRate[i];
                const float bumpStart = 0.24f;
                bool bump = c > bumpStart;
                float damp = (cr > 0f ? cBump[i] * (bump ? 3f : 1f) : cReb[i]) * cr;
                // a damper can only stop the motion, never reverse it within one step (stable at 50 Hz too)
                float dampMax = staticLoad[i] / 9.81f * Mathf.Abs(cr) / dt;
                damp = Mathf.Clamp(damp, -dampMax, dampMax);
                float f = kSpring[i] * c + damp;
                // bump rubber: stiff going in, soft coming back out (it soaks up landings instead of bouncing)
                if (bump) f += kSpring[i] * 10f * (c - bumpStart) * (cr >= 0f ? 1f : 0.3f);
                int p = partner[i];
                if (p >= 0) f += kArb[i] * (c - comp[p]);
                f = Mathf.Clamp(f, 0f, loadCap);
                w.load = f;
                body.AddForceAtPosition(up * f, pos + rot * (w.local + Vector3.up * SusLen));
            }
            GroundedWheels = grounded;
            Grounded = grounded > 0;

            // ---------------------------------------------------------------- 5. tyres + drivetrain
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                fxAcc[i] = fyAcc[i] = 0f;
                tBrake[i] = 0f;
                if (!hitOk[i]) { muN[i] = 0f; rK[i] = 0f; w.lateralSlip = 0f; w.slipAngle = 0f; w.gripUsed = 0f; continue; }
                var hit = hits[i];
                Quaternion steerRot = w.front ? Quaternion.AngleAxis(SteerAngle, up) : Quaternion.identity;
                Vector3 wfd = Vector3.ProjectOnPlane(steerRot * fwd, hit.normal).normalized;
                Vector3 wrt = Vector3.Cross(hit.normal, wfd).normalized;
                wFwd[i] = wfd; wRight[i] = wrt;
                Vector3 pv = body.PointVelocity(hit.point);
                vx[i] = Vector3.Dot(pv, wfd);
                vy[i] = Vector3.Dot(pv, wrt);
                float vref = Mathf.Max(Mathf.Abs(vx[i]), V0);
                tanA[i] = vy[i] / vref;
                var g = TyreModel.For(hit.surface);
                grip[i] = g;
                float loadRatio = w.load / Mathf.Max(1f, staticLoad[i]);
                float sens = Mathf.Clamp(1f - 0.13f * (loadRatio - 1f), 0.72f, 1.15f);
                float mu = g.mu * (1f - g.wetLoss * Mathf.Clamp01(wetness)) * spec.grip * axleGrip[i] * mods.grip * sens;
                muN[i] = mu * w.load;
                rK[i] = w.radius * w.radius * muN[i] * g.shapeC * g.shapeB / (g.peakRatio * vref);
                w.slipAngle = Mathf.Atan(tanA[i]) * Mathf.Rad2Deg;
            }
            // brakes
            float frontBrake = spec.brakeTorque, rearBrake = spec.brakeTorque * (1f - spec.brakeBias) / Mathf.Max(0.05f, spec.brakeBias);
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                tBrake[i] = brakeCmd * (w.front ? frontBrake : rearBrake);
            }
            // engine -> wheel torques (pulling the handbrake dips the clutch and frees the AWD centre, like a rally car)
            hbClutch = hb > 0.5f;
            DriveTorques(dt, driveCmd);

            int sub = Mathf.Clamp(Mathf.CeilToInt(dt / 0.0026f), 2, 10);
            float h = dt / sub;
            bool absOn = a.abs && brakeCmd > 0.05f;
            AbsActive = false;
            for (int s = 0; s < sub; s++)
            {
                for (int i = 0; i < n; i++)
                {
                    var w = wheels[i];
                    float om = w.angularVelocity;
                    float I = inertiaW[i] + (w.driven && clutchLocked ? spec.engineInertia * gearTotal * gearTotal * share[i] : 0f);
                    float fx = 0f, fy = 0f, stiff = 0f;
                    if (hitOk[i] && muN[i] > 0f)
                    {
                        float vref = Mathf.Max(Mathf.Abs(vx[i]), V0);
                        float k = (om * w.radius - vx[i]) / vref;
                        float sigma;
                        TyreModel.Force(k, tanA[i], muN[i], grip[i], out fx, out fy, out sigma);
                        stiff = rK[i] * (sigma < 1.5f ? 1f : Mathf.Lerp(1f, 0.25f, (sigma - 1.5f) / 1.5f));
                        w.slipRatio = k;
                        w.gripUsed = sigma;
                    }
                    else w.slipRatio = 0f;
                    // implicit wheel spin: I dw = h (T - r Fx(w_new)), Fx linearised around w
                    float drive = w.driven ? tDrive[i] : 0f;
                    float wNew = (I * om + h * (drive - w.radius * fx) + h * stiff * om) / (I + h * stiff);
                    if (!hitOk[i]) wNew -= om * 0.4f * h;                 // bearing drag in the air
                    float before = wNew;
                    float bt = tBrake[i] * h / I;
                    if (wNew > 0f) wNew = Mathf.Max(0f, wNew - bt); else wNew = Mathf.Min(0f, wNew + bt);
                    if (absOn && hitOk[i] && vx[i] > 2f && (w.front || hb < 0.5f))
                    {
                        float minW = vx[i] * (1f - grip[i].peakRatio * 1.05f) / w.radius;
                        if (wNew < minW && before > wNew) { wNew = Mathf.Min(before, minW); AbsActive = true; }
                    }
                    if (!w.front && hb > 0f)
                    {
                        float ht = spec.handbrakeTorque * h / I;
                        if (wNew > 0f) wNew = Mathf.Max(0f, wNew - ht); else wNew = Mathf.Min(0f, wNew + ht);
                    }
                    w.angularVelocity = wNew;
                    fxAcc[i] += fx;
                    fyAcc[i] += fy;
                }
                Couple(!hbClutch);
            }
            float mCorner = mass * 0.25f;
            int dc = 0;
            float maxDrivenSlip = 0f, maxDrivenSigma = 0f;
            for (int i = 0; i < n; i++)
            {
                var w = wheels[i];
                if (!hitOk[i]) continue;
                float fx = fxAcc[i] / sub, fy = fyAcc[i] / sub;
                // never push harder than it takes to stop the sideways slide this step (low-speed stability)
                float capY = mCorner * Mathf.Abs(vy[i]) / dt;
                fy = Mathf.Clamp(fy, -capY, capY);
                var g = grip[i];
                float roll = -g.rolling * w.load * Mathf.Clamp(vx[i] / 0.5f, -1f, 1f) - g.looseDrag * w.load * vx[i] * 0.1f;
                // side force at roll-centre height (controlled roll), drive / brake force near the ground (real pitch)
                body.AddForceAtPosition(wRight[i] * fy, hits[i].point + up * ForceHeight);
                body.AddForceAtPosition(wFwd[i] * (fx + roll), hits[i].point + up * LongForceHeight);
                float ls = Mathf.Max(0f, Mathf.Abs(w.angularVelocity * w.radius - vx[i]) - 0.12f * Mathf.Abs(vx[i]) - 1f);
                w.lateralSlip = Mathf.Sqrt(vy[i] * vy[i] + ls * ls);
                if (w.driven && Mathf.Abs(vx[i]) > 1f)
                {
                    maxDrivenSlip = Mathf.Max(maxDrivenSlip, w.slipRatio);
                    if (w.slipRatio > 0.02f) maxDrivenSigma = Mathf.Max(maxDrivenSigma, w.gripUsed);
                    dc++;
                }
            }
            if (clutchLocked && !Shifting && !hbClutch) engRpm = Mathf.Max(DrivenRpm() , spec.idleRpm * 0.7f);
            // traction control
            TcsActive = false;
            if (a.tcsSlip > 0f && dc > 0)
            {
                // slip-ratio limit (launch / straight line) plus a combined-slip limit that tightens with speed:
                // power slides are welcome at rally-corner speeds, not at 120 km/h
                float allow = a.tcsSlip * (IsDrifting ? 2f : 1f);
                float sigmaMax = 1.35f * (a.tcsSlip / 0.24f);
                float excess = Mathf.Max((maxDrivenSlip - allow) * 5f, (maxDrivenSigma - sigmaMax) * 0.7f * Smooth01((speed - 19f) / 8f));
                if (excess > 0f)
                {
                    float t = Mathf.Clamp01(1f - excess);
                    tcsFactor = Mathf.Min(tcsFactor, Mathf.Lerp(tcsFactor, t, 0.6f));
                    TcsActive = true;
                }
                else tcsFactor = Mathf.MoveTowards(tcsFactor, 1f, dt * 4f);
                tcsFactor = Mathf.Max(tcsFactor, 0.12f);
            }
            else tcsFactor = 1f;

            // aero
            body.AddForce(-vel * vel.magnitude * spec.drag);
            if (Grounded)
            {
                // rear-biased downforce: the faster you go the more planted the tail
                float df = ForwardSpeed * ForwardSpeed * spec.downforce;
                body.AddForceAtPosition(-up * df * 0.35f, pos + rot * frontMidLocal);
                body.AddForceAtPosition(-up * df * 0.65f, pos + rot * rearMidLocal);
            }

            // ---------------------------------------------------------------- 6. drift state + assists
            angleLimit = Mathf.Lerp(a.driftWindow, a.driftWindow * 0.45f, Smooth01((speed - 18f) / 18f));
            UpdateDrift(dt, speed, slip, grounded, hb);
            if (grounded >= 2 && speed > 5f && ForwardSpeed > 0f)
            {
                if (IsDrifting && a.driftAssist > 0f)
                {
                    // hold the angle (damp its rate) and lean gently toward what the inputs ask for
                    float blend = Mathf.Clamp01(driftTime / 0.2f) * a.driftAssist;
                    // never lean the car toward the spin window - growing the angle is the driver's job near it
                    float err = Mathf.Clamp(DriftTargetAngle - DriftAngle, -15f, Mathf.Clamp(angleLimit - 8f - DriftAngle, 0f, 8f));
                    float rStar = pathYaw + DriftDirection * 2.4f * err * Mathf.Deg2Rad;
                    float acc = Mathf.Clamp(7f * (rStar - yawRate), -6f, 6f) * blend;
                    body.AddAngularAcceleration(up * acc);
                    if (Throttle > 0.1f && a.slideKeep > 0f)
                        body.AddAcceleration(flatVel.normalized * a.slideKeep * Throttle * Mathf.Clamp01(DriftAngle / 20f) * Mathf.Clamp01(1f - speed / 45f));
                }
                // handbrake + steer: help the car pivot (the locked rears do the rest)
                if (hb > 0.5f && a.hbAssist > 0f && Mathf.Abs(Steer) > 0.2f)
                {
                    float want = Steer * Mathf.Min(2.8f, 1.0f + speed * 0.15f);
                    float err = want - yawRate;
                    if (err * Steer > 0f) body.AddAngularAcceleration(up * Mathf.Clamp(err * 6f, -7f, 7f) * a.hbAssist);
                }
                if (a.stability > 0f && hb < 0.5f)
                {
                    // spin protection: beyond the drift window (narrower at speed - nobody wants 50 deg at 120 km/h)
                    float over = Mathf.Abs(slip) - angleLimit;
                    float sgn = Mathf.Sign(slip);
                    float grow = sgn * (pathYaw - yawRate);         // >0: rotating further out of line
                    float acc = 0f;
                    if (over > -10f && grow > 0f) acc += grow * 9f * Mathf.Clamp01((over + 10f) / 10f);
                    if (over > 0f) acc += Mathf.Min(over, 25f) * 0.4f;
                    if (acc > 0f) body.AddAngularAcceleration(up * sgn * acc * a.stability);
                }
            }
            // anti roll-over: kerbs and bumps must not flip the car
            float rollAng = Mathf.Asin(Mathf.Clamp(Vector3.Dot(right, Vector3.up), -1f, 1f));
            if (a.rollGuard > 0f && grounded > 0 && Mathf.Abs(rollAng) > 0.16f)
            {
                float rollRate = Vector3.Dot(angVel, fwd);
                float ex = Mathf.Abs(rollAng) - 0.16f;
                float acc = -Mathf.Sign(rollAng) * ex * 30f - (rollRate * rollAng > 0f ? rollRate * 5f : 0f);
                body.AddAngularAcceleration(fwd * acc * a.rollGuard);
            }
            // air: auto-level (nose follows the arc a little), steer for a touch of yaw, throttle / brake pitch
            if (grounded == 0)
            {
                Vector3 hv = Vector3.ProjectOnPlane(vel, Vector3.up);
                Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up);
                Vector3 wantFwd = flatFwd.sqrMagnitude > 0.01f ? flatFwd.normalized : fwd;
                if (hv.sqrMagnitude > 4f)
                {
                    float pitchV = Mathf.Atan2(vel.y, hv.magnitude) * 0.45f;
                    wantFwd = (wantFwd * Mathf.Cos(pitchV) + Vector3.up * Mathf.Sin(pitchV)).normalized;
                }
                Vector3 wantUp = (Vector3.up - wantFwd * Vector3.Dot(Vector3.up, wantFwd)).normalized;
                Vector3 err = Vector3.Cross(up, wantUp);
                err -= up * Vector3.Dot(err, up);
                Vector3 pr = angVel - up * Vector3.Dot(angVel, up);
                float air = 0.25f + 0.75f * a.airControl;
                Vector3 acc = (err * 9f - pr * 3.2f) * air;
                acc += right * (-(Throttle - Brake) * 1.8f * air);
                acc += up * (Steer * 2.2f - yawRate * 0.6f) * a.airControl;
                body.AddAngularAcceleration(acc);
            }

            // ---------------------------------------------------------------- 7. telemetry
            if (grounded == 0)
            {
                airTimer += dt;
                lastAirVel = vel;
                if (!tookOff && airTimer > 0.15f) { tookOff = true; TookOffThisStep = true; TakeOffPosition = pos; }
            }
            else
            {
                if (tookOff)
                {
                    LandedThisStep = true;
                    LandingAirTime = airTimer;
                    LandingImpactSpeed = Mathf.Max(0f, -lastAirVel.y);
                    tookOff = false;
                }
                airTimer = 0f;
            }
            AirTime = airTimer;
            DriftIntensity = Grounded && speed > 6f ? Mathf.Clamp01((Mathf.Abs(slip) - 8f) / 30f) : 0f;
            if (IsDrifting && Grounded) DriftIntensity = Mathf.Max(DriftIntensity, 0.35f);
            else if (hb > 0.5f && speed > 5f && Grounded) DriftIntensity = Mathf.Max(DriftIntensity, 0.3f);
            UprightTimer = Vector3.Dot(up, Vector3.up) < 0.25f ? UprightTimer + dt : 0f;
            for (int i = 0; i < n; i++) if (!wheels[i].front && wheels[i].grounded) RearSurface = wheels[i].surface;
            EngineRpm = Mathf.Lerp(EngineRpm, engRpm, 1f - Mathf.Exp(-dt * 30f));
        }

        static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ------------------------------------------------------------------------------------- drivetrain
        float gearTotal;

        float GearRatio(int g)
        {
            if (Reversing) return -spec.reverseRatio * spec.finalDrive;
            int i = Mathf.Clamp(g - 1, 0, spec.gears.Length - 1);
            return spec.gears[i] * spec.finalDrive;
        }

        float DrivenOmega()
        {
            float wd = 0f;
            for (int i = 0; i < wheels.Length; i++) if (wheels[i].driven) wd += share[i] * wheels[i].angularVelocity;
            return wd;
        }

        float DrivenRpm() { return Mathf.Abs(DrivenOmega() * gearTotal) * 9.5493f; }

        float GroundRpm(int g)
        {
            return Mathf.Abs(ForwardSpeed) / Mathf.Max(0.1f, driveRadius) * spec.gears[Mathf.Clamp(g - 1, 0, spec.gears.Length - 1)] * spec.finalDrive * 9.5493f;
        }

        float EngineTorque(float rpm, float thr, float power)
        {
            float full = spec.peakTorque * spec.TorqueCurve(rpm) * power;
            float drag = spec.peakTorque * spec.engineBrake * (0.15f + 0.85f * Mathf.Clamp01(rpm / spec.limiterRpm));
            return thr * full - (1f - thr) * drag;
        }

        float powerMod = 1f;
        float angleLimit = 50f;
        bool hbClutch;

        void UpdateGearbox(float dt, float driveCmd, int shiftReq)
        {
            if (shiftTimer > 0f) shiftTimer -= dt;
            if (shiftCooldown > 0f) shiftCooldown -= dt;
            int n = spec.gears.Length;
            if (Reversing) { Gear = 1; gearTotal = GearRatio(1); return; }
            if (DriveSettings.ManualGears || shiftReq != 0)
            {
                if (shiftReq != 0 && shiftTimer <= 0f)
                {
                    int ng = Mathf.Clamp(Gear + shiftReq, 1, n);
                    if (ng != Gear) StartShift(ng);
                }
            }
            if (!DriveSettings.ManualGears && shiftTimer <= 0f && shiftCooldown <= 0f && Grounded)
            {
                float lim = spec.limiterRpm;
                float gr = GroundRpm(Gear);
                float upAt = Mathf.Lerp(lim * 0.58f, lim - 250f, driveCmd);
                float downAt = Mathf.Lerp(lim * 0.28f, lim * 0.5f, driveCmd);
                if (Gear < n && (gr > upAt || (limiterTime > 0.35f && gr > upAt * 0.72f)))
                    StartShift(Gear + 1);
                else if (Gear > 1 && (IsDrifting ? engRpm < downAt && engRpm * GearRatio(Gear - 1) / GearRatio(Gear) < lim * 0.85f
                                                  : gr < downAt && GroundRpm(Gear - 1) < lim - 700f))
                    StartShift(Gear - 1);
                else if (Gear > 1 && !IsDrifting && driveCmd > 0.9f && engRpm < spec.peakTorqueRpm * 0.6f && GroundRpm(Gear - 1) < lim * 0.82f)
                    StartShift(Gear - 1);      // kick-down
            }
            gearTotal = GearRatio(Gear);
        }

        void StartShift(int g)
        {
            bool upShift = g > Gear;
            Gear = g;
            shiftTimer = upShift ? spec.shiftTime : spec.shiftTime * 0.8f;
            shiftCooldown = shiftTimer + 0.45f;
            limiterTime = 0f;
        }

        void DriveTorques(float dt, float driveCmd)
        {
            gearTotal = GearRatio(Gear);
            float rpmW = DrivenRpm();
            float thr = driveCmd * tcsFactor;
            if (limiterCut > 0f) { limiterCut -= dt; thr = 0f; }
            float idle = spec.idleRpm;
            float torque;
            if (Shifting || hbClutch)
            {
                clutchLocked = false;
                float free = Shifting ? Mathf.Max(idle, rpmW) : Mathf.Lerp(idle, spec.limiterRpm * 0.85f, driveCmd);
                engRpm = Mathf.MoveTowards(engRpm, free, dt * 12000f);
                torque = 0f;
            }
            else
            {
                float slipTarget = idle + Mathf.Clamp01(driveCmd) * (spec.launchRpm - idle);
                // the auto clutch only slips to launch (1st gear / reverse at low revs)
                clutchLocked = driveCmd > 0.05f && (Gear == 1 || Reversing) ? rpmW >= slipTarget * 0.97f : rpmW > idle;
                if (clutchLocked)
                {
                    engRpm = rpmW;
                    torque = EngineTorque(engRpm, thr, powerMod);
                }
                else
                {
                    engRpm = Mathf.MoveTowards(engRpm, Mathf.Max(slipTarget, rpmW), dt * 9000f);
                    torque = Mathf.Max(0f, EngineTorque(engRpm, thr, powerMod));
                }
                if (engRpm >= spec.limiterRpm) { limiterCut = 0.055f; limiterTime += dt; }
                else if (engRpm < spec.limiterRpm - 300f) limiterTime = 0f;
            }
            for (int i = 0; i < wheels.Length; i++)
                tDrive[i] = wheels[i].driven ? torque * gearTotal * DriveEff * share[i] : 0f;
        }

        // limited-slip axles + AWD centre coupling (blend wheel speeds toward their mean)
        void Couple(bool centre)
        {
            int n = wheels.Length;
            float lsd = Mathf.Clamp01(spec.lsd);
            for (int i = 0; i < n; i++)
            {
                int p = partner[i];
                if (p <= i || !wheels[i].driven || !wheels[p].driven) continue;
                float axleLock = spec.drive == Drive.AWD && wheels[i].front ? lsd * 0.5f : lsd;
                float m = 0.5f * (wheels[i].angularVelocity + wheels[p].angularVelocity);
                wheels[i].angularVelocity = Mathf.Lerp(wheels[i].angularVelocity, m, axleLock * 0.5f);
                wheels[p].angularVelocity = Mathf.Lerp(wheels[p].angularVelocity, m, axleLock * 0.5f);
            }
            if (spec.drive == Drive.AWD && centre)
            {
                float f = 0f, r = 0f; int nf = 0, nr = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!wheels[i].driven) continue;
                    if (wheels[i].front) { f += wheels[i].angularVelocity; nf++; } else { r += wheels[i].angularVelocity; nr++; }
                }
                if (nf > 0 && nr > 0)
                {
                    f /= nf; r /= nr;
                    float d = (r - f) * 0.5f * Mathf.Clamp01(spec.centreLock) * 0.5f;
                    for (int i = 0; i < n; i++)
                    {
                        if (!wheels[i].driven) continue;
                        wheels[i].angularVelocity += wheels[i].front ? d : -d;
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------- drift state
        void UpdateDrift(float dt, float speed, float slip, int grounded, float hb)
        {
            bool groundOk = grounded >= 3;
            if (!IsDrifting)
            {
                if (groundOk && speed > 7f && ForwardSpeed > 3f && Mathf.Abs(slip) > 12f && Mathf.Abs(slip) < 100f)
                    BeginDrift(slip > 0f ? -1 : 1);
                else if (groundOk && hb > 0.5f && Mathf.Abs(Steer) > 0.3f && speed > 8f && ForwardSpeed > 3f)
                    BeginDrift(Steer > 0f ? 1 : -1);
            }
            else
            {
                // the slide swung the other way: the drift follows it (smooth left/right transitions)
                if (-slip * DriftDirection < -8f) DriftDirection = -DriftDirection;
            }
            DriftAngle = IsDrifting ? -slip * DriftDirection : 0f;
            if (IsDrifting)
            {
                driftTime += dt;
                driftAirTime = grounded == 0 ? driftAirTime + dt : 0f;
                bool calm = hb < 0.5f && driftTime > 0.4f && Mathf.Abs(slip) < 6f;
                driftExitTimer = calm ? driftExitTimer + dt : 0f;
                if (driftExitTimer > 0.25f || speed < 4.5f || ForwardSpeed < 0.5f || Mathf.Abs(slip) > 120f || driftAirTime > 0.8f)
                    IsDrifting = false;
                float rel = Steer * DriftDirection;
                float want = 6f + 22f * Throttle + 20f * Mathf.Max(rel, 0f) - 24f * Mathf.Max(-rel, 0f) + (hb > 0.5f ? 8f : 0f);
                DriftTargetAngle = Mathf.Clamp(want * spec.driftAngle, 0f, Mathf.Max(12f, angleLimit - 4f));
            }
            if (!IsDrifting) { DriftTargetAngle = 0f; DriftAngle = 0f; }
        }

        void BeginDrift(int dir)
        {
            IsDrifting = true;
            DriftDirection = dir;
            driftTime = 0f;
            driftExitTimer = 0f;
            driftAirTime = 0f;
        }
    }
}
