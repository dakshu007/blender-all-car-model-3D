// CarCatalog.cs - the nine Blossom Rally cars and their handling.
//
// The first constructor arguments (power, vmax, grip, driftGrip, yaw) are the garage display stats.
// The physics personality lives in the chained setters: engine (torque curve), gearbox, chassis (mass
// distribution, centre of gravity, inertia), tyres (axle grip), differentials, suspension and aero.
// VehicleDynamics reads all of it; see Scripts/Driving.
using UnityEngine;

namespace BlossomRally
{
    public enum Drive { RWD, FWD, AWD }

    [System.Serializable]
    public class CarSpec
    {
        public string id;
        public string name;
        public string carClass;
        public string era;
        public Drive drive;
        public float mass = 1150f;
        public float power = 7000f;          // display: power stat (garage bar uses power / mass)
        public float maxSpeedKmh = 185f;     // display: top speed (the gearing / drag give the real one)
        public float grip = 1.05f;           // tyre friction multiplier (also the garage grip stat)
        public float driftGrip = 0.62f;      // display: drift stat (lower = more tail-happy)
        public float driftYaw = 2.2f;        // legacy (unused by the physics)
        public float maxSteer = 34f;         // physical steering lock (deg)
        public float frontTorque = 0.4f;     // AWD torque split to the front axle

        // ---- engine
        public float peakTorque = 260f;      // Nm at the crank
        public float peakTorqueRpm = 5200f;
        public float idleRpm = 900f;
        public float limiterRpm = 7600f;
        public float torqueLow = 0.6f;       // fraction of peak torque at idle
        public float torqueHigh = 0.8f;      // fraction of peak torque at the limiter
        public float turbo = 0f;             // 0..1: boost lag (less torque low down, a kick once on boost)
        public float engineInertia = 0.16f;  // kg m^2
        public float engineBrake = 0.30f;    // off-throttle drag torque at the limiter (fraction of peak)
        public float launchRpm = 3800f;      // auto-clutch slip rpm at full throttle from a standstill
        // ---- gearbox
        public float[] gears = { 3.1f, 2.1f, 1.56f, 1.22f, 1.0f };
        public float finalDrive = 4.4f;
        public float reverseRatio = 3.4f;
        public float shiftTime = 0.14f;      // s of torque cut per shift
        // ---- chassis
        public float weightFront = 0.53f;    // static front axle load share
        public float cgHeight = 0.40f;       // m above the ground
        public float inertia = 1f;           // yaw / pitch inertia scale (lower = more agile)
        // ---- tyres / diffs
        public float frontGrip = 1f, rearGrip = 1f;
        public float lsd = 0.75f;            // driven axle lock (0 open .. 1 spool)
        public float centreLock = 0.45f;     // AWD centre coupling
        // ---- suspension
        public float rideComp = 0.08f;       // static compression (m): bigger = softer
        public float arbFront = 0.85f, arbRear = 0.55f;   // anti-roll bars (fraction of the spring rate)
        // ---- brakes
        public float brakeTorque = 1900f;    // per front wheel (Nm)
        public float brakeBias = 0.64f;      // front share
        public float handbrakeTorque = 2400f;// per rear wheel (Nm)
        // ---- aero
        public float drag = 0.42f;           // N per (m/s)^2
        public float downforce = 0.9f;       // N per (m/s)^2
        // ---- character
        public float driftAngle = 1f;        // scales the drift angle the inputs ask for

        public CarSpec(string id, string name, string cls, string era, Drive d, float mass, float power, float vmax,
                       float grip, float driftGrip, float yaw)
        {
            this.id = id; this.name = name; carClass = cls; this.era = era; drive = d; this.mass = mass; this.power = power;
            maxSpeedKmh = vmax; this.grip = grip; this.driftGrip = driftGrip; driftYaw = yaw;
        }

        public CarSpec Engine(float nm, float peakRpm, float limiter, float low, float high, float turbo = 0f, float launch = 3800f)
        {
            peakTorque = nm; peakTorqueRpm = peakRpm; limiterRpm = limiter; torqueLow = low; torqueHigh = high;
            this.turbo = turbo; launchRpm = launch;
            return this;
        }

        public CarSpec Gearbox(float final, float shift, params float[] ratios)
        {
            finalDrive = final; shiftTime = shift; gears = ratios;
            return this;
        }

        public CarSpec Chassis(float front, float cg, float inertiaScale, float steer = 34f)
        {
            weightFront = front; cgHeight = cg; inertia = inertiaScale; maxSteer = steer;
            return this;
        }

        public CarSpec Tyres(float front, float rear, float lsdLock, float centre = 0.45f, float split = 0.4f)
        {
            frontGrip = front; rearGrip = rear; lsd = lsdLock; centreLock = centre; frontTorque = split;
            return this;
        }

        public CarSpec Suspension(float comp, float arbF, float arbR)
        {
            rideComp = comp; arbFront = arbF; arbRear = arbR;
            return this;
        }

        public CarSpec Character(float drift, float dragCoef, float down)
        {
            driftAngle = drift; drag = dragCoef; downforce = down;
            return this;
        }

        /// <summary>Normalised engine torque (0..1) at an rpm, full throttle.</summary>
        public float TorqueCurve(float rpm)
        {
            if (rpm <= peakTorqueRpm)
            {
                float t = Mathf.Clamp01((rpm - idleRpm) / Mathf.Max(1f, peakTorqueRpm - idleRpm));
                float low = torqueLow;
                if (turbo > 0f) t = Mathf.Lerp(t, t * t * t * (3f - 2f * t) * 1.15f, turbo);   // boost comes in late
                return Mathf.Clamp(Mathf.Lerp(low, 1f, t * (2f - t)), 0f, 1.05f);
            }
            float u = Mathf.Clamp01((rpm - peakTorqueRpm) / Mathf.Max(1f, limiterRpm - peakTorqueRpm));
            return Mathf.Lerp(1f, torqueHigh, u * u);
        }
    }

    [System.Serializable]
    public class WheelData
    {
        public string slot;
        public string transform;
        public float[] unity_local_position;
        public float radius;
        public float width;
        public bool steer;
        public bool drive;
    }

    [System.Serializable]
    public class VehicleData
    {
        public string car_id;
        public string display_name;
        public WheelData[] wheel_list;
    }

    public static class CarCatalog
    {
        //          Engine(Nm, peak rpm, limiter, low, high, turbo, launch)   Gearbox(final, shift s, ratios...)
        //          Chassis(front share, cg height, inertia, lock)           Tyres(front, rear, lsd, centre, AWD split)
        //          Suspension(static comp, ARB front, ARB rear)             Character(drift angle, drag, downforce)
        public static readonly CarSpec[] Cars =
        {
            // Group B: AWD turbo monster - huge traction, pulls out of four-wheel drifts, a bit nose-heavy
            new CarSpec("CAR-009", "Brenner S1", "Group B", "1980s AWD coupe", Drive.AWD, 1180f, 8600f, 205f, 1.12f, 0.70f, 2.4f)
                .Engine(470f, 5400f, 7800f, 0.45f, 0.80f, 0.65f, 4600f).Gearbox(4.75f, 0.10f, 2.95f, 2.02f, 1.54f, 1.24f, 1.03f)
                .Chassis(0.56f, 0.40f, 1.05f, 33f).Tyres(1.0f, 1.0f, 0.60f, 0.40f, 0.33f)
                .Suspension(0.08f, 0.80f, 0.70f).Character(0.95f, 0.44f, 1.20f),
            // Group B: mid-engine RWD wedge - short, pointy, rotates on a whim
            new CarSpec("CAR-019", "Mistral W1", "Group B", "late-70s mid-engine wedge", Drive.RWD, 1150f, 8200f, 210f, 1.10f, 0.60f, 2.5f)
                .Engine(415f, 5800f, 8000f, 0.55f, 0.82f, 0.25f, 4200f).Gearbox(4.55f, 0.11f, 2.92f, 2.0f, 1.52f, 1.22f, 1.01f)
                .Chassis(0.44f, 0.38f, 0.88f, 34f).Tyres(1.0f, 1.03f, 0.80f)
                .Suspension(0.08f, 0.70f, 0.75f).Character(1.1f, 0.42f, 1.20f),
            // Group B: front-engine RWD wedge saloon - brutal but stable, long slides
            new CarSpec("CAR-018", "Isar 3R", "Group B", "mid-80s wedge saloon", Drive.RWD, 1200f, 7800f, 200f, 1.08f, 0.62f, 2.4f)
                .Engine(420f, 5200f, 7600f, 0.55f, 0.80f, 0.35f, 4000f).Gearbox(4.6f, 0.11f, 2.98f, 2.04f, 1.55f, 1.24f, 1.02f)
                .Chassis(0.52f, 0.40f, 1.05f, 34f).Tyres(1.0f, 1.0f, 0.78f)
                .Suspension(0.08f, 0.90f, 0.55f).Character(1.0f, 0.44f, 1.14f),
            // Group 4: the square-saloon drift king - balanced, high-revving, loves sideways
            new CarSpec("CAR-017", "Falcon Mk2 RS", "Group 4", "late-70s square saloon", Drive.RWD, 1100f, 7100f, 188f, 1.04f, 0.58f, 2.5f)
                .Engine(290f, 6200f, 8400f, 0.55f, 0.82f, 0f, 3800f).Gearbox(5.0f, 0.13f, 2.98f, 2.05f, 1.56f, 1.24f, 1.02f)
                .Chassis(0.53f, 0.41f, 0.95f, 35f).Tyres(1.0f, 0.99f, 0.85f)
                .Suspension(0.085f, 0.75f, 0.62f).Character(1.1f, 0.43f, 0.90f),
            // Group 4: four-door works car - planted, predictable, quick on gravel
            new CarSpec("CAR-006", "Torino 131", "Group 4", "late-70s four-door", Drive.RWD, 1180f, 7000f, 186f, 1.05f, 0.60f, 2.3f)
                .Engine(300f, 5600f, 7900f, 0.6f, 0.80f, 0f, 3700f).Gearbox(4.9f, 0.13f, 2.95f, 2.03f, 1.55f, 1.23f, 1.01f)
                .Chassis(0.54f, 0.41f, 1.05f, 34f).Tyres(1.02f, 1.02f, 0.75f)
                .Suspension(0.08f, 0.85f, 0.55f).Character(1.0f, 0.43f, 0.90f),
            // Classic: light early-70s saloon - nimble, easy to throw around
            new CarSpec("CAR-004", "Falcon Mk1 Rally", "Classic RWD", "early-70s saloon", Drive.RWD, 1000f, 6200f, 175f, 1.00f, 0.56f, 2.6f)
                .Engine(225f, 6000f, 8200f, 0.58f, 0.82f, 0f, 3600f).Gearbox(5.1f, 0.15f, 3.0f, 2.06f, 1.55f, 1.22f, 1.0f)
                .Chassis(0.53f, 0.42f, 0.93f, 35f).Tyres(1.0f, 0.99f, 0.82f)
                .Suspension(0.09f, 0.75f, 0.62f).Character(1.05f, 0.42f, 0.75f),
            // Classic: late-60s saloon - torquey, forgiving, a gentle first car
            new CarSpec("CAR-016", "Elster 2T", "Classic RWD", "late-60s saloon", Drive.RWD, 1050f, 6000f, 172f, 1.00f, 0.58f, 2.4f)
                .Engine(235f, 4800f, 7200f, 0.68f, 0.78f, 0f, 3300f).Gearbox(4.6f, 0.15f, 3.05f, 2.08f, 1.56f, 1.22f, 1.0f)
                .Chassis(0.54f, 0.42f, 1.0f, 34f).Tyres(1.02f, 1.01f, 0.72f)
                .Suspension(0.09f, 0.85f, 0.55f).Character(0.95f, 0.42f, 0.75f),
            // Classic: rear-engine coupe - huge traction, pendulum rotation on lift-off
            new CarSpec("CAR-008", "Kestrel 9R", "Classic RWD", "70s rear-engine coupe", Drive.RWD, 1080f, 6800f, 185f, 1.03f, 0.57f, 2.5f)
                .Engine(250f, 5600f, 7800f, 0.6f, 0.80f, 0f, 3800f).Gearbox(4.6f, 0.14f, 3.0f, 2.05f, 1.55f, 1.22f, 1.0f)
                .Chassis(0.41f, 0.40f, 1.0f, 33f).Tyres(1.0f, 1.04f, 0.75f)
                .Suspension(0.085f, 0.95f, 0.45f).Character(1.05f, 0.42f, 0.90f),
            // Classic: tiny light fastback - tail-happy and playful, carries speed through everything
            new CarSpec("CAR-002", "Sorrel A1", "Classic RWD", "60s-70s fastback", Drive.RWD, 860f, 5600f, 170f, 1.02f, 0.60f, 2.7f)
                .Engine(178f, 6000f, 8200f, 0.6f, 0.82f, 0f, 3700f).Gearbox(5.2f, 0.14f, 3.0f, 2.05f, 1.55f, 1.22f, 1.0f)
                .Chassis(0.45f, 0.39f, 0.85f, 36f).Tyres(1.0f, 0.98f, 0.85f)
                .Suspension(0.085f, 0.65f, 0.78f).Character(1.2f, 0.38f, 0.75f),
        };
    }
}
