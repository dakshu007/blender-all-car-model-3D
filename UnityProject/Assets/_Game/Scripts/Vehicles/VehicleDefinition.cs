// VehicleDefinition.cs - gameplay data for one car, kept separate from the visual model.
// Status: written for the rally project, NOT yet compiled or tested in Unity (no Unity
// installation was available when it was authored). Review before use.
using UnityEngine;

namespace RallyGame.Vehicles
{
    public enum VehicleClass { ClassicFWD, ClassicRWD, Group4, GroupB, ModernAWD }
    public enum Drivetrain { FWD, RWD, AWD }

    [CreateAssetMenu(menuName = "Rally/Vehicle Definition", fileName = "VehicleDefinition")]
    public class VehicleDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string vehicleId = "CAR-009";
        public string displayName = "Brenner S1";
        [TextArea] public string description;
        public VehicleClass vehicleClass = VehicleClass.GroupB;
        public GameObject prefab;
        public Sprite previewImage;

        [Header("Mass & drivetrain")]
        public float massKg = 1100f;
        public Drivetrain drivetrain = Drivetrain.AWD;
        [Range(0f, 1f)] public float frontTorqueSplit = 0.45f;   // AWD only
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.15f, 0.05f);

        [Header("Performance")]
        public float maxMotorTorque = 1600f;      // Nm at the wheels, summed
        public float maxSpeedKmh = 190f;
        public float maxBrakeTorque = 3000f;
        public float handbrakeTorque = 4500f;

        [Header("Steering")]
        public float maxSteerAngle = 32f;
        public float steerSpeed = 6f;             // response, higher = snappier
        [Range(0f, 1f)] public float highSpeedSteerReduction = 0.45f;

        [Header("Grip")]
        public float frontGrip = 1.0f;
        public float rearGrip = 0.95f;
        [Range(0.2f, 1f)] public float handbrakeRearGrip = 0.45f;

        [Header("Suspension")]
        public float suspensionDistance = 0.20f;
        public float spring = 38000f;
        public float damper = 4200f;
        [Range(0f, 1f)] public float targetPosition = 0.5f;

        [Header("Surface grip multipliers")]
        public float tarmacGrip = 1.0f;
        public float gravelGrip = 0.78f;
        public float snowGrip = 0.55f;

        [Header("Progression (later)")]
        public bool unlockedByDefault = true;
    }
}
