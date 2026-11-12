// CarCatalog.cs - the Blossom Rally cars (the original nine + twelve for the six-region world) and their handling.
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
        // ---- off-road / per-car extras (the defaults keep the original nine cars exactly as they were)
        public float susTravel = VehicleDynamics.SusLen;   // bump travel above the rest position (m)
        public float damping = 1f;           // damper rate scale
        public float gripTarmac = 1f, gripLoose = 1f, gripSnow = 1f, gripSand = 1f;   // tyre compound per surface
        public float looseDrag = 1f;         // scales the ploughing drag on gravel / dirt / grass / snow / sand
        public string soundClass;            // engine sound family (null = derived from carClass)

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

        /// <summary>Long-travel suspension and the tyre compound: travel (m), damper scale, grip multiplier per
        /// surface group (asphalt + paving, gravel + dirt + grass, snow + ice, sand) and loose-surface drag.</summary>
        public CarSpec OffRoad(float travel, float damper, float tarmac, float loose, float snow, float sand, float ploughDrag = 1f)
        {
            susTravel = travel; damping = damper; gripTarmac = tarmac; gripLoose = loose; gripSnow = snow; gripSand = sand;
            looseDrag = ploughDrag;
            return this;
        }

        public CarSpec Sound(string family) { soundClass = family; return this; }

        public CarSpec Brakes(float front, float handbrake)
        {
            brakeTorque = front; handbrakeTorque = handbrake;
            return this;
        }

        /// <summary>Per-car tyre compound multiplier for a surface (1 for the original cars).</summary>
        public float SurfaceGrip(Surface s)
        {
            switch (s)
            {
                case Surface.Asphalt: case Surface.Paving: return gripTarmac;
                case Surface.Snow: case Surface.Ice: return gripSnow;
                case Surface.Sand: return gripSand;
                default: return gripLoose;
            }
        }

        /// <summary>Engine sound family: "Group B", "Group 4", "Classic RWD" or "Electric".</summary>
        public string SoundClass { get { return !string.IsNullOrEmpty(soundClass) ? soundClass : CarCatalog.SoundFor(carClass); } }

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
        public float[] driver_eye;   // optional (CAR-020+): driver's eye point, car space
        public float[] hood_cam;     // optional (CAR-020+): bonnet camera point, car space
    }

    public static class CarCatalog
    {
        /// <summary>Maps a car class to one of the engine sound families EngineAudio synthesises.</summary>
        public static string SoundFor(string cls)
        {
            switch (cls)
            {
                case "Group B": case "Group 4": case "Classic RWD": return cls;
                case "Electric GT": return "Electric";
                case "Rally Raid": case "Trophy Truck": case "Dune Buggy": case "Modern Rally": case "Snow Rally": return "Group B";
                case "Trail 4x4": case "Overland": case "Retro 4x4": case "Luxury SUV": return "Classic RWD";
                default: return "Group 4";   // Coupe SUV, Super Saloon and anything new
            }
        }

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

            // ===================== the six-region cars (CAR-020..031) =====================
            //          OffRoad(travel m, damper, grip: tarmac, loose, snow+ice, sand, ploughing drag)   Brakes(front Nm, handbrake Nm)
            // Mountains hero: compact two-door 4x4 - diesel-like low-down shove, long travel, all-terrain tyres
            new CarSpec("CAR-020", "Talus Crag", "Trail 4x4", "compact two-door 4x4", Drive.AWD, 1580f, 10200f, 168f, 1.06f, 0.66f, 2.3f)
                .Engine(410f, 3400f, 6200f, 0.82f, 0.70f, 0.15f, 2600f).Gearbox(5.55f, 0.16f, 3.6f, 2.3f, 1.6f, 1.22f, 1.0f)
                .Chassis(0.52f, 0.56f, 1.05f, 36f).Tyres(1.0f, 1.0f, 0.70f, 0.65f, 0.45f)
                .Suspension(0.12f, 1.05f, 0.85f).Character(0.9f, 0.56f, 0.6f)
                .OffRoad(0.34f, 1.15f, 0.94f, 1.12f, 1.10f, 1.15f, 0.7f).Brakes(3600f, 3800f),
            // City / mountain: big luxury SUV - twin-turbo V8 torque, heavy and serene, planted everywhere
            new CarSpec("CAR-021", "Halvard Citadel", "Luxury SUV", "full-size luxury SUV", Drive.AWD, 2350f, 14800f, 205f, 1.04f, 0.70f, 2.0f)
                .Engine(640f, 3600f, 6600f, 0.80f, 0.78f, 0.35f, 3000f).Gearbox(5.0f, 0.12f, 3.4f, 2.2f, 1.55f, 1.2f, 1.0f)
                .Chassis(0.53f, 0.58f, 1.25f, 34f).Tyres(1.0f, 1.0f, 0.60f, 0.55f, 0.42f)
                .Suspension(0.11f, 1.15f, 0.95f).Character(0.85f, 0.62f, 0.7f)
                .OffRoad(0.30f, 1.1f, 1.0f, 1.04f, 1.02f, 1.04f, 0.85f).Brakes(4600f, 4400f),
            // City: sleek coupe-SUV - quick on tarmac, still happy on a gravel road
            new CarSpec("CAR-022", "Velina Arc", "Coupe SUV", "modern coupe-SUV", Drive.AWD, 1720f, 12600f, 225f, 1.08f, 0.66f, 2.3f)
                .Engine(560f, 4200f, 7000f, 0.70f, 0.80f, 0.35f, 3600f).Gearbox(4.5f, 0.10f, 3.2f, 2.1f, 1.55f, 1.22f, 1.0f)
                .Chassis(0.54f, 0.50f, 1.1f, 34f).Tyres(1.0f, 1.0f, 0.60f, 0.50f, 0.40f)
                .Suspension(0.09f, 1.0f, 0.8f).Character(0.95f, 0.50f, 1.0f)
                .OffRoad(0.26f, 1.05f, 1.05f, 0.98f, 0.94f, 0.92f, 0.95f).Brakes(3800f, 3600f),
            // Desert: rally-raid crew-cab pickup - long travel, floats over sand and ruts
            new CarSpec("CAR-023", "Sabka Dunecrew", "Rally Raid", "rally-raid pickup", Drive.AWD, 1850f, 12400f, 185f, 1.08f, 0.64f, 2.3f)
                .Engine(560f, 3800f, 6200f, 0.78f, 0.74f, 0.30f, 3000f).Gearbox(5.3f, 0.13f, 3.5f, 2.25f, 1.6f, 1.22f, 1.0f)
                .Chassis(0.50f, 0.60f, 1.2f, 35f).Tyres(1.0f, 1.0f, 0.70f, 0.60f, 0.45f)
                .Suspension(0.14f, 1.05f, 0.85f).Character(0.95f, 0.60f, 0.6f)
                .OffRoad(0.40f, 1.2f, 0.92f, 1.14f, 1.08f, 1.25f, 0.6f).Brakes(4200f, 4200f),
            // Desert hero: trophy truck - big V8, huge travel, wide track, flies over dunes and lands soft
            new CarSpec("CAR-024", "Simoon T1", "Trophy Truck", "desert trophy truck", Drive.AWD, 1650f, 13600f, 200f, 1.10f, 0.60f, 2.4f)
                .Engine(640f, 4600f, 7000f, 0.72f, 0.80f, 0f, 3600f).Gearbox(5.9f, 0.12f, 3.3f, 2.15f, 1.55f, 1.2f, 1.0f)
                .Chassis(0.48f, 0.58f, 1.15f, 36f).Tyres(1.0f, 1.02f, 0.75f, 0.50f, 0.30f)
                .Suspension(0.17f, 0.95f, 0.75f).Character(1.05f, 0.58f, 0.5f)
                .OffRoad(0.46f, 1.3f, 0.90f, 1.15f, 1.02f, 1.32f, 0.55f).Brakes(3900f, 3800f),
            // Desert / beach: dune buggy - featherweight, paddle tyres, tail-happy, long-travel arms
            new CarSpec("CAR-025", "Skink DB", "Dune Buggy", "open-frame dune buggy", Drive.RWD, 780f, 6200f, 165f, 1.04f, 0.56f, 2.7f)
                .Engine(255f, 5200f, 7600f, 0.68f, 0.80f, 0f, 3600f).Gearbox(7.3f, 0.12f, 3.2f, 2.1f, 1.55f, 1.2f, 1.0f)
                .Chassis(0.40f, 0.46f, 0.85f, 38f).Tyres(1.0f, 1.04f, 0.85f)
                .Suspension(0.15f, 0.75f, 0.60f).Character(1.15f, 0.40f, 0.4f)
                .OffRoad(0.42f, 1.2f, 0.85f, 1.10f, 1.0f, 1.40f, 0.5f).Brakes(2000f, 2300f),
            // Snow hero: studded-tyre rally 4x4 - the best grip there is on snow and ice
            new CarSpec("CAR-026", "Ermine S4", "Snow Rally", "raised snow rally 4x4", Drive.AWD, 1380f, 9600f, 190f, 1.08f, 0.68f, 2.4f)
                .Engine(430f, 4400f, 7200f, 0.62f, 0.80f, 0.55f, 4200f).Gearbox(5.0f, 0.10f, 3.1f, 2.1f, 1.58f, 1.25f, 1.03f)
                .Chassis(0.55f, 0.48f, 1.0f, 34f).Tyres(1.0f, 1.0f, 0.65f, 0.50f, 0.40f)
                .Suspension(0.10f, 0.95f, 0.75f).Character(1.0f, 0.46f, 0.9f)
                .OffRoad(0.28f, 1.05f, 0.90f, 1.06f, 1.40f, 0.95f, 0.8f).Brakes(2600f, 2800f),
            // Snow / mountains: expedition wagon - low gearing and torque that winches it up anything
            new CarSpec("CAR-027", "Muskeg Expedition", "Overland", "expedition overland wagon", Drive.AWD, 2250f, 13000f, 160f, 1.05f, 0.72f, 2.0f)
                .Engine(600f, 2800f, 5200f, 0.88f, 0.70f, 0.25f, 2400f).Gearbox(5.0f, 0.16f, 4.0f, 2.5f, 1.65f, 1.22f, 1.0f)
                .Chassis(0.52f, 0.62f, 1.3f, 34f).Tyres(1.0f, 1.0f, 0.75f, 0.75f, 0.48f)
                .Suspension(0.13f, 1.15f, 0.95f).Character(0.8f, 0.70f, 0.5f)
                .OffRoad(0.36f, 1.2f, 0.94f, 1.12f, 1.14f, 1.15f, 0.7f).Brakes(4600f, 4600f),
            // City / all-round: modern top-class rally hatch - turbo, wide arches, big wing, sharp on every surface
            new CarSpec("CAR-028", "Vireo Apex", "Modern Rally", "modern rally hatchback", Drive.AWD, 1230f, 9300f, 200f, 1.12f, 0.66f, 2.4f)
                .Engine(450f, 4800f, 7600f, 0.55f, 0.82f, 0.50f, 4600f).Gearbox(4.5f, 0.07f, 2.9f, 2.0f, 1.55f, 1.25f, 1.05f)
                .Chassis(0.56f, 0.40f, 0.95f, 35f).Tyres(1.0f, 1.0f, 0.65f, 0.45f, 0.40f)
                .Suspension(0.09f, 0.85f, 0.70f).Character(1.0f, 0.44f, 1.3f)
                .OffRoad(0.26f, 1.05f, 1.02f, 1.08f, 1.05f, 1.0f, 0.9f).Brakes(2300f, 2600f),
            // City hero: four-door super saloon - rear-biased AWD, six gears, fastest car on asphalt
            new CarSpec("CAR-029", "Corvane Nocturne", "Super Saloon", "four-door super saloon", Drive.AWD, 1650f, 12800f, 245f, 1.12f, 0.62f, 2.4f)
                .Engine(600f, 5000f, 7400f, 0.65f, 0.82f, 0.45f, 4200f).Gearbox(4.6f, 0.08f, 3.0f, 2.0f, 1.5f, 1.2f, 1.0f, 0.84f)
                .Chassis(0.53f, 0.42f, 1.1f, 33f).Tyres(1.0f, 1.0f, 0.75f, 0.40f, 0.32f)
                .Suspension(0.075f, 0.95f, 0.80f).Character(1.0f, 0.40f, 1.4f)
                .OffRoad(0.22f, 1.0f, 1.08f, 0.95f, 0.85f, 0.85f, 1.0f).Brakes(3600f, 3200f),
            // City night: electric GT - instant flat torque, two-speed box, silent whine
            new CarSpec("CAR-030", "Lumen Arclight", "Electric GT", "electric GT coupe", Drive.AWD, 1780f, 14200f, 250f, 1.12f, 0.64f, 2.3f)
                .Engine(700f, 5000f, 13000f, 1.0f, 0.45f, 0f, 1800f).Gearbox(6.85f, 0.05f, 1.75f, 1.0f)
                .Chassis(0.50f, 0.38f, 1.05f, 33f).Tyres(1.0f, 1.0f, 0.70f, 0.60f, 0.42f)
                .Suspension(0.075f, 0.95f, 0.85f).Character(0.95f, 0.36f, 1.5f)
                .OffRoad(0.22f, 1.0f, 1.10f, 0.92f, 0.85f, 0.80f, 1.0f).Brakes(3800f, 3400f),
            // Retro mountain climber: 1980s two-tone 4x4 wagon - lazy big-bore torque, soft and sure-footed
            new CarSpec("CAR-031", "Bramble Wagon 84", "Retro 4x4", "1980s 4x4 wagon", Drive.AWD, 1700f, 9800f, 160f, 1.04f, 0.70f, 2.2f)
                .Engine(420f, 3000f, 5400f, 0.85f, 0.72f, 0f, 2400f).Gearbox(4.95f, 0.18f, 3.8f, 2.4f, 1.6f, 1.2f, 1.0f)
                .Chassis(0.52f, 0.58f, 1.15f, 35f).Tyres(1.0f, 1.0f, 0.70f, 0.70f, 0.45f)
                .Suspension(0.13f, 1.05f, 0.85f).Character(0.9f, 0.62f, 0.5f)
                .OffRoad(0.34f, 1.15f, 0.95f, 1.10f, 1.10f, 1.08f, 0.75f).Brakes(3800f, 3800f),
        };
    }
}
