// CarCatalog.cs - the nine Blossom Rally cars and their handling.
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
        public float power = 7000f;          // peak drive force (N)
        public float maxSpeedKmh = 185f;
        public float grip = 1.05f;           // tyre friction coefficient
        public float driftGrip = 0.62f;      // rear grip under power slide (lower = more tail-happy)
        public float driftYaw = 2.2f;        // yaw assist while sliding (rad/s^2 at full lock)
        public float maxSteer = 34f;
        public float frontTorque = 0.4f;     // AWD split

        public CarSpec(string id, string name, string cls, string era, Drive d, float mass, float power, float vmax,
                       float grip, float driftGrip, float yaw)
        {
            this.id = id; this.name = name; carClass = cls; this.era = era; drive = d; this.mass = mass; this.power = power;
            maxSpeedKmh = vmax; this.grip = grip; this.driftGrip = driftGrip; driftYaw = yaw;
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
        public static readonly CarSpec[] Cars =
        {
            new CarSpec("CAR-009", "Brenner S1", "Group B", "1980s AWD coupe", Drive.AWD, 1180f, 8600f, 205f, 1.12f, 0.70f, 2.4f),
            new CarSpec("CAR-019", "Mistral W1", "Group B", "late-70s mid-engine wedge", Drive.RWD, 1150f, 8200f, 210f, 1.10f, 0.60f, 2.5f),
            new CarSpec("CAR-018", "Isar 3R", "Group B", "mid-80s wedge saloon", Drive.RWD, 1200f, 7800f, 200f, 1.08f, 0.62f, 2.4f),
            new CarSpec("CAR-017", "Falcon Mk2 RS", "Group 4", "late-70s square saloon", Drive.RWD, 1100f, 7100f, 188f, 1.04f, 0.58f, 2.5f),
            new CarSpec("CAR-006", "Torino 131", "Group 4", "late-70s four-door", Drive.RWD, 1180f, 7000f, 186f, 1.05f, 0.60f, 2.3f),
            new CarSpec("CAR-004", "Falcon Mk1 Rally", "Classic RWD", "early-70s saloon", Drive.RWD, 1000f, 6200f, 175f, 1.00f, 0.56f, 2.6f),
            new CarSpec("CAR-016", "Elster 2T", "Classic RWD", "late-60s saloon", Drive.RWD, 1050f, 6000f, 172f, 1.00f, 0.58f, 2.4f),
            new CarSpec("CAR-008", "Kestrel 9R", "Classic RWD", "70s rear-engine coupe", Drive.RWD, 1080f, 6800f, 185f, 1.03f, 0.57f, 2.5f),
            new CarSpec("CAR-002", "Sorrel A1", "Classic RWD", "60s-70s fastback", Drive.RWD, 860f, 5600f, 170f, 1.02f, 0.60f, 2.7f),
        };
    }
}
