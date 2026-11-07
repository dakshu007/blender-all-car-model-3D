// RallyTestDriveController.cs - minimal WheelCollider driving for Sprint 1 asset tests
// (does the imported car drive, steer and spin its wheels correctly?). Not the game's
// final handling model.
//
// Input: works with either the legacy Input Manager or the Input System package, without
// changing the project's input settings. Keys: W/S or arrows = throttle/brake-reverse,
// A/D or arrows = steer, Space = handbrake.
//
// Status: NOT yet compiled or tested in Unity. Review before use.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RallyGame.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleWheelVisuals))]
    public class RallyTestDriveController : MonoBehaviour
    {
        public VehicleDefinition definition;

        Rigidbody body;
        VehicleWheelVisuals visuals;
        float steer;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            visuals = GetComponent<VehicleWheelVisuals>();
            if (definition == null)
            {
                Debug.LogError($"{name}: assign a VehicleDefinition", this);
                enabled = false;
                return;
            }
            body.mass = definition.massKg;
            body.centerOfMass += definition.centerOfMassOffset;
            foreach (var w in visuals.wheels)
            {
                if (w?.collider == null) continue;
                var c = w.collider;
                c.suspensionDistance = definition.suspensionDistance;
                c.suspensionSpring = new JointSpring
                {
                    spring = definition.spring, damper = definition.damper, targetPosition = definition.targetPosition
                };
                SetGrip(w, 1f);
            }
        }

        void SetGrip(WheelBinding w, float extra)
        {
            bool front = w.slot.StartsWith("F");
            float g = (front ? definition.frontGrip : definition.rearGrip) * extra;
            var side = w.collider.sidewaysFriction; side.stiffness = g; w.collider.sidewaysFriction = side;
            var fwd = w.collider.forwardFriction; fwd.stiffness = g; w.collider.forwardFriction = fwd;
        }

        static float Axis(bool neg, bool pos) => (pos ? 1f : 0f) - (neg ? 1f : 0f);

        void ReadInput(out float throttle, out float steerIn, out bool handbrake)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null)
            {
                throttle = Axis(k.sKey.isPressed || k.downArrowKey.isPressed, k.wKey.isPressed || k.upArrowKey.isPressed);
                steerIn = Axis(k.aKey.isPressed || k.leftArrowKey.isPressed, k.dKey.isPressed || k.rightArrowKey.isPressed);
                handbrake = k.spaceKey.isPressed;
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            throttle = Input.GetAxis("Vertical");
            steerIn = Input.GetAxis("Horizontal");
            handbrake = Input.GetKey(KeyCode.Space);
#else
            throttle = 0f; steerIn = 0f; handbrake = false;
#endif
        }

        void FixedUpdate()
        {
            ReadInput(out float throttle, out float steerIn, out bool handbrake);
#if UNITY_6000_0_OR_NEWER
            Vector3 velocity = body.linearVelocity;
#else
            Vector3 velocity = body.velocity;
#endif
            float speedKmh = velocity.magnitude * 3.6f;
            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            float speedFactor = Mathf.Clamp01(speedKmh / definition.maxSpeedKmh);
            float maxSteer = definition.maxSteerAngle * (1f - definition.highSpeedSteerReduction * speedFactor);
            steer = Mathf.MoveTowards(steer, steerIn * maxSteer, definition.steerSpeed * maxSteer * Time.fixedDeltaTime);

            bool braking = throttle < 0f && forwardSpeed > 1f;
            int driven = 0;
            foreach (var w in visuals.wheels) if (w?.collider != null && IsDriven(w)) driven++;
            float torque = speedKmh < definition.maxSpeedKmh && !braking ? throttle * definition.maxMotorTorque : 0f;

            foreach (var w in visuals.wheels)
            {
                if (w?.collider == null) continue;
                bool front = w.slot.StartsWith("F");
                if (w.steer) w.collider.steerAngle = steer;
                float share = 0f;
                if (IsDriven(w) && driven > 0)
                {
                    share = definition.drivetrain == Drivetrain.AWD
                        ? (front ? definition.frontTorqueSplit : 1f - definition.frontTorqueSplit) / 2f
                        : 1f / driven;
                }
                w.collider.motorTorque = torque * share;
                float brake = braking ? -throttle * definition.maxBrakeTorque : 0f;
                if (handbrake && !front) brake = Mathf.Max(brake, definition.handbrakeTorque);
                w.collider.brakeTorque = brake;
                if (!front) SetGrip(w, handbrake ? definition.handbrakeRearGrip : 1f);
            }
        }

        bool IsDriven(WheelBinding w)
        {
            switch (definition.drivetrain)
            {
                case Drivetrain.FWD: return w.slot.StartsWith("F");
                case Drivetrain.RWD: return w.slot.StartsWith("R");
                default: return true;
            }
        }
    }
}
